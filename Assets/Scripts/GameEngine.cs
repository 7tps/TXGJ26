using System;
using System.Collections.Generic;
using UnityEngine;

public class GameEngine : MonoBehaviour
{
    public enum State { Aiming, BallsMoving }

    // Hook for scoring / rules later
    public event Action<Ball> BallPocketed;

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

    const float HeadSpotFraction = 0.25f; // cue ball starts this far (of felt width) left of centre
    const float RackApexFraction = 0.1f; // rack apex sits this far right of centre

    public State CurrentState { get; private set; }

    readonly List<Ball> balls = new List<Ball>();
    readonly List<Vector2> pocketWorld = new List<Vector2>();
    Ball cueBall;
    float captureRadiusWorld;
    Vector2 feltMinWorld, feltMaxWorld;

    void Start()
    {
        if (table == null) table = GameObject.Find("Table").transform;
        if (cueStick == null) cueStick = FindFirstObjectByType<CueStick>();

        BuildTable();
        SpawnBalls();

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

    // ---- Turn flow ----

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
        Debug.Log(ball.IsCueBall ? "Scratch! Cue ball pocketed" : "Pocketed ball " + ball.number);
        BallPocketed?.Invoke(ball);
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
        ball.Init(number, GetSprite(number == 0 ? 15 : number - 1));
        balls.Add(ball);
        return ball;
    }

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
