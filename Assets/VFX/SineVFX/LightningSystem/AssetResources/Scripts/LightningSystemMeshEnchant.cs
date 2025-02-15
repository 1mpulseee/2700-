using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.VFX;

public class LightningSystemMeshEnchant : MonoBehaviour
{
    public enum lsmodes
    {
        RandomClosestCell,
        RandomOtherCell
    }
    public lsmodes modes;
    public bool previewCellsInEditor = false;
    public float cellsScale = 1f;
    public float minDistance = 1f;

    [Space(10)]
    public GameObject mesh;
    public bool meshScaleAutoScaleEnabled = false;
    public float meshAutoScaleMultiply = 1f;
    public float masterScale = 1f;

    private struct vertexData
    {
        public Vector3[] vertexPositions;
        public Vector3[] vertexNormals;
    }
    private vertexData vd;
    private MeshFilter mf;
    private Dictionary<Vector3, List<Vector3>> cells = new Dictionary<Vector3, List<Vector3>>();
    private Dictionary<Vector3, List<Vector3>> cellsNormal = new Dictionary<Vector3, List<Vector3>>();
    private Vector3[] cellOffsets = new Vector3[27];

    [Space(10)]    
    public int maximumNumberOfAttempts = 8;
    [Range(1, 3)]
    public int minNumberOfMainStrips = 1;
    [Range(1,3)]
    public int maxNumberOfMainStrips = 1;

    [Space(10)]
    [Range(0f, 1f)]
    public float bonusBranchProbability = 0f;

    [Space(10)]
    public float speed = 10f;
    public AnimationCurve speedVariation;
    public float speedVariationTime = 0f;
    public float speedVariationTimeSpeed = 0.5f;

    VisualEffect visualEffect;
    VFXEventAttribute eventAttribute;

    private float timerCurrent = 0f;
    private float autoScaleValue = 1f;

    private int howManyTimesVFXWasTriggered = 0;
    private int bonusBranchProbabilityResult = 0;

    // Start is called before the first frame update
    void Start()
    {
        cells = new Dictionary<Vector3, List<Vector3>>();
        cellsNormal = new Dictionary<Vector3, List<Vector3>>();
        GenerateCellOffsets();
        visualEffect = GetComponent<VisualEffect>();
        eventAttribute = visualEffect.CreateVFXEventAttribute();

        mf = mesh.GetComponent<MeshFilter>();
        vd = new vertexData();
        vd.vertexPositions = mf.sharedMesh.vertices;
        vd.vertexNormals = mf.sharedMesh.normals;
        CreateCells();

        eventAttribute.SetVector3("BranchedHitPosition1", new Vector3(0f, 0f, 0f));
        eventAttribute.SetVector3("BranchedHitPosition2", new Vector3(0f, 0f, 0f));
        eventAttribute.SetVector3("HitPosition", new Vector3(1f, 1f, 1f));

        ProcessAutoScale();
        eventAttribute.SetFloat("AutoScaleValue", autoScaleValue);

        ResetVFXParameters();
    }

    // Update is called once per frame
    void Update()
    {
        ProcessSpeedVariation();
        ProcessAutoScale();

        eventAttribute.SetFloat("AutoScaleValue", autoScaleValue);

        SpawnLightningEvent();

    }

    // Creates Spatial Hash Cells, this is used to pick 2 vertices to create a Lightning Branch between them later
    void CreateCells()
    {
        Vector3[] verts = mf.sharedMesh.vertices;
        Vector3[] vertNormals = mf.sharedMesh.normals;
        Vector3 farthestPoint = new Vector3(0f, 0f, 0f);
        Vector3 closestPoint = new Vector3(0f, 0f, 0f);
        Vector3 sideLengths = new Vector3(0f, 0f, 0f);

        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].x > farthestPoint.x)
            {
                farthestPoint.x = verts[i].x;
            }
            if (verts[i].y > farthestPoint.y)
            {
                farthestPoint.y = verts[i].y;
            }
            if (verts[i].z > farthestPoint.z)
            {
                farthestPoint.z = verts[i].z;
            }
            if (verts[i].x < closestPoint.x)
            {
                closestPoint.x = verts[i].x;
            }
            if (verts[i].y < closestPoint.y)
            {
                closestPoint.y = verts[i].y;
            }
            if (verts[i].z < closestPoint.z)
            {
                closestPoint.z = verts[i].z;
            }
        }
        sideLengths.x = Mathf.Abs(farthestPoint.x - closestPoint.x);
        sideLengths.y = Mathf.Abs(farthestPoint.y - closestPoint.y);
        sideLengths.z = Mathf.Abs(farthestPoint.z - closestPoint.z);

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 possibleID = new Vector3(Mathf.Floor(verts[i].x * cellsScale), Mathf.Floor(verts[i].y * cellsScale), Mathf.Floor(verts[i].z * cellsScale));

            if (cells.ContainsKey(possibleID) == false)
            {
                cells.Add(possibleID, new List<Vector3>());
                cellsNormal.Add(possibleID, new List<Vector3>());
            }
            cells[possibleID].Add(verts[i]);
            cellsNormal[possibleID].Add(vertNormals[i]);
        }
    }

    // Process and spawns a "Create Lightning" Event in VFX Graph, also sending event attributes
    void SpawnLightningEvent()
    {
        if (timerCurrent >= 1f)
        {
            int rand = Random.Range(0, vd.vertexNormals.Length);
            Vector3 randomVertex = vd.vertexPositions[rand];
            Vector3 randomVertexInOtherCell = new Vector3(0f, 0f, 0f);
            Vector3 randomVertexNormal = vd.vertexNormals[rand];
            Vector3 randomVertexCellId = new Vector3(Mathf.Floor(randomVertex.x * cellsScale), Mathf.Floor(randomVertex.y * cellsScale), Mathf.Floor(randomVertex.z * cellsScale));

            for (int i = 0; i < maximumNumberOfAttempts; i++)
            {
                int rand2 = Random.Range(0, 25);
                if (cells.ContainsKey(randomVertexCellId + cellOffsets[rand2]))
                {
                    switch (modes)
                    {
                        case lsmodes.RandomClosestCell:
                            randomVertexInOtherCell = cells[randomVertexCellId + cellOffsets[rand2]][Random.Range(0, cells[randomVertexCellId + cellOffsets[rand2]].Count)];
                            break;
                        case lsmodes.RandomOtherCell:
                            int rand3 = Random.Range(0, cells.Count - 1);
                            randomVertexInOtherCell = cells.ElementAt(rand3).Value[Random.Range(0, cells.ElementAt(rand3).Value.Count)];
                            break;
                    }
                    if (Vector3.Distance(randomVertex, randomVertexInOtherCell) > minDistance)
                    {
                        eventAttribute.SetVector3("EnchantHitLocalPosition01", randomVertex);
                        eventAttribute.SetVector3("EnchantHitNormal01", randomVertexNormal);

                        eventAttribute.SetVector3("EnchantHitLocalPosition02", randomVertexInOtherCell);
                        eventAttribute.SetVector3("EnchantHitNormal02", cellsNormal[randomVertexCellId + cellOffsets[rand2]][Random.Range(0, cellsNormal[randomVertexCellId + cellOffsets[rand2]].Count)]);
                        
                        eventAttribute.SetVector3("BranchedHitPosition0", randomVertexInOtherCell);

                        int numberOfMainStrips = Random.Range(minNumberOfMainStrips, maxNumberOfMainStrips + 1);
                        eventAttribute.SetInt("NumberOfMainStrips", numberOfMainStrips);
                        eventAttribute.SetFloat("TrueEandom", Random.Range(0f, 1f));

                        //
                        // Workaround for Instantiation in Unity 2022 and above. Can't use GPU events, so the branch separation map should be dispatched along with all other attributes.
                        //

                        ProcessBonusBranchProbability();
                        int count = 0;
                        if (bonusBranchProbabilityResult == 1)
                        {
                            eventAttribute.SetInt("BranchedIDBool" + count, 0);
                            count++;
                        }
                        if (numberOfMainStrips > 1)
                        {
                            eventAttribute.SetInt("BranchedIDBool" + count, 1);
                            count++;
                        }
                        if (numberOfMainStrips > 2)
                        {
                            eventAttribute.SetInt("BranchedIDBool" + count, 2);
                            count++;
                        }

                        eventAttribute.SetInt("howManyTimesVFXWasTriggered", howManyTimesVFXWasTriggered);
                        visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
                        visualEffect.SetInt("TotalBranchedParticleCountHIDDEN", (count * 50));

                        visualEffect.SendEvent("CreateLightning", eventAttribute);
                        howManyTimesVFXWasTriggered = ((howManyTimesVFXWasTriggered + count) % 3);

                        //
                        // End of workaround solution
                        //

                        break;
                    }
                }
            }
            timerCurrent = 0f;
        }
        timerCurrent += (Time.deltaTime * speed * speedVariation.Evaluate(speedVariationTime));
    }

    private void OnDisable()
    {
        ResetVFXParameters();
    }

    // Reset the number of triggered VFX
    void ResetVFXParameters()
    {
        howManyTimesVFXWasTriggered = 0;
        visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
    }

    // Process the rate at which the "SpawnLightningEvent" function is called, this is used to make the spawning of VFX more or less irregular and random
    void ProcessSpeedVariation()
    {
        if (speedVariationTime <= 1f)
        {
            speedVariationTime += Time.deltaTime * speedVariationTimeSpeed;
        }
        else
        {
            speedVariationTime = 0f;
        }
    }

    // Processing Auto Scale, there are two modes, automatic and standard
    // In Automatic Mode the VFX uses one Transform as an Anchor
    // Standard Mode is used when you need to scale the VFX separately in real-time
    void ProcessAutoScale()
    {
        if (meshScaleAutoScaleEnabled == true)
        {
            autoScaleValue = (mesh.transform.lossyScale.x + mesh.transform.lossyScale.y + mesh.transform.lossyScale.z) / 3f * meshAutoScaleMultiply;
        }
        else
        {
            autoScaleValue = masterScale;
        }
    }

    // Generates random Spatial Hash cell offsets, used to pick random cell near the selected vertex
    void GenerateCellOffsets()
    {
        for (int i = 0; i < cellOffsets.Length; i++)
        {
            cellOffsets[i].x = Mathf.Floor(i / 9f) - 1f;
            cellOffsets[i].y = Mathf.Floor(i % 3f) - 1f;
            cellOffsets[i].z = Mathf.Floor(i / 3f) % 3f - 1f;
        }
        var list = cellOffsets.ToList();
        list.RemoveAt(13);
        cellOffsets = list.ToArray();
    }

    // Calculating additive bonus branch probability
    void ProcessBonusBranchProbability()
    {
        if(Random.Range(0f, 1f) > (1f - bonusBranchProbability))
        {
            bonusBranchProbabilityResult = 1;
        }
        else
        {
            bonusBranchProbabilityResult = 0;
        }
    }



    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false && previewCellsInEditor == true && Selection.Contains(this.gameObject))
        {
            cells = new Dictionary<Vector3, List<Vector3>>();
            cellsNormal = new Dictionary<Vector3, List<Vector3>>();
            mf = mesh.GetComponent<MeshFilter>();
            vd = new vertexData();
            CreateCells();

            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            foreach (KeyValuePair<Vector3, List<Vector3>> pair in cells)
            {
                Gizmos.DrawWireCube((pair.Key + new Vector3(0.5f,0.5f,0.5f)) / cellsScale, new Vector3(1f, 1f, 1f) / cellsScale * 0.9f);
            }
        }
    }
    #endif
}
