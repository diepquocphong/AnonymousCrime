# Franklin AirSystem

AirSystem provides two aircraft workflows for Franklin Game:

- A remotely controlled, return-to-home drone.
- A pilotable Gyrocopter with animated entry, semi-physical flight, automatic
  landing, and animated exit.

This document is the architecture contract for the current implementation.
It describes ownership, lifecycle ordering, prefab wiring, collision rules, and
mobile budgets that must remain synchronized with code and prefab changes.

Current contract date: **2026-08-18**

## Scope and integration boundary

AirSystem integrates through public Game Creator 2 APIs. It does not modify the
Game Creator 2 package and does not create a second physical Camera,
AudioListener, or EventSystem.

The implementation spans AirSystem and these project-owned integration points:

| Location | Responsibility |
| --- | --- |
| <code>Assets/Prefab/Player.prefab</code> | Hosts <code>ManagerAirSystem</code>, <code>DronePlayerController</code>, and the handheld-controller presentation. |
| <code>Assets/Scenes/GamePlay.unity</code> | Contains placed aircraft instances. Their world positions are scene-owned and are not part of this README contract. |
| <code>Assets/Prefab/Main Camera.prefab</code> | Existing GC2 Main Camera, AudioListener, and world <code>PhysicsRaycaster</code>. |
| <code>Assets/Ash Assets/Vehicle Integration</code> | Provides <code>CarEntry</code>, <code>IRvrVehicleInputController</code>, and occupant-collision contracts used by the Gyrocopter. |
| <code>Assets/FranklinAnimations/Runtime</code> | Provides vehicle discovery, animation/blob-shadow locks, death cleanup, and GC2 camera-shot ownership. |
| <code>FranklinMobileHud</code> | Provides reference-counted gameplay-control suppression while an aircraft UI owns input. |
| Existing Car/Bike VFX assets | Supply the shared explosion, collision-spark, metal-fragment, and explosion-audio references used by the drone. |

Current project baseline:

- Unity <code>6000.3.21f1</code>
- Universal Render Pipeline <code>17.3.0</code>
- Input System <code>1.20.0</code>
- UGUI <code>2.0.0</code>
- Game Creator 2 Core, Cameras, Characters, and Stats APIs

## Asset structure

~~~text
AirSystem/
├── Art/
│   ├── Animations/Gyrocopter/   Front Left Enter, Front Left Exit, Front Ride
│   ├── Audio/                   Drone and helicopter loops + license notices
│   ├── Materials/               Drone and Gyrocopter runtime materials
│   ├── Models/                  Drone 5 visual and Gyrocopter/door FBX files
│   ├── Textures/                Drone palette and Gyrocopter texture
│   └── UI/                      Native-alpha Air HUD sprites
├── Data/Stats/Drone.asset       GC2 Traits class used by the drone
├── DroneControllerModel/        Handheld FBX and its four remapped materials
├── Documentation/               Retained source-package PDF
├── Prefabs/
│   ├── Canvas Air Control.prefab
│   ├── Drone Controller Handheld.prefab
│   ├── Franklin Drone.prefab
│   └── Franklin Gyrocopter.prefab
├── Runtime/                     AirSystem runtime components
└── README.md
~~~

<code>Runtime/Resources</code> is currently reserved and empty. Runtime code
must not add a <code>Resources.Load</code> dependency without documenting it
here.

The six <code>Drone * UI.mat</code> files and
<code>Drone Connect UI.shader</code> still exist as legacy assets, but the
active Canvas does not reference them. Runtime UI Images use their source
sprites, native PNG alpha, and Unity's default UI material.

## Runtime component map

| Component | Host | Authority |
| --- | --- | --- |
| <code>DronePlayerController</code> | Player | Finds the nearest available drone, owns the remote-control session, reads desktop/gamepad/touch input, and leases Player/camera/HUD state. |
| <code>DroneControllerHandPresentation</code> | Player | Instantiates one handheld controller and blends both humanoid hands and elbow hints onto its grips. |
| <code>DroneFlightController</code> | Franklin Drone | Sole authority over drone Rigidbody state, piloted flight, return markers, obstacle avoidance, motor audio, and parked state. |
| <code>DroneRotorVisuals</code> | Franklin Drone | Spins four rotors at a capped visual update rate and is disabled while parked. |
| <code>DroneHealth</code> | Franklin Drone | Bridges collision damage to the GC2 Traits Health Attribute and publishes impact/health/destroyed events. |
| <code>DroneImpactEffects</code> | Franklin Drone | Lazily pools accepted-impact sparks and mobile metal fragments. |
| <code>DroneDamageEffects</code> | Franklin Drone | Prewarms and plays the terminal explosion, then disables flight, collision, and renderers. |
| <code>DroneMobileHud</code> | Canvas Air Control instance | Owns one safe-area-aware Air overlay, state visibility, move/lift input, and action dispatch. |
| HUD input components | Canvas Air Control | <code>DroneVirtualStick</code>, <code>DroneHoldButton</code>, <code>DroneOrbitArea</code>, and <code>DroneHudActionButton</code> track pointer ownership without per-frame allocation. |
| <code>DroneCameraOrbitInput</code> | Embedded GC2 ShotCamera input | Supplies touch drag, right-mouse drag, or gamepad right-stick delta; GC2 remains responsible for smoothing and automatic alignment. |
| <code>HelicopterFlightController</code> | Franklin Gyrocopter | Owns helicopter Rigidbody flight, parked state, auto-land, rotors, audio, Air HUD, and pair-specific occupant collision ignores. |
| <code>HelicopterDistanceLod</code> | Franklin Gyrocopter | Switches near/far/cull renderer groups using cached squared-distance checks. |
| <code>CarEntry</code> | Franklin Gyrocopter, outside AirSystem Runtime | Owns door animation, GC2 gestures/states, authored entry/exit paths, seat parenting, Character snapshots, and Player controllability. |
| <code>FranklinVehicleInteractionManager</code> | Player, outside AirSystem Runtime | Discovers Gyrocopters from their registry and owns the vehicle animation, blob-shadow, and GC2 camera-shot leases. |

## Controls

### Remote drone

| Action | Keyboard/mouse | Gamepad | Mobile |
| --- | --- | --- | --- |
| Connect | <code>F</code> | North button | Connect image button |
| Exit | <code>F</code> or <code>Escape</code> | East button | Exit image button |
| Move | <code>WASD</code> | Left stick | Left Air stick |
| Yaw | <code>Q/E</code> | Shoulder buttons | Heading follows camera center while moving forward |
| Ascend | <code>Space</code> | Right trigger | Up image button |
| Descend | <code>Left Ctrl</code>, <code>Right Ctrl</code>, or <code>C</code> | Left trigger | Down image button |
| Orbit GC2 shot | Hold right mouse and drag | Right stick | Drag the free right-side orbit area |

Forward flight uses the GC2 Main Camera center direction, including its vertical
component. Looking up or down therefore adds smooth automatic climb/descent;
the Up/Down controls add manual vertical input to that camera-relative command
before the combined velocity is clamped.

### Gyrocopter

| Action | Keyboard | Gamepad | Mobile |
| --- | --- | --- | --- |
| Enter | <code>E</code> through normal vehicle interaction | No AirSystem binding | Existing mobile Enter button |
| Exit/request auto-land | <code>F</code> or <code>Escape</code> | East button | Air Exit image button |
| Cancel pending auto-land | Press Exit again | Press East again | Press Exit again |
| Cyclic movement | <code>WASD</code> or arrow keys | Left stick | Left Air stick |
| Yaw | <code>Q/E</code> | Shoulder buttons | Forward flight aligns toward camera heading |
| Collective up/down | <code>Space</code> / <code>Ctrl</code> or <code>C</code> | Right/left triggers | Up/Down image buttons; hold Down on the ground for 3 s to auto-exit |
| Orbit GC2 shot | Right mouse drag | Right stick | Drag the free right-side orbit area |

## Architecture graph

~~~mermaid
flowchart LR
    Player["Player.prefab / ManagerAirSystem"] --> DPC["DronePlayerController"]
    Player --> Hand["DroneControllerHandPresentation"]
    DPC --> HUD["Canvas Air Control"]
    DPC --> MainCamera["GC2 MainCamera and ShotCamera"]
    DPC --> Drone["DroneFlightController"]
    Drone --> DroneShot["Embedded GC2 Drone Shot"]
    Drone --> Markers["Pooled private GC2 Markers"]
    Drone --> Audio["Motor AudioSource"]
    Drone --> Health["DroneHealth and GC2 Traits"]
    Health --> Impact["Pooled impact effects"]
    Health --> Explosion["Prewarmed terminal explosion"]

    Manager["FranklinVehicleInteractionManager"] --> Entry["CarEntry"]
    Manager --> Helicopter["HelicopterFlightController"]
    Entry <--> Helicopter
    Helicopter --> HUD
    Manager --> MainCamera
    Helicopter --> HeliShot["Embedded GC2 Gyrocopter Shot"]
~~~

## Core design principles

1. **Adapters, not plugin edits.** AirSystem uses GC2 Character, Traits, Marker,
   Easing, MainCamera, and ShotCamera public APIs. GC2 package code is not an
   extension point for AirSystem fixes.
2. **One owner per subsystem.** Drone session state belongs to
   <code>DronePlayerController</code>; drone physics belongs to
   <code>DroneFlightController</code>; Gyrocopter flight belongs to
   <code>HelicopterFlightController</code>; Gyrocopter Character transitions
   belong to <code>CarEntry</code>; vehicle camera ownership belongs to
   <code>FranklinVehicleInteractionManager</code>.
3. **Lease symmetry.** Player controllability, animation locks, HUD suppression,
   world raycaster state, camera shots, hand IK, and collider-pair ignores must
   have matching acquire/release paths, including disable, death, ragdoll,
   camera takeover, and explicit exit. Focus loss and application pause retain
   the active session leases and only clear transient control input.
4. **Restore only owned state.** A camera owner restores its previous shot only
   when the current shot is still the one it acquired. Player/HUD leases are
   always returned even if another system has taken the camera.
5. **Snapshot before mutation.** Character collider, Rigidbody, Driver collision,
   Driver kinematics, movement type, and NPC Animator-culling values are recorded
   before they are disabled for a seat.
6. **Physics in fixed time.** Rigidbody forces, velocity limits, and flight
   envelopes run in <code>FixedUpdate</code>. Camera pivots and rotor visuals are
   presentation work and run separately.
7. **Parked work is removed.** Parked aircraft are kinematic with Discrete
   collision and no interpolation. Expensive rotor, audio, HUD, camera-shot, and
   physics work is disabled when it is not needed.
8. **Bounded long-session memory.** Marker arrays, NonAlloc hit buffers, and
   effect pools have explicit caps. Collider lists are cached and reused. No
   aircraft is instantiated and destroyed on each control switch.
9. **Lifecycle-safe async.** Gyrocopter entry/exit continuations are guarded by
   lifecycle and driver-transition versions. A stale continuation must never
   reattach or re-enable a released Character.
10. **Mobile settings remain project-owned.** AirSystem does not write
    <code>Application.targetFrameRate</code> or compete with graphics/thermal
    profiles.

## Ownership matrix

| Resource | Acquired by | Released/restored by | Invariant |
| --- | --- | --- | --- |
| Player <code>IsControllable</code> during drone control | <code>DronePlayerController</code> | Same component on every session end | Camera loss cannot leak the Player lease. |
| Franklin animation lock and handheld IK | <code>DronePlayerController</code> | Same component, including <code>OnDisable</code> | Controller model is reused, not recreated per flight. |
| Gameplay HUD suppression | Drone or Gyrocopter controller | The same owner that acquired it | Uses <code>FranklinMobileHud</code> reference counting. |
| World <code>PhysicsRaycaster</code> | Drone session only | <code>DronePlayerController</code> | Original enabled state is restored exactly. |
| Drone GC2 shot | <code>DronePlayerController</code> | Same component if it still owns the current shot | Drone shot component is enabled only while piloted. |
| Gyrocopter GC2 shot | <code>FranklinVehicleInteractionManager</code> | Same manager after exit/death/disable | <code>HelicopterFlightController</code> does not restore camera shots. |
| Drone Rigidbody | <code>DroneFlightController</code> | Same component through parked/piloted/return states | External code calls <code>TryAcquire</code>/<code>Release</code>, not Rigidbody toggles. |
| Gyrocopter Rigidbody and root-collider state | <code>HelicopterFlightController</code> for steady flight/parked state; <code>CarEntry</code> temporarily during cabin transitions | <code>CarEntry</code> hands back through the vehicle contract; the flight controller reasserts the steady state | No other component writes ongoing flight physics. Temporary <code>isKinematic</code>/<code>isTrigger</code> mutation belongs to <code>CarEntry</code>. |
| Gyrocopter Character snapshot/seat | <code>CarEntry</code> | <code>CarEntry</code> | Restore occurs outside the cabin before normal collision resumes. |
| Player–Gyrocopter ignored collider pairs | <code>HelicopterFlightController</code> on request from <code>CarEntry</code> | <code>finally</code> cleanup | No global collision-matrix mutation. |

## Drone lifecycle

~~~mermaid
stateDiagram-v2
    [*] --> Parked
    Parked --> Piloted: TryAcquire(owner)
    Piloted --> Returning: Release(owner)
    Piloted --> Returning: normal exit or camera lease loss
    Returning --> Parked: final home Marker reached
    Piloted --> Destroyed: Health reaches zero
    Returning --> Destroyed: Health reaches zero
    Destroyed --> [*]
~~~

### Discovery and control session

- Each enabled <code>DroneFlightController</code> registers itself in a static
  registry. The Player refreshes the nearest eligible drone every 0.15
  seconds; no scene-wide search runs per frame.
- Connect is rejected when the drone is owned, returning, destroyed, missing its
  body/shot, outside the interaction range, or a project modal has suppressed
  normal/fast movement.
- On acquire, the controller records the current GC2 shot and Player
  controllability, locks Franklin locomotion animation, displays the handheld
  controller, suppresses gameplay HUD controls, disables the world
  <code>PhysicsRaycaster</code>, and changes to the embedded drone shot.
- Player death, camera ownership loss, component disable, or explicit Exit all
  use the same idempotent cleanup path.
- Focus loss or application pause clears the cached HUD values and current
  drone input so a missing pointer-up event cannot survive the interruption.
  Keyboard and gamepad state are sampled live rather than stored.

### Flight and camera

- Manual movement is camera-relative. The full camera-forward vector controls
  forward movement; planar camera right controls strafe.
- Horizontal and vertical speeds are capped at 18 m/s and 8 m/s by the prefab.
  Acceleration and braking are bounded, and near-zero idle velocity is snapped
  to zero.
- Yaw can be explicit, while forward input also aligns heading toward the camera
  center.
- Pitch and roll use GC2 <code>Easing</code>. The dedicated camera pivot is
  updated upright in <code>LateUpdate</code>, isolating the shot from body tilt
  and reducing visual vibration.
- The embedded Shot remains GC2 Third Person with orbit and automatic alignment.
  <code>DroneCameraOrbitInput</code> supplies only delta input; GC2 owns pitch
  limits, damping, and alignment.

### Return home

- The launch pose is the first private GC2 <code>Marker</code>. While piloted,
  another marker is sampled every 3 seconds unless it is within 0.75 m of the
  previous marker.
- Exit records a final marker and follows the breadcrumb list backwards. The
  markers are private routing data and are removed from GC2's global spatial
  hash.
- The array is capped at 48 markers. When full, it is compacted in place and its
  marker GameObjects are reused.
- Return steering uses <code>SphereCastNonAlloc</code> with an eight-hit buffer.
  It tests the direct/velocity paths first, then scores horizontal and vertical
  detours only when blocked. A short direction hold prevents rapid oscillation.
- Corner speed is bounded. Inside the final 25 m, GC2
  <code>QuadInOut</code> easing and stopping-distance math reduce 24 m/s return
  speed toward a 1.5 m/s final approach.
- At the home marker, position/yaw are restored exactly and the drone returns to
  the parked contract.

### Health, impact, explosion, audio, and handheld presentation

- <code>Data/Stats/Drone.asset</code> supplies the GC2 Traits class. The prefab
  overrides base Health to 35.
- Accepted collision damage is event-driven, rate-limited, and scaled from
  impact speed. It drives both spark/metal-fragment presentation and the Traits
  Health Attribute.
- Spark effects use a lazy two-slot pool. Metal fragments use one capped
  ParticleSystem; the settings that disable trails and particle collision apply
  to these generated fragments, not necessarily every child module in the shared
  Collision Spark source prefab.
- The terminal explosion is prewarmed once. It uses non-generic
  <code>UnityEngine.Object.Instantiate</code> so legacy component-root particle
  prefabs cannot throw the generic <code>InvalidCastException</code>.
- Reaching zero Health disables flight/collision/renderers, freezes the body,
  stops the Player session through <code>EventControlLost</code>, plays the
  prewarmed effect/audio, and disables GC2 Traits ticking for the terminal drone.
- The motor AudioSource runs only while piloted or returning. Volume and pitch
  are smoothed from input/speed load and the source is stopped while parked.
- The handheld FBX is instantiated once. Its Animator/colliders are disabled;
  shadows and motion vectors are disabled; both hands use humanoid IK plus elbow
  hints with blend-in/out to prevent crossed arms and hard pose pops.

## Gyrocopter lifecycle

~~~mermaid
stateDiagram-v2
    [*] --> Parked
    Parked --> Approaching: E or Mobile Enter
    Approaching --> Entering: door area reached
    Approaching --> Parked: rejected, timeout, death, or ragdoll
    Entering --> Flying: door and Standing to Step to Seat path
    Entering --> Parked: lifecycle cleanup
    Flying --> AutoLanding: Exit request or grounded Air HUD Down hold for 3 s
    AutoLanding --> Flying: Exit pressed again
    AutoLanding --> Exiting: ground and stability gate reached
    Exiting --> Parked: Seat to Step to Standing and lease restore
~~~

### Prefab contract

The root <code>Franklin Gyrocopter</code> is on layer 14
(<code>Air</code>) and contains:

- One 520 kg Rigidbody.
- One fuselage BoxCollider.
- <code>CarEntry</code>, <code>HelicopterFlightController</code>, and
  <code>HelicopterDistanceLod</code>.
- One stopped-at-rest looping AudioSource.
- Three active landing WheelCollider objects. The legacy <code>WheelRamp</code>
  and mesh <code>Collision</code> objects remain inactive.

Required authored transforms:

- <code>EnterL</code>: exterior standing point.
- <code>Driver Entry Step</code>: doorway waypoint.
- <code>Driver Seat</code>: final Character parent.
- The outer/top-level <code>DoorL</code> holder: animated door pivot. Do not
  assign the imported mesh child.
- <code>Lift Point</code>: force application point.
- <code>Camera Pivot</code> and <code>Helicopter Camera Shot</code>: the GC2
  Third Person camera pair.
- <code>PropTop</code> and <code>Prop</code>: main and tail rotor transforms.

The root and door Animators stay disabled. <code>CarEntry</code> owns door pose;
<code>HelicopterFlightController</code> owns manual rotor pose. Enabling the
legacy Animators creates competing transform writers.

The Character transition contract requires:

- <code>useRootMotion = false</code>
- <code>useAuthoredEntryPath = true</code>
- <code>UseSeatEntryAlignment = true</code>
- All three Standing/Step/Seat transforms assigned
- Front Left Enter, Front Left Exit, and looping Front Ride clips assigned

AirSystem uses the GC2 Character gesture/state APIs and does not replace the
Player Animator Controller with the legacy <code>MaleDriver</code> controller.

### Enter and exit ordering

~~~mermaid
flowchart TD
    Select["Manager selects nearby Gyrocopter registry entry"] --> Locks["Manager acquires animation and blob-shadow locks"]
    Locks --> Enter["CarEntry.RequestEnter"]
    Enter --> Ignore["Ignore exact Character and aircraft collider pairs"]
    Ignore --> Approach["GC2 MoveToLocation at priority 10"]
    Approach --> ReleaseMotion["Release approach motion and reset priority"]
    ReleaseMotion --> EntrySafety["Fuselage trigger + body kinematic transition window"]
    EntrySafety --> EnterPath["Door + Enter gesture + Standing to Step to Seat"]
    EnterPath --> Seat["Snapshot and lock Character physics; parent to Driver Seat"]
    Seat --> ShotHandoff["Manager may acquire GC2 shot from seated handoff"]
    ShotHandoff --> Settle["Evaluate seated pose for two frames"]
    Settle --> Flight["SetVehicleEnabled(true)"]
    Flight --> ExitRequest["Exit request or grounded Air HUD Down hold for 3 s"]
    ExitRequest --> Land["Auto-land using raycast and stability gate"]
    Land --> FlightOff["Disable flight presentation and park steady controller state"]
    FlightOff --> ExitPath["Door + Exit gesture + Seat to Step to Standing"]
    ExitPath --> Restore["Restore Character snapshot at exterior while pair-ignore remains held"]
    Restore --> ControlBack["Restore hull/body transition state and Player controllability"]
    ControlBack --> CollisionBack["finally ends exact collider-pair ignores"]
    CollisionBack --> CameraBack["Manager releases animation/blob lock and restores previous GC2 shot"]
~~~

Enter details:

1. The vehicle manager selects the closest available Gyrocopter from
   <code>HelicopterFlightController.Instances</code>; it does not run a
   scene-wide helicopter search.
2. Before calling <code>CarEntry.RequestEnter</code>, the manager acquires the
   Franklin animation and blob-shadow locks and caches its owner references. A
   rejected request runs the matching cancellation path.
3. <code>CarEntry</code> begins pair-specific Player/aircraft collision ignores,
   locks Player control, and issues GC2 <code>MoveToLocation</code> at priority
   10 with a versioned callback and timeout.
4. A fallback accepts the approach only when the Character is already inside the
   authored exterior door tolerance.
5. <code>ReleaseEntryApproachMotion</code> replaces/stops that motion at the same
   priority. This must happen before the seat snapshot because normal Player
   joystick motion uses priority 0.
6. <code>CarEntry</code> temporarily makes the fuselage collider a trigger and
   the body kinematic for the cabin-crossing window. This is separate from the
   exact collider-pair ignore lease.
7. The door rotates from its cached local closed pose while the Front Left Enter
   gesture drives the Character along the authored
   Standing → Step → Seat path.
8. After the gesture/path completes and Driving state is reasserted, Character
   physics is snapshotted before colliders are disabled. The snapshot includes
   Driver collision/kinematics, movement type, every child Collider/Rigidbody,
   and NPC Animator-culling state; the Character is then parented to
   <code>Driver Seat</code> and marked uncontrollable.
9. The manager may acquire the dedicated GC2 shot as soon as it observes that
   seated handoff. <code>CarEntry</code> evaluates the seated pose for two frames
   before enabling <code>HelicopterFlightController</code> flight.

Exit details:

1. Exit first clears input and starts auto-land. Pressing Exit again cancels the
   pending request. As an alternative, holding the Air HUD Down image button
   while at least one active landing WheelCollider stays grounded for 3
   continuous seconds sends the same normal exit request. Releasing Down or
   losing ground contact resets the timer.
2. A downward raycast supplies ground distance. The handoff gate also requires
   low planar speed, low vertical speed, and a sufficiently upright aircraft.
   It is a raycast/stability gate, not a direct
   <code>WheelCollider.isGrounded</code> test.
3. Once stable, the body is stopped and made kinematic; only then does
   <code>CarEntry</code> begin the GC2 exit sequence. At the start of that stopped
   exit the vehicle controller is disabled, releases its Air HUD presentation,
   and parks its steady state while <code>CarEntry</code> temporarily owns the
   transition-safety Rigidbody/collider mutations.
4. With root motion disabled, exit evaluates the exact authored curve backwards:
   Seat → Step → Standing.
5. Character collision, gravity, and kinematics remain locked while the root is
   inside the cabin. At the exterior point, <code>CarEntry</code> restores the
   snapshot while the pair-ignore lease is still active.
6. The runtime Character collider and GC2 Driver collision are reasserted,
   transforms are synchronized, and approach priority 10 is stopped again so
   Player priority-0 joystick input can resume. The hull trigger and generic
   Rigidbody transition state are then restored and Player controllability is
   returned, all while the exact pair-ignore lease is still held.
7. The <code>finally</code> path ends the ignored collider pairs. On the next
   manager update, the animation/blob locks and previous GC2 shot are restored.

### Flight, rotor, camera, and LOD

- Active flight uses gravity and <code>AddForceAtPosition</code> lift. Ground
  effect, translational lift, planar/vertical drag, speed envelopes, and a
  torque-based attitude controller are solved in <code>FixedUpdate</code>.
- Rotor spool and audio load are updated in <code>Update</code>. Main/tail rotor
  transforms are rotated on local negative Z in <code>LateUpdate</code>.
- The looping rotor voice uses the CC0
  <code>Helicopter Rotor Loop HQ.mp3</code> source. Unity imports it as mono
  24 kHz Vorbis, and the controller stops the AudioSource after spool-down.
- Tail visual RPM is 850, intentionally away from common 30/60 FPS harmonics to
  reduce strobing. It is a visual sampling value, not a claim of real aircraft
  RPM.
- The embedded GC2 Third Person ShotCamera is disabled at rest. It uses
  <code>Camera Pivot</code>, <code>DroneCameraOrbitInput</code>, and GC2
  automatic alignment. The vehicle manager, not the flight controller, enables,
  acquires, restores, and disables this shot.
- Parked state is kinematic, gravity off, Discrete, interpolation off, and
  sleeping. Active or preserve-momentum state uses gravity, Interpolate, and
  Continuous Speculative collision.
- LOD checks squared camera distance every 0.25 seconds: near renderers below
  32 m, one low-resolution body plus rotors from 32–140 m, and full cull at
  140 m or farther.

### Failure cleanup

- Async entry/exit work checks lifecycle and driver-transition versions before
  writing Character state.
- Death, ragdoll, destruction, component disable, and unavailable-controller
  paths invalidate the transition before cleanup.
- Active-ragdoll restoration does not overwrite GC2 ragdoll bodies/colliders
  with the old seated snapshot.
- <code>try/finally</code> always releases pair-specific ignored collisions.
- Emergency release stops gesture/state/IK, detaches the Character, restores the
  proper snapshot, releases approach motion, resets trigger/body/control flags,
  and does not run normal <code>onExit</code> instructions.
- Manager disable/death paths release the pilot before returning animation,
  blob-shadow, and camera leases.

## Layers and collision policy

| Object/path | Layer or mask rule | Reason |
| --- | --- | --- |
| Franklin Drone root | Layer 2, <code>Ignore Raycast</code> | Prevents its own body from becoming a world pointer/camera query target. |
| Drone Rigidbody and BoxCollider | Exclude Player (3) and Npc (11) | Drone can pass through Player/NPC without contact response. |
| Drone return obstacle mask | Excludes Ignore Raycast (2), Player (3), and Npc (11) | Characters and the drone body do not redirect autonomous return. |
| Franklin Gyrocopter root and physical wheel colliders | Layer 14, <code>Air</code> | Keeps aircraft semantics explicit; do not change the root back to Ignore Raycast. |
| Gyrocopter ground mask | Excludes Ignore Raycast, Player, Npc, and Air | Ground ray cannot hit the aircraft or characters. |
| Gyrocopter enter/exit | Temporary exact collider-pair ignores | Character can cross the cabin without changing the global physics matrix. |

## Mobile and long-session budget

- Drone nearest refresh: 0.15 s, registry-based, squared distance.
- Drone rotor visuals: maximum 30 visual updates per second and component
  disabled while parked.
- Return markers: maximum 48, duplicate filtering, in-place compaction, pooled
  marker objects.
- Return avoidance: fixed eight-hit <code>SphereCastNonAlloc</code> buffer and
  no per-step collection allocation.
- Gyrocopter collision caches: reusable lists with initial capacities for eight
  vehicle and sixteen occupant colliders; they can grow if a prefab has more.
- Ground-hold auto-exit reuses the cached WheelCollider array and the existing
  landing ray result in <code>FixedUpdate</code>; it adds no recurring search,
  allocation, or physics query.
- Gyrocopter LOD: 0.25 s polling; renderer state changes only when the LOD level
  changes.
- Drone impact FX: bounded lazy pools, capped particle counts, short sleepers,
  no runtime mesh creation.
- Drone explosion: one prewarmed instance and capped particle systems.
- Air HUD: 1920×1080 width-matched scaler, safe-area anchors, no UGUI
  <code>Mask</code>, no stencil masking, default UI material, and native-alpha
  sprites. Only interactive controls have <code>raycastTarget</code> enabled.
- A hidden Air HUD disables its Canvas, GraphicRaycaster, and its own
  <code>Update</code>. The drone HUD is created once on touch platforms; each
  Gyrocopter creates its HUD lazily on first use and then reuses it.
- UI sprites are non-readable, mipmaps off, maximum 512, compressed, and retain
  alpha. Gyrocopter texture is non-readable, maximum 1024 with mipmaps.
- Gyrocopter meshes are non-readable with Low mesh compression and GPU
  optimization; rotor shadows and motion vectors are disabled.
- Drone and helicopter use one spatial looping voice each. The helicopter clip
  is imported mono at 24 kHz and stops completely after spool-down.
- The existing Main Camera <code>PhysicsRaycaster</code> uses a bounded
  intersection buffer. Drone control temporarily disables and then restores it.

## Integration checklist

### Player

- <code>Player.prefab</code> contains one <code>ManagerAirSystem</code> object.
- <code>DronePlayerController</code> references the Player Character,
  <code>FranklinAnimationBridge</code>, <code>Canvas Air Control</code>, and
  <code>DroneControllerHandPresentation</code>.
- <code>DroneControllerHandPresentation</code> references the handheld prefab
  and a humanoid Player Animator.

### Camera and EventSystem

- Keep the existing GC2 Main Camera, AudioListener, active Main Shot, and scene
  EventSystem.
- Do not add a physical Camera, AudioListener, EventSystem, or input module to an
  Air prefab.
- Both aircraft ShotCamera components remain disabled while parked.
- Shot Third Person pivot and automatic alignment remain assigned to the
  dedicated aircraft pivots.

### Franklin Drone

- Rigidbody, BoxCollider, ShotCamera, camera pivot, four rotor transforms,
  motor AudioSource, Traits, Health, impact, and explosion references are
  assigned.
- The root collision exclusions remain Player/Npc.
- The model scale and BoxCollider are edited together.
- Damage/explosion shared assets remain valid project references.

### Franklin Gyrocopter

- Root and physical wheel colliders remain on <code>Air</code>.
- <code>EnterL</code>, <code>Driver Entry Step</code>,
  <code>Driver Seat</code>, outer <code>DoorL</code>, Lift Point, rotors, camera
  pivot, and ShotCamera remain assigned.
- Root/door Animators remain disabled.
- <code>useRootMotion</code> remains false and
  <code>useAuthoredEntryPath</code> remains true.
- Front Left Enter/Exit/Ride clips and the shared humanoid avatar dependency
  remain valid.
- <code>m_DescendGroundExitSeconds</code> remains 3 seconds and the three active
  landing WheelColliders remain enabled for physical ground-contact detection.
- <code>HelicopterFlightController</code> remains discoverable through its
  static registry; do not add a per-frame scene search.

### Canvas Air Control

- All six sprite-bearing Images reference the PNG sprites directly with
  <code>m_Material = None</code>.
- Do not add UGUI Mask components or reassign the legacy custom UI materials.
- Base/knob/decorative Images remain non-raycastable; only the connect, exit,
  move hit area, ascend, descend, and orbit hit area receive pointer events.
- There is no second joystick: the right side is an orbit area plus Up/Down and
  Exit buttons.

## README maintenance rule

Update this README in the same change whenever code or prefab work changes any
of the following:

- A runtime component, public API, interface, or owner.
- A state transition, acquire/release path, or async ordering.
- A required serialized reference, anchor, layer, or collision mask.
- A control binding or HUD interaction.
- An asset location, shared external dependency, or license source.
- A pooling limit, polling interval, NonAlloc buffer, LOD threshold, texture or
  audio import budget.
- Camera ownership, Character snapshot contents, or failure cleanup.

For each affected change:

1. Update the component/asset map.
2. Update the relevant lifecycle section.
3. Update the ownership invariant or checklist.
4. Update the corresponding Mermaid graph.
5. Record only verification that was actually performed.

## Provenance and validation boundary

- Audio provenance and CC0 license details are recorded in
  <code>Art/Audio/THIRD_PARTY_NOTICES.md</code>.
- <code>Documentation/Easy Flying System ReadMe.pdf</code> is retained as source
  package documentation; it is not the authority for Franklin runtime behavior.
- Required RageRun model/material/provenance assets were relocated before unused
  RageRun demo content was removed.
- Runtime QA, Play Mode, builds, compilation tests, and automated tests were not
  run, per the project owner's instruction. This README update was based on
  static code, prefab, scene-reference, importer, layer, and dependency
  inspection only.
