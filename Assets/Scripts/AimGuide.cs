using System.Collections.Generic;
using UnityEngine;

// Draws a GameEngine.ShotPrediction with line renderers:
//   white ring    where the cue ball will be when it hits (or comes to rest)
//   white line    the cue ball's path up to that point
//   blue line     the cue ball's path after the impact
//   yellow line   the path of the ball it hits
public class AimGuide : MonoBehaviour
{
    const int RingPoints = 40;

    LineRenderer approach, moverAfter, targetAfter, ring;

    void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Material material = new Material(shader);

        approach = MakeLine("Approach", material, new Color(1f, 1f, 1f, 0.7f), 0.06f);
        moverAfter = MakeLine("Cue Ball After", material, new Color(0.5f, 0.8f, 1f, 0.7f), 0.05f);
        targetAfter = MakeLine("Target After", material, new Color(1f, 0.9f, 0.3f, 0.8f), 0.05f);
        ring = MakeLine("Ghost Ball", material, new Color(1f, 1f, 1f, 0.9f), 0.04f);
        ring.loop = true;

        Hide();
    }

    public void Show(GameEngine.ShotPrediction prediction, float ballRadius)
    {
        gameObject.SetActive(true);

        SetPath(approach, prediction.approachPath);
        SetPath(moverAfter, prediction.moverPathAfter);
        SetPath(targetAfter, prediction.target != null ? prediction.targetPathAfter : null);
        SetRing(prediction.contactPosition, ballRadius);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    LineRenderer MakeLine(string lineName, Material material, Color color, float width)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sortingOrder = 15; // above the balls, below the cue
        return line;
    }

    void SetPath(LineRenderer line, List<Vector2> points)
    {
        if (points == null || points.Count < 2)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
    }

    void SetRing(Vector2 centre, float radius)
    {
        ring.enabled = true;
        ring.positionCount = RingPoints;
        for (int i = 0; i < RingPoints; i++)
        {
            float angle = i * Mathf.PI * 2f / RingPoints;
            ring.SetPosition(i, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
