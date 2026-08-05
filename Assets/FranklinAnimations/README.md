# Franklin GTA-style locomotion

This integration uses selected in-place Humanoid idle clips from RG Poly Cartoon City Characters.
The Left Shift run uses the project-provided `IP-sprint-loop-smooth.anim` clip.

- RG Poly: https://rg-poly.itch.io/cartoon-city-massive-pack-characters
- License: https://creativecommons.org/publicdomain/zero/1.0/

## GC2 integration

- The Character's **Start State remains empty**, so normal movement and animation use the
  original GC2 locomotion and the Player's configured speed (currently 4).
- Holding **Left Shift** enters `Franklin_GTA_Sprint.asset` through GC2's State API at layer 0;
  releasing the key blends back to GC2's normal animation and restores the pre-sprint speed.
  The sprint State sets movement speed to 6.
- Pressing **Space** makes one jump attempt through `Character.Jump.Do()`. GC2 retains control of
  grounded checks, air-jump allowance, jump force, cooldown and all jump/landing animations.
- Sprint automatically exits before takeoff and stays disabled while airborne, ensuring GC2's
  original airborne controller is the only animation source during a jump.
- GC2 continues to own movement, grounded state, jump physics, rotation and the PlayableGraph.
- `FranklinAnimationBridge` only calls GC2's public Gestures API for occasional idle variants,
  after checking that GC2 is grounded, stationary and not already playing a gesture. It never
  cancels a gesture started by GC2 gameplay, combat or interaction systems.
- Root motion is disabled for every added animation so Character Controller movement stays
  deterministic.
- The source FBX files are used only by the editor installer and removed after the four
  selected `.anim` clips are extracted.

## Selected clips

- `Franklin_Idle_LookAround`: occasional natural look-around variation
- `Franklin_Idle_Natural_A`: occasional full-body idle variation
- `Franklin_Idle_Natural_B`: a second occasional full-body idle variation
- `IP-sprint-loop-smooth`: forward in-place run, active only while Left Shift is held

The three idle variations are one-shot gestures selected randomly after 6–11 stationary seconds.
The same variation is not selected twice in a row. GC2's stock idle remains the default between
these gestures.

Do not edit files under `Assets/Plugins/GameCreator`; this setup only references GC2's public
runtime API and stock locomotion controller.
