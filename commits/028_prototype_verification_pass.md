# 028: Unity prototype verification pass — scaffolding closed

Record that the commit 027 file-level scaffolding for the Unity prototype passed end-to-end runtime and editor verification. Capture the 31 `.meta` files Unity auto-generated on first import so the project is reproducible from a fresh clone. Capture the populated `TestFoundation.unity` scene which now contains the smoke-test GameObject with `PrototypeStartupTest` attached. Append a `**Prototype scaffolding: verified.**` paragraph to `docs/CONSTRAINTS.md` §9 Phase 0 closing the scaffolding side of Phase 0 and naming the next code-level deliverables.

Both smoke tests passed cleanly on first run. The runtime smoke test (`Assets/Scripts/Foundation/PrototypeStartupTest`) logged `"Phase 0 prototype foundation ready"` plus Unity version, platform, and persistent data path diagnostics when the user pressed Play in the `TestFoundation.unity` scene. The editor menu test (`Prototype → Verify Setup`) logged Unity `6000.4.7f1`, the parsed dependency list from `Packages/manifest.json` including `com.unity.mathematics` 1.3.2 and `com.unity.burst` 1.8.24 at their commit-027 pinned versions, and the `"Editor scripting infrastructure verified"` confirmation. No compile errors. No package resolution conflicts.

The smoke tests pass closes the file-level scaffolding side of Phase 0. The remaining Phase 0 deliverables are code-level: double-precision world coordinates with floating origin at the 50 km shift threshold per `docs/NETCODE_CONTRACT.md` §1.1 and `docs/CONSTRAINTS.md` §2 commit 002; the 30 Hz fixed sim-tick loop per `docs/NETCODE_CONTRACT.md` §1.2 with its ten-step cycle (receive peer state → read PhysX → convert to authoritative coords → apply analytic updates → reconcile authoritative state → detect mode transitions → push authoritative back to PhysX → replicate to peers → fire events → advance sim-tick counter); the PhysX-active / Kepler-rails / interstellar-cruise mode separation per `docs/NETCODE_CONTRACT.md` §3; and the cube launches-to-moon-landing validation scenario per `docs/NETCODE_CONTRACT.md` §10 which is Phase 0's binary closure milestone. Code-level work begins in the next session.

## Scope

- 31 `.meta` files under `SPACESIM/Assets/` — created by Unity on first import; captured here so any clone of the repo recovers the same asset GUIDs, the same package references, and the same scene-component bindings. Without committing the `.meta` files, a fresh clone would force Unity to regenerate GUIDs and the scene would lose its `PrototypeStartupTest` component binding.
- `SPACESIM/Assets/Scenes/TestFoundation.unity` — re-saved by the user in Unity with a `GameObject` carrying the `PrototypeStartupTest` MonoBehaviour. Host-side `findstr PrototypeStartupTest TestFoundation.unity` confirms three matches in the scene file (the script class reference, the asset name binding, the editor class identifier). The `git add` for this file picks up the host bytes on replay.
- `docs/CONSTRAINTS.md` — single paragraph append to §9 `### Phase 0 — Decisions (weight 1)` recording the verification pass and naming the next code-level deliverables.

## Rationale

Commit 027 landed file-level scaffolding for the Unity prototype: folder structure, package manifest additions, two test scripts (runtime + editor), an empty `TestFoundation.unity` scene, the `SPACESIM/README.md` orientation doc. Cowork cannot drive Unity to verify that scaffolding works at runtime; the user runs Unity, follows the README's first-open instructions, and reports whether the smoke tests log the expected output. Commit 028 records that verification step and lands the artifacts Unity produced during it.

The `.meta` files are the most consequential captured artifact. Unity uses GUIDs (not filenames) to bind component references — when `TestFoundation.unity` references `PrototypeStartupTest`, the reference points at the GUID stored in `PrototypeStartupTest.cs.meta`, not at the source file path. If a clone of the repo doesn't include `.meta` files, Unity regenerates them on first import with fresh GUIDs; the scene's binding then resolves to nothing and the smoke test silently breaks. Committing the 31 `.meta` files makes the project reproducible: any clone gets identical GUIDs, identical bindings, identical behavior.

The scene-file capture matters for the same reason. With the scene committed in its post-save state, a clone reproduces the smoke test setup without requiring the user to re-author the scene contents. The scene file references `PrototypeStartupTest` by its GUID; the GUID is in the script's `.meta` file; both are committed together so the binding is stable.

The CONSTRAINTS §9 Phase 0 paragraph closes the scaffolding side of Phase 0 in the project's canonical record. Up through commit 027 the doc said "Prototype implementation: started"; commit 028 adds "Prototype scaffolding: verified" with the next-deliverables list. Reading §9 Phase 0's eight paragraphs in order tells the Phase 0 story end-to-end: lock LOCKED items (commit 001) → netcode contract written + prototype required (commit 002) → three-tier content structure (commit 010) → four intensive-craft bodies (commit 021) → contract landed (commit 026) → "prototype work is the next deliverable" (commit 026's closing line) → prototype scaffolding started (commit 027) → scaffolding verified, code-level work next (commit 028).

## Changes

Three operations:

1. **Capture 31 `.meta` files** Unity created at first import on the user's Windows host. Cowork cannot create `.meta` files with deterministic GUIDs that match what Unity will generate — only Unity can. The `.meta` files live on disk in the SPACESIM project tree; Cowork's role here is to inventory them and write them into the artifact's `git add` list. Files captured: `Editor.meta`, `Editor/PrototypeEditorTest.cs.meta`, `InputSystem_Actions.inputactions.meta`, `Materials.meta`, `Prefabs.meta`, `Readme.asset.meta`, `Scenes.meta`, `Scenes/SampleScene.unity.meta`, `Scenes/TestFoundation.unity.meta`, `Scripts.meta`, `Scripts/Foundation.meta`, `Scripts/Foundation/Coordinates.meta`, `Scripts/Foundation/Physics.meta`, `Scripts/Foundation/PrototypeStartupTest.cs.meta`, `Scripts/Foundation/SimTick.meta`, `Settings.meta`, `Settings/DefaultVolumeProfile.asset.meta`, `Settings/Mobile_RPAsset.asset.meta`, `Settings/Mobile_Renderer.asset.meta`, `Settings/PC_RPAsset.asset.meta`, `Settings/PC_Renderer.asset.meta`, `Settings/SampleSceneProfile.asset.meta`, `Settings/UniversalRenderPipelineGlobalSettings.asset.meta`, `TutorialInfo.meta`, `TutorialInfo/Icons.meta`, `TutorialInfo/Icons/URP.png.meta`, `TutorialInfo/Layout.wlt.meta`, `TutorialInfo/Scripts.meta`, `TutorialInfo/Scripts/Editor.meta`, `TutorialInfo/Scripts/Editor/ReadmeEditor.cs.meta`, `TutorialInfo/Scripts/Readme.cs.meta`.

2. **Capture the populated `TestFoundation.unity` scene** in its post-Unity-save state. Host-side `findstr PrototypeStartupTest TestFoundation.unity` returned three matches confirming the scene now contains the smoke-test setup with the `PrototypeStartupTest` component bound to a GameObject. The Cowork sandbox view of this file remained stale (showed the pre-save 3,507-byte truncated version even after the host save); the host bytes are what `git add` picks up on replay. See "Sandbox-mount staleness observation" below.

3. **Append to CONSTRAINTS.md §9 Phase 0** via the standard bash-via-Python boundary-anchor-validated subsection-body-append pattern. Nine boundary anchors captured pre-write covering every prior Phase 0 paragraph from commits 001, 002, 010, 021, 026, 027. All nine preserved exactly once post-write. Internal ordering verified end-to-end. CONSTRAINTS grew from 1958 lines / 229,089 bytes to 1960 lines / 230,361 bytes (+2 lines, +1,272 bytes).

## Verification

96 checks, all passing. Five groups:

### A. `.meta` file inventory and well-formedness (47 checks)

- 31 `.meta` files present under `SPACESIM/Assets/` (matches expected count)
- Every `.meta` file contains `fileFormatVersion:` and a valid 32-character hex GUID
- All 31 GUIDs are unique (no collisions)
- 13 specifically-required `.meta` files exist at expected paths (Scripts/Foundation/PrototypeStartupTest.cs.meta, Editor/PrototypeEditorTest.cs.meta, Scenes/TestFoundation.unity.meta, Scenes/SampleScene.unity.meta, and the 9 folder-level .meta files for the directories created in commit 027)

### B. CONSTRAINTS.md §9 Phase 0 update (16 checks)

- CONSTRAINTS.md line count = 1960
- `**Prototype scaffolding: verified.**` paragraph present exactly once
- New paragraph references the smoke-test marker line `Phase 0 prototype foundation ready`
- New paragraph references commit 028 explicitly
- New paragraph references package versions `com.unity.mathematics` 1.3.2 and `com.unity.burst` 1.8.24
- New paragraph names next-session code-level work
- New paragraph references netcode contract sections §1.1, §1.2, §3, §10
- Nine §9 Phase 0 boundary anchors preserved exactly once each (commits 001, 002 × 2, 010, 021, 026 × 2, 027 × 2)
- §9 Phase 0 internal ordering preserved: eight paragraphs in expected sequence (opener → netcode contract → three-tier → four-body → netcode-landed → "prototype work is the next deliverable" → prototype-started → prototype-verified)

### C. Section h3 counts unchanged from commit 027 (15 checks)

§1=15, §2=15, §3=18, §4=17, §5=6, §6=13, §7=7, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0.

### D. Prior-commit anchors preserved (15 checks)

Commit 014 damage-repair (no malformed line; restored inline reference once); commit 015 first verbatim-with-context anchor; commit 017 `## 3. Gameplay mechanics` and `## 4. World behavior and discovery` line-anchored once each; commit 019 three-category framing preserved verbatim including `Radiation dose, zero-G exposure, mission stress`; commit 021 four-bodies-intensive-hand-craft; commit 022 strip-mining anchor; commit 023 Mission Control heading; commit 024 Flight computers heading; commit 025 atmospheric flight subsection and §10 RESOLVED marker; commit 026 contract path reference; commit 027 SPACESIM path reference.

### E. Cross-section preserved-content battery (3 checks)

`Engineering as the verb` (§1); `Floating origin shift threshold: 50 km default` (§2); `**Crew are physically located on vessels.**`.

## Sandbox-mount staleness observation (Cowork limitation)

The Cowork sandbox mount of `C:\Users\gmkar\space_sim\` showed a stale view of `TestFoundation.unity` after the user saved the scene in Unity. Host-side `findstr PrototypeStartupTest` returned three matches confirming the populated scene; sandbox-side `grep PrototypeStartupTest` returned zero matches and the file appeared as the pre-save 3,507-byte truncated version with mtime stuck at the commit-027 timestamp. A mount-cache-refresh probe (writing and removing a probe file in the same directory) did not flush the staleness. The user's reported state (saved scene with three matches) is canonical; Cowork's sandbox view was wrong.

The recovery path used: Cowork landed everything it could verify cleanly (the 31 `.meta` files, the CONSTRAINTS.md paragraph append, this artifact) and deferred the scene-file commit to host-side `git add` during replay. Git operates on host bytes, not sandbox bytes, so the scene file that lands in commit 028's git history is the host's saved version with the smoke-test setup intact.

This is the first observed instance of sandbox-mount-host divergence in this project. Not formalized as a workflow rule yet; one data point isn't enough to lock procedure. Recorded here so future sessions encountering similar symptoms recognize the pattern: if the sandbox view of a recently-saved file appears stale, byte-level host verification is canonical and the commit should land via host-side `git add` rather than waiting for the sandbox to refresh.

## File-tool reliability observations from the commit 027-028 batch

Two distinct observations worth recording, neither formalized as workflow rules yet:

**Observation 1 (commit 027 hand-written Unity scene YAML is fragile).** Cowork wrote `TestFoundation.unity` as an empty Unity scene by extracting the four scene-level singleton documents from the template `SampleScene.unity`. The empty scene loaded cleanly in Unity. The user added the GameObject and attached `PrototypeStartupTest` via the Unity UI; Unity's save produced a correct populated scene. The split-of-labor — Cowork writes the empty scene structure, Unity writes the GameObject components — is the right division. Future Unity asset authoring should follow this pattern: Cowork writes the minimal asset that Unity will load; Unity populates the contents on import or via user interaction. Cowork should not hand-write GameObjects, Transforms, or MonoBehaviour references into scene files.

**Observation 2 (commit 028 sandbox view of Unity-written files can be stale).** As above. When sandbox view and host view of a file disagree, the host view is canonical. Byte-level host verification (`findstr`, `wc`, `stat`, `xxd`, `Get-FileHash`) is the discriminating tool. Cowork's `Read` tool may return cached or stale bytes for files Unity has recently written; cross-check with sandbox-side `Bash` byte-level reads when the contents look unexpected; cross-check with host-side commands when sandbox-side reads also look unexpected.

If similar symptoms recur in commits 029+, these observations get formalized as workflow rules in `commits/README.md` alongside the existing five rules (preserved-content anchors; Write-over-Edit for multi-subsection changes; tool-layer ~42KB write timeout; Edit may silently fail; markdown-shape rstrip-and-append hazard).

## Replay

The scene file is included in the `git add` list. On replay, git reads the host filesystem (not the sandbox), so the populated `TestFoundation.unity` from the user's Unity save lands in commit 028's history correctly.

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md ^
  SPACESIM/Assets/Scenes/TestFoundation.unity ^
  SPACESIM/Assets/Editor.meta ^
  SPACESIM/Assets/Editor/PrototypeEditorTest.cs.meta ^
  SPACESIM/Assets/InputSystem_Actions.inputactions.meta ^
  SPACESIM/Assets/Materials.meta ^
  SPACESIM/Assets/Prefabs.meta ^
  SPACESIM/Assets/Readme.asset.meta ^
  SPACESIM/Assets/Scenes.meta ^
  SPACESIM/Assets/Scenes/SampleScene.unity.meta ^
  SPACESIM/Assets/Scenes/TestFoundation.unity.meta ^
  SPACESIM/Assets/Scripts.meta ^
  SPACESIM/Assets/Scripts/Foundation.meta ^
  SPACESIM/Assets/Scripts/Foundation/Coordinates.meta ^
  SPACESIM/Assets/Scripts/Foundation/Physics.meta ^
  SPACESIM/Assets/Scripts/Foundation/PrototypeStartupTest.cs.meta ^
  SPACESIM/Assets/Scripts/Foundation/SimTick.meta ^
  SPACESIM/Assets/Settings.meta ^
  SPACESIM/Assets/Settings/DefaultVolumeProfile.asset.meta ^
  SPACESIM/Assets/Settings/Mobile_RPAsset.asset.meta ^
  SPACESIM/Assets/Settings/Mobile_Renderer.asset.meta ^
  SPACESIM/Assets/Settings/PC_RPAsset.asset.meta ^
  SPACESIM/Assets/Settings/PC_Renderer.asset.meta ^
  SPACESIM/Assets/Settings/SampleSceneProfile.asset.meta ^
  SPACESIM/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset.meta ^
  SPACESIM/Assets/TutorialInfo.meta ^
  SPACESIM/Assets/TutorialInfo/Icons.meta ^
  SPACESIM/Assets/TutorialInfo/Icons/URP.png.meta ^
  SPACESIM/Assets/TutorialInfo/Layout.wlt.meta ^
  SPACESIM/Assets/TutorialInfo/Scripts.meta ^
  SPACESIM/Assets/TutorialInfo/Scripts/Editor.meta ^
  SPACESIM/Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs.meta ^
  SPACESIM/Assets/TutorialInfo/Scripts/Readme.cs.meta ^
  commits/028_prototype_verification_pass.md
git commit -F commits/028_prototype_verification_pass.md
```

(On PowerShell or Unix shells, replace the `^` line continuations with backticks or backslashes respectively.)
