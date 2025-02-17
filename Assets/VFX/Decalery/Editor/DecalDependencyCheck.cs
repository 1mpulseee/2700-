#if UNITY_EDITOR

// Disable 'obsolete' warnings
#pragma warning disable 0618

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System;
using UnityEditor.Build;

[InitializeOnLoad]
#if UNITY_2017_4_OR_NEWER
public class DecalDependencyCheck : IActiveBuildTargetChanged
#else
public class DecalDependencyCheck
#endif
{
    static void AddDefine()
    {
        var platform = EditorUserBuildSettings.selectedBuildTargetGroup;
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(platform);
        if (!defines.Contains("DECALERY_INCLUDED"))
        {
            var shader = Shader.Find("Hidden/fGPUDecalWriteShader");
            if (shader == null)
            {
                //Debug.LogError("Decalery is installed, but no fGPUDecalWriteShader is present."); // wait for the next domain reload
                return;
            }

            bool hasShader = false;
            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObj == null)
            {
                Debug.LogError("Can't patch GraphicsSettings in this Unity version. Please add Hidden/fGPUDecalWriteShader to Always Included Shaders if you intend to use runtime GPU-based decals in builds.");
            }
            else
            {
                var serializedObject = new SerializedObject(graphicsSettingsObj);
                var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
                for (int i = 0; i < arrayProp.arraySize; ++i)
                {
                    var arrayElem = arrayProp.GetArrayElementAtIndex(i);
                    if (shader == arrayElem.objectReferenceValue)
                    {
                        hasShader = true;
                        break;
                    }
                }

                if (!hasShader)
                {
                    int arrayIndex = arrayProp.arraySize;
                    arrayProp.InsertArrayElementAtIndex(arrayIndex);
                    var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
                    arrayElem.objectReferenceValue = shader;
                    serializedObject.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log("fGPUDecalWriteShader added to Always Included Shaders.");
                }
            }

            if (defines.Length > 0) defines += ";";
            defines += "DECALERY_INCLUDED";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(platform, defines);
        }
    }

    static DecalDependencyCheck()
    {
        AddDefine();
    }

#if UNITY_2017_4_OR_NEWER
    public int callbackOrder { get { return 0; } }
    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        AddDefine();
    }
#endif
}

#endif


