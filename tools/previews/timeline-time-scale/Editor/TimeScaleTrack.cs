using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// 1. 轨道定义
[TrackColor(0.2f, 0.8f, 0.5f)]
[TrackClipType(typeof(TimeScaleClip))]
public class TimeScaleTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimeScaleMixerBehaviour>.Create(graph, inputCount);
    }
}

// 2. Clip 资产 (【核心修改】：参数移到这里，避开 Timeline 的动画曲线绑定)
[Serializable]
public class TimeScaleClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("时间缩放值：1为正常，0.2为子弹时间慢动作")]
    [Range(0.01f, 5f)]
    public float timeScale = 0.2f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<TimeScaleBehaviour>.Create(graph);
        TimeScaleBehaviour clone = playable.GetBehaviour();

        // 将 Inspector 面板上设置的值，传递给后台逻辑
        clone.timeScale = timeScale;

        return playable;
    }
}

// 3. 数据容器 (保持干净，不暴露给面板)
public class TimeScaleBehaviour : PlayableBehaviour
{
    // 这里不再加 [Serializable] 和公开特性
    public float timeScale = 1f;
}

// 4. 混合与逻辑执行 (保持上一版的安全检查)
public class TimeScaleMixerBehaviour : PlayableBehaviour
{
    private float defaultTimeScale = 1f;

    public override void OnPlayableCreate(Playable playable)
    {
        defaultTimeScale = Time.timeScale;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        float blendedTimeScale = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight > 0f)
            {
                ScriptPlayable<TimeScaleBehaviour> inputPlayable = (ScriptPlayable<TimeScaleBehaviour>)playable.GetInput(i);
                TimeScaleBehaviour input = inputPlayable.GetBehaviour();

                if (input != null)
                {
                    blendedTimeScale += input.timeScale * weight;
                    totalWeight += weight;
                }
            }
        }

        float finalScale = totalWeight > 0f ? blendedTimeScale + (1f - totalWeight) * 1f : 1f;

        // [运行模式]
        if (Application.isPlaying)
        {
            Time.timeScale = finalScale;
        }

        // [编辑模式]
        PlayableGraph graph = playable.GetGraph();
        if (graph.IsValid() && graph.GetRootPlayableCount() > 0)
        {
            if (Application.isPlaying || graph.IsPlaying())
            {
                graph.GetRootPlayable(0).SetSpeed(finalScale);
            }
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (Application.isPlaying)
        {
            Time.timeScale = defaultTimeScale;
        }
        else
        {
            PlayableGraph graph = playable.GetGraph();
            if (graph.IsValid() && graph.GetRootPlayableCount() > 0)
            {
                graph.GetRootPlayable(0).SetSpeed(1f);
            }
        }
    }
}