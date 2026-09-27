using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One button in the upgrade tree, driven entirely by its node id (looked up in UpgradeTreeData) and
// UpgradeProgress. Buying marks the node owned, locks its exclusive partner if it has one, and - only
// for the handful of nodes that map onto a mechanic the game actually has - applies the real effect
// through Upgrades. Everything else just becomes owned with no gameplay effect yet.
public class UpgradeTreeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] string nodeId;
    [SerializeField] Upgrades upgrades;
    [SerializeField] Button button;
    [SerializeField] Image frame;
    [SerializeField] TMP_Text nameText;
    [SerializeField] GameObject lockOverlay; // dims the icon and shows a lock over it while locked
    [SerializeField] Sprite defaultFrame;
    [SerializeField] Sprite milestoneFrame;

    static readonly Color OwnedTint = new Color(0.55f, 1f, 0.55f);
    static readonly Color LockedTextColor = new Color(0.55f, 0.55f, 0.55f);

    // Subscribed rather than polled: buying one node (or earning money back in SampleScene) can change
    // whether a sibling node is still affordable, so every node needs to hear about it, not just the
    // one that was clicked.
    void OnEnable()
    {
        UpgradeProgress.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => UpgradeProgress.Changed -= Refresh;

    // Wired to this node's Button.OnClick
    public void Buy()
    {
        UpgradeNodeDef def = UpgradeTreeData.Get(nodeId);
        UpgradeProgress progress = UpgradeProgress.Instance;
        if (def == null || progress == null) return;
        if (progress.IsOwned(nodeId) || progress.IsExcluded(nodeId) || !IsUnlocked(def, progress)) return;
        if (!progress.TrySpend(UpgradeTreeData.CostOf(nodeId))) return; // can't afford it yet

        progress.Own(nodeId);
        if (def.exclusiveWith != null) progress.Exclude(def.exclusiveWith);

        ApplyRealEffect(nodeId);
        Refresh();
    }

    // Nodes that map onto a mechanic GameEngine/CueStick/Ball actually has today. Buying one of these
    // bumps the matching level by one; GameEngine re-applies all four levels next time SampleScene
    // loads (see GameEngine.ApplyUpgrades). Everything not listed here has no effect yet - see the
    // class comment on UpgradeTreeData.
    void ApplyRealEffect(string id)
    {
        switch (id)
        {
            case "cue_chalk_up":
            case "cue_power_2":
            case "cue_power_3":
            case "cue_power_4":
            case "cue_power_5":
                upgrades.UpgradeShotPower();
                break;

            case "cue_aim_guide_1":
            case "cue_aim_guide_2":
            case "cue_aim_guide_3":
            case "cue_aim_guide_4":
            case "cue_seeing_the_table": // the milestone doubles as the guide's final step
                upgrades.UpgradeAimGuide();
                break;

            case "table_smooth_felt_1":
            case "table_smooth_felt_2":
            case "table_smooth_felt_3":
            case "table_smooth_felt_4":
                upgrades.UpgradeFrictionLevel();
                break;

            case "table_lively_rails_1":
            case "table_lively_rails_2":
            case "table_lively_rails_3":
            case "table_know_the_rails": // ditto - the milestone doubles as lively rails' final step
                upgrades.BuyBouncierRails();
                break;

            case "cue_steady_hand_1":
            case "cue_steady_hand_2":
                upgrades.UpgradeSteadyHand();
                break;
        }
    }

    public void Refresh()
    {
        UpgradeNodeDef def = UpgradeTreeData.Get(nodeId);
        UpgradeProgress progress = UpgradeProgress.Instance;
        if (def == null || progress == null) return;

        frame.sprite = def.type == UpgradeNodeType.Milestone || def.type == UpgradeNodeType.Capstone
            ? milestoneFrame
            : defaultFrame;

        // Selectable's SpriteSwap transition (used below for the locked/unlocked states) works by
        // setting Image.overrideSprite, not Image.sprite - so once a node has been shown locked, that
        // override keeps rendering on top of frame.sprite above and hides it, even after switching to
        // Transition.None, unless it's explicitly cleared here.
        frame.overrideSprite = null;

        if (progress.IsOwned(nodeId))
        {
            // Stays exactly as set here - no hover/disabled sprite swapping once it's bought.
            frame.color = OwnedTint;
            button.transition = Selectable.Transition.None;
            button.interactable = true; // harmless: Buy() is a no-op once already owned
            if (nameText != null) nameText.color = Color.white;
            if (lockOverlay != null) lockOverlay.SetActive(false);
        }
        else if (progress.IsExcluded(nodeId) || !IsUnlocked(def, progress))
        {
            frame.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap; // shows spriteState.disabledSprite (locked)
            button.interactable = false;
            if (nameText != null) nameText.color = LockedTextColor;
            if (lockOverlay != null) lockOverlay.SetActive(true);
        }
        else
        {
            // Unlocked by the tree, but still needs the node's cost overlay to say whether it's
            // actually affordable right now - reuses the locked text color as a "not yet" dim rather
            // than adding a fourth visual state.
            bool canAfford = progress.Money >= UpgradeTreeData.CostOf(nodeId);
            frame.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap;
            button.interactable = canAfford;
            if (nameText != null) nameText.color = canAfford ? Color.white : LockedTextColor;
            if (lockOverlay != null) lockOverlay.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        UpgradeNodeDef def = UpgradeTreeData.Get(nodeId);
        UpgradeProgress progress = UpgradeProgress.Instance;
        if (def == null || Tooltip.Instance == null) return;

        string costLine = progress != null && progress.IsOwned(nodeId) ? "Owned" : $"Cost: ${UpgradeTreeData.CostOf(nodeId)}";
        Tooltip.Instance.Show($"<b>{def.label}</b>\n{def.description}\n\n{costLine}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tooltip.Instance != null) Tooltip.Instance.Hide();
    }

    static bool IsUnlocked(UpgradeNodeDef def, UpgradeProgress progress)
    {
        if (def.id == UpgradeTreeData.CapstoneId)
        {
            foreach (string milestoneId in UpgradeTreeData.CapstoneRequires)
                if (!progress.IsOwned(milestoneId)) return false;
            return true;
        }

        if (def.parent == null) return true; // the centre itself
        if (progress.IsOwned(def.parent)) return true;
        if (def.extraParent != null && progress.IsOwned(def.extraParent)) return true;
        return false;
    }
}
