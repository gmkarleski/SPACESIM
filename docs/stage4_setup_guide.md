# Stage 4 Setup Guide — Wiring `WarpUIController` into `TestVessels.unity`

**Purpose.** Phase A of Stage 4 (Cowork session) wrote `WarpUIController.cs` and added the asmdef reference. Phase B (this guide, for you in Unity) wires the controller into `TestVessels.unity` by creating the `WarpController` GameObject, building the UI hierarchy, and dragging the Inspector references into place.

**Time estimate.** 20-30 minutes if working straight through. The TMP Essentials import (first time only) takes ~30 seconds and is harmless.

**Prerequisites.**

- [ ] Pull is up to date through Stage 4 Phase A's commit on `main`.
- [ ] Unity Editor is open with the SPACESIM project loaded.
- [ ] All existing tests are passing in your local Test Runner (338 EditMode + 6 PlayMode).
- [ ] You have a clean working tree (or are willing to commit/stash before scene edits).

---

## 1. Open `TestVessels.unity`

- [ ] *File → Open Scene* → navigate to `Assets/Scenes/TestVessels.unity`.
- [ ] Save the current scene state first (`Ctrl+S`) if you've made unrelated edits.

---

## 2. Add the `WarpController` GameObject

This is the singleton MonoBehaviour from Stage 2. Until Stage 4, no scene contained one — `WarpController.Instance` has been null in TestVessels and the SimTickController has been falling back to single-tick advancement. Adding the GameObject is what activates time-warp behavior.

- [ ] In the *Hierarchy* panel, right-click in empty space → *Create Empty*.
- [ ] Rename the new GameObject to **`WarpController`** (exact spelling, no spaces).
- [ ] With `WarpController` selected, in the *Inspector* click *Add Component*.
- [ ] In the search field type `WarpController` → select **WarpController** (the one in `SpaceSim.Foundation.SimTick`).
- [ ] Verify the component appears in the Inspector with no errors (no red text, no "Missing Script").

---

## 3. Build the UI hierarchy

### 3.1. Create the Canvas

- [ ] In the *Hierarchy*, right-click → *UI → Canvas*.
- [ ] Select the new `Canvas`. In its Inspector, find the `Canvas` component → *Render Mode* dropdown → set to **Screen Space - Overlay**.

A `Canvas` and `EventSystem` GameObject should both appear in the Hierarchy. The `EventSystem` is created automatically by Unity the first time you add UI to a scene; leave it alone.

> **First-time-only prompt.** When you create your first TextMeshPro element (next step), Unity will pop up a dialog titled "TMP Importer — Import TMP Essentials". **Click `Import TMP Essentials`.** This is expected and normal. The import adds shaders, fonts, and standard TMP resources to the project as a one-time step. Without it, TMP text won't render. If you also see an `Examples & Extras` import button, you can ignore it (we don't use those).

### 3.2. Add the rate display

- [ ] Right-click `Canvas` → *UI → Text - TextMeshPro*.
- [ ] (If TMP Importer dialog appears: click **Import TMP Essentials**, wait ~30 seconds for the import to complete.)
- [ ] Rename the new GameObject to **`RateDisplay`**.
- [ ] In the Inspector, find the `Rect Transform`:
  - Anchor preset: top-left (click the anchor box, hold `Shift` to also set pivot, click top-left).
  - Position: `Pos X = 20`, `Pos Y = -20`.
- [ ] In the `TextMeshPro - Text (UI)` component:
  - *Text* field: type `1x` as the placeholder.
  - *Font Size*: `24`.

### 3.3. Add the halt display

- [ ] Right-click `Canvas` → *UI → Text - TextMeshPro*.
- [ ] Rename to **`HaltDisplay`**.
- [ ] In the `Rect Transform`:
  - Anchor preset: top-left.
  - Position: `Pos X = 20`, `Pos Y = -60`.
- [ ] In the `TextMeshPro - Text (UI)` component:
  - *Text* field: leave empty.
  - *Font Size*: `16`.
  - *Vertex Color*: pick a red or orange (click the color swatch — e.g., RGB `(255, 100, 50)`). This is the visual cue that the halt area is an alert zone.

### 3.4. Add the pause / resume buttons

- [ ] Right-click `Canvas` → *UI → Button - TextMeshPro*.
- [ ] Rename to **`PauseButton`**.
- [ ] Position somewhere visible — e.g., right side of the screen. Suggested:
  - Anchor: top-right.
  - Pos X = `-100`, Pos Y = `-20`, Width = `120`, Height = `40`.
- [ ] Expand the button in the Hierarchy → select its child `Text (TMP)` → change the *Text* field to `Pause`.

- [ ] Right-click `Canvas` → *UI → Button - TextMeshPro*.
- [ ] Rename to **`ResumeButton`**.
- [ ] Position: same column as `PauseButton`, below it (e.g., `Pos Y = -65`).
- [ ] Child *Text*: `Resume`.

### 3.5. Add the discrete rate buttons

Create one button per discrete level: `1`, `5`, `10`, `100`, `1000`, `10000`, `100000`. Use the naming convention `Discrete{N}xButton`. Position them in a row or column under the pause/resume area — they don't have to be pretty, just clickable.

For each level `N`:

- [ ] Right-click `Canvas` → *UI → Button - TextMeshPro*.
- [ ] Rename to **`Discrete{N}xButton`** — for example `Discrete1xButton`, `Discrete5xButton`, … `Discrete100000xButton`.
- [ ] Set the Rect Transform position so the button sits visibly (e.g., stack them downward at `Pos X = -100` increasing `Pos Y` by `-45` per button).
- [ ] Expand the button → child `Text (TMP)` → set *Text* to `{N}x` — for example `1x`, `5x`, … `100000x`.

You should end up with 7 discrete-rate buttons named consistently.

### 3.6. Add the continuous slider

- [ ] Right-click `Canvas` → *UI → Slider*.
- [ ] Rename to **`ContinuousSlider`**.
- [ ] Position below the discrete-rate buttons. Suggested: center-screen or below the discrete column.
- [ ] In the `Slider` component:
  - *Min Value*: `1`
  - *Max Value*: `1000`
  - *Whole Numbers*: ✅ checked (critical — the WarpUIController defensively clamps but `Whole Numbers` makes the slider snap visually)
  - *Value*: leave default

### 3.7. Add the clear-halt button

- [ ] Right-click `Canvas` → *UI → Button - TextMeshPro*.
- [ ] Rename to **`ClearHaltButton`**.
- [ ] Position near the `HaltDisplay` so the visual pairing is obvious — e.g., directly below it.
- [ ] Child *Text*: `Clear Halt`.

---

## 4. Add the `WarpUIController` component to the Canvas

- [ ] In the Hierarchy, select the `Canvas` GameObject.
- [ ] In the Inspector, click *Add Component* → search `WarpUIController` → select it.

The component appears with 13 Inspector fields, all currently set to `None`. They go in the next step.

---

## 5. Wire the Inspector references

For each field on `WarpUIController`, drag the matching Hierarchy GameObject onto the field in the Inspector. The table below maps field names to GameObjects.

| Inspector field             | Drag from Hierarchy             |
|-----------------------------|---------------------------------|
| **Rate Display Text**       | `RateDisplay`                   |
| **Halt Display Text**       | `HaltDisplay`                   |
| **Pause Button**            | `PauseButton`                   |
| **Resume Button**           | `ResumeButton`                  |
| **Discrete 1x Button**      | `Discrete1xButton`              |
| **Discrete 5x Button**      | `Discrete5xButton`              |
| **Discrete 10x Button**     | `Discrete10xButton`             |
| **Discrete 100x Button**    | `Discrete100xButton`            |
| **Discrete 1000x Button**   | `Discrete1000xButton`           |
| **Discrete 10000x Button**  | `Discrete10000xButton`          |
| **Discrete 100000x Button** | `Discrete100000xButton`         |
| **Continuous Slider**       | `ContinuousSlider`              |
| **Clear Halt Button**       | `ClearHaltButton`               |

- [ ] After wiring, verify every field shows the corresponding GameObject name (no `None` entries remaining).

> **Why the warning matters.** When you enter Play mode, `WarpUIController.Awake` validates every reference. If any is missing, the Console will log an error naming the unbound field — but the rest of the controller is also disabled (buttons become un-clickable, the UI is inert). If buttons don't respond in Play mode, this is the first place to check.

---

## 6. Save the scene

- [ ] `Ctrl+S` to save `TestVessels.unity`.

---

## 7. Verification — enter Play mode

- [ ] Press the *Play* button at the top of the Editor.
- [ ] Check the Console for any errors (red text). Expected state: no errors.

Then exercise the UI:

- [ ] **Initial rate.** The `RateDisplay` text should show `1x`.
- [ ] **Discrete 5x.** Click the `5x` button. The `RateDisplay` should change to `5x`. Looking at the SimTickController in the Hierarchy, its `Tick Number` should now climb 5× faster per FixedUpdate than before. (If your scene doesn't have a visible sim-tick counter, you can check the Inspector's `Tick Number` field on the SimTickController GameObject — it should be incrementing rapidly.)
- [ ] **Pause.** Click `Pause`. `RateDisplay` shows `Paused`. SimTickController's `Tick Number` should stop incrementing (well — it will still advance by 1 per FixedUpdate per the always-at-least-1 floor, which is intentional; the rate display showing "Paused" is the user-facing signal).
- [ ] **Resume.** Click `Resume`. `RateDisplay` returns to whatever rate was active before Pause (`5x` if you came from §7's step above). SimTickController resumes climbing at that rate.
- [ ] **Continuous slider.** Drag the `ContinuousSlider`. The `RateDisplay` should update live as you drag — e.g., `247x`, `891x`. Try the extremes (`1x` and `1000x`).
- [ ] **Cycle the discrete levels.** Click `10x`, `100x`, `1000x`, `10000x`, `100000x` in sequence. The display should update to match each level. (At `100000x` you may notice the FixedUpdate cycle struggling if your scene has many vessels; for TestVessels.unity with the standard prototype setup, even `100000x` should remain smooth.)

Exit Play mode (press Play again).

---

## 8. Common pitfalls (and how to fix them)

**Buttons don't respond when clicked.**
- Check the Console — `WarpUIController.Awake` logs an error for each missing Inspector reference.
- Confirm the `Canvas` has a `Graphic Raycaster` component (added automatically when you create a Canvas; if it's missing, *Add Component → Graphic Raycaster*).
- Confirm the `EventSystem` GameObject is in the Hierarchy.

**Text doesn't appear (buttons are blank squares, RateDisplay is invisible).**
- TMP Essentials didn't import. Re-trigger: *Window → TextMeshPro → Import TMP Essential Resources*.
- After import, you may need to re-enter Play mode to see the text render.

**RateDisplay shows `1x` but clicking buttons does nothing.**
- `WarpController` GameObject is missing from the scene (Step 2). The `WarpUIController.Awake` logs a warning when `WarpController.Instance` is null and disables the buttons. Add the GameObject per Step 2 and re-enter Play mode.

**Inspector shows `Missing Script` on the Canvas after add-component.**
- The `WarpUIController` class isn't recognized yet. Save the scene, exit Unity, re-open the project — Unity needs to compile the new asmdef-referenced TMPro classes once.
- If that doesn't fix it, check the Console for compile errors. The SimTick.asmdef should list `Unity.TextMeshPro` in its references — if not, the script can't compile against TMPro.

**Halt display never shows anything.**
- Halts fire when a predictor reports an imminent event (within 1 sim-tick of current). On a quiet vessel, no halt fires by design. To test the halt path, you'd need a vessel configuration where atmospheric entry or surface impact is predicted within the current tick + 1 window. Stage 3's tests exercise this programmatically; the UI displays the result if a halt does fire during play.

---

## 9. After verification — commit

- [ ] Stage the scene change: `git add SPACESIM/Assets/Scenes/TestVessels.unity` (and any associated `.meta` updates).
- [ ] Commit with a Stage 4 Phase B message — e.g., `048-stage4b: wire WarpController GameObject + WarpUIController Canvas into TestVessels.unity per Phase A guide; manual play-mode verification confirms rate display + pause/resume + discrete levels + continuous slider all functional`.
- [ ] Push.

Stage 4 is complete after this commit. Stage 5 wraps up with a Stage-4-end-state artifact and any cleanup; it's a separate prompt.
