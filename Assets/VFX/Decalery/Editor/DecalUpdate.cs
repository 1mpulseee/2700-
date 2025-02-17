#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class DecalUpdate
{
    [MenuItem("Decalery/Update all decals", false, 51)]
    private static void GenerateAllDecals()
    {

#if BAKERY_INCLUDED
#else
        var storageGOUnused = GameObject.Find("!decalLightmaps");
        if (storageGOUnused != null)
        {
            UnityEngine.Object.DestroyImmediate(storageGOUnused);
        }
#endif

        CPUDecalUtils.checkPrefabs = CPUBurstDecalUtils.checkPrefabs = true;

        var renderers = Resources.FindObjectsOfTypeAll(typeof(Renderer)) as Renderer[];
        var prtList = new List<GameObject>();
        for(int i=0; i<renderers.Length; i++)
        {
            var mr = renderers[i];
            var proot = PrefabUtility.GetPrefabObject(mr.gameObject);
            if (proot != null) continue;
            if (mr.name.Length > 10 && mr.name.Substring(0,10) == "NEW_DECAL#")
            {
                var parent = mr.transform.parent;
                if (parent != null && parent.name == "NEW_DECAL#parent")
                {
                    if (!prtList.Contains(parent.gameObject)) prtList.Add(parent.gameObject);
                }
                Object.DestroyImmediate(mr.gameObject);
            }
        }
        for(int i=0; i<prtList.Count; i++)
        {
            Object.DestroyImmediate(prtList[i]);
        }

        var objs = Resources.FindObjectsOfTypeAll(typeof(DecalGroup)) as DecalGroup[];
        foreach(DecalGroup obj in objs)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (path != "") continue; // must belond to scene

            var topmostParent = obj.transform;
            while(topmostParent.parent != null) topmostParent = topmostParent.parent;
            if (!topmostParent.gameObject.activeInHierarchy) continue;

            if (obj.useBurst)
            {
                CPUBurstDecalUtils.UpdateDecal(obj);
            }
            else
            {
                CPUDecalUtils.UpdateDecal(obj);
            }
            if (obj.autoHideRenderer)
            {
                var mr = obj.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
        }
    }

    [MenuItem("Decalery/Select decal source", false, 61)]
    private static void SelectFromDecal()
    {
        var objs = Selection.objects;
        var newObjs = new List<GameObject>();
        for(int i=0; i<objs.Length; i++)
        {
            var parts = objs[i].name.Split('#');
            if (parts.Length > 2)
            {
                var name2 = parts[1];
                var obj2 = GameObject.Find(name2);
                if (obj2 != null && obj2.GetComponent<DecalGroup>() != null)
                {
                    newObjs.Add(obj2);
                }
            }
        }
        if (newObjs.Count > 0)
        {
            Selection.objects = newObjs.ToArray();
        }
    }
}

#endif
