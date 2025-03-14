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
using System;
using System.Collections;
using System.Collections.Generic;

[CustomEditor(typeof(RCC_DemoMaterials))]
public class RCC_DemoMaterialsEditor : Editor {

    RCC_DemoMaterials prop;
    GUISkin skin;

    public void OnEnable() {

        skin = Resources.Load<GUISkin>("RCC_WindowSkin");

    }

    public override void OnInspectorGUI() {

        prop = (RCC_DemoMaterials)target;
        serializedObject.Update();

        GUI.skin = skin != null ? skin : EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);

        DrawDefaultInspector();

        if (GUILayout.Button("Select All Demo Materials For Converting To URP")) {

            if (prop.demoMaterials == null || prop.demoMaterials.Length == 0) {

                EditorUtility.DisplayDialog("Realistic Car Controller | No Demo Materials Found",
                "There are no demo materials assigned. Please check RCC_DemoMaterials.", "Close");
                return;

            }

            EditorUtility.DisplayDialog("Realistic Car Controller | Converting All Demo Materials To URP",
            "All demo materials will be selected in your project now. After that, you'll need to convert them to URP shaders while they have been selected. You can convert them from Edit --> Render Pipeline --> Universal Render Pipeline --> Convert Selected Materials.", "Close");

            UnityEngine.Object[] objects = new UnityEngine.Object[prop.demoMaterials.Length];

            for (int i = 0; i < objects.Length; i++) {

                objects[i] = prop.demoMaterials[i];

            }

            Selection.objects = objects;
        }

        if (GUILayout.Button("Convert All Demo Vehicle Materials To URP")) {

            if (prop.demoMaterials == null || prop.demoMaterials.Length == 0) {

                EditorUtility.DisplayDialog("Realistic Car Controller | No Demo Vehicle Materials Found",
                "There are no demo vehicle materials assigned. Please check RCC_DemoMaterials.", "Close");
                return;

            }

            EditorUtility.DisplayDialog("Realistic Car Controller | Converting All Demo Vehicle Materials To URP",
            "All demo vehicle materials will be converted to URP now.", "Close");

            prop.ConvertCarBodyShadersToURP();

        }

        if (GUILayout.Button("Clean For Empty Elements")) {

            prop.CleanEmptyMaterials();

        }

        EditorGUILayout.LabelField("Ekrem Bugra Ozdoganlar\nBoneCracker Games",
        EditorStyles.centeredGreyMiniLabel, GUILayout.MaxHeight(50f));

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

        serializedObject.ApplyModifiedProperties();

    }

}
