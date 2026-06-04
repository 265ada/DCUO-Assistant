# DCUO Quality of Life — v1.2.0

Big update focused on **getting better at DPS** + a new **Builds** library.

## ⚔ DPS Meter — now a real coach, not just numbers
- **MY PERFORMANCE strip** on the overlay: sustained DPS, **Burst (best 10s)**, **Activity %** with an
  S/A/B/C/D/F grade (color-coded like a parse rank), **Crit %**, **APM**, and a **Might / Supercharge /
  Precision split bar** so you see exactly where your damage comes from.
- **Live COACH** — reads your fight and tells you the #1 thing to fix right now: too much downtime,
  low crit, leaning too hard on weapon attacks, no supercharge used… and when you're clean, it gives
  the **pro tip for your power**.
- **Personal bests** — tracks your best burst per power. Beat it and you get a 🏆 **NEW BURST RECORD**
  (with a chime). Otherwise it shows how close you are ("Burst 88% of best").
- **Ability bars** — each ability now has a proportional damage bar, brighter colors, crit % and hit count.
- **Sparring Target parse mode** — auto-detected, marked ⊕ PARSE.
- **Copy Parse to Clipboard** (right-click the overlay) — share a clean parse summary anywhere.

## 📊 Reports — full fight analysis
- **DPS-over-time graph** for each saved fight.
- **Rotation timeline** — every cast with timing, category color, crit marks, and **downtime gap markers**,
  plus a summary (avg gap, longest gap, downtime count) and your power's **ideal rotation** to compare against.
- **Death recap** — the last hits you took before dying.
- Per-fight metric cards (activity+grade, crit, APM, burst) + Coach verdict.

## 🛠 Builds (new tab)
- Current (2026) **Might/Superpowered DPS builds for 15 powers**: Fire, Rage, Atomic, Electricity, Gadgets,
  Mental, Quantum, Munitions, Celestial, Earth, Light, Ice, Water, Nature, Sorcery.
- Each build: numbered **loadout**, **group + solo rotations**, **artifacts** (Transformation Card,
  Strategist Card + 3rd slot), **skill points**, **mods**, and a **"how to pump damage"** tip.
- Opens to **the power you're currently playing** when you have a fight going.
- Sources: dcuobloguide.com power guides + recent community rotation guides. Verify in-game — balance shifts.

## Notes
- combat.log is **local-player only** — this meter is built around making *you* better, not group comparison
  (DCUO doesn't log other players' damage).
