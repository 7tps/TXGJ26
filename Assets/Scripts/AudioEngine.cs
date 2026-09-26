using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AudioEngine : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] AudioClip ballCollision;
    [SerializeField] AudioClip cueStrong;
    [SerializeField] AudioClip cueWeak;

    [Header("Tuning")]
    [SerializeField, Range(0f, 1f)] float strongThreshold = 0.5f; // cue power at or above this plays the strong hit
    [SerializeField] float minBallImpact = 0.3f; // quieter ball collisions make no sound
    [SerializeField] float loudBallImpact = 12f; // ball impact speed that plays at full volume

    AudioSource source;
    CueStick cueStick;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D sound
    }

    void OnEnable()
    {
        Ball.BallsCollided += OnBallsCollided;

        cueStick = FindFirstObjectByType<CueStick>();
        if (cueStick != null) cueStick.CueStruck += OnCueStruck;
    }

    void OnDisable()
    {
        Ball.BallsCollided -= OnBallsCollided;
        if (cueStick != null) cueStick.CueStruck -= OnCueStruck;
    }

    void OnCueStruck(float power)
    {
        AudioClip clip = power >= strongThreshold ? cueStrong : cueWeak;
        Play(clip, Mathf.Lerp(0.5f, 1f, power));
    }

    void OnBallsCollided(float impactSpeed)
    {
        if (impactSpeed < minBallImpact) return;
        Play(ballCollision, Mathf.Clamp(impactSpeed / loudBallImpact, 0.15f, 1f));
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        source.PlayOneShot(clip, volume);
    }

}
