using System.Collections.Generic;
using UnityEngine;

// Holds the player's upgrade levels (and anything else that should survive a scene change, like
// currency). Pure data - no references to gameplay or scene objects, so it works the same whether
// it's read from the Upgrade Tree scene, the Lobby, or SampleScene.
//
// One instance exists for the whole run, created the first time any scene asks for it, and carried
// across every scene load via DontDestroyOnLoad. It does NOT currently save to disk, so progress
// resets when the game is closed - see the note on Load()/Save() below if that should change.
public class UpgradeProgress : MonoBehaviour
{
    public static UpgradeProgress Instance { get; private set; }

    public int aimGuideLevel = 0;
    public int powerLevel = 0;
    public int frictionLevel = 0;
    public int bouncyRailsLevel = 0;

    // Generic upgrade-tree progress: which UpgradeTreeData node ids have been bought, and which have
    // been permanently locked out by buying their exclusive-pair partner instead.
    readonly HashSet<string> ownedNodes = new HashSet<string>();
    readonly HashSet<string> excludedNodes = new HashSet<string>();

    public bool IsOwned(string nodeId) => ownedNodes.Contains(nodeId);
    public bool IsExcluded(string nodeId) => excludedNodes.Contains(nodeId);
    public void Own(string nodeId) => ownedNodes.Add(nodeId);
    public void Exclude(string nodeId) => excludedNodes.Add(nodeId);

    // Guarantees an instance exists before any scene's Awake() runs, regardless of which scene the
    // game (or the editor) actually starts from.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        Instance = new GameObject("UpgradeProgress").AddComponent<UpgradeProgress>();
    }

    void Awake()
    {
        // A second one can appear if a scene is opened directly in the editor after Bootstrap already
        // ran elsewhere; keep the first and discard the newcomer instead of having two sources of truth.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // To persist across separate play sessions (not just scene changes within one run), add
    // PlayerPrefs or JSON save/load calls here and call Load() from Bootstrap() / Save() whenever a
    // level changes. Left out for now since nothing asked for it yet.
}
