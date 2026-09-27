using TMPro;
using UnityEngine;

// A single shared tooltip box that follows the mouse, shown by whichever UpgradeTreeNodeUI is
// currently hovered (see its OnPointerEnter/Exit) and hidden the moment the pointer leaves it. Lives
// directly on the Canvas, outside the pan/zoomable tree content, so it always reads at a fixed,
// legible size no matter the tree's current zoom level.
public class Tooltip : MonoBehaviour
{
    public static Tooltip Instance { get; private set; }

    [SerializeField] RectTransform panel;
    [SerializeField] TMP_Text text;
    [SerializeField] RectTransform canvasRect; // the root canvas, for converting the mouse position

    void Awake()
    {
        Instance = this;
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!panel.gameObject.activeSelf) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 local);
        panel.anchoredPosition = local + new Vector2(24, -24); // offset so the cursor doesn't sit on top of the text
    }

    public void Show(string content)
    {
        text.text = content;
        panel.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
