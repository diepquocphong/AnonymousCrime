# Franklin Player HUD — ImageGen prompts

Mode: built-in ImageGen (`image_gen`), generation.

## HUD mockup

```text
Use case: ui-mockup
Asset type: 16:9 Unity mobile game HUD design reference
Primary request: polished in-game HUD mockup showing a compact player-status cluster in the upper-right corner with simulated money, health, current weapon, and a simulated mini-map.
Scene/backdrop: blurred third-person open-world urban gameplay screenshot used only as subdued context.
Subject: upper-right player HUD is the clear focal point. Place a circular mini-map at the far upper-right with a graphite black translucent glass background, subtle street grid, small white triangular player marker, one cyan waypoint dot, and a thin silver/chrome ring. Immediately to its left, stack three compact horizontal modules: money at top showing exactly "$ 12,480" in white with a restrained mint accent; health in the middle with a white heart icon and a red fill bar around 78%; current weapon at bottom with a clean white compact pistol silhouette and exactly "12 / 48".
Style/medium: realistic shippable game UI mockup, dark graphite panels, soft translucent black glass, thin silver/chrome outlines, subtle inner highlights, matching premium black-and-chrome mobile control buttons.
Composition/framing: 1920x1080 landscape. Keep the HUD fully inside a 48 px safe margin. Reserve the top-right 600x300 region for the HUD. Show a separate rounded-square exit/interaction control displaced below the HUD to demonstrate collision avoidance. No other UI near the HUD.
Color palette: charcoal black, gunmetal, white, health red, small mint/cyan status accents.
Typography: bold condensed sans-serif, high contrast, readable at mobile scale.
Text (verbatim): "$ 12,480", "12 / 48"
Constraints: practical hierarchy, crisp edges, legible text, no brand names, no logos, no trademarks, no watermark.
Avoid: cyberpunk neon overload, fantasy ornament, excessive red, clutter, tiny unreadable labels, HUD touching screen edges.
```

## Mini-map texture

```text
Use case: stylized-concept
Asset type: square Unity game UI mini-map texture
Primary request: a clean simulated top-down urban mini-map background for a mobile open-world game HUD.
Scene/backdrop: dense but readable street grid with a few broad avenues, smaller side streets, two city blocks, one subtle park area, no buildings in perspective.
Subject: map texture only, dark graphite road map.
Style/medium: crisp flat 2D game UI texture, high-end minimal map graphic.
Composition/framing: square 1:1, centered street network, important streets remain readable when cropped into a circle. Fill the canvas edge-to-edge.
Color palette: near-black and charcoal base, medium gray streets, thin soft silver road edges, one subtle desaturated teal park/route accent.
Constraints: no player marker, no waypoint dots, no icons, no border or outer frame, no text, no labels, no logos, no trademarks, no watermark.
Avoid: satellite imagery, 3D perspective, bright colors, gradients, bevels, glow, clutter, tiny street detail.
```

## Weapon cluster mockup v2

Mode: built-in ImageGen (`image_gen`), precise-object edit.

Input image: gameplay screenshot supplied by the user; used as the edit target.

```text
Use case: precise-object-edit
Asset type: Unity mobile open-world crime game HUD mockup
Input images: Image 1: edit target gameplay screenshot; preserve its city background, sky, camera crop, and the partial circular mini-map on the right.
Primary request: replace only the current disconnected weapon card and the two separate grenade/molotov boxes with one cohesive, professional weapon-and-throwables HUD cluster that feels strongly inspired by premium modern urban open-world crime games while remaining an original design.
Subject: create a single unified smoked-black translucent cluster. The main weapon tier is a wide low-profile panel with subtle asymmetric clipped corners, a large clean white pistol silhouette on the left, small uppercase text "PISTOL" beside it, and bold ammo "12 / 48" aligned on the right. Directly attached to the lower edge is a compact quick-slot rail containing two equal cells: grenade icon with count "3", molotov icon with count "2". The grenade cell is active, indicated by a thin restrained mint/teal underline and slightly brighter icon; the molotov cell is inactive gray-white.
Style/medium: shippable game UI, minimalist GTA-like urban crime HUD language, flat vector-like icons, smoked charcoal glass, subtle soft black shadow, thin low-contrast gray separators, no chunky chrome bezel, no glossy mobile-app buttons.
Composition/framing: keep the cluster in the same upper-right gameplay region, left of the mini-map. Main weapon panel about 300x62 pixels relative to the screenshot; attached quick-slot rail about 150x54 pixels, right-aligned under the weapon panel. Maintain generous breathing room and precise alignment with the mini-map.
Color palette: near-black translucent charcoal, white, cool gray, one small mint/teal selection accent only.
Typography: condensed bold sans-serif, crisp high contrast.
Text (verbatim): "PISTOL", "12 / 48", "3", "2"
Constraints: change only the weapon/throwable HUD; keep the gameplay background and partial mini-map unchanged; make all text exact and readable; the three functions must read as one unified component; no logos, no brand names, no trademarks, no watermark.
Avoid: separate floating cards, thick white borders, rounded glossy chrome frames, oversized padding, cyan outlines around every element, cyberpunk neon, fantasy ornament, UI touching the mini-map.
```
