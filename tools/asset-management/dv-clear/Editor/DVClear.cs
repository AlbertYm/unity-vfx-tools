#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 使用命名空间防止与 GameSetupWindow.cs 中的类名冲突
namespace EffectTools.Cleanup
{
    public class DVClear : EditorWindow
    {
        private const int MaxReferenceDepth = 8;

        private Object selectedTarget;
        private List<AssetResult> scanResults = new List<AssetResult>();
        private Vector2 scrollPos;
        private bool hideUsed = false;
        private string searchFilter = "";
        private bool isSearchingDependencies = false; // 默认为特效师最常用的“反向查找”

        private int detailIndex = -1;
        private int resultPage;
        private const int PageSize = 100;

        // 引用树状态。节点展开状态与扫描缓存分离，收起详情后再次打开仍保留原位置。
        private readonly HashSet<string> expandedReferenceNodes = new HashSet<string>();
        private readonly HashSet<string> selectedReferencePaths = new HashSet<string>();
        private readonly Dictionary<string, List<string>> nestedReferenceCache =
            new Dictionary<string, List<string>>();
        private DVClearIndex index;
        private DVClearIndex buildingIndex;
        private bool indexDirty = true;
        private bool scanBusy;
        private int projectRevision, scanRevision;
        private List<string> pendingDeletion;
        private string status = "请选择资源或文件夹。首次扫描会建立全项目引用索引。";

        // 定义结果数据结构
        private struct AssetResult
        {
            public string guid;
            public string path;
            public string name;
            public System.Type type;
            public int refCount;
            public bool isSelected;
            public List<string> references;
        }

        [MenuItem("Effect Tools/DV Clear")]
        public static void ShowWindow()
        {
            // 使用完整路径获取窗口，确保唯一性
            DVClear window = GetWindow<DVClear>("DV Clear - 资源整理助手");
            window.minSize = new Vector2(760, 620);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUI.BeginDisabledGroup(scanBusy);
            // --- 顶部控制区 ---
            EditorGUILayout.BeginVertical("box");
            GUILayout.Space(5);
            GUILayout.Label("DV Clear | 资源引用与清理工具", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            Object nextTarget = EditorGUILayout.ObjectField("目标(文件夹/文件)", selectedTarget, typeof(Object), false);
            if (nextTarget != selectedTarget)
            {
                selectedTarget = nextTarget;
                scanResults.Clear(); selectedReferencePaths.Clear(); detailIndex = -1;
                if (index != null && index.Completed && !indexDirty) BuildResults();
            }
            if (GUILayout.Button("开始扫描", GUILayout.Width(100), GUILayout.Height(20)))
            {
                ScanAssets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            string[] modes = { "查我用了谁 (正向)", "查谁用了我 (反向)" };
            int modeIndex = isSearchingDependencies ? 0 : 1;
            int newModeIndex = GUILayout.Toolbar(modeIndex, modes);
            if (newModeIndex != modeIndex)
            {
                isSearchingDependencies = (newModeIndex == 0);
                if (scanResults.Count > 0)
                {
                    ScanAssets(); // 模式切换时自动刷新
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox(status, indexDirty && index != null ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (scanBusy)
            {
                Rect progress = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progress, buildingIndex == null || buildingIndex.Paths.Length == 0 ? 0 : (float)buildingIndex.Processed / buildingIndex.Paths.Length, "分批扫描中");
                if (GUILayout.Button("取消扫描", GUILayout.Width(90))) CancelScan();
            }
            else
            {
                if (GUILayout.Button("重建索引")) { indexDirty = true; ScanAssets(); }
                EditorGUI.BeginDisabledGroup(index == null);
                if (GUILayout.Button("导出报告 / 诊断")) ExportReport();
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
            // --- 过滤工具栏 ---
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            searchFilter = EditorGUILayout.TextField("搜索:", searchFilter, EditorStyles.toolbarSearchField);
            hideUsed = GUILayout.Toggle(hideUsed, "只看扫描结果为 0", EditorStyles.toolbarButton);
            if (GUILayout.Button("按引用排序", EditorStyles.toolbarButton))
            {
                scanResults = scanResults.OrderBy(r => r.refCount).ToList();
                detailIndex = -1;
            }
            if (GUILayout.Button("收起全部引用树", EditorStyles.toolbarButton, GUILayout.Width(105)))
            {
                expandedReferenceNodes.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                isSearchingDependencies
                    ? "正向模式：数量是 Unity 直接依赖与文本/include 候选的去重总数。可展开追踪间接关系；0 不代表未被使用。"
                    : "反向索引覆盖 Assets、Packages 和 ProjectSettings：合并 Unity 直接依赖、文本及 .meta GUID 候选、Shader include。文本命中可能只是注释或字符串；0 不代表安全可删，代码动态加载仍需人工核对。",
                MessageType.Info);

            int visibleCount = scanResults.Count(IsVisible);
            int pageCount = System.Math.Max(1, (visibleCount + PageSize - 1) / PageSize);
            resultPage = Mathf.Clamp(resultPage, 0, pageCount - 1);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("匹配 " + visibleCount + " 项 · 第 " + (resultPage + 1) + "/" + pageCount + " 页（每页 " + PageSize + " 项）");
            EditorGUI.BeginDisabledGroup(resultPage == 0);
            if (GUILayout.Button("上一页", GUILayout.Width(65))) resultPage--;
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(resultPage >= pageCount - 1);
            if (GUILayout.Button("下一页", GUILayout.Width(65))) resultPage++;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            DrawHeader();

            // --- 列表展示区 ---
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            if (scanResults.Count == 0)
            {
                EditorGUILayout.HelpBox("请拖入文件夹或资源并点击“开始扫描”", MessageType.Info);
            }

            int visibleOrdinal = 0;
            for (int i = 0; i < scanResults.Count; i++)
            {
                AssetResult res = scanResults[i];

                // 过滤逻辑
                if (hideUsed && res.refCount > 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(searchFilter) &&
                    res.path.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int ordinal = visibleOrdinal++;
                if (ordinal < resultPage * PageSize || ordinal >= (resultPage + 1) * PageSize) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                res.isSelected = EditorGUILayout.Toggle(res.isSelected, GUILayout.Width(20));

                // 颜色逻辑：未引用标红，当前选中详情标黄
                GUI.color = (detailIndex == i) ? Color.yellow : (res.refCount == 0 ? new Color(1f, 0.8f, 0.4f) : Color.white);
                if (GUILayout.Button(new GUIContent(res.name, res.path), EditorStyles.label, GUILayout.Width(250)))
                {
                    PingAndSelectAsset(res.path);
                }
                GUI.color = Color.white;

                string typeName = res.type != null ? res.type.Name : "Unknown";
                EditorGUILayout.LabelField(typeName, GUILayout.Width(80));
                EditorGUILayout.LabelField(res.refCount.ToString(), GUILayout.Width(40));

                if (GUILayout.Button(detailIndex == i ? "收起" : "详情", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    detailIndex = (detailIndex == i) ? -1 : i;
                }

                EditorGUILayout.EndHorizontal();

                // 详情展示
                if (detailIndex == i)
                {
                    DrawDetailList(res);
                }

                EditorGUILayout.EndVertical();
                scanResults[i] = res;
            }
            EditorGUILayout.EndScrollView();

            // --- 底部操作区 ---
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(25);
            EditorGUILayout.LabelField("资源名称", GUILayout.Width(250));
            EditorGUILayout.LabelField("类型", GUILayout.Width(80));
            EditorGUILayout.LabelField("引用数", GUILayout.Width(40));
            EditorGUILayout.LabelField("操作");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDetailList(AssetResult res)
        {
            EditorGUILayout.BeginVertical("box");
            if (res.references.Count == 0)
            {
                GUILayout.Label("  (无引用记录)", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                isSearchingDependencies ? "[该资源使用的依赖]:" : "[引用该资源的物体]:",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("收起此树", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                string rootPrefix = res.path + ">";
                expandedReferenceNodes.RemoveWhere(key => key.StartsWith(rootPrefix));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "点击三角可继续追踪下一层引用；点击资源名称可在 Project 与 Inspector 中定位。",
                MessageType.None);

            HashSet<string> branchPaths = new HashSet<string> { res.path };
            List<string> rootReferences = res.references
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path)
                .ToList();

            foreach (string refPath in rootReferences)
            {
                string nodeKey = res.path + ">" + refPath;
                DrawReferenceNode(refPath, nodeKey, 0, branchPaths);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawReferenceNode(
            string assetPath,
            string nodeKey,
            int depth,
            HashSet<string> branchPaths)
        {
            bool isCycle = branchPaths.Contains(assetPath);
            bool reachedDepthLimit = depth >= MaxReferenceDepth;
            List<string> childReferences = isCycle
                ? new List<string>()
                : GetReferencesForPath(assetPath);
            bool hasChildren = childReferences.Count > 0;
            bool isExpanded = expandedReferenceNodes.Contains(nodeKey);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8 + depth * 18);

            bool isSelected = selectedReferencePaths.Contains(assetPath);
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(18));
            if (newSelected != isSelected)
            {
                if (newSelected)
                {
                    selectedReferencePaths.Add(assetPath);
                }
                else
                {
                    selectedReferencePaths.Remove(assetPath);
                }
            }

            bool oldEnabled = GUI.enabled;
            GUI.enabled = !isCycle && !reachedDepthLimit && hasChildren;
            string foldoutText = isExpanded ? "▼" : (hasChildren ? "▶" : "•");
            if (GUILayout.Button(foldoutText, EditorStyles.miniButton, GUILayout.Width(22)))
            {
                if (isExpanded)
                {
                    expandedReferenceNodes.Remove(nodeKey);
                }
                else
                {
                    expandedReferenceNodes.Add(nodeKey);
                }
            }
            GUI.enabled = oldEnabled;

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            GUIContent assetContent = EditorGUIUtility.ObjectContent(asset, asset != null ? asset.GetType() : typeof(Object));
            if (assetContent.image != null)
            {
                GUILayout.Label(assetContent.image, GUILayout.Width(18), GUILayout.Height(18));
            }
            else
            {
                GUILayout.Space(20);
            }

            string[] chain = nodeKey.Split('>');
            string parentPath = chain.Length > 1 ? chain[chain.Length - 2] : "";
            string evidence = index == null ? "" : (isSearchingDependencies ? index.Describe(parentPath, assetPath) : index.Describe(assetPath, parentPath));
            GUILayout.Label(new GUIContent("依据", evidence), EditorStyles.miniLabel, GUILayout.Width(30));
            string displayName = Path.GetFileName(assetPath);
            // linkLabel is internal in early Unity 2019 editors.
            if (GUILayout.Button(new GUIContent(displayName, assetPath), EditorStyles.label))
            {
                PingAndSelectAsset(assetPath);
            }

            GUILayout.FlexibleSpace();
            string typeName = asset != null ? asset.GetType().Name : "Missing";
            EditorGUILayout.LabelField(typeName, GUILayout.Width(90));
            EditorGUILayout.LabelField(
                isCycle ? "循环" : childReferences.Count.ToString(),
                GUILayout.Width(42));
            EditorGUILayout.EndHorizontal();

            if (isCycle)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(38 + depth * 18);
                GUILayout.Label("检测到循环引用，已停止继续展开。", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (reachedDepthLimit && hasChildren)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(38 + depth * 18);
                GUILayout.Label(
                    "已达到最大展开层级 " + MaxReferenceDepth + "，防止引用链过深。",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (!isExpanded)
            {
                return;
            }

            if (!hasChildren)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(38 + depth * 18);
                GUILayout.Label("(没有下一层引用)", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            branchPaths.Add(assetPath);
            foreach (string childPath in childReferences)
            {
                string childNodeKey = nodeKey + ">" + childPath;
                DrawReferenceNode(childPath, childNodeKey, depth + 1, branchPaths);
            }
            branchPaths.Remove(assetPath);
        }

        private List<string> GetReferencesForPath(string assetPath)
        {
            List<string> cached;
            if (nestedReferenceCache.TryGetValue(assetPath, out cached)) return cached;
            var references = index == null ? new List<string>() : index.Links(assetPath, isSearchingDependencies);
            nestedReferenceCache[assetPath] = references;
            return references;
        }

        private static void PingAndSelectAsset(string assetPath)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private List<string> GetSelectedAssetPaths()
        {
            return scanResults
                .Where(result => result.isSelected)
                .Select(result => result.path)
                .Concat(selectedReferencePaths)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal("box");
            if (GUILayout.Button("全选可见项", GUILayout.Width(90)))
            {
                for (int i = 0; i < scanResults.Count; i++)
                {
                    AssetResult result = scanResults[i];
                    if (IsVisible(result)) result.isSelected = true;
                    scanResults[i] = result;
                }
            }

            if (GUILayout.Button("取消全部选择", GUILayout.Width(90)))
            {
                for (int i = 0; i < scanResults.Count; i++)
                {
                    AssetResult result = scanResults[i];
                    result.isSelected = false;
                    scanResults[i] = result;
                }
                selectedReferencePaths.Clear();
            }

            GUILayout.FlexibleSpace();

            List<string> selectedPaths = GetSelectedAssetPaths();
            EditorGUILayout.LabelField("已选: " + selectedPaths.Count, GUILayout.Width(58));

            EditorGUI.BeginDisabledGroup(scanBusy || indexDirty || index == null || !index.Completed || index.Issues.Count > 0 || selectedPaths.Count == 0);
            GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
            string deleteButtonText = selectedPaths.Count > 0
                ? "核验并清理 (" + selectedPaths.Count + ")"
                : "核验并清理";
            if (GUILayout.Button(deleteButtonText, GUILayout.Width(145), GUILayout.Height(22)))
            {
                DeleteSelected();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void OnEnable()
        {
            EditorApplication.update += TickScan;
            EditorApplication.projectChanged += ProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickScan;
            EditorApplication.projectChanged -= ProjectChanged;
            if (scanBusy) { scanBusy = false; indexDirty = true; }
            pendingDeletion = null;
        }

        private void ProjectChanged()
        {
            projectRevision++;
            indexDirty = true;
            if (!scanBusy) status = "工程资源发生变化，结果已过期。请重新扫描。";
            Repaint();
        }

        private void ScanAssets()
        {
            if (scanBusy) return;
            if (selectedTarget == null) { status = "请先选择资源或文件夹。"; return; }
            string target = AssetDatabase.GetAssetPath(selectedTarget);
            if (!(target == "Assets" || target.StartsWith("Assets/") || target.StartsWith("Packages/")))
            { status = "请选择 Project 中的资源或文件夹。"; return; }
            if (index != null && index.Completed && !indexDirty)
            { BuildResults(); return; }
            StartIndex();
        }

        private void StartIndex()
        {
            scanBusy = true;
            scanRevision = projectRevision;
            indexDirty = true;
            status = "正在建立 Assets、Packages 和 ProjectSettings 的引用索引……";
            try { buildingIndex = new DVClearIndex(); buildingIndex.Initialize(); }
            catch (System.Exception e) { scanBusy = false; pendingDeletion = null; status = "索引初始化失败：" + e.Message; }
            Repaint();
        }

        private void TickScan()
        {
            if (!scanBusy) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            double until = EditorApplication.timeSinceStartup + 0.008;
            try
            {
                do
                {
                    if (!buildingIndex.Step())
                    {
                        index = buildingIndex;
                        buildingIndex = null;
                        scanBusy = false;
                        indexDirty = projectRevision != scanRevision;
                        BuildResults();
                        var toDelete = pendingDeletion;
                        pendingDeletion = null;
                        if (toDelete != null) ConfirmDeletion(toDelete);
                        Repaint();
                        return;
                    }
                } while (EditorApplication.timeSinceStartup < until);
                status = "扫描 " + buildingIndex.Processed + "/" + buildingIndex.Paths.Length + "：" + buildingIndex.CurrentPath;
            }
            catch (System.Exception e)
            { scanBusy = false; indexDirty = true; buildingIndex = null; pendingDeletion = null; status = "扫描失败，未生成可用于清理的完整结果：" + e.Message; }
            Repaint();
        }

        private void BuildResults()
        {
            scanResults.Clear();
            resultPage = 0;
            nestedReferenceCache.Clear();
            expandedReferenceNodes.Clear();
            selectedReferencePaths.Clear();
            detailIndex = -1;
            if (index == null || selectedTarget == null) return;
            string target = AssetDatabase.GetAssetPath(selectedTarget);
            IEnumerable<string> paths = AssetDatabase.IsValidFolder(target)
                ? index.Paths.Where(p => p.StartsWith(target.TrimEnd('/') + "/", System.StringComparison.Ordinal))
                : new[] { target };
            foreach (string path in paths)
            {
                if (!(path.StartsWith("Assets/") || path.StartsWith("Packages/"))) continue;
                var refs = index.Links(path, isSearchingDependencies);
                scanResults.Add(new AssetResult { guid = AssetDatabase.AssetPathToGUID(path), path = path,
                    name = Path.GetFileName(path), type = AssetDatabase.GetMainAssetTypeAtPath(path), references = refs, refCount = refs.Count });
            }
            status = (indexDirty ? "扫描期间工程有变化，结果已过期；请重扫。" : "扫描完成。") +
                " 索引 " + index.Paths.Length + " 个文件，目标 " + scanResults.Count + " 个；读取问题 " + index.Issues.Count +
                " 项，未解析 GUID 候选 " + index.Unresolved.Count + " 项。";
        }

        private void CancelScan()
        {
            scanBusy = false;
            buildingIndex = null;
            pendingDeletion = null;
            indexDirty = true;
            status = "已取消。未完成的索引已丢弃；旧结果仅供参考，不能用于清理。";
        }

        private bool IsVisible(AssetResult result)
        {
            return !(hideUsed && result.refCount > 0) && (string.IsNullOrEmpty(searchFilter) ||
                result.path.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ExportReport()
        {
            if (index == null) return;
            string path = EditorUtility.SaveFilePanel("导出 DV Clear 报告", "", "DVClear-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                using (var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(true)))
                {
                    writer.WriteLine("状态,方向,目标资源,关联资源,依据");
                    string state = indexDirty ? "过期/取消，不可用于清理" : index.Issues.Count > 0 ? "完成但存在读取问题" : "扫描完成，动态加载未覆盖";
                    foreach (var row in scanResults)
                    {
                        if (!IsVisible(row)) continue;
                        if (row.references.Count == 0) writer.WriteLine(Csv(state) + "," + Csv(isSearchingDependencies ? "正向" : "反向") + "," + Csv(row.path) + ",,扫描范围内无记录");
                        foreach (string other in row.references)
                            writer.WriteLine(Csv(state) + "," + Csv(isSearchingDependencies ? "正向" : "反向") + "," + Csv(row.path) + "," + Csv(other) + "," + Csv(isSearchingDependencies ? index.Describe(row.path, other) : index.Describe(other, row.path)));
                    }
                    foreach (string issue in index.Issues) writer.WriteLine(Csv("读取问题") + ",,,," + Csv(issue));
                    foreach (string unresolved in index.Unresolved) writer.WriteLine(Csv("未解析 GUID 候选") + ",,,," + Csv(unresolved));
                }
                status = "报告已导出：" + path;
            }
            catch (System.Exception e) { status = "导出失败：" + e.Message; }
        }

        private static string Csv(string value)
        {
            value = value ?? "";
            if (value.Length > 0 && "=+-@".IndexOf(value[0]) >= 0) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void DeleteSelected()
        {
            if (scanBusy) return;
            var paths = GetSelectedAssetPaths();
            if (paths.Count == 0) { status = "请先勾选要清理的资源。"; return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { status = "请在编辑模式且导入/编译完成后重试。"; return; }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { status = "资源已刷新，请等待编译完成再核验。"; return; }
            pendingDeletion = paths;
            StartIndex(); // Never authorize removal from a cached or forward-only count.
        }

        internal static List<string> RemovalBlockers(DVClearIndex snapshot, List<string> paths)
        {
            var blockers = new List<string>();
            var selected = new HashSet<string>(paths, System.StringComparer.Ordinal);
            var protectedExtensions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { ".cs", ".dll", ".asmdef", ".asmref", ".shader", ".hlsl", ".cginc", ".compute", ".unity" };
            foreach (string path in paths)
            {
                if (!path.StartsWith("Assets/", System.StringComparison.Ordinal) || AssetDatabase.IsValidFolder(path) || !File.Exists(path) || !File.Exists(path + ".meta"))
                { blockers.Add(path + "：仅允许具有 .meta 的 Assets 内单个资源文件"); continue; }
                string lower = path.ToLowerInvariant();
                if (protectedExtensions.Contains(Path.GetExtension(path)) || lower.Contains("/resources/") || lower.Contains("/streamingassets/") || lower.Contains("/editor/"))
                    blockers.Add(path + "：代码、Shader、场景或特殊加载目录已保护");
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName)) blockers.Add(path + "：设置了 AssetBundle 名称");
                foreach (string reference in snapshot.Links(path, false))
                    if (!selected.Contains(reference)) blockers.Add(path + " ← " + reference);
            }
            return blockers;
        }

        private void ConfirmDeletion(List<string> paths)
        {
            if (indexDirty || index == null || !index.Completed || index.Issues.Count != 0)
            { status = "已停止清理：索引过期、不完整或存在读取问题。可导出报告查看。"; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { status = "请在编辑模式且编译完成后重试。"; return; }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                { status = "已停止清理：存在未保存场景，请先自行保存，再重新核验。"; return; }
            if (Resources.FindObjectsOfTypeAll<Object>().Any(o => o != null && EditorUtility.IsPersistent(o) && EditorUtility.IsDirty(o)))
            { status = "已停止清理：存在未保存的资源修改，请先自行保存。"; return; }
            List<string> blockers = RemovalBlockers(index, paths);
            if (blockers.Count > 0)
            {
                status = "已停止清理：存在外部引用或受保护资源。";
                EditorUtility.DisplayDialog("不能清理所选资源", string.Join("\n", blockers.Take(15).ToArray()) + (blockers.Count > 15 ? "\n……共 " + blockers.Count + " 项" : ""), "知道了");
                return;
            }
            string preview = string.Join("\n", paths.Take(12).ToArray());
            if (paths.Count > 12) preview += "\n……共 " + paths.Count + " 个（以窗口勾选路径为准）";
            string previewFile = Path.GetFullPath("Logs/DVClearCleanupPreview.txt");
            try { Directory.CreateDirectory("Logs"); File.WriteAllLines(previewFile, paths.ToArray()); }
            catch (System.Exception e) { status = "无法生成完整清理清单，已停止：" + e.Message; return; }
            int choice = EditorUtility.DisplayDialogComplex("备份并移入系统回收站？", preview +
                "\n\n已重新扫描，未发现所选集合之外的已知引用；字符串路径、动态加载和外部代码仍可能使用这些资源。\n先在项目根目录 DVClearBackups 备份资源和 .meta，再移入系统回收站。\n完整清单：" + previewFile, "备份并移入回收站", "取消", "查看完整清单");
            if (choice == 2) { EditorUtility.RevealInFinder(previewFile); status = "已生成完整清单，未移动资源；审阅后可再次点击核验并清理。"; return; }
            if (choice != 0)
            { status = "已取消清理，未移动任何资源。"; return; }
            if (indexDirty) { status = "确认期间工程发生变化，请重新核验。"; return; }
            string backup = Path.GetFullPath(Path.Combine("DVClearBackups", System.DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
            var moved = new List<string>();
            var failed = new List<string>();
            try
            {
                status = "正在备份所选资源……";
                foreach (string path in paths)
                    foreach (string file in new[] { path, path + ".meta" })
                    {
                        string dest = Path.Combine(backup, file);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(file, dest, false);
                        if (FileHash(file) != FileHash(dest)) throw new IOException("备份校验失败：" + file);
                    }
                File.WriteAllLines(Path.Combine(backup, "selected-paths.txt"), paths.ToArray());
                File.WriteAllText(Path.Combine(backup, "恢复说明.txt"), "关闭 Unity 后，将本目录 Assets 下需要恢复的文件及其 .meta 放回原工程相同位置。不要覆盖后续创建的文件；路径冲突时先核对。移动结果见 moved.txt 和 failed.txt。");
                if (indexDirty) throw new IOException("备份期间工程发生变化，未开始移动资源，请重新核验。");
                foreach (string path in paths)
                {
                    if (AssetDatabase.MoveAssetToTrash(path)) moved.Add(path); else failed.Add(path);
                    File.WriteAllLines(Path.Combine(backup, "moved.txt"), moved.ToArray());
                    File.WriteAllLines(Path.Combine(backup, "failed.txt"), failed.ToArray());
                }
                status = "清理完成：已移入回收站 " + moved.Count + "，失败 " + failed.Count + "。备份：" + backup;
            }
            catch (System.Exception e)
            { status = "清理中止：" + e.Message + "；已移动 " + moved.Count + "。备份：" + backup; Debug.LogWarning(status); }
            finally
            {
                indexDirty = true;
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("DV Clear 清理结果", status, "知道了");
            }
        }

        private static string FileHash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                return System.BitConverter.ToString(sha.ComputeHash(stream));
        }

        [System.Serializable]
        private class DiagnosticReport
        {
            public string unity, time, error;
            public int files, edges;
            public double seconds;
            public string[] issues, unresolvedGuidCandidates, nasShaderDependencies;
        }

        // Optional batch entry point: read dependencies and write a diagnostic report only.
        public static void WriteProjectDiagnostics()
        {
            var report = new DiagnosticReport { unity = Application.unityVersion, time = System.DateTime.Now.ToString("o") };
            double started = EditorApplication.timeSinceStartup;
            try
            {
                var snapshot = new DVClearIndex(); snapshot.Initialize();
                while (snapshot.Step()) { }
                report.files = snapshot.Paths.Length;
                report.edges = snapshot.Forward.Values.Sum(edges => edges.Count);
                report.issues = snapshot.Issues.ToArray();
                report.unresolvedGuidCandidates = snapshot.Unresolved.ToArray();
                report.nasShaderDependencies = snapshot.Links("Assets/Apps/MMO/Bundles/Shaders/Nas_Effect_Uber.shader", true).ToArray();
            }
            catch (System.Exception e) { report.error = e.ToString(); }
            report.seconds = EditorApplication.timeSinceStartup - started;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DVClearProjectAudit.json", JsonUtility.ToJson(report, true));
            if (!string.IsNullOrEmpty(report.error)) Debug.LogError("DVCLEAR_PROJECT_AUDIT_FAILED " + report.error);
            else Debug.Log("DVCLEAR_PROJECT_AUDIT_COMPLETE files=" + report.files + " issues=" + report.issues.Length);
        }

    }
}
#endif
