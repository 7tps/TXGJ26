using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Walks the player through the tutorial across its three scenes (table -> Lobby -> Upgrade Tree ->
// Lobby -> table), one step at a time in a panel in the bottom-left corner. Each step either waits for
// Next, or for the player to actually do the thing in the real (copied) scene: aim, shoot, sink a ball,
// go to a scene, buy an upgrade, pay the rent.
//
// Created by TutorialSession when the tutorial starts and destroyed when it ends. It lives across scene
// loads (DontDestroyOnLoad) and builds its own UI in code, so the tutorial scenes need nothing added to
// them. It never changes game rules: the table, Lobby and tree work exactly like the real ones, just on
// the tutorial's own state (see TutorialSession).
public class TutorialDirector : MonoBehaviour
{
    enum Goal { Next, Aim, Shoot, Pocket, GoToScene, BuyUpgrade, PayRent, Finish }

    class Step
    {
        public string scene, title, body;
        public Goal goal;
        public string target; // for GoToScene
        public int topUpTo; // the crowd tips the player up to this much when the step starts
        public bool rentDue; // brings a practice bill due and tops up enough to pay it

        public Step(string scene, string title, string body, Goal goal, string target = null, int topUpTo = 0, bool rentDue = false)
        {
            this.scene = scene;
            this.title = title;
            this.body = body;
            this.goal = goal;
            this.target = target;
            this.topUpTo = topUpTo;
            this.rentDue = rentDue;
        }
    }

    const string Table = TutorialSession.SceneName;
    const string Lobby = TutorialSession.LobbySceneName;
    const string Tree = TutorialSession.TreeSceneName;

    const float AutoAdvanceDelay = 1.2f;
    const float AimDegreesNeeded = 45f;
    const int ShopTip = 300;

    static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.08f, 0.92f);
    static readonly Color Gold = new Color(1f, 0.88f, 0.4f);
    static readonly Color Muted = new Color(0.7f, 0.7f, 0.7f);

    static readonly Step[] Steps =
    {
        new Step(Table, "Welcome to the Table",
            "You used to be the best pool player around. Now you're hustling at a dive bar to get back on top. " +
            "The crowd bets on every ball you sink: pocket balls, collect the bets, and spend them to improve your game.",
            Goal.Next),
        new Step(Table, "Aim",
            "Move the mouse around the white cue ball. The cue stick follows it and points where your shot will go.",
            Goal.Aim),
        new Step(Table, "Shoot",
            "Click and hold, then drag the mouse back away from the ball to pull the cue. The further you pull, the harder the shot. " +
            "Let go to shoot. (Right-click while pulling to cancel.)",
            Goal.Shoot),
        new Step(Table, "Value and Multiplier",
            "The number above a ball is its value: the bet riding on it. The yellow number under it is its multiplier. " +
            "Every time a ball gets hit the crowd gets more excited and its multiplier goes up.",
            Goal.Next),
        new Step(Table, "Sink the Ball",
            "Knock the ball into any pocket. It pays its value x its multiplier. " +
            "Sinking the white cue ball is a scratch: it comes back, but pays nothing.",
            Goal.Pocket),
        new Step(Table, "Rack Cleared",
            "Clearing the table ends the round and takes your winnings back to the Lobby. " +
            "(In the real game each night also has a timer - it's frozen during the tutorial.)",
            Goal.GoToScene, Lobby),
        new Step(Lobby, "The Lobby",
            "This is where you go between nights. From here you can play the next night, pay the bar's table fees, " +
            "or spend your winnings in the Upgrade Tree. The crowd tipped you some cash - click UPGRADE TREE.",
            Goal.GoToScene, Tree, ShopTip),
        new Step(Tree, "The Upgrade Tree",
            "Hover a node to see what it does and what it costs. Nodes unlock once the node they branch from is owned. " +
            "Claim the free Rack 'Em in the middle, then the free Chalk Up, then buy Aim Guide I or Power II. Buy one or two.",
            Goal.BuyUpgrade, null, ShopTip),
        new Step(Tree, "Head Back",
            "Upgrades last for the whole run. Click BACK to return to the Lobby.",
            Goal.GoToScene, Lobby),
        new Step(Lobby, "Paying Rent",
            "Every {rentEvery} nights the bar charges table fees, starting at ${rentFirst} and going up each time. " +
            "When a bill is due, PLAY GAME stays locked until you pay at the TABLE FEES board - and if you can't afford it, " +
            "you're evicted and the run is over. A practice bill is due now: click TABLE FEES to pay it.",
            Goal.PayRent, null, 0, true),
        new Step(Lobby, "Next Night",
            "Rent's paid, so PLAY GAME is open again. Your upgrades come with you - click PLAY GAME.",
            Goal.GoToScene, Table),
        new Step(Table, "Try Your Upgrades",
            "Take a shot. With Aim Guide a line shows where your shot will go; with Power II you can hit harder.",
            Goal.Shoot),
        new Step(Table, "You're Ready",
            "Play nights, build your multipliers, buy upgrades, and always keep enough for rent. " +
            "Press Finish to leave the tutorial - nothing you did here carries over.",
            Goal.Finish),
    };

    static TutorialDirector instance;

    TMP_Text titleText, bodyText, progressText, hintText, nextLabel;
    Button nextButton;

    string sceneName;
    CueStick cue;
    GameEngine engine;

    int index;
    bool started;
    bool goalDone;
    float aimTravelled;
    Vector2 lastAim;
    int moneyAtStepStart, billsAtStepStart;

    Step Current => Steps[index];
    bool InStepScene => sceneName == Current.scene;

    // ---- Lifetime (driven by TutorialSession) ----

    public static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("Tutorial Director");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TutorialDirector>();
        instance.BuildUI();
    }

    public static void DestroyInstance()
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = null;
    }

    public static void OnSceneLoaded(Scene scene)
    {
        if (instance != null) instance.HandleScene(scene.name);
    }

    void HandleScene(string loaded)
    {
        loaded = TutorialSession.Normalize(loaded); // the real Lobby/Upgrade Tree count as the tutorial's
        sceneName = loaded;
        engine = FindFirstObjectByType<GameEngine>();
        cue = FindFirstObjectByType<CueStick>();
        if (cue != null) cue.ShotTaken += OnShotTaken;
        if (engine != null) engine.BallPocketed += OnBallPocketed;

        if (!started)
        {
            started = true;
            int first = System.Array.FindIndex(Steps, s => s.scene == loaded);
            GoTo(first < 0 ? 0 : first);
            return;
        }

        // Arriving where the current step asked to go completes it; arriving somewhere else early (say
        // the ball dropped during the Shoot step and the rack cleared) skips ahead to that scene's steps
        if (Current.goal == Goal.GoToScene && Current.target == loaded)
        {
            GoTo(index + 1);
        }
        else if (!InStepScene)
        {
            for (int i = index + 1; i < Steps.Length; i++)
            {
                if (Steps[i].scene != loaded) continue;
                GoTo(i);
                return;
            }
            Show();
        }
        else
        {
            Show();
        }
    }

    // ---- Steps ----

    void GoTo(int i)
    {
        index = Mathf.Clamp(i, 0, Steps.Length - 1);
        Step step = Current;

        UpgradeProgress wallet = UpgradeProgress.Instance;
        if (step.rentDue && Rent.Instance != null)
        {
            Rent.Instance.MakeDueNow();
            TopUp(Rent.Instance.AmountDue);
        }
        if (step.topUpTo > 0) TopUp(step.topUpTo);
        if (step.rentDue) UpgradeProgress.NotifyChanged(); // the Lobby's TABLE FEES board shows the bill straight away

        moneyAtStepStart = wallet != null ? wallet.Money : 0;
        billsAtStepStart = Rent.Instance != null ? Rent.Instance.BillsPaid : 0;
        aimTravelled = 0f;
        if (cue != null) lastAim = cue.AimDirection;

        goalDone = step.goal == Goal.Next || step.goal == Goal.Finish;
        Show();
    }

    void Show()
    {
        Step step = Current;
        progressText.text = $"{index + 1} / {Steps.Length}";

        if (!InStepScene)
        {
            titleText.text = step.title;
            bodyText.text = $"Head back to the {Friendly(step.scene)} to continue the tutorial.";
            hintText.text = "";
            nextButton.interactable = false;
            nextLabel.text = "Next";
            return;
        }

        titleText.text = step.title;
        bodyText.text = FillIn(step.body);
        nextLabel.text = step.goal == Goal.Finish ? "Finish" : "Next";
        nextButton.interactable = goalDone;
        hintText.text = goalDone ? "" : Hint(step);
    }

    public void Next()
    {
        if (!goalDone || !InStepScene) return;
        if (Current.goal == Goal.Finish) ExitTutorial();
        else GoTo(index + 1);
    }

    // Loading a real scene ends the tutorial: TutorialSession swaps the real state back in
    public void ExitTutorial() => TutorialSession.ExitTo(TutorialSession.ReturnScene);

    void Complete(bool autoAdvance)
    {
        if (goalDone) return;
        goalDone = true;
        Show();
        if (autoAdvance) StartCoroutine(AdvanceAfterDelay(index));
    }

    IEnumerator AdvanceAfterDelay(int fromStep)
    {
        yield return new WaitForSeconds(AutoAdvanceDelay);
        if (index == fromStep) GoTo(index + 1);
    }

    void Update()
    {
        if (goalDone || !InStepScene) return;

        switch (Current.goal)
        {
            case Goal.Aim:
                if (cue == null) return;
                aimTravelled += Vector2.Angle(lastAim, cue.AimDirection);
                lastAim = cue.AimDirection;
                if (aimTravelled >= AimDegreesNeeded) Complete(true);
                break;

            case Goal.BuyUpgrade:
                // Spending money means a real purchase (the free nodes on the way don't count)
                if (UpgradeProgress.Instance != null && UpgradeProgress.Instance.Money < moneyAtStepStart) Complete(false);
                break;

            case Goal.PayRent:
                if (Rent.Instance != null && Rent.Instance.BillsPaid > billsAtStepStart) Complete(true);
                break;
        }
    }

    void OnShotTaken()
    {
        if (InStepScene && Current.goal == Goal.Shoot) Complete(true);
    }

    void OnBallPocketed(Ball ball)
    {
        if (!InStepScene || Current.goal != Goal.Pocket) return;
        if (ball.IsCueBall) hintText.text = "Scratch! The cue ball comes back next turn - try again";
        else Complete(true);
    }

    // The crowd "tips" the player on the tutorial's own wallet, so the shop and the bill are affordable
    static void TopUp(int target)
    {
        UpgradeProgress wallet = UpgradeProgress.Instance;
        if (wallet != null && wallet.Money < target) wallet.AddMoney(target - wallet.Money);
    }

    static string Hint(Step step) => step.goal switch
    {
        Goal.Aim => "Move the mouse to continue",
        Goal.Shoot => "Take a shot to continue",
        Goal.Pocket => "Sink the ball to continue",
        Goal.GoToScene => $"Go to the {Friendly(step.target)} to continue",
        Goal.BuyUpgrade => "Buy an upgrade to continue (then Next, or buy another first)",
        Goal.PayRent => "Pay the bill to continue",
        _ => "",
    };

    static string Friendly(string scene) => scene switch
    {
        Table => "table",
        Lobby => "Lobby",
        Tree => "Upgrade Tree",
        _ => scene,
    };

    static string FillIn(string body)
    {
        int every = Rent.Instance != null ? Rent.Instance.NightsBetweenBills : 3;
        int first = Rent.Instance != null ? Rent.Instance.FirstBill : 200;
        return body.Replace("{rentEvery}", every.ToString()).Replace("{rentFirst}", first.ToString("N0"));
    }

    // ---- UI, built in code so no scene needs anything added ----

    void BuildUI()
    {
        TMP_FontAsset font = FindFirstObjectByType<TMP_Text>()?.font; // match whatever font the game's UI uses

        GameObject canvasGO = new GameObject("Tutorial UI", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // over every scene's own UI
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Bottom-left: under the table in the table scene, and clear of the Lobby's buttons
        RectTransform panel = Rect("Step Panel", canvasGO.transform, Vector2.zero, new Vector2(20, 12), new Vector2(1000, 215));
        panel.gameObject.AddComponent<Image>().color = PanelColor;

        titleText = Label(panel, "Title", font, 40, Gold, TextAlignmentOptions.TopLeft);
        Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(20, -10), new Vector2(700, 48));

        progressText = Label(panel, "Progress", font, 26, Muted, TextAlignmentOptions.TopRight);
        Place(progressText.rectTransform, new Vector2(1, 1), new Vector2(-206, -12), new Vector2(160, 40));

        bodyText = Label(panel, "Body", font, 26, Color.white, TextAlignmentOptions.TopLeft);
        RectTransform body = bodyText.rectTransform;
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(20, 44);
        body.offsetMax = new Vector2(-206, -56); // leaves the right-hand column for the buttons
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 16;
        bodyText.fontSizeMax = 26;

        hintText = Label(panel, "Hint", font, 22, Muted, TextAlignmentOptions.BottomLeft);
        hintText.fontStyle = FontStyles.Italic;
        Place(hintText.rectTransform, new Vector2(0, 0), new Vector2(20, 10), new Vector2(760, 30));

        // Stacked in the right-hand column: Next above, Exit below
        nextButton = MakeButton(panel, "Next Button", font, "Next", new Color(0.25f, 0.55f, 0.3f), new Vector2(-16, 140), out nextLabel);
        nextButton.onClick.AddListener(Next);

        Button exit = MakeButton(panel, "Exit Button", font, "Exit Tutorial", new Color(0.6f, 0.22f, 0.22f), new Vector2(-16, 12), out _);
        exit.onClick.AddListener(ExitTutorial);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, anchor, pos, size);
        return rt;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    static Button MakeButton(Transform parent, string name, TMP_FontAsset font, string caption, Color color, Vector2 pos, out TMP_Text label)
    {
        RectTransform rt = Rect(name, parent, new Vector2(1, 0), pos, new Vector2(170, 56));
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = Label(rt, "Label", font, 28, Color.white, TextAlignmentOptions.Center);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(6, 4);
        lr.offsetMax = new Vector2(-6, -4);
        label.text = caption;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16;
        label.fontSizeMax = 28;
        return button;
    }
}
