Player tools covers how the player interacts with the game: Mission Control as the primary UI, map view, vessel control panels (the flight UI when piloting a craft), time controls, the agency dashboard, and the logbook.

This piece is scoped but not designed in detail. Several specific elements have emerged from other design conversations.

## Decisions captured so far

**Mission Control is the primary UI** (per CONSTRAINTS), not a craft-centric view like KSP defaults to. Map view is the dominant interaction surface; the player spends most of their time looking at a map of their operations rather than at a vessel cockpit.

**Map view shows:**

- Vessels as icons (with status indicators including red alert for MIA / damaged vessels)
- Supply routes as lines between locations
- Active operations
- Time controls
- Toggleable supply overlay showing resource flows
- Crew location (you can see where each crew member is)

**Map drills down to per-craft view.** Player can select a specific vessel and see its details, including who's onboard, current mission, fuel state.

**Logbook auto-populates from discovery.** When player discovers a new body, a photo and metadata are captured to the logbook. Player records of discoveries are visual.

**Notifications come through a radio metaphor and pop-ups.** Routine events go in the log silently. Important events surface via radio audio + visual pop-up in the corner of the screen. Alerts are toggleable / mutable.

**Sensor data flows into the logbook.** Scanning a body adds its fact sheet (composition, atmosphere, etc.) to the logbook entry for that body.

**Crew viewable from map view.** See where everyone is. Also accessible from per-craft view (who's on this vessel).

## Open questions / not yet designed

- Specific screens and interactions for the agency dashboard
- Vessel control panel for piloting craft manually
- How crew assignment is surfaced
- How research projects are surfaced (research system itself not yet designed)
- Specific look of the map view (icons, theming, visual style)
- How notifications are visually themed
- Time control UI specifics
- Multiple-vessel selection / management
- Build UI integration (covered in Vessels design)

## Status

Large piece that lands incrementally as different game systems come online. Each system that completes needs its UI to exist as part of that system's completion. No focused design work has happened on the player_tools piece as a whole; UI surfaces have been captured incidentally during other design conversations.
