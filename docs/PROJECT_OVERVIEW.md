# SPACESIM Project Overview

SPACESIM is a narrative-driven space simulation game set in the real solar system. Players inherit a fledgling space program in the 2050s and uncover what happened to a previous generation of astronauts who reached Saturn before their wave of space exploration mysteriously ended. The game combines KSP-style rocket building and orbital flight with a coherent simulation foundation that doesn't break at long timescales or unusual conditions.

## Top-level project structure

The project organizes into eight top-level pieces. Everything done on the project fits under one of these.

**1. Engine** (`docs/engine/`). The mathematical and architectural foundation. Coordinates, physics modes, orbital mechanics, simulation tick, reference frames, save/load. The invisible plumbing that lets everything else work. Mostly complete as of Phase 1 entry.

**2. Vessels** (`docs/vessels/`). Everything about player-built craft. Parts library, craft assembly, design validation, structural simulation. The "you build rockets" half of the genre.

**3. World** (`docs/world/`). Everything about the universe the vessels move through. Planet procgen, terrain, atmospheres, body hierarchies, the home system, distant interstellar destinations. The "where you go" half of the genre.

**4. Player tools** (`docs/player_tools/`). How the player interacts with the game. Mission Control UI, map view, vessel control, time controls, agency dashboard, logbook. The screens and interactions through which the player plays.

**5. Game systems** (`docs/game_systems/`). The rules that turn physics and worlds into a game. Resource economy, supply lines, crew, agency management, mission objectives, research, progression.

**6. Automation** (`docs/automation/`). The autopilot system. Lets player-built craft fly without manual control. The scripting language underneath. Decoupled from supply lines: v1 supply lines abstract the autopilot away; autopilot lands as its own system, possibly post-launch.

**7. Narrative** (`docs/narrative/`). The Saturn anomaly storyline, the two-eras worldbuilding, Tier A artifact placement, in-game text, character writing.

**8. Production** (`docs/production/`). Everything around the game itself. Audio, music, marketing, Steam page, demo, post-launch update plan, eventually mod support and multiplayer.

## How to use this structure

Each folder contains piece-specific documents. STATUS.md in each folder is the orientation document — read it to feel oriented about that piece. DESIGN.md (when present) captures locked design content for the piece. OPEN_QUESTIONS.md (when present) captures unresolved questions specific to that piece.

Cross-cutting documents stay at `docs/` root: CONSTRAINTS.md (locked global design), NETCODE_CONTRACT.md (the data shape contract), PHASE_TRACKER.md (temporal view across phases), DECISIONS.md (global decision log), ARCHITECTURE.md, SESSION_PROTOCOL.md.

When working on the project, identify which of the eight pieces your work belongs to, then look at that folder's STATUS.md for orientation and any DESIGN.md for relevant design content.
