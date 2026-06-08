# DCUO Assistant — Autonomous Build Log

Goal: best DPS meter DCUO has. Plus Builds tab (Fire priority, current ≤5mo).
User asleep. Work autonomously. On usage reset, READ THIS FILE, continue where left off.

## STATE: ALL 11 PHASES DONE + extras. v1.2.0 built, verified RUNNING. Release HELD for user smoke-test.

## ★ FOR USER WHEN YOU WAKE ★
Everything built + running (v1.2.0). I did NOT push to GitHub or cut a release — your standing rule is
"smoke-test + ask yes/no before uploading to a release," and you were asleep. Nothing committed to git either
(all changes are saved in the project files + baked into the running exe).

WHEN YOU'RE READY:
1. Use the app — check the new overlay COACH line, the BUILDS tab, and a saved fight in REPORTS (graph +
   rotation timeline + death recap).
2. Tell me "yes it works" → I'll commit, push to GitHub, and publish the v1.2.0 release (not pre-release).
   Or tell me what to fix first.
See RELEASE_NOTES_v1.2.0.md for the full changelog.

Known limitation (honest): Activity%/APM/rotation are derived from damage ticks (the log has no cast-start
events), so DoT-heavy powers like Fire read slightly generous on uptime. Still directionally useful.

## Standing rules (from memory + session)
- Caveman mode full intensity
- After EVERY build: Stop-Process old exe, publish single-file, relaunch, verify PID running
- Bundle ALL runtimes into exe, push binary to GitHub release after build (not pre-release)
- Smoke-test before push: launch exe, normally ask user yes/no — but USER ASLEEP so verify PID-running instead
- Icon colored only when feature active (DPS button etc.)
- combat.log: C:\Users\prkid\Documents\My Games\DC Universe Online\Logs\combat.log
- combat.log = LOCAL PLAYER ONLY. Group DPS impossible. Don't try again.

## Roadmap (priority order)
- [ ] Phase 1: Real-time animated DPS bars (sorted, grow) in overlay
- [ ] Phase 2: Activity/uptime %, crit % overall + per ability
- [ ] Phase 3: Parse mode — sparring target detect, 1min/5min clean parse + grade S/A/B/C
- [ ] Phase 4: Burst window (best 10s DPS), sustained DPS
- [ ] Phase 5: Might vs Precision split (power dmg vs weapon dmg)
- [ ] Phase 6: Rotation timeline / ability sequence + clipping detection
- [ ] Phase 7: Personal best compare + "beat best by X%"
- [ ] Phase 8: Death recap (incoming dmg that killed)
- [ ] Phase 9: Builds tab (Fire first) — loadout, artifacts, SP, rotation
- [ ] Phase 10: DPS grade vs CR-adjusted benchmark, power-specific tips
- [ ] Phase 11: Polish UI, version bump, GitHub release

## Research notes

### Meter feature ideas (from Details!/WoW, FFLogs/FFXIV ACT)
- PARSE CULTURE: DCUO players parse on Sparring Target for fixed durations (1min/5min std). Detect "Sparring Target" name in log -> parse mode. Clean DPS = total dmg / duration.
- GRADE/PARSE COLOR: FFLogs ranks by percentile, color-coded (grey<25, green25, blue50, purple75, orange95, pink99, tan100). Replicate as S/A/B/C/D/F grade w/ same colors based on DPS-per-CR benchmark.
- ACTIVITY %: fraction of fight time actively dealing dmg (gaps = downtime). Key skill metric.
- APM: actions/abilities per minute. Casts per min.
- BURST: best rolling 10s DPS window vs sustained DPS.
- UPTIME: buff/debuff uptime (limited in DCUO log).
- DEATH RECAP: last incoming hits before death.
- Per-ability: total, %share, hits, crit%, avg, max. Sorted bars.

### DCUO-specific metrics to build
- Might (power dmg) vs Precision (weapon dmg) split — DCUO core. Classify via ability category (Weapon/Power/Supercharge already in PowerTypes.cs).
- Supercharge usage count + dmg.
- DPS = damage / time. Parse DPS on sparring target.

### Fire DPS build (CURRENT, dcuobloguide, Might-based superpowered)
- Loadout: Flame Cascade, Fireburst, Spontaneous Combustion, Meteor Strike, Inferno, Volcanic Calamity (ST) / Fireball Barrage (multi)
- Stats: Focus Superpowered 1pt, Crit Attack Chance 20, Crit Attack Dmg 40, Might&Power 175, Health rest
- Rotation solo: Meteor Strike > Spontaneous Combustion > Fireburst > Flame Cascade
- Rotation group: Inferno > Meteor Strike > Spontaneous Combustion > Fireburst > Flame Cascade
- Mods: Weapon Blast/Absorption Adapter, Neck Escalating Might, Back Berserker, Hands Max Damage
- Sockets: Red Might, Yellow Might&Power, Blue Might&Health
- Artifacts (universal might meta): Transformation Card, Strategist Card, + power-specific. Atomic uses Lernae's Amulet/Candle of Neron/Omega Totality/Purified Pages.
- Sources: dcuobloguide.com/traits/powers/fire-dps-guide, YT "DCUO 2026 Fire DPS Rotation Guide" (oi1fofO9nXM, Mar 2026)

## DONE LOG
- v1.2.2 RELEASED (commit 50af132, gh release Latest, exe+sha256 835516ed): fixed mid-fight metric resets (FIGHT_GAP 5->18s), stale data after char/power swap (overlay re-fetches live player), MY CHARACTER auto-detect (top non-pet source -> syncs overlays+drop filter), click-through overlays via Ctrl+0 (Services/ClickThrough.cs, gold=editable/cyan=passthrough), overlay positions persisted (Dps+Scorecard Left/Top in settings). STILL QUEUED: nicer update-progress visual + party-frame region persistence.
- TIERS vertical fill: bands now UniformGrid(Columns=1) stretch to share full height + bigger badges/labels (was wasted bottom void). Smoke-test PASSED by user. PUSHED v1.2.0 RELEASE: commit 14ff8a6 -> master; gh release v1.2.0 (Latest, not prerelease, not draft) w/ exe (204MB) + exe.sha256 (hash c4a36f...). Next: user wants minor bump v1.2.1 to test auto-updater on their installed v1.2.0.
- POWER PRIORITY baked into all builds (all roles+battle): "WHERE YOUR POWER COMES FROM" banner w/ segmented bar (Artifacts ~50-70% / Allies ~10-20% / SP+Gear rest) + "level 5 artifacts to 200 FIRST". Section headers relabeled: ARTIFACTS=#1 POWER (~50-70%), ALLIES=#2 (~10-20%), SKILL POINTS(+gear=rest).
- SCREEN REAL ESTATE: TIERS tab tier entries now flow into wrapping CARDS (WrapPanel) w/ bigger text — fill width, add columns as window grows (was cramped on left 1/3). PERMANENT RULE saved to memory (ui_use_full_screen.md): all GUIs must expand/reflow/scale to fill enlarged windows. BUILT+RUNNING PID25784.
- WATER -> S+ (user-corrected, verified Nov2025 official Water rebalance): added SP tier (#FF79C6 pink) above S; Water now S+ in DPS/HEAL/BATTLEHEAL/OVERALL, removed from old A/S. Noted Light is NEXT rebalance (on Hard Light). Verified rest of board stands.
- ALLIES on all builds: AllyRec(Slot,Name,Why,Alt) + AlliesForRole(role) (battle->DPS allies). DPS combat = Starfire (verified +359% Starbolt, top). Support ×2 = Legendary allies (Cyborg/Professor Zoom/The Flash — buffed to Legendary, 2nd support ability) w/ role-matching passives + combat-ally cooldown passive. Green ALLIES section on build page (slot badge + why + alt) + niche-meta disclaimer. LOWEST-confidence area (web thin) — flagged for user confirmation. BUILT+RUNNING PID5788.
- ABILITY TAGS on all loadout abilities (both trays): Models/AbilityTag.cs classifier (ordered keyword rules tuned to BuildLibrary ability names) → color-coded chip per ability: DMG/DoT/COMBO/SHIELD/HEAL/HoT/GROUP POWER/DEBUFF/ST TAUNT/AGGRO PULL/BREAKOUT/PET/CC/BUFF/MITIGATE/SUPERCHARGE/ICONIC/AUGMENT (some w/ sub-notes e.g. "COMBO (weave)", "DEF DEBUFF", "PULL + HEAL"). LoadoutSlot carries TagBrush; tag chip right-aligned on each Bar1/Bar2 row. Default DMG for unmatched (correct for most). BUILT+RUNNING PID45584.
- BATTLE ROLES + richer alts + tier-in-builds: BuildRole enum +BattleTank/BattleHealer/BattleController. MakeBattleRole(power) static-ctor appends a battle build to all 15 powers (hybrid stats/mods, mixed support+damage artifacts, damage loadout, mechanic+rotation+tips). Iconics expanded to 4-5 per role + BattleIconics (damage-leaning) — all noted as SP-tree unlockables, each with 2-3 alternatives. Artifact alts expanded to 3-4 each. TierLibrary: added BattleTank/BattleHeal/BattleTroll tables + Lookup(power,role); TIERS tab now has B.TANK/B.HEAL/B.TROLL buttons. Build page shows a TIER badge + rationale per power+role (incl battle). BUILT+RUNNING PID34608.
- TIER LIST tab (🏆 TIERS): Models/TierList.cs. Role selector DPS/TANK/HEAL/TROLL/OVERALL, color S/A/B/C bands w/ per-power rationale + dated/sourced disclaimer. KEY accuracy framing: DPS output is balanced in DCUO -> DPS tiers = practicality not raw dmg; support roles have real gaps. Tanks: Fire+Rage S (self-heal kings, corrected from earlier bad SEO answer), Atomic+Ice A, Earth B. Heal: Water+Celestial S. Troll: Mental+Gadgets S. Researched via creators/forums/bloguide, BLOCKED seo junk (iggm/gamer-choice/mmokb). Builds confirmed current (bloguide GU73). Invited user (the expert) to correct placements. BUILT+RUNNING PID22928.
- GROUP DPS via SCORECARD OCR (TOS-safe, no memory): User confirmed in-game Scorecard→Leaderboard shows everyone. Built ScorecardScanner (OCR region, parse rows: name + Damage Out + heal/power/deaths, + "Time Since Start MM:SS" → group DPS). Models/ScorecardData.cs. New ScorecardOverlay (ranked Name/DPS/Damage + bars, highlights me gold). Green ▦ SCORECARD calibrate button in header (persists ScoreX/Y/W/H to settings). F10 global hotkey scans + shows overlay. Refused memory-scan (bannable). BUILT+RUNNING PID47588.
- BUILDS v3 (5 artifacts + iconics): Confirmed GU155 = 5 artifact slots. ArtifactRec(Name,Why,Alt). Per-role 5-artifact sets w/ WHY + substitute each: DPS (Transformation/Strategist/Grimorium/Eye of Gemini/Source Shard), Tank (Everyman/Mystic Symbol/Manacles/Cog/Amulet of Rao), Heal (Purple Healing Ray/Philosopher's Stone/Scrap/Clarion/Amulet of Rao), Troll (Quislet/Tetrahedron/Entwined Rings/Strategist/Scrap). Added ICONIC POWERS section per role (general iconics everyone can slot: Heat Vision/Neo-Venom/Robot Sidekick/Word of Power + alts) as tray substitutes; "White Lantern Augment" entries now point to it. UI: rich artifact cards (slot/name/why/alt) + iconics list. BUILT+RUNNING PID37792.
- BUILDS v2 (dual-tray revamp): Confirmed real 2026 change — 2nd ability tray + Iconics + White Lantern Power Array Augment. Reworked PowerBuild model → per-power Roles[] (RoleBuild = Bar1[6]+Bar2[6], mechanic, artifacts, stats, mods, rotations, tips, source). Researched + added DPS for 15 powers PLUS support: TANK (Fire/Ice/Earth/Rage/Atomic), HEALER (Electricity/Celestial/Sorcery/Nature/Water), TROLL (Gadgets/Mental/Quantum/Munitions/Light) — all from dcuobloguide GU73 guides. UI: role selector buttons (DPS/Tank/Heal/Troll), mechanic callout, PRIMARY TRAY + SECONDARY TRAY (12 abilities), per-role everything. Coach + reports ideal-rotation updated to b.Dps. BUILT+RUNNING PID34108.
- CKPT5-8: v1.2.0 bump. DPS-over-time graph in reports (Polyline from persisted DpsCurve). "Copy Parse to Clipboard" on overlay. PB chime (SoundAlert.PlayPersonalBest, gated by sound toggle). Artifacts refined to real 2026 meta (Transformation Card +20%crit/+30%critdmg, Strategist Card, 3rd-slot options). Ideal-rotation tie-in in reports (actual vs recommended). Builds tab auto-selects your live power. Cast detector tightened (0.45s floor) to stop DoT-tick inflation on Fire/burn powers. ALL BUILT+VERIFIED RUNNING.
- CKPT4: Reports PERFORMANCE ANALYSIS — picks my/top player. Metric cards (activity+grade, crit, APM, burst), Might/SC/Prec split bar, COACH verdict, ROTATION TIMELINE (cast-by-cast w/ time, category color, crit ✶, downtime gap markers ▽ + summary: casts/avg gap/longest/downtime count), DEATH RECAP (last hits taken, shown if died). FightReport extended to persist all analytics + casts(cap150) + incoming(cap12). Coach refactored to primitives overload. BUILT+RUNNING PID20208.
- CKPT2: BUILDS tab — 15 DPS powers (Fire,Rage,Atomic,Electricity,Gadgets,Mental,Quantum,Munitions,Celestial,Earth,Light,Ice,Water,Nature,Sorcery). Each: loadout(numbered), group+solo rotation, artifacts(R200), mods, skill points, "how to pump damage" tips, source. Models/PowerBuild.cs + BuildLibrary. Master-detail UI mirrors Reports. BUILT+RUNNING.
- CKPT3: Live COACH (Models/Coach.cs) — adaptive tip picks worst issue: downtime/low-crit/too-weapon-heavy/no-SC, else power-specific pro tip. Personal-best tracking (Services/ParseBests.cs, per-power best burst persisted %AppData%) — "NEW BURST RECORD" / "% of best" on overlay. BUILT+RUNNING PID41172.
- CKPT1: Overlay MY PERFORMANCE strip — sustained DPS, burst(best 10s), activity% + S/A/B/C/D/F grade (color), crit%, APM, Might/Super/Precision split bar. Per-ability crit%+hits on bars. Brighter colors. Parse-mode title indicator. Data model: PlayerStats activity/burst/casts/incoming/death tracking; FightData IsSparringParse. Parser handles Damage In (death recap data) + KO + sparring detect. Models/DpsGrade.cs (FFLogs palette). BUILT+RUNNING PID38556.
