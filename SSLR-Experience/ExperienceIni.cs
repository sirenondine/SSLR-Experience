using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SslrExperience
{
    /// <summary>
    /// Line-preserving INI editor: sets [section] key = value pairs in place, appending
    /// missing keys/sections, so keys this patcher does not own keep their installed values.
    /// </summary>
    class ExperienceIni
    {
        private static readonly Regex SectionRx = new(@"^\s*\[(?<name>[^\]]+)\]\s*$", RegexOptions.Compiled);
        private static readonly Regex KeyRx = new(@"^\s*(?<key>[^;=\s][^=]*?)\s*=", RegexOptions.Compiled);

        private readonly List<string> lines;

        public ExperienceIni(IEnumerable<string> lines)
        {
            this.lines = lines.ToList();
        }

        public static ExperienceIni Load(string path) =>
            new(File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>());

        /// <summary>Reads [section] key = value pairs, ignoring comments.</summary>
        public static IEnumerable<(string Section, string Key, string Value)> ReadPairs(string path)
        {
            string section = "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = Regex.Replace(raw, @"\s*;.*$", "");
                var sm = SectionRx.Match(line);
                if (sm.Success)
                {
                    section = sm.Groups["name"].Value.Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    yield return (section, line[..eq].Trim(), line[(eq + 1)..].Trim());
                }
            }
        }

        public void Set(string section, string key, string value)
        {
            int sectionStart = -1, sectionEnd = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                var sm = SectionRx.Match(lines[i]);
                if (!sm.Success) continue;
                if (sectionStart >= 0)
                {
                    sectionEnd = i;
                    break;
                }
                if (sm.Groups["name"].Value.Trim().Equals(section, StringComparison.OrdinalIgnoreCase))
                {
                    sectionStart = i;
                }
            }

            if (sectionStart < 0)
            {
                if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
                lines.Add($"[{section}]");
                lines.Add($"{key} = {value}");
                return;
            }

            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                var km = KeyRx.Match(lines[i]);
                if (km.Success && km.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{key} = {value}";
                    return;
                }
            }

            // insert after the section's last non-blank line
            int insertAt = sectionEnd;
            while (insertAt - 1 > sectionStart && lines[insertAt - 1].Trim().Length == 0) insertAt--;
            lines.Insert(insertAt, $"{key} = {value}");
        }

        /// <summary>Removes a key from every section; returns how many lines were dropped.</summary>
        public int Remove(string key)
        {
            return lines.RemoveAll(l =>
            {
                var km = KeyRx.Match(l);
                return km.Success && km.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase);
            });
        }

        public IReadOnlyList<string> Lines => lines;

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, lines);
        }
    }
}
