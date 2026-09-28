using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only tool that builds the Upgrade Tree scene from scratch using the project's own art and
// the tree structure in UpgradeTreeData. Run it from Tools > Build Upgrade Tree Scene. Safe to re-run
// - it wipes and rebuilds the scene every time, so hand edits made directly in that scene are lost on
// the next run; tweak the layout constants or UpgradeTreeData instead.
public static class BuildUpgradeTreeScene
{
    const string ScenePath = "Assets/Scenes/Upgrade Tree.unity";
    const string FontPath = "Assets/Fonts/monogram SDF.asset";
    const string BackScene = "Lobby"; // where the Back button returns to

    const string FrameDefault = "Assets/Sprites/skill tree frames/default.png";
    const string FrameHover = "Assets/Sprites/skill tree frames/default hover.png";
    const string FrameLocked = "Assets/Sprites/skill tree frames/locked.png";
    const string FrameMilestone = "Assets/Sprites/skill tree frames/milestone.png";
    const string FrameMilestoneHover = "Assets/Sprites/skill tree frames/milestone hover.png";

    // One category icon per cardinal direction, marking which of the four paths lies that way.
    static readonly (UpgradeBranch branch, string iconPath, float angleDeg)[] Categories =
    {
        (UpgradeBranch.Ball, "Assets/Sprites/category icons/ball.png", 90f),    // North
        (UpgradeBranch.Cue, "Assets/Sprites/category icons/cue.png", 0f),      // East
        (UpgradeBranch.Table, "Assets/Sprites/category icons/table.png", 270f), // South
        (UpgradeBranch.Flair, "Assets/Sprites/category icons/flair.png", 180f), // West
    };

    // Layout tuning. Each branch gets its own angular wedge measured from the centre (true radial
    // layout, not an x/y offset), so two branches can never drift into each other's space no matter
    // how wide either one's sideways spread gets - unlike the flat "depth + sideways offset" version,
    // which let Ball's sprawl and Flair's reach land two unrelated nodes almost on top of each other.
    const float WedgeHalfWidthDeg = 38f; // leaves a gap between adjacent branches' wedges
    const float DepthSpacing = 150f; // world units per step away from the centre
    const float NodeSize = 64f;
    const float IconSize = 150f;
    const float CapstoneExtraDepth = 2f; // how much further out than the ball icon the capstone sits
    const float CapstoneAngleDeg = 45f; // between Ball (North) and Cue (East) - its own area, not inline with either
    const float GridSize = 20f; // every node/icon position snaps to the nearest multiple of this

    const float MaxZoom = 3.5f;
    const float DefaultZoom = 2f; // PanZoom computes its own minimum (whatever fits the whole tree)

    // One icon per node, from Assets/Sprites/skill tree icons/<branch>/ - 87 files for the 87
    // non-centre, non-capstone nodes. The centre and capstone aren't in here (there's no file for
    // them) and stay icon-less; their distinct root/milestone frames already set them apart.
    const string IconRoot = "Assets/Sprites/skill tree icons/";

    static readonly Dictionary<string, string> NodeIcons = new Dictionary<string, string>
    {
        // Ball
        ["ball_second"] = IconRoot + "ball/second ball.png",
        ["ball_warm_up"] = IconRoot + "ball/value_.png",
        ["ball_lucky_break"] = IconRoot + "ball/luck.png",
        ["ball_loaded_dice_1"] = IconRoot + "ball/dice 1.png",
        ["ball_vault_ball"] = IconRoot + "ball/vault.png",
        ["ball_glass_ball"] = IconRoot + "ball/glass.png",
        ["ball_high_rollers_1"] = IconRoot + "ball/high roller.png",
        ["ball_collector"] = IconRoot + "ball/collector.png",
        ["ball_high_rollers_2"] = IconRoot + "ball/high roller 2.png",
        ["ball_loaded_dice_2"] = IconRoot + "ball/dice 2.png",
        ["ball_loaded_dice_3"] = IconRoot + "ball/dice 3.png",
        ["ball_reading_the_rack"] = IconRoot + "ball/read.png",
        ["ball_hot_hand"] = IconRoot + "ball/hot hand.png",
        ["ball_slow_burn"] = IconRoot + "ball/slowburn.png",
        ["ball_hot_ball"] = IconRoot + "ball/hot.png",
        ["ball_wild_ball"] = IconRoot + "ball/wild.png",
        ["ball_money_ball"] = IconRoot + "ball/money.png",
        ["ball_value_2"] = IconRoot + "ball/value 2.png",
        ["ball_value_3"] = IconRoot + "ball/value 3.png",
        ["ball_last_ball_standing"] = IconRoot + "ball/koth.png",
        ["ball_value_4"] = IconRoot + "ball/value 4.png",
        ["ball_third_ball"] = IconRoot + "ball/third ball.png",
        ["ball_small_rack"] = IconRoot + "ball/small rack.png",
        ["ball_half_rack"] = IconRoot + "ball/half rack.png",
        ["ball_big_rack"] = IconRoot + "ball/big rack.png",
        ["ball_full_rack"] = IconRoot + "ball/full rack.png",
        ["ball_crowded_table"] = IconRoot + "ball/crowded.png",
        ["ball_extra_cue_ball"] = IconRoot + "ball/extra cue.png",
        ["ball_tandem"] = IconRoot + "ball/tandem.png",

        // Flair
        ["flair_crowd_pleaser_1"] = IconRoot + "flair/crowd pleaser.png",
        ["flair_endurance_1"] = IconRoot + "flair/endurance.png",
        ["flair_endurance_2"] = IconRoot + "flair/endurance 2.png",
        ["flair_clean_sweep_1"] = IconRoot + "flair/clean sweep.png",
        ["flair_clean_sweep_2"] = IconRoot + "flair/clean sweep 2.png",
        ["flair_run_the_table"] = IconRoot + "flair/run the table.png",
        ["flair_second_wind"] = IconRoot + "flair/second wind.png",
        ["flair_closer"] = IconRoot + "flair/closer.png",
        ["flair_endurance_3"] = IconRoot + "flair/endurance 3.png",
        ["flair_endurance_4"] = IconRoot + "flair/endurance 4.png",
        ["flair_leftovers"] = IconRoot + "flair/leftovers.png",
        ["flair_nothing_to_lose"] = IconRoot + "flair/nothing to lose.png",
        ["flair_clean_pocket_1"] = IconRoot + "flair/clean pocket.png",
        ["flair_clean_pocket_2"] = IconRoot + "flair/clean pocket 2.png",
        ["flair_cash_out"] = IconRoot + "flair/cash out.png",
        ["flair_hot_streak"] = IconRoot + "flair/streak.png",
        ["flair_clean_pocket_3"] = IconRoot + "flair/clean pocket 3.png",
        ["flair_crowd_pleaser_2"] = IconRoot + "flair/crowd pleaser 2.png",
        ["flair_crowd_pleaser_3"] = IconRoot + "flair/crowd pleaser 3.png",
        ["flair_long_chain"] = IconRoot + "flair/longchain.png",
        ["flair_crowd_pleaser_4"] = IconRoot + "flair/crowd pleaser 4.png",
        ["flair_head_start_1"] = IconRoot + "flair/headstart.png",
        ["flair_head_start_2"] = IconRoot + "flair/headstart 2.png",

        // Cue
        ["cue_chalk_up"] = IconRoot + "cue/#chalked #chopped.png",
        ["cue_aim_guide_1"] = IconRoot + "cue/aim guide.png",
        ["cue_aim_guide_2"] = IconRoot + "cue/aim guide 2.png",
        ["cue_aim_guide_3"] = IconRoot + "cue/aim guide 3.png",
        ["cue_aim_guide_4"] = IconRoot + "cue/aim guide 4.png",
        ["cue_seeing_the_table"] = IconRoot + "cue/visible.png",
        ["cue_power_game"] = IconRoot + "cue/power game.png",
        ["cue_touch_game"] = IconRoot + "cue/touch.png",
        ["cue_clean_contact"] = IconRoot + "cue/clean.png",
        ["cue_power_2"] = IconRoot + "cue/power 2.png",
        ["cue_power_3"] = IconRoot + "cue/poweer 3.png", // sic - that's the actual filename on disk
        ["cue_power_4"] = IconRoot + "cue/power 4.png",
        ["cue_power_5"] = IconRoot + "cue/power 5.png",
        ["cue_break_shot"] = IconRoot + "cue/shot break.png",
        ["cue_steady_hand_1"] = IconRoot + "cue/steady.png",
        ["cue_steady_hand_2"] = IconRoot + "cue/steady 2.png",
        ["cue_stop_shot"] = IconRoot + "cue/stop_.png",
        ["cue_scratch_insurance"] = IconRoot + "cue/insurance.png",

        // Table
        ["table_smooth_felt_1"] = IconRoot + "table/smooth.png",
        ["table_lively_rails_1"] = IconRoot + "table/lively rails.png",
        ["table_lively_rails_2"] = IconRoot + "table/lively rails 2.png",
        ["table_lively_rails_3"] = IconRoot + "table/lively rails 3.png",
        ["table_know_the_rails"] = IconRoot + "table/know rails.png",
        ["table_call_your_pocket"] = IconRoot + "table/call pocket.png",
        ["table_wide_open"] = IconRoot + "table/wide open.png",
        ["table_bank_shot"] = IconRoot + "table/bank shot.png",
        ["table_cushion_cash"] = IconRoot + "table/cash cushion.png",
        ["table_wide_pockets_1"] = IconRoot + "table/wide pockets.png",
        ["table_wide_pockets_2"] = IconRoot + "table/wide pockets 2.png",
        ["table_wide_pockets_3"] = IconRoot + "table/wide pockets 3.png",
        ["table_tight_table"] = IconRoot + "table/tight table.png",
        ["table_side_action"] = IconRoot + "table/action side.png",
        ["table_smooth_felt_2"] = IconRoot + "table/smooth 2.png",
        ["table_smooth_felt_3"] = IconRoot + "table/smooth 3.png",
        ["table_smooth_felt_4"] = IconRoot + "table/smooth 4.png",
    };

    static string IconPathFor(UpgradeNodeDef def) =>
        NodeIcons.TryGetValue(def.id, out string path) ? path : null;

    static Vector2 SnapToGrid(Vector2 p) =>
        new Vector2(Mathf.Round(p.x / GridSize) * GridSize, Mathf.Round(p.y / GridSize) * GridSize);

    [MenuItem("Tools/Build Upgrade Tree Scene")]
    public static void Build()
    {
        FixSpriteImport("Assets/Sprites/background/full bg.png");
        FixSpriteImport(FrameDefault);
        FixSpriteImport(FrameHover);
        FixSpriteImport(FrameLocked);
        FixSpriteImport(FrameMilestone);
        FixSpriteImport(FrameMilestoneHover);
        foreach ((UpgradeBranch _, string iconPath, float _) in Categories) FixSpriteImport(iconPath);
        foreach (string iconPath in NodeIcons.Values) FixSpriteImport(iconPath);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        BuildCamera();
        BuildEventSystem();
        Canvas canvas = BuildCanvas();
        Transform canvasT = canvas.transform;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        BuildBackground(canvasT);
        BuildTitle(canvasT);
        BuildBackButton(canvasT);
        BuildMoneyText(canvasT);

        // ---- Layout: a tidy tree per branch, fanned out North/East/South/West from the centre ----
        Dictionary<string, List<string>> childrenOf = new Dictionary<string, List<string>>();
        foreach (UpgradeNodeDef def in UpgradeTreeData.All)
        {
            if (def.parent == null) continue;
            if (!childrenOf.TryGetValue(def.parent, out List<string> list))
                childrenOf[def.parent] = list = new List<string>();
            list.Add(def.id);
        }

        Dictionary<string, Vector2> positions = new Dictionary<string, Vector2> { [UpgradeTreeData.CentreId] = Vector2.zero };
        int globalMaxDepth = 0;

        foreach ((UpgradeBranch branch, string _, float angleDeg) in Categories)
        {
            string rootId = null;
            foreach (UpgradeNodeDef def in UpgradeTreeData.All)
                if (def.branch == branch && def.parent == UpgradeTreeData.CentreId) { rootId = def.id; break; }
            if (rootId == null) continue;

            int leafCount = 0;
            Dictionary<string, float> spread = new Dictionary<string, float>();
            Dictionary<string, int> depth = new Dictionary<string, int>();
            int maxDepth = AssignSpread(rootId, 1, childrenOf, spread, depth, ref leafCount);
            globalMaxDepth = Mathf.Max(globalMaxDepth, maxDepth);

            foreach (KeyValuePair<string, float> kv in spread)
            {
                // Map this node's slot to an angle within the branch's own wedge - never outside it,
                // so it's geometrically impossible for two different branches' nodes to collide.
                float t = leafCount > 1 ? kv.Value / (leafCount - 1) : 0.5f;
                float angle = angleDeg + (t - 0.5f) * 2f * WedgeHalfWidthDeg;
                positions[kv.Key] = SnapToGrid(Polar(angle, depth[kv.Key] * DepthSpacing));
            }
        }

        positions[UpgradeTreeData.CapstoneId] =
            SnapToGrid(Polar(CapstoneAngleDeg, (globalMaxDepth + CapstoneExtraDepth) * DepthSpacing));

        float outerRadius = (globalMaxDepth + 1) * DepthSpacing;
        List<(Sprite sprite, Vector2 pos)> categoryMarkers = new List<(Sprite, Vector2)>();
        foreach ((UpgradeBranch _, string iconPath, float angleDeg) in Categories)
            categoryMarkers.Add((LoadSprite(iconPath), SnapToGrid(Polar(angleDeg, outerRadius))));

        // ---- Bounding box, so the pannable content is sized to fit everything ----
        Vector2 min = Vector2.zero, max = Vector2.zero;
        foreach (Vector2 p in positions.Values) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        foreach ((Sprite _, Vector2 p) in categoryMarkers) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        Vector2 margin = new Vector2(200, 200);
        min -= margin;
        max += margin;
        Vector2 boundsCentre = (min + max) / 2f;
        Vector2 boundsSize = max - min;

        Transform content = BuildTreeView(canvasT, boundsSize);

        // ---- Draw connectors first, so node frames render on top of the lines ----
        foreach (UpgradeNodeDef def in UpgradeTreeData.All)
        {
            if (def.parent != null)
                BuildConnector(content, positions[def.id] - boundsCentre, positions[def.parent] - boundsCentre,
                    new Color(1f, 1f, 1f, 0.5f), 4f);
            if (def.extraParent != null)
                BuildConnector(content, positions[def.id] - boundsCentre, positions[def.extraParent] - boundsCentre,
                    new Color(0.4f, 0.85f, 1f, 0.6f), 3f);
        }
        // The capstone sits in its own area, unconnected by lines - what it needs is spelled out in
        // its "Needs:" label instead (see BuildNode) rather than drawn as long connectors crossing
        // the whole diagram to reach all four milestones.

        // ---- Category markers (decorative - show which path lies in which direction) ----
        foreach ((Sprite sprite, Vector2 pos) in categoryMarkers)
            BuildIcon(content, sprite, pos - boundsCentre, IconSize);

        // ---- All 89 nodes ----
        foreach (UpgradeNodeDef def in UpgradeTreeData.All)
            BuildNode(content, def, positions[def.id] - boundsCentre);

        // Built last (and parented directly to the Canvas, not the pan/zoomable Content) so it draws
        // on top of everything and stays a fixed, readable size regardless of the tree's zoom level.
        BuildTooltip(canvasT, canvasRect);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Upgrade Tree scene built: {UpgradeTreeData.All.Length} nodes.");
    }

    static Vector2 Polar(float angleDeg, float radius) =>
        radius * new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));

    // Post-order: leaves get sequential slots. A node with one child just inherits that child's slot
    // exactly, so a long single-path chain (most of this tree) stays perfectly straight all the way
    // out - no sideways drift, no elbow bend needed on any link in it. Only a node with two or more
    // children (an actual fork) spreads out, and only there does the parent sit at their average.
    // Returns the deepest depth reached in this subtree.
    static int AssignSpread(string id, int depthValue, Dictionary<string, List<string>> childrenOf,
        Dictionary<string, float> spread, Dictionary<string, int> depth, ref int nextLeafSlot)
    {
        depth[id] = depthValue;

        if (!childrenOf.TryGetValue(id, out List<string> kids) || kids.Count == 0)
        {
            spread[id] = nextLeafSlot++;
            return depthValue;
        }

        if (kids.Count == 1)
        {
            int onlyChildDepth = AssignSpread(kids[0], depthValue + 1, childrenOf, spread, depth, ref nextLeafSlot);
            spread[id] = spread[kids[0]];
            return onlyChildDepth;
        }

        int maxDepth = depthValue;
        float sum = 0f;
        foreach (string kid in kids)
        {
            maxDepth = Mathf.Max(maxDepth, AssignSpread(kid, depthValue + 1, childrenOf, spread, depth, ref nextLeafSlot));
            sum += spread[kid];
        }
        spread[id] = sum / kids.Count;
        return maxDepth;
    }

    // ---- Import fix ----

    internal static void FixSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("BuildUpgradeTreeScene: no texture importer at " + path);
            return;
        }

        bool changed = false;
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    internal static Sprite LoadSprite(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite) return sprite;

        Debug.LogError("BuildUpgradeTreeScene: no sprite found at " + path);
        return null;
    }

    // ---- Scene scaffolding ----
    // (internal so BuildTableFeesScene builds its scene from the same pieces)

    internal static void BuildCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    internal static void BuildEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    internal static Canvas BuildCanvas()
    {
        GameObject go = new GameObject("Canvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    internal static void BuildBackground(Transform parent)
    {
        GameObject go = new GameObject("Background", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.sprite = LoadSprite("Assets/Sprites/background/full bg.png");
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void BuildTitle(Transform parent)
    {
        TextMeshProUGUI text = BuildLabel(parent, "UPGRADES", Vector2.zero, new Vector2(900, 100), 72, Color.white);
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -40);
    }

    static void BuildBackButton(Transform parent)
    {
        GameObject go = new GameObject("Back Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.sprite = LoadSprite("Assets/Sprites/menu icons/blank.png");

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        SceneButton sceneButton = go.AddComponent<SceneButton>();
        SerializedObject sceneButtonSO = new SerializedObject(sceneButton);
        sceneButtonSO.FindProperty("sceneName").stringValue = BackScene;
        sceneButtonSO.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(button.onClick, sceneButton.Load);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40, -40);
        rt.sizeDelta = new Vector2(220, 70);

        BuildLabel(go.transform, "Back", Vector2.zero, new Vector2(220, 70), 32, Color.white);
    }

    // Shows UpgradeProgress.Money in the top-right corner, refreshed by UpgradeMoneyDisplay - the same
    // component Lobby's own "Money Text" object uses (see EnsureUpgradeProgressInLobby), so both scenes
    // read from one place instead of duplicating the refresh logic.
    internal static void BuildMoneyText(Transform parent)
    {
        TextMeshProUGUI text = BuildLabel(parent, "$0", Vector2.zero, new Vector2(260, 60), 40, Color.white);
        text.gameObject.name = "Money Text";
        text.alignment = TextAlignmentOptions.Right;

        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40, -40);

        UpgradeMoneyDisplay display = text.gameObject.AddComponent<UpgradeMoneyDisplay>();
        SerializedObject so = new SerializedObject(display);
        so.FindProperty("text").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The pannable/zoomable area below the title. PanZoom sits on Viewport (an ancestor of every node
    // button), so a click-drag or scroll anywhere over the tree - including directly over a node -
    // reaches it via UGUI's normal event bubbling; a plain click on a button still registers as a
    // click as long as the pointer doesn't move past Unity's drag threshold. Returns the Content
    // transform everything else parents to.
    static Transform BuildTreeView(Transform parent, Vector2 contentSize)
    {
        GameObject viewGO = new GameObject("Tree View", typeof(RectTransform));
        viewGO.transform.SetParent(parent, false);
        RectTransform viewRt = viewGO.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.offsetMin = Vector2.zero;
        viewRt.offsetMax = new Vector2(0, -140); // leave room for the title under the top edge

        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(viewGO.transform, false);
        RectTransform viewportRt = viewportGO.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGO.AddComponent<RectMask2D>();

        Image viewportBg = viewportGO.AddComponent<Image>();
        viewportBg.color = new Color(0, 0, 0, 0); // invisible; gives drag/scroll something to hit over empty space

        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRt = contentGO.GetComponent<RectTransform>();
        contentRt.anchorMin = contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.sizeDelta = contentSize;
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.localScale = Vector3.one * DefaultZoom; // PanZoom.Awake() only sets this once Play starts

        PanZoom panZoom = viewportGO.AddComponent<PanZoom>();
        SerializedObject so = new SerializedObject(panZoom);
        so.FindProperty("target").objectReferenceValue = contentRt;
        so.FindProperty("maxZoom").floatValue = MaxZoom;
        so.FindProperty("defaultZoom").floatValue = DefaultZoom;
        so.ApplyModifiedPropertiesWithoutUndo();

        return contentGO.transform;
    }

    // A single skill-tree node: a button whose frame swaps between default / hover / locked while
    // it's buyable, or is forced to a fixed tint once owned (see UpgradeTreeNodeUI.Refresh), with its
    // own icon inside from NodeIcons.
    static void BuildNode(Transform parent, UpgradeNodeDef def, Vector2 pos)
    {
        bool isMilestone = def.type == UpgradeNodeType.Milestone || def.type == UpgradeNodeType.Capstone;

        GameObject node = new GameObject(def.label + " Node", typeof(RectTransform));
        node.transform.SetParent(parent, false);

        RectTransform nodeRt = node.GetComponent<RectTransform>();
        nodeRt.anchorMin = nodeRt.anchorMax = new Vector2(0.5f, 0.5f);
        nodeRt.pivot = new Vector2(0.5f, 0.5f);
        nodeRt.anchoredPosition = pos;
        nodeRt.sizeDelta = new Vector2(NodeSize, NodeSize);

        Image frame = node.AddComponent<Image>();
        frame.sprite = LoadSprite(isMilestone ? FrameMilestone : FrameDefault);

        Button button = node.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = LoadSprite(isMilestone ? FrameMilestoneHover : FrameHover),
            pressedSprite = LoadSprite(isMilestone ? FrameMilestoneHover : FrameHover),
            selectedSprite = LoadSprite(isMilestone ? FrameMilestone : FrameDefault),
            disabledSprite = LoadSprite(FrameLocked),
        };

        string iconPath = IconPathFor(def);
        if (iconPath != null)
        {
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(node.transform, false);
            Image icon = iconGO.AddComponent<Image>();
            icon.sprite = LoadSprite(iconPath);
            icon.raycastTarget = false;
            RectTransform iconRt = iconGO.GetComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = Vector2.one * NodeSize * 0.62f; // inset from the frame's own edge
        }

        // UpgradeTreeNodeUI.Refresh() only runs from OnEnable, and the first time that fires is right
        // now, during this very build - before Play mode exists, so before UpgradeProgress exists to
        // read from. Without this, every node would sit in the scene looking "available" (whatever
        // frame.sprite was just set to above) until the tree is actually played and re-Refreshed.
        // Only the centre starts genuinely unlocked; everything else needs its parent owned first.
        button.interactable = def.parent == null && def.id != UpgradeTreeData.CapstoneId;
        if (!button.interactable) frame.sprite = LoadSprite(FrameLocked);

        // A dimming rectangle over the icon plus a lock glyph on top of that, shown only while the
        // node is locked (see UpgradeTreeNodeUI.Refresh). There's no standalone padlock asset, so this
        // reuses the frame's own locked.png, sized down - it still reads clearly as "a lock" at icon size.
        GameObject lockOverlay = new GameObject("Lock Overlay", typeof(RectTransform));
        lockOverlay.transform.SetParent(node.transform, false);
        RectTransform lockOverlayRt = lockOverlay.GetComponent<RectTransform>();
        lockOverlayRt.anchorMin = lockOverlayRt.anchorMax = new Vector2(0.5f, 0.5f);
        lockOverlayRt.pivot = new Vector2(0.5f, 0.5f);
        lockOverlayRt.sizeDelta = Vector2.one * NodeSize * 0.62f; // matches the icon's own footprint

        Image dim = lockOverlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = false;

        GameObject lockIconGO = new GameObject("Lock Icon", typeof(RectTransform));
        lockIconGO.transform.SetParent(lockOverlay.transform, false);
        Image lockIcon = lockIconGO.AddComponent<Image>();
        lockIcon.sprite = LoadSprite(FrameLocked);
        lockIcon.raycastTarget = false;
        RectTransform lockIconRt = lockIconGO.GetComponent<RectTransform>();
        lockIconRt.anchorMin = lockIconRt.anchorMax = new Vector2(0.5f, 0.5f);
        lockIconRt.pivot = new Vector2(0.5f, 0.5f);
        lockIconRt.sizeDelta = Vector2.one * NodeSize * 0.55f;

        lockOverlay.SetActive(!button.interactable);

        TextMeshProUGUI nameText = BuildLabel(node.transform, def.label, new Vector2(0, -NodeSize * 0.9f),
            new Vector2(NodeSize * 2f, 34), 13, Color.white);

        if (def.id == UpgradeTreeData.CapstoneId)
        {
            System.Text.StringBuilder needs = new System.Text.StringBuilder("Needs: ");
            for (int i = 0; i < UpgradeTreeData.CapstoneRequires.Length; i++)
            {
                if (i > 0) needs.Append(", ");
                needs.Append(UpgradeTreeData.Get(UpgradeTreeData.CapstoneRequires[i]).label);
            }
            BuildLabel(node.transform, needs.ToString(), new Vector2(0, -NodeSize * 0.9f - 46),
                new Vector2(NodeSize * 5f, 70), 14, new Color(1f, 0.75f, 0.35f));
        }

        UpgradeTreeNodeUI nodeUI = node.AddComponent<UpgradeTreeNodeUI>();
        SerializedObject so = new SerializedObject(nodeUI);
        so.FindProperty("nodeId").stringValue = def.id;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("frame").objectReferenceValue = frame;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("lockOverlay").objectReferenceValue = lockOverlay;
        so.FindProperty("defaultFrame").objectReferenceValue = LoadSprite(FrameDefault);
        so.FindProperty("milestoneFrame").objectReferenceValue = LoadSprite(FrameMilestone);
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(button.onClick, nodeUI.Buy);
    }

    static void BuildIcon(Transform parent, Sprite sprite, Vector2 pos, float size)
    {
        GameObject go = new GameObject("Category Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
    }

    // An orthogonal "elbow" link between two points: a horizontal run at a's height, then a vertical
    // run at b's position, meeting at a right-angle corner - never a diagonal, regardless of where a
    // and b actually are. This is what most tech-tree diagrams draw instead of a straight line.
    static void BuildConnector(Transform parent, Vector2 a, Vector2 b, Color color, float thickness)
    {
        Vector2 corner = new Vector2(b.x, a.y);
        BuildSegment(parent, a, corner, color, thickness);
        BuildSegment(parent, corner, b, color, thickness);
    }

    // One straight horizontal-or-vertical stretch of a connector, made from a stretched/rotated Image
    // with no sprite (plain white swatch) - UGUI has no built-in line primitive. Skips zero-length
    // segments, which happen whenever a and b already share an axis and the elbow collapses to one run.
    static void BuildSegment(Transform parent, Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.01f) return;

        GameObject go = new GameObject("Connector Segment", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;
        rt.sizeDelta = new Vector2(delta.magnitude, thickness);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // The shared hover tooltip - a dark panel that follows the mouse, hidden by default (Tooltip
    // itself hides it in Awake at Play time; hidden here too so it isn't visible in the Editor before
    // that first runs - see the same reasoning as the locked-frame fix in BuildNode).
    static void BuildTooltip(Transform canvasParent, RectTransform canvasRect)
    {
        // rootGO stays active always, so Tooltip.Awake() actually runs and sets Instance - an inactive
        // GameObject never gets Awake called until something activates it, which nothing here would.
        // Its zero size means every anchor on the Panel child collapses to this same point (root's own
        // pivot, at the canvas centre), matching the canvas-centre-origin space
        // ScreenPointToLocalPointInRectangle returns in Tooltip.Update().
        GameObject rootGO = new GameObject("Tooltip", typeof(RectTransform));
        rootGO.transform.SetParent(canvasParent, false);
        RectTransform rootRt = rootGO.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        // Panel is the part that actually toggles on/off (see Tooltip.Show/Hide) - safe to deactivate
        // since it doesn't hold the script itself.
        GameObject panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(rootGO.transform, false);
        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        bg.raycastTarget = false;
        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = Vector2.zero;
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.sizeDelta = new Vector2(460, 170);

        TextMeshProUGUI text = BuildLabel(panelGO.transform, "", Vector2.zero, new Vector2(430, 150), 28, Color.white);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18;
        text.fontSizeMax = 28;

        Tooltip tooltip = rootGO.AddComponent<Tooltip>();
        SerializedObject so = new SerializedObject(tooltip);
        so.FindProperty("panel").objectReferenceValue = panelRt;
        so.FindProperty("text").objectReferenceValue = text;
        so.FindProperty("canvasRect").objectReferenceValue = canvasRect;
        so.ApplyModifiedPropertiesWithoutUndo();

        panelGO.SetActive(false);
    }

    // A UI text object anchored to its parent's centre, offset by `anchoredPos`.
    internal static TextMeshProUGUI BuildLabel(Transform parent, string content, Vector2 anchoredPos, Vector2 size,
        float fontSize, Color color)
    {
        GameObject go = new GameObject(content + " Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return text;
    }
}
