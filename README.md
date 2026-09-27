# TXGJ26

A 2D billiards game built in Unity for a game jam. Currently a playable prototype: rack of 15 balls plus a cue ball, mouse-controlled cue, and working pockets. Full 8-ball rules are not implemented yet.

## Requirements

- Unity **6000.0.66f2** (Unity 6), 2D / Universal Render Pipeline
- Input System package (already listed in `Packages/manifest.json`)

## Getting started

1. Clone the repo and open the folder in Unity Hub with the version above.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play.

## Controls

| Action | Input |
|---|---|
| Aim | Move the mouse. The cue points at the cursor. |
| Pull back | Hold left mouse and drag away from the ball. |
| Shoot | Release left mouse. The cue snaps forward and hits the ball. |
| Cancel a pull | Right click while pulling back. |

The further you pull the cue back, the faster it strikes and the harder the cue ball is hit. Releasing with a very short pull cancels the shot.

## How it works

| Script | Role |
|---|---|
| `Assets/Scripts/GameEngine.cs` | Builds invisible cushion colliders and pocket positions from the table sprite's geometry, spawns the cue ball and the rack, detects pocketed balls, and runs the turn loop (`Aiming` / `BallsMoving`). Pocketing the cue ball respawns it on the head spot. Exposes a `BallPocketed` event for future rules. |
| `Assets/Scripts/Ball.cs` | Configures its own physics (no gravity, damping for felt friction, continuous collision, bouncy material), snaps to rest at low speed, and plays the sink animation when pocketed. |
| `Assets/Scripts/CueStick.cs` | Aim, pull-back, strike and follow-through. Fires `ShotTaken` at the moment of impact. |
| `Assets/Scripts/AudioEngine.cs` | Placeholder for sound effects. |

## Scene setup

The `GameEngine` object needs these references (in the scene already):

- **Ball Prefab**: `Assets/Prefabs/Ball.prefab`
- **Ball Sprites**: all 16 sprites sliced from `Assets/Sprites/Temp/Balls1.png`. They are looked up by name: `Balls1_0` to `Balls1_14` are balls 1-15, and `Balls1_15` is the cue ball.
- **Table** and **Cue Stick**: found automatically if left empty (the object named `Table`, and the object with the `CueStick` component).

The `CueStick` component goes on the cue sprite object.

## Tuning

- **Table geometry** (`GameEngine`): felt bounds, pocket positions, gap sizes and capture radius are measured in sprite pixels from the centre of the table sprite. Sprites import at 32 pixels per unit, and the scale is read from the table sprite, so the values stay correct if the table is scaled. Select the `GameEngine` object in the scene to see the felt (yellow) and pocket capture circles (red) as gizmos.
- **Cue feel** (`CueStick`): Max Pull, Min Pull, Max Shot Speed, Tip Gap, Follow-through Time.
- **Ball feel** (`Ball`): Linear Damping, Bounciness, Stop Speed.

## Roadmap

- Group assignment (solids / stripes) and 8-ball win/loss
- Turn switching and fouls
- Sound effects via `AudioEngine`
- Aim guide line
