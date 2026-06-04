using System.Collections.Generic;

namespace DCUOTracker.Models
{
    public record TierEntry(string Power, string Icon, string Why);
    public class TierBand
    {
        public string Tier  { get; init; } = "";
        public string Hex   { get; init; } = "#9CA3AF";
        public string Label { get; init; } = "";
        public List<TierEntry> Entries { get; init; } = new();
    }
    public class TierTable
    {
        public string Note { get; init; } = "";
        public List<TierBand> Bands { get; init; } = new();
    }

    /// <summary>
    /// Community-consensus power tier lists (as of June 2026, GU73 / dual-tray + 5-artifact era).
    /// Synthesized from DCUO creator guides, official-forum discussion and the bloguide.
    /// IMPORTANT: tiers are opinion and shift every episode. DCUO balances DPS *output* closely,
    /// so the DPS list ranks practicality (ease / range / mobility / AoE / burst / utility), NOT raw damage.
    /// Support roles (tank/heal/troll) have the real gaps.
    /// </summary>
    public static class TierLibrary
    {
        // Tier colors (S+ best -> D)
        private const string SP = "#FF79C6";  // pink — revamped / clear standout
        private const string S = "#FFD24A";  // gold
        private const string A = "#FF8000";  // orange
        private const string B = "#C084FC";  // purple
        private const string C = "#3B9DFF";  // blue
        private const string D = "#9CA3AF";  // grey

        // Power -> icon (matches the Builds tab)
        private static readonly Dictionary<string, string> Icon = new()
        {
            ["Fire"]="🔥",["Rage"]="🩸",["Atomic"]="☢",["Electricity"]="⚡",["Gadgets"]="⚙",
            ["Mental"]="🧠",["Quantum"]="🌀",["Munitions"]="💥",["Celestial"]="✨",["Earth"]="🪨",
            ["Light"]="💡",["Ice"]="❄",["Water"]="🌊",["Nature"]="🌿",["Sorcery"]="🔮",
        };
        private static TierEntry E(string power, string why) =>
            new(power, Icon.TryGetValue(power, out var i) ? i : "◆", why);

        public static readonly string[] Roles = { "DPS", "TANK", "HEAL", "TROLL", "BTANK", "BHEAL", "BTROLL", "OVERALL" };

        public static TierTable For(string role) => role.ToUpperInvariant() switch
        {
            "TANK"    => Tank,
            "HEAL"    => Heal,
            "TROLL"   => Troll,
            "BTANK"   => BattleTankT,
            "BHEAL"   => BattleHealT,
            "BTROLL"  => BattleTrollT,
            "OVERALL" => Overall,
            _         => Dps,
        };

        /// <summary>Tier + rationale for a power in a given build role (for the Builds tab badge).</summary>
        public static (string tier, string hex, string why)? Lookup(string power, BuildRole role)
        {
            var tbl = role switch
            {
                BuildRole.Tank             => Tank,
                BuildRole.Healer           => Heal,
                BuildRole.Controller       => Troll,
                BuildRole.BattleTank       => BattleTankT,
                BuildRole.BattleHealer     => BattleHealT,
                BuildRole.BattleController  => BattleTrollT,
                _                          => Dps,
            };
            string Norm(string s) => s.Replace("Hard Light", "Light");
            foreach (var band in tbl.Bands)
                foreach (var e in band.Entries)
                    if (Norm(e.Power).Equals(Norm(power), System.StringComparison.OrdinalIgnoreCase))
                        return (band.Tier, band.Hex, e.Why);
            return null;
        }

        private static readonly TierTable Dps = new()
        {
            Note = "DCUO balances DPS output closely — every power can top a meter in the right hands. "
                 + "These tiers rank PRACTICALITY: ease, range, mobility, AoE vs single-target, burst & power cost. Not raw damage.",
            Bands = new()
            {
                new() { Tier="S+", Hex=SP, Label="REVAMPED — CURRENT #1 (not close)", Entries = {
                    E("Water","Nov 2025 rebalance: big damage buffs, combos now ADD damage, AoE splits to 4 targets, faster animations + stronger High Tide/Crushed synergy. The top DPS right now."),
                }},
                new() { Tier="S", Hex=S, Label="EASIEST-STRONGEST / META-FAVORED", Entries = {
                    E("Munitions","Ranged, mobile (move while firing), low power cost, easy — most popular DPS."),
                    E("Hard Light","Highest output ceiling via clipless construct combos — but mostly melee range. (Light rebalance is NEXT — expect it to climb.)"),
                    E("Electricity","Great range + strong AoE and burst; very forgiving."),
                    E("Gadgets","Stealth-opener crit burst + strong PIs (watch the power drain)."),
                }},
                new() { Tier="A", Hex=A, Label="STRONG, FULLY VIABLE", Entries = {
                    E("Mental","Excellent AoE + Daze/Terror PIs."),
                    E("Celestial","High combo damage, great in cleave."),
                    E("Rage","Big melee burst windows."),
                    E("Fire","Strong burn/DoT burst."),
                    E("Atomic","Aura-combo sustained damage."),
                    E("Quantum","Solid damage + control utility."),
                    E("Earth","Hits hard with aftershock micromanagement."),
                }},
                new() { Tier="B", Hex=B, Label="GOOD / HIGHER EFFORT FOR SAME RESULT", Entries = {
                    E("Ice","Simple and reliable, slightly lower ceiling."),
                    E("Nature","DoT-stack then detonate; setup-heavy."),
                    E("Sorcery","Pet-reliant; fine but fiddly to optimize."),
                }},
            }
        };

        private static readonly TierTable Tank = new()
        {
            Note = "Real gaps here. Self-heal tanks dominate hard/elite content (they out-heal incoming damage); "
                 + "shield & pet tanks are safe and strong in normal content.",
            Bands = new()
            {
                new() { Tier="S", Hex=S, Label="ELITE KINGS (self-heal)", Entries = {
                    E("Fire","'Immortal' self-heal (Fire Soul + Backdraft/Burning Determination). Forgiving, no crash to manage."),
                    E("Rage","Highest self-heal ceiling (lifesteal) — but you must master Rage Crash. Edges Fire when played well."),
                }},
                new() { Tier="A", Hex=A, Label="EXCELLENT", Entries = {
                    E("Atomic","Battle-tank: self-heal + one of the best supercharges + real damage. Keep the aura up."),
                    E("Ice","Shield-stacking, the safest/most forgiving tank — but burst can punch through a dropped shield."),
                }},
                new() { Tier="B", Hex=B, Label="STRONG BUT DEMANDING", Entries = {
                    E("Earth","Brick pet eats huge incoming damage — but high skill, and if the pet dies you get squishy."),
                }},
            }
        };

        private static readonly TierTable Heal = new()
        {
            Note = "All five heal endgame content fine. Gaps are throughput vs. utility/playstyle.",
            Bands = new()
            {
                new() { Tier="S+", Hex=SP, Label="REVAMPED — CURRENT #1 (not close)", Entries = {
                    E("Water","Post-rebalance the strongest healer: massive High Tide throughput + heals while doing real damage. Top healer right now."),
                }},
                new() { Tier="S", Hex=S, Label="TOP", Entries = {
                    E("Celestial","Battle-healer: strong heals AND does damage; very relevant in 2026."),
                }},
                new() { Tier="A", Hex=A, Label="STRONG", Entries = {
                    E("Electricity","Powerful reactive/burst heals — great for spike damage."),
                    E("Nature","Reliable HoT healer (keep Pheromones up)."),
                }},
                new() { Tier="B", Hex=B, Label="VIABLE", Entries = {
                    E("Sorcery","Pet healer; dipped a little lately but still clears everything."),
                }},
            }
        };

        private static readonly TierTable Troll = new()
        {
            Note = "Controller job = power battery + debuffs + crowd control. Power efficiency matters as much as damage.",
            Bands = new()
            {
                new() { Tier="S", Hex=S, Label="TOP", Entries = {
                    E("Mental","Top control + the best power efficiency of the trolls."),
                    E("Gadgets","Strongest debuffs/CC kit — but notorious power drain to manage."),
                }},
                new() { Tier="A", Hex=A, Label="STRONG", Entries = {
                    E("Quantum","Best lockdown/utility (unique relocate-while-locked), damage is average."),
                    E("Light","Solid all-round troll with strong encasement CC."),
                }},
                new() { Tier="B", Hex=B, Label="VIABLE", Entries = {
                    E("Munitions","Works as a troll, just the least common pick."),
                }},
            }
        };

        private static readonly TierTable Overall = new()
        {
            Note = "Versatility-weighted: how strong the power is across the role(s) it can play. "
                 + "The most subjective list — tell me to adjust any placement.",
            Bands = new()
            {
                new() { Tier="S+", Hex=SP, Label="REVAMPED — CURRENT #1", Entries = {
                    E("Water","Just revamped to S+ DPS AND S+ healer — elite in two roles at once. The best overall power right now."),
                }},
                new() { Tier="S", Hex=S, Label="BEST ALL-ROUND", Entries = {
                    E("Rage","S-tier elite tank + strong melee DPS."),
                    E("Fire","S-tier 'immortal' tank + strong DPS."),
                    E("Celestial","S-tier battle-healer + A-tier DPS."),
                    E("Munitions","S-tier practical DPS + viable troll."),
                    E("Electricity","Strong DPS + strong reactive healer."),
                }},
                new() { Tier="A", Hex=A, Label="GREAT", Entries = {
                    E("Atomic","A-tier battle-tank + good DPS."),
                    E("Gadgets","Top practical DPS + S-tier troll."),
                    E("Mental","S-tier troll + strong AoE DPS."),
                    E("Hard Light","Top DPS ceiling + A-tier troll."),
                    E("Quantum","Good DPS + best control utility."),
                }},
                new() { Tier="B", Hex=B, Label="SOLID", Entries = {
                    E("Ice","Forgiving tank + simple DPS."),
                    E("Earth","Strong pet-soak tank + hard-hitting DPS (both high effort)."),
                    E("Nature","Strong HoT healer + DPS."),
                    E("Sorcery","Pet healer + DPS; jack-of-trades."),
                }},
            }
        };

        // ── BATTLE role tiers (support duty + damage) ──
        private static readonly TierTable BattleTankT = new()
        {
            Note = "Battle tank = tank while doing real damage. Self-heal/aura tanks shine because damage feeds survival.",
            Bands = new()
            {
                new() { Tier="S", Hex=S, Label="BEST BATTLE TANKS", Entries = {
                    E("Fire","Fire Soul makes your damage = your defense. The best battle tank, period."),
                    E("Rage","Lifesteal scales with damage — go aggressive, heal more the harder you hit."),
                    E("Atomic","Quark-Gluon Aura absorbs + self-heals while you AoE combo."),
                }},
                new() { Tier="A", Hex=A, Label="STRONG", Entries = {
                    E("Earth","Big Jackhammer melee damage while Brick soaks — high skill, pet-dependent."),
                }},
                new() { Tier="B", Hex=B, Label="WORKS", Entries = {
                    E("Ice","Safest, but the lowest damage ceiling of the tanks."),
                }},
            }
        };
        private static readonly TierTable BattleHealT = new()
        {
            Note = "Battle healer = heal while doing damage. Some powers are built for it.",
            Bands = new()
            {
                new() { Tier="S+", Hex=SP, Label="REVAMPED — CURRENT #1", Entries = {
                    E("Water","The revamp boosted both damage AND High Tide healing — the best battle healer right now. Tsunami heals + hits, AoE to 4."),
                }},
                new() { Tier="S", Hex=S, Label="BEST BATTLE HEALERS", Entries = {
                    E("Celestial","THE classic battle healer — combo curses for near-DPS damage while healing."),
                }},
                new() { Tier="A", Hex=A, Label="STRONG", Entries = {
                    E("Electricity","Shared electric abilities heal and damage; reactive."),
                    E("Nature","Poison DoTs do real damage while HoTs roll."),
                }},
                new() { Tier="B", Hex=B, Label="VIABLE", Entries = {
                    E("Sorcery","Pet does most of the damage; lower personal output."),
                }},
            }
        };
        private static readonly TierTable BattleTrollT = new()
        {
            Note = "Battle troll = battery/debuff while doing damage. Power efficiency decides how much you can DPS.",
            Bands = new()
            {
                new() { Tier="S", Hex=S, Label="BEST BATTLE TROLLS", Entries = {
                    E("Mental","Best power efficiency of the trolls = the most room to DPS."),
                    E("Gadgets","Strong PI damage + stealth burst (mind the power drain)."),
                }},
                new() { Tier="A", Hex=A, Label="STRONG", Entries = {
                    E("Quantum","Good damage + the best control/utility kit."),
                    E("Light","Construct-combo damage while you debuff."),
                }},
                new() { Tier="B", Hex=B, Label="WORKS", Entries = {
                    E("Munitions","Reload batteries while the Shrapnel Burn keeps damage flowing."),
                }},
            }
        };
    }
}
