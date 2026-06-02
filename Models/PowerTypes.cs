namespace DCUOTracker.Models
{
    public enum DcuoRole   { Unknown, DPS, Tank, Healer, Controller }

    public enum DcuoPower
    {
        Unknown,
        Fire, Ice, Earth, Rage, Atomic,          // Tank powers
        Gadgets, Mental, Light, Quantum, Munitions, // Controller powers
        Sorcery, Nature, Electricity, Celestial, Water // Healer powers
    }

    public static class PowerDetector
    {
        // Role icons — matching DCUO party frame indicators exactly
        // DPS=fire icon, Tank=shield, Healer=plus sign, Controller=squiggly
        public static string RoleIcon(DcuoRole r) => r switch
        {
            DcuoRole.DPS        => "🔥",
            DcuoRole.Tank       => "🛡",
            DcuoRole.Healer     => "✚",
            DcuoRole.Controller => "〰",
            _                   => "◆"
        };

        // Power type icon (emoji)
        public static string PowerIcon(DcuoPower p) => p switch
        {
            DcuoPower.Fire        => "🔥",
            DcuoPower.Ice         => "❄",
            DcuoPower.Earth       => "🪨",
            DcuoPower.Rage        => "💢",
            DcuoPower.Atomic      => "⚛",
            DcuoPower.Gadgets     => "⚙",
            DcuoPower.Mental      => "🔮",
            DcuoPower.Light       => "💡",
            DcuoPower.Quantum     => "🌀",
            DcuoPower.Munitions   => "🔫",
            DcuoPower.Sorcery     => "✨",
            DcuoPower.Nature      => "🌿",
            DcuoPower.Electricity => "⚡",
            DcuoPower.Celestial   => "☀",
            DcuoPower.Water       => "🌊",
            _                     => "◆"
        };

        // Power type color (hex)
        public static string PowerColor(DcuoPower p) => p switch
        {
            DcuoPower.Fire        => "#f97316",
            DcuoPower.Ice         => "#7dd3fc",
            DcuoPower.Earth       => "#a16207",
            DcuoPower.Rage        => "#dc2626",
            DcuoPower.Atomic      => "#fb923c",
            DcuoPower.Gadgets     => "#94a3b8",
            DcuoPower.Mental      => "#e879f9",
            DcuoPower.Light       => "#a3e635",
            DcuoPower.Quantum     => "#a78bfa",
            DcuoPower.Munitions   => "#6b7280",
            DcuoPower.Sorcery     => "#c084fc",
            DcuoPower.Nature      => "#4ade80",
            DcuoPower.Electricity => "#fbbf24",
            DcuoPower.Celestial   => "#fcd34d",
            DcuoPower.Water       => "#22d3ee",
            _                     => "#00d4ff"
        };

        // Ability keyword → power detection table (from research)
        private static readonly (DcuoPower p, string[] kw)[] _table =
        [
            (DcuoPower.Fire,        ["Flame Cascade","Fireburst","Spontaneous Combustion","Meteor Strike","Mass Detonation","Inferno","Volcanic","Fireball Barrage","Absorb Heat","Backdraft","Immolation","Burnout","Stoke Flames","Burning Determination","Engulf","Overheat"]),
            (DcuoPower.Ice,         ["Snowball","Arctic Gust","Impaling Ice","Wintry Tempest","Frost Blast","Blizzard","Ice Elemental","Bitter Winds","Deep Freeze","Reflection","Shatter Restraints","Winter Ward","Inescapable Storm","Ice Form","Ice Bash","Cold Snap","Crystallize","Frozen Bastion"]),
            (DcuoPower.Earth,       ["Fortify Golem","Debris Field","Unstoppable","Jackhammer","Striking Stones","Upheaval","Earthen Grip","Epicenter","Localized Tremor","Geokinesis","Seismic","Granite","Rock","Boulder","Tremor"]),
            (DcuoPower.Rage,        ["Relentless Anger","Outrage","Revenge","Severe Punishment","Galling Eruption","Plasma Retch","Dreadful Blast","Frenetic Bombardment","Berserk","Mangle","Rage","Violent","Frenzy","Intimidation","Battering Ram","Cower"]),
            (DcuoPower.Atomic,      ["Nuclear Burst","Atom-Powered Assault","Atom Splitter","Neutron Bomb","Electron Flare","Proton Remedy","Neutrino Blast","Atomic","Nuclear","Fission","Fusion","Ionic","Quark","Radioactive"]),
            (DcuoPower.Gadgets,     ["Cryo-Foam","Sticky Bomb","Paralyzing Dart","Battle Awareness","Sonic Cry","Photon Blast","Gauss Grenade","Neural Neutralizer","Taser Pull","Thermite","Suppressor","Turret","Device","Drone","Mine","Orbital","Napalm"]),
            (DcuoPower.Mental,      ["Mass Levitation","Pyrokinesis","Telekinetic Pull","Psychic Shock","Ambush: Sleep","Robot Sidekick","Psychic Prison","Terrorize","Mass Deception","Telekinesis","Illusion","Phantom","Deceptive","Menacing","Thought"]),
            (DcuoPower.Light,       ["Ram","Impact","Light Weight Strike","Chompers","Fan","Snap Trap","Light Claws","Light Blast","Minigun","Chainsaw","Boxing","Light Claw","Hard Light","Construct","Ring","Crystalline","Emerald","Dazzle"]),
            (DcuoPower.Quantum,     ["Time Bomb","Singularity","Gravitonic Field","Gravity Bomb","Inspiral Waves","Time Shift","Distortion Wave","Warp Barrage","Tachyon Blast","Quantum","Temporal","Warp","Event Horizon","Phase Shift","Distortion"]),
            (DcuoPower.Munitions,   ["Shrapnel Grenade","Five-Barrel Minigun","Chain Gun","Mini-Nuke","Neo-Venom Boost","Biggun","Smoke Grenade","Assault","Mortar","Missile","Sniper","Rifle","Rocket","Explosive","Concussion","Grenade"]),
            (DcuoPower.Sorcery,     ["Soul Well","Soul Barrage","Shard of Life","Karmic Suspension","Circle of Destruction","Arbiter","Baleful","Soul Bolt","Transmutation","Summon Fury","Summon Watcher","Offering","Boon","Wield Soul","Blight","Ward","Glyph","Ritual","Wrath","Totem"]),
            (DcuoPower.Nature,      ["Serpent Call","Briar","Vine Lash","Harvest","Roar","Gorilla Form","Carnivorous Plants","Swarm","Hive Mind","Blossom","Regeneration","Flora","Bestial","Wolf","Savage","Nature's"]),
            (DcuoPower.Electricity, ["Arc Lightning","Voltaic Blast","Tesla Ball","Electrocute","Electrogenesis","Overcharge","Circuit Breaker","Megavolt","Galvanize","Voltaic Bolt","Bio-Electric","Static","Lightning","Surge","Chain Lightning"]),
            (DcuoPower.Celestial,   ["Sacred Light","Plague","Blight","Divine Light","Admonish","Retribution","Smite","Wrath of the Presence","Cursed Idol","Consume Soul","Curse","Death Mark","Haunt","Malediction","Wither","Cleansed","Blessed","Empyrean","Sunstone","Holy"]),
            (DcuoPower.Water,       ["Drown","High Tide","Tsunami Strikes","Ebb","Aqualance","Shark Frenzy","Call of the Deep","Whirlpool","Riptide","Geyser","Tidal Wave","Aqua","Torrent","Surge Tide","Flow","Current"]),
        ];

        public static DcuoPower DetectPower(IEnumerable<string> abilityNames)
        {
            var counts = new Dictionary<DcuoPower, int>();
            foreach (var ab in abilityNames)
                foreach (var (p, kw) in _table)
                    if (kw.Any(k => ab.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        counts[p] = counts.GetValueOrDefault(p) + 1;

            return counts.Count > 0
                ? counts.OrderByDescending(x => x.Value).First().Key
                : DcuoPower.Unknown;
        }

        public static string ClassifyAbility(string name)
        {
            string lo = name.ToLowerInvariant();
            if (lo is "melee attack" or "ranged attack" or "weapon attack" or "block counter" or "lunge"
                || lo.Contains("weapon combo") || lo.Contains("attack effect") || lo.Contains("combo"))
                return "Weapon";
            if (lo.Contains("supercharge") || lo.Contains("super charge"))
                return "Supercharge";
            return "Power";
        }
    }
}
