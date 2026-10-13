#if UNITY_EDITOR
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Effect Tools/Timeline Face Preview Config", fileName = "TimelineFacePreviewConfig")]
public sealed class TimelineFacePreviewConfig : ScriptableObject
{
    [Serializable]
    public sealed class Binding
    {
        [Tooltip("优先按 Mesh 匹配；不填写时按 Renderer Name 匹配。")]
        public Mesh mesh;
        public string rendererName;
        public int materialIndex;
        public string textureProperty = "_MainTex";
        [Tooltip("按事件 Int 编号排列，Textures[0] 对应编号 0。")]
        public Texture2D[] textures;
        [Tooltip("可选：非正方形贴图启用此关键字；没有对应 Shader 逻辑时留空。")]
        public string winkKeyword;
        [Tooltip("可选：仅原材质启用该关键字时，编号 0 保留启用，其他编号关闭。")]
        public string sequenceKeyword;
    }
    public Binding[] bindings = new Binding[0];
}
#endif
