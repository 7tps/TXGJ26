using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The night's clock: draws the progress clock in the Lobby, Main and Upgrade Tree scenes, and enforces
// what happens when the night runs out. The time itself lives on Upgrades (NightLength, NightTimeLeft),
// because that's what the rest of the game already reads.
//
// A night is one 60s timer (Upgrades.NightLength) that keeps running as you move between the Lobby,
// Main and Upgrade Tree. When it hits 0 the player is sent back to the Lobby and Main / Upgrade Tree stay
// locked until the fees are paid in the Table Fees scene (see TableFees), which in turn can only be entered
// once the night is over. Loading a clock scene with no night running (and no fees owed) starts the next one.
//
// Like UpgradeProgress this creates itself before the first scene loads and survives scene changes, and it
// builds its own overlay UI, so nothing has to be placed in any scene.
public class NightClock : MonoBehaviour
{
    public const string LobbyScene = "Lobby";
    public const string MainScene = "Main";
    public const string UpgradeTreeScene = "Upgrade Tree";
    public const string TableFeesScene = "Table Fees";

    // UI layout (canvas is 1920x1080 reference, so these are in those units)
    const float ClockSize = 96f;
    const float ClockTopMargin = 20f;

    public static NightClock Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing()
    {
        if (Instance != null) return;
        new GameObject("NightClock").AddComponent<NightClock>();
    }

    // Scenes that show the clock and count as part of the night
    static bool IsNightScene(string scene) => scene == LobbyScene || scene == MainScene || scene == UpgradeTreeScene;

    // Main and Upgrade Tree can't be entered while the night's fees are still owed, and Table Fees can't be
    // entered until they are
    public static bool IsLocked(string scene)
    {
        Upgrades up = Upgrades.Instance;
        if (up == null) return false;
        if (scene == TableFeesScene) return !up.FeesDue;
        return up.FeesDue && (scene == MainScene || scene == UpgradeTreeScene);
    }

    // Use this instead of SceneManager.LoadScene for buttons that move between scenes, so the lock applies
    public static bool TryLoadScene(string scene)
    {
        if (IsLocked(scene))
        {
            string reason = scene == TableFeesScene ? "nothing is owed until the night is over" : "the night is over and the table fees are due";
            Debug.Log($"[NightClock] '{scene}' is locked - {reason}.");
            return false;
        }

        SceneManager.LoadScene(scene);
        return true;
    }

    Sprite[] frames;
    Canvas canvas;
    Image clockImage;
    TMP_Text feesLabel;
    int shownFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        NightClockFrames asset = Resources.Load<NightClockFrames>("NightClockFrames");
        frames = asset != null ? asset.frames : null;
        if (frames == null || frames.Length == 0)
            Debug.LogWarning("[NightClock] No clock sprites found. In the Unity editor run Tools > Sync Night Clock Frames (it reads Assets/Sprites/clock it).");

        BuildUi();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Upgrades up = Upgrades.Instance;
        if (up != null)
        {
            if (IsLocked(scene.name))
                SceneManager.LoadScene(LobbyScene); // safety net for anything that loaded a locked scene directly
            else if (IsNightScene(scene.name))
                up.BeginNightIfNeeded();
        }

        Refresh();
    }

    void Update()
    {
        Upgrades up = Upgrades.Instance;
        if (up != null && up.NightTimeUp)
        {
            // Main ends its own night (GameEngine lets the last shot finish and pay out first), then
            // returns to the Lobby itself. Anywhere else the night just ends here.
            string scene = SceneManager.GetActiveScene().name;
            if (scene != MainScene)
            {
                up.EndNight();
                if (scene == UpgradeTreeScene) SceneManager.LoadScene(LobbyScene);
            }
        }

        Refresh();
    }

    // Picks the clock sprite for how far through the night we are: the first frame at the start, the
    // full circle only once the time is actually up
    void Refresh()
    {
        bool visible = IsNightScene(SceneManager.GetActiveScene().name);
        canvas.enabled = visible;
        if (!visible) return;

        Upgrades up = Upgrades.Instance;
        bool feesDue = up != null && up.FeesDue;
        if (feesDue && !feesLabel.gameObject.activeSelf) feesLabel.text = $"Night over - pay your ${up.CurrentTableFee:N0} table fees";
        feesLabel.gameObject.SetActive(feesDue);

        if (frames == null || frames.Length == 0) return;

        float progress = up != null ? up.NightProgress : 0f;
        int frame = Mathf.Clamp(Mathf.FloorToInt(progress * (frames.Length - 1)), 0, frames.Length - 1);
        if (frame == shownFrame) return;

        shownFrame = frame;
        clockImage.sprite = frames[frame];
        clockImage.enabled = frames[frame] != null;
    }

    void BuildUi()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Top centre: the Lobby, Main and Upgrade Tree already use the top corners for money
        RectTransform clockRect = NewChild<Image>(canvasObject.transform, "Clock", out clockImage);
        clockRect.anchorMin = clockRect.anchorMax = clockRect.pivot = new Vector2(0.5f, 1f);
        clockRect.anchoredPosition = new Vector2(0f, -ClockTopMargin);
        clockRect.sizeDelta = new Vector2(ClockSize, ClockSize);
        clockImage.preserveAspect = true;
        clockImage.raycastTarget = false; // never blocks the buttons underneath
        clockImage.enabled = false; // an Image with no sprite draws a white square; Refresh turns it on with the first frame

        RectTransform labelRect = NewChild<TextMeshProUGUI>(canvasObject.transform, "Fees Label", out TextMeshProUGUI label);
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -(ClockTopMargin + ClockSize + 8f));
        labelRect.sizeDelta = new Vector2(700f, 44f);
        label.text = "Night over - pay your table fees";
        label.fontSize = 30f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        feesLabel = label;
        feesLabel.gameObject.SetActive(false);
    }

    static RectTransform NewChild<T>(Transform parent, string name, out T component) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        component = go.AddComponent<T>();
        return (RectTransform)go.transform;
    }
}
