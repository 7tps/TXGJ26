using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Steady Hand power readout")]
    [SerializeField] float readoutFontSize = 3f;
    [SerializeField] float readoutGap = 0.15f; // space between the top of the cue ball and the readout (world units)
    [SerializeField] int readoutBarSegments = 20;

    float powerCurve = 1f; // 1 = linear; Steady Hand raises it for finer control at low/mid power
    bool showPowerReadout;
    TextMeshPro powerLabel;

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
            if (phase == Phase.Charging && pull >= minPull) return PowerFraction(pull) * maxShotSpeed;
            return 0f;
        }
    }

    // 0-1 power for a given pull. Full pull is always full power; the curve only reshapes the middle.
    float PowerFraction(float pullAmount)
    {
        if (maxPull <= 0f) return 0f;
        return Mathf.Pow(Mathf.Clamp01(pullAmount / maxPull), powerCurve);
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 20; // above the balls
        halfLength = sr.sprite.bounds.extents.x * transform.lossyScale.x;
        cam = Camera.main;
        Hide();
    }

    // Upgrades are read in Start, not Awake: by then the tutorial sandbox (swapped in when a tutorial
    // scene finishes loading, see TutorialSession) is already in place, so the tutorial's cue reads the
    // tutorial's own upgrades
    void Start()
    {
        if (Upgrades.Instance != null)
        {
            maxPull = Upgrades.Instance.CurrentMaxPull;
            maxShotSpeed = Upgrades.Instance.CurrentMaxShotSpeed;
            powerCurve = Upgrades.Instance.CurrentPowerCurve;
            showPowerReadout = Upgrades.Instance.SteadyHandLevel > 0;
        }

        if (showPowerReadout) CreatePowerLabel();
    }

    void OnDestroy()
    {
        if (powerLabel != null) Destroy(powerLabel.gameObject);
    }

    // Not parented to the cue: the cue rotates, and a child would also get copied when GameEngine
    // clones this object for Extra Cue Ball's second stick
    void CreatePowerLabel()
    {
        powerLabel = new GameObject("Power Readout").AddComponent<TextMeshPro>();
        powerLabel.fontSize = readoutFontSize;
        powerLabel.alignment = TextAlignmentOptions.Bottom;
        powerLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        powerLabel.rectTransform.sizeDelta = new Vector2(6f, 1f);
        powerLabel.color = Color.white;
        powerLabel.outlineWidth = 0.25f;
        powerLabel.outlineColor = Color.black;
        powerLabel.sortingOrder = 21; // above the cue
        powerLabel.gameObject.SetActive(false);
    }

    void UpdatePowerLabel()
    {
        if (powerLabel == null) return;

        bool visible = phase == Phase.Charging;
        powerLabel.gameObject.SetActive(visible);
        if (!visible) return;

        bool willShoot = pull >= minPull;
        float fraction = willShoot ? PowerFraction(pull) : 0f;
        int filled = Mathf.RoundToInt(fraction * readoutBarSegments);

        string bar = $"<color=#FFE066>{new string('|', filled)}</color><color=#555555>{new string('|', readoutBarSegments - filled)}</color>";
        string percent = willShoot ? $"{fraction * 100f:0}%" : "<color=#888888>cancel</color>";
        powerLabel.text = $"{percent}\n{bar}";

        powerLabel.transform.position = (Vector3)(cueBall.Rb.position + Vector2.up * (cueBall.WorldRadius + readoutGap));
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
        UpdatePowerLabel();
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

            // A click on a UI button (pause, tutorial Next, ...) shouldn't also start pulling the cue back
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse.leftButton.wasPressedThisFrame && !overUI)
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
        UpdatePowerLabel();
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
        strikeSpeed = PowerFraction(pull) * maxShotSpeed;
        phase = Phase.Striking;
        UpdatePowerLabel();
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

    // Set this to change how far the cue can be pulled back, e.g. from an upgrade
    public void SetMaxPull(float value)
    {
        maxPull = Mathf.Max(0f, value);
    }

    // Set this to change the cue's max speed at full pull, e.g. from an upgrade
    public void SetMaxShotSpeed(float value)
    {
        maxShotSpeed = Mathf.Max(0f, value);
    }
}
