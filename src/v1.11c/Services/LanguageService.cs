using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HikiNotifier.Services
{
    internal sealed class LanguagePack
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Path { get; set; }
        public Dictionary<string, Dictionary<string, string>> Values { get; set; }
        public override string ToString() => Name;
    }

    internal sealed class LanguageService
    {
        private readonly string directory;
        private List<LanguagePack> packs = new List<LanguagePack>();
        private string selected;
        public IReadOnlyList<LanguagePack> Packs => packs;
        public string Language
        {
            get => selected;
            set => selected = Resolve(value);
        }

        public LanguageService(string language, string languageDirectory = null)
        {
            directory = languageDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang");
            Rescan();
            Language = language;
        }

        public void Rescan()
        {
            var found = new List<LanguagePack>();
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.GetFiles(directory, "*.ini").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var values = Parse(path);
                        var metadata = values["Language"];
                        var name = metadata["Name"].Trim();
                        var code = metadata["Code"].Trim().ToLowerInvariant();
                        if (name.Length == 0 || name.Length > 80 || !Regex.IsMatch(code, "^[a-z][a-z0-9-]{1,15}$"))
                            throw new FormatException("Invalid language metadata");
                        found.Add(new LanguagePack { Name = name, Code = code, Path = path, Values = values });
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is KeyNotFoundException || ex is DecoderFallbackException)
                    {
                        Debug.WriteLine("Ignored language pack " + path + ": " + ex.Message);
                    }
                }
            }
            packs = found.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(p => string.Equals(System.IO.Path.GetFileNameWithoutExtension(p.Path), p.Code, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).First())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            selected = Resolve(selected);
        }

        public string Get(string section, string key)
        {
            var value = Read(selected, section, key) ?? Read("en", section, key) ?? key;
            return value.Replace("\\n", Environment.NewLine);
        }

        private string Resolve(string code)
        {
            var pack = packs.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            return pack?.Code ?? packs.FirstOrDefault(p => p.Code == "en")?.Code ?? "en";
        }

        private string Read(string code, string section, string key)
        {
            var pack = packs.FirstOrDefault(p => p.Code == code);
            if (pack == null || !pack.Values.TryGetValue(section, out var values)) return null;
            return values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
        }

        private static Dictionary<string, Dictionary<string, string>> Parse(string path)
        {
            string content;
            var bytes = File.ReadAllBytes(path);
            try { content = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { content = Encoding.GetEncoding(949).GetString(bytes); }
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> section = null;
            foreach (var raw in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var name = line.Substring(1, line.Length - 2).Trim();
                    if (name.Length == 0) throw new FormatException("Empty section");
                    if (!result.TryGetValue(name, out section)) result[name] = section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                var equal = line.IndexOf('=');
                if (section == null || equal <= 0) throw new FormatException("Invalid INI line");
                section[line.Substring(0, equal).Trim()] = line.Substring(equal + 1).Trim();
            }
            return result;
        }
    }
}
