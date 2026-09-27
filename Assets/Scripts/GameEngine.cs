using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] TMP_Text timerText; // optional, shows time left tonight
    [SerializeField] int[] ballValues = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150 }; // money for balls 1-15

    [Header("References")]
    [SerializeField] Ball ballPrefab;
    [SerializeField] Sprite[] ballSprites; // one sprite per ball: "cue ball" for the cue ball, "1".."15" for the rest
    [SerializeField] Transform table;
    [SerializeField] CueStick cueStick;

    // Measured in sprite pixels from the centre of the table sprite (y up). These are specific to
    // whichever image is on the Table object - re-measure them if that sprite is ever swapped again.
    [Header("Table geometry (sprite pixels)")]
    [SerializeField] Vector2 feltMin = new Vector2(-740f, -344f);
    [SerializeField] Vector2 feltMax = new Vector2(739f, 345f);
    [SerializeField] float cornerGap = 46f; // cushions stop this far from each corner
    [SerializeField] float sideGap = 33f; // half-width of the side pocket openings
    [SerializeField] float cushionThickness = 100f;
    [SerializeField] float pocketCaptureRadius = 32f; // a ball whose centre gets this close is pocketed
    [SerializeField] Vector2[] pockets =
    {
        new Vector2(-726f, 383f), new Vector2(0f, 383f), new Vector2(726f, 383f),
        new Vector2(-726f, -383f), new Vector2(0f, -383f), new Vector2(726f, -383f),
    };

    [Header("Aim guide")]
    [SerializeField] int guideBounces = 2; // how many cushion bounces the aim guide shows
    [SerializeField] float cushionBounciness = 0.9f;
    PhysicsMaterial2D cushionMaterial;

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

    // Extra Cue Ball (Ball tree, tier 3): a second cue ball + cue stick, created once if the upgrade is owned
    Ball cueBall2;
    CueStick cueStick2;
    bool extraCueBallEnabled;

    // Reset every turn (BeginAiming), not every shot, so a second shot from Extra Cue Ball still
    // counts as part of the same chain/turn
    readonly List<Ball> chainOrder = new List<Ball>(); // object balls, in the order each was first hit this turn
    readonly Dictionary<Ball.SpecialType, int> specialPocketedThisTurn = new Dictionary<Ball.SpecialType, int>();
    bool cue1ContributedThisTurn, cue2ContributedThisTurn;
    int shotsThisTurn;

    // Flair tree, also reset every turn
    int pocketedThisTurn, payoutThisTurn; // Cash Out, Hot Streak
    int ballsOnTableAtTurnStart, longChainTierThisTurn; // Long Chain
    bool turnHasLastShotOfNight; // Closer: the shot still rolling when the timer hit 0
    bool turnHasFinalShots; // Nothing to Lose: a shot taken in the night's last few seconds
    bool roundOver; // stops EndTurn running twice while the menu scene loads

    public int ChainLengthNow => chainOrder.Count;

    // 0 for the first object ball hit this turn, 1 for the second, ... -1 if not in the chain (e.g. a cue ball)
    public int ChainPosition(Ball ball) => chainOrder.IndexOf(ball);

    // True for the whole first shot of a turn (including its chain reaction), false from the
    // second shot onward - only relevant once Extra Cue Ball allows a second shot in one turn
    public bool IsFirstShotOfTurn => shotsThisTurn <= 1;

    // True once both cue balls have each hit something this turn, and the Ball tier-4 upgrade for it is owned
    public bool BothCueBallsContributed =>
        cue1ContributedThisTurn && cue2ContributedThisTurn &&
        Upgrades.Instance != null && Upgrades.Instance.HasDoubleMultOnMergedChains;

    void Start()
    {
        if (table == null) table = GameObject.Find("Table").transform;
        if (cueStick == null) cueStick = FindFirstObjectByType<CueStick>();

        // Pull whatever's currently owned from Upgrades before anything else is built, since
        // purchases happen in the menu scene where none of these objects exist to push a value into
        if (Upgrades.Instance != null)
        {
            if (Upgrades.Instance.HasAimGuide) guideBounces = Upgrades.Instance.CurrentGuideBounces;
            cushionBounciness = Upgrades.Instance.CurrentCushionBounciness;
            Money = Upgrades.Instance.Money;
            Upgrades.Instance.BeginNightIfNeeded();
        }

        UpdateBalanceText();
        UpdateTimerText();
        BuildTable();
        SpawnBalls();
        aimGuide = new GameObject("AimGuide").AddComponent<AimGuide>();

        cueStick.ShotTaken += OnShotTaken;

        if (Upgrades.Instance != null && Upgrades.Instance.HasExtraCueBall) EnableExtraCueBall();

        BeginAiming();
    }

    void OnDestroy()
    {
        if (cueStick != null) cueStick.ShotTaken -= OnShotTaken;
        if (cueStick2 != null) cueStick2.ShotTaken -= OnShotTaken;
    }

    void Update()
    {
        if (roundOver) return;

        UpdateTimerText();

        if (Upgrades.Instance != null && Upgrades.Instance.NightTimeUp)
        {
            // Out of time with nothing rolling: the night ends right away
            if (CurrentState == State.Aiming)
            {
                FinishNight(false);
                roundOver = true;
                returnToMenu();
                return;
            }

            // Out of time mid-shot: no new shots, but this one finishes rolling and pays out
            if (!turnHasLastShotOfNight) OnTimeRanOutMidShot();
        }

        if (CurrentState == State.BallsMoving && balls.TrueForAll(b => b.IsSettled)) EndTurn();
    }

    void OnTimeRanOutMidShot()
    {
        turnHasLastShotOfNight = true;
        HideIdleCueSticks(); // with Extra Cue Ball, the other stick can't start a new shot

        // Closer doubles the whole buzzer-beater shot, including anything it already pocketed before the timer hit 0
        if (Upgrades.Instance.HasCloser)
        {
            int extra = Mathf.RoundToInt(payoutThisTurn * (Upgrades.Instance.CloserPayoutMultiplier - 1f));
            payoutThisTurn += extra;
            Earn(extra);
        }
    }

    void FinishNight(bool rackCleared)
    {
        if (!rackCleared && Upgrades.Instance.HasLeftovers) PayLeftovers(Upgrades.Instance.LeftoversFraction);
        Upgrades.Instance.EndNight();
    }

    // Runs once when every ball has settled after a turn: end-of-turn Flair bonuses, then either
    // the next turn, or back to the menu if the rack is cleared or the night is out of shots
    void EndTurn()
    {
        Upgrades up = Upgrades.Instance;

        if (up != null)
        {
            if (up.HasCashOut && pocketedThisTurn >= up.CashOutMinBalls)
                Earn(Mathf.RoundToInt(payoutThisTurn * (up.CashOutMultiplier - 1f)));

            if (up.HasSecondWind && chainOrder.Count >= up.SecondWindMinChain) up.AddTime(up.SecondWindSeconds);

            up.RecordShotResult(pocketedThisTurn > 0);
        }

        bool cleared = RackCleared();
        if (cleared && up != null)
        {
            Earn(up.CleanSweepPayout);
            up.RecordRoundCleared();
        }

        bool nightOver = up != null && up.NightTimeUp;
        if (nightOver) FinishNight(cleared);

        UpdateTimerText();

        if (cleared || nightOver)
        {
            roundOver = true;
            returnToMenu();
        }
        else
        {
            BeginAiming();
        }
    }

    // Leftovers: every object ball still on the table when the night ends pays a fraction of its payout
    void PayLeftovers(float fraction)
    {
        foreach (Ball ball in balls)
            if (!ball.IsCueBall && !ball.IsPocketed) Earn(Mathf.RoundToInt(ball.Payout * fraction));
    }

    // A rack is cleared once every ball but the cue ball(s) has been pocketed
    bool RackCleared()
    {
        foreach (Ball ball in balls)
            if (!ball.IsCueBall && !ball.IsPocketed) return false;
        return true;
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
        shotsThisTurn++;

        if (Upgrades.Instance != null && Upgrades.Instance.NightTimeLeft <= Upgrades.Instance.NothingToLoseSeconds)
            turnHasFinalShots = true;
    }

    void HideIdleCueSticks()
    {
        if (cueStick.IsAiming) cueStick.Hide();
        if (cueStick2 != null && cueStick2.IsAiming) cueStick2.Hide();
    }

    void BeginAiming()
    {
        CurrentState = State.Aiming;

        // A new turn starts a fresh chain, even if Extra Cue Ball's second shot hasn't happened yet
        chainOrder.Clear();
        specialPocketedThisTurn.Clear();
        cue1ContributedThisTurn = false;
        cue2ContributedThisTurn = false;
        shotsThisTurn = 0;
        pocketedThisTurn = 0;
        payoutThisTurn = 0;
        longChainTierThisTurn = 0;
        turnHasLastShotOfNight = false;
        turnHasFinalShots = false;

        ballsOnTableAtTurnStart = 0;
        foreach (Ball ball in balls)
            if (!ball.IsCueBall && !ball.IsPocketed) ballsOnTableAtTurnStart++;

        // Slow Burn milestone: every ball still on the table gets a little multiplier each new turn
        if (Upgrades.Instance != null && Upgrades.Instance.HasSlowBurn)
            foreach (Ball ball in balls)
                if (!ball.IsCueBall) ball.TickSlowBurn(Upgrades.Instance.SlowBurnAmount);

        // scratch logic
        if (cueBall.IsPocketed) cueBall.Respawn(FindFreeSpot(HeadSpot()));
        cueStick.Show(cueBall);

        if (extraCueBallEnabled)
        {
            if (cueBall2.IsPocketed) cueBall2.Respawn(FindFreeSpot(HeadSpot() + Vector2.up * cueBall.WorldRadius * 4f));
            cueStick2.Show(cueBall2);
        }
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
            // V x M, then x C if it went down as part of a chain (2+ balls hit this turn)
            int payout = ball.Payout;
            bool inChain = ChainLengthNow >= 2;
            if (inChain) payout = Mathf.RoundToInt(payout * ChainPocketBonus());
            payout = ApplyCloser(payout);

            pocketedThisTurn++;
            payoutThisTurn += payout;
            Earn(payout);

            if (ball.Special != Ball.SpecialType.None)
            {
                specialPocketedThisTurn.TryGetValue(ball.Special, out int count);
                count++;
                specialPocketedThisTurn[ball.Special] = count;

                if (count == 2 && Upgrades.Instance != null && Upgrades.Instance.HasSpecialComboBonus)
                    Earn(Upgrades.Instance.SpecialComboBonusAmount);
            }

            Debug.Log($"Pocketed ball {ball.number}: ${ball.Value} x {ball.Multiplier:0.0#}{(inChain ? $" x C {ChainPocketBonus():0.0#}" : "")} = ${payout} (total ${Money})");
        }

        BallPocketed?.Invoke(ball);
    }

    // Records the order object balls are first hit each turn, and which cue ball(s) have hit something
    public void RegisterHit(Ball a, Ball b)
    {
        ConsiderHit(a);
        ConsiderHit(b);
    }

    void ConsiderHit(Ball ball)
    {
        if (ball.IsCueBall)
        {
            if (ball == cueBall) cue1ContributedThisTurn = true;
            else if (ball == cueBall2) cue2ContributedThisTurn = true;
        }
        else if (!chainOrder.Contains(ball))
        {
            chainOrder.Add(ball);
            CheckLongChain();
        }
    }

    // Long Chain: each time the chain reaches a new tier (40/60/80/100% of the balls on the table),
    // every ball in it that's still on the table gets bonus M
    void CheckLongChain()
    {
        if (Upgrades.Instance == null || !Upgrades.Instance.HasLongChain) return;

        int tier = Upgrades.Instance.LongChainTier(chainOrder.Count, ballsOnTableAtTurnStart);
        if (tier <= longChainTierThisTurn) return;

        float bonus = (tier - longChainTierThisTurn) * Upgrades.Instance.LongChainBonusPerTier;
        longChainTierThisTurn = tier;
        foreach (Ball b in chainOrder) b.AddMultiplier(bonus);
    }

    // C: Clean Pocket's base, x Hot Streak, x Nothing to Lose during the last shots of the night
    float ChainPocketBonus()
    {
        Upgrades up = Upgrades.Instance;
        if (up == null) return 1f;

        float c = up.CurrentChainPocketBonus * up.HotStreakFactor;
        if (turnHasFinalShots && up.HasNothingToLose) c *= up.NothingToLoseBonus;
        return c;
    }

    // Closer: everything paid out during the night's last shot is doubled
    int ApplyCloser(int payout)
    {
        if (turnHasLastShotOfNight && Upgrades.Instance != null && Upgrades.Instance.HasCloser)
            return Mathf.RoundToInt(payout * Upgrades.Instance.CloserPayoutMultiplier);
        return payout;
    }

    // Every payout goes through here so the scene's Money and Upgrades' persistent wallet stay in step
    void Earn(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        Upgrades.Instance?.AddMoney(amount);
        UpdateBalanceText();
        MoneyChanged?.Invoke(Money);
    }

    // True once every ball except `exception` and the cue ball(s) has been pocketed (for Money Ball)
    public bool AllOtherBallsPocketed(Ball exception)
    {
        foreach (Ball ball in balls)
            if (ball != exception && !ball.IsCueBall && !ball.IsPocketed) return false;
        return true;
    }

    // Glass shatters mid-table instead of being pocketed: pay it out and sink it in place
    public void PayGlass(Ball ball)
    {
        Earn(ApplyCloser(ball.Payout));

        ball.Pocket(ball.Rb.position);
        BallPocketed?.Invoke(ball);
    }

    // Creates the second cue ball and cue stick for the Extra Cue Ball upgrade. Safe to call more
    // than once (e.g. every time this scene loads) - it only builds them the first time.
    public void EnableExtraCueBall()
    {
        if (extraCueBallEnabled) return;
        extraCueBallEnabled = true;

        Vector2 spot = FindFreeSpot(HeadSpot() + Vector2.up * cueBall.WorldRadius * 4f);
        cueBall2 = SpawnBall(-1, spot);

        cueStick2 = Instantiate(cueStick.gameObject, cueStick.transform.parent).GetComponent<CueStick>();
        cueStick2.name = "Cue Stick 2";
        cueStick2.ShotTaken += OnShotTaken;
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
            float restitution = approach > BounceThreshold ? cushionBounciness : 0f;
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
        cushionMaterial = new PhysicsMaterial2D { bounciness = cushionBounciness, friction = 0f };
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
        box.sharedMaterial = cushionMaterial;
    }

    void SpawnBalls()
    {
        cueBall = SpawnBall(0, HeadSpot());

        // Ball Value upgrade (tier 1): how many object balls make the rack. Starts at 1 (per the lore:
        // you can't handle more yet) and grows toward the normal 15 via the Expanded Rack upgrade.
        int rackSize = Upgrades.Instance != null ? Upgrades.Instance.RackSize : 15;

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

        List<Ball> spawned = new List<Ball>();

        if (rackSize >= 15)
        {
            // full triangular rack, 8-ball fixed in the middle
            int next = 0;
            for (int row = 0; row < 5; row++)
            {
                for (int i = 0; i <= row; i++)
                {
                    Vector2 pos = apex + new Vector2(row * spacing * 0.866f, (i - row / 2f) * spacing);
                    int number = (row == 2 && i == 1) ? 8 : numbers[next++];
                    spawned.Add(SpawnBall(number, pos));
                }
            }
        }
        else
        {
            // a partial rack (below the normal 15): no triangle shape, just a simple line of however
            // many balls the Expanded Rack upgrade allows so far
            List<int> partialNumbers = new List<int>(numbers) { 8 };
            for (int i = partialNumbers.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (partialNumbers[i], partialNumbers[j]) = (partialNumbers[j], partialNumbers[i]);
            }

            for (int i = 0; i < rackSize; i++)
            {
                Vector2 pos = apex + new Vector2(i * spacing, 0f);
                spawned.Add(SpawnBall(partialNumbers[i], pos));
            }
        }

        // Milestone: every rack is guaranteed at least one special ball
        if (Upgrades.Instance != null && Upgrades.Instance.HasGuaranteedSpecialPerRack)
        {
            bool anySpecial = spawned.Exists(b => b.Special != Ball.SpecialType.None);
            if (!anySpecial)
            {
                Ball.SpecialType forced = Upgrades.Instance.RollSpecialType(true);
                if (forced != Ball.SpecialType.None)
                    spawned[UnityEngine.Random.Range(0, spawned.Count)].ApplySpecialType(forced);
            }
        }
    }

    Ball SpawnBall(int number, Vector2 pos)
    {
        Ball ball = Instantiate(ballPrefab, pos, Quaternion.identity);

        int value = number > 0 ? ValueOf(number) : 0;
        Ball.SpecialType type = Ball.SpecialType.None;

        if (number > 0 && Upgrades.Instance != null)
            type = Upgrades.Instance.RollSpecialType(false);

        ball.Init(number, GetSprite(number), value, type);

        if (Upgrades.Instance != null)
        {
            ball.SetLinearDamping(Upgrades.Instance.CurrentFriction);
            if (number > 0) ball.AddMultiplier(Upgrades.Instance.StartingMultiplierBonus);
        }

        balls.Add(ball);
        return ball;
    }

    void UpdateBalanceText()
    {
        if (balanceText != null) balanceText.text = $"${Money:N0}";
    }

    void UpdateTimerText()
    {
        if (timerText == null || Upgrades.Instance == null) return;
        int seconds = Mathf.CeilToInt(Upgrades.Instance.NightTimeLeft);
        timerText.text = $"{seconds / 60}:{seconds % 60:00}";
    }

    // The cue ball (0 or below) is worth nothing. Ball Value upgrade adds a flat bonus on top.
    int ValueOf(int number)
    {
        int value = number >= 1 && number <= ballValues.Length ? ballValues[number - 1] : 0;
        return Upgrades.Instance != null ? Upgrades.Instance.ApplyBallValueBonus(value) : value;
    }

    // Looks a ball's sprite up by name: "cue ball" for either cue ball, its own number otherwise. A texture
    // sliced to a single sprite gets named "<filename>_0" by Unity, so that suffix is stripped before comparing.
    Sprite GetSprite(int ballNumber)
    {
        string expected = ballNumber <= 0 ? "cue ball" : ballNumber.ToString();

        foreach (Sprite s in ballSprites)
        {
            if (s == null) continue;
            string spriteName = s.name.EndsWith("_0") ? s.name[..^2] : s.name;
            if (string.Equals(spriteName, expected, StringComparison.OrdinalIgnoreCase)) return s;
        }

        string who = ballNumber <= 0 ? "the cue ball" : "ball " + ballNumber;
        Debug.LogError($"No sprite found for {who} in GameEngine's Ball Sprites list (looked for \"{expected}\")");
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

    public void returnToMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    public void quit()
    {
        Application.Quit();
    }

    public Ball getBall(int id)
    {
        return balls.Find(u => u.number == id);
    }

    // Set this to change how bouncy the rails are, e.g. from an upgrade
    public void SetCushionBounciness(float value)
    {
        cushionBounciness = Mathf.Clamp01(value);
        cushionMaterial.bounciness = cushionBounciness;
    }

    // Spends money if there's enough, and reports whether it succeeded
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || Money < amount) return false;

        Money -= amount;
        UpdateBalanceText();
        MoneyChanged?.Invoke(Money);
        return true;
    }
}
