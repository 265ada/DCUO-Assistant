## DCUO Quality of Life — v1.2.2

Fixes & quality-of-life on top of v1.2.0/1.2.1.

### Fixes
- **DPS metrics no longer reset mid-fight.** Combat timeout raised 5s → 18s so boss pauses, running to adds, and mechanics no longer split one encounter into many (which wiped your numbers).
- **Fixed stale data after a character / power swap** — the overlay now always tracks the character you're actually playing, so DPS, burst, activity, crit, Might/Precision split and rotation update correctly when you switch.
- **MY CHARACTER auto-detects** the character you're playing (top damage source, pets excluded) and syncs the overlay, scorecard and drop filtering automatically.

### New
- **Click-through overlays** — the DPS and Scorecard overlays let your mouse pass straight to the game by default. Press **Ctrl+0** to toggle (border turns gold = interactable so you can drag/configure; cyan = click-through).
- **Overlay positions are remembered** across relaunches and updates.

> Self-contained single-file build — just run `DCUO-QualityOfLife.exe`. Reads only your own `combat.log` + screen pixels.
