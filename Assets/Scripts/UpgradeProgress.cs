using System;
using System.Collections.Generic;
using UnityEngine;

// Holds the player's upgrade levels (and anything else that should survive a scene change, like
// currency). Pure data - no references to gameplay or scene objects, so it works the same whether
// it's read from the Upgrade Tree scene, the Lobby, or SampleScene.
//
// Lives on a GameObject placed in the Lobby scene (the hub every scene returns to - see
// GameEngine.ReturnToLobbyAfterDelay), not created automatically. DontDestroyOnLoad carries that one
// instance across every scene load for the rest of the run. It does NOT currently save to disk, so
// progress resets when the game is closed - see the note on Load()/Save() below if that should change.
public class UpgradeProgress : MonoBehaviour
{
    public static UpgradeProgress Instance { get; private set; }

    public int aimGuideLevel = 0;
    public int powerLevel = 0;
    public int frictionLevel = 0;
    public int bouncyRailsLevel = 0;
    public int steadyHandLevel = 0;

    // The shared currency spent in the Upgrade Tree, earned back in SampleScene. GameEngine seeds its
    // own local Money from this at Start() and pushes every payout back in via AddMoney, so the total
    // carries across the Lobby <-> SampleScene <-> Upgrade Tree scene loop.
    public int Money { get; private set; }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        Changed?.Invoke();
    }

    // Spends money if there's enough, and reports whether it succeeded
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > Money) return false;
        Money -= amount;
        Changed?.Invoke();
        return true;
    }

    // Generic upgrade-tree progress: which UpgradeTreeData node ids have been bought, and which have
    // been permanently locked out by buying their exclusive-pair partner instead.
    readonly HashSet<string> ownedNodes = new HashSet<string>();
    readonly HashSet<string> excludedNodes = new HashSet<string>();

    public bool IsOwned(string nodeId) => ownedNodes.Contains(nodeId);
    public bool IsExcluded(string nodeId) => excludedNodes.Contains(nodeId);
    public void Own(string nodeId) { ownedNodes.Add(nodeId); Changed?.Invoke(); }
    public void Exclude(string nodeId) { excludedNodes.Add(nodeId); Changed?.Invoke(); }

    // Raised after money or ownership changes, so tree UI (and a money readout) can refresh without
    // polling every frame. Node buttons other than the one just bought need this too, since buying one
    // node can change whether a sibling is still affordable.
    public static event Action Changed;

    void Awake()
    {
        // A second one can appear if the Lobby is reloaded (e.g. returning to it again later) while
        // the first instance is still alive from DontDestroyOnLoad; keep the first and discard the
        // newcomer instead of having two sources of truth.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // To persist across separate play sessions (not just scene changes within one run), add
    // PlayerPrefs or JSON save/load calls here and call Load() from Awake() / Save() whenever a level
    // changes. Left out for now since nothing asked for it yet.
}
