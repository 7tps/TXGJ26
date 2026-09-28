using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Sets up everything the tutorial needs in the Editor, the same way BuildUpgradeTreeScene builds the tree.
// Safe to run again - and worth re-running whenever the Lobby or Upgrade Tree changes, because it
// re-copies them so the tutorial's versions stay identical to the real ones.
//
//   Tools/Build Tutorial     - adds rent to the Lobby, copies Lobby -> "Tutorial Lobby" and Upgrade Tree ->
//                              "Tutorial Upgrade Tree", puts all three tutorial scenes in Build Settings,
//                              and adds a Tutorial button to the title screen
//   Tools/Add Rent To Lobby  - just the Lobby rent part (TableFees on the TABLE FEES board + eviction screen)
//
// The tutorial walkthrough panel isn't built here: TutorialDirector creates it in code at runtime.
// Scenes are opened next to whatever you have open, saved, and closed again; prefabs, sprites and the
// Main scene are never touched.
public static class BuildTutorialScene
{
    const string ScenesFolder = "Assets/Scenes/";
    const string TutorialScenePath = ScenesFolder + TutorialSession.SceneName + ".unity";
    const string LobbyScenePath = ScenesFolder + "Lobby.unity";
    const string TreeScenePath = ScenesFolder + "Upgrade Tree.unity";
    const string TutorialLobbyPath = ScenesFolder + TutorialSession.LobbySceneName + ".unity";
    const string TutorialTreePath = ScenesFolder + TutorialSession.TreeSceneName + ".unity";
    const string TitleScenePath = ScenesFolder + "Main Menu.unity";
    const string FontPath = "Assets/Fonts/monogram SDF.asset";

    const string TitleButtonName = "Tutorial Button";
    const string EvictedPanelName = "Evicted Panel";

    static TMP_FontAsset font;

    [MenuItem("Tools/Build Tutorial")]
    public static void BuildTutorial()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (!AssetExists(TutorialScenePath))
        {
            Debug.LogError($"Build Tutorial: {TutorialScenePath} is missing.");
            return;
        }

        if (!AddRent()) return;
        if (!CopyScene(LobbyScenePath, TutorialLobbyPath) || !CopyScene(TreeScenePath, TutorialTreePath)) return;
        AddToBuildSettings(TutorialScenePath, TutorialLobbyPath, TutorialTreePath);
        if (!AddTitleButton()) return;

        Debug.Log("Tutorial built: rent is in the Lobby, \"Tutorial Lobby\" and \"Tutorial Upgrade Tree\" are fresh copies, " +
                  "all three tutorial scenes are in Build Settings, and the title screen has a Tutorial button.");
    }

    [MenuItem("Tools/Add Rent To Lobby")]
    public static void AddRentToLobby()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (AddRent()) Debug.Log("Rent added to the Lobby. Run Tools/Build Tutorial to copy it into the Tutorial Lobby too.");
    }

    // ---- Rent in the Lobby ----

    static bool AddRent()
    {
        Scene scene = OpenForEdit(LobbyScenePath, out bool openedHere);

        GameObject board = FindByName(scene, "Table Fees");
        GameObject play = FindByName(scene, "Play Game");
        if (board == null || play == null)
        {
            Debug.LogError("Rent: couldn't find the \"Table Fees\" and \"Play Game\" objects in the Lobby.");
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return false;
        }

        Canvas canvas = board.GetComponentInParent<Canvas>().rootCanvas;
        Transform old = canvas.transform.Find(EvictedPanelName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (TableFees existing in board.GetComponents<TableFees>()) Object.DestroyImmediate(existing);

        TableFees fees = board.AddComponent<TableFees>();

        Button boardButton = board.GetComponent<Button>();
        if (boardButton == null) boardButton = board.AddComponent<Button>();
        ClearListeners(boardButton.onClick);
        UnityEventTools.AddPersistentListener(boardButton.onClick, fees.Pay);

        // Two lines now ("TABLE FEES" + the bill), so let the existing label shrink to fit rather than overflow
        TMP_Text boardLabel = board.GetComponentInChildren<TMP_Text>(true);
        if (boardLabel != null)
        {
            boardLabel.enableAutoSizing = true;
            boardLabel.fontSizeMax = boardLabel.fontSize;
            boardLabel.fontSizeMin = boardLabel.fontSize * 0.35f;
        }

        // Eviction screen: full-screen, on top of the rest of the Lobby canvas, hidden until needed
        RectTransform panel = Rect(EvictedPanelName, canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
        panel.SetAsLastSibling();

        Label(panel, "Title", "EVICTED", 120, new Color(0.9f, 0.3f, 0.3f), TextAlignmentOptions.Center, new Vector2(0, 170), new Vector2(1200, 160));
        TextMeshProUGUI message = Label(panel, "Message", "", 44, Color.white, TextAlignmentOptions.Center, new Vector2(0, 10), new Vector2(1200, 180));
        Button startOver = MakeButton(panel, "Start Over Button", "BACK TO TITLE", new Color(0.6f, 0.22f, 0.22f), 40, new Vector2(0, -170), new Vector2(420, 90));
        UnityEventTools.AddPersistentListener(startOver.onClick, fees.StartOver);

        panel.gameObject.SetActive(false);

        SerializedObject so = new SerializedObject(fees);
        so.FindProperty("payButton").objectReferenceValue = boardButton;
        so.FindProperty("label").objectReferenceValue = boardLabel;
        so.FindProperty("playGameButton").objectReferenceValue = play.GetComponent<Button>();
        so.FindProperty("evictedPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("evictedText").objectReferenceValue = message;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndClose(scene, openedHere);
        return true;
    }

    // ---- Tutorial copies of the menus ----

    // A fresh copy every run, so the tutorial versions always match the real scenes. The copies need no
    // edits: TutorialSession.Route sends "Main"/"Lobby"/"Upgrade Tree" to the tutorial copies while the
    // tutorial is running.
    static bool CopyScene(string from, string to)
    {
        Scene open = SceneManager.GetSceneByPath(to);
        if (open.isLoaded)
        {
            Debug.LogError($"Build Tutorial: close {to} first - it gets replaced with a fresh copy.");
            return false;
        }

        if (AssetExists(to)) AssetDatabase.DeleteAsset(to);
        if (!AssetDatabase.CopyAsset(from, to))
        {
            Debug.LogError($"Build Tutorial: couldn't copy {from} to {to}.");
            return false;
        }
        return true;
    }

    static void AddToBuildSettings(params string[] paths)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        foreach (string path in paths)
        {
            int i = scenes.FindIndex(s => s.path == path);
            if (i >= 0) scenes[i] = new EditorBuildSettingsScene(path, true);
            else scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---- Title screen button ----

    static bool AddTitleButton()
    {
        Scene scene = OpenForEdit(TitleScenePath, out bool openedHere);

        StartMenu menu = FindInScene<StartMenu>(scene);
        Button start = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                    if (b.onClick.GetPersistentMethodName(i) == nameof(StartMenu.StartGame)) start = b;

        if (menu == null || start == null)
        {
            Debug.LogError("Build Tutorial: couldn't find the title screen's StartMenu and Start button in Main Menu.unity.");
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return false;
        }

        Transform parent = start.transform.parent;
        Transform old = parent.Find(TitleButtonName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Stacked one step below the lowest button in the same column, using their existing spacing
        float lowest = float.MaxValue, highest = float.MinValue;
        int count = 0;
        foreach (Transform child in parent)
        {
            if (child.GetComponent<Button>() == null) continue;
            float y = ((RectTransform)child).anchoredPosition.y;
            lowest = Mathf.Min(lowest, y);
            highest = Mathf.Max(highest, y);
            count++;
        }
        RectTransform startRect = (RectTransform)start.transform;
        float spacing = count > 1 ? (highest - lowest) / (count - 1) : startRect.sizeDelta.y * 1.5f;

        GameObject clone = Object.Instantiate(start.gameObject, parent);
        clone.name = TitleButtonName;
        ((RectTransform)clone.transform).anchoredPosition = new Vector2(startRect.anchoredPosition.x, lowest - spacing);

        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = "Tutorial";
            text.gameObject.name = "Tutorial";
        }

        Button button = clone.GetComponent<Button>();
        ClearListeners(button.onClick);
        UnityEventTools.AddPersistentListener(button.onClick, menu.OpenTutorial);

        SaveAndClose(scene, openedHere);
        return true;
    }

    // ---- Helpers ----

    static bool AssetExists(string path) => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

    // Opens the scene next to whatever is already open (or uses it if it's already open)
    static Scene OpenForEdit(string path, out bool openedHere)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        return scene;
    }

    static void SaveAndClose(Scene scene, bool openedHere)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject FindByName(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string content, float size, Color color,
        TextAlignmentOptions align, Vector2 pos, Vector2 box)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, box);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    static Button MakeButton(Transform parent, string name, string caption, Color color, float fontSize, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = Label(rt, "Label", caption, fontSize, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(10, 6);
        label.rectTransform.offsetMax = new Vector2(-10, -6);
        return button;
    }

    static void ClearListeners(UnityEventBase evt)
    {
        while (evt.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(evt, 0);
    }
}
