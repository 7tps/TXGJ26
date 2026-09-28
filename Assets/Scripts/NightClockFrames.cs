using UnityEngine;

// The clock sprites in order, frame 0 (no progress) to the last frame (full circle). Lives in Resources
// so NightClock can load it in any scene without a reference to place. Filled from Assets/Sprites/clock it
// by NightClockFramesSync, so there's nothing to assign by hand.
public class NightClockFrames : ScriptableObject
{
    public Sprite[] frames;
}
