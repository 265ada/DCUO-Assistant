using System;

namespace DCUOTracker.Models
{
    /// <summary>
    /// Classifies a loadout ability into a short "what it does / main use" tag for the Builds tab.
    /// Tuned to the ability names used in BuildLibrary. Order matters (first match wins);
    /// anything unmatched defaults to DMG, which is correct for most damage abilities.
    /// </summary>
    public static class AbilityTag
    {
        public record Tag(string Text, string Hex);

        private const string DMG="#FF6B35", DOT="#FB923C", COMBO="#22D3EE", SHIELD="#60A5FA",
                             HEAL="#34D399", HOT="#6EE7B7", POWER="#FBBF24", DEBUFF="#C084FC",
                             TAUNT="#F87171", AGGRO="#FCA5A5", BREAK="#38BDF8", PET="#5FE0A8",
                             CC="#F472B6", BUFF="#A3E635", MIT="#93C5FD", SC="#FFD24A",
                             ICON="#C7A6FF", AUG="#9CA3AF";

        // Ordered rules: if the ability name CONTAINS the key (case-insensitive) → that tag.
        private static readonly (string key, string tag, string hex)[] _rules =
        {
            // markers already in the name
            ("(iconic)","ICONIC",ICON), ("augment","AUGMENT",AUG),
            ("(sc)","SUPERCHARGE",SC), ("supercharge","SUPERCHARGE",SC),
            ("(st taunt)","ST TAUNT",TAUNT), ("taunt","ST TAUNT",TAUNT),

            // pets
            ("robot sidekick","PET",PET), ("brick golem","PET (soak)",PET),
            ("fortify golem","PET SHIELD",PET), ("suppressor turret","PET",PET),
            ("summon fury","PET",PET), ("watcher","PET",PET),

            // group power battery (troll)
            ("defibrillator","GROUP POWER",POWER), ("reload","GROUP POWER",POWER),
            ("transducer","GROUP POWER",POWER), ("psychic empowerment","GROUP POWER",POWER),
            ("temporal extorsion","GROUP POWER",POWER), ("recharge","GROUP POWER",POWER),

            // heals / HoTs (specific names first so they beat the SHIELD keywords)
            ("blessing of the depths","HoT",HOT), ("consume soul","HoT",HOT),
            ("circle of protection","HoT",HOT), ("solace of the sea","HoT",HOT),
            ("regeneration","HoT",HOT), ("virtuous light","HoT (glyph)",HOT),
            ("flourish","HoT",HOT), ("cross pollination","HEAL",HEAL),
            ("rejuvenate","HEAL",HEAL), ("ritualistic word","HEAL",HEAL),
            ("invocation of renewal","BIG HEAL",HEAL), ("renew","HEAL",HEAL),
            ("admonish","HEAL",HEAL), ("blossom","HEAL",HEAL), ("metabolism","HEAL",HEAL),
            ("mending wave","HEAL",HEAL), ("soothing mist","HEAL",HEAL),
            ("bioelectric surge","HEAL",HEAL), ("recover","HEAL",HEAL),
            ("bio-capacitor","EMERGENCY HEAL",HEAL), ("tranquil pool","SC HEAL",HEAL),
            ("stoke flames","HEAL AURA",HEAL), ("burning determination","SELF-HEAL",HEAL),
            ("remorseless recovery","LIFESTEAL",HEAL), ("proton remedy","SELF-HEAL",HEAL),
            ("backdraft","PULL + HEAL",AGGRO), ("flux","EMERGENCY HEAL",HEAL),
            ("(heal)","HEAL",HEAL), ("rejuv","HEAL",HEAL),

            // shields
            ("shield","SHIELD",SHIELD), ("winter ward","SHIELD",SHIELD), ("reflection","SHIELD",SHIELD),
            ("barrier","SHIELD",SHIELD), ("density","SHIELD",SHIELD), ("bubble","SHIELD",SHIELD),
            ("hibernation","SHIELD/HEAL",SHIELD), ("tempest guard","GROUP SHIELD",SHIELD),
            ("boon of souls","SHIELD",SHIELD), ("transcendence","GROUP SHIELD",SHIELD),
            ("quantum tunneling","SHIELD/REZ",SHIELD), ("redirected rage","SHIELD",SHIELD),
            ("immolation","SHIELD",SHIELD), ("blessing","SHIELD",SHIELD),

            // breakout (group CC immunity)
            ("burnout","BREAKOUT",BREAK), ("shatter restraints","BREAKOUT",BREAK),
            ("soothing sands","BREAKOUT + HEAL",BREAK), ("neutrino blast","BREAKOUT",BREAK),
            ("ire","BREAKOUT",BREAK),

            // buffs / mitigation
            ("high tide","DMG+HEAL BUFF",BUFF), ("eviscerating chain","CRASH MITIGATE",MIT),
            ("cryokinesis","DMG MITIGATE",MIT),

            // aggro / pulls (tank)
            ("engulf","AGGRO PULL",AGGRO), ("inescapable storm","AGGRO PULL",AGGRO),
            ("earthen grip","AGGRO PULL",AGGRO), ("ragebringer","AGGRO PULL",AGGRO),
            ("atomic reorganization","AGGRO",AGGRO), ("epicenter","PULL",AGGRO),
            ("telekinetic pull","PULL",AGGRO),

            // debuffs
            ("stasis field","DEBUFF",DEBUFF), ("napalm","HEAL DEBUFF",DEBUFF),
            ("paralyzing dart","DMG DEBUFF",DEBUFF), ("smoke grenade","DEF DEBUFF",DEBUFF),
            ("singularity","DEF DEBUFF",DEBUFF), ("alcubierre wave","DMG DEBUFF",DEBUFF),
            ("light blast","DEF DEBUFF",DEBUFF), ("psychic shock","DEF DEBUFF",DEBUFF),
            ("entrap","HEAL DEBUFF",DEBUFF), ("multi-net","DEBUFF + CC",DEBUFF),
            ("horrific visage","SC + DEBUFF",DEBUFF), ("anomaly","SC + DEBUFF",DEBUFF),
            ("distract","STEALTH",DEBUFF), ("snap trap","CC",CC), ("sleep dart","CC",CC),

            // crowd control
            ("mass levitation","CC + DMG",CC), ("without mercy","AoE KNOCKDOWN",CC),
            ("frost slam","KNOCKDOWN",CC), ("mesmerizing","CC",CC), ("sonic cry","AoE CC",CC),
            ("mass deception","CC",CC),

            // damage-over-time / setters
            ("plague","DoT COMBO",DOT), ("blight","DoT COMBO",DOT), ("briar","POISON DoT",DOT),
            ("vine lash","POISON DoT",DOT), ("serpent call","POISON DoT",DOT),
            ("carnivorous plants","DoT",DOT), ("swarm","DoT",DOT), ("aqualance","AoE DoT",DOT),
            ("inferno","BURN DoT",DOT), ("overheat","BURN DoT",DOT), ("poison","POISON DoT",DOT),
            ("spontaneous combustion","BURN DoT",DOT), ("electrocute","DoT",DOT),

            // combo / weave (press & combo — don't clip)
            ("jackhammer","COMBO (weave)",COMBO), ("unstoppable","COMBO (weave)",COMBO),
            ("atom splitter","COMBO (weave)",COMBO), ("atom-powered assault","COMBO (weave)",COMBO),
            ("thermochemical","COMBO",COMBO), ("tsunami strikes","COMBO (heal+dmg)",COMBO),
            ("ram","CONSTRUCT COMBO",COMBO), ("impact","CONSTRUCT COMBO",COMBO),
            ("chompers","CONSTRUCT COMBO",COMBO), ("light weight strike","COMBO + DAZE",COMBO),
            ("light claws","CONSTRUCT COMBO",COMBO), ("sacred light","COMBO (sets Purify)",COMBO),
            ("divine light","COMBO (sets Purify)",COMBO), ("drown","COMBO (crusher PI)",COMBO),
            ("ebb","COMBO",COMBO),
        };

        public static Tag For(string ability)
        {
            if (string.IsNullOrWhiteSpace(ability)) return new("DMG", DMG);
            string lo = ability.ToLowerInvariant();
            foreach (var (key, tag, hex) in _rules)
                if (lo.Contains(key)) return new(tag, hex);
            return new("DMG", DMG); // most powerset abilities are damage
        }
    }
}
