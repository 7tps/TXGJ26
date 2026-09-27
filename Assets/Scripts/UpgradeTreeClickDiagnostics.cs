using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TEMPORARY - delete once node clicks are working. Appears by itself whenever the Upgrade Tree scene
// is running and prints, on every left click, everything the UI would hit under the mouse and the
// state of the node button it lands on. It exists to answer "does the click reach the node at all?".
public class UpgradeTreeClickDiagnostics : MonoBehaviour
{
    // Hooked before the first scene so it also fires when the tree is reached from the Lobby, not just
    // when it's the scene Play was pressed in
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Upgrade Tree") return;
        new GameObject("UpgradeTreeClickDiagnostics").AddComponent<UpgradeTreeClickDiagnostics>();
    }

    void Start()
    {
        EventSystem es = EventSystem.current;
        UpgradeProgress p = UpgradeProgress.Instance;
        Debug.Log($"[Diag] Upgrade Tree running. EventSystem: {(es == null ? "MISSING" : es.currentInputModule != null ? es.currentInputModule.GetType().Name : "no input module")}. " +
                  $"UpgradeProgress: {(p == null ? "MISSING" : "$" + p.Money)}. Upgrades: {(Upgrades.Instance == null ? "MISSING" : "ok")}. " +
                  $"Node buttons: {FindObjectsByType<UpgradeTreeNodeUI>(FindObjectsSortMode.None).Length}.");
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        EventSystem es = EventSystem.current;
        if (es == null) { Debug.Log("[Diag] Click, but there is no EventSystem."); return; }

        List<RaycastResult> hits = new List<RaycastResult>();
        es.RaycastAll(new PointerEventData(es) { position = mouse.position.ReadValue() }, hits);

        StringBuilder sb = new StringBuilder($"[Diag] Click at {mouse.position.ReadValue()}: {hits.Count} UI hit(s), top first:");
        foreach (RaycastResult h in hits)
            sb.Append($"\n  {Path(h.gameObject.transform)}");

        if (hits.Count > 0)
        {
            Button b = hits[0].gameObject.GetComponentInParent<Button>();
            UpgradeTreeNodeUI node = hits[0].gameObject.GetComponentInParent<UpgradeTreeNodeUI>();
            sb.Append(b == null ? "\n  -> top hit is not inside a Button" : $"\n  -> Button '{b.name}' interactable={b.interactable}, node component={(node != null ? "yes" : "NO")}");
        }
        Debug.Log(sb.ToString());
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
