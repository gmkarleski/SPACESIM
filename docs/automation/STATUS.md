Automation covers the autopilot system: the player-built craft autopilot (lets a craft the player designed fly missions between any two bodies without manual control), the scripting language underneath (the engine that drives autopilot, exposed to advanced players as a scripting interface), and the mission planning logic.

This piece is scoped, partial design captured in conversation, no implementation.

## Decisions captured

**Decoupled from v1 supply lines.** Tonight's design decision: v1 supply lines are abstracted as lines on the map (handled in game systems). The autopilot system is its own work stream that can land independently. Possibly v1, possibly post-launch.

**Long-term goal.** Autopilot must work on any craft the player builds, between any two bodies. This is the central distinctive capability of the project compared to KSP and similar games.

**When autopilot ships, supply lines can reconnect to it.** Player flags a route as "use my actual autopiloted vessel" instead of the abstracted line. This is post-launch material.

**Scripting language present underneath.** The autopilot is built on a scripting language (Vizzy-equivalent under a different name). The UI auto-generates scripts from per-mission-type templates parameterized by destination and craft. Advanced players can use the scripting language directly for custom behavior.

**v1 scope uncertain.** Three options under consideration:

- Hardcoded route templates (no scripting language exposed to players in v1)
- Minimal scripting language designed specifically for automation
- Full scripting language with UI hiding it until post-launch reveal

**Validation up front.** When the player sets up an autopiloted route, the system validates that the craft has sufficient delta-v, fuel capacity, etc. Rejects unsuitable craft with informative feedback.

## Not yet designed in detail

- Mission planning approach (per-mission-type templates vs general trajectory planning)
- Scripting language specifics (visual-blocks vs text-based; command vocabulary; error handling)
- Specific mission types autopilot handles (orbit-to-orbit, surface-to-orbit, atmospheric landing, rendezvous, docking, multi-stop)
- Staging handling
- Failure recovery
- Integration with player progression
- UI for setting up automated routes
- Monitoring and intervention during automated missions

## Open question

The deeper design pass on autopilot was paused because v1 supply lines no longer require it. The system needs design before implementation but the priority is lower than other v1 systems. Autopilot may slip to post-launch.

## Status

One of the largest unbuilt pieces. Multi-month effort when implementation begins. Design alone is significant. Currently on hold pending higher-priority work.
