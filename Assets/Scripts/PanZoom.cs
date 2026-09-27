using UnityEngine;
using UnityEngine.EventSystems;

// Click-and-drag panning and scroll-wheel zoom for a RectTransform, driven from wherever this
// component sits - it should be on an ancestor of everything the player can click (a node button
// mid-drag still triggers a click normally if the pointer barely moves; past Unity's drag threshold
// it pans instead, and OnScroll reaches this component from anywhere underneath it too, via UGUI's
// normal event bubbling). Attach to the tree's Viewport object; point `target` at its Content child -
// this component's own RectTransform (on Viewport) is used as the visible area to clamp/fit against.
public class PanZoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    [SerializeField] RectTransform target;
    [SerializeField] float maxZoom = 3.5f;
    [SerializeField] float defaultZoom = 2f; // zoom level the view opens at
    [SerializeField] float zoomStep = 0.15f; // scale change per scroll notch

    RectTransform Viewport => (RectTransform)transform;

    void Awake()
    {
        if (target == null) return;

        float minZoom = MinZoomToFit();
        target.localScale = Vector3.one * Mathf.Clamp(defaultZoom, minZoom, maxZoom);
        ClampPosition();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Nothing to do here - this only exists because UGUI won't route OnDrag to an object at all
        // unless that same object (or an ancestor) also implements IBeginDragHandler.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;

        // eventData.delta is in screen pixels; dividing by the target's own lossy scale keeps the
        // content tracking the mouse 1:1 regardless of the current zoom level or canvas scaling.
        target.anchoredPosition += eventData.delta / target.lossyScale.x;
        ClampPosition();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (target == null) return;

        float minZoom = MinZoomToFit();
        float notches = eventData.scrollDelta.y;
        float newScale = Mathf.Clamp(target.localScale.x + notches * zoomStep, minZoom, maxZoom);
        target.localScale = new Vector3(newScale, newScale, 1f);
        ClampPosition(); // zooming out shrinks how far off-centre the content is allowed to sit
    }

    // The scale at which the whole content rect just fits inside the viewport on both axes - "zoomed
    // all the way out" always means "see the entire tree", however big the tree or the screen is,
    // rather than some fixed number that would only happen to be right for one particular tree size.
    float MinZoomToFit()
    {
        Vector2 viewportSize = Viewport.rect.size;
        Vector2 contentSize = target.rect.size;
        if (contentSize.x <= 0f || contentSize.y <= 0f) return 0.1f;
        return Mathf.Min(viewportSize.x / contentSize.x, viewportSize.y / contentSize.y);
    }

    // Keeps the content covering the viewport - never far enough that empty space beyond the tree's
    // own bounds shows on either side. When the (scaled) content is smaller than the viewport on an
    // axis - which happens exactly at minimum zoom, by definition of MinZoomToFit - that axis is
    // pinned to 0, so "zoomed all the way out" is also always centred, not just fully visible.
    void ClampPosition()
    {
        Vector2 viewportSize = Viewport.rect.size;
        Vector2 scaledContentSize = target.rect.size * target.localScale.x;

        Vector2 maxOffset = Vector2.Max(Vector2.zero, (scaledContentSize - viewportSize) / 2f);
        Vector2 pos = target.anchoredPosition;
        target.anchoredPosition = new Vector2(
            Mathf.Clamp(pos.x, -maxOffset.x, maxOffset.x),
            Mathf.Clamp(pos.y, -maxOffset.y, maxOffset.y));
    }
}
