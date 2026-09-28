using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only tool that builds the Table Fees scene (see TableFees) from scratch, out of the same pieces
// as the Upgrade Tree scene (see BuildUpgradeTreeScene), and adds it to the Build Settings so
// NightClock.TryLoadScene can load it. Run it from Tools > Build Table Fees Scene. Safe to re-run - it
// wipes and rebuilds the scene every time, so hand edits made directly in that scene are lost on the
// next run; tweak the layout constants here instead.
public static class BuildTableFeesScene
{
    const string ScenePath = "Assets/Scenes/" + NightClock.TableFeesScene + ".unity";
    const string ButtonSprite = "Assets/Sprites/menu icons/blank.png";

    // Layout tuning (canvas is 1920x1080 reference, so these are in those units)
    static readonly Vector2 BillPosition = new Vector2(0, 90);
    static readonly Vector2 BillSize = new Vector2(1400, 260);
    static readonly Vector2 ButtonSize = new Vector2(320, 90);
    const float ButtonY = -170f;
    const float ButtonSpacing = 220f; // each button's distance from the centre line

    [MenuItem("Tools/Build Table Fees Scene")]
    public static void Build()
    {
        BuildUpgradeTreeScene.FixSpriteImport("Assets/Sprites/background/full bg.png");
        BuildUpgradeTreeScene.FixSpriteImport(ButtonSprite);

        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        BuildUpgradeTreeScene.BuildCamera();
        BuildUpgradeTreeScene.BuildEventSystem();
        Canvas canvas = BuildUpgradeTreeScene.BuildCanvas();
        Transform canvasT = canvas.transform;

        BuildUpgradeTreeScene.BuildBackground(canvasT);
        BuildTitle(canvasT);
        BuildUpgradeTreeScene.BuildMoneyText(canvasT);

        TextMeshProUGUI bill = BuildUpgradeTreeScene.BuildLabel(canvasT, "", BillPosition, BillSize, 56, Color.white);
        bill.gameObject.name = "Bill Text";

        TableFees fees = canvas.gameObject.AddComponent<TableFees>();
        Button pay = BuildButton(canvasT, "Pay Button", "Pay", new Vector2(-ButtonSpacing, ButtonY), out TextMeshProUGUI payText);
        Button walkAway = BuildButton(canvasT, "Walk Away Button", "Walk Away", new Vector2(ButtonSpacing, ButtonY), out _);
        UnityEventTools.AddPersistentListener(pay.onClick, fees.Pay);
        UnityEventTools.AddPersistentListener(walkAway.onClick, fees.WalkAway);

        SerializedObject so = new SerializedObject(fees);
        so.FindProperty("billText").objectReferenceValue = bill;
        so.FindProperty("payButton").objectReferenceValue = pay;
        so.FindProperty("payButtonText").objectReferenceValue = payText;
        so.FindProperty("walkAwayButton").objectReferenceValue = walkAway;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        Debug.Log("Table Fees scene built.");
    }

    static void BuildTitle(Transform parent)
    {
        TextMeshProUGUI text = BuildUpgradeTreeScene.BuildLabel(parent, "TABLE FEES", Vector2.zero, new Vector2(900, 100), 72, Color.white);
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -40);
    }

    // Same look as the Upgrade Tree's Back button: the blank menu frame with a label on top
    static Button BuildButton(Transform parent, string name, string label, Vector2 pos, out TextMeshProUGUI labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.sprite = BuildUpgradeTreeScene.LoadSprite(ButtonSprite);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = ButtonSize;

        labelText = BuildUpgradeTreeScene.BuildLabel(go.transform, label, Vector2.zero, ButtonSize, 36, Color.white);
        return button;
    }

    // SceneManager.LoadScene only finds scenes listed (and enabled) in the Build Settings
    static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(s => s.path == ScenePath);
        if (index >= 0 && scenes[index].enabled) return;

        if (index >= 0) scenes[index].enabled = true;
        else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Added {ScenePath} to the Build Settings.");
    }
}
