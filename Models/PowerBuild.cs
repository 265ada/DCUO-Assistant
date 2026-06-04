using System.Collections.Generic;
using System.Linq;

namespace DCUOTracker.Models
{
    public enum BuildRole { DPS, Tank, Healer, Controller, BattleTank, BattleHealer, BattleController }

    /// <summary>One of the 5 equipped artifact slots: what to run, why, and a substitute.</summary>
    public record ArtifactRec(string Name, string Why, string Alt);

    /// <summary>An iconic/power tray slot: best-in-slot pick, why, and an alternative.</summary>
    public record IconicRec(string Name, string Why, string Alt);

    /// <summary>An ally slot recommendation (Combat or Support ×2) with synergy reasoning + alternatives.</summary>
    public record AllyRec(string Slot, string Name, string Why, string Alt);

    /// <summary>One role's build for a power: two 6-ability trays (2026 dual-tray revamp).</summary>
    public class RoleBuild
    {
        public BuildRole Role     { get; init; } = BuildRole.DPS;
        public string   RoleLabel { get; init; } = "DPS";
        public string   RoleIcon  { get; init; } = "🔥";
        public string   RoleHex   { get; init; } = "#F87171";
        public string   Mechanic  { get; init; } = "";   // tank/heal/troll mechanic note
        public string[] Bar1      { get; init; } = [];    // primary tray (6)
        public string[] Bar2      { get; init; } = [];    // secondary tray (6) — situational/iconic/augment
        public ArtifactRec[] Artifacts { get; init; } = [];  // 5 equipped artifact slots
        public string   StatPoints{ get; init; } = "";
        public string[] Mods      { get; init; } = [];
        public string   RotationGroup { get; init; } = "";
        public string   RotationSolo  { get; init; } = "";
        public string   Tips      { get; init; } = "";
        public string   Source    { get; init; } = "";
    }

    /// <summary>A power set with all of its role builds.</summary>
    public class PowerBuild
    {
        public string Power    { get; init; } = "";
        public string Icon     { get; init; } = "◆";
        public string ColorHex { get; init; } = "#00d4ff";
        public List<RoleBuild> Roles { get; init; } = new();

        public RoleBuild? Dps     => Roles.FirstOrDefault(r => r.Role == BuildRole.DPS);
        public RoleBuild? Support => Roles.FirstOrDefault(r => r.Role != BuildRole.DPS);
    }

    /// <summary>
    /// Current (2026, GU73 / dual ability-tray era) DCUO build library.
    /// DPS + support (Tank/Healer/Controller) for every power, two 6-ability trays each.
    /// Primary tray + stats/mods/rotation sourced from dcuobloguide power guides.
    /// Secondary tray = situational swaps, iconics and the White Lantern Power Array Augment.
    /// Verify in-game — balance shifts each episode.
    /// </summary>
    public static class BuildLibrary
    {
        // ── Role icons/colors ──
        private const string DpsIcon="🔥", DpsHex="#F87171";
        private const string TankIcon="🛡", TankHex="#60A5FA";
        private const string HealIcon="✚", HealHex="#34D399";
        private const string TrollIcon="〰", TrollHex="#FBBF24";

        // ── DPS (Might/Superpowered) shared loadout pieces ──
        private const string MightStats =
            "Focus: Superpowered (1) · Crit Attack Chance (20) · Crit Attack Damage (40) · Might & Power (175) · Health (rest)";
        private static readonly ArtifactRec[] MightArtifacts =
        [
            new("Transformation Card", "+20% crit chance & +30% crit damage on attacks — your single biggest damage multiplier.", "La-Mort Card · Solar Amplifier · Girdle of the Gods"),
            new("The Strategist Card", "Buffs Might & Precision when you weave a weapon attack into a power — top sustained DPS.", "Solar Amplifier · La-Mort Card · Trans-Volcanic Stone"),
            new("Grimorium Verum", "Void Gazer pet adds passive damage and sets the Penetrating Impact debuff for you.", "Orb of Arion · Tetrahedron of Urgrund · Quislet"),
            new("Eye of Gemini", "Extends your damage-buff window and builds supercharge fast for more bursts.", "Scrap of the Soul Cloak (don't run with Eye) · Source Shard · Cog of Mxyzptlk"),
            new("Source Shard", "5th-slot damage proc / extra burst.", "Quislet · La-Mort Card · Philosopher's Stone · Tetrahedron of Urgrund"),
        ];
        private static readonly string[] MightMods =
        [
            "Weapon: Blast Adapter (or Absorption Adapter)", "Neck: Escalating Might",
            "Back: Berserker (or Breakout Regeneration)", "Chest: Core Strength", "Hands: Max Damage",
            "Sockets: Red Might · Yellow Might&Power · Blue Might&Health",
        ];

        // ── Tank shared pieces ──
        private static readonly ArtifactRec[] TankArtifacts =
        [
            new("Everyman Prototype Suit", "Big defense + self-heal proc when you take damage — top raw survivability.", "Gauntlet of Gnomon · Mainframe Override Device"),
            new("Mystic Symbol of the Seven", "Shield + heal-over-time when you cast a shield — layered mitigation.", "Cog of Mxyzptlk · Philosopher's Stone"),
            new("Manacles of Force", "Cuts shield cooldowns 10% and resets your key tank shield (Density/Gemstone/Immolation/Redirected Rage/Winter Ward).", "Everyman Prototype Suit · Gauntlet of Gnomon"),
            new("Cog of Mxyzptlk", "Boosts the strength of your shields & self-heals — great for shield tanks (Ice/Earth/Atomic).", "Philosopher's Stone · Mystic Symbol of the Seven"),
            new("Amulet of Rao", "Aura lowers enemy attack & defense — protects the group and boosts threat.", "The Strategist Card (threat/damage) · Dilustel Refractor · Transformation Card"),
        ];
        private static readonly string[] TankMods =
        [
            "Weapon: Absorption Adapter", "Neck: Fortified Assault (or Fortified Blocking)",
            "Back: Breakout Protection", "Feet: Deadly Block", "Chest: Quick Healing",
            "Hands: Regenerative Shielding · Sockets: Dominance combos",
        ];

        // ── Healer shared pieces ──
        private const string HealStats =
            "Focus: Hybrid (1) · Crit Healing Chance (20) · Crit Healing Magnitude (40) · Restoration (175) · Might & Power (rest)";
        private static readonly ArtifactRec[] HealArtifacts =
        [
            new("Purple Healing Ray", "Fires a powerful burst group heal when you heal — huge for spike damage.", "Scrap of the Soul Cloak · Clarion"),
            new("Philosopher's Stone", "Heals and refunds power to you — keeps you topped and sustained.", "Amulet of Rao · Clarion · Mystic Symbol of the Seven"),
            new("Scrap of the Soul Cloak", "+27% max supercharge & generation, -22.5% SC cooldown — emergency group heals far more often.", "Eye of Gemini (don't run both) · Source Shard"),
            new("Clarion", "Buffs the group's stats and adds healing — strong group support.", "Tetrahedron of Urgrund · Dilustel Refractor"),
            new("Amulet of Rao", "Enemy attack/defense debuff aura — less incoming damage for the whole group.", "Dilustel Refractor · Strategist Card (battle heal)"),
        ];
        private static readonly string[] HealMods =
        [
            "Weapon: Restorative Adapter", "Neck: Fortified Restoration",
            "Back: Breakout Regeneration", "Feet: Explosive Block", "Chest: Quick Healing",
            "Hands: Regenerative Shielding · Sockets: Restoration combos",
        ];

        // ── Controller (troll) shared pieces ──
        private const string TrollStats =
            "Focus: Superpowered/Hybrid (1) · Crit Power Chance (20) · Crit Power Magnitude (10) · Might&Power (50%) · Vitalization (50%) · Dominance (5)";
        private static readonly ArtifactRec[] TrollArtifacts =
        [
            new("Quislet", "Passive power-over-time to the group — the core controller battery.", "Tetrahedron of Urgrund · Source Shard"),
            new("Tetrahedron of Urgrund", "Group Might buff for 12s after you cast a Might power — best swap-slot artifact in the game.", "Quislet · Dilustel Refractor"),
            new("Entwined Rings of Azar", "Amplifies health-buff artifacts (like Tetrahedron) off your Vitalization without inflating your Health.", "Dilustel Refractor · Clarion"),
            new("The Strategist Card", "Lets the controller still put out real damage while trolling.", "Eye of Gemini · Transformation Card · Grimorium Verum"),
            new("Scrap of the Soul Cloak", "More supercharge for your group buffs and shields.", "Cog of Mxyzptlk · Eye of Gemini · Source Shard"),
        ];

        // General Iconic powers — ALL unlocked with Skill Points in the Iconic tree (nobody starts
        // with them slotted). Best-in-slot first, each line gives extra alternatives to obtain.
        private static readonly IconicRec[] DpsIconics =
        [
            new("Heat Vision", "+40% damage until your hit counter resets — strongest universal DPS iconic.", "Neo-Venom Boost · Word of Power · Robot Sidekick"),
            new("Neo-Venom Boost", "Supercharge that spikes your Might & Precision for a burst window.", "Heat Vision · Word of Power"),
            new("Robot Sidekick", "Summons a pet for free, constant extra damage.", "Word of Power · Sonic Cry"),
            new("Word of Power", "Self/group Might buff — raises your damage output.", "Heat Vision · Neo-Venom Boost"),
            new("Sonic Cry", "Ranged AoE burst + crowd control filler.", "Freezing Breath · Batarang Multi-Shot · Sonic Shout"),
        ];
        private static readonly IconicRec[] TankIconics =
        [
            new("Robot Sidekick", "An extra body that shares aggro and chips in damage.", "Neo-Venom Boost · Word of Power"),
            new("Amazonium Deflection", "Deflects attacks, cuts damage and knocks enemies down — a panic button.", "Hard-Light Shield · Super-Strength"),
            new("Hard-Light Shield", "Extra on-demand shield to cover a spike.", "Amazonium Deflection · Super-Strength"),
            new("Neo-Venom Boost", "Stat burst to power through a dangerous window.", "Heat Vision · Word of Power"),
            new("Mesmerizing Lasso", "Single-target lockdown to peel a dangerous add.", "Sonic Cry · Gag Glove"),
        ];
        private static readonly IconicRec[] HealIconics =
        [
            new("Word of Power", "Group Might buff between heals — boosts the whole group's damage.", "Robot Sidekick · Neo-Venom Boost"),
            new("Robot Sidekick", "Light extra damage/utility on easy fights.", "Word of Power · Heat Vision (battle)"),
            new("Amazonium Deflection", "Personal panic defense when you get focused.", "Hard-Light Shield · Super-Strength"),
            new("Hard-Light Shield", "Extra self shield to survive a spike.", "Amazonium Deflection"),
        ];
        private static readonly IconicRec[] TrollIconics =
        [
            new("Word of Power", "Group Might buff — raises everyone's damage while you troll.", "Neo-Venom Boost · Robot Sidekick"),
            new("Robot Sidekick", "Adds damage while you handle power and debuffs.", "Heat Vision (battle) · Word of Power"),
            new("Sonic Cry", "AoE crowd control utility.", "Mesmerizing Lasso · Gag Glove · Sonic Shout"),
            new("Neo-Venom Boost", "Burst your own damage in a window.", "Word of Power · Heat Vision"),
        ];
        // Battle roles want damage iconics + one survival option.
        private static readonly IconicRec[] BattleIconics =
        [
            new("Heat Vision", "+40% damage — the battle-role staple to add real DPS while supporting.", "Neo-Venom Boost · Word of Power"),
            new("Neo-Venom Boost", "Supercharge stat burst for your damage windows.", "Heat Vision · Robot Sidekick"),
            new("Robot Sidekick", "Passive pet damage while you keep up your role duty.", "Word of Power · Pheromone Bloom"),
            new("Word of Power", "Might buff that helps both your damage and the group.", "Heat Vision · Neo-Venom Boost"),
            new("Amazonium Deflection", "Panic survival button so battling doesn't get you killed.", "Hard-Light Shield · Super-Strength"),
        ];

        public static IconicRec[] IconicsForRole(BuildRole r) => r switch
        {
            BuildRole.Tank             => TankIconics,
            BuildRole.Healer           => HealIconics,
            BuildRole.Controller       => TrollIconics,
            BuildRole.BattleTank       => BattleIconics,
            BuildRole.BattleHealer     => BattleIconics,
            BuildRole.BattleController  => BattleIconics,
            _                          => DpsIconics,
        };

        // Allies = 1 Combat + 2 Support. Legendary allies (Cyborg, Professor Zoom, The Flash — buffed
        // to Legendary, get a 2nd Support Ability) are the meta; synergy comes from picking passive
        // abilities that match your role + one that cuts your Combat Ally's cooldown.
        private static readonly AllyRec[] DpsAllies =
        [
            new("COMBAT","Starfire","Starbolt Flurry hits up to +359% at max affinity — the top DPS combat ally. Summon it inside your burst window.","Crispus Allen (Spectre) · Raven"),
            new("SUPPORT ×2","Legendary allies + damage passives","Run two Legendary allies and slot damage passives — one boosting Might, one cutting your Combat Ally's cooldown so Starfire is up more often.","Cyborg · Professor Zoom · The Flash"),
        ];
        private static readonly AllyRec[] TankAllies =
        [
            new("COMBAT","Starfire / a damage ally","A damage combat ally still helps hold aggro while you battle-tank — pop it on the pull.","Crispus Allen · Raven"),
            new("SUPPORT ×2","Legendary allies + survival passives","Two Legendary allies with passives that boost Dominance/Health (plus a combat-ally cooldown passive) to stay sturdy while you DPS.","Cyborg · The Flash"),
        ];
        private static readonly AllyRec[] HealAllies =
        [
            new("COMBAT","Starfire (battle heal) / utility ally","Battle healers run a damage combat ally for extra DPS; pure healers can take a utility ally instead.","Crispus Allen · a utility ally"),
            new("SUPPORT ×2","Legendary allies + Resto/utility passives","Two Legendary allies with passives boosting Restoration or reducing your combat-ally cooldown.","Cyborg · Professor Zoom"),
        ];
        private static readonly AllyRec[] TrollAllies =
        [
            new("COMBAT","Starfire / a damage ally","A damage combat ally adds output while you battery — summon between power dumps.","Crispus Allen · Raven"),
            new("SUPPORT ×2","Legendary allies + power/damage passives","Two Legendary allies; pick passives boosting Vitalization/power return plus a combat-ally cooldown passive.","Cyborg · The Flash"),
        ];

        public static AllyRec[] AlliesForRole(BuildRole r) => r switch
        {
            BuildRole.Tank             => TankAllies,
            BuildRole.Healer           => HealAllies,
            BuildRole.Controller       => TrollAllies,
            // Battle roles want damage — same as DPS allies (Starfire + damage passives)
            _                          => DpsAllies,
        };
        private static readonly string[] TrollMods =
        [
            "Weapon: Replenishing Adapter", "Neck: Escalating Replenishing Procs",
            "Back: Breakout Regeneration", "Feet: Explosive Block", "Chest: Reserve Tank",
            "Hands: Max Damage · Sockets: Vitalization & Power combos",
        ];

        private static RoleBuild Dps(string[] b1, string[] b2, string rotG, string rotS, string tips, string src) => new()
        {
            Role=BuildRole.DPS, RoleLabel="DPS", RoleIcon=DpsIcon, RoleHex=DpsHex,
            Bar1=b1, Bar2=b2, Artifacts=MightArtifacts, StatPoints=MightStats, Mods=MightMods,
            RotationGroup=rotG, RotationSolo=rotS, Tips=tips, Source=src,
        };

        // ── Battle (hybrid) role shared pieces ──
        private const string BattleTankStats =
            "Focus: Hybrid (1) · Dominance (~110, enough to hold aggro & survive) · Might & Power (~110) · Health (rest) · Crit Attack Chance (20) · Crit Attack Damage (20). More Dominance in elite, more Might in farm.";
        private const string BattleHealStats =
            "Focus: Hybrid (1) · Restoration (~110) · Might & Power (~120) · Crit Healing (10) · Crit Attack Chance (20) · Crit Attack Damage (20). More Resto for hard content, more Might for clears.";
        private const string BattleTrollStats =
            "Focus: Hybrid (1) · Vitalization (~90, keep power flowing) · Might & Power (~150) · Crit Power (10) · Crit Attack Chance (20) · Crit Attack Damage (10).";

        private static readonly ArtifactRec[] BattleTankArts =
        [
            new("Everyman Prototype Suit", "Keeps you alive while you swing for damage — the survival anchor.", "Gauntlet of Gnomon · Mystic Symbol of the Seven"),
            new("Transformation Card", "+20% crit chance & +30% crit damage — your damage backbone.", "La-Mort Card · Solar Amplifier"),
            new("The Strategist Card", "Might & Precision when weaving weapon + power — sustained battle damage.", "Eye of Gemini · Grimorium Verum"),
            new("Amulet of Rao", "Enemy attack/defense debuff aura — survival + threat + group buff in one.", "Manacles of Force · Cog of Mxyzptlk"),
            new("Mystic Symbol of the Seven", "Shield + HoT so a damage rotation doesn't get you killed.", "Cog of Mxyzptlk · Philosopher's Stone"),
        ];
        private static readonly ArtifactRec[] BattleHealArts =
        [
            new("Purple Healing Ray", "Burst group heal proc — keeps the group up while you DPS.", "Clarion · Scrap of the Soul Cloak"),
            new("Transformation Card", "+20% crit chance & +30% crit damage — your damage backbone.", "La-Mort Card · Solar Amplifier"),
            new("The Strategist Card", "Might & Precision when weaving — sustained battle damage.", "Eye of Gemini · Grimorium Verum"),
            new("Scrap of the Soul Cloak", "More supercharge = your group SC heal/shield up more often.", "Eye of Gemini · Source Shard"),
            new("Philosopher's Stone", "Heals + refunds power so you can keep casting damage.", "Amulet of Rao · Clarion"),
        ];
        private static readonly ArtifactRec[] BattleTrollArts =
        [
            new("Quislet", "Passive group power — you still battery while you DPS.", "Tetrahedron of Urgrund · Source Shard"),
            new("Tetrahedron of Urgrund", "Group Might buff after a Might power — buffs you AND the group.", "Quislet · Dilustel Refractor"),
            new("Transformation Card", "+20% crit chance & +30% crit damage — damage backbone.", "La-Mort Card · Solar Amplifier"),
            new("The Strategist Card", "Might & Precision when weaving — sustained battle damage.", "Eye of Gemini · Grimorium Verum"),
            new("Eye of Gemini", "Damage-buff window + supercharge generation.", "Scrap of the Soul Cloak · Source Shard"),
        ];

        private static readonly string[] BattleTankMods =
        [
            "Weapon: Absorption Adapter", "Neck: Escalating Might", "Back: Breakout Regeneration",
            "Feet: Deadly Block", "Chest: Core Strength", "Hands: Max Damage",
            "Sockets: Dominance&Might · Might&Power · Dominance&Health",
        ];
        private static readonly string[] BattleHealMods =
        [
            "Weapon: Restorative Adapter", "Neck: Escalating Might", "Back: Breakout Regeneration",
            "Chest: Core Strength", "Hands: Max Damage",
            "Sockets: Restoration&Might · Might&Power · Restoration&Health",
        ];
        private static readonly string[] BattleTrollMods =
        [
            "Weapon: Replenishing Adapter", "Neck: Escalating Might", "Back: Breakout Regeneration",
            "Chest: Core Strength", "Hands: Max Damage",
            "Sockets: Vitalization&Might · Might&Power · Vitalization&Power",
        ];

        private static RoleBuild BTank(string[] b1, string[] b2, string mech, string rotG, string tips, string src) => new()
        {
            Role=BuildRole.BattleTank, RoleLabel="BATTLE TANK", RoleIcon="🛡", RoleHex="#7FB3FF", Mechanic=mech,
            Bar1=b1, Bar2=b2, Artifacts=BattleTankArts, StatPoints=BattleTankStats, Mods=BattleTankMods,
            RotationGroup=rotG, Tips=tips, Source=src,
        };
        private static RoleBuild BHeal(string[] b1, string[] b2, string mech, string rotG, string tips, string src) => new()
        {
            Role=BuildRole.BattleHealer, RoleLabel="BATTLE HEALER", RoleIcon="✚", RoleHex="#5FE0A8", Mechanic=mech,
            Bar1=b1, Bar2=b2, Artifacts=BattleHealArts, StatPoints=BattleHealStats, Mods=BattleHealMods,
            RotationGroup=rotG, Tips=tips, Source=src,
        };
        private static RoleBuild BTroll(string[] b1, string[] b2, string mech, string rotG, string tips, string src) => new()
        {
            Role=BuildRole.BattleController, RoleLabel="BATTLE TROLL", RoleIcon="〰", RoleHex="#FFD24A", Mechanic=mech,
            Bar1=b1, Bar2=b2, Artifacts=BattleTrollArts, StatPoints=BattleTrollStats, Mods=BattleTrollMods,
            RotationGroup=rotG, Tips=tips, Source=src,
        };

        public static readonly IReadOnlyList<PowerBuild> All = new List<PowerBuild>
        {
            new() { Power="Fire", Icon="🔥", ColorHex="#FF6B35", Roles={
                Dps(["Flame Cascade","Fireburst","Spontaneous Combustion","Meteor Strike","Inferno","Volcanic Calamity (ST)"],
                    ["Fireball Barrage (AoE)","Mass Detonation","Overheat","Absorb Heat","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Inferno → Meteor Strike → Spontaneous Combustion → Fireburst → Flame Cascade",
                    "Meteor Strike → Spontaneous Combustion → Fireburst → Flame Cascade",
                    "Keep burn DoTs ticking before you burst. Volcanic Calamity for bosses, Fireball Barrage for groups. Don't clip Flame Cascade.",
                    "dcuobloguide Fire DPS"),
                new(){ Role=BuildRole.Tank, RoleLabel="TANK", RoleIcon=TankIcon, RoleHex=TankHex,
                    Mechanic="Fire Soul: +30% Defense (not blocking), rises to +50% as you damage Burning enemies.",
                    Bar1=["Engulf","Backdraft","Stoke Flames","Burnout","Immolation","Burning Determination"],
                    Bar2=["Fireball (ST taunt)","Absorb Heat","Eternal Flame","Robot Sidekick (iconic)","Hard Light Shield (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TankArtifacts, Mods=TankMods,
                    StatPoints="Focus: Hybrid (1) · Health (175) · Dominance (175) · Restoration (175) · Crit Heal Chance (20) · Crit Heal Mag (40)",
                    RotationGroup="Open with Engulf to grab aggro + apply Burning, keep Stoke Flames/Immolation up; Backdraft to reset when overwhelmed.",
                    RotationSolo="Engulf → Stoke Flames → Immolation, refresh Burning constantly to hold Fire Soul at +50%.",
                    Tips="You're a self-healing tank — staying ON Burning enemies IS your defense. Burnout breaks the group out of CC. Backdraft buys the healer time.",
                    Source="dcuobloguide Fire Tank (GU73)" } } },

            new() { Power="Rage", Icon="🩸", ColorHex="#C0392B", Roles={
                Dps(["Outrage","Revenge","Severe Punishment","Relentless Anger","Galling Eruption","Berserk (SC)"],
                    ["Frenetic Bombardment","Dreadful Blast","Plasma Retch","Mangle (SC)","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Severe Punishment → Relentless Anger → Galling Eruption → Outrage + Tap/Hold Melee → Revenge",
                    "Range: Outrage → Frenetic Bombardment → Dreadful Blast → Plasma Retch → Galling Eruption",
                    "Melee hits hardest. Push through Rage Crash, keep Relentless Anger up, weave melee combos after Severe Punishment.",
                    "dcuobloguide Rage DPS"),
                new(){ Role=BuildRole.Tank, RoleLabel="TANK", RoleIcon=TankIcon, RoleHex=TankHex,
                    Mechanic="Rage tanking lives and dies by managing Rage Crash (the penalty after Enrage).",
                    Bar1=["Ragebringer","Without Mercy","Eviscerating Chain","Ire","Remorseless Recovery","Severe Punishment"],
                    Bar2=["Rage Blast (ST taunt)","Redirected Rage (shield)","Outrage","Robot Sidekick (iconic)","Neo-Venom Boost (SC)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TankArtifacts, Mods=TankMods,
                    StatPoints="Focus: Hybrid (1) · Crit Attack Chance (10) · Crit Attack Damage (10) · Health & Dominance (split rest 50/50) · Restoration after",
                    RotationGroup="Ragebringer to drag/aggro → Without Mercy AoE knockdown → time Eviscerating Chain + Remorseless Recovery to negate Rage Crash.",
                    RotationSolo="Hold aggro with Ragebringer, keep healing from damage via Remorseless Recovery, manage crash windows.",
                    Tips="Eviscerating Chain + Remorseless Recovery are your crash mitigation — time them, don't waste them. Stay aggressive; your healing comes from dealing damage.",
                    Source="dcuobloguide Rage Tank" } } },

            new() { Power="Atomic", Icon="☢", ColorHex="#7CFC00", Roles={
                Dps(["Nuclear Burst","Neutron Bomb","Atom Splitter","Atom-Powered Assault","Geiger Beam","Neutrino Blast (SC)"],
                    ["Thermochemical Explosion","Electron Flare","Proton Remedy","Robot Sidekick (iconic)","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Neutron Bomb → Atom-Powered Assault → alternate Neutron Bomb & Nuclear Burst during Quark-Gluon Aura",
                    "Nuclear Burst → Atom-Powered Assault → Atom Splitter combos; keep Quark-Gluon Aura rolling",
                    "Everything keys off Quark-Gluon Aura uptime — use the held Aura combos to keep it active. That's your damage multiplier.",
                    "dcuobloguide Atomic DPS"),
                new(){ Role=BuildRole.Tank, RoleLabel="TANK", RoleIcon=TankIcon, RoleHex=TankHex,
                    Mechanic="Quark-Gluon Aura (after 2 Atomic Combos): +25% damage absorption (not blocking), attacks heal 2% max HP, +15% control resist.",
                    Bar1=["Atomic Reorganization","Atom Splitter","Thermochemical Explosion","Proton Remedy","Neutrino Blast","Density"],
                    Bar2=["Particle Beam (ST taunt)","Geiger Beam","Atom-Powered Assault","Robot Sidekick (iconic)","Hard Light Shield (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TankArtifacts, Mods=TankMods,
                    StatPoints="Focus: Hybrid (1) · Dominance (175) · Health (175) · Might & Power after · Crit Attack Chance/Damage (10 each)",
                    RotationGroup="Atom Splitter + Atomic Combo ×2 + Proton Remedy to build & hold the Aura, then maintain combos to keep absorption + self-heal up.",
                    RotationSolo="Keep the Aura up at all times via combos; Density for a hard shield when you take spikes.",
                    Tips="A battle-tank: you MUST keep doing combos to sustain Quark-Gluon Aura — that's your absorption AND self-heal. Drop the aura and you get squishy fast.",
                    Source="dcuobloguide Atomic Tank" } } },

            new() { Power="Earth", Icon="🪨", ColorHex="#B5651D", Roles={
                Dps(["Fortify Golem","Debris Field","Unstoppable","Jackhammer","Shatter","Cataclysm (SC)"],
                    ["Striking Stones","Upheaval","Localized Tremor","Gemstone Shield","Robot Sidekick (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Fortify Golem → Debris Field → Unstoppable → Jackhammer + Tap Melee (refresh Crush)",
                    "Fortify Golem → Debris Field → Unstoppable → Jackhammer melee combos",
                    "Maintain the Crush PI with Jackhammer combos; keep Fortify Golem's aura up. Weave Aftershock (held) combos for extra ticks.",
                    "dcuobloguide Earth DPS"),
                new(){ Role=BuildRole.Tank, RoleLabel="TANK", RoleIcon=TankIcon, RoleHex=TankHex,
                    Mechanic="Earthen Bond: Brick Golem soaks a portion of your incoming damage while you're not blocking.",
                    Bar1=["Earthen Grip","Epicenter","Gemstone Shield","Soothing Sands","Fortify Golem","Brick Golem"],
                    Bar2=["Jackhammer (battle-tank)","Debris Field","Totem","Robot Sidekick (iconic)","Hard Light Shield (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TankArtifacts, Mods=TankMods,
                    StatPoints="Focus: Hybrid (1) · Dominance (175) · Crit Attack Chance/Damage (10 each) · Restoration & Health (5 each after Dom)",
                    RotationGroup="Summon Brick first, keep Fortify Golem on it; Earthen Grip to drag/aggro, Epicenter to gather, Gemstone Shield (tap melee 3×) for absorption.",
                    RotationSolo="Keep Brick alive & fortified — it's your damage soak. Re-shield with Gemstone Shield constantly.",
                    Tips="Your pet is your survival — never let Brick die, keep Fortify Golem on it. Soothing Sands breaks the group out of CC. Gemstone Shield melee-taps boost absorption.",
                    Source="dcuobloguide Earth Tank" } } },

            new() { Power="Ice", Icon="❄", ColorHex="#5DADE2", Roles={
                Dps(["Snowball","Arctic Gust","Impaling Ice","Wintry Tempest","Frost Blast","Blizzard (SC)"],
                    ["Bitter Winds","Deep Freeze","Reflection","Ice Elemental (SC)","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Wintry Tempest → Impaling Ice → Frost Blast → Arctic Gust / Snowball",
                    "Wintry Tempest → Impaling Ice → Arctic Gust / Snowball",
                    "Wintry Tempest is your opener and biggest hit. Keep the cold PI from Impaling Ice up; Snowball/Arctic Gust fill between cooldowns.",
                    "dcuobloguide Ice DPS"),
                new(){ Role=BuildRole.Tank, RoleLabel="TANK", RoleIcon=TankIcon, RoleHex=TankHex,
                    Mechanic="Shield tank: +65% Defense (not blocking) in role, +35% more while Ice Armor/shields are active.",
                    Bar1=["Inescapable Storm","Frost Slam","Reflection","Winter Ward","Shatter Restraints","Hibernation"],
                    Bar2=["Frost Snipe (ST taunt)","Hard-Light Shield","Bitter Winds","Robot Sidekick (iconic)","Neo-Venom Boost (SC)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TankArtifacts,
                    Mods=["Weapon: Absorption Adapter","Neck: Fortified Assault","Back: Breakout Protection","Feet: Deadly Block","Chest: Quick Healing","Hands: Regenerative Shielding"],
                    StatPoints="Focus: Hybrid (1) · Dominance (175) · Restoration (175) · Health (175) · Crit Attack Chance/Damage (10 each)",
                    RotationGroup="Inescapable Storm to drag/aggro, then ROTATE shields — Reflection → Winter Ward → Hibernation — so one is always up. Shatter Restraints for group CC immunity.",
                    RotationSolo="Never let all shields drop at once; stagger them. Frost Slam for knockdown control.",
                    Tips="Pure shield tank: your job is shield uptime. Stagger Reflection/Winter Ward/Hibernation so you're never bare. Frost Snipe single-target taunts bosses.",
                    Source="dcuobloguide Ice Tank" } } },

            new() { Power="Electricity", Icon="⚡", ColorHex="#FFD24A", Roles={
                Dps(["Arc Lightning","Voltaic Blast","Tesla Ball","Electrocute","Electrogenesis","Circuit Breaker (SC)"],
                    ["Overcharge","Megavolt (SC)","Galvanize","Bio-Capacitor","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Arc Lightning → Tesla Ball → Electrocute → Voltaic Blast → Overcharge",
                    "Arc Lightning → Tesla Ball → Electrocute → Electrogenesis → Voltaic Blast",
                    "Keep the Electrified PI up (Arc Lightning). Voltaic Blast is your instant filler. Reapply DoTs before they fall off.",
                    "dcuobloguide Electricity DPS"),
                new(){ Role=BuildRole.Healer, RoleLabel="HEALER", RoleIcon=HealIcon, RoleHex=HealHex,
                    Mechanic="Burst healer — strong reactive heals; manage cooldowns so a big heal is always ready.",
                    Bar1=["Bioelectric Surge","Recover","Arc Lightning","Galvanize","Bio-Capacitor","Group Transducer"],
                    Bar2=["Electrogenesis","Flux","Wired","Robot Sidekick (iconic)","Bio-Electric Surge (SC)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=HealArtifacts,
                    Mods=["Weapon: Restorative Adapter","Neck: Focused Restoration","Back: Breakout Regeneration","Feet: Explosive Block","Chest: Quick Healing","Hands: Regenerative Shielding"],
                    StatPoints=HealStats,
                    RotationGroup="Bioelectric Surge + Recover as your core heals; hold Bio-Capacitor & Flux for emergency spikes; Group Transducer for power back to the group.",
                    RotationSolo="(Solo you'll DPS — see the DPS tab.) As healer, pre-cast before big hits land.",
                    Tips="Electric is a reactive burst healer. Don't dump every heal at once — stagger cooldowns and save Bio-Capacitor/Flux for emergencies.",
                    Source="dcuobloguide Electricity Healer" } } },

            new() { Power="Gadgets", Icon="⚙", ColorHex="#00E5A0", Roles={
                Dps(["Cryo-Field","Implosion Mine","Gauss Grenade","EMP Pulse","Suppressor Turret","Stasis Field (SC)"],
                    ["Vortex Cannon","Photon Blast","Napalm Grenade","Distract (stealth)","Robot Sidekick (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Cryo-Field → Implosion Mine → Gauss Grenade → EMP Pulse → Gauss Grenade → Implosion Mine",
                    "Stasis Field → EMP Pulse → Vortex Cannon → Gauss Grenade → Suppressor Turret",
                    "Keep enemies in the defensive-debuff PI. Loop Implosion Mine / Gauss Grenade. A Stealth (Distract) opener gives a big crit burst.",
                    "dcuobloguide Gadgets DPS"),
                new(){ Role=BuildRole.Controller, RoleLabel="TROLL", RoleIcon=TrollIcon, RoleHex=TrollHex,
                    Mechanic="Controller: feed power, debuff bosses (defense/damage/heal debuffs), keep the group running.",
                    Bar1=["Stasis Field","Napalm Grenade","Paralyzing Dart","Defibrillator","Distract","Reload"],
                    Bar2=["Gauss Grenade","Sleep Dart","Cryo-Foam","Robot Sidekick (iconic)","Group Shielding (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TrollArtifacts, Mods=TrollMods, StatPoints=TrollStats,
                    RotationGroup="Defense debuff (Paralyzing Dart) + healer debuff (Napalm) on bosses; Defibrillator/Reload to feed power & weapon buff; Stasis Field builds SC.",
                    RotationSolo="(Solo = DPS tab.) As troll, prioritize keeping the group's power bars topped.",
                    Tips="Your #1 job is power — keep Defibrillator/Reload on cooldown. Stack debuffs (Paralyzing Dart, Napalm) on bosses. Distract shields rezzes.",
                    Source="dcuobloguide Gadgets Controller" } } },

            new() { Power="Mental", Icon="🧠", ColorHex="#C084FC", Roles={
                Dps(["Mass Levitation","Pyrokinesis","Telekinetic Pull","Psychic Shock","Grandeur","Robot Sidekick"],
                    ["Psychic Prison","Terrorize","Menace (SC)","Mass Deception","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Mass Levitation → Pyrokinesis → Telekinetic Pull → Psychic Shock ×3 (repeat before effects expire)",
                    "Pyrokinesis → Telekinetic Pull → Psychic Shock; keep Daze/Terror up",
                    "Maximize Daze and Terror PIs — they multiply your damage. Psychic Shock spam carries; refresh the PI abilities before they drop.",
                    "dcuobloguide Mental DPS"),
                new(){ Role=BuildRole.Controller, RoleLabel="TROLL", RoleIcon=TrollIcon, RoleHex=TrollHex,
                    Mechanic="Controller: defense debuff + power feed; Mental has strong CC (encasements, stuns).",
                    Bar1=["Horrific Visage","Psychic Shock","Cryokinesis","Psychic Empowerment","Telekinetic Shield","Grandeur"],
                    Bar2=["Bolster","Thought Bubble","Terrorize","Robot Sidekick (iconic)","Group Shielding (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TrollArtifacts, Mods=TrollMods, StatPoints=TrollStats,
                    RotationGroup="Keep Psychic Shock's defense debuff on the boss; Psychic Empowerment for group power; Cryokinesis to mitigate boss damage; Horrific Visage builds SC + heal debuff.",
                    RotationSolo="(Solo = DPS tab.) As troll, defense debuff uptime + power feed first.",
                    Tips="Hold the defense debuff (Psychic Shock) on the boss and keep power flowing with Psychic Empowerment. Cryokinesis is a clutch boss-damage mitigation.",
                    Source="dcuobloguide Mental Controller" } } },

            new() { Power="Quantum", Icon="🌀", ColorHex="#4DD0E1", Roles={
                Dps(["Inspiral Waves","Gravity Bomb","Gravitonic Field","Singularity","Time Bomb","Tachyon Burst (SC)"],
                    ["Distortion Wave","Warp Barrage","Tachyon Blast","Time Shift","Robot Sidekick (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Time Bomb → Singularity → Gravitonic Field → Gravity Bomb → Inspiral Waves ×2",
                    "Time Bomb → Singularity → Gravitonic Field → weapon attacks",
                    "Time Bomb is a delayed nuke — open with it so it detonates after your combo. Keep the Gravitonic Field PI up.",
                    "dcuobloguide Quantum DPS"),
                new(){ Role=BuildRole.Controller, RoleLabel="TROLL", RoleIcon=TrollIcon, RoleHex=TrollHex,
                    Mechanic="Controller: strong debuffs + the unique Quantum Tunneling group shield/revive.",
                    Bar1=["Anomaly","Alcubierre Wave","Singularity","Temporal Extorsion","Quantum Tunneling","Time Bomb"],
                    Bar2=["Time Bubble","Warped Reality","Tachyon Burst","Robot Sidekick (iconic)","Group Shielding (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TrollArtifacts, Mods=TrollMods, StatPoints=TrollStats,
                    RotationGroup="Singularity (defense debuff) + Alcubierre Wave (damage debuff) on bosses; Temporal Extorsion for group power; Anomaly builds SC + heal debuff; Quantum Tunneling to shield/revive.",
                    RotationSolo="(Solo = DPS tab.) As troll, stack debuffs and feed power.",
                    Tips="Quantum Tunneling is a lifesaver — shield + pick people up. Keep Singularity's defense debuff up and power flowing with Temporal Extorsion.",
                    Source="dcuobloguide Quantum Controller" } } },

            new() { Power="Munitions", Icon="💥", ColorHex="#E67E22", Roles={
                Dps(["Shrapnel Grenade Launcher","Five-Barrel Minigun","Chain Gun","Smoke Grenade Launcher","Mini-Nuke","Neo-Venom Boost (SC)"],
                    ["Biggun (SC)","Stealth Bombing","Cluster Grenade","Fixed Flak Cannon","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Shrapnel Grenade Launcher → Smoke Grenade Launcher → Mini-Nuke → Five-Barrel Minigun → Chain Gun",
                    "Shrapnel Grenade Launcher → Smoke Grenade Launcher → Five-Barrel Minigun → Chain Gun",
                    "Keep the Burn from Shrapnel Grenade Launcher up at ALL times — it's the core of your damage. Neo-Venom Boost for single-target burst.",
                    "dcuobloguide Munitions DPS"),
                new(){ Role=BuildRole.Controller, RoleLabel="TROLL", RoleIcon=TrollIcon, RoleHex=TrollHex,
                    Mechanic="Controller: debuffs + power feed via Reload; sturdy ranged kit.",
                    Bar1=["Shrapnel Grenade Launcher","Multi-Net Launcher","Smoke Grenade Launcher","Reload (power)","Survival","Reload (weapon buff)"],
                    Bar2=["Gauss Cannon","Suppressing Fire","Mini-Nuke","Robot Sidekick (iconic)","Group Shielding (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TrollArtifacts, Mods=TrollMods, StatPoints=TrollStats,
                    RotationGroup="Smoke Grenade Launcher (defense debuff) on bosses; Reload for group power + weapon buff; Multi-Net for CC; keep debuffs rolling.",
                    RotationSolo="(Solo = DPS tab.) As troll, prioritize power feed (Reload) and debuff uptime.",
                    Tips="Reload does double duty (power + weapon buff) — keep it cycling. Smoke Grenade is your defense debuff. Multi-Net locks down adds.",
                    Source="dcuobloguide Munitions Controller" } } },

            new() { Power="Celestial", Icon="✨", ColorHex="#FBBF24", Roles={
                Dps(["Sacred Light","Plague","Blight","Divine Light","Admonish","Wither (SC)"],
                    ["Retribution","Smite","Cursed Idol","Consume Soul","Heat Vision (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Sacred Light → Plague (+combo) → Divine Light → Blight (+combo)",
                    "Sacred Light → Plague (+combo) → weapon attacks",
                    "Combo-chained: Plague & Blight need the Purify effect (from Sacred/Divine Light) for bonus damage. Set up Purify first, then combo the curse.",
                    "dcuobloguide Celestial DPS"),
                new(){ Role=BuildRole.Healer, RoleLabel="HEALER", RoleIcon=HealIcon, RoleHex=HealHex,
                    Mechanic="Combo healer: chain blessing combos; keep HoTs (Consume Soul, Blight Cleansed) always rolling.",
                    Bar1=["Renew","Admonish","Consume Soul","Virtuous Light","Blessing","Guardian's Light"],
                    Bar2=["Benediction","Malediction","Cleansed combo","Robot Sidekick (iconic)","Plague (Cleansed)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=HealArtifacts, Mods=HealMods, StatPoints=HealStats,
                    RotationGroup="Renew + Admonish core heals; keep Consume Soul & Blight(Cleansed) HoTs up; Virtuous Light glyphs for sustained; save Blessing + Guardian's Light for emergencies.",
                    RotationSolo="(Solo = DPS tab.) As healer, maintain HoTs and combo into bigger heals.",
                    Tips="Celestial healing is combo-driven — chain into your Cleansed heals. Keep two HoTs ticking at all times and bank Blessing/Guardian's Light for spikes.",
                    Source="dcuobloguide Celestial Healer" } } },

            new() { Power="Sorcery", Icon="🔮", ColorHex="#9B59B6", Roles={
                Dps(["Soul Bolt","Soul Barrage","Shard of Life","Karmic Suspension","Circle of Destruction","Arbiter of Destiny (SC)"],
                    ["Soul Storm","Vengeance","Final Ruin","Weapon of Destiny","Summon Fury (pet)","White Lantern Augment / Iconic (see below)"],
                    "Circle of Destruction → Karmic Suspension → Shard of Life → Soul Barrage → Soul Bolt",
                    "Karmic Suspension → Soul Barrage → Soul Bolt + weapon during cooldowns",
                    "Keep your pet (Fury/Guardian) summoned — free sustained damage. Circle of Destruction sets the PI. Arbiter form is a strong burst SC.",
                    "dcuobloguide Sorcery DPS"),
                new(){ Role=BuildRole.Healer, RoleLabel="HEALER", RoleIcon=HealIcon, RoleHex=HealHex,
                    Mechanic="Pet healer option: Watcher boosts output but reduces your passive power regen.",
                    Bar1=["Rejuvenate","Ritualistic Word","Circle of Protection","Boon of Souls","Invocation of Renewal","Transcendence"],
                    Bar2=["Soul Well","Offering","Watcher (pet)","Robot Sidekick (iconic)","Word of Power","White Lantern Augment / Iconic (see below)"],
                    Artifacts=HealArtifacts, Mods=HealMods, StatPoints=HealStats,
                    RotationGroup="Rejuvenate + Ritualistic Word as core heals; Circle of Protection HoT zone; Boon of Souls/Transcendence shields for emergencies; Invocation for max single heal (don't get interrupted).",
                    RotationSolo="(Solo = DPS tab.) As healer, keep Circle of Protection down and stagger your heals.",
                    Tips="Watcher pet adds healing but costs power regen — run it only if your controller is solid. Invocation of Renewal is your biggest heal but is interruptible; cast it safe.",
                    Source="dcuobloguide Sorcery Healer" } } },

            new() { Power="Nature", Icon="🌿", ColorHex="#58D68D", Roles={
                Dps(["Serpent Call","Briar","Vine Lash","Harvest","Roar","Gorilla Form (SC)"],
                    ["Carnivorous Plants","Swarm","Primal Wolf Form","Pheromone Bloom","Robot Sidekick (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Serpent Call → Briar → Vine Lash (stack Poison) → Harvest (spread) → Roar (detonate)",
                    "Stack poison (Serpent Call/Briar/Vine Lash) → Roar to consume for burst",
                    "DoT-detonate power: stack as much Poison as possible, then Roar consumes it for a big hit. Harvest spreads effects before you detonate.",
                    "dcuobloguide Nature DPS"),
                new(){ Role=BuildRole.Healer, RoleLabel="HEALER", RoleIcon=HealIcon, RoleHex=HealHex,
                    Mechanic="HoT healer: keep Pheromones up (refresh via Flourish) to amplify your heals.",
                    Bar1=["Blossom","Metabolism","Cross Pollination","Flourish","Swarm Shield","Regeneration"],
                    Bar2=["Bloom","Harvest (heal)","Savage Growth","Robot Sidekick (iconic)","Hive Mind","White Lantern Augment / Iconic (see below)"],
                    Artifacts=HealArtifacts,
                    Mods=["Weapon: Restorative Adapter","Neck: Fortified Restoration","Back: Breakout Regeneration","Feet: Explosive Block","Chest: Quick Healing","Hands: Regenerative Shielding"],
                    StatPoints=HealStats,
                    RotationGroup="Blossom + Metabolism core heals; Cross Pollination wave heal + DoT; refresh Flourish to keep Pheromones up (it boosts healing); Swarm Shield + Regeneration for spikes.",
                    RotationSolo="(Solo = DPS tab.) As healer, never dump all heals at once — master the cooldowns.",
                    Tips="Keep Pheromones active (refresh with Flourish) — it amplifies everything. Stagger heal cooldowns; Nature punishes panic-casting.",
                    Source="dcuobloguide Nature Healer" } } },

            new() { Power="Water", Icon="🌊", ColorHex="#2E86DE", Roles={
                Dps(["Drown","High Tide","Tsunami Strikes","Ebb","Shark Frenzy (ST)","Whirlpool (SC)"],
                    ["Aqualance (AoE)","Call of the Deep","Riptide","Geyser","Robot Sidekick (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Drown → High Tide → Shark Frenzy → Tsunami Strikes (+combo) → Ebb → Tsunami Strikes",
                    "Drown → High Tide → Aqualance → Tsunami Strikes combos → Ebb",
                    "High Tide is a damage buff — keep it active. Drown applies the crusher PI. Shark Frenzy for ST boss crit, Aqualance for AoE DoT.",
                    "dcuobloguide Water DPS"),
                new(){ Role=BuildRole.Healer, RoleLabel="HEALER", RoleIcon=HealIcon, RoleHex=HealHex,
                    Mechanic="High Tide powers your healing — battle-healer build heals while doing light damage.",
                    Bar1=["High Tide","Tsunami Strikes","Mending Wave","Blessing of the Depths","Tempest Guard","Solace of the Sea"],
                    Bar2=["Tranquil Pool (SC)","Soothing Mist","Bubble","Robot Sidekick (iconic)","Cleansing Torrent","White Lantern Augment / Iconic (see below)"],
                    Artifacts=HealArtifacts,
                    Mods=["Weapon: Restorative Adapter","Neck: Fortified Restoration","Back: Breakout Regeneration","Feet: Explosive Block","Chest: Quick Healing","Hands: Regenerative Shielding"],
                    StatPoints=HealStats,
                    RotationGroup="Battle-healer: High Tide up → Tsunami Strikes (heals + cuts High Tide cooldown) → Mending Wave + Blessing of the Depths HoT; Tempest Guard shields 7; Solace for area HoT.",
                    RotationSolo="(Solo = DPS tab.) As healer, keep High Tide active — it enhances every heal.",
                    Tips="Water's gimmick is High Tide — keep it up and your heals get much stronger. Tsunami Strikes both heals AND resets High Tide; use it on cooldown.",
                    Source="dcuobloguide Water Healer" } } },

            new() { Power="Light", Icon="💡", ColorHex="#2ECC71", Roles={
                Dps(["Light Weight Strike","Impact","Chompers","Ram","Hand Clap","Entrap (SC)"],
                    ["Fan","Snap Trap","Light Claws","Inspiration (SC)","Robot Sidekick (iconic)","White Lantern Augment / Iconic (see below)"],
                    "Light Weight Strike / Chompers (daze) → Ram + Construct Combos → Impact + Construct Combos",
                    "Chompers → Ram → construct combos (chain without clipping)",
                    "Hard Light rewards CHAINED construct combos (the Light Claws bonus ramps as you chain). Never clip a combo. Open with a daze for the PI.",
                    "dcuobloguide Light DPS"),
                new(){ Role=BuildRole.Controller, RoleLabel="TROLL", RoleIcon=TrollIcon, RoleHex=TrollHex,
                    Mechanic="Controller: debuff bosses + feed power; Light has strong encasement CC.",
                    Bar1=["Light Weight Strike","Entrap","Light Blast","Recharge","Light Barrier","Boxing"],
                    Bar2=["Hand Clap","Snap Trap","Group Transducer","Robot Sidekick (iconic)","Group Shielding (iconic)","White Lantern Augment / Iconic (see below)"],
                    Artifacts=TrollArtifacts, Mods=TrollMods, StatPoints=TrollStats,
                    RotationGroup="Light Weight Strike (damage debuff protect) + Entrap (healer debuff) + Light Blast (defense debuff) on bosses; Recharge to feed group power.",
                    RotationSolo="(Solo = DPS tab.) As troll, hold debuffs and keep power flowing.",
                    Tips="Stack your debuffs (Light Weight Strike, Entrap, Light Blast) on the boss and keep power up with Recharge. Light Barrier shields rezzes.",
                    Source="dcuobloguide Light Controller" } } },
        };

        // Append the Battle (hybrid) role to every power once, centrally.
        static BuildLibrary()
        {
            foreach (var p in All)
            {
                var battle = MakeBattleRole(p.Power);
                if (battle != null) p.Roles.Add(battle);
            }
        }

        private const string Aug = "White Lantern Augment / Iconic (see below)";
        private const string HV  = "Heat Vision (iconic)";

        private static RoleBuild? MakeBattleRole(string power) => power switch
        {
            // ── BATTLE TANKS ──
            "Fire" => BTank(
                ["Engulf","Stoke Flames","Fireburst","Spontaneous Combustion","Inferno","Meteor Strike"],
                ["Backdraft","Immolation","Flame Cascade","Volcanic Calamity (SC)",HV,Aug],
                "Fire Soul rewards you for damaging Burning enemies (+50% Def) — so dealing damage literally IS your defense. The premier battle tank.",
                "Engulf to aggro → Stoke Flames (heal aura) → weave Fireburst / Spontaneous Combustion / Meteor Strike, keep everything Burning.",
                "Best battle tank in the game. Keep Burning on all targets, Backdraft/Stoke self-heal you, and your damage holds aggro for free.",
                "Battle-tank framework + Fire Tank/DPS data"),
            "Rage" => BTank(
                ["Ragebringer","Remorseless Recovery","Outrage","Severe Punishment","Relentless Anger","Galling Eruption"],
                ["Eviscerating Chain","Without Mercy","Revenge","Berserk (SC)",HV,Aug],
                "Lifesteal (Remorseless Recovery) scales with how hard you hit — Rage heals MORE the more damage you do.",
                "Ragebringer to aggro → keep Relentless Anger → melee combos (Outrage/Severe Punishment) → time Eviscerating Chain for Rage Crash.",
                "Top-tier battle tank: go aggressive, your self-heal grows with your damage. Just never miss your crash mitigation window.",
                "Battle-tank framework + Rage Tank/DPS data"),
            "Atomic" => BTank(
                ["Atomic Reorganization","Atom Splitter","Thermochemical Explosion","Atom-Powered Assault","Proton Remedy","Density"],
                ["Nuclear Burst","Neutron Bomb","Geiger Beam","Neutrino Blast (SC)",HV,Aug],
                "Quark-Gluon Aura (from Atomic Combos) gives damage absorption + self-heal (40% of Dominance) WHILE you combo — built for battle tanking.",
                "Atom Splitter + combo ×2 to light the Aura + Proton Remedy → then Nuclear Burst/Neutron Bomb AoE while keeping the Aura up.",
                "Excellent battle tank — the aura heals+absorbs as you deal big AoE. Drop the combo cadence and you lose both.",
                "Battle-tank framework + Atomic Tank/DPS data"),
            "Ice" => BTank(
                ["Inescapable Storm","Winter Ward","Wintry Tempest","Impaling Ice","Arctic Gust","Snowball"],
                ["Reflection","Hibernation","Frost Blast","Blizzard (SC)",HV,Aug],
                "Shield tank: keep one shield (Reflection/Winter Ward) up for the +35% Defense, then fill with Ice damage.",
                "Inescapable Storm to aggro → Winter Ward → Wintry Tempest → Impaling Ice → Arctic Gust/Snowball; stagger shields so one's always up.",
                "Safe battle tank — always have a shield up, then DPS the rest. Lower damage ceiling than Fire/Rage but very forgiving.",
                "Battle-tank framework + Ice Tank/DPS data"),
            "Earth" => BTank(
                ["Earthen Grip","Fortify Golem","Jackhammer","Debris Field","Unstoppable","Brick Golem"],
                ["Gemstone Shield","Shatter","Striking Stones","Cataclysm (SC)",HV,Aug],
                "Brick pet soaks your incoming damage (Earthen Bond) while you Jackhammer-combo — keep the pet alive and shielded.",
                "Summon + Fortify Brick → Earthen Grip to aggro → Gemstone Shield → Jackhammer + aftershocks for damage.",
                "Battle tank leans on Brick to tank for you while you melee. High skill: if the pet dies you lose your soak.",
                "Battle-tank framework + Earth Tank/DPS data"),

            // ── BATTLE HEALERS ──
            "Celestial" => BHeal(
                ["Renew","Blessing","Sacred Light","Plague","Divine Light","Admonish"],
                ["Consume Soul","Blight","Wither","Guardian's Light (SC)",HV,Aug],
                "THE archetype battle healer: combo your curses (Plague/Blight via Purify) for big damage while Renew/Admonish + Consume Soul HoT keep the group up.",
                "Set Purify (Sacred/Divine Light) → combo Plague/Blight for damage → top with Renew/Admonish, keep Consume Soul ticking.",
                "Strongest battle healer — you do near-DPS damage while healing. Keep two HoTs up and bank Guardian's Light for spikes.",
                "Battle-heal framework + Celestial Heal/DPS data"),
            "Water" => BHeal(
                ["High Tide","Tsunami Strikes","Mending Wave","Solace of the Sea","Tempest Guard","Ebb"],
                ["Drown","Shark Frenzy","Blessing of the Depths","Whirlpool (SC)",HV,Aug],
                "High Tide powers BOTH your heals and your damage — Water is a top battle healer.",
                "High Tide up → Tsunami Strikes (heals + cuts cooldown + damage) → Mending Wave/Solace HoT → Drown/Shark Frenzy for damage.",
                "Top battle healer: keep High Tide active and Tsunami Strikes on cooldown — it heals AND damages. Tempest Guard shields the group.",
                "Battle-heal framework + Water Heal/DPS data"),
            "Electricity" => BHeal(
                ["Bioelectric Surge","Group Transducer","Arc Lightning","Electrocute","Tesla Ball","Voltaic Blast"],
                ["Recover","Bio-Capacitor","Electrogenesis","Circuit Breaker (SC)",HV,Aug],
                "Electric heals are reactive bursts — slot damage abilities and DPS between heal windows; you also have strong electric damage.",
                "Keep Electrified PI (Arc Lightning) for damage → heal reactively with Bioelectric Surge/Recover → Group Transducer for power; bank Bio-Capacitor.",
                "Good battle healer — your damage rotation and heals share electric abilities. Don't dump all heals; save Bio-Capacitor for spikes.",
                "Battle-heal framework + Electricity Heal/DPS data"),
            "Nature" => BHeal(
                ["Blossom","Swarm Shield","Briar","Vine Lash","Harvest","Metabolism"],
                ["Cross Pollination","Flourish","Roar","Gorilla Form (SC)",HV,Aug],
                "Poison damage ticks while your HoTs heal — keep Pheromones up (Flourish) for healing, stack poison for damage.",
                "Stack poison (Briar/Vine Lash) → Harvest to spread → Roar to detonate for burst → keep Blossom/Metabolism + Pheromones for the group.",
                "Solid battle healer: your DoTs do real damage while HoTs roll. Refresh Flourish so Pheromones keeps amplifying heals.",
                "Battle-heal framework + Nature Heal/DPS data"),
            "Sorcery" => BHeal(
                ["Rejuvenate","Transcendence","Soul Bolt","Soul Barrage","Shard of Life","Circle of Protection"],
                ["Boon of Souls","Invocation of Renewal","Summon Fury (pet)","Arbiter of Destiny (SC)",HV,Aug],
                "Your pet (Fury) deals damage while you heal — Sorcery battle-heals well with the pet always out.",
                "Keep Fury summoned → Circle of Protection HoT → Rejuvenate priority heals → Soul Bolt/Barrage for your own damage.",
                "Good battle healer thanks to the pet. Keep Fury alive, stagger heals, and add Soul damage between casts.",
                "Battle-heal framework + Sorcery Heal/DPS data"),

            // ── BATTLE TROLLS ──
            "Gadgets" => BTroll(
                ["Defibrillator","Stasis Field","Gauss Grenade","EMP Pulse","Implosion Mine","Cryo-Field"],
                ["Paralyzing Dart","Reload","Suppressor Turret","Distract",HV,Aug],
                "Keep the group's power up (Defibrillator) + a defense debuff, then DPS with Gadgets' strong PIs and a stealth burst.",
                "Defibrillator for power → Stasis Field (debuff + builds SC) → loop Gauss Grenade/Implosion Mine/EMP for damage → Paralyzing Dart on bosses.",
                "Battle troll: never let the group go dry (Defibrillator/Reload), hold the defense debuff, then pump Gadgets damage. Watch your own power.",
                "Battle-troll framework + Gadgets Troll/DPS data"),
            "Mental" => BTroll(
                ["Psychic Empowerment","Psychic Shock","Pyrokinesis","Telekinetic Pull","Mass Levitation","Grandeur"],
                ["Horrific Visage","Cryokinesis","Terrorize","Menace (SC)",HV,Aug],
                "Best power efficiency of the trolls — feed power + hold the defense debuff, then AoE with Mental's Daze/Terror damage.",
                "Psychic Empowerment for power → Psychic Shock (defense debuff + damage) → Pyrokinesis/Telekinetic Pull/Mass Levitation for AoE.",
                "Top battle troll: great power efficiency lets you DPS more. Keep the defense debuff up and AoE hard.",
                "Battle-troll framework + Mental Troll/DPS data"),
            "Quantum" => BTroll(
                ["Temporal Extorsion","Singularity","Time Bomb","Gravity Bomb","Gravitonic Field","Tachyon Blast"],
                ["Anomaly","Alcubierre Wave","Quantum Tunneling","Tachyon Burst (SC)",HV,Aug],
                "Feed power (Temporal Extorsion) + Singularity defense debuff, then Time-Bomb-led damage. Quantum Tunneling is a clutch group save.",
                "Temporal Extorsion for power → Singularity (debuff) → Time Bomb opener → Gravity Bomb/Gravitonic Field → Tachyon Blast.",
                "Battle troll with the best utility — keep power + debuff up, lead damage with Time Bomb, and Quantum Tunneling saves wipes.",
                "Battle-troll framework + Quantum Troll/DPS data"),
            "Munitions" => BTroll(
                ["Reload","Smoke Grenade Launcher","Shrapnel Grenade Launcher","Five-Barrel Minigun","Chain Gun","Mini-Nuke"],
                ["Multi-Net Launcher","Survival","Biggun (SC)","Neo-Venom Boost (SC)",HV,Aug],
                "Reload feeds power + weapon buff; Smoke gives the defense debuff. Keep the Shrapnel Burn rolling for steady damage.",
                "Reload (power+weapon buff) → Smoke Grenade Launcher (debuff) → keep Shrapnel Burn up → Five-Barrel/Chain Gun/Mini-Nuke for damage.",
                "Battle troll: Reload does double duty, Smoke is your debuff, and the Burn keeps damage flowing while you battery.",
                "Battle-troll framework + Munitions Troll/DPS data"),
            "Light" => BTroll(
                ["Recharge","Light Blast","Light Weight Strike","Ram","Impact","Chompers"],
                ["Entrap","Group Transducer","Hand Clap","Inspiration (SC)",HV,Aug],
                "Keep power (Recharge) + debuffs up, then chain construct combos for damage — Light battle-trolls well with its combo damage.",
                "Recharge for power → Light Blast (defense debuff) → daze (Light Weight Strike/Chompers) → Ram/Impact construct combos for damage.",
                "Battle troll: power + debuffs first, then never clip your construct combos — that's where the damage is.",
                "Battle-troll framework + Light Troll/DPS data"),
            _ => null,
        };
    }
}
