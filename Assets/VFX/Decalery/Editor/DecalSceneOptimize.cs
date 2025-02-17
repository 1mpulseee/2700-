#if UNITY_EDITOR

// Disable 'obsolete' warnings
#pragma warning disable 0618

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class DecalSceneOptimize : EditorWindow
{
    public int maxVertexCountPerBatch = 65535;
    public float maxBoundsSizePerBatch = 70.0f;

    struct Surface
    {
        public Mesh mesh;
        public MeshRenderer mr;
        public int submesh;
        public Transform parent;
    };

    [MenuItem("Decalery/Optimize batch count", false, 55)]
    private static void OptimizeDrawCallsWindow()
    {
        var instance = (DecalSceneOptimize)GetWindow(typeof(DecalSceneOptimize));
        instance.titleContent.text = "Optimize batch count";
        instance.minSize = new Vector2(250, 80);
        instance.maxSize = new Vector2(instance.minSize.x, instance.minSize.y + 1);
        instance.Show();
    }

    static void OptimizeDrawCalls(int maxVertexCountPerBatch, float maxBoundsSizePerBatch)
    {
        var listA = new List<Surface>();

        int inDraws = 0;
        int outDraws = 0;

        var originals = new List<GameObject>();

        var decals = FindObjectsOfType<DecalGroup>();
        for(int i=0; i<decals.Length; i++)
        {
            var objs = decals[i].sceneObjects;
            if (objs == null) continue;
            for(int j=0; j<objs.Count; j++)
            {
                var obj = objs[j];
                if (obj == null) continue;
                if (!obj.activeInHierarchy) continue;

                var mr = obj.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (!mr.enabled) continue;

                var mf = obj.GetComponent<MeshFilter>();
                if (mf == null) continue;
                
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                var mats = mr.sharedMaterials;
                for (int k = 0; k < mesh.subMeshCount; k++)
                {
                    if (mats.Length <= k) continue;

                    var ci = new Surface();
                    ci.mesh = mf.sharedMesh;
                    ci.submesh = j;
                    ci.mr = mr;
                    ci.parent = obj.transform.parent;
                    listA.Add(ci);

                    originals.Add(obj);

                    inDraws++;
                }
            }
        }

        var lists = new List<List<Surface>>();

        while (listA.Count > 0)
        {
            var listB = new List<Surface>();
            var mat = listA[0].mr.sharedMaterials[listA[0].submesh];
            var parent = listA[0].parent;
            var mesh = listA[0].mesh;
            int vertCount = mesh.vertexCount;
            var aabb = listA[0].mr.bounds;
            int lightmapIndex = listA[0].mr.lightmapIndex;
            var newList = new List<Surface>();
            lists.Add(newList);

            for (int i = 0; i < listA.Count; i++)
            {
                int vertCount2 = listA[i].mesh.vertexCount;

                if (i > 0)
                {
                    if (parent != listA[i].parent)
                    {
                        listB.Add(listA[i]);
                        continue;
                    }

                    if (mat != listA[i].mr.sharedMaterials[listA[i].submesh])
                    {
                        listB.Add(listA[i]);
                        continue;
                    }

                    if (lightmapIndex != listA[i].mr.lightmapIndex)
                    {
                        listB.Add(listA[i]);
                        continue;
                    }

                    if (vertCount + vertCount2 > maxVertexCountPerBatch)
                    {
                        listB.Add(listA[i]);
                        continue;
                    }

                    var testAABB = new Bounds(aabb.center, aabb.size);
                    testAABB.Encapsulate(listA[i].mr.bounds);
                    if (testAABB.size.x > maxBoundsSizePerBatch ||
                        testAABB.size.y > maxBoundsSizePerBatch ||
                        testAABB.size.z > maxBoundsSizePerBatch)
                    {
                        listB.Add(listA[i]);
                        continue;
                    }

                    aabb.Encapsulate(listA[i].mr.bounds);
                }

                vertCount += vertCount2;
                newList.Add(listA[i]);
            }
            listA = listB;
        }

#if BAKERY_INCLUDED
        var storageGO = GameObject.Find("!ftraceLightmaps");
        if (storageGO == null)
        {
            storageGO = new GameObject();
            storageGO.name = "!ftraceLightmaps";
            storageGO.hideFlags = HideFlags.HideInHierarchy;
        }
        var storage = storageGO.GetComponent<ftLightmapsStorage>();
        if (storage == null)
        {
            storage = storageGO.AddComponent<ftLightmapsStorage>();
        }
#else
        var storageGO = GameObject.Find("!decalLightmaps");
        if (storageGO == null)
        {
            storageGO = new GameObject();
            storageGO.name = "!decalLightmaps";
            storageGO.hideFlags = HideFlags.HideInHierarchy;
        }
        var storage = storageGO.GetComponent<DecalLightmapsStorage>();
        if (storage == null)
        {
            storage = storageGO.AddComponent<DecalLightmapsStorage>();
        }
#endif

        for (int j = 0; j < lists.Count; j++)
        {
            var list = lists[j];
            var subRoot = new GameObject();
            subRoot.name = "NEW_DECAL#_Optimized_" + j;
            if (list.Count > 0) subRoot.transform.parent = list[0].parent;
            GameObjectUtility.SetStaticEditorFlags(subRoot, StaticEditorFlags.OccludeeStatic);
            var combineInstances = new CombineInstance[list.Count];

            for (int i = 0; i < list.Count; i++)
            {
                var ci = new CombineInstance();
                ci.lightmapScaleOffset = list[i].mr.lightmapScaleOffset;
                ci.mesh = list[i].mesh;
                ci.subMeshIndex = list[i].submesh;
                ci.transform = list[i].mr.transform.localToWorldMatrix;
                combineInstances[i] = ci;
            }

            var mesh = new Mesh();
            mesh.name = subRoot.name;
            mesh.CombineMeshes(combineInstances, true, true, true);

            var mf = subRoot.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = subRoot.AddComponent<MeshRenderer>();
            mr.sharedMaterial = list[0].mr.sharedMaterials[list[0].submesh];

            int lightmapIndex = list[0].mr.lightmapIndex;
            var prevIndex = storage.bakedRenderers.LastIndexOf(list[0].mr);
            if (prevIndex >= 0) lightmapIndex = storage.bakedIDs[prevIndex];

            storage.bakedRenderers.Add(mr);
            storage.bakedIDs.Add(lightmapIndex);
            storage.bakedScaleOffset.Add(new Vector4(1,1,0,0));
#if BAKERY_INCLUDED
            storage.bakedVertexOffset.Add(-1);
            storage.bakedVertexColorMesh.Add(null);
#endif

            outDraws++;
        }

        for(int i=0; i<originals.Count; i++)
        {
            DestroyImmediate(originals[i]);
        }

        EditorUtility.SetDirty(storage);
        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("Drawcalls: " + inDraws + " -> " + outDraws);
    }

    void OnGUI()
    {
        maxVertexCountPerBatch = EditorGUILayout.IntField("Limit vertices per batch", maxVertexCountPerBatch);
        maxBoundsSizePerBatch = EditorGUILayout.FloatField("Limit world batch size", maxBoundsSizePerBatch);
        
        GUILayout.Space(10);

        if (GUILayout.Button("Optimize", GUILayout.Height(24)))
        {
            OptimizeDrawCalls(maxVertexCountPerBatch, maxBoundsSizePerBatch);
        }
    }
}

#endif