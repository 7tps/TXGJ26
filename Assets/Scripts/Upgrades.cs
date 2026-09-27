using UnityEngine;

public class Upgrades : MonoBehaviour
{
    [SerializeField] GameEngine gameEngine;
    [SerializeField] CueStick cueStick;
    [SerializeField] Ball ball;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (gameEngine == null)
        {
            gameEngine = FindFirstObjectByType<GameEngine>();
        }
        if (cueStick == null)
        {
            cueStick = FindFirstObjectByType<CueStick>();
        }
        if (ball == null)
        {
            ball = FindFirstObjectByType<Ball>();
        }
    }
    

    [Header("Cue Ball Upgrades")]
    [Space(15)]
    [Header("Cue - Aim Guide upgrade")]
    [SerializeField] int aimGuideLevel = 0; //starts at no guide, then incremental bounces
    [SerializeField] int maxAimGuideLevel = 5;

    public void UpgradeAimGuide()
    {
        if (aimGuideLevel >= maxAimGuideLevel) return;
        ApplyAimGuideLevel();
        aimGuideLevel++;
    }

    void ApplyAimGuideLevel()
    {
        gameEngine.GuideBounces = aimGuideLevel;
    }

    [Space(15)]
    [Header("Cue - Power")]
    [SerializeField] int powerLevel = 1; //starts at no guide, then incremental power
    [SerializeField] int maxPowerLevel = 5;

    public void UpgradeShotPower()
    {
        if (powerLevel >= maxPowerLevel)
        {
            return;
        }
        //need to implement cleanshot powertree

        ApplyPowerLevel();
        powerLevel++;
    }

    void ApplyPowerLevel()
    {
        cueStick.increaseMaxPull();
        cueStick.increaseMaxPower();
    }

    [Header("Table Upgrades")]
    [Space(15)]
    [Header("Table - Smooth Felt")]

    [SerializeField] int frictionLevel = 1; //starts at no guide, then incremental friction
    [SerializeField] int maxFrictionLevel = 4;

    public void UpgradeFrictionLevel()
    {
        if (frictionLevel >= maxFrictionLevel)
        {
            return;
        }
        //need to implement cleanshot powertree

        ApplyFrictionLevel();
        frictionLevel++;
    }

    void ApplyFrictionLevel()
    {
        ball.lowerDamping();
    }


    [Space(15)]
    [Header("Table - Lively Rails")]

    [SerializeField] float bounceIncreasePerLevel = 0.05f;
    [SerializeField] int bouncyRailsLevel = 0;
    [SerializeField] int maxBouncyRailsLevel = 4;

    public bool CanBuyBouncierRails => bouncyRailsLevel < maxBouncyRailsLevel;

    public void BuyBouncierRails()
    {
        if (!CanBuyBouncierRails) return;
        bouncyRailsLevel++;
        gameEngine.increaseCushionBounciness(bounceIncreasePerLevel);
    }
    // Update is called once per frame
    void Update()
    {
        
    }


}
