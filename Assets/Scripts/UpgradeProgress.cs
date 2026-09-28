using System;
using System.Collections.Generic;
using UnityEngine;

// Holds what should survive a scene change: the shared currency and which tree nodes are owned (the
// per-upgrade levels live on Upgrades). Pure data - no references to gameplay or scene objects, so it works the same whether
// it's read from the Upgrade Tree scene, the Lobby, or SampleScene.
//
// Created automatically before the first scene loads, so it exists whichever scene you press Play in
// (a copy placed in the Lobby scene just removes itself in Awake). DontDestroyOnLoad carries the one
// instance across every scene load for the rest of the run. It does NOT currently save to disk, so
// progress resets when the game is closed - see the note on Load()/Save() below if that should change.
public class UpgradeProgress : MonoBehaviour
{
    public static UpgradeProgress Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance()
    {
        Instance = null;
        stashed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing()
    {
        if (Instance != null) return;
        new GameObject("UpgradeProgress").AddComponent<UpgradeProgress>();
    }

    // Tutorial sandbox (see TutorialSession): the real wallet and tree progress are set aside and a
    // fresh empty one is used instead, then the real one comes back when the tutorial ends
    static UpgradeProgress stashed;

    public static void EnterSandbox()
    {
        if (stashed != null) return;
        stashed = Instance;
        if (stashed != null) stashed.gameObject.SetActive(false);
        Instance = null;
        new GameObject("UpgradeProgress (Tutorial)").AddComponent<UpgradeProgress>();
    }

    public static void ExitSandbox()
    {
        UpgradeProgress sandbox = Instance;
        Instance = stashed;
        if (Instance != null) Instance.gameObject.SetActive(true);
        stashed = null;
        if (sandbox != null && sandbox != Instance) Destroy(sandbox.gameObject);
    }

    // Lets anything that swaps the wallet (the tutorial sandbox) refresh every money readout and tree node
    public static void NotifyChanged() => Changed?.Invoke();

    public int OwnedCount => ownedNodes.Count;

    // Evicted (see Rent.StartNewRun): a fresh wallet and no owned nodes for the next run
    public static void ResetForNewRun()
    {
        if (Instance != null) Destroy(Instance.gameObject);
        Instance = null;
        new GameObject("UpgradeProgress").AddComponent<UpgradeProgress>();
        Changed?.Invoke();
    }

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
            Debug.Log($"[Diag] UpgradeProgress duplicate in scene '{gameObject.scene.name}' removed; keeping the existing one (${Instance.Money}).");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[Diag] UpgradeProgress is now the active instance (created in scene '{gameObject.scene.name}').");
    }

    // TEMPORARY diagnostics: says when the wallet goes away, and from where
    void OnDestroy()
    {
        if (Instance == this) Debug.LogWarning("[Diag] The active UpgradeProgress was destroyed.\n" + System.Environment.StackTrace);
    }

    // To persist across separate play sessions (not just scene changes within one run), add
    // PlayerPrefs or JSON save/load calls here and call Load() from Awake() / Save() whenever a level
    // changes. Left out for now since nothing asked for it yet.
}
