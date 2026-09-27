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
        if (def == null || progress == null)
        {
            Debug.LogWarning($"Can't buy \"{nodeId}\": {(def == null ? "no such node in UpgradeTreeData" : "there is no UpgradeProgress")}.");
            return;
        }
        if (progress.IsOwned(nodeId) || progress.IsExcluded(nodeId)) return;
        if (!IsUnlocked(def, progress))
        {
            Debug.Log($"Can't buy \"{def.label}\" yet: it's still locked (parent node not owned, or a prerequisite is missing).");
            return;
        }

        // Nodes with a real implementation buy through Upgrades, which charges its own cost from the
        // shared wallet and applies the effect. The rest have nothing to apply, so they're just paid for
        // at the tree's placeholder price and marked owned.
        Upgrades upgrades = Upgrades.Instance;
        bool bought;
        if (upgrades != null && upgrades.HasNodeBinding(nodeId))
        {
            bought = upgrades.TryBuyNode(nodeId);
        }
        else
        {
            int cost = UpgradeTreeData.CostOf(nodeId);
            bought = cost <= 0 || progress.TrySpend(cost);
        }
        if (!bought)
        {
            Debug.Log($"Couldn't buy \"{def.label}\": costs ${CostOf(nodeId)}, you have ${progress.Money} (or its own prerequisite isn't met).");
            return;
        }

        Debug.Log($"Bought \"{def.label}\" (${progress.Money} left).");
        progress.Own(nodeId);
        if (def.exclusiveWith != null) progress.Exclude(def.exclusiveWith);
        Refresh();
    }

    // What this node actually costs: the Buy method's own price when it has one, the tree's placeholder otherwise
    static int CostOf(string id)
    {
        Upgrades upgrades = Upgrades.Instance;
        return upgrades != null && upgrades.HasNodeBinding(id) ? upgrades.NodeCost(id) : UpgradeTreeData.CostOf(id);
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
            bool canAfford = progress.Money >= CostOf(nodeId);
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

        string costLine = progress != null && progress.IsOwned(nodeId) ? "Owned" : $"Cost: ${CostOf(nodeId)}";
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

        // Conditions the parent links can't express (e.g. needing both of two nodes, not either)
        if (Upgrades.Instance != null && !Upgrades.Instance.NodeRequirementsMet(def.id)) return false;

        if (def.parent == null) return true; // the centre itself
        if (progress.IsOwned(def.parent)) return true;
        if (def.extraParent != null && progress.IsOwned(def.extraParent)) return true;
        return false;
    }
}
