using UnityEditor;
using UnityEngine;

namespace FxShader.EditorTools
{
    [InitializeOnLoad]
    internal static class ParticleSystemCreationDefaults
    {
        static ParticleSystemCreationDefaults()
        {
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
        }

        private static void OnComponentWasAdded(Component component)
        {
            var particleSystem = component as ParticleSystem;
            if (particleSystem == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorUtility.IsPersistent(particleSystem))
                return;

            var main = particleSystem.main;
            if (!main.playOnAwake)
                return;

            // 只在创建时设置默认值，保留后续手动修改。
            main.playOnAwake = false;
        }
    }
}