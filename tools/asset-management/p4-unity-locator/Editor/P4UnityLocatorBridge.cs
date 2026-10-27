using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityVfxTools.P4UnityLocator
{
    [InitializeOnLoad]
    internal static class P4UnityLocatorBridge
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string Inbox = Path.Combine(Root, "Library", "P4UnityLocator");
        private static double nextPoll;
        private static double nextHeartbeat;

        static P4UnityLocatorBridge()
        {
            Directory.CreateDirectory(Inbox);
            EditorApplication.update += Update;
            EditorApplication.quitting += () => { try { File.Delete(Path.Combine(Inbox, "editor.pid")); } catch { } };
        }

        [MenuItem("Tools/Effect Tools/P4 资源定位/检查连接")]
        private static void CheckConnection()
        {
            EditorUtility.DisplayDialog("P4 资源定位", "定位服务已加载。\n项目：" + Root + "\n在 P4V 中双击材质，或使用右键定位入口。", "确定");
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            try
            {
                if (EditorApplication.timeSinceStartup >= nextHeartbeat)
                {
                    File.WriteAllText(Path.Combine(Inbox, "editor.pid"), Process.GetCurrentProcess().Id.ToString());
                    nextHeartbeat = EditorApplication.timeSinceStartup + 2;
                }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                foreach (string request in Directory.GetFiles(Inbox, "*.request")) Handle(request);
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[P4 资源定位] " + e.Message); }
        }

        private static void Handle(string request)
        {
            string result;
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(request) > TimeSpan.FromSeconds(45))
                    throw new InvalidOperationException("定位请求已过期，请在 P4V 中重新打开该资源。");
                string full = Path.GetFullPath(File.ReadAllText(request, Encoding.UTF8).Trim());
                if (full.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) full = full.Substring(0, full.Length - 5);
                string prefix = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("资源不属于当前项目的 Assets 目录。");
                string assetPath = "Assets/" + full.Substring(prefix.Length).Replace('\\', '/');
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null) throw new InvalidOperationException("Unity 尚未导入或找不到资源：" + assetPath);
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                result = "OK\n" + AssetDatabase.GetAssetPath(Selection.activeObject) + "\n" + AssetDatabase.AssetPathToGUID(assetPath);
                File.WriteAllText(Path.Combine(Inbox, "last-result.txt"), result, new UTF8Encoding(false));
                UnityEngine.Debug.Log("[P4 资源定位] 已选中：" + assetPath);
            }
            catch (Exception e) { result = "ERROR\n" + e.Message; }
            File.WriteAllText(request + ".result", result, new UTF8Encoding(false));
            File.Delete(request);
        }
    }
}
