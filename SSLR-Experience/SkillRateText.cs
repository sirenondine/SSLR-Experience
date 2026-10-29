using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace SslrExperience
{
    /// <summary>
    /// With use-based skill gains suppressed, every "skills improve X% faster/slower" bonus
    /// (Rested, Lover's Comfort, perks using the ModSkillUse entry point, ...) does nothing.
    /// StripText: removes those sentences from effect, perk and spell descriptions.
    /// BonusXP: perks whose ModSkillUse multiplier applies to all skills also grant a hidden ability that
    /// scales character XP gains by the same multiplier (SSLR_XPRateEffect / SSLR_XPRateController scripts);
    /// their descriptions are reworded to experience. Skill-specific ones fall back to StripText.
    /// </summary>
    static class SkillRateText
    {
        public const string NoEffectText = "Skill learning bonuses have no effect with Static Skill Leveling.";
        private const string EffectScript = "SSLR_XPRateEffect";
        private const string ControllerScript = "SSLR_XPRateController";

        // a short sentence about skill/experience gain speed; long sentences that mix in other effects are left alone
        private static readonly Regex RateSentenceRx = new(
            @"\b(skills?|experience)\b.*\b(faster|slower|gain(ed)? is reduced|experience gained)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SentenceSplitRx = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
        private const int MaxSentenceLength = 100;

        // skill-rate wording => experience wording, applied in order
        private static readonly (Regex Rx, string To)[] XpWording =
        {
            (new Regex(@"\ball skills to improve\b", RegexOptions.IgnoreCase), "experience to be gained"),
            (new Regex(@"\b(all )?skills improve\b", RegexOptions.IgnoreCase), "experience is gained"),
            (new Regex(@"\bskill improvement is\b", RegexOptions.IgnoreCase), "experience is gained"),
            (new Regex(@"\bskill rate( is)?\b", RegexOptions.IgnoreCase), "experience is gained"),
            (new Regex(@"\bskill experience\b", RegexOptions.IgnoreCase), "experience"),
            (new Regex(@"\bskill gain is\b", RegexOptions.IgnoreCase), "experience gain is"),
        };
        private static readonly Regex GenericRateRx = new(@"\b(all skills|skill rate|skill experience)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Settings settings)
        {
            var mode = settings.SkillSettings.SkillRateEffects;
            Console.WriteLine($"Settings.SkillSettings.SkillRateEffects is {mode}");
            if (mode == SkillSettings.SkillRateMode.Leave || !settings.SkillSettings.SuppressSkillGains)
            {
                return false;
            }

            var converted = new HashSet<FormKey>();
            int abilities = 0;
            if (mode == SkillSettings.SkillRateMode.BonusXP)
            {
                abilities = AddXpAbilities(state, converted);
                // effects that apply a converted perk while active (Rested, Lover's Desire, ...)
                foreach (var m in state.LoadOrder.PriorityOrder.MagicEffect().WinningOverrides())
                {
                    if (!m.PerkToApply.IsNull && converted.Contains(m.PerkToApply.FormKey)) converted.Add(m.FormKey);
                }
            }

            bool xp = mode == SkillSettings.SkillRateMode.BonusXP;
            int count = 0;
            foreach (var mgef in state.LoadOrder.PriorityOrder.MagicEffect().WinningOverrides())
            {
                if (Rewrite(mgef.Description?.String, mgef.EditorID, xp && converted.Contains(mgef.FormKey), xp, out var text))
                {
                    state.PatchMod.MagicEffects.GetOrAddAsOverride(mgef).Description = text;
                    count++;
                }
            }
            foreach (var perk in state.LoadOrder.PriorityOrder.Perk().WinningOverrides())
            {
                if (Rewrite(perk.Description?.String, perk.EditorID, xp && converted.Contains(perk.FormKey), xp, out var text))
                {
                    state.PatchMod.Perks.GetOrAddAsOverride(perk).Description = text;
                    count++;
                }
            }
            foreach (var spell in state.LoadOrder.PriorityOrder.Spell().WinningOverrides())
            {
                if (Rewrite(spell.Description?.String, spell.EditorID, xp && converted.Contains(spell.FormKey), xp, out var text))
                {
                    state.PatchMod.Spells.GetOrAddAsOverride(spell).Description = text;
                    count++;
                }
            }
            Console.WriteLine($" + Skill-rate descriptions rewritten: {count}");
            return count > 0 || abilities > 0;
        }

        /// <summary>
        /// Adds an XP-rate ability to every perk whose ModSkillUse multiplier is not limited to one skill.
        /// Collects the converted perks plus the effects/spells their conditions check (Rested's effect, ...),
        /// whose descriptions then get experience wording.
        /// </summary>
        static int AddXpAbilities(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, HashSet<FormKey> converted)
        {
            Quest? controller = null;
            var effectsByMult = new Dictionary<float, MagicEffect>();
            int abilities = 0;

            foreach (var perk in state.LoadOrder.PriorityOrder.Perk().WinningOverrides().ToList())
            {
                List<IPerkEntryPointModifyValueGetter> entries;
                try
                {
                    entries = perk.Effects.OfType<IPerkEntryPointModifyValueGetter>()
                        .Where(e => e.EntryPoint == APerkEntryPointEffect.EntryType.ModSkillUse)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WARN: {perk.FormKey} {perk.EditorID}: unreadable perk effects, skipped ({ex.Message})");
                    continue;
                }
                if (entries.Count == 0) continue;

                var allSkills = entries.Where(AppliesToAllSkills).ToList();
                if (allSkills.Count == 0)
                {
                    Console.WriteLine($"   {perk.EditorID}: skill-specific or non-multiplier skill rate, not converted");
                    continue;
                }

                controller ??= CreateController(state);
                var patchPerk = state.PatchMod.Perks.GetOrAddAsOverride(perk);
                int i = 0;
                foreach (var entry in allSkills)
                {
                    float mult = entry.Value!.Value;
                    if (!effectsByMult.TryGetValue(mult, out var mgef))
                    {
                        mgef = CreateEffect(state, controller, mult);
                        effectsByMult[mult] = mgef;
                    }

                    var ownerConditions = entry.Conditions
                        .Where(c => c.RunOnTabIndex == 0)
                        .SelectMany(c => c.Conditions)
                        .ToList();
                    var effect = new Effect
                    {
                        BaseEffect = mgef.ToNullableLink<IMagicEffectGetter>(),
                        Data = new EffectData(),
                    };
                    effect.Conditions.AddRange(ownerConditions.Select(c => c.DeepCopy()));

                    var spell = state.PatchMod.Spells.AddNew($"SSLR_XPRate_{perk.EditorID}_{i++}");
                    spell.Name = "Experience Rate";
                    spell.Type = SpellType.Ability;
                    spell.CastType = CastType.ConstantEffect;
                    spell.TargetType = TargetType.Self;
                    spell.Effects.Add(effect);

                    patchPerk.Effects.Add(new PerkAbilityEffect
                    {
                        Ability = spell.ToLink(),
                        Rank = entry.Rank,
                        Priority = entry.Priority,
                    });
                    abilities++;

                    foreach (var link in ownerConditions.SelectMany(c => c.EnumerateFormLinks()))
                    {
                        converted.Add(link.FormKey);
                        if (state.LinkCache.TryResolve<ISpellGetter>(link.FormKey, out var condSpell))
                        {
                            foreach (var e in condSpell.Effects) converted.Add(e.BaseEffect.FormKey);
                        }
                    }
                    Console.WriteLine($"   {perk.EditorID}: XP x{mult} while [{string.Join(" AND ", ownerConditions.Select(DescribeCondition))}]");
                }
                converted.Add(perk.FormKey);
            }

            if (controller != null)
            {
                WriteScripts(state);
            }
            Console.WriteLine($" + XP-rate abilities added: {abilities}");
            return abilities;
        }

        // multipliers outside this range are XP-scaling tricks for one activity (e.g. enchanting staves), not a learning bonus
        private const float MinMult = 0.1f, MaxMult = 5f;

        /// <summary>
        /// True when a ModSkillUse entry is a plain multiplier gated only on the perk owner's state.
        /// Conditions on tab 1 (the skill being used), entry-point functions (EPMagic_IsAdvanceSkill, ...) or a
        /// crafting station make it specific to one skill or activity, which character XP cannot express.
        /// </summary>
        static bool AppliesToAllSkills(IPerkEntryPointModifyValueGetter entry)
        {
            if (entry.Modification != PerkEntryPointModifyValue.ModificationType.Multiply || !entry.Value.HasValue) return false;
            if (entry.Value.Value < MinMult || entry.Value.Value > MaxMult) return false;
            foreach (var tab in entry.Conditions)
            {
                if (tab.Conditions.Count == 0) continue;
                if (tab.RunOnTabIndex != 0) return false;
                foreach (var c in tab.Conditions)
                {
                    var fn = c.Data.Function.ToString();
                    if (fn.StartsWith("EP") || fn == "IsCurrentFurnitureObj" || fn == "IsCurrentFurnitureRef") return false;
                }
            }
            return true;
        }

        static Quest CreateController(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var quest = state.PatchMod.Quests.AddNew("SSLR_XPRateQuest");
            quest.Name = "SSLR XP Rate Controller";
            quest.Flags = Quest.Flag.StartGameEnabled;
            quest.VirtualMachineAdapter = new QuestAdapter();
            quest.VirtualMachineAdapter.Scripts.Add(new ScriptEntry { Name = ControllerScript });
            return quest;
        }

        static MagicEffect CreateEffect(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Quest controller, float mult)
        {
            var mgef = state.PatchMod.MagicEffects.AddNew($"SSLR_XPRateEffect_{Math.Round(mult * 100)}");
            mgef.Name = "Experience Rate";
            mgef.Flags = MagicEffect.Flag.HideInUI | MagicEffect.Flag.NoMagnitude | MagicEffect.Flag.NoArea
                | MagicEffect.Flag.NoDuration | MagicEffect.Flag.Painless | MagicEffect.Flag.NoHitEvent;
            mgef.CastType = CastType.ConstantEffect;
            mgef.TargetType = TargetType.Self;
            mgef.MagicSkill = ActorValue.None;
            mgef.ResistValue = ActorValue.None;
            mgef.SecondActorValue = ActorValue.None;
            mgef.Archetype = new MagicEffectArchetype(MagicEffectArchetype.TypeEnum.Script);
            mgef.VirtualMachineAdapter = new VirtualMachineAdapter();
            mgef.VirtualMachineAdapter.Scripts.Add(new ScriptEntry
            {
                Name = EffectScript,
                Properties =
                {
                    new ScriptFloatProperty { Name = "Mult", Data = mult },
                    new ScriptObjectProperty { Name = "Controller", Object = controller.ToLink() },
                },
            });
            return mgef;
        }

        /// <summary>Writes the embedded compiled scripts to Data/Scripts.</summary>
        static void WriteScripts(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var dir = Path.Combine(state.DataFolderPath, "Scripts");
            Directory.CreateDirectory(dir);
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("Scripts.") && n.EndsWith(".pex")))
            {
                var path = Path.Combine(dir, name["Scripts.".Length..]);
                using var src = asm.GetManifestResourceStream(name)!;
                using var dst = File.Create(path);
                src.CopyTo(dst);
                Console.WriteLine($"Writing script {path}");
            }
        }

        static string DescribeCondition(IConditionGetter c) =>
            $"{c.Data.Function} {string.Join(",", c.Data.EnumerateFormLinks().Select(l => l.FormKey.ToString()))} {c.CompareOperator} {(c as IConditionFloatGetter)?.ComparisonValue}";

        /// <param name="toXp">record's bonus was converted to XP: reword every skill-rate sentence</param>
        /// <param name="xpMode">BonusXP mode: also reword generic ("all skills", "skill rate") sentences on records not linked to a converted perk
        /// (e.g. a perk whose script casts the converted effect)</param>
        static bool Rewrite(string? description, string? editorId, bool toXp, bool xpMode, out string result)
        {
            result = "";
            if (string.IsNullOrWhiteSpace(description)) return false;

            var sentences = SentenceSplitRx.Split(description.Trim());
            var kept = new List<string>();
            bool changed = false;
            foreach (var sentence in sentences)
            {
                if (RateSentenceRx.IsMatch(sentence))
                {
                    if (sentence.Length > MaxSentenceLength)
                    {
                        Console.WriteLine($"INFO: {editorId}: skill-rate text inside a longer sentence left as-is: \"{sentence}\"");
                        kept.Add(sentence);
                        continue;
                    }
                    changed = true;
                    if (toXp || (xpMode && GenericRateRx.IsMatch(sentence)))
                    {
                        var reworded = XpWording.Aggregate(sentence, (s, w) => w.Rx.Replace(s, w.To));
                        if (Regex.IsMatch(reworded, @"\bexperience\b", RegexOptions.IgnoreCase))
                        {
                            kept.Add(char.ToUpperInvariant(reworded[0]) + reworded[1..]);
                        }
                    }
                    continue;
                }
                kept.Add(sentence);
            }
            if (!changed) return false;

            result = kept.Count > 0 ? string.Join(" ", kept) : NoEffectText;
            if (Program.Settings.General.Debug)
            {
                Console.WriteLine($"{editorId}: \"{description}\" => \"{result}\"");
            }
            return result != description;
        }
    }
}
