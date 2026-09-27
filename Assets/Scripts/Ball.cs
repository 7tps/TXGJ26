using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Ball : MonoBehaviour
{
    // Raised once per ball-on-ball collision, with the impact speed
    public static event Action<float> BallsCollided;

    public bool striped; // True if the ball is striped, false if solid
    public int number; // 0 = cue ball, 1-15 = object balls

    [Header("Physics")]
    [SerializeField] float linearDamping = 0.8f; // felt friction
    [SerializeField] float bounciness = 0.9f;
    [SerializeField] float stopSpeed = 0.075f; // below this the ball snaps to rest
    [SerializeField] float sinkTime = 0.25f;

    [Header("Friction upgrade")]
    [SerializeField] float dampingPerLevel = 0.1f; // subtracted from linearDamping per upgrade level

    float baseLinearDamping; // linearDamping at level 0, captured before any upgrade is applied

    [Header("Scoring")]
    [SerializeField] float multiplierGain = 0.1f; // added to the multiplier on each ball-on-ball collision
    [SerializeField] float minCollisionSpeed = 0.1f; // gentler contacts (resting jitter) don't count

    [Header("Multiplier label")]
    [SerializeField] TMP_FontAsset labelFont; // optional, leave empty for the default TMP font
    [SerializeField] float labelFontSize = 3f;
    [SerializeField] float labelGap = 0.1f; // space between the top of the ball and the bottom of the label (world units)

    public Rigidbody2D Rb { get; private set; }
    public CircleCollider2D Collider { get; private set; }

    // Money paid out when the ball is pocketed, before the multiplier
    public int Value
    {
        get => baseValue;
        set
        {
            baseValue = value;
            UpdateLabel();
        }
    }

    // Starts at 1x and goes up every time this ball collides with another ball
    public float Multiplier
    {
        get => multiplier;
        private set
        {
            multiplier = value;
            UpdateLabel();
        }
    }

    public int Payout => Mathf.RoundToInt(Value * Multiplier);

    // Read by GameEngine to predict where a moving ball will go
    public float Bounciness => bounciness;
    public float LinearDamping => linearDamping;
    public float StopSpeed => stopSpeed;

    public bool IsCueBall => number == 0;
    public bool IsPocketed { get; private set; }

    // Radius in world units
    public float WorldRadius => Collider.radius * transform.lossyScale.x;

    // True once the ball is pocketed (and finished sinking) or has come to rest
    public bool IsSettled => !sinking && (IsPocketed || Rb.linearVelocity.sqrMagnitude <= stopSpeed * stopSpeed);

    bool sinking;
    int baseValue;
    float multiplier = 1f;
    Vector3 startScale;
    SpriteRenderer sr;
    TextMeshPro label;

    void Awake()
    {
        startScale = transform.localScale;

        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 11; // above the table, below the value label

        Rb = GetComponent<Rigidbody2D>();
        if (Rb == null) Rb = gameObject.AddComponent<Rigidbody2D>();
        baseLinearDamping = linearDamping;

        Rb.bodyType = RigidbodyType2D.Dynamic;
        Rb.gravityScale = 0f; // top-down view
        Rb.linearDamping = linearDamping;
        Rb.freezeRotation = false; // no sprite spinning
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Collider = GetComponent<CircleCollider2D>();
        if (Collider == null) Collider = gameObject.AddComponent<CircleCollider2D>();
        Collider.radius = sr.sprite != null ? sr.sprite.bounds.extents.x : Collider.radius;
        Collider.sharedMaterial = new PhysicsMaterial2D { bounciness = bounciness, friction = 0f };

        CreateLabel();
    }

    public void Init(int ballNumber, Sprite sprite, int value)
    {
        number = ballNumber;
        Value = value;
        UpdateLabel();
        striped = ballNumber > 8;
        if (sprite != null)
        {
            sr.sprite = sprite;
            Collider.radius = sprite.bounds.extents.x;
        }
        name = IsCueBall ? "CueBall" : "Ball " + ballNumber;
    }

    // Set the ball moving at the given velocity (units per second)
    public void Hit(Vector2 velocity)
    {
        Rb.linearVelocity = velocity;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.TryGetComponent(out Ball other)) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        // Both balls get this callback, so each one raises its own multiplier
        if (impactSpeed >= minCollisionSpeed) Multiplier += multiplierGain;

        // ...but only the lower ID reports the collision
        if (GetInstanceID() < other.GetInstanceID()) BallsCollided?.Invoke(impactSpeed);
    }

    // A world-space TextMeshPro that floats above the ball showing its base value with the multiplier under it
    void CreateLabel()
    {
        GameObject go = new GameObject("Value Label");
        go.transform.SetParent(transform, false);

        label = go.AddComponent<TextMeshPro>();
        if (labelFont != null) label.font = labelFont;
        label.fontSize = labelFontSize;
        label.alignment = TextAlignmentOptions.Bottom;
        label.rectTransform.pivot = new Vector2(0.5f, 0f); // anchored by its bottom edge, so the value line sits above the multiplier
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;
        label.sortingOrder = 12; // above the ball, below the aim guide
        label.rectTransform.sizeDelta = new Vector2(5f, 1f); // wide enough that it never wraps

        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (label == null) return;

        label.gameObject.SetActive(number > 0); // the cue ball doesn't pay out, so it gets no label
        label.text = $"${baseValue}\n<color=#FFE066>{multiplier:0.0#}x</color>";
    }

    // Keeps the label upright and above the ball even if the ball rolls or spins
    void LateUpdate()
    {
        if (label == null) return;

        label.transform.rotation = Quaternion.identity;
        label.transform.position = transform.position + Vector3.up * (WorldRadius + labelGap);
    }

    void FixedUpdate()
    {
        if (IsPocketed) return;

        // kill tiny velocities
        if (Rb.linearVelocity.sqrMagnitude < stopSpeed * stopSpeed)
            Rb.linearVelocity = Vector2.zero;
    }

    // Drops the ball into the pocket at pocketPos and hides it
    public void Pocket(Vector2 pocketPos)
    {
        if (IsPocketed) return;

        IsPocketed = true;
        Rb.linearVelocity = Vector2.zero;
        Rb.simulated = false; // no more collisions
        StartCoroutine(Sink(pocketPos));
    }

    IEnumerator Sink(Vector2 target)
    {
        sinking = true;
        Vector3 from = transform.position;
        Vector3 to = new Vector3(target.x, target.y, from.z);

        for (float t = 0f; t < sinkTime; t += Time.deltaTime)
        {
            float k = t / sinkTime;
            transform.position = Vector3.Lerp(from, to, k);
            transform.localScale = startScale * (1f - k);
            yield return null;
        }

        sinking = false;
        gameObject.SetActive(false);
    }

    // Puts a pocketed ball back on the table
    public void Respawn(Vector2 position)
    {
        StopAllCoroutines();
        sinking = false;
        IsPocketed = false;

        gameObject.SetActive(true);
        transform.localScale = startScale;
        transform.position = position;
        Rb.position = position;
        Rb.linearVelocity = Vector2.zero;
        Rb.simulated = true;
    }

    // Sets linearDamping from the upgrade level directly, rather than nudging it down each call - so
    // calling this again with the same level (e.g. every time the gameplay scene reloads) is always
    // correct, instead of compounding. Pushes the change to the Rigidbody2D too: setting the field
    // alone never reached the physics engine, since Awake() only copies it across once.
    public void ApplyFrictionLevel(int level)
    {
        linearDamping = Mathf.Max(0f, baseLinearDamping - level * dampingPerLevel);
        Rb.linearDamping = linearDamping;
    }
}
