using System.Collections.Generic;
using UnityEngine;

public class Upgrades : MonoBehaviour
{
    // Persists across the Menu <-> SampleScene reload, since purchases happen in the menu scene
    // where no GameEngine/CueStick/Ball exists yet. GameEngine and CueStick pull their starting
    // values from here (Instance) when they wake up, instead of this script pushing into them.
    public static Upgrades Instance { get; private set; }

    // Runs once when the game starts, before the first scene loads, whichever scene you press Play in.
    // Guarantees an Upgrades object exists even if none was placed in a scene. A scene-placed one
    // loaded later sees this one already set as Instance and removes itself (see Awake).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing()
    {
        if (Instance != null) return;
        new GameObject("Upgrades").AddComponent<Upgrades>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [Header("Money")]
    [SerializeField] int money = 0;
    public int Money => money;

    public void AddMoney(int amount)
    {
        if (amount > 0) money += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || money < amount) return false;
        money -= amount;
        return true;
    }

    // Run state for the current night, not purchases. Lives here because this is the only object
    // that survives the Menu <-> SampleScene reloads a night is made of.
    // The timer ticks here, in both scenes, so time spent in the shop between rounds counts too
    [Header("Night (run state)")]
    [SerializeField] float baseNightSeconds = 60f;
    [SerializeField] bool nightInProgress;
    [SerializeField] float nightTimeLeft;
    [SerializeField] int roundsClearedTonight;
    [SerializeField] int hotStreak;

    public float NightTimeLeft => nightTimeLeft;
    public bool NightTimeUp => nightInProgress && nightTimeLeft <= 0f;
    public int RoundsClearedTonight => roundsClearedTonight;
    public int HotStreak => hotStreak;
    public float NightLength => baseNightSeconds + EnduranceSecondsByLevel[Mathf.Clamp(enduranceLevel, 0, 4)];

    void Update()
    {
        if (nightInProgress && nightTimeLeft > 0f)
            nightTimeLeft = Mathf.Max(0f, nightTimeLeft - Time.deltaTime);
    }

    // Called every time the table scene loads: a fresh night with a full timer
    public void StartNewNight()
    {
        nightInProgress = true;
        nightTimeLeft = NightLength;
        roundsClearedTonight = 0;
        hotStreak = 0;
    }

    public void AddTime(float seconds) { if (nightInProgress && seconds > 0f) nightTimeLeft += seconds; }
    public void RecordRoundCleared() { roundsClearedTonight++; }
    public void RecordShotResult(bool pocketedSomething) { hotStreak = pocketedSomething ? hotStreak + 1 : 0; }

    public void EndNight()
    {
        nightInProgress = false;
        nightTimeLeft = 0f;
    }

    [Header("Cue Ball Upgrades")]
    [Space(15)]

    // ---- Cue - Aim Guide ----
    // level 0 = not bought = no guide, level 1 = line to the first ball, level 2+ = +1 bounce per level
    [Header("Cue - Aim Guide")]
    [SerializeField] int aimGuideLevel = 0;
    [SerializeField] int costAimGuideLevel1 = 100;
    [SerializeField] int costAimGuideLevel2 = 200;
    [SerializeField] int costAimGuideLevel3 = 300;
    [SerializeField] int costAimGuideLevel4 = 400;
    [SerializeField] int costAimGuideLevel5 = 500;

    public bool HasAimGuide => aimGuideLevel > 0;
    public int CurrentGuideBounces => Mathf.Max(0, aimGuideLevel - 1);

    public void BuyAimGuideLevel1(int money) { if (aimGuideLevel == 0 && money > costAimGuideLevel1 && TrySpend(costAimGuideLevel1)) aimGuideLevel = 1; }
    public void BuyAimGuideLevel2(int money) { if (aimGuideLevel == 1 && money > costAimGuideLevel2 && TrySpend(costAimGuideLevel2)) aimGuideLevel = 2; }
    public void BuyAimGuideLevel3(int money) { if (aimGuideLevel == 2 && money > costAimGuideLevel3 && TrySpend(costAimGuideLevel3)) aimGuideLevel = 3; }
    public void BuyAimGuideLevel4(int money) { if (aimGuideLevel == 3 && money > costAimGuideLevel4 && TrySpend(costAimGuideLevel4)) aimGuideLevel = 4; }
    public void BuyAimGuideLevel5(int money) { if (aimGuideLevel == 4 && money > costAimGuideLevel5 && TrySpend(costAimGuideLevel5)) aimGuideLevel = 5; }

    [Space(15)]

    // ---- Cue - Power ----
    // level 1 (free/base) -> maxPull 4, maxShotSpeed 25; level 2->5/30; level3->6/35; IV->7/40; V->8/45
    // Taking Breakshot instead of Power IV skips that tier's numeric bonus for good: Power V after
    // Breakshot lands back on 7/40, never reaching the 8/45 the IV path gets to.
    [Header("Cue - Power")]
    [SerializeField] int powerLevel = 1;
    [SerializeField] bool tookPowerIV;
    [SerializeField] bool hasBreakshot;
    [SerializeField] int costPowerLevel2 = 100;
    [SerializeField] int costPowerLevel3 = 200;
    [SerializeField] int costPowerLevelIV = 300;
    [SerializeField] int costBreakshot = 300;
    [SerializeField] int costPowerLevelV = 400;

    static readonly float[] PullByLevel = { 0f, 4f, 5f, 6f, 7f, 8f };
    static readonly float[] SpeedByLevel = { 0f, 25f, 30f, 35f, 40f, 45f };

    public float CurrentMaxPull => PullByLevel[Mathf.Clamp(powerLevel, 1, 5)];
    public float CurrentMaxShotSpeed => SpeedByLevel[Mathf.Clamp(powerLevel, 1, 5)];
    public bool HasBreakshot => hasBreakshot;

    public void BuyPowerLevel2(int money) { if (powerLevel == 1 && money > costPowerLevel2 && TrySpend(costPowerLevel2)) powerLevel = 2; }
    public void BuyPowerLevel3(int money) { if (powerLevel == 2 && money > costPowerLevel3 && TrySpend(costPowerLevel3)) powerLevel = 3; }

    // Exclusive with BuyBreakshot: only one of the two can ever be bought
    public void BuyPowerLevelIV(int money)
    {
        if (powerLevel != 3 || tookPowerIV || hasBreakshot) return;
        if (money > costPowerLevelIV && TrySpend(costPowerLevelIV)) { tookPowerIV = true; powerLevel = 4; }
    }

    // Exclusive with BuyPowerLevelIV. hasBreakshot only sets the flag - the +50% first-shot-of-round
    // bonus still needs CueStick to check it, which isn't wired up yet.
    public void BuyBreakshot(int money)
    {
        if (powerLevel != 3 || tookPowerIV || hasBreakshot) return;
        if (money > costBreakshot && TrySpend(costBreakshot)) hasBreakshot = true;
    }

    public void BuyPowerLevelV(int money)
    {
        bool tierFourResolved = tookPowerIV || hasBreakshot;
        if (!tierFourResolved || powerLevel >= 5) return;
        if (money > costPowerLevelV && TrySpend(costPowerLevelV))
            powerLevel = tookPowerIV ? 5 : 4; // breakshot path permanently skips one tier's numeric bonus
    }

    [Space(15)]

    // ---- Cue - Steady Hand (tier 2, off Power II) ----
    // Power = (pull / maxPull) ^ exponent. Full pull is still full power at every level, so reaching max
    // power costs no extra dragging - the curve just spreads the low and mid powers over more of the drag.
    // Level 1+ also shows the power readout above the cue ball while pulling back.
    [Header("Cue - Steady Hand")]
    [SerializeField] int steadyHandLevel = 0;
    [SerializeField] int costSteadyHandLevel1 = 150;
    [SerializeField] int costSteadyHandLevel2 = 250;
    [SerializeField] int costSteadyHandLevel3 = 400;

    static readonly float[] PowerCurveByLevel = { 1f, 1.35f, 1.7f, 2f };

    public int SteadyHandLevel => steadyHandLevel;
    public float CurrentPowerCurve => PowerCurveByLevel[Mathf.Clamp(steadyHandLevel, 0, 3)];

    public void BuySteadyHandLevel1(int money) { if (powerLevel >= 2 && steadyHandLevel == 0 && money > costSteadyHandLevel1 && TrySpend(costSteadyHandLevel1)) steadyHandLevel = 1; }
    public void BuySteadyHandLevel2(int money) { if (steadyHandLevel == 1 && money > costSteadyHandLevel2 && TrySpend(costSteadyHandLevel2)) steadyHandLevel = 2; }
    public void BuySteadyHandLevel3(int money) { if (steadyHandLevel == 2 && money > costSteadyHandLevel3 && TrySpend(costSteadyHandLevel3)) steadyHandLevel = 3; }

    [Header("Table Upgrades")]
    [Space(15)]

    // ---- Table - Smooth Felt ----
    // level 1 (free/base) -> linearDamping 0.8; each level -0.1
    [Header("Table - Smooth Felt")]
    [SerializeField] int frictionLevel = 1;
    [SerializeField] int costFrictionLevel2 = 100;
    [SerializeField] int costFrictionLevel3 = 200;
    [SerializeField] int costFrictionLevel4 = 300;

    static readonly float[] DampingByLevel = { 0f, 0.8f, 0.7f, 0.6f, 0.5f };
    public float CurrentFriction => DampingByLevel[Mathf.Clamp(frictionLevel, 1, 4)];

    public void BuyFrictionLevel2(int money) { if (frictionLevel == 1 && money > costFrictionLevel2 && TrySpend(costFrictionLevel2)) frictionLevel = 2; }
    public void BuyFrictionLevel3(int money) { if (frictionLevel == 2 && money > costFrictionLevel3 && TrySpend(costFrictionLevel3)) frictionLevel = 3; }
    public void BuyFrictionLevel4(int money) { if (frictionLevel == 3 && money > costFrictionLevel4 && TrySpend(costFrictionLevel4)) frictionLevel = 4; }

    [Space(15)]

    // ---- Table - Lively Rails ----
    // level 0 (free/base) -> cushionBounciness 0.9; each level +0.05, clamped to 1
    // (levels 3 and 4 land on the same clamped 1.0 as level 2 - carried over as-is)
    [Header("Table - Lively Rails")]
    [SerializeField] int bouncyRailsLevel = 0;
    [SerializeField] int costBouncyRailsLevel1 = 100;
    [SerializeField] int costBouncyRailsLevel2 = 200;
    [SerializeField] int costBouncyRailsLevel3 = 300;
    [SerializeField] int costBouncyRailsLevel4 = 400;

    static readonly float[] BounceByLevel = { 0.9f, 0.95f, 1f, 1f, 1f };
    public float CurrentCushionBounciness => BounceByLevel[Mathf.Clamp(bouncyRailsLevel, 0, 4)];

    public void BuyBouncyRailsLevel1(int money) { if (bouncyRailsLevel == 0 && money > costBouncyRailsLevel1 && TrySpend(costBouncyRailsLevel1)) bouncyRailsLevel = 1; }
    public void BuyBouncyRailsLevel2(int money) { if (bouncyRailsLevel == 1 && money > costBouncyRailsLevel2 && TrySpend(costBouncyRailsLevel2)) bouncyRailsLevel = 2; }
    public void BuyBouncyRailsLevel3(int money) { if (bouncyRailsLevel == 2 && money > costBouncyRailsLevel3 && TrySpend(costBouncyRailsLevel3)) bouncyRailsLevel = 3; }
    public void BuyBouncyRailsLevel4(int money) { if (bouncyRailsLevel == 3 && money > costBouncyRailsLevel4 && TrySpend(costBouncyRailsLevel4)) bouncyRailsLevel = 4; }

    [Header("Ball Upgrades")]
    [Space(15)]

    // ---- Ball - tier 1: Ball Value ----
    [Header("Ball - Ball Value")]
    [SerializeField] int ballValueLevel = 1;
    [SerializeField] int maxBallValueLevel = 5;
    [SerializeField] int ballValueBonusPerLevel = 10;
    [SerializeField] int costBallValue = 150;

    public int ApplyBallValueBonus(int baseValue) => baseValue + ballValueBonusPerLevel * (ballValueLevel - 1);

    public void BuyBallValue(int money)
    {
        if (ballValueLevel >= maxBallValueLevel) return;
        if (money > costBallValue && TrySpend(costBallValue)) ballValueLevel++;
    }

    [Space(15)]

    // ---- Ball - tier 2: Expanded Rack ----
    // Starts at 1 object ball (per the lore - you can't handle more yet), grows toward the normal 15
    [Header("Ball - Expanded Rack")]
    [SerializeField] int rackSize = 1;
    [SerializeField] int costPerRackBall = 80;

    public int RackSize => rackSize;

    public void BuyExpandedRack(int money)
    {
        if (rackSize >= 15) return;
        int cost = costPerRackBall * rackSize;
        if (money > cost && TrySpend(cost)) rackSize++;
    }

    [Space(15)]

    // ---- Ball - tier 2: Lucky Ball ----
    // Each level adds 5% chance per ball, at rack time, of spawning as one of the unlocked special types
    [Header("Ball - Lucky Ball")]
    [SerializeField] int luckyBallLevel = 0;
    [SerializeField] int maxLuckyBallLevel = 3;
    [SerializeField] float luckyBallChancePerLevel = 0.05f;
    [SerializeField] int costLuckyBall = 200;

    public bool HasLuckyBall => luckyBallLevel > 0;

    public void BuyLuckyBall(int money)
    {
        if (luckyBallLevel >= maxLuckyBallLevel) return;
        if (money > costLuckyBall && TrySpend(costLuckyBall)) luckyBallLevel++;
    }

    [Space(15)]

    // ---- Ball - tier 3: "name tbd" - raises the spawn chance further, on top of Lucky Ball ----
    [Header("Ball - Special Chance Boost")]
    [SerializeField] int specialChanceBoostLevel = 0;
    [SerializeField] int maxSpecialChanceBoostLevel = 3;
    [SerializeField] float specialChanceBoostPerLevel = 0.05f;
    [SerializeField] int costSpecialChanceBoost = 250;

    public void BuySpecialChanceBoost(int money)
    {
        if (!HasLuckyBall || specialChanceBoostLevel >= maxSpecialChanceBoostLevel) return;
        if (money > costSpecialChanceBoost && TrySpend(costSpecialChanceBoost)) specialChanceBoostLevel++;
    }

    // ---- Ball - tier 3: special type unlocks (one node per type) ----
    [Header("Ball - Special Type Unlocks")]
    [SerializeField] bool unlockedGold, unlockedHot, unlockedVault, unlockedWild, unlockedGlass, unlockedMoney;
    [SerializeField] int costUnlockGold = 200;
    [SerializeField] int costUnlockHot = 200;
    [SerializeField] int costUnlockVault = 250;
    [SerializeField] int costUnlockWild = 250;
    [SerializeField] int costUnlockGlass = 300;
    [SerializeField] int costUnlockMoney = 350;

    public void BuyUnlockGold(int money) { if (HasLuckyBall && !unlockedGold && money > costUnlockGold && TrySpend(costUnlockGold)) unlockedGold = true; }
    public void BuyUnlockHot(int money) { if (HasLuckyBall && !unlockedHot && money > costUnlockHot && TrySpend(costUnlockHot)) unlockedHot = true; }
    public void BuyUnlockVault(int money) { if (HasLuckyBall && !unlockedVault && money > costUnlockVault && TrySpend(costUnlockVault)) unlockedVault = true; }
    public void BuyUnlockWild(int money) { if (HasLuckyBall && !unlockedWild && money > costUnlockWild && TrySpend(costUnlockWild)) unlockedWild = true; }
    public void BuyUnlockGlass(int money) { if (HasLuckyBall && !unlockedGlass && money > costUnlockGlass && TrySpend(costUnlockGlass)) unlockedGlass = true; }
    public void BuyUnlockMoney(int money) { if (HasLuckyBall && !unlockedMoney && money > costUnlockMoney && TrySpend(costUnlockMoney)) unlockedMoney = true; }

    List<Ball.SpecialType> UnlockedTypes()
    {
        List<Ball.SpecialType> list = new List<Ball.SpecialType>();
        if (unlockedGold) list.Add(Ball.SpecialType.Gold);
        if (unlockedHot) list.Add(Ball.SpecialType.Hot);
        if (unlockedVault) list.Add(Ball.SpecialType.Vault);
        if (unlockedWild) list.Add(Ball.SpecialType.Wild);
        if (unlockedGlass) list.Add(Ball.SpecialType.Glass);
        if (unlockedMoney) list.Add(Ball.SpecialType.Money);
        return list;
    }

    // Called once per non-cue ball as it's spawned. guaranteed=true skips the chance roll (the
    // "every rack guaranteed a special" milestone), still picking only from unlocked types.
    public Ball.SpecialType RollSpecialType(bool guaranteed)
    {
        List<Ball.SpecialType> unlocked = UnlockedTypes();
        if (unlocked.Count == 0) return Ball.SpecialType.None;

        if (!guaranteed)
        {
            float chance = luckyBallLevel * luckyBallChancePerLevel + specialChanceBoostLevel * specialChanceBoostPerLevel;
            if (Random.value > chance) return Ball.SpecialType.None;
        }

        return unlocked[Random.Range(0, unlocked.Count)];
    }

    [Space(15)]

    // ---- Ball - tier 3: Extra Cue Ball ----
    [Header("Ball - Extra Cue Ball")]
    [SerializeField] bool hasExtraCueBall;
    [SerializeField] int costExtraCueBall = 500;

    public bool HasExtraCueBall => hasExtraCueBall;

    public void BuyExtraCueBall(int money)
    {
        if (hasExtraCueBall) return;
        if (money > costExtraCueBall && TrySpend(costExtraCueBall)) hasExtraCueBall = true;
    }

    [Space(15)]

    // ---- Ball - tier 4 ----
    [Header("Ball - Tier 4")]
    [SerializeField] bool hasDoubleMultOnMergedChains;
    [SerializeField] int costDoubleMultOnMergedChains = 400;
    [SerializeField] bool hasSpecialComboBonus;
    [SerializeField] int costSpecialComboBonus = 400;
    [SerializeField] int specialComboBonusAmount = 50;

    public bool HasDoubleMultOnMergedChains => hasDoubleMultOnMergedChains;
    public bool HasSpecialComboBonus => hasSpecialComboBonus;
    public int SpecialComboBonusAmount => specialComboBonusAmount;

    public void BuyDoubleMultOnMergedChains(int money)
    {
        if (!hasExtraCueBall || hasDoubleMultOnMergedChains) return;
        if (money > costDoubleMultOnMergedChains && TrySpend(costDoubleMultOnMergedChains)) hasDoubleMultOnMergedChains = true;
    }

    public void BuySpecialComboBonus(int money)
    {
        if (hasSpecialComboBonus) return;
        if (money > costSpecialComboBonus && TrySpend(costSpecialComboBonus)) hasSpecialComboBonus = true;
    }

    [Space(15)]

    // ---- Ball - milestone: guaranteed special per rack ----
    [Header("Ball - Milestone: Guaranteed Special")]
    [SerializeField] bool hasGuaranteedSpecialPerRack;
    [SerializeField] int costGuaranteedSpecialPerRack = 600;

    public bool HasGuaranteedSpecialPerRack => hasGuaranteedSpecialPerRack;

    public void BuyGuaranteedSpecialPerRack(int money)
    {
        bool tier4Complete = hasDoubleMultOnMergedChains && hasSpecialComboBonus;
        if (!tier4Complete || hasGuaranteedSpecialPerRack) return;
        if (money > costGuaranteedSpecialPerRack && TrySpend(costGuaranteedSpecialPerRack)) hasGuaranteedSpecialPerRack = true;
    }

    // ---- Ball - exclusive milestone pair: Hot Hand vs Slow Burn ----
    [Header("Ball - Hot Hand / Slow Burn")]
    [SerializeField] bool hasHotHand;
    [SerializeField] bool hasSlowBurn;
    [SerializeField] float slowBurnAmount = 0.02f;
    [SerializeField] int costHotHand = 600;
    [SerializeField] int costSlowBurn = 600;

    public bool HasHotHand => hasHotHand;
    public bool HasSlowBurn => hasSlowBurn;
    public float SlowBurnAmount => slowBurnAmount;

    public void BuyHotHand(int money)
    {
        if (!hasGuaranteedSpecialPerRack || hasHotHand || hasSlowBurn) return;
        if (money > costHotHand && TrySpend(costHotHand)) hasHotHand = true;
    }

    public void BuySlowBurn(int money)
    {
        if (!hasGuaranteedSpecialPerRack || hasHotHand || hasSlowBurn) return;
        if (money > costSlowBurn && TrySpend(costSlowBurn)) hasSlowBurn = true;
    }

    [Space(15)]

    // ---- Ball - tier 5: one mastery per special type ----
    [Header("Ball - Masteries")]
    [SerializeField] bool goldMastery, hotMastery, vaultMastery, wildMastery, glassMastery, moneyMastery;
    [SerializeField] int costGoldMastery = 300;
    [SerializeField] int costHotMastery = 300;
    [SerializeField] int costVaultMastery = 300;
    [SerializeField] int costWildMastery = 300;
    [SerializeField] int costGlassMastery = 300;
    [SerializeField] int costMoneyMastery = 300;

    public bool HasGoldMastery => goldMastery;
    public bool HasHotMastery => hotMastery;
    public bool HasVaultMastery => vaultMastery;
    public bool HasWildMastery => wildMastery;
    public bool HasGlassMastery => glassMastery;
    public bool HasMoneyMastery => moneyMastery;

    public void BuyGoldMastery(int money) { if (unlockedGold && !goldMastery && money > costGoldMastery && TrySpend(costGoldMastery)) goldMastery = true; }
    public void BuyHotMastery(int money) { if (unlockedHot && !hotMastery && money > costHotMastery && TrySpend(costHotMastery)) hotMastery = true; }
    public void BuyVaultMastery(int money) { if (unlockedVault && !vaultMastery && money > costVaultMastery && TrySpend(costVaultMastery)) vaultMastery = true; }
    public void BuyWildMastery(int money) { if (unlockedWild && !wildMastery && money > costWildMastery && TrySpend(costWildMastery)) wildMastery = true; }
    public void BuyGlassMastery(int money) { if (unlockedGlass && !glassMastery && money > costGlassMastery && TrySpend(costGlassMastery)) glassMastery = true; }
    public void BuyMoneyMastery(int money) { if (unlockedMoney && !moneyMastery && money > costMoneyMastery && TrySpend(costMoneyMastery)) moneyMastery = true; }

    [Header("Flair Upgrades")]
    [Space(15)]

    // ---- Flair - root: Crowd Pleaser ----
    // A ball at chain position n (0 = first ball hit) gains multiplierGain x (1 + level x bonus x n)
    [Header("Flair - Crowd Pleaser")]
    [SerializeField] int crowdPleaserLevel = 0;
    [SerializeField] float crowdPleaserBonusPerLevel = 0.25f;
    [SerializeField] int costCrowdPleaserLevel1 = 100;
    [SerializeField] int costCrowdPleaserLevel2 = 200;
    [SerializeField] int costCrowdPleaserLevel3 = 300;
    [SerializeField] int costCrowdPleaserLevel4 = 400;

    public float CrowdPleaserFactor(int chainPosition) => 1f + crowdPleaserLevel * crowdPleaserBonusPerLevel * Mathf.Max(0, chainPosition);

    public void BuyCrowdPleaserLevel1(int money) { if (crowdPleaserLevel == 0 && money > costCrowdPleaserLevel1 && TrySpend(costCrowdPleaserLevel1)) crowdPleaserLevel = 1; }
    public void BuyCrowdPleaserLevel2(int money) { if (crowdPleaserLevel == 1 && money > costCrowdPleaserLevel2 && TrySpend(costCrowdPleaserLevel2)) crowdPleaserLevel = 2; }
    public void BuyCrowdPleaserLevel3(int money) { if (crowdPleaserLevel == 2 && money > costCrowdPleaserLevel3 && TrySpend(costCrowdPleaserLevel3)) crowdPleaserLevel = 3; }
    public void BuyCrowdPleaserLevel4(int money) { if (crowdPleaserLevel == 3 && money > costCrowdPleaserLevel4 && TrySpend(costCrowdPleaserLevel4)) crowdPleaserLevel = 4; }

    [Space(15)]

    // ---- Flair - Endurance ----
    // Seconds added to the base night length, measured from the base (not stacked): +15 / +30 / +45 / +60.
    // Takes effect from the next night.
    [Header("Flair - Endurance")]
    [SerializeField] int enduranceLevel = 0;
    [SerializeField] int costEnduranceLevel1 = 150;
    [SerializeField] int costEnduranceLevel2 = 250;
    [SerializeField] int costEnduranceLevel3 = 400;
    [SerializeField] int costEnduranceLevel4 = 600;

    static readonly float[] EnduranceSecondsByLevel = { 0f, 15f, 30f, 45f, 60f };

    public void BuyEnduranceLevel1(int money) { if (crowdPleaserLevel >= 1 && enduranceLevel == 0 && money > costEnduranceLevel1 && TrySpend(costEnduranceLevel1)) enduranceLevel = 1; }
    public void BuyEnduranceLevel2(int money) { if (enduranceLevel == 1 && money > costEnduranceLevel2 && TrySpend(costEnduranceLevel2)) enduranceLevel = 2; }
    public void BuyEnduranceLevel3(int money) { if (enduranceLevel == 2 && money > costEnduranceLevel3 && TrySpend(costEnduranceLevel3)) enduranceLevel = 3; }
    public void BuyEnduranceLevel4(int money) { if (enduranceLevel == 3 && money > costEnduranceLevel4 && TrySpend(costEnduranceLevel4)) enduranceLevel = 4; }

    [Space(15)]

    // ---- Flair - Clean Sweep (off Endurance II) ----
    [Header("Flair - Clean Sweep")]
    [SerializeField] int cleanSweepLevel = 0;
    [SerializeField] int cleanSweepPayoutLevel1 = 100;
    [SerializeField] int cleanSweepPayoutLevel2 = 250;
    [SerializeField] int costCleanSweepLevel1 = 300;
    [SerializeField] int costCleanSweepLevel2 = 500;

    public int CleanSweepPayout => cleanSweepLevel >= 2 ? cleanSweepPayoutLevel2 : cleanSweepLevel == 1 ? cleanSweepPayoutLevel1 : 0;

    public void BuyCleanSweepLevel1(int money) { if (enduranceLevel >= 2 && cleanSweepLevel == 0 && money > costCleanSweepLevel1 && TrySpend(costCleanSweepLevel1)) cleanSweepLevel = 1; }
    public void BuyCleanSweepLevel2(int money) { if (cleanSweepLevel == 1 && money > costCleanSweepLevel2 && TrySpend(costCleanSweepLevel2)) cleanSweepLevel = 2; }

    [Space(15)]

    // ---- Flair - milestone: Run the Table (off Clean Sweep II or Head Start II) ----
    [Header("Flair - Milestone: Run the Table")]
    [SerializeField] bool hasRunTheTable;
    [SerializeField] float runTheTableMultPerRound = 0.25f;
    [SerializeField] int costRunTheTable = 800;

    public bool HasRunTheTable => hasRunTheTable;

    public void BuyRunTheTable(int money)
    {
        bool reachable = cleanSweepLevel >= 2 || headStartLevel >= 2;
        if (!reachable || hasRunTheTable) return;
        if (money > costRunTheTable && TrySpend(costRunTheTable)) hasRunTheTable = true;
    }

    // ---- Flair - exclusive pair: Second Wind vs Closer ----
    [Header("Flair - Second Wind / Closer")]
    [SerializeField] bool hasSecondWind;
    [SerializeField] bool hasCloser;
    [SerializeField] int secondWindMinChain = 5;
    [SerializeField] float secondWindSeconds = 5f;
    [SerializeField] float closerPayoutMultiplier = 2f;
    [SerializeField] int costSecondWind = 700;
    [SerializeField] int costCloser = 700;

    public bool HasSecondWind => hasSecondWind;
    public bool HasCloser => hasCloser;
    public int SecondWindMinChain => secondWindMinChain;
    public float SecondWindSeconds => secondWindSeconds;
    public float CloserPayoutMultiplier => closerPayoutMultiplier;

    public void BuySecondWind(int money)
    {
        if (!hasRunTheTable || hasSecondWind || hasCloser) return;
        if (money > costSecondWind && TrySpend(costSecondWind)) hasSecondWind = true;
    }

    public void BuyCloser(int money)
    {
        if (!hasRunTheTable || hasSecondWind || hasCloser) return;
        if (money > costCloser && TrySpend(costCloser)) hasCloser = true;
    }

    [Space(15)]

    // ---- Flair - Leftovers (twist, off Endurance III) -> Nothing to Lose (conditional) ----
    [Header("Flair - Leftovers / Nothing to Lose")]
    [SerializeField] bool hasLeftovers;
    [SerializeField] float leftoversFraction = 0.25f;
    [SerializeField] int costLeftovers = 400;
    [SerializeField] bool hasNothingToLose;
    [SerializeField] float nothingToLoseSeconds = 15f;
    [SerializeField] float nothingToLoseBonus = 1.5f;
    [SerializeField] int costNothingToLose = 500;

    public bool HasLeftovers => hasLeftovers;
    public float LeftoversFraction => leftoversFraction;
    public bool HasNothingToLose => hasNothingToLose;
    public float NothingToLoseSeconds => nothingToLoseSeconds;
    public float NothingToLoseBonus => nothingToLoseBonus;

    public void BuyLeftovers(int money)
    {
        if (enduranceLevel < 3 || hasLeftovers) return;
        if (money > costLeftovers && TrySpend(costLeftovers)) hasLeftovers = true;
    }

    public void BuyNothingToLose(int money)
    {
        if (!hasLeftovers || hasNothingToLose) return;
        if (money > costNothingToLose && TrySpend(costNothingToLose)) hasNothingToLose = true;
    }

    [Space(15)]

    // ---- Flair - Clean Pocket (C, the chain pocket bonus) ----
    // C multiplies the payout of any ball pocketed as part of a chain (2+ balls hit this turn).
    // Level 0 = 1.0, so nothing changes until it's bought.
    [Header("Flair - Clean Pocket")]
    [SerializeField] int cleanPocketLevel = 0;
    [SerializeField] int costCleanPocketLevel1 = 150;
    [SerializeField] int costCleanPocketLevel2 = 250;
    [SerializeField] int costCleanPocketLevel3 = 400;

    static readonly float[] ChainPocketBonusByLevel = { 1f, 1.25f, 1.5f, 1.75f };
    public float CurrentChainPocketBonus => ChainPocketBonusByLevel[Mathf.Clamp(cleanPocketLevel, 0, 3)];

    public void BuyCleanPocketLevel1(int money) { if (crowdPleaserLevel >= 1 && cleanPocketLevel == 0 && money > costCleanPocketLevel1 && TrySpend(costCleanPocketLevel1)) cleanPocketLevel = 1; }
    public void BuyCleanPocketLevel2(int money) { if (cleanPocketLevel == 1 && money > costCleanPocketLevel2 && TrySpend(costCleanPocketLevel2)) cleanPocketLevel = 2; }
    public void BuyCleanPocketLevel3(int money) { if (cleanPocketLevel == 2 && money > costCleanPocketLevel3 && TrySpend(costCleanPocketLevel3)) cleanPocketLevel = 3; }

    // ---- Flair - Cash Out (twist, off Clean Pocket II) -> Hot Streak (conditional) ----
    [Header("Flair - Cash Out / Hot Streak")]
    [SerializeField] bool hasCashOut;
    [SerializeField] int cashOutMinBalls = 3;
    [SerializeField] float cashOutMultiplier = 1.5f;
    [SerializeField] int costCashOut = 400;
    [SerializeField] bool hasHotStreak;
    [SerializeField] float hotStreakBonusPerShot = 0.1f;
    [SerializeField] int costHotStreak = 500;

    public bool HasCashOut => hasCashOut;
    public int CashOutMinBalls => cashOutMinBalls;
    public float CashOutMultiplier => cashOutMultiplier;
    public float HotStreakFactor => hasHotStreak ? 1f + hotStreak * hotStreakBonusPerShot : 1f;

    public void BuyCashOut(int money)
    {
        if (cleanPocketLevel < 2 || hasCashOut) return;
        if (money > costCashOut && TrySpend(costCashOut)) hasCashOut = true;
    }

    public void BuyHotStreak(int money)
    {
        if (!hasCashOut || hasHotStreak) return;
        if (money > costHotStreak && TrySpend(costHotStreak)) hasHotStreak = true;
    }

    [Space(15)]

    // ---- Flair - Long Chain (twist, off Crowd Pleaser III, needs a 5+ ball rack) ----
    // Tier thresholds are fractions of the balls on the table at the start of the turn (min 3 balls)
    [Header("Flair - Long Chain")]
    [SerializeField] bool hasLongChain;
    [SerializeField] int longChainMinBalls = 3;
    [SerializeField] int longChainMinRack = 5;
    [SerializeField] float longChainBonusPerTier = 0.5f;
    [SerializeField] int costLongChain = 500;

    static readonly float[] LongChainFractions = { 0.4f, 0.6f, 0.8f, 1f };

    public bool HasLongChain => hasLongChain;
    public float LongChainBonusPerTier => longChainBonusPerTier;

    public int LongChainTier(int chainLength, int ballsOnTable)
    {
        int tier = 0;
        foreach (float fraction in LongChainFractions)
            if (chainLength >= Mathf.Max(longChainMinBalls, Mathf.CeilToInt(fraction * ballsOnTable))) tier++;
        return tier;
    }

    public void BuyLongChain(int money)
    {
        if (crowdPleaserLevel < 3 || rackSize < longChainMinRack || hasLongChain) return;
        if (money > costLongChain && TrySpend(costLongChain)) hasLongChain = true;
    }

    [Space(15)]

    // ---- Flair - Head Start (off Crowd Pleaser II) ----
    [Header("Flair - Head Start")]
    [SerializeField] int headStartLevel = 0;
    [SerializeField] int costHeadStartLevel1 = 250;
    [SerializeField] int costHeadStartLevel2 = 400;

    static readonly float[] HeadStartByLevel = { 0f, 0.25f, 0.5f };

    public void BuyHeadStartLevel1(int money) { if (crowdPleaserLevel >= 2 && headStartLevel == 0 && money > costHeadStartLevel1 && TrySpend(costHeadStartLevel1)) headStartLevel = 1; }
    public void BuyHeadStartLevel2(int money) { if (headStartLevel == 1 && money > costHeadStartLevel2 && TrySpend(costHeadStartLevel2)) headStartLevel = 2; }

    // Extra M every object ball starts the round with: Head Start + Run the Table's per-cleared-round bonus
    public float StartingMultiplierBonus =>
        HeadStartByLevel[Mathf.Clamp(headStartLevel, 0, 2)] +
        (hasRunTheTable ? roundsClearedTonight * runTheTableMultPerRound : 0f);
}
