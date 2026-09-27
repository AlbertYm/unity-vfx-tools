#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MaterialRename : EditorWindow
{
    [MenuItem("Unity Tool/Material Rename")]
    public static void ShowWindow()
    {
        GetWindow<MaterialRename>("Material Rename");
    }

    [MenuItem("Assets/TD/Rename Material Textures", false, 10)]
    private static void RenameMaterialTextures()
    {
        RenameSelectedMaterials();
    }

    private void OnGUI()
    {
        GUILayout.Label("Material Rename", EditorStyles.boldLabel);
        if (GUILayout.Button("Rename Selected Materials"))
        {
            RenameSelectedMaterials();
        }
    }

    private static void RenameSelectedMaterials()
    {
        var objs = Selection.objects;
        foreach (var obj in objs)
        {
            if (obj is Material material)
            {
                string newMatName = GetMaterialTexturesName(material);
                if (!string.IsNullOrEmpty(newMatName))
                {
                    RenameInternal(material, newMatName);
                }
                else
                {
                    Debug.LogError($"{obj.name} does not have valid textures to rename.");
                }
            }
            else
            {
                Debug.LogError($"{obj.name} is not a material.");
            }
        }
    }

    private static string GetMaterialTexturesName(Material material)
    {
        List<string> textureNames = new List<string>();

        Shader shader = material.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < propertyCount; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    textureNames.Add(texture.name);
                }
            }
        }

        return textureNames.Count > 0 ? string.Join("@", textureNames) : null;
    }

    private static void RenameInternal(Material material, string newMatName)
    {
        string path = AssetDatabase.GetAssetPath(material);
        AssetDatabase.RenameAsset(path, newMatName);
        AssetDatabase.SaveAssets();
    }
}

#endif
