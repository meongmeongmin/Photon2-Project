# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Unity 6000.3.8f1 project. A 2D physics-based co-op game built on Photon Fusion netcode: four players
jointly control a single ragdoll-style robot, one player per limb (two legs, two arms), driven by mouse
position and 2-bone IK, with host-authoritative physics. The only scene is `Assets/Scenes/3MainScene.unity`.

## Setup

`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` holds the real Photon Fusion App ID and is
gitignored. On a fresh clone:

1. Copy `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset.template` to `PhotonAppSettings.asset`.
2. Set `AppIdFusion` to a Fusion App ID from https://dashboard.photonengine.com (shared via the team channel).
3. Open Fusion's Realtime Settings window in the editor to confirm it picked up the value.

(See `Assets/Photon/Fusion/Resources/SETUP.md` for the same steps in Korean.)

## In-progress: physical-joint robot conversion

`Docs/물리_관절형_로봇_전환_설계서.md` (Korean) is the design doc for an in-progress rewrite of the robot
from the current Transform-driven 2-bone IK (described below) to real `Rigidbody2D`/`HingeJoint2D` physics
chains with PD-motor joint control, host-authoritative as today. The plan is staged (offline single-leg
prototype first, then full rig, then Photon wiring, then removal of the old IK path) and calls for new code
to land under `Assets/Scripts/RobotPhysics/` and `Assets/Resources/Prefabs/Physical *.prefab` alongside the
existing files, gated by a temporary `usePhysicalJointRobot` flag on `UIManager` — not by editing
`BodyController`/`LegManager`/`ArmManager` in place. Read that doc before making structural changes to the
robot's physics.

## Commands

This is a Unity Editor project — there is no CLI build/lint/test pipeline.

- **Run/iterate**: open `3MainScene.unity` and press Play in the Editor.
- **Local multiplayer testing**: menu `Tools > Run Multiplayer > Win64 / Mac > {1-4} Players`
  (`Assets/Scripts/Editor/MultiplayerBuildAndRun.cs`). Win64 builds one standalone player and launches N
  copies of it so you can test a 2-4 player session on a single machine; Mac builds N separately-numbered
  `.app` bundles instead.
- **Tests**: none exist yet. `com.unity.test-framework` is a package dependency but there are no test
  assemblies or test files in the project.

## Architecture

### Gameplay code lives in `Assets/Scripts/LegTest/`

Despite the folder name, this holds the actual game, not throwaway tests. Everything compiles into the
default `Assembly-CSharp` assembly — there's no `.asmdef` splitting gameplay code from Fusion.

### The robot is five separate NetworkObjects, not a Transform hierarchy

Fusion does not support nesting NetworkObjects, so the torso and all four limbs are spawned as independent
top-level NetworkObjects and only glued together visually, every frame, via anchor points:

- `BodyController.cs` — the torso. Owns the `Rigidbody2D` physics and exposes four anchor `GameObject`s
  (`sholderL`, `sholderR`, `pelvisL`, `pelvisR`) that the limbs snap their root transform onto.
- `LegManager.cs` (spawned twice, Left/Right) — 2-bone IK leg (thigh/shin), foot planting, and produces
  `PelvisPull` (a movement request) and `StandPressure` for the torso to consume.
- `ArmManager.cs` (spawned twice, Left/Right) — 2-bone IK arm (upper arm/forearm), produces a
  `-1..1` torso-lean request via `GetTorsoLeanRequest()`.
- `UIManager.SpawnLimb()` does the actual spawning and anchor wiring, triggered from
  `UIManager.OnPlayButtonPressed()` when the host presses Play.
- The four `sholder*` fields are spelled that way everywhere (typo for "shoulder", not `shoulderL`).
  It's load-bearing: prefabs reference these fields by name through Unity's serialization, so silently
  "fixing" the spelling will detach the Inspector-wired anchor references.

### Host-authority model

Only the client with State Authority (the host) ever runs physics or gameplay logic. Almost every method
that computes something starts with an `Object.HasStateAuthority` guard and returns early otherwise —
non-authority clients just display whatever Fusion already synced (`NetworkTransform`, `[Networked]`
fields). Each limb's Input Authority is a single player, assigned in join order when spawned
(`leftLeg`, `rightLeg`, `leftArm`, `rightArm` — see `UIManager.OnPlayButtonPressed`): that player's mouse
position (`NetworkInputData.MouseWorldPos`, collected per-tick in `NetworkManager.OnInput`) drives that one
limb, but the simulation itself still only runs on the host, inside `BodyController.FixedUpdateNetwork()`.

### Per-tick order in `BodyController.FixedUpdateNetwork()` is intentional, not incidental

Legs are simulated first (their `PelvisPull`/`StandPressure` decide torso movement), then torso physics
forces are applied, then arms are simulated and their lean request applies torque. After Fusion moves the
`Rigidbody2D`, `BodyController.AfterTick()` (`IAfterTick`) re-snaps every limb's root to the torso's new
anchor position — skip that and limbs visually detach from the torso for one tick after the body moves.

### Networking/session glue

- `NetworkManager.cs` — wraps `NetworkRunner` (room create/join, `INetworkRunnerCallbacks`, per-tick input
  collection).
- `SessionRpc.cs` — one instance, spawned by the host right after session start; relays room-wide RPCs
  (chat, screen change) via `[Rpc(RpcSources.All, RpcTargets.All)]`.
- `Chatting.cs` — lobby/room list UI and chat panel logic.
- `UIManager.cs` — canvas switching, robot/limb spawning and player assignment, camera follow (position
  only — rotation is intentionally not inherited from the torso, so the camera doesn't spin when the robot
  falls over).

### Comment style

Code comments (and most in-editor strings) are in Korean. When adding or editing comments, match the
existing tone: plain, concrete wording that reads clearly in one pass — avoid dense Sino-Korean technical
compressions (e.g. prefer "발이 땅에 닿았는지" over "접지 여부") even when a shorter jargon term exists.
