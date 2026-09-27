#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace EffectTools.Cleanup
{
    // A snapshot. The window owns cancellation, freshness and all user actions.
    internal sealed class DVClearIndex
    {
        [Flags] internal enum Evidence { UnityDependency = 1, TextGuid = 2, ImporterGuid = 4, ShaderInclude = 8 }
        internal readonly Dictionary<string, Dictionary<string, Evidence>> Forward = new Dictionary<string, Dictionary<string, Evidence>>(StringComparer.Ordinal);
        internal readonly Dictionary<string, HashSet<string>> Reverse = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        internal readonly List<string> Issues = new List<string>();
        internal readonly List<string> Unresolved = new List<string>();
        internal string[] Paths;
        internal int Processed;
        internal bool Completed;
        internal string CurrentPath = "";
        private readonly Dictionary<string, string> guidPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> packageRoots = new Dictionary<string, string>();
        private static readonly Regex GuidPattern = new Regex(@"(?<![0-9a-fA-F])[0-9a-fA-F]{32}(?![0-9a-fA-F])", RegexOptions.Compiled);
        private static readonly Regex IncludePattern = new Regex("^\\s*#\\s*include(?:_with_pragmas)?\\s*[\"<]([^\">]+)[\">]", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".prefab", ".unity", ".mat", ".asset", ".controller", ".overridecontroller", ".anim", ".playable", ".mask", ".rendertexture", ".cubemap", ".flare", ".lighting", ".guiskin", ".physicmaterial", ".physicsmaterial2d", ".terrainlayer", ".spriteatlas", ".spriteatlasv2", ".shadergraph", ".shadersubgraph", ".vfx", ".vfxoperator", ".vfxblock", ".json", ".txt", ".yaml", ".yml", ".xml", ".uxml", ".uss", ".tss", ".cs", ".shader", ".hlsl", ".cginc", ".compute", ".asmdef", ".asmref", ".meta" };
        private const long TextLimit = 16 * 1024 * 1024;

        internal void Initialize()
        {
            var paths = AssetDatabase.GetAllAssetPaths()
                .Where(p => (p.StartsWith("Assets/", StringComparison.Ordinal) || p.StartsWith("Packages/", StringComparison.Ordinal)) && !AssetDatabase.IsValidFolder(p))
                .ToList();
            if (Directory.Exists("ProjectSettings"))
                paths.AddRange(Directory.GetFiles("ProjectSettings", "*", SearchOption.TopDirectoryOnly).Select(p => p.Replace('\\', '/')));
            Paths = paths.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (string p in Paths)
            {
                if (p.StartsWith("ProjectSettings/")) continue;
                string guid = AssetDatabase.AssetPathToGUID(p);
                if (!string.IsNullOrEmpty(guid)) guidPaths[guid] = p;
            }
        }

        internal bool Step()
        {
            if (Processed >= Paths.Length) { Completed = true; return false; }
            string p = Paths[Processed++];
            CurrentPath = p;
            try
            {
                if (!p.StartsWith("ProjectSettings/", StringComparison.Ordinal))
                    foreach (string dependency in AssetDatabase.GetDependencies(p, false))
                        Add(p, dependency, Evidence.UnityDependency);
            }
            catch (Exception e) { Issues.Add(p + "：依赖读取失败：" + e.Message); }
            string disk = DiskPath(p);
            ReadText(p, disk, Evidence.TextGuid);
            if (File.Exists(disk + ".meta")) ReadText(p, disk + ".meta", Evidence.ImporterGuid);
            return true;
        }

        internal string DiskPath(string path)
        {
            if (!path.StartsWith("Packages/", StringComparison.Ordinal)) return path;
            string[] parts = path.Split('/');
            if (parts.Length < 3) return path;
            string key = "Packages/" + parts[1];
            string root;
            if (!packageRoots.TryGetValue(key, out root))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                root = package == null ? key : package.resolvedPath;
                packageRoots[key] = root;
            }
            return Path.Combine(root, path.Substring(key.Length + 1));
        }

        internal static IEnumerable<string> GuidTokens(string text)
        {
            return GuidPattern.Matches(text).Cast<Match>().Select(m => m.Value.ToLowerInvariant())
                .Where(g => !g.StartsWith("0000000000000000", StringComparison.Ordinal)).Distinct();
        }

        private void ReadText(string source, string disk, Evidence evidence)
        {
            if (!TextExtensions.Contains(Path.GetExtension(disk))) return;
            try
            {
                if (!File.Exists(disk)) { Issues.Add(source + "：源文件不可读：" + disk); return; }
                if (new FileInfo(disk).Length > TextLimit) { Issues.Add(source + "：文本超过 16 MB，未进行 GUID 补充扫描"); return; }
                string text;
                using (var reader = new StreamReader(disk, System.Text.Encoding.UTF8, true))
                {
                    char[] probe = new char[2048];
                    int count = reader.Read(probe, 0, probe.Length);
                    if (Array.IndexOf(probe, '\0', 0, count) >= 0) return; // binary assets use the Unity dependency API
                    text = new string(probe, 0, count) + reader.ReadToEnd();
                }
                foreach (string guid in GuidTokens(text))
                {
                    string target;
                    if (!guidPaths.TryGetValue(guid, out target))
                    {
                        target = AssetDatabase.GUIDToAssetPath(guid);
                        guidPaths[guid] = target;
                    }
                    if (!string.IsNullOrEmpty(target)) Add(source, target, evidence);
                    else if (Unresolved.Count < 5000) Unresolved.Add(source + " → " + guid + "（未解析文本 GUID，可能是缺失引用或普通字符串）");
                }
                string extension = Path.GetExtension(source).ToLowerInvariant();
                if (evidence == Evidence.TextGuid && (extension == ".shader" || extension == ".hlsl" || extension == ".cginc" || extension == ".compute"))
                    foreach (Match match in IncludePattern.Matches(text))
                    {
                        string include = match.Groups[1].Value.Replace('\\', '/');
                        string target = ResolveInclude(source, include);
                        if (target != null) Add(source, target, Evidence.ShaderInclude);
                    }
            }
            catch (Exception e) { Issues.Add(source + "：文本读取失败：" + e.Message); }
        }

        private string ResolveInclude(string source, string include)
        {
            string candidate;
            if (include.StartsWith("Assets/") || include.StartsWith("Packages/")) candidate = include;
            else
            {
                // Normalize relative includes in the logical Assets/Packages namespace.
                var pieces = new List<string>((Path.GetDirectoryName(source).Replace('\\', '/') + "/" + include).Split('/'));
                var normalized = new List<string>();
                foreach (string part in pieces)
                {
                    if (part == "." || part == "") continue;
                    if (part == "..") { if (normalized.Count == 0) return null; normalized.RemoveAt(normalized.Count - 1); }
                    else normalized.Add(part);
                }
                candidate = string.Join("/", normalized.ToArray());
            }
            if (!(candidate.StartsWith("Assets/") || candidate.StartsWith("Packages/"))) return null;
            return File.Exists(DiskPath(candidate)) ? candidate : null; // Unity built-in includes are outside project scope
        }

        internal void Add(string source, string target, Evidence evidence)
        {
            if (string.IsNullOrEmpty(target) || target == source ||
                !(target.StartsWith("Assets/", StringComparison.Ordinal) || target.StartsWith("Packages/", StringComparison.Ordinal))) return;
            Dictionary<string, Evidence> edges;
            if (!Forward.TryGetValue(source, out edges)) Forward[source] = edges = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            Evidence old;
            edges.TryGetValue(target, out old);
            edges[target] = old | evidence;
            HashSet<string> parents;
            if (!Reverse.TryGetValue(target, out parents)) Reverse[target] = parents = new HashSet<string>(StringComparer.Ordinal);
            parents.Add(source);
        }

        internal List<string> Links(string path, bool forward)
        {
            if (forward) { Dictionary<string, Evidence> edges; return Forward.TryGetValue(path, out edges) ? edges.Keys.OrderBy(p => p, StringComparer.Ordinal).ToList() : new List<string>(); }
            HashSet<string> parents;
            return Reverse.TryGetValue(path, out parents) ? parents.OrderBy(p => p, StringComparer.Ordinal).ToList() : new List<string>();
        }
        internal string Describe(string source, string target)
        {
            Dictionary<string, Evidence> edges; Evidence evidence;
            if (!Forward.TryGetValue(source, out edges) || !edges.TryGetValue(target, out evidence)) return "";
            var labels = new List<string>();
            if ((evidence & Evidence.UnityDependency) != 0) labels.Add("Unity 直接依赖");
            if ((evidence & Evidence.TextGuid) != 0) labels.Add("文本 GUID 候选");
            if ((evidence & Evidence.ImporterGuid) != 0) labels.Add("导入配置 GUID 候选");
            if ((evidence & Evidence.ShaderInclude) != 0) labels.Add("Shader include");
            return string.Join(" + ", labels.ToArray());
        }
    }
}
#endif
