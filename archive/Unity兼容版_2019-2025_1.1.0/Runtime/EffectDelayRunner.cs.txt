using UnityEngine;

namespace EffectDelayMaker
{
    [AddComponentMenu("")]
    public sealed class EffectDelayRunner : MonoBehaviour
    {
        private EffectDelayHelper target;
        private float remaining;

        internal void Begin(EffectDelayHelper owner, float seconds)
        {
            target = owner;
            remaining = float.IsNaN(seconds) || float.IsInfinity(seconds)
                ? 0f : Mathf.Max(0f, seconds);
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }
            Transform parent = target.transform.parent;
            if (!target.enabled || (parent != null && !parent.gameObject.activeInHierarchy))
            {
                target.AbortDelay(this);
                Destroy(gameObject);
                return;
            }
            // Like the original WaitForSeconds, this uses scaled time.
            remaining -= Time.deltaTime;
            if (remaining > 0f) return;
            target.CompleteDelay(this);
            Destroy(gameObject);
        }
    }
}