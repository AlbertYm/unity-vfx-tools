#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Install in Assets/Editor. No component needs to be added to the character.
public sealed class TimelineIntEventPreview : EditorWindow
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private AnimationTrack track;
    [SerializeField] private MonoBehaviour receiver;
    [SerializeField] private TimelineFacePreviewConfig faceConfig;
    [SerializeField] private string functionName = "OnChangeFace";
    [SerializeField] private int resetId;
    private bool running, queued;
    private int? applied;
    private double lastTime = double.NaN;
    private string status = "选择 Director 和角色动画轨道，然后查找接收组件。";
    private MethodInfo method;
    private TimelineAsset timeline;
    private sealed class FacePreview
    {
        public SkinnedMeshRenderer renderer;
        public Material[] original;
        public Texture2D[] textures;
        public TimelineFacePreviewConfig.Binding binding;
        public readonly List<Material> temporary = new List<Material>();
        public readonly List<bool> sequences = new List<bool>();
    }
    private readonly List<FacePreview> facePreviews = new List<FacePreview>();
    [SerializeField] private bool embedded;
    [SerializeField] private bool expanded = true;
    [SerializeField] private List<TimelineIntEventPreview> entries = new List<TimelineIntEventPreview>();
    private Vector2 scroll;
    private readonly Dictionary<AnimationClip, AnimationEvent[]> events =
        new Dictionary<AnimationClip, AnimationEvent[]>();

    [MenuItem("Tools/Timeline/Int Event Preview")]
    private static void Open() { GetWindow<TimelineIntEventPreview>("事件预览"); }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }
    private void OnDisable()
    {
        Stop();
        if (!embedded)
            foreach (var entry in entries) if (entry) entry.Stop();
        EditorApplication.update -= Tick;
        EditorApplication.delayCall -= Sample;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
    }
    private void OnPlayMode(PlayModeStateChange state) { Stop(); }
    private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) { Stop(); }

    private void OnDestroy()
    {
        if (!embedded)
            foreach (var entry in entries) if (entry) DestroyImmediate(entry);
    }
    private void OnInspectorUpdate() { if (!embedded) Repaint(); }

    private void OnGUI()
    {
        if (embedded) { DrawSingle(); return; }
        entries.RemoveAll(entry => !entry);
        int active = running ? 1 : 0;
        foreach (var entry in entries) if (entry.running) active++;
        EditorGUILayout.LabelField("多轨道表情预览", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("已配置 " + (entries.Count + 1) + " 条 | 正在预览 " + active + " 条");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("一键开启全部"))
                {
                    if (!running) StartPreview();
                    foreach (var entry in entries) if (!entry.running) entry.StartPreview();
                }
            if (GUILayout.Button("一键停止全部并恢复"))
            { Stop(); foreach (var entry in entries) entry.Stop(); }
        }
        if (GUILayout.Button("添加轨道配置"))
        {
            var entry = CreateInstance<TimelineIntEventPreview>();
            entry.embedded = true;
            entry.hideFlags = HideFlags.HideAndDontSave;
            entry.director = director;
            entry.functionName = functionName;
            entries.Add(entry);
        }
        EditorGUILayout.HelpBox("每个折叠项配置一条轨道。批量开启会跳过已运行项；失败项显示错误，不影响其他有效轨道。", MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawEntry(this, 1, false);
        for (int i = 0; i < entries.Count; i++)
            if (DrawEntry(entries[i], i + 2, true))
            {
                var removed = entries[i];
                removed.Stop(); entries.RemoveAt(i); DestroyImmediate(removed); i--;
            }
        EditorGUILayout.EndScrollView();
    }
    private bool DrawEntry(TimelineIntEventPreview entry, int number, bool removable)
    {
        bool remove = false;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string label = "轨道 " + number + " · " + (entry.track ? entry.track.name : "未配置") +
                    (entry.running ? " · 预览中" : " · 已停止");
                entry.expanded = EditorGUILayout.Foldout(entry.expanded, label, true);
                if (removable && GUILayout.Button("移除", GUILayout.Width(50))) remove = true;
            }
            if (entry.expanded) entry.DrawSingle();
            else EditorGUILayout.LabelField(entry.status, EditorStyles.wordWrappedLabel);
        }
        return remove;
    }
    private void DrawSingle()
    {
        EditorGUILayout.HelpBox("读取已有 int 动画事件，在编辑模式拖动 Timeline 时应用最后一个有效编号。仅用于可重复调用的状态切换。", MessageType.Info);
        using (new EditorGUI.DisabledScope(running))
        {
            director = (PlayableDirector)EditorGUILayout.ObjectField("Timeline Director", director, typeof(PlayableDirector), true);
            if (GUILayout.Button("使用当前 Timeline 的 Director"))
            {
                director = TimelineEditor.inspectedDirector;
                track = null; receiver = null; faceConfig = null;
                status = director ? "已取得 Director，请选择角色轨道。" : "请先在 Timeline 中打开场景里的 Timeline 实例。";
            }
            DrawTracks();
            if (GUILayout.Button("使用 Timeline 当前选中的动画片段"))
            {
                var selected = TimelineEditor.selectedClip;
                var selectedTrack = selected != null ? selected.GetParentTrack() as AnimationTrack : null;
                var currentDirector = TimelineEditor.inspectedDirector;
                if (selectedTrack && currentDirector)
                {
                    director = currentDirector;
                    track = selectedTrack;
                    receiver = null;
                    faceConfig = null;
                    status = "已选择片段：" + selected.displayName + "。请自动查找表情配置后开始预览。";
                }
                else status = "请先在 Timeline 中单击 角色动画片段 这样的动画片段矩形，再点击此按钮。";
            }
            functionName = EditorGUILayout.TextField("事件函数", functionName);
            receiver = (MonoBehaviour)EditorGUILayout.ObjectField("接收组件", receiver, typeof(MonoBehaviour), true);
            if (GUILayout.Button("从轨道绑定角色查找接收组件")) FindReceiver();
            faceConfig = (TimelineFacePreviewConfig)EditorGUILayout.ObjectField("表情配置资产", faceConfig, typeof(TimelineFacePreviewConfig), false);
            if (GUILayout.Button("无接收组件：自动查找表情配置")) FindFaceConfig();
            resetId = EditorGUILayout.IntField("初始 / 停止恢复编号", resetId);
        }
        EditorGUILayout.HelpBox("有接收组件时调用原函数；无接收组件时读取配置资产 的表情贴图，使用临时材质预览并恢复原材质。", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button(running ? "停止预览并恢复" : "开始事件预览"))
            { if (running) Stop(); else StartPreview(); }
        }
        if (running && GUILayout.Button("重新读取事件并刷新当前帧"))
        { events.Clear(); applied = null; lastTime = double.NaN; }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(running ? "状态：预览已开启" : "状态：预览未开启", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.LabelField("关闭窗口或保存场景会停止预览；无组件模式恢复原材质。", EditorStyles.wordWrappedLabel);
    }

    private void DrawTracks()
    {
        var asset = director ? director.playableAsset as TimelineAsset : null;
        if (!asset) { EditorGUILayout.LabelField("请指定带 TimelineAsset 的 Director。"); return; }
        var tracks = new List<AnimationTrack>();
        var labels = new List<string>();
        foreach (var root in asset.GetRootTracks()) CollectTracks(root, "", tracks, labels);
        if (tracks.Count == 0) { EditorGUILayout.LabelField("没有 Animation Track。"); return; }
        int oldIndex = tracks.IndexOf(track);
        int index = EditorGUILayout.Popup("角色动画轨道", oldIndex < 0 ? 0 : oldIndex, labels.ToArray());
        if (oldIndex != index) { track = tracks[index]; receiver = null; faceConfig = null; }
    }
    private static void CollectTracks(TrackAsset item, string prefix, List<AnimationTrack> tracks, List<string> labels)
    {
        string path = prefix + item.name;
        var animation = item as AnimationTrack;
        if (animation)
        {
            bool muted = false;
            for (TrackAsset ancestor = animation; ancestor; ancestor = ancestor.parent as TrackAsset)
                if (ancestor.muted) muted = true;
            var names = new List<string>();
            foreach (var clip in animation.GetClips()) names.Add(clip.displayName);
            if (names.Count == 0 && animation.infiniteClip) names.Add(animation.infiniteClip.name);
            tracks.Add(animation);
            labels.Add(path + (muted ? " [静音]" : " [启用]") + " — " + string.Join(", ", names.ToArray()));
        }
        foreach (var child in item.GetChildTracks()) CollectTracks(child, path + "/", tracks, labels);
    }
    private static MethodInfo ResolveMethod(MonoBehaviour component, string name)
    {
        if (!component || string.IsNullOrEmpty(name)) return null;
        for (Type type = component.GetType(); type != null; type = type.BaseType)
        {
            var result = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(int) }, null);
            if (result != null && result.ReturnType == typeof(void)) return result;
        }
        return null;
    }
    private void FindReceiver()
    {
        if (!director || !track) { status = "请先选择 Director 和动画轨道。"; return; }
        UnityEngine.Object bound = director.GetGenericBinding(track);
        // Override animation tracks can inherit their parent's binding.
        for (TrackAsset parent = track.parent as TrackAsset; !bound && parent; parent = parent.parent as TrackAsset)
            bound = director.GetGenericBinding(parent);
        var component = bound as Component;
        var go = bound as GameObject;
        if (component) go = component.gameObject;
        if (!go) { status = "轨道没有有效角色绑定，请先在 Timeline 绑定 Animator。"; return; }
        var candidates = new List<MonoBehaviour>();
        foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (ResolveMethod(script, functionName) != null) candidates.Add(script);
        if (candidates.Count == 1)
        { receiver = candidates[0]; status = "找到：" + receiver.GetType().Name + "." + functionName + "(int)"; }
        else if (candidates.Count == 0)
            status = "绑定对象及其子对象中未找到 void " + functionName + "(int)。可手动拖入父对象或其他对象上的接收组件。";
        else
        {
            var menu = new GenericMenu();
            foreach (var candidate in candidates)
            {
                var captured = candidate;
                menu.AddItem(new GUIContent(candidate.gameObject.name + "/" + candidate.GetType().Name + " #" + candidate.GetInstanceID()), false,
                    () => { receiver = captured; status = "已选择接收组件。"; Repaint(); });
            }
            menu.ShowAsContext();
        }
    }
    private void StartPreview()
    {
        timeline = director ? director.playableAsset as TimelineAsset : null;
        method = ResolveMethod(receiver, functionName);
        if (!timeline || !track)
        { status = "请先选择 Director 和角色动画轨道。"; return; }
        var targetObject = BoundObject();
        foreach (var other in Resources.FindObjectsOfTypeAll<TimelineIntEventPreview>())
        {
            if (other == this || !other.running) continue;
            if (receiver && receiver == other.receiver)
            { status = "另一个预览窗口正在使用同一个接收组件，请先停止那个窗口。"; return; }
            var otherObject = other.BoundObject();
            if (!targetObject || !otherObject) continue;
            var targets = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var otherTargets = otherObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var target in targets)
                foreach (var otherTarget in otherTargets)
                    if (target == otherTarget)
                    { status = "另一窗口的轨道控制了同一个模型，不能同时决定它的表情。独立角色的轨道可以分别开启，窗口数量不设固定上限。"; return; }
        }
        if (receiver && method == null)
        { status = "选中的组件没有 void " + functionName + "(int)，请清空接收组件后使用配置资产。"; return; }
        if (receiver && (EditorUtility.IsPersistent(receiver) || !receiver.gameObject.scene.IsValid()))
        { status = "接收组件必须来自场景中的角色实例，不能使用 Project 中的 Prefab 资产。"; return; }
        if (!receiver)
        {
            try
            {
                if (functionName != "OnChangeFace") throw new InvalidOperationException("无组件预览仅支持 OnChangeFace 表情事件。");
                if (!faceConfig) FindFaceConfig();
                BuildFacePreview();
            }
            catch (Exception exception) { RestoreMaterials(); status = exception.Message; return; }
        }
        events.Clear(); applied = null; lastTime = double.NaN; running = true;
        if (!embedded) titleContent = new GUIContent("多轨道事件预览");
        status = "请开启 Timeline 的 Preview，再拖动时间轴。";
    }
    private void Tick()
    {
        if (!running || queued) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !director || (!receiver && facePreviews.Count == 0) || !track || director.playableAsset != timeline)
        { Stop(); return; }
        if (!double.IsNaN(lastTime) && Math.Abs(director.time - lastTime) < 1e-9) return;
        // Run after the editor update that evaluates Timeline; do not evaluate the director ourselves.
        queued = true;
        EditorApplication.delayCall += Sample;
    }
    private bool IsMuted()
    {
        for (TrackAsset item = track; item; item = item.parent as TrackAsset)
            if (item.muted) return true;
        return false;
    }
    private string MutedTrackName()
    {
        for (TrackAsset item = track; item; item = item.parent as TrackAsset)
            if (item.muted) return item.name;
        return "";
    }
    private void Sample()
    {
        queued = false;
        if (!running || !director || (!receiver && facePreviews.Count == 0) || !track) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || director.playableAsset != timeline) { Stop(); return; }
        lastTime = director.time;
        try
        {
            AnimationClip source = null;
            double localTime = 0;
            if (!IsMuted())
            {
                TimelineClip selected = null;
                int count = 0;
                foreach (var clip in track.GetClips())
                {
                    if (!(clip.asset is AnimationPlayableAsset)) continue;
                    if (lastTime >= clip.start && lastTime < clip.end)
                    { selected = clip; count++; }
                }
                if (count > 1)
                {
                    Apply(resetId);
                    status = "当前处于两个动画片段重叠区：此版本不决定混合时的表情归属，已使用恢复编号。";
                    Repaint(); return;
                }
                if (selected == null)
                    foreach (var clip in track.GetClips())
                        if (clip.asset is AnimationPlayableAsset && clip.IsExtrapolatedTime(lastTime))
                        { selected = clip; break; }
                if (selected != null)
                {
                    var asset = (AnimationPlayableAsset)selected.asset;
                    source = asset.clip;
                    localTime = selected.ToLocalTime(lastTime);
                    if (source && source.length > 0)
                    {
                        bool loop = asset.loop == AnimationPlayableAsset.LoopMode.On ||
                            (asset.loop == AnimationPlayableAsset.LoopMode.UseSourceAsset && source.isLooping);
                        localTime = loop ? ((localTime % source.length) + source.length) % source.length
                            : Math.Max(0, Math.Min(source.length, localTime));
                    }
                }
                else if (!track.inClipMode && track.infiniteClip)
                {
                    source = track.infiniteClip;
                    localTime = Math.Max(0, Math.Min(source.length, lastTime));
                }
            }
            int id = resetId;
            double eventTime = double.NegativeInfinity;
            int matches = 0;
            if (source)
            {
                AnimationEvent[] entries;
                if (!events.TryGetValue(source, out entries))
                { entries = AnimationUtility.GetAnimationEvents(source); events.Add(source, entries); }
                foreach (var entry in entries)
                {
                    if (entry.functionName != functionName) continue;
                    matches++;
                    if (entry.time <= localTime + 1e-6 && entry.time >= eventTime)
                    { eventTime = entry.time; id = entry.intParameter; }
                }
            }
            Apply(id);
            status = source ? "动画：" + source.name + "\n动画时间：" + localTime.ToString("F3") +
                " 秒 | 匹配事件：" + matches + " | 当前 Int：" + id +
                (double.IsNegativeInfinity(eventTime) ? "（初始编号）" : "\n生效事件时间：" + eventTime.ToString("F3") + " 秒")
                : IsMuted() ? "所选轨道被静音：" + MutedTrackName() + "。请停止预览，选择实际播放的动画片段所在轨道。"
                : "所选轨道在 Timeline " + lastTime.ToString("F3") + " 秒没有有效动画片段，使用初始编号：" + id;
        }
        catch (Exception exception)
        {
            running = false; applied = null;
            RestoreMaterials();
            Exception actual = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
            status = "接收函数执行失败，预览已暂停：" + actual.Message + "\n请查看 Console。若依赖运行时初始化，需要调整接收函数。";
            Debug.LogException(actual, receiver);
        }
        Repaint();
    }
    private void Apply(int id)
    {
        if (applied == id) return;
        if (receiver) method.Invoke(receiver, new object[] { id });
        else
        {
            foreach (var preview in facePreviews)
                if (id < 0 || id >= preview.textures.Length || !preview.textures[id])
                    throw new InvalidOperationException("表情编号 " + id + " 超出配置范围或贴图为空。");
            foreach (var preview in facePreviews)
            {
                var texture = preview.textures[id];
                for (int i = 0; i < preview.temporary.Count; i++)
                {
                    var material = preview.temporary[i];
                    material.SetTexture(preview.binding.textureProperty, texture);
                    if (!string.IsNullOrEmpty(preview.binding.winkKeyword))
                    {
                        if (texture.width != texture.height && !preview.sequences[i]) material.EnableKeyword(preview.binding.winkKeyword);
                        else material.DisableKeyword(preview.binding.winkKeyword);
                    }
                    if (preview.sequences[i])
                    {
                        if (id == 0) material.EnableKeyword(preview.binding.sequenceKeyword);
                        else material.DisableKeyword(preview.binding.sequenceKeyword);
                    }
                }
            }
        }
        applied = id;
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
    private void Stop()
    {
        EditorApplication.delayCall -= Sample;
        queued = false;
        bool restore = running || applied.HasValue;
        running = false;
        if (restore && receiver && method != null)
        {
            try { method.Invoke(receiver, new object[] { resetId }); }
            catch (Exception exception) { Debug.LogWarning("表情恢复失败：" + (exception.InnerException ?? exception).Message, receiver); }
        }
        applied = null; lastTime = double.NaN;
        RestoreMaterials();
        SceneView.RepaintAll();
        status = receiver ? "预览已停止；恢复目标编号：" + resetId + "。" : "预览已停止，已恢复原材质。";
    }

    private GameObject BoundObject()
    {
        if (!director || !track) return null;
        UnityEngine.Object bound = director.GetGenericBinding(track);
        for (TrackAsset parent = track.parent as TrackAsset; !bound && parent; parent = parent.parent as TrackAsset)
            bound = director.GetGenericBinding(parent);
        var component = bound as Component;
        return component ? component.gameObject : bound as GameObject;
    }
    private void FindFaceConfig()
    {
        var go = BoundObject();
        if (!go) { status = "请先绑定角色 Animator。"; return; }
        var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var candidates = new List<TimelineFacePreviewConfig>();
        foreach (var guid in AssetDatabase.FindAssets("t:TimelineFacePreviewConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<TimelineFacePreviewConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (!config || config.bindings == null || config.bindings.Length == 0) continue;
            bool match = true;
            foreach (var binding in config.bindings)
            {
                int count = 0;
                foreach (var renderer in renderers)
                    if (binding.mesh ? renderer.sharedMesh == binding.mesh : renderer.name == binding.rendererName) count++;
                if (count != 1) { match = false; break; }
            }
            if (match) candidates.Add(config);
        }
        if (candidates.Count == 1)
        { faceConfig = candidates[0]; status = "已找到唯一匹配的表情配置资产。"; }
        else status = "匹配配置数量：" + candidates.Count + "。请手动选择表情配置资产；首次使用需创建配置。";
    }
    private void BuildFacePreview()
    {
        RestoreMaterials();
        var go = BoundObject();
        if (!go || !go.scene.IsValid() || EditorUtility.IsPersistent(go))
            throw new InvalidOperationException("动画轨道必须绑定场景角色实例。");
        if (!faceConfig || faceConfig.bindings == null || faceConfig.bindings.Length == 0)
            throw new InvalidOperationException("请创建并指定有效的表情配置资产。");
        var targets = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var binding in faceConfig.bindings)
        {
            var matches = new List<SkinnedMeshRenderer>();
            foreach (var target in targets)
                if (binding.mesh ? target.sharedMesh == binding.mesh : target.name == binding.rendererName) matches.Add(target);
            if (matches.Count != 1) throw new InvalidOperationException("配置模型未唯一匹配，请检查 Mesh 或 Renderer Name。");
            foreach (var existing in facePreviews)
                if (existing.renderer == matches[0]) throw new InvalidOperationException("同一模型存在重复配置。");
            var preview = new FacePreview { renderer = matches[0], original = matches[0].sharedMaterials,
                textures = binding.textures, binding = binding };
            if (preview.textures == null || preview.textures.Length == 0)
                throw new InvalidOperationException("配置没有表情贴图。");
            if (binding.materialIndex < 0 || binding.materialIndex >= preview.original.Length)
                throw new InvalidOperationException("Material Index 超出模型材质范围。");
            var original = preview.original[binding.materialIndex];
            if (!original || !original.HasProperty(binding.textureProperty))
                throw new InvalidOperationException("材质缺少配置的 Texture Property。");
            facePreviews.Add(preview);
            var temporaryArray = (Material[])preview.original.Clone();
            var temporary = new Material(original) { hideFlags = HideFlags.HideAndDontSave };
            temporary.name = original.name + " (Timeline Preview)";
            temporaryArray[binding.materialIndex] = temporary;
            preview.temporary.Add(temporary);
            preview.sequences.Add(!string.IsNullOrEmpty(binding.sequenceKeyword) && original.IsKeywordEnabled(binding.sequenceKeyword));
            preview.renderer.sharedMaterials = temporaryArray;
        }
    }
    private void RestoreMaterials()
    {
        foreach (var preview in facePreviews)
        {
            if (preview.renderer) preview.renderer.sharedMaterials = preview.original;
            foreach (var material in preview.temporary)
                if (material) DestroyImmediate(material);
        }
        facePreviews.Clear();
    }
}
#endif
