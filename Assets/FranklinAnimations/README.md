# Franklin GTA-style locomotion

This integration uses four selected in-place Humanoid clips from the user's licensed
MoCapCentral `MC_Idles` package. GC2's supplied Walk and Run states provide normal movement and
light jogging; `IP-sprint-loop-smooth.anim` is used only for maximum sprint. Movement Animset Pro
supplies run start/stop transitions and five context-sensitive jump combos.

## GC2 integration

- The Character's **Start State is GC2's supplied `Walk` state** (speed 2), so WASD always uses
  GC2's normal walk animation and speed by default.
- Holding **Left Shift** temporarily enters GC2's supplied `Run` state (speed 4) on layer 0:
  this is the light jog and works in all movement directions. Releasing Shift blends back to Walk.
- While moving forward relative to Camera Shot, press **Left Shift** quickly at least twice to
  enter `Franklin_GTA_Sprint.asset` (IP sprint) on that same layer. Keep tapping within
  `Sprint Tap Grace` (0.45 seconds by default) to keep sprinting; otherwise the Player falls
  back to Jog if Shift is held, or Walk if it is not. `FranklinAnimationBridge > Sprint > Run Speed`
  controls maximum sprint speed without changing GC2's Walk or Run speeds. When sprint begins,
  speed lerps from the jog speed to `Run Speed` over `Run Start Acceleration Time` (0.77 seconds by
  default), matching `MAP_Run_Start` instead of jumping to full speed immediately. The default
  `Run Start Acceleration Power` of 2.2 applies a quadratic-style ease-in, keeping the beginning
  deliberately slow and accelerating harder near the end.
  Sprint accepts only input pointing forwards relative to Camera Shot (`Run Forward Input Threshold`,
  default 0.5), locks its movement to the camera's horizontal forward direction, and waits until
  the Player faces that direction within `Run Camera Alignment Angle` (default 8°) before starting.
- A decrease to Traits Attribute `hp` enters **Damage locomotion** for `Damage Jog Duration`
  seconds (default 6). During this safety window, Walk is temporarily covered by GC2 Run/Jog
  for both keyboard and mobile joystick input; holding Left Shift starts maximum sprint directly,
  without the repeated-tap requirement. Each further HP loss restarts the timer. When it expires,
  the standard Walk → Shift Jog → repeated-Shift Sprint control scheme returns.
- Pressing **Space** makes one jump attempt through `Character.Jump.Do()`. GC2 retains control of
  grounded checks, air-jump allowance and cooldown. A root-locked MAP combo is chosen
  for stationary, moving or sprinting visuals and normalized to about 1.05 seconds.
- `FranklinAnimationBridge > Jump Combos > Jump Height` controls the approximate apex height in
  meters. The bridge converts it with GC2's current upward gravity and writes the result through
  GC2's public `Motion.JumpForce` property before the jump attempt.
- Sprint automatically exits before takeoff and stays disabled while airborne, ensuring GC2's
  movement physics remain authoritative during the MAP jump gesture.
- GC2 continues to own movement, grounded state, jump physics, rotation and the PlayableGraph.
- `FranklinAnimationBridge` only calls GC2's public Gestures API for occasional idle variants,
  after checking that GC2 is grounded, stationary and not already playing a gesture. It never
  cancels a gesture started by GC2 gameplay, combat or interaction systems.
- Idle variations use their RootT/RootQ motion for natural weight shifts and foot placement. Jump
  and the IP sprint loop remain root-locked; Run Start/Stop can opt into root motion through the
  mode described below while GC2 continues to drive the Character Controller.
- The source FBX files are used only by the editor installer and removed after the four
  selected `.anim` clips are extracted.

## Selected clips

- `Franklin_Idle_MC_LookAround` (5.50 s): natural look-around
- `Franklin_Idle_MC_RubNeck` (3.83 s): rubs the neck and loosens one shoulder
- `Franklin_Idle_MC_LookAtNails` (5.50 s): subtle hand/wrist inspection
- `Franklin_Idle_MC_BrushLeg` (4.17 s): looks down and brushes off both legs
- GC2 `Walk`: 8-direction default locomotion, active with WASD
- GC2 `Run`: 16-direction light jog, active while Left Shift is held
- `IP-sprint-loop-smooth`: forward in-place maximum sprint, active after quick repeated Left Shift taps
- `MAP_Run_Start`: plays once when maximum sprint begins
- `MAP_Run_Stop_Left` / `MAP_Run_Stop_Right`: alternate when sprint movement fully stops
  (detected from the movement-input edge, including when Left Shift remains held). Run Stop eases
  the speed cap back to normal over `Run Stop Ease Out Time` (1 second by default). During that
  interval the bridge temporarily caps GC2's effective deceleration so high values such as 10 do
  not collapse velocity in a few frames, then restores the exact original
  `Use Acceleration` and `Deceleration` settings.
- `Run Transition Motion Mode` selects how Run Start/Stop displacement is produced:
  `GC2 Motion` uses the acceleration/ease settings above, while `Animation Root Motion` lets
  GC2's gesture graph feed each clip's root translation into the Character Controller.
  The Player defaults to `Animation Root Motion`. During camera-locked sprint, root rotation is
  temporarily disabled so GC2 Facing can keep the Player aimed at the camera; it is restored when
  sprint input stops. If a replacement clip has no root curves, the bridge logs one warning and
  safely falls back to `GC2 Motion`.
- `Camera Shot` includes `Franklin Sprint Camera Yaw`: it enables the existing Third Person
  `Max Yaw` constraint only after the Player is facing the Camera Shot direction and running,
  then restores the Camera Shot's original enabled/disabled state as soon as running stops. The
  Camera Shot's configured yaw value (120° by default) remains yours to adjust.
- `MAP_Jump_Place`: stationary jump combo
- `MAP_Jump_Walk_Left` / `MAP_Jump_Walk_Right`: alternating normal-movement jump combos
- `MAP_Jump_Run_Left` / `MAP_Jump_Run_Right`: alternating IP-sprint jump combos

The four idle variations are one-shot gestures selected randomly after 6–11 stationary seconds.
The same variation is not selected twice in a row. GC2's stock idle remains the default between
these gestures.

Do not edit files under `Assets/Plugins/GameCreator`; this setup only references GC2's public
runtime API and stock locomotion controller.

## Retarget Pro V5 integration

- Retarget Pro V5.1.3 is installed under `Assets/KINEMATION`.
- `GC2_Mannequin_To_Franklin.asset` maps the stock GC2 Mannequin skeleton to Franklin and uses
  Retarget Pro's Humanoid A/T reference pose.
- Open **Tools > Franklin Game > Retarget Pro > Open GC2 to Franklin baker** to preview a source
  clip before baking. Baked experiments are kept under
  `Assets/FranklinAnimations/RetargetPro/Baked`.
- Run **Validate safe integration** after changing the profile. Validation requires the Player to
  remain Humanoid, retain GC2's `CompleteLocomotion.controller`, and have no
  `DynamicRetargeter` component.
- Retarget Pro is editor-only for this Player. Its runtime `DynamicRetargeter` creates another
  animation graph, while GC2 already owns the Player's graph. Keeping it off the Player avoids
  graph conflicts and avoids a duplicate animated source character on mobile.
- V5.1.3's window baker outputs Generic transform curves. Always inspect a baked clip in the
  Retarget Pro preview before assigning it; do not replace the current Humanoid clips merely
  because a bake completed.
