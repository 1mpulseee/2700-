//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the emissive properties on a specified material in the renderer. 
/// This script allows dynamically adjusting the emissive color and intensity based on a shared Light's settings.
/// </summary>
[System.Serializable]
public class RCC_Emission {

    /// <summary>
    /// The Renderer component that contains the emissive material.
    /// </summary>
    public Renderer lightRenderer;

    /// <summary>
    /// The index of the target material in the Renderer’s material array.
    /// </summary>
    public int materialIndex = 0;

    /// <summary>
    /// If true, the material does not rely on an external texture for emission and uses the light color directly.
    /// </summary>
    public bool noTexture = false;

    /// <summary>
    /// If true, the alpha channel is also applied to the emission color, matching light intensity.
    /// </summary>
    public bool applyAlpha = false;

    /// <summary>
    /// A multiplier applied to the emissive intensity.
    /// </summary>
    [Range(.1f, 10f)]
    public float multiplier = 1f;

    /// <summary>
    /// Internal ID used to set the emission color property on the material.
    /// </summary>
    private int emissionColorID;

    /// <summary>
    /// A reference to the target material instance on which the emissive color is set.
    /// </summary>
    private Material material;

    /// <summary>
    /// The color value used for the emissive property, updated based on the shared light's settings.
    /// </summary>
    private Color targetColor;

    /// <summary>
    /// Tracks if the emission component has been successfully initialized.
    /// </summary>
    private bool initialized = false;

    /// <summary>
    /// Initializes the emission settings by enabling emission keywords and retrieving the relevant shader property IDs.
    /// </summary>
    public void Init() {

        // Ensure a renderer is assigned.
        if (!lightRenderer) {

            Debug.LogError("No renderer assigned for emission! Please assign a renderer or disable emission functionality.");
            return;

        }

        // Retrieve the correct material from the renderer.
        material = lightRenderer.materials[materialIndex];

        // Enable the emission keyword on the material.
        material.EnableKeyword("_EMISSION");

        // Get the shader property ID for the emission color.
        emissionColorID = Shader.PropertyToID("_EmissionColor");

        // Verify the material has the emission color property.
        if (!material.HasProperty(emissionColorID))
            Debug.LogError("Material does not have an _EmissionColor property!");

        // Mark as initialized.
        initialized = true;

    }

    /// <summary>
    /// Sets the emissive color and intensity on the material based on the specified Light.
    /// </summary>
    /// <param name="sharedLight">The Light component whose intensity and color are used for the emissive effect.</param>
    public void Emission(Light sharedLight) {

        // If not yet initialized, attempt initialization first.
        if (!initialized) {

            Init();
            return;

        }

        // If the light is disabled or its intensity is effectively zero, dim the emission fully.
        if (!sharedLight.enabled || Mathf.Approximately(sharedLight.intensity, 0f)) {

            targetColor = Color.white * 0f;

        } else {

            // If the material uses no texture for emission, use the actual color of the light.
            // Otherwise, default to a white color modulated by the light's intensity.
            if (!noTexture)
                targetColor = Color.white * sharedLight.intensity * multiplier;
            else
                targetColor = sharedLight.color * sharedLight.intensity * multiplier;

            // If alpha is applied, incorporate it into the emission color’s alpha channel.
            if (applyAlpha)
                targetColor = new Color(targetColor.r, targetColor.g, targetColor.b, sharedLight.intensity * multiplier);

        }

        // Update the material's emission color if there is a change.
        if (material.GetColor(emissionColorID) != targetColor)
            material.SetColor(emissionColorID, targetColor);
    }

}
