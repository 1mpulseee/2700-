#if UNITY_EDITOR

// Disable 'obsolete' warnings
#pragma warning disable 0618

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[CustomEditor(typeof(DecalGroup))]
[CanEditMultipleObjects]
public class DecalGroupInspector : UnityEditor.Editor {

    SerializedProperty ftParentObject;
    SerializedProperty ftParentObjectsAdditional;
    SerializedProperty ftPickMode;
    SerializedProperty ftBoxScale;
    SerializedProperty ftBias;
    SerializedProperty ftRayLength;
    SerializedProperty ftPickByBox;
    SerializedProperty ftPickByRaycast;
    SerializedProperty ftLayerMask;
    SerializedProperty ftAngleClip;
    SerializedProperty ftAngleFade;
    SerializedProperty ftTangents;
    SerializedProperty ftLinkToParent;
    SerializedProperty ftLinkToDecalGroup;
    SerializedProperty ftOptimize;
    SerializedProperty ftUseBurst;
    SerializedProperty ftMakeAsset;
    SerializedProperty ftAutoHide;
    //SerializedProperty ftOptionalMateralChange;
    SerializedProperty ftAtlasMinX;
    SerializedProperty ftAtlasMinY;
    SerializedProperty ftAtlasMaxX;
    SerializedProperty ftAtlasMaxY;
    SerializedProperty[] ftAtlasValues;

    static GUIStyle stylePick = null;
    static GUIStyle styleSmall = null;
    static GUIContent labelMaterialChange;
    static GUIContent labelRemoveReceiver;
    static GameObject pickedObject;
    static bool callbackRegistered = false;
    static bool objectPickerMode = false;
    static bool previewMode = false;
    static DecalGroupInspector editor;

    static float buffAtlasMinX, buffAtlasMinY, buffAtlasMaxX, buffAtlasMaxY;

    public enum ParentMode
    {
        SceneRoot,
        Receivers,
        Source
    }

    bool valuesChangedDirectly = false;
    bool geometryNeedsToBeRebuilt = true;
    GameObject enableMaterialInput = null;

    int cachedPickMode = -1;
    int cachedLayerMask = 0;
    float cachedBoxScale = -1.0f;
    float cachedBias = 0.0f;
    float cachedRayLength = -1.0f;
    float cachedAngleClip = -1.0f;
    bool cachedAngleFade = false;
    bool cachedTangents = false;
    bool cachedLinkToParent = false;
    bool cachedLinkToDecalGroup = false;
    bool showAtlas = false;

    int dragMode = -1;

    static GameObject[] pickerIgnore;

    void OnEnable()
    {
        ftParentObject = serializedObject.FindProperty("parentObject");
        ftParentObjectsAdditional = serializedObject.FindProperty("parentObjectsAdditional");
        ftPickMode = serializedObject.FindProperty("pickMode");
        ftBoxScale = serializedObject.FindProperty("boxScale");
        ftBias = serializedObject.FindProperty("bias");
        ftRayLength = serializedObject.FindProperty("rayLength");
        ftPickByRaycast = serializedObject.FindProperty("pickParentWithRaycast");
        ftPickByBox = serializedObject.FindProperty("pickParentWithBox");
        ftLayerMask = serializedObject.FindProperty("layerMask");
        ftAngleClip = serializedObject.FindProperty("angleClip");
        ftAngleFade = serializedObject.FindProperty("angleFade");
        ftTangents = serializedObject.FindProperty("tangents");
        ftLinkToParent = serializedObject.FindProperty("linkToParent");
        ftLinkToDecalGroup = serializedObject.FindProperty("linkToDecalGroup");
        ftOptimize = serializedObject.FindProperty("optimize");
        ftUseBurst = serializedObject.FindProperty("useBurst");
        ftMakeAsset = serializedObject.FindProperty("makeAsset");
        ftAutoHide = serializedObject.FindProperty("autoHideRenderer");
        //ftOptionalMateralChange = serializedObject.FindProperty("optionalMateralChange");
        ftAtlasMinX = serializedObject.FindProperty("atlasMinX");
        ftAtlasMinY = serializedObject.FindProperty("atlasMinY");
        ftAtlasMaxX = serializedObject.FindProperty("atlasMaxX");
        ftAtlasMaxY = serializedObject.FindProperty("atlasMaxY");
        ftAtlasValues = new SerializedProperty[4];
        ftAtlasValues[0] = ftAtlasMinX;
        ftAtlasValues[1] = ftAtlasMinY;
        ftAtlasValues[2] = ftAtlasMaxX;
        ftAtlasValues[3] = ftAtlasMaxY;

        enableMaterialInput = null;
    }

    void AddReceiver(GameObject obj)
    {
        Undo.RecordObjects(targets, "Change decal receivers");
        foreach(DecalGroup decal in targets)
        {
            if (decal.parentObject == obj) continue;
            if (decal.parentObjectsAdditional != null && decal.parentObjectsAdditional.Contains(obj)) continue;

            if (decal.parentObject == null)
            {
                decal.parentObject = obj;
            }
            else
            {
                if (decal.parentObjectsAdditional == null) decal.parentObjectsAdditional = new List<GameObject>();
                decal.parentObjectsAdditional.Add(obj);
            }
        }
    }

    static void UpdateCallback(SceneView view)
    {
        if (previewMode)
        {
            objectPickerMode = false;

            var tform = (editor.target as DecalGroup).transform;
            if (tform.hasChanged)
            {
                editor.geometryNeedsToBeRebuilt = true;
                tform.hasChanged = false;
            }

            if (editor.geometryNeedsToBeRebuilt)
            {
                //Debug.LogError("REGEN");
                editor.Regenerate();
                editor.geometryNeedsToBeRebuilt = false;
                editor.valuesChangedDirectly = true;
            }
        }

        if (objectPickerMode)
        {
            var cur = Event.current;
            if (cur.type == EventType.MouseUp && cur.button == 0)
            {
                pickedObject = HandleUtility.PickGameObject(cur.mousePosition, false, pickerIgnore);
                editor.Repaint();
                //objectPickerMode = false;
            }
            else if (cur.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(0);
            }
            else if (cur.type == EventType.MouseMove)
            {
                var obj = HandleUtility.PickGameObject(cur.mousePosition, false, pickerIgnore);
                if (obj != null)
                {
                    var mf = obj.GetComponent<MeshFilter>();
                    if (mf != null)
                    {
                        var mesh = mf.sharedMesh;
                        if (mesh != null)
                        {
                            DecalGroup.drawGizmoMode = true;
                            DecalGroup.drawGizmoMesh = mesh;
                            DecalGroup.drawGizmoMatrix = obj.transform.localToWorldMatrix;
                        }
                    }
                    else
                    {
                        var smf = obj.GetComponent<SkinnedMeshRenderer>();
                        if (smf != null)
                        {
                            var mesh = smf.sharedMesh;
                            if (mesh != null)
                            {
                                DecalGroup.drawGizmoMode = true;
                                DecalGroup.drawGizmoMesh = mesh;
                                DecalGroup.drawGizmoMatrix = obj.transform.localToWorldMatrix;
                            }
                        }
                    }
                }
            }
        }
    }

    static void SelectionCallback()
    {
        objectPickerMode = false;
        previewMode = false;
    }

    UnityEngine.Object DrawObjectPicker(Rect rect, GUIContent label, SerializedProperty obj, SerializedProperty primaryObj, int arrayIndex, bool serialize = true)
    {
        float minFieldWidth = EditorGUIUtility.currentViewWidth * 0.65f;

        var decal = target as DecalGroup;
        Material curMat = null;
        bool differentMaterials = false;
        if (obj == primaryObj)
        {
            bool first = true;
            curMat = decal.optionalMateralChange;
            //differentMaterials = ftOptionalMateralChange.hasMultipleDifferentValues;
            foreach(DecalGroup d in targets)
            {
                var prevMat = curMat;
                curMat = d.optionalMateralChange;
                if (prevMat != curMat && !first) differentMaterials = true;
                first = false;
            }
        }
        else
        {
            bool first = true;
            foreach(DecalGroup d in targets)
            {
                if (d.optionalMateralChangeAdditional == null) d.optionalMateralChangeAdditional = new List<Material>();
                while(d.optionalMateralChangeAdditional.Count <= arrayIndex) d.optionalMateralChangeAdditional.Add(null);
                var prevMat = curMat;
                curMat = d.optionalMateralChangeAdditional[arrayIndex];
                if (prevMat != curMat && !first) differentMaterials = true;
                first = false;
            }
        }

        bool showM = (enableMaterialInput != obj.objectReferenceValue && curMat == null) || primaryObj == null;
        if (obj.objectReferenceValue == null) showM = true;
        if (differentMaterials) showM = false;

        EditorGUI.BeginProperty(rect, label, obj);
        if (!serialize) EditorGUI.showMixedValue = false;
        EditorGUI.BeginChangeCheck();
        var newVal = EditorGUI.ObjectField(new Rect(rect.x, rect.y, showM ? ((rect.width - rect.height) - 9) : minFieldWidth, rect.height), label, serialize ? obj.objectReferenceValue : null, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            if (serialize) obj.objectReferenceValue = newVal;
        }
        if (primaryObj != null)
        {
            var delRect = new Rect((rect.x + rect.width - rect.height) + 14, rect.y, rect.height, rect.height);
            if (GUI.Button(delRect, labelRemoveReceiver))
            {
                enableMaterialInput = null;
                //if (obj.objectReferenceValue != null)
                {
                    Undo.RecordObjects(targets, "Change decal receivers");

                    obj.objectReferenceValue = null;

                    if (obj == primaryObj)
                    {
                        foreach(DecalGroup d in targets)
                        {
                            if (d.parentObjectsAdditional != null && d.parentObjectsAdditional.Count > 0)
                            {
                                // Set primary to array[0], remove first from array
                                d.parentObject = d.parentObjectsAdditional[0];
                                d.parentObjectsAdditional.RemoveAt(0);
                                valuesChangedDirectly = true;
                            }
                            if (d.optionalMateralChangeAdditional != null && d.optionalMateralChangeAdditional.Count > 0)
                            {
                                d.optionalMateralChange = d.optionalMateralChangeAdditional[0];
                                d.optionalMateralChangeAdditional.RemoveAt(0);
                            }
                        }
                    }
                    else
                    {
                        // Remove from array
                        foreach(DecalGroup d in targets)
                        {
                            d.parentObjectsAdditional.RemoveAt(arrayIndex);
                            if (d.optionalMateralChangeAdditional != null && d.optionalMateralChangeAdditional.Count > arrayIndex)
                            {
                                d.optionalMateralChangeAdditional.RemoveAt(arrayIndex);
                            }
                        }
                        valuesChangedDirectly = true;
                    }
                }
            }

            if (showM)
            {
                var matRect = new Rect((rect.x + rect.width - rect.height) - 7, rect.y, rect.height+1, rect.height);
                if (GUI.Button(matRect, labelMaterialChange, styleSmall))
                {
                    enableMaterialInput = (GameObject)obj.objectReferenceValue;
                }
            }
            else
            {
                EditorGUI.showMixedValue = differentMaterials;
                EditorGUI.BeginChangeCheck();
                var newMat = (Material)EditorGUI.ObjectField(new Rect(rect.x + minFieldWidth + 2, rect.y, (rect.width - rect.height) - (minFieldWidth-11), rect.height), new GUIContent(""), curMat, typeof(Material), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(targets, "Change decal receiver materials");
                    if (obj == primaryObj)
                    {
                        foreach(DecalGroup d in targets) d.optionalMateralChange = newMat;
                    }
                    else
                    {
                        foreach(DecalGroup d in targets) d.optionalMateralChangeAdditional[arrayIndex] = newMat;
                    }
                    EditorUtility.SetDirty(decal);
                    valuesChangedDirectly = true;
                }
            }
        }
        EditorGUI.EndProperty();
        return newVal;
    }

    void Regenerate()
    {
        foreach(DecalGroup decal in targets)
        {
            ClearDecal(decal);
            bool origOpt = decal.optimize;
            decal.optimize = false;
            //if (useBurst)
            if (decal.useBurst)
            {
                CPUBurstDecalUtils.UpdateDecal(decal);
            }
            else
            {
                CPUDecalUtils.UpdateDecal(decal);
            }
            decal.optimize = origOpt;
        }
    }

    void TestPreviewRefreshProperty(ref int cached, int newVal)
    {
        //if (cached >= 0)
        {
            if (cached != newVal)
            {
                geometryNeedsToBeRebuilt = true;
            }
        }
        cached = newVal;
    }

    void TestPreviewRefreshProperty(ref float cached, float newVal)
    {
        //if (cached >= 0)
        {
            if (cached != newVal)
            {
                geometryNeedsToBeRebuilt = true;
            }
        }
        cached = newVal;
    }

    void TestPreviewRefreshProperty(ref bool cached, bool newVal)
    {
        if (cached != newVal)
        {
            geometryNeedsToBeRebuilt = true;
        }
        cached = newVal;
    }

    bool CursorAroundRect(Rect rect, Vector2 mpos)
    {
        float margin = 15.0f;
        rect.x -= margin;
        rect.y -= margin;
        rect.width += margin * 2;
        rect.height += margin * 2;
        return rect.Contains(mpos);
    }

    public override void OnInspectorGUI()
    {
        //DrawDefaultInspector();

        editor = this;

        if (!callbackRegistered)
        {
            callbackRegistered = true;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= UpdateCallback;
            SceneView.duringSceneGui += UpdateCallback;
#else
            SceneView.onSceneGUIDelegate -= UpdateCallback;
            SceneView.onSceneGUIDelegate += UpdateCallback;
#endif
            Selection.selectionChanged -= SelectionCallback;
            Selection.selectionChanged += SelectionCallback;
        }

        if (stylePick == null)
        {
            stylePick = new GUIStyle("Button");
            stylePick.fixedHeight = 32;

            styleSmall = new GUIStyle("Button");
            styleSmall.alignment = TextAnchor.MiddleCenter;
            styleSmall.clipping = TextClipping.Overflow;
            styleSmall.contentOffset = new Vector2(1.5f, 0.0f);

            labelMaterialChange = new GUIContent("M", "Change material for this receiver");
            labelRemoveReceiver = new GUIContent("X", "Remove this receiver");
        }

        int lineHeight = 20;

        serializedObject.Update();

        if (pickedObject != null)
        {
            AddReceiver(pickedObject);
            if (targets.Length > 1)
            {
                AssetDatabase.Refresh();
                serializedObject.Update();
                OnEnable();
            }
            valuesChangedDirectly = true;
            pickedObject = null;
        }

        bool differentPickMode = ftPickByRaycast.hasMultipleDifferentValues || ftPickByBox.hasMultipleDifferentValues;
        if (differentPickMode)
        {
            EditorGUILayout.LabelField("Selected decals have different receiver picking mode.");
        }
        else
        {
            if (ftPickByRaycast.boolValue)
            {
                ftPickMode.intValue = (int)DecalGroup.PickMode.RaycastFromVertices;
            }
            else if (ftPickByBox.boolValue)
            {
                ftPickMode.intValue = (int)DecalGroup.PickMode.BoxIntersection;
            }
            else
            {
                ftPickMode.intValue = (int)DecalGroup.PickMode.Manual;
            }
            EditorGUILayout.PropertyField(ftPickMode, new GUIContent("Receiver selection mode", "Defines how decal-receiving objects are selected"));
            if (ftPickMode.intValue == (int)DecalGroup.PickMode.Manual)
            {
                ftPickByRaycast.boolValue = ftPickByBox.boolValue = false;
            }
            else if (ftPickMode.intValue == (int)DecalGroup.PickMode.BoxIntersection)
            {
                ftPickByRaycast.boolValue = false;
                ftPickByBox.boolValue = true;
            }
            else if (ftPickMode.intValue == (int)DecalGroup.PickMode.RaycastFromVertices)
            {
                ftPickByRaycast.boolValue = true;
                ftPickByBox.boolValue = false;
            }
            TestPreviewRefreshProperty(ref cachedPickMode, ftPickMode.intValue);

            if (ftPickMode.intValue == (int)DecalGroup.PickMode.Manual)
            {
                var rect = new Rect(18, lineHeight*2, EditorGUIUtility.currentViewWidth - 50, 18);
#if UNITY_2019_1_OR_NEWER
#else
                var r = GUILayoutUtility.GetLastRect();
                rect.y += r.y;
#endif
                var addRcvText = "Drag a GameObject into this field to add it as a receiver or click the circle to choose. You can also use 'Pick in scene' and click on objects.";
                DrawObjectPicker(rect, new GUIContent(ftParentObject.objectReferenceValue == null ? "Add receiver..." : "Receiver 0", ftParentObject.objectReferenceValue == null ? addRcvText : ""), ftParentObject, ftParentObject, 0);

                int idx = 1;
                int showLimit = 99999;
                foreach(DecalGroup d in targets)
                {
                    if (d.parentObjectsAdditional == null) continue;
                    showLimit = System.Math.Min(showLimit, d.parentObjectsAdditional.Count);
                }
                int ctr = 0;
                foreach(SerializedProperty subProperty in ftParentObjectsAdditional)
                {
                    rect.y += lineHeight;
                    DrawObjectPicker(rect, new GUIContent("Receiver " + idx), subProperty, ftParentObject, idx-1);
                    idx++;
                    ctr++;
                    if (ctr == showLimit) break;
                }
                rect.y += lineHeight;
                var origParentObject = ftParentObject.objectReferenceValue;
                if (origParentObject != null)
                {
                    rect.y += 10;
                    //ftParentObject.objectReferenceValue = null;
                    var pickedObj = DrawObjectPicker(rect, new GUIContent("Add receiver...", addRcvText), ftParentObject, null, 0, false);
                    if (pickedObj != null)
                    {
                        AddReceiver((GameObject)pickedObj);
                        if (targets.Length > 1)
                        {
                            AssetDatabase.Refresh();
                            serializedObject.Update();
                            OnEnable();
                        }
                        valuesChangedDirectly = true;
                    }
                    /*ftParentObject.objectReferenceValue = origParentObject;*/
                    //rect.y += 5;
                    //rect.y += lineHeight;
                }

                //Rect controlRect = 
                EditorGUILayout.GetControlRect(false, rect.y);

                objectPickerMode = GUILayout.Toggle(objectPickerMode, new GUIContent("Pick in scene", "Pick receivers by clicking on them in Scene View") , stylePick);
                if (objectPickerMode)
                {
                    var list = new List<GameObject>();
                    var allDecals = FindObjectsOfType<DecalGroup>();
                    var numDecals = allDecals.Length;
                    for(int k=0; k<numDecals; k++)
                    {
                        var objs = allDecals[k].sceneObjects;
                        if (objs == null) continue;
                        int numObjs = objs.Count;
                        for(int l=0; l<numObjs; l++)
                        {
                            if (objs[l] == null) continue;
                            list.Add(objs[l]);
                        }
                    }
                    pickerIgnore = list.ToArray();

                    previewMode = false;
                }
            }
            else if (ftPickMode.intValue == (int)DecalGroup.PickMode.BoxIntersection)
            {
                EditorGUILayout.PropertyField(ftBoxScale, new GUIContent("Box scale"));
                if (ftBoxScale.floatValue < 0) ftBoxScale.floatValue = 0.0f;
                TestPreviewRefreshProperty(ref cachedBoxScale, ftBoxScale.floatValue);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(ftRayLength, new GUIContent("Forward distance", "Decal projection distance (inside)"));
        if (ftRayLength.floatValue < 0) ftRayLength.floatValue = 0.0f;
        TestPreviewRefreshProperty(ref cachedRayLength, ftRayLength.floatValue);

        EditorGUILayout.PropertyField(ftBias, new GUIContent("Backward distance", "Decal projection distance (outside)"));
        if (ftBias.floatValue < 0) ftBias.floatValue = 0.0f;
        TestPreviewRefreshProperty(ref cachedBias, ftBias.floatValue);

        EditorGUILayout.PropertyField(ftAngleClip, new GUIContent("Angle clip", "Maximum allowed angle between each decal triangle and projector triangle. The value is from -1 (-180) to 1 (180). This is to prevent texture stretching on surfaces parallel to the projection."));
        TestPreviewRefreshProperty(ref cachedAngleClip, ftAngleClip.floatValue);

        EditorGUILayout.PropertyField(ftAngleFade, new GUIContent("Angle fade", "Fades vertex color/alpha to 0 as the projection approaches Angle clip value"));
        TestPreviewRefreshProperty(ref cachedAngleFade, ftAngleFade.boolValue);

        EditorGUILayout.PropertyField(ftLayerMask, new GUIContent("Layer mask", "Mask to filter receiving objects"));
        TestPreviewRefreshProperty(ref cachedLayerMask, ftLayerMask.intValue);

        bool differentParentMode = ftLinkToParent.hasMultipleDifferentValues || ftLinkToDecalGroup.hasMultipleDifferentValues;
        ParentMode parentMode = (ParentMode)255;
        if (differentParentMode)
        {
            EditorGUI.showMixedValue = true;
        }
        else
        {
            if (ftLinkToParent.boolValue)
            {
                parentMode = ParentMode.Receivers;
            }
            else if (ftLinkToDecalGroup.boolValue)
            {
                parentMode = ParentMode.Source;
            }
            else
            {
                parentMode = ParentMode.SceneRoot;
            }
        }
        var newParentMode = (ParentMode)EditorGUILayout.EnumPopup(new GUIContent("Parent mode", "Defines to which objects the resulting decals will be parented."), parentMode);
        if (newParentMode != parentMode)
        {
            if (newParentMode == ParentMode.Receivers)
            {
                ftLinkToParent.boolValue = true;
                ftLinkToDecalGroup.boolValue = false;
            }
            else if (newParentMode == ParentMode.Source)
            {
                ftLinkToParent.boolValue = false;
                ftLinkToDecalGroup.boolValue = true;
            }
            else
            {
                ftLinkToParent.boolValue = false;
                ftLinkToDecalGroup.boolValue = false;
            }
        }
        if (differentParentMode) EditorGUI.showMixedValue = false;

        //EditorGUILayout.PropertyField(ftLinkToParent, new GUIContent("Link to parent", "If enabled, decal meshes will be parented to their receivers"));
        TestPreviewRefreshProperty(ref cachedLinkToParent, ftLinkToParent.boolValue);
        TestPreviewRefreshProperty(ref cachedLinkToDecalGroup, ftLinkToDecalGroup.boolValue);

        EditorGUILayout.PropertyField(ftTangents, new GUIContent("Generate tangents", "Does decal geometry need per-vertex tangents generated? Tangents are required for normal-mapping, parallax and other effects."));
        TestPreviewRefreshProperty(ref cachedTangents, ftTangents.boolValue);

        EditorGUILayout.PropertyField(ftAutoHide, new GUIContent("Hide on Update", "Automatically hide source Mesh Renderer on this object when the decal is generated"));

        EditorGUILayout.PropertyField(ftOptimize, new GUIContent("Optimize", "Performs vertex welding and vertex order optimization"));

        EditorGUILayout.PropertyField(ftMakeAsset, new GUIContent("Create mesh asset", "Saves decal mesh to an asset file instead of storing it inside the scene. With this option, meshes are saved to Assets/DecaleryMeshes/[sceneName]_[objectName].asset"));

        EditorGUILayout.PropertyField(ftUseBurst, new GUIContent("Use Burst", "Use Burst to generate decal geometry. Requires Burst/Mathematics packages installed. Runs faster."));

        showAtlas = EditorGUILayout.Foldout(showAtlas, "Atlas part selection", EditorStyles.foldout);
        if (showAtlas)
        {
            var mr = (target as DecalGroup).GetComponent<MeshRenderer>();
            if (mr == null)
            {
                EditorGUILayout.LabelField("No MeshRenderer on decal");
            }
            else
            {
                var mats = mr.sharedMaterials;
                if (mats.Length == 0)
                {
                    EditorGUILayout.LabelField("No materials on MeshRenderer");
                }
                else if (mats.Length > 1)
                {
                    EditorGUILayout.LabelField("More than one material on MeshRenderer");
                }
                else
                {
                    var tex = mats[0].mainTexture;
                    if (tex == null)
                    {
                        EditorGUILayout.LabelField("No _MainTex or [MainTexture] in material");
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(ftAtlasMinX, new GUIContent("Left"));
                        EditorGUILayout.PropertyField(ftAtlasMinY, new GUIContent("Top"));
                        EditorGUILayout.PropertyField(ftAtlasMaxX, new GUIContent("Right"));
                        EditorGUILayout.PropertyField(ftAtlasMaxY, new GUIContent("Bottom"));

                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(new GUIContent("Copy", "Store atlas values in temporary memory"), GUILayout.Width(100), GUILayout.Height(24)))
                        {
                            buffAtlasMinX = ftAtlasMinX.floatValue;
                            buffAtlasMinY = ftAtlasMinY.floatValue;
                            buffAtlasMaxX = ftAtlasMaxX.floatValue;
                            buffAtlasMaxY = ftAtlasMaxY.floatValue;
                        }
                        if (GUILayout.Button(new GUIContent("Paste", "Paste atlas values from temporary memory"), GUILayout.Width(100), GUILayout.Height(24)))
                        {
                            ftAtlasMinX.floatValue = buffAtlasMinX;
                            ftAtlasMinY.floatValue = buffAtlasMinY;
                            ftAtlasMaxX.floatValue = buffAtlasMaxX;
                            ftAtlasMaxY.floatValue = buffAtlasMaxY;
                        }
                        GUILayout.EndHorizontal();

                        EditorGUILayout.Space();

                        var rect = GUILayoutUtility.GetAspectRect(tex.width / (float)tex.height);

#if UNITY_2018_1_OR_NEWER
                        EditorGUI.DrawTextureTransparent(rect, tex, ScaleMode.StretchToFill, 0, 0);
#else
                        EditorGUI.DrawTextureTransparent(rect, tex, ScaleMode.StretchToFill, 0);
#endif

                        var evt = Event.current;
                        var mpos = evt.mousePosition;
                        if (evt.type == EventType.MouseUp || evt.type == EventType.MouseDrag)
                        {
                            if (dragMode >= 0)
                            {
                                var prevVal = ftAtlasValues[dragMode].floatValue;
                                var newVal = Mathf.Clamp( (dragMode == 0 || dragMode == 2) ? ((mpos.x - rect.x) / (float)rect.width) : ((mpos.y - rect.y) / (float)rect.height), 0.0f, 1.0f );
                                ftAtlasValues[dragMode].floatValue = newVal;
                                Repaint();
                                if (prevVal != newVal)
                                {
                                    geometryNeedsToBeRebuilt = true;
                                }
                            }
                            if (evt.type == EventType.MouseUp) dragMode = -1;
                        }
                        bool isClick = evt.type == EventType.MouseDown;

                        rect = new Rect(
                                        Mathf.Lerp(rect.x, rect.x + rect.width, ftAtlasMinX.floatValue),
                                        Mathf.Lerp(rect.y, rect.y + rect.height, ftAtlasMinY.floatValue),
                                        rect.width * (ftAtlasMaxX.floatValue - ftAtlasMinX.floatValue),
                                        rect.height * (ftAtlasMaxY.floatValue - ftAtlasMinY.floatValue)
                            );

                        var clr = EditorGUIUtility.isProSkin ? Color.yellow : Color.blue;

                        var rectT = rect;
                        rectT.height = 1.0f;
                        EditorGUI.DrawRect(rectT, clr);

                        var rectB = rectT;
                        rectB.y = rect.y + rect.height;
                        EditorGUI.DrawRect(rectB, clr);

                        var rectL = rect;
                        rectL.width = 1.0f;
                        EditorGUI.DrawRect(rectL, clr);

                        var rectR = rectL;
                        rectR.x = rect.x + rect.width;
                        EditorGUI.DrawRect(rectR, clr);

                        float s = 8.0f;
                        var rect2 = new Rect(rect.x + rect.width*0.5f - s*0.5f, rect.y - s*0.5f, s, s);
                        EditorGUI.DrawRect(rect2, clr);
                        if (isClick && CursorAroundRect(rect2, mpos)) dragMode = 1;

                        rect2 = new Rect(rect.x + rect.width*0.5f - s*0.5f, rect.y + rect.height - s*0.5f, s, s);
                        EditorGUI.DrawRect(rect2, clr);
                        if (isClick && CursorAroundRect(rect2, mpos)) dragMode = 3;

                        rect2 = new Rect(rect.x - s*0.5f, rect.y + rect.height*0.5f - s*0.5f, s, s);
                        EditorGUI.DrawRect(rect2, clr);
                        if (isClick && CursorAroundRect(rect2, mpos)) dragMode = 0;

                        rect2 = new Rect(rect.x + rect.width - s*0.5f, rect.y + rect.height*0.5f - s*0.5f, s, s);
                        EditorGUI.DrawRect(rect2, clr);
                        if (isClick && CursorAroundRect(rect2, mpos)) dragMode = 2;
                    }
                }
            }
        }

        if (!objectPickerMode) DecalGroup.drawGizmoMode = false;

        EditorGUILayout.Space();

        bool prevPreviewMode = previewMode;
        previewMode = GUILayout.Toggle(previewMode, new GUIContent("Live Preview", "Manipulate decal with real-time feedback"), stylePick);
        if (!prevPreviewMode && previewMode)
        {
            CPUDecalUtils.checkPrefabs = CPUBurstDecalUtils.checkPrefabs = !previewMode;
            foreach(DecalGroup decal in targets)
            {
                ClearDecal(decal);
                valuesChangedDirectly = true;
            }
        }
        TestPreviewRefreshProperty(ref prevPreviewMode, previewMode);

        EditorGUILayout.BeginHorizontal("box");
        if (GUILayout.Button(new GUIContent("Update", "(Re)generate decal geometry"), stylePick))
        {
            CPUDecalUtils.checkPrefabs = CPUBurstDecalUtils.checkPrefabs = true;
            foreach(DecalGroup decal in targets)
            {
                ClearDecal(decal);
                if (decal.useBurst)
                {
                    CPUBurstDecalUtils.UpdateDecal(decal);
                }
                else
                {
                    CPUDecalUtils.UpdateDecal(decal);
                }
                if (decal.autoHideRenderer)
                {
                    var mr = decal.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }
                valuesChangedDirectly = true;
            }
        }

        if (GUILayout.Button(new GUIContent("Remove", "Remove previously generated decal geometry from scene"), stylePick))
        {
            foreach(DecalGroup decal in targets)
            {
                ClearDecal(decal);
                valuesChangedDirectly = true;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!valuesChangedDirectly)
        {
            serializedObject.ApplyModifiedProperties();
        }
        else
        {
            foreach(DecalGroup decal in targets) EditorUtility.SetDirty(decal);
        }
        valuesChangedDirectly = false;

        //Regenerate();
    }

    void ClearDecal(DecalGroup decal)
    {
        if (decal.originalName == decal.name)
        {
            var objs = decal.sceneObjects;
            if (objs == null) return;
            for(int i=0; i<objs.Count; i++)
            {
                if (objs[i] == null) continue;

                var ptype = PrefabUtility.GetPrefabType(objs[i]);
                if (ptype == PrefabType.PrefabInstance)
                {
                    Debug.LogError("Can't remove decal from a prefab: " + objs[i]);
                    continue;
                }

                var prt = objs[i].transform.parent;
                if (prt != null && prt.name == "NEW_DECAL#parent")
                {
                    DestroyImmediate(prt.gameObject);
                }
                DestroyImmediate(objs[i]);
            }
        }
        decal.sceneObjects = new List<GameObject>();
        decal.originalName = decal.name;
    }

    static Renderer GetValidRenderer(GameObject obj)
    {
        var mr = obj.GetComponent<Renderer>();
        if (mr as MeshRenderer == null && mr as SkinnedMeshRenderer == null)
        {
            // possibly multiple renderers on one gameobject?
            mr = obj.GetComponent<MeshRenderer>() as Renderer;
            if (mr != null) return mr;
            mr = obj.GetComponent<SkinnedMeshRenderer>() as Renderer;
            if (mr != null) return mr;
            return null;
        }
        return mr;
    }
}
#endif
