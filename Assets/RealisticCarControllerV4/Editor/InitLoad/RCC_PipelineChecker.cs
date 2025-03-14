//----------------------------------------------
//            Realistic Car Controller
//
// Copyright � 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;

[InitializeOnLoad]
public class RCC_PipelineChecker {

    // Static constructor is called as soon as the editor is loaded or scripts are recompiled
    static RCC_PipelineChecker() {

        if (GraphicsSettings.defaultRenderPipeline == null)
            return;

#if !BCG_RCC
        return;
#endif

#if !RCC_RP
        // Subscribe to the update event for delayed execution
        EditorApplication.delayCall += DelayedInitLoad;
#endif

    }

    public static void DelayedInitLoad() {

        int decision = EditorUtility.DisplayDialogComplex("Realistic Car Controller | Materials", "Seems like you're using custom render pipeline, but materials of the demo content still have builtin shaders.", "I'll be using URP", "Cancel", "I'll be using HDRP");

        if (decision == 0)
            SessionState.SetBool("SelectMaterialsAfterCompile", true);

        if (decision == 1)
            EditorUtility.DisplayDialog("Realistic Car Controller | Materials", "All demo materials will remain the same. You'll need to convert the materials to make them compatible with your custom render pipeline.", "Ok");

        if (decision == 2)
            EditorUtility.DisplayDialog("Realistic Car Controller | Materials", "Please import the HDRP version of the asset located in the HDRP folder. Please read the readme text before importing the HDRP version.", "Ok");

        RCC_SetScriptingSymbol.SetEnabled("RCC_RP", true);

        // Subscribe to the play mode state changed event
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (!SessionState.GetBool("SelectMaterialsAfterCompile", false))
            return;

        SessionState.EraseBool("SelectMaterialsAfterCompile");

        Selection.objects = RCC_DemoMaterials.Instance.SelectEnvironmentShadersForURP();
        EditorUtility.DisplayDialog("Realistic Car Controller | Materials", "All demo materials have been selected now. You can now convert them from Edit --> Rendering --> Convert. After converting materials, you must convert RCC Car Body Shader from Tools --> BCG --> RCC --> URP.\n\nYou may want to reload the scene after converting the materials.", "Ok");

        RCC_SetScriptingSymbol.SetEnabled("RCC_URP", true);

        ManageShaders();

    }

    public static void ManageShaders() {

        string builtInShaderPackagePath = RCC_AssetPaths.Instance.importBuiltinShaders != null ? RCC_AssetPaths.Instance.GetPath(RCC_AssetPaths.Instance.importBuiltinShaders) : "";
        string urpShaderPackagePath = RCC_AssetPaths.Instance.importURPShaders != null ? RCC_AssetPaths.Instance.GetPath(RCC_AssetPaths.Instance.importURPShaders) : "";

        string builtInShaderPath = RCC_AssetPaths.Instance.builtinShaders != null ? RCC_AssetPaths.Instance.GetPath(RCC_AssetPaths.Instance.builtinShaders) : "";
        string urpShaderPath = RCC_AssetPaths.Instance.URPShaders != null ? RCC_AssetPaths.Instance.GetPath(RCC_AssetPaths.Instance.URPShaders) : "";

        // URP
        if (!ShaderExists(urpShaderPath)) {

            Debug.Log("URP detected. Importing URP Shader and deleting Built-in Shader.");

            EditorUtility.DisplayDialog("Realistic Car Controller | URP detected", "Importing URP Shader and deleting Built-in Shader. Please import the package.", "Ok");

            EditorApplication.isPlaying = false;

            if (urpShaderPackagePath != "")
                ImportShaderPackage(urpShaderPackagePath);
            else
                Debug.Log("URP shader package couldn't found.");

            if (ShaderExists(builtInShaderPath))
                DeleteActualShader(builtInShaderPath);

        } else {

            if (ShaderExists(builtInShaderPath))
                DeleteActualShader(builtInShaderPath);

        }

    }

    public static void OnPlayModeStateChanged(PlayModeStateChange state) {

        if (state == PlayModeStateChange.ExitingEditMode)
            ManageShaders();

    }

    // Check if the shader already exists in the project
    public static bool ShaderExists(string shaderPath) {

        return File.Exists(shaderPath);

    }

    public static void ImportShaderPackage(string packagePath) {

        if (File.Exists(packagePath)) {

            AssetDatabase.ImportPackage(packagePath, true);
            Debug.Log($"Imported shader package: {packagePath}");

        } else {

            Debug.LogError($"Shader package not found at: {packagePath}");

        }

    }

    public static void DeleteActualShader(string shaderPath) {

        string shaderDir = Path.GetDirectoryName(shaderPath);

        if (Directory.Exists(shaderDir)) {

            FileUtil.DeleteFileOrDirectory(shaderDir);
            AssetDatabase.Refresh();
            Debug.Log($"Deleted shader: {shaderDir}");

        } else {

            Debug.LogError($"Shader directory not found at: {shaderDir}");

        }

    }

}
