using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.VFX;

public class LightningSystem : MonoBehaviour
{
    public bool previewGizmosInEditor = false;
    public enum lsmodes
    {
        Spherical,
        Cone
    }
    public lsmodes modes;

    [Range(0,1)]
    public float hemisphereDistance = 1f;

    [Space(10)]
    public float maxDistance = 8f;
    public bool maxDistanceAutoScaleEnabled = true;
    public bool maxDistanceAffectedByAnchor = false;
    public Transform maxDistanceAnchor;
    public float maxDistanceAnchorMultiply = 1f;

    [Space(10)]
    public float autoScaleMultiply = 1f;

    [Space(10)]
    public float coneModeCircleMinRadius = 0f;
    public float coneModeCircleMaxRadius = 2f;
    public int maximumNumberOfAttempts = 8;
    public int maximumNumberOfBranchedAttempts = 8;

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
    private int howManyTimesBranchesWereTriggered = 0;
    private int howManyTimesVFXWasTriggered = 0;

    // Start is called before the first frame update
    void Start()
    {
        visualEffect = GetComponent<VisualEffect>();
        eventAttribute = visualEffect.CreateVFXEventAttribute();

        ResetVFXParameters();
    }

    // Update is called once per frame
    void Update()
    {
        ProcessSpeedVariation();
        ProcessMaxDistance();
        ProcessAutoScale();
        SpawnLightningEvent();
    }

    // Spawning a Lightning Event if the main raycast hits. Setting all necessary attributes for the VFX Graph.
    void SpawnLightningEvent()
    {
        if (timerCurrent >= 1f)
        {
            Vector3 direction = Vector3.zero;
            for (int i = 0; i < maximumNumberOfAttempts; i++)
            {
                switch (modes)
                {
                    case lsmodes.Spherical:
                        direction = Random.insideUnitSphere.normalized;
                        actualPossibleHitDistance = processedMaxDistance;
                        break;
                    case lsmodes.Cone:
                        float randomRadius = Random.Range(coneModeCircleMinRadius, coneModeCircleMaxRadius);
                        float randomAngle = Random.Range(0, 2 * Mathf.PI);
                        direction = new Vector3(randomRadius * Mathf.Cos(randomAngle), randomRadius * Mathf.Sin(randomAngle), processedMaxDistance);
                        actualPossibleHitDistance = Vector3.Magnitude(direction);
                        direction = transform.TransformDirection(direction.normalized);
                        break;
                }

                Ray ray = new Ray(this.transform.position, direction);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, actualPossibleHitDistance))
                {
                    if (Vector3.Dot(direction, transform.forward) < hemisphereDistance)
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
                                    Ray rayb = new Ray(Vector3.Lerp(this.transform.position, hit.point, 0.5f), Vector3.Normalize((hit.point + Random.insideUnitSphere.normalized * randomBranchedCircleMaxRadius * autoScaleValue) - this.transform.position));
                                    RaycastHit hitb;
                                    if (Physics.Raycast(rayb, out hitb, (actualPossibleHitDistance / 1.5f)))
                                    {
                                        eventAttribute.SetVector3("BranchedHitPosition" + numberOfBranchedStrips, hitb.point);
                                        numberOfBranchedStrips++;
                                        break;
                                    }
                                }
                            }
                            eventAttribute.SetInt("BranchesCount", numberOfBranchedStrips);
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

                        eventAttribute.SetVector3("HitPosition", hit.point);
                        eventAttribute.SetInt("NumberOfMainStrips", Random.Range(minNumberOfMainStrips, maxNumberOfMainStrips + 1));
                        eventAttribute.SetFloat("TrueEandom", Random.Range(0f, 1f));
                        eventAttribute.SetFloat("AutoScaleValue", autoScaleValue);

                        visualEffect.SendEvent("CreateLightning", eventAttribute);

                        howManyTimesBranchesWereTriggered = howManyTimesBranchesWereTriggered + numberOfBranchedStrips;
                        howManyTimesVFXWasTriggered = howManyTimesVFXWasTriggered + count;
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
        howManyTimesBranchesWereTriggered = 0;
        visualEffect.SetInt("HowManyTimesBranchesWereTriggeredHIDDEN", howManyTimesBranchesWereTriggered);
        howManyTimesVFXWasTriggered = 0;
        visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
    }

    // Public function for spawning a Lightning Event hitting the desired position from a random vertex. No Lightning Branches.
    public void ManualSpawnLightningEventFromRandomVertex(Vector3 HitPosition, int NumberOfMainStrips)
    {
        eventAttribute.SetVector3("BranchedHitPosition0", Vector3.zero);
        eventAttribute.SetVector3("BranchedHitPosition1", Vector3.zero);
        eventAttribute.SetVector3("BranchedHitPosition2", Vector3.zero);
        eventAttribute.SetInt("BranchesCount", 0);
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
    public void ManualSpawnLightningEventRandomVertex(Vector3 BranchedHitPosition0, Vector3 BranchedHitPosition1, Vector3 BranchedHitPosition2, int BranchesCount, Vector3 HitPosition, int NumberOfMainStrips, float AutoScaleValue)
    {
        eventAttribute.SetVector3("BranchedHitPosition0", BranchedHitPosition0);
        eventAttribute.SetVector3("BranchedHitPosition1", BranchedHitPosition1);
        eventAttribute.SetVector3("BranchedHitPosition2", BranchedHitPosition2);
        eventAttribute.SetInt("BranchesCount", BranchesCount);
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
        if (maxDistanceAffectedByAnchor == true && maxDistanceAnchor != null)
        {
            processedMaxDistance = maxDistance * (maxDistanceAnchor.transform.lossyScale.x + maxDistanceAnchor.transform.lossyScale.y + maxDistanceAnchor.transform.lossyScale.z) / 3f * maxDistanceAnchorMultiply;
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
        if (maxDistanceAutoScaleEnabled == true)
        {
            meshAutoScaleFactor = processedMaxDistance / 8f;
        }
        else
        {
            meshAutoScaleFactor = 1f;
        }
        
        autoScaleValue = 1f * autoScaleMultiply * meshAutoScaleFactor;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false && previewGizmosInEditor == true && Selection.Contains(this.gameObject))
        {
            ProcessMaxDistance();

            switch (modes)
            {
                case lsmodes.Spherical:
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(this.transform.position, processedMaxDistance);
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(this.transform.position + transform.forward * processedMaxDistance * hemisphereDistance, transform.forward, Mathf.Sin(Mathf.Acos(hemisphereDistance)) * processedMaxDistance);
                    break;
                case lsmodes.Cone:
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(this.transform.position, this.transform.position + transform.forward * processedMaxDistance + transform.TransformDirection(new Vector3(0, coneModeCircleMaxRadius, 0)));
                    Gizmos.DrawLine(this.transform.position, this.transform.position + transform.forward * processedMaxDistance + transform.TransformDirection(new Vector3(0, -coneModeCircleMaxRadius, 0)));
                    Gizmos.DrawLine(this.transform.position, this.transform.position + transform.forward * processedMaxDistance + transform.TransformDirection(new Vector3(coneModeCircleMaxRadius, 0, 0)));
                    Gizmos.DrawLine(this.transform.position, this.transform.position + transform.forward * processedMaxDistance + transform.TransformDirection(new Vector3(-coneModeCircleMaxRadius, 0, 0)));
                    Handles.color = Color.cyan;
                    Handles.DrawWireDisc(this.transform.position + transform.forward * processedMaxDistance, transform.forward, coneModeCircleMaxRadius);
                    Handles.DrawWireDisc(this.transform.position + transform.forward * processedMaxDistance, transform.forward, coneModeCircleMinRadius);
                    break;
            }
        }        
    }
    #endif
}
