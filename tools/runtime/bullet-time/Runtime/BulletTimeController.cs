using UnityEngine;

// Only control global time during Play mode.
public class BulletTimeController : MonoBehaviour
{
    [Range(0.001f, 1f)] public float slowMagnitude = 0.1f;
    [Min(0f)] public float slowDuration = 2f;
    [Tooltip("勾选触发一次；结束或取消后自动取消勾选。")]
    public bool enableBulletTime;

    private static BulletTimeController owner;
    private bool active;
    private float originalTimeScale;
    private float originalFixedDeltaTime;
    private double endTime;
    private static double RealtimeNow
    {
        get
        {
#if UNITY_2020_2_OR_NEWER
            return Time.realtimeSinceStartupAsDouble;
#else
            return Time.realtimeSinceStartup;
#endif
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (active)
        {
            if (!enableBulletTime || RealtimeNow >= endTime)
                RestoreTime();
            return;
        }
        if (!enableBulletTime) return;
        if (owner != null && owner != this)
        {
            enableBulletTime = false;
            Debug.LogWarning("另一个 BulletTimeController 正在控制时间，本次触发已忽略。", this);
            return;
        }
        if (float.IsNaN(slowMagnitude) || float.IsInfinity(slowMagnitude) ||
            float.IsNaN(slowDuration) || float.IsInfinity(slowDuration) ||
            slowDuration <= 0f || Time.timeScale <= 0f)
        {
            enableBulletTime = false;
            return;
        }
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        float factor = Mathf.Clamp(slowMagnitude, 0.001f, 1f);
        owner = this;
        active = true;
        endTime = RealtimeNow + slowDuration;
        Time.timeScale = originalTimeScale * factor;
        Time.fixedDeltaTime = originalFixedDeltaTime * factor;
    }

    private void RestoreTime()
    {
        if (!active) return;
        if (owner == this)
        {
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            owner = null;
        }
        active = false;
        enableBulletTime = false;
    }

    private void OnDisable() { RestoreTime(); }
    private void OnDestroy() { RestoreTime(); }
}
