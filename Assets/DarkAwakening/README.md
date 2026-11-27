# The Dark Awakening — VR demo (3–5 min)

Seated VR horror/survival demo based on the design document (Krasue, 1930s rural Thailand). All in-game text and audio cues are in English.

## Run it
1. Open the project in Unity 6 (URP). Wait for the import to finish.
2. Menu **Dark Awakening ▸ Build Demo Scene** (it also builds automatically once after the first import if nothing is unsaved).
   This creates `Assets/Scenes/TheDarkAwakening.unity`, the materials and the Krasue animator, and adds the scene to Build Settings.
3. **Play** with a headset (Quest Link / PCVR, OpenXR) or in the Editor with mouse:
   - hold **right mouse button** = look, **left click** = trigger, **R** = recenter.
4. Quest build: switch to Android, OpenXR + Meta Quest feature group, add the scene (already in Build Settings), Build & Run.

## Controls (VR)
- Point with either controller and press the **trigger** (or look at a glowing marker for 2 s — gaze fallback).
- Hold the trigger to use the lighter / raise the thorn branch.
- Movement is "blink" between fixed seated spots (no locomotion → comfortable).

## Flow
Intro (old photo) → wake up → window → shutters → find lighter + thorn branch → repel the Krasue at the door vent (fire) and wall vent (thorns) → hide → sunrise ending with a choice.

## Notes / tweaks
- Names "Chai" (you) and "Mali" (friend) are placeholders: see `DAUI.FriendName` and `DAGame.Friend`.
- If the Krasue faces the wrong way, change **Model Yaw Offset** on the `Krasue` object (`DAKrasue`).
- The TV and the rotary phone are slightly anachronistic for a 1930s village; the "jujube thorn branch" uses the dried bay-leaf branch asset as a stand-in.
- Everything generated lives in `Assets/DarkAwakening/` (Models, Textures, Audio, Scripts, Shaders, Editor, Materials). Re-running the builder rebuilds the scene.
- Audio and procedural textures are generated placeholders; replace the clips in `Audio/` and re-run the builder.
