World covers the universe the vessels move through: planet procgen (the multi-stage pipeline that generates planet surfaces, terrain, biomes, atmospheres), surface rendering (LOD scheme, mesh management, atmospheric scattering), body hierarchies (the home system structure, named bodies, masses, SOI radii, orbital relationships), and the distant interstellar destination.

This piece is scoped and substantively designed at the architectural level. No implementation has started. The reference body infrastructure (BodyRegistry, SOI re-rooting, parent-child hierarchy) exists in engine.

## Architectural decisions locked

**Five pipelines** (four after icy collapses as subtype of rocky):

1. **Rocky bodies** (most of system, varied appearances, crater-heavy or smoother). Includes icy subtype (same geometry, different shader/coloring/features). Icy moons have visible jets/plumes from subsurface activity.

2. **Earth-like bodies** (full climate, biomes, oceans, atmospheres, weather). Most complex pipeline. V1 ships with home planet as primary habitable world; pipeline supports more but home system may have just one.

3. **Gas giants** (banded clouds, sometimes rings, storms, no surface). Different rendering entirely (no patches of terrain, just atmospheric layers).

4. **Small non-spherical bodies** (individual asteroids and small moons). Doesn't use sphere geometry. Asteroid fields (clusters) deferred post-v1; v1 has individual asteroids only.

**Cubed sphere geometry with triangulated patches.** Quadtree-of-patches LOD with distance-based subdivision. Patches generated on-demand as player approaches. Smooth blending between LOD levels.

**Single seed per planet.** System seed authoritative; per-body seeds derived deterministically from system seed plus body index. Reproducible: same seed produces identical planets forever (good Minecraft-style social feature).

**Realistic interplanetary distances.** Real solar system scale (Earth-Mars ~1 AU apart, Saturn at ~9.5 AU). Time-warp essential for routine play; strengthens case for event predictor and time-warp work in Phase 1 critical path.

**Determinism preserved across version changes.** If procgen algorithms change between versions, existing saves preserve their planet appearances (baked data) rather than regenerate.

**Quality bar: as good as Juno.** Not photorealistic but believable. KSP-quality is too dated by modern standards. Real attention to shader work, lighting, atmosphere rendering, terrain blending required.

**Visible features in v1:** atmospheric scattering, cloud layers, ice caps, ocean rendering (height-based), realistic temperature/biome variation. Weather visible if possible. Aurora effects on magnetic-field bodies. Seasonal effects deferred post-launch.

**Surface variation:** rocky bodies span gray to rust-red to alien colors, with crater-heavy and smoother variants. Earth-like bodies have significant continent variation (20% to 90% ocean coverage possible), varied vegetation including alien colors (red, blue, purple foliage on some habitable worlds). Gas giants span pastel Earth-system styles to wild alien colors. All within "believable" rather than "neon plastic."

**Deferred for v1:** oblateness (planets are perfectly spherical), tectonic plate simulation, full erosion simulation, asteroid fields, civilization layer (cities, ruins, large structures). All deferred to post-launch.

**Craters and major features:** craters in v1 "look right" rather than physically simulated. Major features (mountain ranges, valleys, basins) are noise-derived with directional bias for natural appearance. No specific volcano/fault placement — that's noise output that looks natural.

**Surface feature placement:** Tier A artifacts (Apollo sites, Saturn station, etc.) are deterministically placed by a separate placement stage that runs after procgen. Placement finds suitable spots (flat enough, accessible). Pillar foundations or local terrain flattening handle non-flat areas if needed.

**Persistence model:** save file stores planet seed + revealed-metadata-state (discovered through gameplay) + player-modification-list. Generated visuals deterministic from seed. Player modifications layer on top.

**First-look load time target: 1-5 seconds.** Streaming continues lazily after that.

**Discovery metadata system.** Each planet has a "fact sheet" of properties (composition, atmospheric profile, mass, radius, temperature, biome distribution if applicable) revealed through sensor scans. Mechanic: player flies sensor-equipped craft near planet, scans it, gets metadata revealed in logbook. Discovery is gated behind gameplay, not free.

**Logbook photo capture.** When player first discovers a body, an auto-captured photo is stored in the logbook. Player records of discoveries are visual.

**Quality slider for performance.** Player can scale procgen detail for hardware. Juno-style comprehensive quality menu.

**Performance target: 60fps preferred, 30fps acceptable on lower hardware.**

## Generation stages (working order)

1. System layout (place bodies at distances, decide types based on distance from sun)
2. Per-body classification (which of 4 pipelines applies)
3. Per-body base geometry (cubed sphere or asteroid for small bodies)
4. Coarse elevation (large-scale terrain features for spherical bodies)
5. Ocean masking (if applicable)
6. Climate computation (if applicable)
7. Biome assignment (if applicable)
8. Fine terrain detail (within patches, on-demand at view-time)
9. Craters and impact features
10. Surface texturing and coloring
11. Cloud layer (if atmosphere)
12. Atmospheric scattering parameters
13. Surface feature placement (rocks, vegetation patches — at view-time, near active camera only)
14. Metadata generation (the fact sheet for discovery)

Stages 1-3 run once at system creation. Stages 4-7, 9, 10 run once at body creation (after first approach). Stages 8, 13 run lazily per-patch as player gets close. Stages 11, 12 are global per-body parameters. Stage 14 produces queryable data.

## Home system

Four bodies receive intensive handcraft (per CONSTRAINTS): home planet (Earth-equivalent), home moon (rocky/desolate, inherited rover and partially-operational base), Mars-equivalent (Mars-like, minimal research outpost), Saturn-equivalent (gas giant with multiple moons, including the icy moon with subsurface ocean and visible jets where the Saturn anomaly takes place).

Other home system bodies receive hand-tuned parameters but less designed content density. Total target body count: 8 bodies in home system, with the four intensive ones plus four others.

## Open questions

- Specific noise function selection per pipeline (simplex vs Perlin vs ridged multifractal vs Worley) — implementation detail.
- Specific shader approach for atmospheric rendering — implementation detail.
- Whether home moon's rover is a Tier A artifact at a specific procgen-determined location.
- Specific parameter ranges per pipeline (how varied is "varied," numerically).

## Status

Largest unbuilt piece of the project alongside Vessels. Probably multi-month effort when implementation begins. Design is architecturally substantive but stops short of stage-by-stage algorithm specification.
