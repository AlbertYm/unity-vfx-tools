using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ParticleSystemEditorExtension : EditorWindow
{
    public string myString = "";
    public int gridId;
    [MenuItem("Window/粒子参数编辑")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(ParticleSystemEditorExtension));
    }

    void OnGUI()
    {
        GUILayout.Label("");
        gridId = GUILayout.SelectionGrid(gridId, new[] { "批量设置", "基础值加", "基础值减"},3);
        GUILayout.Label("Base Settings", EditorStyles.boldLabel);
        myString = EditorGUILayout.TextField("修改值:", myString);
        bool canChange = GUILayout.Button("确认修改");
        if (canChange) 
        {
            GameObject tempObjcet = Selection.activeGameObject.gameObject;
            if (tempObjcet != null || myString != "")
            {
                foreach (ParticleSystem ps in tempObjcet.transform.GetComponentsInChildren<ParticleSystem>())
                {
                    if (ps != null)
                    {
                        float value= float.Parse(myString);
                        var tempMain = ps.main;
                        float baseValue = tempMain.startDelay.constant;
                        switch (gridId) 
                        {
                            case 0:
                                tempMain.startDelay = value;
                                break;
                            case 1:
                                tempMain.startDelay = baseValue + value;
                                break;
                            case 2:
                                float tempValue= baseValue - value;
                                if (tempValue <= 0) 
                                {
                                    tempValue = 0;
                                }
                                tempMain.startDelay = tempValue;
                                break;
                        }
                    }
                }
            }
        }
    }
}