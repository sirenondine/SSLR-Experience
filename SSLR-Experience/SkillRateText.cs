using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace SslrExperience
{
    /// <summary>
    /// With use-based skill gains suppressed, every "skills improve X% faster/slower" bonus
    /// (Rested, Lover's Comfort, perks using the ModSkillUse entry point, ...) does nothing.
    /// Strips those sentences from effect, perk and spell descriptions so tooltips stop promising them.
    /// </summary>
    static class SkillRateText
    {
        public const string NoEffectText = "Skill learning bonuses have no effect with Static Skill Leveling.";

        // a short sentence about skill/experience gain speed; long sentences that mix in other effects are left alone
        private static readonly Regex RateSentenceRx = new(
            @"\b(skills?|experience)\b.*\b(faster|slower|gain(ed)? is reduced|experience gained)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SentenceSplitRx = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
        private const int MaxSentenceLength = 100;

        public static bool RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Settings settings)
        {
            Console.WriteLine($"Settings.SkillSettings.FixSkillRateText is {settings.SkillSettings.FixSkillRateText}");
            if (!settings.SkillSettings.FixSkillRateText || !settings.SkillSettings.SuppressSkillGains)
            {
                return false;
            }

            int count = 0;
            foreach (var mgef in state.LoadOrder.PriorityOrder.MagicEffect().WinningOverrides())
            {
                if (Rewrite(mgef.Description?.String, mgef.EditorID, out var text))
                {
                    state.PatchMod.MagicEffects.GetOrAddAsOverride(mgef).Description = text;
                    count++;
                }
            }
            foreach (var perk in state.LoadOrder.PriorityOrder.Perk().WinningOverrides())
            {
                if (Rewrite(perk.Description?.String, perk.EditorID, out var text))
                {
                    state.PatchMod.Perks.GetOrAddAsOverride(perk).Description = text;
                    count++;
                }
            }
            foreach (var spell in state.LoadOrder.PriorityOrder.Spell().WinningOverrides())
            {
                if (Rewrite(spell.Description?.String, spell.EditorID, out var text))
                {
                    state.PatchMod.Spells.GetOrAddAsOverride(spell).Description = text;
                    count++;
                }
            }
            Console.WriteLine($" + Skill-rate descriptions rewritten: {count}");
            return count > 0;
        }

        static bool Rewrite(string? description, string? editorId, out string result)
        {
            result = "";
            if (string.IsNullOrWhiteSpace(description)) return false;

            var sentences = SentenceSplitRx.Split(description.Trim());
            var kept = new List<string>();
            bool removed = false;
            foreach (var sentence in sentences)
            {
                if (RateSentenceRx.IsMatch(sentence))
                {
                    if (sentence.Length <= MaxSentenceLength)
                    {
                        removed = true;
                        continue;
                    }
                    Console.WriteLine($"INFO: {editorId}: skill-rate text inside a longer sentence left as-is: \"{sentence}\"");
                }
                kept.Add(sentence);
            }
            if (!removed) return false;

            result = kept.Count > 0 ? string.Join(" ", kept) : NoEffectText;
            if (Program.Settings.General.Debug)
            {
                Console.WriteLine($"{editorId}: \"{description}\" => \"{result}\"");
            }
            return true;
        }
    }
}
