using System.Collections;
using UnityEngine;

public class Ball : MonoBehaviour
{
    public bool striped; // True if the ball is striped, false if solid
    public int number; // 0 = cue ball, 1-15 = object balls

    [Header("Physics")]
    [SerializeField] float linearDamping = 0.8f; // felt friction
    [SerializeField] float bounciness = 0.9f;
    [SerializeField] float stopSpeed = 0.075f; // below this the ball snaps to rest
    [SerializeField] float sinkTime = 0.25f;

    public Rigidbody2D Rb { get; private set; }
    public CircleCollider2D Collider { get; private set; }

    public bool IsCueBall => number == 0;
    public bool IsPocketed { get; private set; }

    // Radius in world units
    public float WorldRadius => Collider.radius * transform.lossyScale.x;

    // True once the ball is pocketed (and finished sinking) or has come to rest
    public bool IsSettled => !sinking && (IsPocketed || Rb.linearVelocity.sqrMagnitude <= stopSpeed * stopSpeed);

    bool sinking;
    Vector3 startScale;
    SpriteRenderer sr;

    void Awake()
    {
        startScale = transform.localScale;

        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 10; 

        Rb = GetComponent<Rigidbody2D>();
        if (Rb == null) Rb = gameObject.AddComponent<Rigidbody2D>();
        Rb.bodyType = RigidbodyType2D.Dynamic;
        Rb.gravityScale = 0f; // top-down view
        Rb.linearDamping = linearDamping;
        Rb.freezeRotation = true; // no sprite spinning
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Collider = GetComponent<CircleCollider2D>();
        if (Collider == null) Collider = gameObject.AddComponent<CircleCollider2D>();
        Collider.radius = sr.sprite != null ? sr.sprite.bounds.extents.x : Collider.radius;
        Collider.sharedMaterial = new PhysicsMaterial2D { bounciness = bounciness, friction = 0f };
    }

    public void Init(int ballNumber, Sprite sprite)
    {
        number = ballNumber;
        striped = ballNumber > 8;
        sr.sprite = sprite;
        Collider.radius = sprite.bounds.extents.x;
        name = IsCueBall ? "CueBall" : "Ball " + ballNumber;
    }

    // Set the ball moving at the given velocity (units per second)
    public void Hit(Vector2 velocity)
    {
        Rb.linearVelocity = velocity;
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
