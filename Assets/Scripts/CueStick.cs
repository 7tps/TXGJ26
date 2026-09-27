using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Aim: the cue points at the mouse.
// Shoot: hold left mouse and drag away from the ball to pull the cue back, release to strike.
// Cancel: right click while pulling back.
public class CueStick : MonoBehaviour
{
    // Fired the moment the tip hits the cue ball
    public event Action ShotTaken;

    // Fired with it, carrying the strike power from 0 to 1
    public event Action<float> CueStruck;

    [SerializeField] float tipGap = 0.2f; // resting distance between the tip and the ball's edge
    [SerializeField] float maxPull = 4f; // how far the cue can be pulled back (world units)
    [SerializeField] float minPull = 0.25f; // releasing with less than this cancels the shot
    [SerializeField] float maxShotSpeed = 25f; // cue speed (and so ball speed) at max pull (units per second)
    [SerializeField] float followThroughTime = 0.2f; // how long the cue stays on the ball after impact

    [Header("Power upgrade")]
    [SerializeField] float pullPerLevel = 1f; // added to maxPull per upgrade level
    [SerializeField] float speedPerLevel = 5f; // added to maxShotSpeed per upgrade level

    float baseMaxPull, baseMaxShotSpeed; // maxPull/maxShotSpeed at level 0, captured before any upgrade is applied

    enum Phase { Hidden, Aiming, Charging, Striking, FollowThrough }

    SpriteRenderer sr;
    Camera cam;
    Ball cueBall;
    float halfLength; // half the cue's length in world units
    Phase phase = Phase.Hidden;
    Vector2 aimDir = Vector2.right;
    Vector2 dragStart;
    float pull;
    float tipDistance; // gap between the tip and the ball's edge
    float strikeSpeed;
    float followTimer;

    // For the aim guide: true while the player is lining up or pulling back a shot
    public bool IsAiming => phase == Phase.Aiming || phase == Phase.Charging;
    public Vector2 AimDirection => aimDir;

    // The speed the cue ball would leave at if the shot were released now. While
    // just aiming this is full power. Pulling back less than the minimum gives 0
    // because that shot would be cancelled.
    public float PreviewSpeed
    {
        get
        {
            if (phase == Phase.Aiming) return maxShotSpeed;
            if (phase == Phase.Charging && pull >= minPull) return pull / maxPull * maxShotSpeed;
            return 0f;
        }
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 20; // above the balls
        halfLength = sr.sprite.bounds.extents.x * transform.lossyScale.x;
        cam = Camera.main;
        baseMaxPull = maxPull;
        baseMaxShotSpeed = maxShotSpeed;
        Hide();
    }

    public void Show(Ball ball)
    {
        cueBall = ball;
        phase = Phase.Aiming;
        pull = 0f;
        tipDistance = tipGap;
        sr.enabled = true;
        UpdateTransform();
    }

    public void Hide()
    {
        phase = Phase.Hidden;
        sr.enabled = false;
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Aiming:
            case Phase.Charging:
                HandleInput();
                break;
            case Phase.Striking:
                Strike();
                break;
            case Phase.FollowThrough:
                followTimer -= Time.deltaTime;
                if (followTimer <= 0f) Hide();
                break;
        }
    }

    void HandleInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());

        if (phase == Phase.Aiming)
        {
            Vector2 toMouse = mouseWorld - cueBall.Rb.position;
            if (toMouse.sqrMagnitude > 0.01f) aimDir = toMouse.normalized;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                phase = Phase.Charging;
                dragStart = mouseWorld;
            }
        }
        else
        {
            // Dragging opposite the aim direction pulls the cue back
            pull = Mathf.Clamp(Vector2.Dot(dragStart - mouseWorld, aimDir), 0f, maxPull);

            if (mouse.rightButton.wasPressedThisFrame)
            {
                phase = Phase.Aiming;
                pull = 0f;
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                Release();
            }
        }

        tipDistance = tipGap + pull;
        UpdateTransform();
    }

    void Release()
    {
        if (pull < minPull)
        {
            phase = Phase.Aiming;
            pull = 0f;
            return;
        }

        // The further the cue was pulled back, the faster it comes forward
        strikeSpeed = pull / maxPull * maxShotSpeed;
        phase = Phase.Striking;
    }

    // Drives the cue forward until the tip touches the ball, then hands the ball the cue's speed
    void Strike()
    {
        tipDistance -= strikeSpeed * Time.deltaTime;

        if (tipDistance <= 0f)
        {
            tipDistance = 0f;
            cueBall.Hit(aimDir * strikeSpeed);

            phase = Phase.FollowThrough;
            followTimer = followThroughTime;
            CueStruck?.Invoke(strikeSpeed / maxShotSpeed);
            ShotTaken?.Invoke();
        }

        UpdateTransform();
    }

    // The sprite's tip is on its right (+x) side, so rotate +x onto the aim direction
    void UpdateTransform()
    {
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        float distance = cueBall.WorldRadius + tipDistance + halfLength;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.position = cueBall.Rb.position - aimDir * distance;
    }

    // Sets maxPull/maxShotSpeed from the upgrade level directly, rather than nudging them - so calling
    // this again with the same level (e.g. every time the gameplay scene reloads) is always correct,
    // instead of compounding.
    public void ApplyPowerLevel(int level)
    {
        maxPull = baseMaxPull + level * pullPerLevel;
        maxShotSpeed = baseMaxShotSpeed + level * speedPerLevel;
    }
}
