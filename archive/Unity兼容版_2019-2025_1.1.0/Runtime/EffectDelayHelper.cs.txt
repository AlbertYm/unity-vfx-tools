using UnityEngine;

namespace EffectDelayMaker
{
    public class EffectDelayHelper : MonoBehaviour
    {
        [Min(0f)] public float delay;
        // Retained for serialized compatibility; unused by the original version.
        public float totalTime;
        public bool havePlayed;
        [SerializeField] private bool replay;

        private bool hidingForDelay;
        private EffectDelayRunner pending;

        private void Awake() { Application.targetFrameRate = 60; }

        private void OnEnable()
        {
            if (Application.isPlaying && !havePlayed) Replay();
        }

        private void OnDisable()
        {
            if (hidingForDelay) return;
            CancelDelay();
            havePlayed = false;
        }

        private void OnDestroy() { CancelDelay(); }

        private void Update()
        {
            if (!replay) return;
            replay = false;
            Replay();
        }

        public void Replay()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            CancelDelay();
            replay = false;
            havePlayed = false;
            // A separate host survives while this effect is inactive.
            GameObject host = new GameObject("Effect Delay Runner");
            host.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, gameObject.scene);
            pending = host.AddComponent<EffectDelayRunner>();
            pending.Begin(this, delay);
            hidingForDelay = true;
            gameObject.SetActive(false);
            hidingForDelay = false;
        }

        internal void CompleteDelay(EffectDelayRunner runner)
        {
            if (pending != runner) return;
            pending = null;
            havePlayed = true;
            gameObject.SetActive(true);
        }

        internal void AbortDelay(EffectDelayRunner runner)
        {
            if (pending != runner) return;
            pending = null;
            havePlayed = false;
            // Restore activeSelf while the parent or component remains disabled.
            gameObject.SetActive(true);
        }

        private void CancelDelay()
        {
            if (pending == null) return;
            Destroy(pending.gameObject);
            pending = null;
        }
    }
}