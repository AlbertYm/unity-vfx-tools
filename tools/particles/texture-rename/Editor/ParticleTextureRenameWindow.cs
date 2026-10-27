using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FxShader.EditorTools
{
    public sealed class ParticleTextureRenameWindow : EditorWindow
    {
        private sealed class Row
        {
            public GameObject Target;
            public Texture Texture;
            public string Before, After, Source, Path;
        }

        private readonly List<Row> rows = new List<Row>();
        private Vector2 scroll;
        private bool includeChildren = true;
        private string propertyOverride = "";
        private string result = "";
        private static bool quickRenameQueued;

        [MenuItem("GameObject/粒子按贴图命名（仅所选）", false, 49)]
        private static void RenameSelected()
        {
            // Unity may dispatch GameObject menu commands for multiple selected objects.
            // Queue one batch and capture the selection before the context menu closes.
            if (quickRenameQueued || !CanRenameSelected()) return;
            var selection = Selection.gameObjects;
            quickRenameQueued = true;
            EditorApplication.delayCall += () =>
            {
                quickRenameQueued = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var batch = BuildRows(selection, false, "");
                int count = ApplyRows(batch);
                Debug.Log($"粒子贴图命名：已修改 {count} 个，名称已符合 {batch.Count(r => r.Texture != null) - count} 个，无可用贴图跳过 {batch.Count(r => r.Texture == null)} 个。Ctrl+Z 可撤销本次修改。");
                foreach (var window in Resources.FindObjectsOfTypeAll<ParticleTextureRenameWindow>())
                {
                    window.result = $"右键命名已修改 {count} 个粒子。Ctrl+Z 可撤销。";
                    window.Refresh();
                }
                EditorApplication.RepaintHierarchyWindow();
            };
        }

        [MenuItem("GameObject/粒子按贴图命名（仅所选）", true)]
        private static bool CanRenameSelected()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && Selection.gameObjects.Any(go =>
                go != null && !EditorUtility.IsPersistent(go) && go.scene.IsValid() &&
                go.GetComponent<ParticleSystem>() != null);
        }

        [MenuItem("Effect Tools/粒子按贴图批量命名")]
        private static void Open()
        {
            var window = GetWindow<ParticleTextureRenameWindow>("粒子贴图命名");
            window.minSize = new Vector2(760, 380);
            window.Refresh();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("选中特效父节点，按粒子主贴图命名", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("处理 Hierarchy 及 Prefab 编辑模式中的粒子，包含未激活对象。按层级顺序编号：贴图名、贴图名_1、贴图名_2。仅修改节点名，可 Ctrl+Z 一次撤销。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            includeChildren = EditorGUILayout.ToggleLeft("包含所选节点的全部子层", includeChildren);
            propertyOverride = EditorGUILayout.TextField(new GUIContent("指定贴图属性（可选）", "留空自动识别；例如 _TextureSample0 或 _MainTex。指定后找不到该贴图则跳过。"), propertyOverride);
            if (EditorGUI.EndChangeCheck()) Refresh();
            EditorGUILayout.HelpBox("自动：第一份粒子材质的 _TextureSample0 → Shader 主贴图 → _BaseMap / _MainTex → 第一张已赋值贴图。使用 Project 内贴图资源；拖尾材质不参与。改名可能影响 Animation 路径或按名称查找节点的脚本，请检查这些绑定。", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新预览", GUILayout.Width(100))) Refresh();
                int changes = rows.Count(r => r.Texture != null && r.Before != r.After);
                EditorGUILayout.LabelField($"粒子 {rows.Count}  /  待改名 {changes}  /  跳过 {rows.Count(r => r.Texture == null)}");
                using (new EditorGUI.DisabledScope(changes == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("应用批量命名", GUILayout.Width(130))) Apply();
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
            if (rows.Count == 0) EditorGUILayout.HelpBox("请在 Hierarchy 中选择粒子或特效父节点。Project 中的 Prefab 请先双击进入编辑模式。", MessageType.Info);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (var row in rows)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(row.Target, typeof(GameObject), true, GUILayout.Width(180));
                            EditorGUILayout.LabelField(new GUIContent(row.Before + " → " + (row.After ?? "（跳过）"), row.Path));
                            EditorGUILayout.ObjectField(row.Texture, typeof(Texture), false, GUILayout.Width(160));
                        }
                        EditorGUILayout.LabelField(row.Source, EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void Refresh()
        {
            rows.Clear();
            rows.AddRange(BuildRows());
            Repaint();
        }

        private List<Row> BuildRows()
        {
            return BuildRows(Selection.gameObjects, includeChildren, propertyOverride);
        }

        private static List<Row> BuildRows(IEnumerable<GameObject> selection, bool includeChildren, string propertyOverride)
        {
            var particles = new HashSet<ParticleSystem>();
            foreach (var go in selection)
            {
                if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
                if (includeChildren)
                    foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) particles.Add(ps);
                else if (go.TryGetComponent<ParticleSystem>(out var ps)) particles.Add(ps);
            }
            var list = new List<Row>();
            foreach (var ps in particles.OrderBy(p => SortKey(p.transform), StringComparer.Ordinal))
            {
                var row = new Row { Target = ps.gameObject, Before = ps.name, Path = HierarchyPath(ps.transform) };
                row.Texture = FindTexture(ps.GetComponent<ParticleSystemRenderer>(), propertyOverride, out row.Source);
                list.Add(row);
            }
            // Reserve names of unaffected siblings; also ensure names are unique across this batch.
            var changing = new HashSet<GameObject>(list.Where(r => r.Texture != null).Select(r => r.Target));
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in list.Where(r => r.Texture != null))
            {
                var go = row.Target;
                IEnumerable<GameObject> siblings = go.transform.parent != null
                    ? go.transform.parent.Cast<Transform>().Select(t => t.gameObject)
                    : go.scene.GetRootGameObjects();
                foreach (var sibling in siblings)
                    if (!changing.Contains(sibling)) used.Add(sibling.name);
            }
            foreach (var row in list.Where(r => r.Texture != null))
            {
                string basename = row.Texture.name;
                string candidate = basename;
                int suffix = 1;
                while (!used.Add(candidate)) candidate = basename + "_" + suffix++;
                row.After = candidate;
            }
            return list;
        }

        private static Texture FindTexture(ParticleSystemRenderer renderer, string propertyOverride, out string source)
        {
            source = "跳过：没有粒子 Renderer 或第一份材质。";
            if (renderer == null || renderer.sharedMaterial == null) return null;
            var mat = renderer.sharedMaterial;
            var shader = mat.shader;
            if (shader == null) { source = "跳过：材质没有 Shader。"; return null; }
            var properties = new List<string>();
            string requested = propertyOverride.Trim();
            if (requested.Length != 0) properties.Add(requested);
            else
            {
                properties.Add("_TextureSample0");
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture &&
                        (shader.GetPropertyFlags(i) & ShaderPropertyFlags.MainTexture) != 0)
                        properties.Add(shader.GetPropertyName(i));
                properties.Add("_BaseMap");
                properties.Add("_MainTex");
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                        properties.Add(shader.GetPropertyName(i));
            }
            foreach (string property in properties.Distinct())
            {
                int index = shader.FindPropertyIndex(property);
                if (index < 0 || shader.GetPropertyType(index) != ShaderPropertyType.Texture) continue;
                var texture = mat.GetTexture(property);
                if (texture == null || string.IsNullOrEmpty(texture.name)) continue;
                string path = AssetDatabase.GetAssetPath(texture);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                source = mat.name + " / " + property + " / " + path;
                return texture;
            }
            source = "跳过：" + mat.name + " 没有可用的贴图资源" + (requested.Length == 0 ? "。" : "（" + requested + "）。");
            return null;
        }

        private void Apply()
        {
            // Revalidate preview: edits to materials/hierarchy must never silently change the plan.
            var current = BuildRows();
            if (current.Count != rows.Count || current.Where((r, i) => r.Target != rows[i].Target ||
                r.Before != rows[i].Before || r.After != rows[i].After || r.Texture != rows[i].Texture || r.Source != rows[i].Source).Any())
            {
                Refresh();
                result = "选择、层级或材质已变化，预览已更新。请检查后再次应用。";
                return;
            }
            int count = ApplyRows(current);
            result = $"已改名 {count} 个粒子。Ctrl+Z 可撤销；请按正常流程保存场景或 Prefab。";
            Refresh();
        }

        private static int ApplyRows(IEnumerable<Row> current)
        {
            var changes = current.Where(r => r.Target != null && r.Texture != null && r.Before != r.After).ToArray();
            if (changes.Length == 0) return 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("粒子按贴图批量命名");
            Undo.RecordObjects(changes.Select(r => (UnityEngine.Object)r.Target).ToArray(), "粒子按贴图批量命名");
            foreach (var row in changes)
            {
                row.Target.name = row.After;
                if (PrefabUtility.IsPartOfPrefabInstance(row.Target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(row.Target);
                EditorSceneManager.MarkSceneDirty(row.Target.scene);
            }
            Undo.CollapseUndoOperations(group);
            return changes.Length;
        }

        private static string SortKey(Transform t)
        {
            string key = "";
            for (var node = t; node != null; node = node.parent) key = node.GetSiblingIndex().ToString("D10") + "/" + key;
            return t.gameObject.scene.handle.ToString("D10") + "/" + key;
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
