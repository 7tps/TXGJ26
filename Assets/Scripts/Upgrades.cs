using System;
using System.Collections.Generic;
using UnityEngine;

public class Upgrades : MonoBehaviour
{
    // Persists across the Lobby <-> SampleScene reload, since purchases happen in the Upgrade Tree scene
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

    // Money lives on UpgradeProgress (the persistent wallet shared with the Upgrade Tree scene), not
    // here. Every Buy method below pays through this, so a missing UpgradeProgress just means
    // nothing can be bought, instead of a null reference.
    static bool Pay(int cost) => UpgradeProgress.Instance != null && UpgradeProgress.Instance.TrySpend(cost);

    // Run state for the current night, not purchases. Lives here because this is the only object
    // that survives the Menu <-> SampleScene reloads a night is made of.
    // The timer ticks here, in both scenes, so time spent in the shop between rounds counts too
    [Header("Night (run state)")]
    [SerializeField] float baseNightSeconds = 60f;
    [SerializeField] bool nightInProgress;
    [SerializeField] float nightTimeLeft;
    [SerializeField] float nightDuration; // this night's total, so the clock can show elapsed / total
    [SerializeField] bool feesDue; // the night ended and the table fees haven't been paid yet - locks Main and the Upgrade Tree (see NightClock)
    [SerializeField] int nightsPaid; // table fees paid so far this run - each one raises the next bill
    [SerializeField] int roundsClearedTonight;
    [SerializeField] int hotStreak;

    public float NightTimeLeft => nightTimeLeft;
    public bool NightTimeUp => nightInProgress && nightTimeLeft <= 0f;
    public bool NightInProgress => nightInProgress;
    public bool FeesDue => feesDue;
    public int RoundsClearedTonight => roundsClearedTonight;
    public int HotStreak => hotStreak;
    public float NightLength => baseNightSeconds + EnduranceSecondsByLevel[Mathf.Clamp(enduranceLevel, 0, 4)];

    // 0 at the start of a night up to 1 when it runs out, which is what the clock sprites are drawn from.
    // Stays at 1 while the fees are due, and at 0 between the fees and the next night.
    public float NightProgress =>
        feesDue ? 1f :
        nightInProgress && nightDuration > 0f ? Mathf.Clamp01(1f - nightTimeLeft / nightDuration) : 0f;

    void Update()
    {
        if (nightInProgress && nightTimeLeft > 0f)
            nightTimeLeft = Mathf.Max(0f, nightTimeLeft - Time.deltaTime);
    }

    // A fresh night with a full timer. The night spans the Lobby, Main and Upgrade Tree scenes, so
    // this is not called on every scene load - see BeginNightIfNeeded
    public void StartNewNight()
    {
        nightInProgress = true;
        nightDuration = NightLength;
        nightTimeLeft = nightDuration;
        feesDue = false;
        roundsClearedTonight = 0;
        hotStreak = 0;
    }

    // Starts a night only if one isn't already running and the last one's fees aren't still owed
    public void BeginNightIfNeeded()
    {
        if (!nightInProgress && !feesDue) StartNewNight();
    }

    public void AddTime(float seconds)
    {
        if (!nightInProgress || seconds <= 0f) return;
        nightTimeLeft += seconds;
        nightDuration += seconds;
    }

    public void RecordRoundCleared() { roundsClearedTonight++; }
    public void RecordShotResult(bool pocketedSomething) { hotStreak = pocketedSomething ? hotStreak + 1 : 0; }

    public void EndNight()
    {
        nightInProgress = false;
        nightTimeLeft = 0f;
        feesDue = true;
    }

    // Lifts the Main / Upgrade Tree lock. PayTableFee calls this once the bill is paid
    public void ClearFeesLock() { feesDue = false; }

    // ---- Night - Table Fees ----
    // After every night the bar bills you for the table before you can play again, and every bill paid
    // makes the next one bigger: baseTableFee x tableFeeGrowth ^ nightsPaid (20, 30, 45, 68, 101...).
    // Not paying loses the run (see TableFees).
    [Header("Night - Table Fees")]
    [SerializeField] int baseTableFee = 20;
    [SerializeField] float tableFeeGrowth = 1.5f;

    public int NightsPaid => nightsPaid;
    public int CurrentTableFee => Mathf.RoundToInt(baseTableFee * Mathf.Pow(tableFeeGrowth, nightsPaid));
    public bool CanAffordTableFee => UpgradeProgress.Instance != null && UpgradeProgress.Instance.Money >= CurrentTableFee;

    // Pays tonight's bill if it's due and affordable, and reports whether it went through
    public bool PayTableFee()
    {
        if (!feesDue || !Pay(CurrentTableFee)) return false;
        nightsPaid++;
        ClearFeesLock();
        return true;
    }

    // Losing the run (walking out on the table fees) throws away every upgrade and all the night state:
    // the old instance is destroyed and a fresh one with the defaults takes its place
    public static void ResetRun()
    {
        if (Instance != null) Destroy(Instance.gameObject);
        Instance = null;
        CreateIfMissing();
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

    public void BuyAimGuideLevel1() { if (aimGuideLevel == 0 && Pay(costAimGuideLevel1)) aimGuideLevel = 1; }
    public void BuyAimGuideLevel2() { if (aimGuideLevel == 1 && Pay(costAimGuideLevel2)) aimGuideLevel = 2; }
    public void BuyAimGuideLevel3() { if (aimGuideLevel == 2 && Pay(costAimGuideLevel3)) aimGuideLevel = 3; }
    public void BuyAimGuideLevel4() { if (aimGuideLevel == 3 && Pay(costAimGuideLevel4)) aimGuideLevel = 4; }
    public void BuyAimGuideLevel5() { if (aimGuideLevel == 4 && Pay(costAimGuideLevel5)) aimGuideLevel = 5; }

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

    public void BuyPowerLevel2() { if (powerLevel == 1 && Pay(costPowerLevel2)) powerLevel = 2; }
    public void BuyPowerLevel3() { if (powerLevel == 2 && Pay(costPowerLevel3)) powerLevel = 3; }

    // Exclusive with BuyBreakshot: only one of the two can ever be bought
    public void BuyPowerLevelIV()
    {
        if (powerLevel != 3 || tookPowerIV || hasBreakshot) return;
        if (Pay(costPowerLevelIV)) { tookPowerIV = true; powerLevel = 4; }
    }

    // Exclusive with BuyPowerLevelIV. hasBreakshot only sets the flag - the +50% first-shot-of-round
    // bonus still needs CueStick to check it, which isn't wired up yet.
    public void BuyBreakshot()
    {
        if (powerLevel != 3 || tookPowerIV || hasBreakshot) return;
        if (Pay(costBreakshot)) hasBreakshot = true;
    }

    public void BuyPowerLevelV()
    {
        bool tierFourResolved = tookPowerIV || hasBreakshot;
        if (!tierFourResolved || powerLevel >= 5) return;
        if (Pay(costPowerLevelV))
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

    public void BuySteadyHandLevel1() { if (powerLevel >= 2 && steadyHandLevel == 0 && Pay(costSteadyHandLevel1)) steadyHandLevel = 1; }
    public void BuySteadyHandLevel2() { if (steadyHandLevel == 1 && Pay(costSteadyHandLevel2)) steadyHandLevel = 2; }
    public void BuySteadyHandLevel3() { if (steadyHandLevel == 2 && Pay(costSteadyHandLevel3)) steadyHandLevel = 3; }

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

    public void BuyFrictionLevel2() { if (frictionLevel == 1 && Pay(costFrictionLevel2)) frictionLevel = 2; }
    public void BuyFrictionLevel3() { if (frictionLevel == 2 && Pay(costFrictionLevel3)) frictionLevel = 3; }
    public void BuyFrictionLevel4() { if (frictionLevel == 3 && Pay(costFrictionLevel4)) frictionLevel = 4; }

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

    public void BuyBouncyRailsLevel1() { if (bouncyRailsLevel == 0 && Pay(costBouncyRailsLevel1)) bouncyRailsLevel = 1; }
    public void BuyBouncyRailsLevel2() { if (bouncyRailsLevel == 1 && Pay(costBouncyRailsLevel2)) bouncyRailsLevel = 2; }
    public void BuyBouncyRailsLevel3() { if (bouncyRailsLevel == 2 && Pay(costBouncyRailsLevel3)) bouncyRailsLevel = 3; }
    public void BuyBouncyRailsLevel4() { if (bouncyRailsLevel == 3 && Pay(costBouncyRailsLevel4)) bouncyRailsLevel = 4; }

    [Header("Ball Upgrades")]
    [Space(15)]

    // ---- Ball - tier 1: Ball Value ----
    [Header("Ball - Ball Value")]
    [SerializeField] int ballValueLevel = 1;
    [SerializeField] int maxBallValueLevel = 5;
    [SerializeField] int ballValueBonusPerLevel = 10;
    [SerializeField] int costBallValue = 150;

    public int ApplyBallValueBonus(int baseValue) => baseValue + ballValueBonusPerLevel * (ballValueLevel - 1);

    public void BuyBallValue()
    {
        if (ballValueLevel >= maxBallValueLevel) return;
        if (Pay(costBallValue)) ballValueLevel++;
    }

    [Space(15)]

    // ---- Ball - tier 2: Expanded Rack ----
    // Starts at 1 object ball (per the lore - you can't handle more yet), grows toward the normal 15
    [Header("Ball - Expanded Rack")]
    [SerializeField] int rackSize = 1;
    [SerializeField] int costPerRackBall = 80;

    public int RackSize => rackSize;

    public void BuyExpandedRack()
    {
        if (rackSize >= 15) return;
        int cost = costPerRackBall * rackSize;
        if (Pay(cost)) rackSize++;
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

    public void BuyLuckyBall()
    {
        if (luckyBallLevel >= maxLuckyBallLevel) return;
        if (Pay(costLuckyBall)) luckyBallLevel++;
    }

    [Space(15)]

    // ---- Ball - tier 3: "name tbd" - raises the spawn chance further, on top of Lucky Ball ----
    [Header("Ball - Special Chance Boost")]
    [SerializeField] int specialChanceBoostLevel = 0;
    [SerializeField] int maxSpecialChanceBoostLevel = 3;
    [SerializeField] float specialChanceBoostPerLevel = 0.05f;
    [SerializeField] int costSpecialChanceBoost = 250;

    public void BuySpecialChanceBoost()
    {
        if (!HasLuckyBall || specialChanceBoostLevel >= maxSpecialChanceBoostLevel) return;
        if (Pay(costSpecialChanceBoost)) specialChanceBoostLevel++;
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

    public void BuyUnlockGold() { if (HasLuckyBall && !unlockedGold && Pay(costUnlockGold)) unlockedGold = true; }
    public void BuyUnlockHot() { if (HasLuckyBall && !unlockedHot && Pay(costUnlockHot)) unlockedHot = true; }
    public void BuyUnlockVault() { if (HasLuckyBall && !unlockedVault && Pay(costUnlockVault)) unlockedVault = true; }
    public void BuyUnlockWild() { if (HasLuckyBall && !unlockedWild && Pay(costUnlockWild)) unlockedWild = true; }
    public void BuyUnlockGlass() { if (HasLuckyBall && !unlockedGlass && Pay(costUnlockGlass)) unlockedGlass = true; }
    public void BuyUnlockMoney() { if (HasLuckyBall && !unlockedMoney && Pay(costUnlockMoney)) unlockedMoney = true; }

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
            if (UnityEngine.Random.value > chance) return Ball.SpecialType.None;
        }

        return unlocked[UnityEngine.Random.Range(0, unlocked.Count)];
    }

    [Space(15)]

    // ---- Ball - tier 3: Extra Cue Ball ----
    [Header("Ball - Extra Cue Ball")]
    [SerializeField] bool hasExtraCueBall;
    [SerializeField] int costExtraCueBall = 500;

    public bool HasExtraCueBall => hasExtraCueBall;

    public void BuyExtraCueBall()
    {
        if (hasExtraCueBall) return;
        if (Pay(costExtraCueBall)) hasExtraCueBall = true;
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

    public void BuyDoubleMultOnMergedChains()
    {
        if (!hasExtraCueBall || hasDoubleMultOnMergedChains) return;
        if (Pay(costDoubleMultOnMergedChains)) hasDoubleMultOnMergedChains = true;
    }

    public void BuySpecialComboBonus()
    {
        if (hasSpecialComboBonus) return;
        if (Pay(costSpecialComboBonus)) hasSpecialComboBonus = true;
    }

    [Space(15)]

    // ---- Ball - milestone: guaranteed special per rack ----
    [Header("Ball - Milestone: Guaranteed Special")]
    [SerializeField] bool hasGuaranteedSpecialPerRack;
    [SerializeField] int costGuaranteedSpecialPerRack = 600;

    public bool HasGuaranteedSpecialPerRack => hasGuaranteedSpecialPerRack;

    public void BuyGuaranteedSpecialPerRack()
    {
        bool tier4Complete = hasDoubleMultOnMergedChains && hasSpecialComboBonus;
        if (!tier4Complete || hasGuaranteedSpecialPerRack) return;
        if (Pay(costGuaranteedSpecialPerRack)) hasGuaranteedSpecialPerRack = true;
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

    public void BuyHotHand()
    {
        if (!hasGuaranteedSpecialPerRack || hasHotHand || hasSlowBurn) return;
        if (Pay(costHotHand)) hasHotHand = true;
    }

    public void BuySlowBurn()
    {
        if (!hasGuaranteedSpecialPerRack || hasHotHand || hasSlowBurn) return;
        if (Pay(costSlowBurn)) hasSlowBurn = true;
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

    public void BuyGoldMastery() { if (unlockedGold && !goldMastery && Pay(costGoldMastery)) goldMastery = true; }
    public void BuyHotMastery() { if (unlockedHot && !hotMastery && Pay(costHotMastery)) hotMastery = true; }
    public void BuyVaultMastery() { if (unlockedVault && !vaultMastery && Pay(costVaultMastery)) vaultMastery = true; }
    public void BuyWildMastery() { if (unlockedWild && !wildMastery && Pay(costWildMastery)) wildMastery = true; }
    public void BuyGlassMastery() { if (unlockedGlass && !glassMastery && Pay(costGlassMastery)) glassMastery = true; }
    public void BuyMoneyMastery() { if (unlockedMoney && !moneyMastery && Pay(costMoneyMastery)) moneyMastery = true; }

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

    public void BuyCrowdPleaserLevel1() { if (crowdPleaserLevel == 0 && Pay(costCrowdPleaserLevel1)) crowdPleaserLevel = 1; }
    public void BuyCrowdPleaserLevel2() { if (crowdPleaserLevel == 1 && Pay(costCrowdPleaserLevel2)) crowdPleaserLevel = 2; }
    public void BuyCrowdPleaserLevel3() { if (crowdPleaserLevel == 2 && Pay(costCrowdPleaserLevel3)) crowdPleaserLevel = 3; }
    public void BuyCrowdPleaserLevel4() { if (crowdPleaserLevel == 3 && Pay(costCrowdPleaserLevel4)) crowdPleaserLevel = 4; }

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

    public void BuyEnduranceLevel1() { if (crowdPleaserLevel >= 1 && enduranceLevel == 0 && Pay(costEnduranceLevel1)) enduranceLevel = 1; }
    public void BuyEnduranceLevel2() { if (enduranceLevel == 1 && Pay(costEnduranceLevel2)) enduranceLevel = 2; }
    public void BuyEnduranceLevel3() { if (enduranceLevel == 2 && Pay(costEnduranceLevel3)) enduranceLevel = 3; }
    public void BuyEnduranceLevel4() { if (enduranceLevel == 3 && Pay(costEnduranceLevel4)) enduranceLevel = 4; }

    [Space(15)]

    // ---- Flair - Clean Sweep (off Endurance II) ----
    [Header("Flair - Clean Sweep")]
    [SerializeField] int cleanSweepLevel = 0;
    [SerializeField] int cleanSweepPayoutLevel1 = 100;
    [SerializeField] int cleanSweepPayoutLevel2 = 250;
    [SerializeField] int costCleanSweepLevel1 = 300;
    [SerializeField] int costCleanSweepLevel2 = 500;

    public int CleanSweepPayout => cleanSweepLevel >= 2 ? cleanSweepPayoutLevel2 : cleanSweepLevel == 1 ? cleanSweepPayoutLevel1 : 0;

    public void BuyCleanSweepLevel1() { if (enduranceLevel >= 2 && cleanSweepLevel == 0 && Pay(costCleanSweepLevel1)) cleanSweepLevel = 1; }
    public void BuyCleanSweepLevel2() { if (cleanSweepLevel == 1 && Pay(costCleanSweepLevel2)) cleanSweepLevel = 2; }

    [Space(15)]

    // ---- Flair - milestone: Run the Table (off Clean Sweep II or Head Start II) ----
    [Header("Flair - Milestone: Run the Table")]
    [SerializeField] bool hasRunTheTable;
    [SerializeField] float runTheTableMultPerRound = 0.25f;
    [SerializeField] int costRunTheTable = 800;

    public bool HasRunTheTable => hasRunTheTable;

    public void BuyRunTheTable()
    {
        bool reachable = cleanSweepLevel >= 2 || headStartLevel >= 2;
        if (!reachable || hasRunTheTable) return;
        if (Pay(costRunTheTable)) hasRunTheTable = true;
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

    public void BuySecondWind()
    {
        if (!hasRunTheTable || hasSecondWind || hasCloser) return;
        if (Pay(costSecondWind)) hasSecondWind = true;
    }

    public void BuyCloser()
    {
        if (!hasRunTheTable || hasSecondWind || hasCloser) return;
        if (Pay(costCloser)) hasCloser = true;
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

    public void BuyLeftovers()
    {
        if (enduranceLevel < 3 || hasLeftovers) return;
        if (Pay(costLeftovers)) hasLeftovers = true;
    }

    public void BuyNothingToLose()
    {
        if (!hasLeftovers || hasNothingToLose) return;
        if (Pay(costNothingToLose)) hasNothingToLose = true;
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

    public void BuyCleanPocketLevel1() { if (crowdPleaserLevel >= 1 && cleanPocketLevel == 0 && Pay(costCleanPocketLevel1)) cleanPocketLevel = 1; }
    public void BuyCleanPocketLevel2() { if (cleanPocketLevel == 1 && Pay(costCleanPocketLevel2)) cleanPocketLevel = 2; }
    public void BuyCleanPocketLevel3() { if (cleanPocketLevel == 2 && Pay(costCleanPocketLevel3)) cleanPocketLevel = 3; }

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

    public void BuyCashOut()
    {
        if (cleanPocketLevel < 2 || hasCashOut) return;
        if (Pay(costCashOut)) hasCashOut = true;
    }

    public void BuyHotStreak()
    {
        if (!hasCashOut || hasHotStreak) return;
        if (Pay(costHotStreak)) hasHotStreak = true;
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

    public void BuyLongChain()
    {
        if (crowdPleaserLevel < 3 || rackSize < longChainMinRack || hasLongChain) return;
        if (Pay(costLongChain)) hasLongChain = true;
    }

    [Space(15)]

    // ---- Flair - Head Start (off Crowd Pleaser II) ----
    [Header("Flair - Head Start")]
    [SerializeField] int headStartLevel = 0;
    [SerializeField] int costHeadStartLevel1 = 250;
    [SerializeField] int costHeadStartLevel2 = 400;

    static readonly float[] HeadStartByLevel = { 0f, 0.25f, 0.5f };

    public void BuyHeadStartLevel1() { if (crowdPleaserLevel >= 2 && headStartLevel == 0 && Pay(costHeadStartLevel1)) headStartLevel = 1; }
    public void BuyHeadStartLevel2() { if (headStartLevel == 1 && Pay(costHeadStartLevel2)) headStartLevel = 2; }

    // Extra M every object ball starts the round with: Head Start + Run the Table's per-cleared-round bonus
    public float StartingMultiplierBonus =>
        HeadStartByLevel[Mathf.Clamp(headStartLevel, 0, 2)] +
        (hasRunTheTable ? roundsClearedTonight * runTheTableMultPerRound : 0f);

    // ---- Upgrade Tree bindings ----
    // Maps an UpgradeTreeData node id onto the Buy method that implements it, so the tree scene buys
    // through the same code (and the same costs and prerequisites) as everything above instead of
    // pricing nodes itself. Nodes not listed here have no implementation yet and stay on the tree's
    // placeholder cost. A cost of 0 with no Buy is a free base tier (Power I, Smooth Felt I).
    class NodeBinding
    {
        public Func<int> Cost;
        public Action Buy;
        public Func<bool> Requires; // conditions the tree's single-parent links can't express, if any
    }

    Dictionary<string, NodeBinding> nodeBindings;

    void Bind(string id, Func<int> cost, Action buy, Func<bool> requires = null)
    {
        nodeBindings[id] = new NodeBinding { Cost = cost, Buy = buy, Requires = requires };
    }

    Dictionary<string, NodeBinding> NodeBindings
    {
        get
        {
            if (nodeBindings == null)
            {
                nodeBindings = new Dictionary<string, NodeBinding>();
                BuildNodeBindings();
            }
            return nodeBindings;
        }
    }

    void BuildNodeBindings()
    {
        // Cue
        Bind("cue_chalk_up", () => 0, null); // Power I: the free base level
        Bind("cue_power_2", () => costPowerLevel2, BuyPowerLevel2);
        Bind("cue_power_3", () => costPowerLevel3, BuyPowerLevel3);
        Bind("cue_power_4", () => costPowerLevelIV, BuyPowerLevelIV);
        Bind("cue_break_shot", () => costBreakshot, BuyBreakshot);
        Bind("cue_power_5", () => costPowerLevelV, BuyPowerLevelV);
        Bind("cue_steady_hand_1", () => costSteadyHandLevel1, BuySteadyHandLevel1);
        Bind("cue_steady_hand_2", () => costSteadyHandLevel2, BuySteadyHandLevel2);
        Bind("cue_aim_guide_1", () => costAimGuideLevel1, BuyAimGuideLevel1);
        Bind("cue_aim_guide_2", () => costAimGuideLevel2, BuyAimGuideLevel2);
        Bind("cue_aim_guide_3", () => costAimGuideLevel3, BuyAimGuideLevel3);
        Bind("cue_aim_guide_4", () => costAimGuideLevel4, BuyAimGuideLevel4);
        Bind("cue_seeing_the_table", () => costAimGuideLevel5, BuyAimGuideLevel5); // the milestone is the guide's last level

        // Table
        Bind("table_smooth_felt_1", () => 0, null); // level 1: the free base
        Bind("table_smooth_felt_2", () => costFrictionLevel2, BuyFrictionLevel2);
        Bind("table_smooth_felt_3", () => costFrictionLevel3, BuyFrictionLevel3);
        Bind("table_smooth_felt_4", () => costFrictionLevel4, BuyFrictionLevel4);
        Bind("table_lively_rails_1", () => costBouncyRailsLevel1, BuyBouncyRailsLevel1);
        Bind("table_lively_rails_2", () => costBouncyRailsLevel2, BuyBouncyRailsLevel2);
        Bind("table_lively_rails_3", () => costBouncyRailsLevel3, BuyBouncyRailsLevel3);
        Bind("table_know_the_rails", () => costBouncyRailsLevel4, BuyBouncyRailsLevel4); // the milestone is the rails' last level

        // Ball: rack size (each node raises the rack to its total, paying for every ball in between)
        Bind("ball_second", () => RackCostTo(2), () => BuyRackTo(2));
        Bind("ball_third_ball", () => RackCostTo(3), () => BuyRackTo(3));
        Bind("ball_small_rack", () => RackCostTo(5), () => BuyRackTo(5));
        Bind("ball_half_rack", () => RackCostTo(7), () => BuyRackTo(7));
        Bind("ball_big_rack", () => RackCostTo(10), () => BuyRackTo(10));
        Bind("ball_full_rack", () => RackCostTo(15), () => BuyRackTo(15));

        // Ball: value, specials, extra cue ball
        Bind("ball_warm_up", () => costBallValue, BuyBallValue);
        Bind("ball_value_2", () => costBallValue, BuyBallValue);
        Bind("ball_value_3", () => costBallValue, BuyBallValue);
        Bind("ball_value_4", () => costBallValue, BuyBallValue);
        Bind("ball_lucky_break", () => costLuckyBall + costUnlockGold, BuyLuckyBreak); // Lucky Ball + the Gold unlock together
        Bind("ball_loaded_dice_1", () => costSpecialChanceBoost, BuySpecialChanceBoost);
        Bind("ball_loaded_dice_2", () => costSpecialChanceBoost, BuySpecialChanceBoost);
        Bind("ball_loaded_dice_3", () => costSpecialChanceBoost, BuySpecialChanceBoost);
        Bind("ball_hot_ball", () => costUnlockHot, BuyUnlockHot);
        Bind("ball_vault_ball", () => costUnlockVault, BuyUnlockVault);
        Bind("ball_wild_ball", () => costUnlockWild, BuyUnlockWild);
        Bind("ball_glass_ball", () => costUnlockGlass, BuyUnlockGlass);
        Bind("ball_money_ball", () => costUnlockMoney, BuyUnlockMoney);
        Bind("ball_extra_cue_ball", () => costExtraCueBall, BuyExtraCueBall);
        Bind("ball_tandem", () => costDoubleMultOnMergedChains, BuyDoubleMultOnMergedChains);
        Bind("ball_collector", () => costSpecialComboBonus, BuySpecialComboBonus);
        Bind("ball_reading_the_rack", () => costGuaranteedSpecialPerRack, BuyGuaranteedSpecialPerRack,
            () => hasDoubleMultOnMergedChains && hasSpecialComboBonus); // needs both tier-4 nodes, not either
        Bind("ball_hot_hand", () => costHotHand, BuyHotHand);
        Bind("ball_slow_burn", () => costSlowBurn, BuySlowBurn);

        // Flair
        Bind("flair_crowd_pleaser_1", () => costCrowdPleaserLevel1, BuyCrowdPleaserLevel1);
        Bind("flair_crowd_pleaser_2", () => costCrowdPleaserLevel2, BuyCrowdPleaserLevel2);
        Bind("flair_crowd_pleaser_3", () => costCrowdPleaserLevel3, BuyCrowdPleaserLevel3);
        Bind("flair_crowd_pleaser_4", () => costCrowdPleaserLevel4, BuyCrowdPleaserLevel4);
        Bind("flair_endurance_1", () => costEnduranceLevel1, BuyEnduranceLevel1);
        Bind("flair_endurance_2", () => costEnduranceLevel2, BuyEnduranceLevel2);
        Bind("flair_endurance_3", () => costEnduranceLevel3, BuyEnduranceLevel3);
        Bind("flair_endurance_4", () => costEnduranceLevel4, BuyEnduranceLevel4);
        Bind("flair_clean_sweep_1", () => costCleanSweepLevel1, BuyCleanSweepLevel1);
        Bind("flair_clean_sweep_2", () => costCleanSweepLevel2, BuyCleanSweepLevel2);
        Bind("flair_run_the_table", () => costRunTheTable, BuyRunTheTable);
        Bind("flair_second_wind", () => costSecondWind, BuySecondWind);
        Bind("flair_closer", () => costCloser, BuyCloser);
        Bind("flair_leftovers", () => costLeftovers, BuyLeftovers);
        Bind("flair_nothing_to_lose", () => costNothingToLose, BuyNothingToLose);
        Bind("flair_clean_pocket_1", () => costCleanPocketLevel1, BuyCleanPocketLevel1);
        Bind("flair_clean_pocket_2", () => costCleanPocketLevel2, BuyCleanPocketLevel2);
        Bind("flair_clean_pocket_3", () => costCleanPocketLevel3, BuyCleanPocketLevel3);
        Bind("flair_cash_out", () => costCashOut, BuyCashOut);
        Bind("flair_hot_streak", () => costHotStreak, BuyHotStreak);
        Bind("flair_long_chain", () => costLongChain, BuyLongChain, () => rackSize >= longChainMinRack);
        Bind("flair_head_start_1", () => costHeadStartLevel1, BuyHeadStartLevel1);
        Bind("flair_head_start_2", () => costHeadStartLevel2, BuyHeadStartLevel2);
    }

    // Lucky Break in the tree is Lucky Ball level 1 and the Gold unlock as a single node
    public void BuyLuckyBreak()
    {
        if (luckyBallLevel > 0 || unlockedGold) return;
        if (Pay(costLuckyBall + costUnlockGold)) { luckyBallLevel = 1; unlockedGold = true; }
    }

    // Same per-ball price as BuyExpandedRack, paid for every ball from the current size up to target
    int RackCostTo(int target)
    {
        int total = 0;
        for (int size = rackSize; size < target; size++) total += costPerRackBall * size;
        return total;
    }

    public void BuyRackTo(int target)
    {
        if (target <= rackSize || target > 15) return;
        if (Pay(RackCostTo(target))) rackSize = target;
    }

    public bool HasNodeBinding(string nodeId) => NodeBindings.ContainsKey(nodeId);

    // The price the tree should show and charge for this node, from the Buy method's own cost
    public int NodeCost(string nodeId) => NodeBindings.TryGetValue(nodeId, out NodeBinding b) ? b.Cost() : 0;

    // Extra prerequisites beyond the tree's parent links (e.g. Reading the Rack needs both tier-4 nodes)
    public bool NodeRequirementsMet(string nodeId) =>
        !NodeBindings.TryGetValue(nodeId, out NodeBinding b) || b.Requires == null || b.Requires();

    // Runs the node's Buy method. Returns true if it went through: a free node always does, otherwise
    // it's judged by whether the Buy method actually took money (it refuses silently when its own
    // prerequisites or the balance aren't met).
    public bool TryBuyNode(string nodeId)
    {
        if (!NodeBindings.TryGetValue(nodeId, out NodeBinding b) || !NodeRequirementsMet(nodeId)) return false;
        if (b.Buy == null) return true;

        UpgradeProgress wallet = UpgradeProgress.Instance;
        if (wallet == null) return false;

        int before = wallet.Money;
        b.Buy();
        return wallet.Money < before;
    }
}
