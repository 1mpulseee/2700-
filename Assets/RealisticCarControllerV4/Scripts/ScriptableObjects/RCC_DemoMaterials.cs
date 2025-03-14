//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Contains and manages demo materials used in RCC for both environment and vehicles.
/// This includes functionality for assigning shaders based on the render pipeline (Built-in or URP).
/// </summary>
public class RCC_DemoMaterials : ScriptableObject {

    #region Singleton

    /// <summary>
    /// Singleton instance of RCC_DemoMaterials. Loads from "Resources/RCC Assets/RCC_DemoMaterials".
    /// </summary>
    private static RCC_DemoMaterials instance;

    /// <summary>
    /// Public getter for the singleton instance. Auto-loads the ScriptableObject if not already loaded.
    /// </summary>
    public static RCC_DemoMaterials Instance {

        get {

            if (instance == null)
                instance = Resources.Load("RCC Assets/RCC_DemoMaterials") as RCC_DemoMaterials;

            return instance;

        }

    }

    #endregion

    /// <summary>
    /// An array of general demo materials for the environment or other objects.
    /// </summary>
    public Material[] demoMaterials;

    /// <summary>
    /// An array of materials specifically intended for demo vehicles.
    /// </summary>
    public Material[] demoVehicleMaterials;

    /// <summary>
    /// The name of the built-in shader to be used for car bodies in the Built-in Render Pipeline.
    /// </summary>
    public string builtInShaderNameForCarBody = "RCC Car Body Shader";

    /// <summary>
    /// The name of the URP shader to be used for car bodies in the Universal Render Pipeline.
    /// </summary>
    public string urpShaderNameForCarBody = "RCC Car Body Shader URP";

    /// <summary>
    /// Updates the shaders of the demoVehicleMaterials array based on the current render pipeline (Built-in or URP).
    /// </summary>
    public void ConvertCarBodyShadersToURP() {

        if (demoVehicleMaterials == null)
            return;

        Shader shaderToAssign;

        // Determines which shader to use depending on the render pipeline.
        if (!IsURP())
            shaderToAssign = Shader.Find(builtInShaderNameForCarBody);  // Built-in
        else
            shaderToAssign = Shader.Find(urpShaderNameForCarBody);      // URP

        // If the shader is found, assign it to all vehicle materials.
        if (shaderToAssign != null) {

            foreach (var material in demoVehicleMaterials) {

                if (material != null) {

                    material.shader = shaderToAssign;
                    Debug.Log($"Assigned {shaderToAssign.name} shader to {material.name}");

                }

            }

        } else {

            Debug.LogError("Failed to find the appropriate shader for the current render pipeline.");

        }

    }

    /// <summary>
    /// Returns the environment demo materials for use in the current scene. 
    /// Currently, this method does not change or filter the materials, 
    /// but can be extended to handle URP-specific environment settings.
    /// </summary>
    /// <returns>An array of demoMaterials.</returns>
    public Material[] SelectEnvironmentShadersForURP() {

        if (demoMaterials == null)
            return null;

        // In this demo, simply returns the materials without further processing.
        return demoMaterials;

    }

    /// <summary>
    /// Removes any null entries from the demoMaterials and demoVehicleMaterials arrays to keep them clean.
    /// </summary>
    public void CleanEmptyMaterials() {

        // Cleans null references from demoMaterials array.
        if (demoMaterials != null) {

            List<Material> materialsList = new List<Material>();

            for (int i = 0; i < demoMaterials.Length; i++) {
                if (demoMaterials[i] != null)
                    materialsList.Add(demoMaterials[i]);
            }

            demoMaterials = materialsList.ToArray();

        }

        // Cleans null references from demoVehicleMaterials array.
        if (demoVehicleMaterials != null) {

            List<Material> materialsVehicleList = new List<Material>();

            for (int i = 0; i < demoVehicleMaterials.Length; i++) {
                if (demoVehicleMaterials[i] != null)
                    materialsVehicleList.Add(demoVehicleMaterials[i]);
            }

            demoVehicleMaterials = materialsVehicleList.ToArray();

        }

    }

    /// <summary>
    /// Checks if the current project is using the Universal Render Pipeline (URP).
    /// </summary>
    /// <returns>True if URP is being used, otherwise false.</returns>
    private bool IsURP() {

        RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;

#if RCC_URP
        // Checks if the active pipeline is UniversalRenderPipelineAsset.
        if (currentPipeline != null && currentPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
            return true;
#endif

        return false;

    }

}
