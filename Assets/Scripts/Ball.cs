using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Ball : MonoBehaviour
{
    public enum SpecialType { None, Gold, Hot, Vault, Wild, Glass, Money }

    // Raised once per ball-on-ball collision, with the impact speed
    public static event Action<float> BallsCollided;

    public bool striped; // True if the ball is striped, false if solid
    public int number; // 0 or below = a cue ball, 1-15 = object balls

    [Header("Physics")]
    [SerializeField] float linearDamping = 0.8f; // felt friction
    [SerializeField] float bounciness = 0.9f;
    [SerializeField] float stopSpeed = 0.075f; // below this the ball snaps to rest
    [SerializeField] float sinkTime = 0.25f;

    [Header("Scoring")]
    [SerializeField] float multiplierGain = 0.1f; // added to the multiplier on each ball-on-ball collision
    [SerializeField] float minCollisionSpeed = 0.1f; // gentler contacts (resting jitter) don't count

    [Header("Multiplier label")]
    [SerializeField] TMP_FontAsset labelFont; // optional, leave empty for the default TMP font
    [SerializeField] float labelFontSize = 3f;
    [SerializeField] float labelGap = 0.1f; // space between the top of the ball and the bottom of the label (world units)

    [Header("Special ball sprites")]
    [SerializeField] Sprite[] specialSprites; // named "gold", "hot", "vault", "wild", "glass", "money"

    [Header("Glass")]
    [SerializeField] int glassHitsToShatter = 3;

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

    // Which special type this ball is, if any. Set once at spawn (or by a milestone forcing one on)
    public SpecialType Special { get; private set; } = SpecialType.None;

    // The actual money this ball pays out if pocketed right now. Special types change this from the
    // plain Value x Multiplier: Vault needs a long enough chain, Money needs the round nearly cleared,
    // Wild adds a random bonus on top.
    public int Payout
    {
        get
        {
            float effectiveValue = baseValue;

            if (Special == SpecialType.Vault)
            {
                int required = (Upgrades.Instance != null && Upgrades.Instance.HasVaultMastery) ? 2 : 3;
                if (gameEngine == null || gameEngine.ChainLengthNow < required) effectiveValue = 0f;
            }
            else if (Special == SpecialType.Money)
            {
                bool unlocked = gameEngine != null && gameEngine.AllOtherBallsPocketed(this);
                if (!unlocked)
                    effectiveValue = (Upgrades.Instance != null && Upgrades.Instance.HasMoneyMastery) ? baseValue * 0.3f : 0f;
            }

            float payout = effectiveValue * multiplier;

            if (Special == SpecialType.Wild)
            {
                float ceiling = (Upgrades.Instance != null && Upgrades.Instance.HasWildMastery) ? 8f : 5f;
                payout *= UnityEngine.Random.Range(0f, ceiling);
            }

            return Mathf.RoundToInt(payout);
        }
    }

    // Read by GameEngine to predict where a moving ball will go
    public float Bounciness => bounciness;
    public float LinearDamping => linearDamping;
    public float StopSpeed => stopSpeed;

    // A second cue ball (from the Extra Cue Ball upgrade) also counts as a cue ball: never pays out,
    // scratches the same way, and is spawned with number <= 0 too (see GameEngine.EnableExtraCueBall)
    public bool IsCueBall => number <= 0;
    public bool IsPocketed { get; private set; }

    // Radius in world units
    public float WorldRadius => Collider.radius * transform.lossyScale.x;

    // True once the ball is pocketed (and finished sinking) or has come to rest
    public bool IsSettled => !sinking && (IsPocketed || Rb.linearVelocity.sqrMagnitude <= stopSpeed * stopSpeed);

    bool sinking;
    int baseValue;
    float multiplier = 1f;
    int glassHits;
    bool shattered;
    Vector3 startScale;
    SpriteRenderer sr;
    TextMeshPro label;
    GameEngine gameEngine;

    void Awake()
    {
        startScale = transform.localScale;

        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 11; // above the table, below the value label

        Rb = GetComponent<Rigidbody2D>();
        if (Rb == null) Rb = gameObject.AddComponent<Rigidbody2D>();
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

        gameEngine = FindFirstObjectByType<GameEngine>();

        CreateLabel();
    }

    public void Init(int ballNumber, Sprite sprite, int value, SpecialType type = SpecialType.None)
    {
        number = ballNumber;
        Value = value;
        striped = ballNumber > 8;
        if (sprite != null)
        {
            sr.sprite = sprite;
            Collider.radius = sprite.bounds.extents.x;
        }
        name = IsCueBall ? "CueBall" : "Ball " + ballNumber;

        ApplySpecialType(type);
    }

    // Applies (or re-applies) a special type's effect: a value multiplier, a bigger multiplier gain,
    // a swapped sprite, or just a flag that Payout/OnCollisionEnter2D checks later
    public void ApplySpecialType(SpecialType type)
    {
        Special = type;

        switch (Special)
        {
            case SpecialType.Gold:
                float goldMult = (Upgrades.Instance != null && Upgrades.Instance.HasGoldMastery) ? 4f : 3f;
                Value = Mathf.RoundToInt(baseValue * goldMult);
                break;
            case SpecialType.Hot:
                float hotMult = (Upgrades.Instance != null && Upgrades.Instance.HasHotMastery) ? 3f : 2f;
                multiplierGain *= hotMult;
                break;
            case SpecialType.Glass:
                glassHits = 0;
                shattered = false;
                break;
        }

        Sprite special = FindSpecialSprite(Special);
        if (special != null) sr.sprite = special;

        UpdateLabel();
    }

    Sprite FindSpecialSprite(SpecialType type)
    {
        if (type == SpecialType.None) return null;

        string expected = type.ToString();
        foreach (Sprite s in specialSprites)
            if (s != null && string.Equals(s.name, expected, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    // Set the ball moving at the given velocity (units per second)
    public void Hit(Vector2 velocity)
    {
        Rb.linearVelocity = velocity;
    }

    // Adds a small multiplier for every shot this ball survives on the table (Slow Burn milestone)
    public void TickSlowBurn(float amount)
    {
        if (IsPocketed) return;
        Multiplier += amount;
    }

    // Flat M bonus from outside a collision (Head Start, Run the Table, Long Chain)
    public void AddMultiplier(float amount)
    {
        if (IsPocketed || amount <= 0f) return;
        Multiplier += amount;
    }

    // Sets how much felt friction slows this ball, e.g. from the Smooth Felt upgrade
    public void SetLinearDamping(float value)
    {
        linearDamping = Mathf.Max(0.05f, value);
        if (Rb != null) Rb.linearDamping = linearDamping;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.TryGetComponent(out Ball other)) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        bool counts = impactSpeed >= minCollisionSpeed;

        // Registered first so this ball already has its chain position when its gain is worked out
        gameEngine?.RegisterHit(this, other);

        if (counts)
        {
            float gain = multiplierGain;
            if (gameEngine != null && gameEngine.BothCueBallsContributed) gain *= 2f;
            if (gameEngine != null && gameEngine.IsFirstShotOfTurn && Upgrades.Instance != null && Upgrades.Instance.HasHotHand) gain *= 3f;
            if (gameEngine != null && Upgrades.Instance != null) gain *= Upgrades.Instance.CrowdPleaserFactor(gameEngine.ChainPosition(this));
            Multiplier += gain;
        }

        // ...but only the lower ID reports the collision
        if (GetInstanceID() < other.GetInstanceID()) BallsCollided?.Invoke(impactSpeed);

        if (counts && Special == SpecialType.Glass && !shattered)
        {
            glassHits++;
            int threshold = (Upgrades.Instance != null && Upgrades.Instance.HasGlassMastery) ? 2 : glassHitsToShatter;
            if (glassHits >= threshold)
            {
                shattered = true;
                gameEngine?.PayGlass(this);
            }
        }
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
}
