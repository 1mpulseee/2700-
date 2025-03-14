//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;

public class RCC_InitLoad : EditorWindow {

    [InitializeOnLoadMethod]
    static void InitOnLoad() {

        EditorApplication.delayCall += EditorUpdate;

    }

    public static void EditorUpdate() {

        bool checkInitialLayersOnPrefabs = false;

#if !BCG_RCC

        // Enable BCG_RCC scripting define symbol
        RCC_SetScriptingSymbol.SetEnabled("BCG_RCC", true);

        checkInitialLayersOnPrefabs = true;

        // Show a single informative dialog
        EditorUtility.DisplayDialog(
            "Realistic Car Controller | Setup Instructions",
            "Thank you for purchasing and using Realistic Car Controller. Please read the documentation before use. Also check out the online documentation for updated info.\n\n"
            + "Important: RCC uses the new Input System. The legacy input system is deprecated. Make sure your project has the Input System package installed via Package Manager.",
            "Got it!"
        );

        // Open welcome window
        RCC_WelcomeWindow.OpenWindow();

#endif

        // Run installation checks
        EditorApplication.delayCall += RCC_Installation.Check;

        if (checkInitialLayersOnPrefabs)
            EditorApplication.delayCall += RCC_Installation.CheckAllLayers;

    }

}
