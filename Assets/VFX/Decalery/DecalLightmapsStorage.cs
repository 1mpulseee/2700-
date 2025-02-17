#if BAKERY_INCLUDED
#else

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[ExecuteInEditMode]
public class DecalLightmapsStorage : MonoBehaviour
{
    // List of baked lightmaps
    public List<Texture2D> maps = new List<Texture2D>();
    public List<Texture2D> masks = new List<Texture2D>();
    public List<Texture2D> dirMaps = new List<Texture2D>();

    // new props
    public List<Renderer> bakedRenderers = new List<Renderer>();
    public List<int> bakedIDs = new List<int>();
    public List<Vector4> bakedScaleOffset = new List<Vector4>();

    public int[] idremap;

    void Awake()
    {
        DecalLightmaps.RefreshScene(gameObject.scene, this);
    }

    void Start()
    {
        // Unity can for some reason alter lightmapIndex after the scene is loaded in a multi-scene setup, so fix that
#if UNITY_2021_1_OR_NEWER
         DecalLightmaps.RefreshScene(gameObject.scene, this); // new Unity can destroy lightmaps after Awake if the lighting data asset is set
#endif
        DecalLightmaps.RefreshScene2(gameObject.scene, this);//, appendOffset);
    }
}

#endif