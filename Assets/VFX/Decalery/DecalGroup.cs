using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DecalGroup : MonoBehaviour
{
    public GameObject parentObject;
    public List<GameObject> parentObjectsAdditional;
    public bool pickParentWithRaycast = false;
    public bool pickParentWithBox = false;
    public float bias = 0;
    public float rayLength = 0.1f;
    public bool roadTransform = false;
    public Material optionalMateralChange;
    public List<Material> optionalMateralChangeAdditional;
    public bool tangents = false;
    public bool linkToParent = false;
    public bool linkToDecalGroup = false;
    public bool angleFade = false;
    public bool optimize = false;
    public bool useBurst = false;
    public bool makeAsset = false;
    public bool autoHideRenderer = true;

    public Material[] surfaceReplacement;
    public Material[] surfaceReplacementMaterial;

    public enum PickMode
    {
        Manual,
        BoxIntersection,
        RaycastFromVertices
    }
    
    public PickMode pickMode = PickMode.Manual;
    public float boxScale = 1.0f;

    public LayerMask layerMask = ~0;

    [Range(-1.0f, 1.0f)]
    public float angleClip = 0.5f;

    public string originalName;
    public List<GameObject> sceneObjects;

    [Range(0.0f, 1.0f)]
    public float atlasMinX = 0;
    [Range(0.0f, 1.0f)]
    public float atlasMinY = 0;
    [Range(0.0f, 1.0f)]
    public float atlasMaxX = 1;
    [Range(0.0f, 1.0f)]
    public float atlasMaxY = 1;

    public Vector3 avgDir;

    public static bool drawGizmoMode;
    public static Mesh drawGizmoMesh;
    public static Matrix4x4 drawGizmoMatrix;

    void OnDrawGizmos()
    {
        var tform = transform;
        if (Mathf.Abs(avgDir.x) + Mathf.Abs(avgDir.y) + Mathf.Abs(avgDir.z) > 0.1f)
        {
            Gizmos.matrix = Matrix4x4.TRS(tform.position, Quaternion.LookRotation(-avgDir), Vector3.one);
            Gizmos.color = Color.white;
            Gizmos.DrawFrustum(Vector3.zero, 90, 0.1f, 0, 1.0f);
            var clr = Color.cyan;
            clr.a = 0.5f;
            Gizmos.color = clr;
            Gizmos.DrawCube(new Vector3(0, 0, 0.1f), new Vector3(0.2f, 0.2f, 0.01f));
        }
        else
        {
            var clr = Color.cyan;
            clr.a = 0.5f;
            Gizmos.color = clr;
            Gizmos.DrawSphere(tform.position, 0.1f);
            Gizmos.DrawWireSphere(tform.position, 0.1f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        if (pickMode == PickMode.BoxIntersection)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var size = mr.bounds.extents * 2 * boxScale;
                size = new Vector3(Mathf.Max(size.x, rayLength), Mathf.Max(size.y, rayLength), Mathf.Max(size.z, rayLength));
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireCube(mr.bounds.center, size);
            }
        }

        if (!drawGizmoMode) return;
        Gizmos.matrix = drawGizmoMatrix;
        Gizmos.DrawWireMesh(drawGizmoMesh);//, drawGizmoMatrix.GetColumn(3), drawGizmoMatrix.rotation, drawGizmoMatrix.lossyScale);
    }
}

