#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EffectTools.PrefabThumbnails
{
    [Serializable]
    public sealed class ThumbnailSettings
    {
        public string version = "1.1.0";
        public float seconds = .5f;
        public float duration = 5f;
        public int fps = 30;
        public int seed = 12345;
        public Vector2 orbit = new Vector2(25f, 18f);
        public Vector3 center;
        public float distance = 5f;
        public Color background = new Color(.12f, .14f, .17f, 1f);
        public bool orthographic;
        public string clipGuid;
        public long clipLocalId;
        public string dependencyHash;
    }

    [InitializeOnLoad]
    public static class VFXThumbnailStore
    {
        public const string Version = "1.1.0";
        public static string Folder { get { return Path.GetFullPath("UserSettings/VFXPrefabThumbnails"); } }
        static readonly HashSet<string> known = new HashSet<string>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly LinkedList<string> recent = new LinkedList<string>();
        const int Capacity = 128;

        static VFXThumbnailStore()
        {
            ReloadIndex();
            EditorApplication.projectWindowItemOnGUI += DrawProjectItem;
            AssemblyReloadEvents.beforeAssemblyReload += ClearTextures;
            EditorApplication.quitting += ClearTextures;
        }

        public static bool IsPrefab(UnityEngine.Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return asset is GameObject && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        public static string GuidFor(GameObject prefab)
        {
            return IsPrefab(prefab) ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)) : "";
        }

        static void CheckGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || guid.Length != 32)
                throw new ArgumentException("无效的 Prefab GUID。");
            foreach (char c in guid)
                if (!Uri.IsHexDigit(c)) throw new ArgumentException("无效的 Prefab GUID。");
        }

        public static void ReloadIndex()
        {
            ClearTextures(); known.Clear();
            if (Directory.Exists(Folder))
                foreach (var file in Directory.GetFiles(Folder, "*.png")) known.Add(Path.GetFileNameWithoutExtension(file));
            EditorApplication.RepaintProjectWindow();
        }

        static void ClearTextures()
        {
            foreach (var texture in textures.Values)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            textures.Clear(); recent.Clear();
        }

        static void Invalidate(string guid)
        {
            Texture2D old;
            if (textures.TryGetValue(guid, out old) && old != null) UnityEngine.Object.DestroyImmediate(old);
            textures.Remove(guid); recent.Remove(guid);
            EditorApplication.RepaintProjectWindow();
        }

        public static Texture2D LoadTexture(string guid)
        {
            if (!known.Contains(guid)) return null;
            Texture2D image;
            if (!textures.TryGetValue(guid, out image))
            {
                try
                {
                    image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    image.hideFlags = HideFlags.HideAndDontSave;
                    image.name = "VFX thumbnail " + guid;
                    if (!image.LoadImage(File.ReadAllBytes(Path.Combine(Folder, guid + ".png")), true))
                    { UnityEngine.Object.DestroyImmediate(image); image = null; }
                }
                catch (Exception error) { Debug.LogWarning("缩略图读取失败：" + error.Message); image = null; }
                textures[guid] = image;
                while (textures.Count > Capacity && recent.First != null)
                {
                    string victim = recent.First.Value; recent.RemoveFirst();
                    if (textures[victim] != null) UnityEngine.Object.DestroyImmediate(textures[victim]);
                    textures.Remove(victim);
                }
            }
            recent.Remove(guid); recent.AddLast(guid);
            return image;
        }

        public static ThumbnailSettings LoadSettings(string guid)
        {
            CheckGuid(guid);
            string path = Path.Combine(Folder, guid + ".json");
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<ThumbnailSettings>(File.ReadAllText(path)); }
            catch { return null; }
        }

        public static bool Has(string guid) { return known.Contains(guid); }

        static void WriteWithBackup(string path, byte[] bytes)
        {
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous");
            else File.Move(temporary, path);
        }

        public static void Save(string guid, byte[] png, ThumbnailSettings settings)
        {
            CheckGuid(guid);
            if (png == null || png.Length < 8) throw new ArgumentException("没有可保存的图像。");
            Directory.CreateDirectory(Folder);
            WriteWithBackup(Path.Combine(Folder, guid + ".json"), System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(settings, true)));
            WriteWithBackup(Path.Combine(Folder, guid + ".png"), png);
            known.Add(guid); Invalidate(guid);
        }

        public static void RestoreDefault(string guid)
        {
            CheckGuid(guid);
            // Retain removed custom data in a timestamped backup directory.
            string backup = Path.Combine(Folder, "Backups", guid + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            foreach (string extension in new[] { ".png", ".json" })
            {
                string path = Path.Combine(Folder, guid + extension);
                if (!File.Exists(path)) continue;
                Directory.CreateDirectory(backup);
                File.Move(path, Path.Combine(backup, guid + extension));
            }
            known.Remove(guid); Invalidate(guid);
        }

        public static Rect IconRect(Rect item)
        {
            if (item.height <= 22f) return new Rect(item.x, item.y + (item.height - 16f) * .5f, 16f, 16f);
            float size = Mathf.Min(item.width, item.height - 16f);
            return new Rect(item.x + (item.width - size) * .5f, item.y, size, size);
        }

        static void DrawProjectItem(string guid, Rect item)
        {
            if (Event.current.type != EventType.Repaint || !known.Contains(guid)) return;
            var image = LoadTexture(guid);
            if (image == null) return;
            Rect icon = IconRect(item);
            EditorGUI.DrawRect(icon, new Color(.12f, .14f, .17f, 1f));
            GUI.DrawTexture(icon, image, ScaleMode.ScaleToFit, false);
            if (icon.width >= 40f)
            {
                Rect badge = new Rect(icon.x + 3f, icon.yMax - 15f, 25f, 13f);
                EditorGUI.DrawRect(badge, new Color(.05f, .08f, .10f, .85f));
                GUI.Label(badge, "FX", EditorStyles.whiteMiniLabel);
            }
        }
    }
}
#endif
