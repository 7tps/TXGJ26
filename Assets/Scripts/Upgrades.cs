using UnityEngine;

// Lives in the Upgrade Tree scene. Reads and writes the persisted upgrade levels on UpgradeProgress
// only - it has no reference to GameEngine/CueStick/Ball, since those don't exist outside SampleScene.
// GameEngine applies the current levels itself when that scene loads (see GameEngine.ApplyUpgrades).
public class Upgrades : MonoBehaviour
{
    UpgradeProgress Progress => UpgradeProgress.Instance;

    [Header("Cue Ball Upgrades")]
    [Space(15)]
    [Header("Cue - Aim Guide upgrade")]
    [SerializeField] int maxAimGuideLevel = 5;

    public bool CanUpgradeAimGuide => Progress.aimGuideLevel < maxAimGuideLevel;

    public void UpgradeAimGuide()
    {
        if (!CanUpgradeAimGuide) return;
        Progress.aimGuideLevel++;
    }

    [Space(15)]
    [Header("Cue - Power")]
    [SerializeField] int maxPowerLevel = 5;

    public bool CanUpgradeShotPower => Progress.powerLevel < maxPowerLevel;

    public void UpgradeShotPower()
    {
        if (!CanUpgradeShotPower) return;
        Progress.powerLevel++;
    }

    [Header("Table Upgrades")]
    [Space(15)]
    [Header("Table - Smooth Felt")]
    [SerializeField] int maxFrictionLevel = 4;

    public bool CanUpgradeFrictionLevel => Progress.frictionLevel < maxFrictionLevel;

    public void UpgradeFrictionLevel()
    {
        if (!CanUpgradeFrictionLevel) return;
        Progress.frictionLevel++;
    }

    [Space(15)]
    [Header("Table - Lively Rails")]
    [SerializeField] int maxBouncyRailsLevel = 4;

    public bool CanBuyBouncierRails => Progress.bouncyRailsLevel < maxBouncyRailsLevel;

    public void BuyBouncierRails()
    {
        if (!CanBuyBouncierRails) return;
        Progress.bouncyRailsLevel++;
    }
}
