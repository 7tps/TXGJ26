using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameEngine : MonoBehaviour
{
    public enum State { Aiming, BallsMoving }

    // Hook for scoring / rules later
    public event Action<Ball> BallPocketed;

    // Raised with the new total whenever a pocketed ball pays out
    public event Action<int> MoneyChanged;

    public int Money { get; private set; }

    [Header("Scoring")]
    [SerializeField] TMP_Text balanceText; // shows the total balance
    [SerializeField] int[] ballValues = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150 }; // money for balls 1-15

    [Header("References")]
    [SerializeField] Ball ballPrefab;
    [SerializeField] Sprite[] ballSprites; 
    [SerializeField] string spritePrefix = "Balls1_";
    [SerializeField] Transform table;
    [SerializeField] CueStick cueStick;

    // measured in sprite pixels from the centre of the table sprite (y up)
    [Header("Table geometry (sprite pixels)")]
    [SerializeField] Vector2 feltMin = new Vector2(-232.5f, -126.5f);
    [SerializeField] Vector2 feltMax = new Vector2(232.5f, 119.5f);
    [SerializeField] float cornerGap = 26f; // cushions stop this far from each corner
    [SerializeField] float sideGap = 20f; // half-width of the side pocket openings
    [SerializeField] float cushionThickness = 40f;
    [SerializeField] float pocketCaptureRadius = 24f; // a ball whose centre gets this close is pocketed
    [SerializeField] Vector2[] pockets =
    {
        new Vector2(-240.5f, 128.5f), new Vector2(0f, 128.5f), new Vector2(240.5f, 128.5f),
        new Vector2(-240.5f, -128f), new Vector2(0f, -128f), new Vector2(240.5f, -128f),
    };

    [Header("Aim guide")]
    [SerializeField] int guideBounces = 2; // how many cushion bounces the aim guide shows

    // Set this to change how many bounces the aim guide shows, e.g. from an upgrade
    public int GuideBounces
    {
        get => guideBounces;
        set => guideBounces = Mathf.Max(0, value);
    }

    [SerializeField] float guideEndLength = 4f; // length of the path drawn after the last bounce (world units)

    const float HeadSpotFraction = 0.25f; // cue ball starts this far (of felt width) left of centre
    const float RackApexFraction = 0.1f; // rack apex sits this far right of centre
    const float BounceThreshold = .5f; // slower impacts don't bounce (Physics 2D "Velocity Threshold")
    const float TouchDistance = 0.02f; // a cast hit closer than this counts as already touching

    public State CurrentState { get; private set; }

    // where a moving ball ends up and how a collision changes things
    public class ShotPrediction
    {
        public Ball target; // the first ball the mover hits, or null if it hits none
        public Vector2 contactPosition; // the mover's centre when it touches the target, or where its path ends
        public Vector2 moverVelocityAfter; // right after the impact
        public Vector2 targetVelocityAfter;
        public bool moverPocketed;
        public bool targetPocketed;
        public readonly List<Vector2> approachPath = new List<Vector2>(); // mover, up to the impact
        public readonly List<Vector2> moverPathAfter = new List<Vector2>(); // mover, after the impact
        public readonly List<Vector2> targetPathAfter = new List<Vector2>(); // target, after being hit
    }

    readonly List<Ball> balls = new List<Ball>();
    readonly List<Vector2> pocketWorld = new List<Vector2>();
    readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    ContactFilter2D castFilter;
    AimGuide aimGuide;
    Ball cueBall;
    float captureRadiusWorld;
    Vector2 feltMinWorld, feltMaxWorld;

    void Start()
    {
        if (table == null) table = GameObject.Find("Table").transform;
        if (cueStick == null) cueStick = FindFirstObjectByType<CueStick>();

        UpdateBalanceText();
        BuildTable();
        SpawnBalls();
        aimGuide = new GameObject("AimGuide").AddComponent<AimGuide>();

        cueStick.ShotTaken += OnShotTaken;
        BeginAiming();
    }

    void OnDestroy()
    {
        if (cueStick != null) cueStick.ShotTaken -= OnShotTaken;
    }

    void Update()
    {
        if (CurrentState == State.BallsMoving && balls.TrueForAll(b => b.IsSettled))
            BeginAiming();
    }

    // After the cue has moved for the frame, redraw the guide
    void LateUpdate()
    {
        UpdateAimGuide(GuideBounces);
    }

    // Draws the guide for the shot the cue is lined up, showing the given number of cushion bounces
    void UpdateAimGuide(int bounces)
    {
        if (CurrentState == State.Aiming && cueStick.IsAiming && cueStick.PreviewSpeed > 0f)
        {
            ShotPrediction prediction = PredictShot(cueBall, cueStick.AimDirection * cueStick.PreviewSpeed, bounces);
            aimGuide.Show(prediction, cueBall.WorldRadius);
        }
        else
        {
            aimGuide.Hide();
        }
    }

    void FixedUpdate()
    {
        foreach (Ball ball in balls)
        {
            if (ball.IsPocketed) continue;

            Vector2 pos = ball.Rb.position;
            int nearest = NearestPocket(pos, out float sqrDist);

            bool inPocket = sqrDist < captureRadiusWorld * captureRadiusWorld;
            // pockets any balls past cushions for safety
            bool escaped = pos.x < feltMinWorld.x || pos.x > feltMaxWorld.x || pos.y < feltMinWorld.y || pos.y > feltMaxWorld.y;

            if (inPocket || escaped) PocketBall(ball, pocketWorld[nearest]);
        }
    }

    // TURNS

    void OnShotTaken()
    {
        CurrentState = State.BallsMoving;
    }

    void BeginAiming()
    {
        CurrentState = State.Aiming;

        // scratch logic
        if (cueBall.IsPocketed) cueBall.Respawn(FindFreeSpot(HeadSpot()));

        cueStick.Show(cueBall);
    }

    void PocketBall(Ball ball, Vector2 pocketPos)
    {
        ball.Pocket(pocketPos);

        if (ball.IsCueBall)
        {
            Debug.Log("Scratch! Cue ball pocketed");
        }
        else
        {
            // Value x this ball's own multiplier
            int payout = ball.Payout;
            Money += payout;
            Debug.Log($"Pocketed ball {ball.number}: ${ball.Value} x {ball.Multiplier:0.0#} = ${payout} (total ${Money})");
            UpdateBalanceText();
            MoneyChanged?.Invoke(Money);
        }

        BallPocketed?.Invoke(ball);
    }

    // PREDICTION

    public ShotPrediction PredictShot(Ball mover, Vector2 velocity, int bounces)
    {
        ShotPrediction prediction = new ShotPrediction();

        Ball hit = TracePath(mover, null, mover.Rb.position, velocity, bounces, false, prediction.approachPath,
            out Vector2 endPosition, out Vector2 endVelocity, out bool pocketed);

        prediction.contactPosition = endPosition;
        prediction.moverPocketed = pocketed;

        if (hit != null) PredictImpact(prediction, mover, hit, endPosition, endVelocity, bounces);
        return prediction;
    }

    public void PredictImpact(ShotPrediction prediction, Ball mover, Ball target, Vector2 contactPosition,
        Vector2 velocity, int bounces)
    {
        prediction.target = target;
        prediction.contactPosition = contactPosition;
        
        Vector2 normal = target.Rb.position - contactPosition;
        normal = normal.sqrMagnitude > 0f ? normal.normalized : velocity.normalized;

        ResolveBallCollision(velocity, target.Rb.linearVelocity, normal, mover.Bounciness,
            out prediction.moverVelocityAfter, out prediction.targetVelocityAfter);

        TracePath(mover, target, contactPosition, prediction.moverVelocityAfter, bounces, true, prediction.moverPathAfter,
            out _, out _, out bool moverPocketed);
        TracePath(target, mover, target.Rb.position, prediction.targetVelocityAfter, bounces, true, prediction.targetPathAfter,
            out _, out _, out bool targetPocketed);

        prediction.moverPocketed |= moverPocketed;
        prediction.targetPocketed = targetPocketed;
    }

    
    public static void ResolveBallCollision(Vector2 v1, Vector2 v2, Vector2 normal, float bounciness,
        out Vector2 newV1, out Vector2 newV2)
    {
        float approach = Vector2.Dot(v1 - v2, normal);
        if (approach <= 0f)
        {
            newV1 = v1;
            newV2 = v2;
            return;
        }

        float restitution = approach > BounceThreshold ? bounciness : 0f;
        float push = (1f + restitution) / 2f * approach;
        newV1 = v1 - normal * push;
        newV2 = v2 + normal * push;
    }

    Ball TracePath(Ball mover, Ball ignore, Vector2 start, Vector2 velocity, int maxBounces, bool capFirstLeg,
        List<Vector2> path, out Vector2 endPosition, out Vector2 endVelocity, out bool pocketed)
    {
        float radius = mover.WorldRadius;
        float damping = mover.LinearDamping;
        Vector2 pos = start;
        Vector2 vel = velocity;
        pocketed = false;
        path.Add(pos);

        for (int bounce = 0; bounce <= maxBounces; bounce++)
        {
            float speed = vel.magnitude;
            if (speed <= mover.StopSpeed)
            {
                vel = Vector2.zero;
                break;
            }

            // roll distance factoring in damping
            Vector2 dir = vel / speed;
            float range = (speed - mover.StopSpeed) / damping;

            // cap aim guide 
            if (bounce == maxBounces && (bounce > 0 || capFirstLeg)) range = Mathf.Min(range, guideEndLength);

            bool found = CastBall(mover, ignore, pos, radius, dir, range, out RaycastHit2D hit);
            float distance = found ? hit.distance : range;

            if (PocketOnSegment(pos, dir, distance, out Vector2 pocketPos))
            {
                path.Add(pocketPos);
                pos = pocketPos;
                vel = Vector2.zero;
                pocketed = true;
                break;
            }

            pos = found ? hit.centroid : pos + dir * distance;
            path.Add(pos);

            if (!found)
            {
                vel = Vector2.zero;
                break;
            }

            vel = dir * (speed - damping * distance);

            if (hit.collider.TryGetComponent(out Ball other))
            {
                endPosition = pos;
                endVelocity = vel;
                return other;
            }

            // lose small amt of speed on cushion bounce
            float approach = -Vector2.Dot(vel, hit.normal);
            float restitution = approach > BounceThreshold ? mover.Bounciness : 0f;
            vel += (1f + restitution) * approach * hit.normal;
        }

        endPosition = pos;
        endVelocity = vel;
        return null;
    }

    // Sweeps a circle from origin along dir and finds the nearest thing it would hit, skipping the
    // moving ball itself (and `ignore`)
    bool CastBall(Ball mover, Ball ignore, Vector2 origin, float radius, Vector2 dir, float distance, out RaycastHit2D closest)
    {
        closest = default;
        bool found = false;
        float best = float.MaxValue;

        int count = Physics2D.CircleCast(origin, radius, dir, castFilter, castHits, distance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D h = castHits[i];
            if (h.collider == mover.Collider || (ignore != null && h.collider == ignore.Collider)) continue;

            // A hit at the very start means the ball is already touching that surface. The cast reports
            // this even when the ball is moving away (straight after a bounce, say) and its normal can't
            // be trusted, so work out which way the surface really faces and only count it if the ball
            // is heading into it.
            if (h.distance < TouchDistance)
            {
                Vector2 away = origin - h.collider.ClosestPoint(origin);
                if (away.sqrMagnitude > 1e-8f)
                {
                    if (Vector2.Dot(dir, away) >= 0f) continue;
                    h.normal = away.normalized;
                }
                else
                {
                    h.normal = -dir;
                }
                h.centroid = origin;
            }

            if (h.distance < best)
            {
                best = h.distance;
                closest = h;
                found = true;
            }
        }
        return found;
    }

    // Finds where a ball travelling along pos -> pos + dir * length first gets close enough to a pocket to drop in
    bool PocketOnSegment(Vector2 pos, Vector2 dir, float length, out Vector2 entry)
    {
        entry = default;
        float best = float.MaxValue;

        foreach (Vector2 pocket in pocketWorld)
        {
            Vector2 toPocket = pocket - pos;
            float along = Vector2.Dot(toPocket, dir);
            float discriminant = captureRadiusWorld * captureRadiusWorld - (toPocket.sqrMagnitude - along * along);
            if (discriminant < 0f) continue;

            float half = Mathf.Sqrt(discriminant);
            if (along - half > length || along + half < 0f) continue; // pocket is off the ends of the segment

            float t = Mathf.Max(along - half, 0f);
            if (t < best)
            {
                best = t;
                entry = pos + dir * t;
            }
        }
        return best < float.MaxValue;
    }

    // GAME SETUP

    void BuildTable()
    {
        captureRadiusWorld = pocketCaptureRadius * PxScale();

        pocketWorld.Clear();
        foreach (Vector2 p in pockets) pocketWorld.Add(PxToWorld(p));

        // Balls further out than this have escaped the table
        feltMinWorld = PxToWorld(feltMin - Vector2.one * cushionThickness);
        feltMaxWorld = PxToWorld(feltMax + Vector2.one * cushionThickness);

        // Invisible cushions around the felt, with gaps left open at the pockets
        Transform root = new GameObject("Cushions").transform;
        float cx = (feltMin.x + feltMax.x) / 2f;
        float l = feltMin.x, r = feltMax.x, b = feltMin.y, t = feltMax.y, th = cushionThickness;

        AddCushion(root, l + cornerGap, cx - sideGap, t, t + th); // top left
        AddCushion(root, cx + sideGap, r - cornerGap, t, t + th); // top right
        AddCushion(root, l + cornerGap, cx - sideGap, b - th, b); // bottom left
        AddCushion(root, cx + sideGap, r - cornerGap, b - th, b); // bottom right
        AddCushion(root, l - th, l, b + cornerGap, t - cornerGap); // left
        AddCushion(root, r, r + th, b + cornerGap, t - cornerGap); // right
    }

    void AddCushion(Transform parent, float minX, float maxX, float minY, float maxY)
    {
        GameObject go = new GameObject("Cushion");
        go.transform.SetParent(parent);
        go.transform.position = PxToWorld(new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f));

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(maxX - minX, maxY - minY) * PxScale();
    }

    void SpawnBalls()
    {
        cueBall = SpawnBall(0, HeadSpot());

        // setup triangle
        List<int> numbers = new List<int>();
        for (int n = 1; n <= 15; n++)
            if (n != 8) numbers.Add(n);
        for (int i = numbers.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (numbers[i], numbers[j]) = (numbers[j], numbers[i]);
        }

        float spacing = cueBall.WorldRadius * 2f * 1.01f; 
        Vector2 apex = PxToWorld(FeltCentre() + new Vector2((feltMax.x - feltMin.x) * RackApexFraction, 0f));
        int next = 0;

        for (int row = 0; row < 5; row++)
        {
            for (int i = 0; i <= row; i++)
            {
                Vector2 pos = apex + new Vector2(row * spacing * 0.866f, (i - row / 2f) * spacing);
                int number = (row == 2 && i == 1) ? 8 : numbers[next++];
                SpawnBall(number, pos);
            }
        }
    }

    Ball SpawnBall(int number, Vector2 pos)
    {
        Ball ball = Instantiate(ballPrefab, pos, Quaternion.identity);
        ball.Init(number, GetSprite(number == 0 ? 15 : number - 1), ValueOf(number));
        balls.Add(ball);
        return ball;
    }

    void UpdateBalanceText()
    {
        if (balanceText != null) balanceText.text = $"${Money:N0}";
    }

    // The cue ball (0) is worth nothing
    int ValueOf(int number) => number >= 1 && number <= ballValues.Length ? ballValues[number - 1] : 0;

    Sprite GetSprite(int index)
    {
        string spriteName = spritePrefix + index;
        foreach (Sprite s in ballSprites)
            if (s != null && s.name == spriteName) return s;

        Debug.LogError("No ball sprite named " + spriteName + " in GameEngine's Ball Sprites list");
        return null;
    }

    // HELPERS

    Vector2 FeltCentre() => (feltMin + feltMax) / 2f;

    Vector2 HeadSpot() => PxToWorld(FeltCentre() - new Vector2((feltMax.x - feltMin.x) * HeadSpotFraction, 0f));
    
    Vector2 FindFreeSpot(Vector2 spot)
    {
        float radius = cueBall.WorldRadius;
        for (int i = 0; i < 50 && Physics2D.OverlapCircle(spot, radius) != null; i++)
            spot.x += radius;
        return spot;
    }

    int NearestPocket(Vector2 pos, out float sqrDist)
    {
        int best = 0;
        sqrDist = float.MaxValue;
        for (int i = 0; i < pocketWorld.Count; i++)
        {
            float d = (pos - pocketWorld[i]).sqrMagnitude;
            if (d < sqrDist)
            {
                sqrDist = d;
                best = i;
            }
        }
        return best;
    }

    // scale sprite pixels to world units
    float PxScale() => table.lossyScale.x / table.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit;

    Vector2 PxToWorld(Vector2 px) => (Vector2)table.position + px * PxScale();

    // display felt, pockets and capture radius in editor
    void OnDrawGizmosSelected()
    {
        if (table == null) return;

        Gizmos.color = Color.yellow;
        Vector2 min = PxToWorld(feltMin), max = PxToWorld(feltMax);
        Gizmos.DrawWireCube((min + max) / 2f, max - min);

        Gizmos.color = Color.red;
        foreach (Vector2 p in pockets)
            Gizmos.DrawWireSphere(PxToWorld(p), pocketCaptureRadius * PxScale());
    }
}
