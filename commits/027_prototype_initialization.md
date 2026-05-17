# 027: Unity prototype initialization — Phase 0 prototype scaffolding

File-level setup for the Phase 0 netcode contract prototype. The Unity project at `SPACESIM/` (created via Unity Hub with Unity 6000.4.7f1 LTS and the Universal 3D template) is augmented with the folder structure, packages, scripts, scenes, and documentation needed to begin implementing the netcode contract specified in `docs/NETCODE_CONTRACT.md`.

This commit does no Unity authoring (Cowork cannot drive Unity directly). It does file-level work: creates `.gitignore` at repo root, adds `com.unity.mathematics` and `com.unity.burst` to the Unity package manifest, creates `Assets/Scripts/Foundation/{Coordinates,SimTick,Physics}` folder structure plus `Editor/`, `Materials/`, `Prefabs/`, writes a minimal `PrototypeStartupTest` MonoBehaviour and a `PrototypeEditorTest` editor menu, writes an empty `TestFoundation.unity` scene (no GameObjects — user adds them on first open), writes `SPACESIM/README.md` with project orientation and first-open instructions, and appends a `**Prototype implementation: started.**` paragraph to `docs/CONSTRAINTS.md` §9 Phase 0 describing the prototype's commencement.

The folder name is `SPACESIM/` (the actual Unity project location), not `game/` (the placeholder used in the originating instruction). The folder name itself is irrelevant to the prototype; the implementation work and the user-side verification step both operate against `SPACESIM/`.

## Scope

Eight file-system operations and one CONSTRAINTS edit:

- `/.gitignore` — created. Repo-root .gitignore covering the Unity project at `SPACESIM/` and any future Unity projects in the repo. Ignores `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `MemoryCaptures/`, `obj/`, `Build/`, `Builds/`, IDE auto-generated files (`*.csproj`, `*.sln`, `*.slnx`, `*.suo`, `*.user`, etc.), Unity packed assets (`*.apk`, `*.aab`, `*.unitypackage`), IDE workspaces (`.vscode/`, `.vs/`, `.idea/`), OS-specific files (`.DS_Store`, `Thumbs.db`, `desktop.ini`), editor backup files, and `*.recovery` files from the bash-via-Python escape-hatch write pattern.
- `SPACESIM/Packages/manifest.json` — modified. Added `com.unity.mathematics: 1.3.2` and `com.unity.burst: 1.8.24` to the dependencies dict, alphabetically positioned. All other dependencies preserved unchanged (47 total dependencies post-write).
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/.gitkeep` — created. Folder placeholder.
- `SPACESIM/Assets/Scripts/Foundation/SimTick/.gitkeep` — created. Folder placeholder.
- `SPACESIM/Assets/Scripts/Foundation/Physics/.gitkeep` — created. Folder placeholder.
- `SPACESIM/Assets/Prefabs/.gitkeep` — created. Folder placeholder.
- `SPACESIM/Assets/Materials/.gitkeep` — created. Folder placeholder.
- `SPACESIM/Assets/Scripts/Foundation/PrototypeStartupTest.cs` — created. MonoBehaviour with `Start()` logging "Phase 0 prototype foundation ready" plus diagnostics. Namespace `SpaceSim.Foundation`.
- `SPACESIM/Assets/Editor/PrototypeEditorTest.cs` — created. Editor script registering "Prototype/Verify Setup" menu item. Namespace `SpaceSim.Foundation.EditorTools`. Logs Unity version, parses `Packages/manifest.json` line-by-line, confirms presence of mathematics / burst / inputsystem / URP packages, and confirms editor scripting infrastructure works.
- `SPACESIM/Assets/Scenes/TestFoundation.unity` — created. Minimal empty Unity scene with the four required scene-level singletons (OcclusionCullingSettings, RenderSettings, LightmapSettings, NavMeshSettings) derived verbatim from the template SampleScene.unity. No GameObjects — the user creates an empty GameObject and attaches `PrototypeStartupTest` on first Unity open per the README's first-open instructions.
- `SPACESIM/README.md` — created. Project orientation including Unity version, package list with versions, planned prototype scope (cube launches-to-moon-landing per netcode contract §10), folder layout, first-open user instructions, and implementation roadmap.
- `docs/CONSTRAINTS.md` — modified. Single paragraph append to §9 `### Phase 0 — Decisions (weight 1)` recording the prototype's commencement.

## Rationale

`docs/CONSTRAINTS.md` §9 Phase 0 names the netcode contract as a two-part deliverable: written contract (commit 026) plus prototype implementation. Commit 026 landed the written contract at `docs/NETCODE_CONTRACT.md`. The prototype was named as the remaining Phase 0 deliverable; commit 027 starts that work.

The work split between Cowork (the artifact's author) and the user (the Unity operator) is structural to how Cowork operates: Cowork can write source files, scene files, configuration files, and documentation; Cowork cannot open Unity, drive its UI, or trigger asset import. The user opens Unity, drives the UI, and verifies asset import results. Commit 027 lands everything Cowork can land; commit 028 (next commit) will record the user's verification pass and commit the `.meta` files Unity auto-generates on first import.

The five locked architectural decisions named in the netcode contract (30 Hz fixed sim-tick, authoritative-state-only determinism, 2-4 player multiplayer for v1.1+, single-player as multiplayer degenerate case, mode separation per CONSTRAINTS §2) inform the folder layout. `Scripts/Foundation/Coordinates/` will hold double-precision world coordinate types and floating-origin shift logic. `Scripts/Foundation/SimTick/` will hold the 30 Hz fixed-timestep loop and the ten-step sim-tick cycle from contract §1.2. `Scripts/Foundation/Physics/` will hold the PhysX-active mode boundary, mode transitions, and the read-write contract between PhysX and authoritative state. The three folders correspond directly to the three structural responsibilities the contract names; folder layout is the design specification given physical form.

The `com.unity.mathematics` package adds `double3` and SIMD-friendly math types needed for the double-precision world coordinates the contract specifies. The `com.unity.burst` package adds Burst-compilable hot paths for the sim-tick advance loop, which runs 30 times per second and must be performant on target hardware. The Input System and URP packages were template-provided and remain at their template versions; commit 027 added only the two packages the prototype work requires beyond the template baseline.

The empty `TestFoundation.unity` scene with the four scene-level singletons is the minimal valid Unity 6 scene. Writing a scene with GameObjects by hand is risky — Unity 6's YAML scene format requires specific GUID generation and component-ordering rules that hand-authoring can violate subtly. The user adds the GameObject and attaches the script via Unity's UI on first open, which is a 30-second interactive task; the file-level setup gets the scene asset on disk so git tracks it.

The `.meta` files Unity auto-generates for the new scripts, scene, and folders are deliberately not pre-written by Cowork. Pre-writing `.meta` files with hand-chosen GUIDs risks collisions and inconsistencies with Unity's own GUID generation pass. Unity overwrites pre-written `.meta` files on first import anyway. The clean path: Cowork writes the source files; the user opens Unity, which generates the `.meta` files; commit 028 commits those `.meta` files to git.

The CONSTRAINTS.md §9 Phase 0 paragraph append records the prototype's commencement at the same level of granularity as commits 002, 010, 021, and 026's prior Phase 0 paragraphs. The verbatim-with-context anchor discipline (workflow rule 5) applied: seven boundary anchors captured pre-write covering every commit's Phase 0 paragraph, verified preserved post-write, internal ordering verified.

## Changes

Operation sequence:

1. Write `/.gitignore` at repo root (new file, ~70 lines).
2. Modify `SPACESIM/Packages/manifest.json` via Python json module — load, mutate dependencies dict to add two packages alphabetically, dump with 2-space indent matching Unity's format, write-then-rename atomic.
3. Create folder structure under `SPACESIM/Assets/`: `Scripts/Foundation/Coordinates/`, `Scripts/Foundation/SimTick/`, `Scripts/Foundation/Physics/`, `Prefabs/`, `Materials/`, `Editor/`. Place `.gitkeep` in directories that would otherwise be empty (Coordinates, SimTick, Physics, Prefabs, Materials).
4. Write `Assets/Scripts/Foundation/PrototypeStartupTest.cs` — MonoBehaviour, ~25 lines.
5. Write `Assets/Scenes/TestFoundation.unity` — empty Unity 6 scene derived from SampleScene.unity. Extracted the four scene-level singleton documents from SampleScene.unity (lines 1-121, ending just before the first `--- !u!1 ` GameObject marker) verbatim. 121 lines / 3,507 bytes. Sanity: exactly 4 document markers; zero GameObject (`!u!1`) declarations.
6. Write `Assets/Editor/PrototypeEditorTest.cs` — editor script with menu item, ~80 lines.
7. Write `SPACESIM/README.md` — orientation doc, ~110 lines.
8. Append `**Prototype implementation: started.**` paragraph to `docs/CONSTRAINTS.md` §9 Phase 0 via the standard subsection-body-append pattern with boundary-anchor validation.

CONSTRAINTS.md grew from 1956 lines / 228,053 bytes to 1958 lines / 229,089 bytes (+2 lines, +1,036 bytes).

## Verification

114 checks, all passing on first run. Nine groups:

### A. File and folder existence (21 checks)

- 12 expected files exist: `.gitignore`, `Packages/manifest.json`, `PrototypeStartupTest.cs`, `PrototypeEditorTest.cs`, `TestFoundation.unity`, `SPACESIM/README.md`, `CONSTRAINTS.md`, plus five `.gitkeep` placeholders
- 9 expected directories exist: `Scripts/`, `Scripts/Foundation/`, `Scripts/Foundation/Coordinates/`, `Scripts/Foundation/SimTick/`, `Scripts/Foundation/Physics/`, `Scenes/`, `Materials/`, `Prefabs/`, `Editor/`

### B. `.gitignore` content (14 checks)

The `.gitignore` contains all required ignore entries: Unity caches (`**/Library/`, `**/Temp/`, `**/Logs/`, `**/UserSettings/`, `**/MemoryCaptures/`, `**/obj/`, `**/Build/`, `**/Builds/`), IDE files (`**/*.csproj`, `**/*.sln`, `**/*.slnx`), OS files (`.DS_Store`, `Thumbs.db`, `desktop.ini`), and the recovery-file pattern (`*.recovery`).

### C. `manifest.json` validity and required packages (6 checks)

- File parses as valid JSON via `json.load`
- `com.unity.mathematics` pinned at `1.3.2`
- `com.unity.burst` pinned at `1.8.24`
- `com.unity.inputsystem` present (template-provided, unchanged)
- `com.unity.render-pipelines.universal` present (template-provided, unchanged)
- `com.unity.test-framework` present (template-provided, unchanged)

### D. Script content sanity (10 checks)

`PrototypeStartupTest.cs`: `using UnityEngine`, `namespace SpaceSim.Foundation`, private `Start()` method, `Debug.Log("Phase 0 prototype foundation ready")`, sealed MonoBehaviour class signature.

`PrototypeEditorTest.cs`: `using UnityEditor`, `[MenuItem(MenuItemPath)]` with path `"Prototype/Verify Setup"`, reads `Packages/manifest.json`, logs `Application.unityVersion`, confirms `"Editor scripting infrastructure verified"`.

### E. `TestFoundation.unity` validity (7 checks)

- Starts with `%YAML 1.1`
- Contains the Unity tag declaration `%TAG !u! tag:unity3d.com,2011:`
- Contains all four required scene-level singletons: OcclusionCullingSettings, RenderSettings, LightmapSettings, NavMeshSettings
- Exactly 4 document markers (`^--- !u!`)
- Zero GameObject declarations (`^--- !u!1 `) — confirms empty scene as designed

### F. `SPACESIM/README.md` content (7 checks)

References Unity 6000.4.7f1, the netcode contract path `../docs/NETCODE_CONTRACT.md`, the CONSTRAINTS.md path `../docs/CONSTRAINTS.md`, the cube-launches-to-moon-landing scenario, the "First-open instructions" section for user verification, and the specific package versions (1.3.2 for Mathematics, 1.8.24 for Burst).

### G. `CONSTRAINTS.md` §9 Phase 0 update (6 checks)

- `**Prototype implementation: started.**` paragraph present exactly once
- References `SPACESIM/` path
- References Unity 6000.4.7f1
- References commit 027 explicitly
- References the cube-launches-to-moon-landing scenario
- References `docs/NETCODE_CONTRACT.md` §10 as the validation milestone source

### H. Seven boundary anchors in §9 Phase 0 preserved + ordering (8 checks)

Boundary anchors from commits 001, 002, 010, 021, 026 all preserved exactly once. §9 Phase 0 internal ordering preserved end-to-end: opener (commit 001) < netcode contract paragraph (commit 002) < three-tier paragraph (commit 010) < four-body paragraph (commit 021) < netcode-landed paragraph (commit 026) < closing "Prototype work is the next Phase 0 deliverable." sentence (commit 026) < new prototype-started paragraph (commit 027).

### I. Section h3 counts and prior-commit anchors (35 checks)

- All 15 section h3 counts unchanged from commit 026: §1=15, §2=15, §3=18, §4=17, §5=6, §6=13, §7=7, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0
- Commit 014 damage-repair anchor preserved
- Commit 015 first verbatim-with-context anchor preserved
- Commit 017 section headings (`## 3. Gameplay mechanics`, `## 4. World behavior and discovery`) preserved
- Commit 019 three-category framing preserved verbatim (`Three-category architecture for crew sustenance and exposure`; `Radiation dose, zero-G exposure, mission stress`)
- Commit 021 four-bodies-intensive-hand-craft preserved
- Commit 022 strip-mining anchor preserved
- Commit 023 Mission Control heading preserved
- Commit 024 Flight computers heading preserved
- Commit 025 atmospheric flight subsection and pillar both preserved
- Commit 025 §10 RESOLVED marker preserved
- Commit 025 Phase 5 weight 4 reference preserved
- Commit 026 contract path reference preserved
- Cross-section anchors: `Engineering as the verb` (§1), `Floating origin shift threshold: 50 km default` (§2)

## User-side verification step (post-commit)

Cowork cannot drive Unity. The verification path Cowork cannot execute, which the user runs after this commit lands:

1. Open Unity Hub. Open the `SPACESIM` project.
2. Wait for Unity to import the two new packages (`com.unity.mathematics`, `com.unity.burst`). Console should report package resolution.
3. **Confirm package resolution:** Window → Package Manager. Verify Mathematics 1.3.2 and Burst 1.8.24 are listed. If Unity resolved to different versions, surface for adjustment.
4. **Run the editor verify menu:** Prototype → Verify Setup. Confirm console logs:
   - `Unity version: 6000.4.7f1`
   - Multiple `package:` lines including `com.unity.mathematics` and `com.unity.burst`
   - `Editor scripting infrastructure verified`
5. **Open the test scene:** Project window → Assets/Scenes → TestFoundation.unity. The scene is empty (no GameObjects, no camera, no light — just the four scene-level singletons).
6. **Add the smoke test:** Right-click in Hierarchy → Create Empty. Name it "PrototypeTest". With it selected, click Add Component in the Inspector. Search for `PrototypeStartupTest`. Attach the component. Save the scene (Ctrl+S / Cmd+S).
7. **Press Play.** Confirm console logs:
   - `Phase 0 prototype foundation ready`
   - `Unity version: 6000.4.7f1`
   - Platform identifier and persistent data path
8. Stop play mode.

If steps 3, 4, and 7 all log the expected lines without errors, the file-level setup is verified at runtime. Commit 028 will record the verification pass and commit the `.meta` files Unity generated during the first import.

If any step fails, stop and surface the failure. The most likely failure mode is Unity refusing to load `TestFoundation.unity` (the hand-derived scene file format may not match Unity 6's strict expectations exactly). Fallback: delete `TestFoundation.unity`, in Unity use File → New Scene → 3D Core, save as TestFoundation.unity in the Scenes folder, then continue from step 6. The fallback path produces a working scene without disturbing other prototype work.

## Replay

```
cd C:\Users\gmkar\space_sim
git add .gitignore SPACESIM/Packages/manifest.json SPACESIM/Assets/Scripts/Foundation/PrototypeStartupTest.cs SPACESIM/Assets/Scripts/Foundation/Coordinates/.gitkeep SPACESIM/Assets/Scripts/Foundation/SimTick/.gitkeep SPACESIM/Assets/Scripts/Foundation/Physics/.gitkeep SPACESIM/Assets/Prefabs/.gitkeep SPACESIM/Assets/Materials/.gitkeep SPACESIM/Assets/Editor/PrototypeEditorTest.cs SPACESIM/Assets/Scenes/TestFoundation.unity SPACESIM/README.md docs/CONSTRAINTS.md commits/027_prototype_initialization.md
git commit -F commits/027_prototype_initialization.md
```
