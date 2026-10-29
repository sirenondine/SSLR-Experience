using Mutagen.Bethesda.Synthesis.Settings;

namespace SslrExperience
{
    public class Settings
    {
        [SynthesisSettingName("General")]
        public General General = new();
        [SynthesisSettingName("Skill caps (read by SSLR)")]
        public SkillCapSettings SkillCaps = new();
        [SynthesisSettingName("Skills-related Settings")]
        public SkillSettings SkillSettings = new();
        [SynthesisSettingName("Killing XP settings")]
        public RacesSettings RacesSettings = new();
    }

    public class General
    {
        [SynthesisSettingName("Patch Quests XP")]
        [SynthesisTooltip("Re-type quests per QuestSettings.json so Experience awards the intended XP.")]
        public bool PatchQuests = true;

        [SynthesisTooltip("Base Level UP XP required per level (fXPLevelUpBase)")]
        public float LevelUpXPBase = 100;

        [SynthesisTooltip("Multiplied Level UP XP required per level (fXPLevelUpMult)")]
        public float LevelUpXPMult = 50;

        [SynthesisSettingName("Preset for Experience")]
        [SynthesisTooltip("Merge preset values into Experience.ini (only keys the preset lists are changed)\r\nNone - leave XP values alone (skill caps are still written)\r\nTrue Unlevelled - kill XP ignores level difference\r\nExtra Rewarding - kill XP grows with enemy level above yours")]
        public ExperiencePreset Preset = ExperiencePreset.TrueUnlevelled;

        [SynthesisSettingName("Enable debug")]
        [SynthesisTooltip("Generate additional debug files")]
        public bool Debug = false;

        public enum ExperiencePreset
        {
            None, TrueUnlevelled, ExtraRewarding
        }
    }

    public class SkillCapSettings
    {
        [SynthesisSettingName("Write skill caps to Experience.ini")]
        [SynthesisTooltip("SSLR takes its per-skill caps from Experience (cap = base + mult x player level, plus racial bonus).")]
        public bool WriteSkillCaps = true;

        [SynthesisTooltip("fSkillCapBase: skill cap at player level 0")]
        public float SkillCapBase = 18;

        [SynthesisTooltip("fSkillCapMult: skill cap gained per player level")]
        public float SkillCapMult = 2;

        [SynthesisTooltip("bUseRacialCaps: add race skill bonuses on top of the cap")]
        public bool UseRacialCaps = true;

        [SynthesisTooltip("iMaxPlayerLevel; 0 leaves the installed value unchanged")]
        public int MaxPlayerLevel = 0;
    }

    public class SkillSettings
    {
        [SynthesisTooltip("Stops skills levelling from use (skill UseMult = 0) and sets bEnableSkillXP=false in Experience.ini\n" +
            "so skill-ups give no character XP. SSLR raises skills directly when you spend points, so it is unaffected.")]
        public bool SuppressSkillGains = true;

        [SynthesisTooltip("Remove Skills from Skill Books, so they cannot bypass SSLR")]
        public bool PatchSkillBooks = true;

        [SynthesisTooltip("Skill Books value multiplier, %. Requires Skill Books patching to be enabled.")]
        public uint SkillBooksValueMultiplier = 200;

        [SynthesisSettingName("Skill-rate effects")]
        [SynthesisTooltip("\"Skills improve X% faster/slower\" effects (Rested, Lover's Comfort, ...) do nothing once skill gains are suppressed.\n" +
            "Leave - change nothing\n" +
            "StripText - remove the skill-rate sentence from effect, perk and spell descriptions\n" +
            "BonusXP - character XP gains are scaled by the same multiplier instead (penalties too); skill-specific ones fall back to StripText")]
        public SkillRateMode SkillRateEffects = SkillRateMode.BonusXP;

        public enum SkillRateMode
        {
            Leave, StripText, BonusXP
        }
    }

    public class RacesSettings
    {
        [SynthesisSettingName("Patch Killing XP")]
        [SynthesisTooltip("Generate race and unique-actor kill XP files for Experience.")]
        public bool PatchRaces = true;

        [SynthesisSettingName("Use SkyPatcher NPC levels")]
        [SynthesisTooltip("Read fixed NPC levels set at runtime by SkyPatcher INIs (SKSE/Plugins/SkyPatcher/npc) instead of the plugin record levels.")]
        public bool UseSkyPatcherLevels = true;

        [SynthesisTooltip("Races output file; default replaces the one shipped with Experience")]
        public string OutputFile = "Default.ini";

        [SynthesisTooltip("Unique actors output file; written next to Experience's own curated actors list, which is kept")]
        public string ActorsOutputFile = "SSLR_Synthesis.ini";

        [SynthesisTooltip("Use Unique flag on NPC records if NPC level above threshold")]
        public int NPCUniqueFlagThreshold = 20;

        [SynthesisSettingName("Averaging Function")]
        [SynthesisTooltip("Function to average race level basing on its members")]
        public AverageMode Mode = AverageMode.Mean;

        [SynthesisTooltip("Formula which converts enemy level into XP value. Old formula is just \"[level]\"")]
        public string LevelFormula = "pow( [level], 1.28 ) / 2";

        public enum AverageMode
        {
            Mean, GeometricMean, HarmonicMean, RootMeanSquare, Median
        }
    }
}
