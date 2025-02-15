using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst.CompilerServices;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.VFX;

public class LightningSystemMeshRaycast : MonoBehaviour
{
    public bool previewNormalsInEditor = false;
    public bool showNormalHitDistance = false;
    public GameObject mesh;
    private struct vertexData
    {
        public Vector3[] vertexPositions;
        public Vector3[] vertexNormals;
    }
    private vertexData vd;
    
    [Space(10)]
    public float maxDistance = 5f;
    public bool maxDistanceAffectedByMeshScale = false;
    public float maxDistanceAffectedByMeshScaleMultiply = 1f;
    public bool meshScaleAutoScaleEnabled = false;
    public float meshAutoScaleMultiply = 1f;
    public float autoScaleMultiply = 1f;

    [Space(10)]
    public bool normalAdjustEnabled = false;
    public Vector3 normalAdjust = Vector3.forward;
    [Range(0f, 1f)]
    public float normalAdjustAmount = 0.5f;

    [Space(10)]
    [Range(1, 2)]
    public int maximumNumberOfAttempts = 2;
    [Range(1, 4)]
    public int maximumNumberOfBranchedAttempts = 4;

    [Space(10)]
    [Range(1, 3)]
    public int minNumberOfMainStrips = 1;
    [Range(1,3)]
    public int maxNumberOfMainStrips = 1;
    [Range(0, 3)]
    public int minNumberOfBranchedStrips = 0;
    [Range(0, 3)]
    public int maxNumberOfBranchedStrips = 2;
    [Space(10)]
    public float speed = 10f;
    public AnimationCurve speedVariation;
    public float speedVariationTime = 0f;
    public float speedVariationTimeSpeed = 0.5f;

    [Space(10)]
    public float randomBranchedCircleMinRadius = 0f;
    public float randomBranchedCircleMaxRadius = 1f;

    VisualEffect visualEffect;
    VFXEventAttribute eventAttribute;

    private float timerCurrent = 0f;
    private float autoScaleValue = 1f;
    private float actualPossibleHitDistance;
    private float meshAutoScaleFactor = 1f;
    private float processedMaxDistance = 1f;
    private float normalSlerpValue = 0f;
    private Vector3 normalAdjustPreviousValue = new Vector3(0.22f, 0.51f, 0.47f);
    private int howManyTimesBranchesWereTriggered = 0;
    private int howManyTimesVFXWasTriggered = 0;

    // Start is called before the first frame update
    void Start()
    {
        visualEffect = GetComponent<VisualEffect>();
        eventAttribute = visualEffect.CreateVFXEventAttribute();

        MeshFilter mf = mesh.GetComponent<MeshFilter>();
        vd = new vertexData();
        vd.vertexPositions = mf.sharedMesh.vertices;
        vd.vertexNormals = mf.sharedMesh.normals;

        ResetVFXParameters();
    }

    // Update is called once per frame
    void Update()
    {
        ProcessNormalSlerpValue();
        ProcessSpeedVariation();
        ProcessMaxDistance();
        ProcessAutoScale();
        ProcessLightningEvent();
    }

    // Spawning a Lightning Event if the main raycast hits. Setting all necessary attributes for the VFX Graph.
    // In the case of Mesh Raycast VFX, it is recommended to use not more than 2 maximum number of attempts.
    void ProcessLightningEvent()
    {
        if (timerCurrent >= 1f)
        {
            actualPossibleHitDistance = processedMaxDistance;

            for (int i = 0; i < maximumNumberOfAttempts; i++)
            {
                int rand = Random.Range(0, vd.vertexNormals.Length);
                Vector3 direction2 = mesh.transform.TransformDirection(Vector3.Slerp(vd.vertexNormals[rand], normalAdjust, normalSlerpValue));
                Vector3 raypos = mesh.transform.TransformPoint(vd.vertexPositions[rand]);

                Ray ray = new Ray(raypos, direction2);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, actualPossibleHitDistance))
                {
                    eventAttribute.SetVector3("BranchedHitPosition0", Vector3.zero);
                    eventAttribute.SetVector3("BranchedHitPosition1", Vector3.zero);
                    eventAttribute.SetVector3("BranchedHitPosition2", Vector3.zero);

                    int numberOfBranchedStrips = 0;
                    if (maxNumberOfBranchedStrips > 0)
                    {
                        int howManyBranchesRandom = Mathf.RoundToInt(Random.Range((float)minNumberOfBranchedStrips, (float)maxNumberOfBranchedStrips));
                        for (int n = 0; n < howManyBranchesRandom; n++)
                        {
                            for (int m = 0; m < maximumNumberOfBranchedAttempts; m++)
                            {
                                Ray rayb = new Ray(Vector3.Lerp(raypos, hit.point, 0.5f), Vector3.Normalize((hit.point + Random.insideUnitSphere.normalized * randomBranchedCircleMaxRadius * autoScaleValue) - raypos));
                                RaycastHit hitb;
                                if (Physics.Raycast(rayb, out hitb, (actualPossibleHitDistance / 1.5f)))
                                {
                                    eventAttribute.SetVector3("BranchedHitPosition" + numberOfBranchedStrips, hitb.point);
                                    numberOfBranchedStrips++;
                                    break;
                                }
                            }
                        }                        
                    }

                    //
                    // Workaround for Instantiation in Unity 2022 and above. Can't use GPU events, so the branch separation map should be dispatched along with all other attributes.
                    // Number of spawned particles is stored in a VFX Graph variable, because it can't be passed as an EventAttribute.
                    //

                    visualEffect.SetInt("BranchesCountHIDDEN", numberOfBranchedStrips);
                    int numberOfMainStrips = Random.Range(minNumberOfMainStrips, maxNumberOfMainStrips + 1);

                    visualEffect.SetInt("HowManyTimesBranchesWereTriggeredHIDDEN", howManyTimesBranchesWereTriggered);
                    

                    int count = 0;
                    if (numberOfBranchedStrips > 0)
                    {
                        eventAttribute.SetInt("BranchedIDBool" + count, 0);
                        count++;
                    }
                    if (numberOfBranchedStrips > 1)
                    {
                        eventAttribute.SetInt("BranchedIDBool" + count, 1);
                        count++;
                    }
                    if (numberOfBranchedStrips > 2)
                    {
                        eventAttribute.SetInt("BranchedIDBool" + count, 2);
                        count++;
                    }
                    if (numberOfMainStrips > 1)
                    {
                        eventAttribute.SetInt("BranchedIDBool" + count, 3);
                        count++;
                    }
                    if (numberOfMainStrips > 2)
                    {
                        eventAttribute.SetInt("BranchedIDBool" + count, 4);
                        count++;
                    }

                    visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
                    visualEffect.SetInt("TotalBranchedParticleCountHIDDEN", (count * 100));

                    //
                    // End of workaround solution
                    //

                    eventAttribute.SetVector3("HitSourcePosition", raypos);
                    eventAttribute.SetVector3("HitSourceLocalPosition", vd.vertexPositions[rand]);
                    eventAttribute.SetVector3("HitPosition", hit.point);
                    eventAttribute.SetInt("NumberOfMainStrips", numberOfMainStrips);
                    eventAttribute.SetFloat("TrueEandom", Random.Range(0f, 1f));
                    eventAttribute.SetFloat("AutoScaleValue", autoScaleValue);

                    visualEffect.SendEvent("CreateLightning", eventAttribute);

                    howManyTimesBranchesWereTriggered = howManyTimesBranchesWereTriggered + numberOfBranchedStrips;
                    howManyTimesVFXWasTriggered = howManyTimesVFXWasTriggered + count;
                    break;
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
        howManyTimesBranchesWereTriggered = 0;
        visualEffect.SetInt("HowManyTimesBranchesWereTriggeredHIDDEN", howManyTimesBranchesWereTriggered);
        howManyTimesVFXWasTriggered = 0;
        visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
    }

    // Public function for spawning a Lightning Event hitting the desired position from a random vertex. No Lightning Branches.
    public void ManualSpawnLightningEventFromRandomVertex(Vector3 HitPosition, int NumberOfMainStrips)
    {
        int rand = Random.Range(0, vd.vertexNormals.Length);
        eventAttribute.SetVector3("BranchedHitPosition0", Vector3.zero);
        eventAttribute.SetVector3("BranchedHitPosition1", Vector3.zero);
        eventAttribute.SetVector3("BranchedHitPosition2", Vector3.zero);
        eventAttribute.SetInt("BranchesCount", 0);
        eventAttribute.SetVector3("HitSourcePosition", mesh.transform.TransformPoint(vd.vertexPositions[rand]));
        eventAttribute.SetVector3("HitSourceLocalPosition", vd.vertexPositions[rand]);
        eventAttribute.SetVector3("HitPosition", HitPosition);
        eventAttribute.SetInt("NumberOfMainStrips", NumberOfMainStrips);
        eventAttribute.SetFloat("TrueEandom", Random.Range(0f, 1f));
        eventAttribute.SetFloat("AutoScaleValue", autoScaleValue);

        int count = 0;
        if (NumberOfMainStrips > 1)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 3);
            count++;
        }
        if (NumberOfMainStrips > 2)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 4);
            count++;
        }

        visualEffect.SetInt("BranchesCountHIDDEN", 0);
        visualEffect.SetInt("HowManyTimesBranchesWereTriggeredHIDDEN", 0);
        visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
        visualEffect.SetInt("TotalBranchedParticleCountHIDDEN", (count * 100));

        visualEffect.SendEvent("CreateLightning", eventAttribute);

        howManyTimesVFXWasTriggered = howManyTimesVFXWasTriggered + count;
    }

    // Public function for spawning a Lightning Event hitting the desired position from a random vertex. Manual Lightning Branches positions.
    // Use Vector3.zero if you don't want a Lightning Branch to spawn.
    public void ManualSpawnLightningEventRandomVertex(Vector3 BranchedHitPosition0, Vector3 BranchedHitPosition1, Vector3 BranchedHitPosition2, int BranchesCount, Vector3 HitSourcePosition, Vector3 HitSourceLocalPosition, Vector3 HitPosition, int NumberOfMainStrips, float AutoScaleValue)
    {
        eventAttribute.SetVector3("BranchedHitPosition0", BranchedHitPosition0);
        eventAttribute.SetVector3("BranchedHitPosition1", BranchedHitPosition1);
        eventAttribute.SetVector3("BranchedHitPosition2", BranchedHitPosition2);
        eventAttribute.SetInt("BranchesCount", BranchesCount);
        eventAttribute.SetVector3("HitSourcePosition", HitSourcePosition);
        eventAttribute.SetVector3("HitSourceLocalPosition", HitSourceLocalPosition);
        eventAttribute.SetVector3("HitPosition", HitPosition);
        eventAttribute.SetInt("NumberOfMainStrips", NumberOfMainStrips);
        eventAttribute.SetFloat("TrueEandom", Random.Range(0f, 1f));
        eventAttribute.SetFloat("AutoScaleValue", AutoScaleValue);

        int count = 0;
        if (BranchesCount > 0)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 0);
            count++;
        }
        if (BranchesCount > 1)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 1);
            count++;
        }
        if (BranchesCount > 2)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 2);
            count++;
        }
        if (NumberOfMainStrips > 1)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 3);
            count++;
        }
        if (NumberOfMainStrips > 2)
        {
            eventAttribute.SetInt("BranchedIDBool" + count, 4);
            count++;
        }

        visualEffect.SetInt("BranchesCountHIDDEN", BranchesCount);
        visualEffect.SetInt("HowManyTimesBranchesWereTriggeredHIDDEN", howManyTimesBranchesWereTriggered);
        visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
        visualEffect.SetInt("TotalBranchedParticleCountHIDDEN", (count * 100));

        visualEffect.SendEvent("CreateLightning", eventAttribute);

        howManyTimesBranchesWereTriggered = howManyTimesBranchesWereTriggered + BranchesCount;
        howManyTimesVFXWasTriggered = howManyTimesVFXWasTriggered + count;
    }

    // Process the final Maximum Distance value, which  can be affected by mesh lossy scale. If you planning to dynamically scale the mesh with VFX attached,
    // it is recommended to enable both "maxDistanceAffectedByMeshScale" and "meshScaleAutoScaleEnabled" parameters.
    private void ProcessMaxDistance()
    {
        if (maxDistanceAffectedByMeshScale == true)
        {
            processedMaxDistance = maxDistance * (mesh.transform.lossyScale.x + mesh.transform.lossyScale.y + mesh.transform.lossyScale.z) / 3f * maxDistanceAffectedByMeshScaleMultiply;
        }
        else
        {
            processedMaxDistance = maxDistance;
        }
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

    // Processing Auto Scale, scales the effects based on mesh lossy scale. Final VFX scale can be adjusted with "meshAutoScaleMultiply" variable.
    void ProcessAutoScale()
    {
        if (meshScaleAutoScaleEnabled == true)
        {
            meshAutoScaleFactor = (mesh.transform.lossyScale.x + mesh.transform.lossyScale.y + mesh.transform.lossyScale.z) / 3f * meshAutoScaleMultiply;
        }
        else
        {
            meshAutoScaleFactor = 1f;
        }

        autoScaleValue = 1f * autoScaleMultiply * meshAutoScaleFactor;
    }

    void ProcessNormalSlerpValue()
    {
        if (normalAdjustEnabled == true)
        {
            if (normalAdjust != normalAdjustPreviousValue)
            {
                normalAdjust = Vector3.Normalize(normalAdjust);
                normalAdjustPreviousValue = normalAdjust;
            }
            normalSlerpValue = normalAdjustAmount;
        }
        else
        {
            normalSlerpValue = 0f;
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false && Selection.Contains(this.gameObject) && previewNormalsInEditor == true)
        {
            ProcessNormalSlerpValue();
            float normalDistance;
            if (showNormalHitDistance == true)
            {
                ProcessMaxDistance();
                normalDistance = processedMaxDistance;
            }
            else
            {
                normalDistance = 1f;
            }

            MeshFilter mf = mesh.GetComponent<MeshFilter>();
            vd = new vertexData();
            vd.vertexPositions = mf.sharedMesh.vertices;
            vd.vertexNormals = mf.sharedMesh.normals;

            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;

            for (int i = 0; i < vd.vertexNormals.Length; i++)
            {
                float actualLength = transform.TransformVector(vd.vertexNormals[i]).magnitude;
                Gizmos.DrawLine(vd.vertexPositions[i], vd.vertexPositions[i] + Vector3.Slerp(vd.vertexNormals[i], normalAdjust, normalSlerpValue) * normalDistance / actualLength);
            }
        }
    }
    #endif
}
