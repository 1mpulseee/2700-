#if BAKERY_INCLUDED
#else

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Disable 'obsolete' warnings
#pragma warning disable 0618

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.IO;
#endif

using UnityEngine.SceneManagement;

#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public class DecalLightmaps
{

    static List<int> lightmapRefCount;

    public static void RefreshFull()
    {
        var activeScene = SceneManager.GetActiveScene();
        var sceneCount = SceneManager.sceneCount;

        for(int i=0; i<sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            SceneManager.SetActiveScene(scene);
            LightmapSettings.lightmaps = new LightmapData[0];
        }

        for(int i=0; i<sceneCount; i++)
        {
            RefreshScene(SceneManager.GetSceneAt(i), null, true);
        }
        SceneManager.SetActiveScene(activeScene);
    }

    public static GameObject FindInScene(string nm, Scene scn)
    {
        var objs = scn.GetRootGameObjects();
        for(int i=0; i<objs.Length; i++)
        {
            if (objs[i].name == nm) return objs[i];
            var obj = objs[i].transform.Find(nm);
            if (obj != null) return obj.gameObject;
        }
        return null;
    }

    public static void RefreshScene(Scene scene, DecalLightmapsStorage storage = null, bool updateNonBaked = false)
    {
        var sceneCount = SceneManager.sceneCount;
        var existingLmaps = LightmapSettings.lightmaps;

        // Acquire storage
        if (storage == null)
        {
            if (!scene.isLoaded)
            {
                //Debug.LogError("dbg: Scene not loaded");
                return;
            }
            SceneManager.SetActiveScene(scene);

            var go = FindInScene("!decalLightmaps", scene);
            if (go==null) {
                //Debug.LogError("dbg: no storage");
                return;
            }

            storage = go.GetComponent<DecalLightmapsStorage>();
            if (storage == null) {
                //Debug.LogError("dbg: no storage 2");
                return;
            }
        }
        if (storage.idremap == null || storage.idremap.Length != storage.maps.Count)
        {
            storage.idremap = new int[storage.maps.Count];
        }

        for(int i=0; i<storage.maps.Count; i++)
        {
            var texlm = storage.maps[i];
            Texture2D texmask = null;
            Texture2D texdir = null;
            if (storage.masks.Count > i) texmask = storage.masks[i];
            if (storage.dirMaps.Count > i) texdir = storage.dirMaps[i];

            //bool found = false;
            int firstEmpty = -1;
            for(int j=0; j<existingLmaps.Length; j++)
            {
                if (existingLmaps[j].lightmapColor == texlm && existingLmaps[j].shadowMask == texmask)
                {
                    // lightmap already added - reuse
                    storage.idremap[i] = j;
                    //found = true;
                    break;
                }
                else if (firstEmpty < 0 && existingLmaps[j].lightmapColor == null && existingLmaps[j].shadowMask == null)
                {
                    // free (deleted) entry in existing lightmap list - possibly reuse
                    storage.idremap[i] = j;
                    firstEmpty = j;
                }
            }
        }

        // Set lightmap data on mesh renderers
        var emptyVec4 = new Vector4(1,1,0,0);
        for(int i=0; i<storage.bakedRenderers.Count; i++)
        {
            var r = storage.bakedRenderers[i];
            if (r == null)
            {
                continue;
            }
            //if (r.isPartOfStaticBatch) continue;
            var id = storage.bakedIDs[i];

            int globalID = (id < 0 || id >= storage.idremap.Length) ? id : storage.idremap[id];
            r.lightmapIndex = globalID;

            if (!r.isPartOfStaticBatch)
            {
                // scaleOffset is baked on static batches already
                var scaleOffset = id < 0 ? emptyVec4 : storage.bakedScaleOffset[i];
                r.lightmapScaleOffset = scaleOffset;
            }
        }
    }

    public static void RefreshScene2(Scene scene, DecalLightmapsStorage storage)
    {
        Renderer r;
        int id;
        for(int i=0; i<storage.bakedRenderers.Count; i++)
        {
            r = storage.bakedRenderers[i];
            if (r == null) continue;

            id = storage.bakedIDs[i];
            r.lightmapIndex = (id < 0 || id >= storage.idremap.Length) ? id : storage.idremap[id];
        }
    }
}

#endif