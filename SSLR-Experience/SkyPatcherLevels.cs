using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins;

namespace SslrExperience
{
    /// <summary>
    /// Collects fixed NPC levels that SkyPatcher applies at runtime, from
    /// SKSE/Plugins/SkyPatcher/npc/**/*.ini lines of the form
    /// filterByNpcs=Plugin.esp|0ABCDE,...:...:level=30:...
    /// Only per-NPC filters are understood; rule lines (filterByModNames, restrictToSkill, ...) are ignored.
    /// SkyPatcher applies files in path order, so a later file's level wins.
    /// </summary>
    static class SkyPatcherLevels
    {
        private static readonly Regex NpcFilterRx = new(@"(?:^|:)filterByNpcs\s*=\s*(?<npcs>[^:]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LevelRx = new(@":level\s*=\s*(?<level>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NpcRefRx = new(@"^\s*(?<mod>[^|]+?)\s*\|\s*(?:0x)?(?<id>[0-9A-Fa-f]{1,8})\s*$", RegexOptions.Compiled);

        public static Dictionary<FormKey, int> Read(string dataFolder, out int fileCount)
        {
            var result = new Dictionary<FormKey, int>();
            fileCount = 0;
            var root = Path.Combine(dataFolder, "SKSE", "Plugins", "SkyPatcher", "npc");
            if (!Directory.Exists(root)) return result;

            var files = Directory.EnumerateFiles(root, "*.ini", SearchOption.AllDirectories)
                .OrderBy(f => Path.GetRelativePath(root, f), StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                fileCount++;
                foreach (var raw in File.ReadLines(file))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(';')) continue;
                    var filter = NpcFilterRx.Match(line);
                    var level = LevelRx.Match(line);
                    if (!filter.Success || !level.Success) continue;
                    int lvl = int.Parse(level.Groups["level"].Value);
                    foreach (var npc in filter.Groups["npcs"].Value.Split(','))
                    {
                        var m = NpcRefRx.Match(npc);
                        if (!m.Success) continue;
                        // SkyPatcher accepts full load-order IDs; keep only the local part
                        uint id = Convert.ToUInt32(m.Groups["id"].Value, 16) & 0xFFFFFF;
                        result[new FormKey(ModKey.FromFileName(m.Groups["mod"].Value), id)] = lvl;
                    }
                }
            }
            return result;
        }
    }
}
