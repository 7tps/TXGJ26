using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only one-off: prepares the Lobby scene so it (1) owns the UpgradeProgress singleton, since
// Lobby is the hub every scene returns to (see GameEngine.ReturnToLobbyAfterDelay), rather than however
// a RuntimeInitializeOnLoadMethod bootstrap happened to create it, and (2) has the existing "Money
// Text" placeholder wired up to actually show the carried-over total. Run it once from Tools > Prepare
// Lobby Scene. Safe to re-run - each step no-ops if it's already done, and neither touches anything
// else already in the scene.
public static class EnsureUpgradeProgressInLobby
{
    const string ScenePath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Tools/Prepare Lobby Scene")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool dirty = false;

        dirty |= EnsureUpgradeProgress(scene);
        dirty |= WireMoneyText(scene);

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static bool EnsureUpgradeProgress(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<UpgradeProgress>() != null)
            {
                Debug.Log("Lobby already has an UpgradeProgress - nothing to do.");
                return false;
            }
        }

        new GameObject("UpgradeProgress").AddComponent<UpgradeProgress>();
        Debug.Log("Added UpgradeProgress to Lobby.");
        return true;
    }

    // The Lobby scene already has a "Money Text" TMP object placeholder (m_text: "XX$") - just give it
    // the same UpgradeMoneyDisplay component the Upgrade Tree scene's copy uses, rather than duplicating
    // that refresh logic in a Lobby-specific script.
    static bool WireMoneyText(Scene scene)
    {
        Transform moneyText = FindByName(scene, "Money Text");
        if (moneyText == null)
        {
            Debug.LogWarning("No \"Money Text\" object found in Lobby - add one or rename this to match.");
            return false;
        }

        if (moneyText.GetComponent<UpgradeMoneyDisplay>() != null)
        {
            Debug.Log("Lobby's Money Text is already wired up - nothing to do.");
            return false;
        }

        TMPro.TMP_Text tmp = moneyText.GetComponent<TMPro.TMP_Text>();
        if (tmp == null)
        {
            Debug.LogWarning("Lobby's Money Text object has no TMP_Text component - can't wire it up.");
            return false;
        }

        UpgradeMoneyDisplay display = moneyText.gameObject.AddComponent<UpgradeMoneyDisplay>();
        SerializedObject so = new SerializedObject(display);
        so.FindProperty("text").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("Wired UpgradeMoneyDisplay onto Lobby's Money Text.");
        return true;
    }

    static Transform FindByName(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        }
        return null;
    }
}
