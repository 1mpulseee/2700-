using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.VFX;

public class LightningSystemChain : MonoBehaviour
{
    public bool previewChainPointsInEditor = false;
    public bool vfxEnabled = true;
    [Space(10)]

    public Transform[] chainPoints;
    private Texture2D chainPointPositionsTexture;

    public float masterScale = 1f;
    public bool autoScaleEnabled = false;
    public Transform autoScaleAnchor;
    public float autoScaleMultiply = 1f;

    [Space(10)]    
    //public Camera myCamera;
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
    [Space(10)]
    public float randomBranchedCircleMinRadius = 0f;
    public float randomBranchedCircleMaxRadius = 1f;

    VisualEffect visualEffect;
    VFXEventAttribute eventAttribute;

    private float timerCurrent = 0f;
    private float autoScaleValue = 1f;
    private Color col = new Color(1f,1f,1f, 1f);

    public int howManyTimesVFXWasTriggered = 0;
    public int howManyTimesVFXWasTriggeredPerTrigger = 0;
    private int bonusBranchProbabilityResult = 0;

    // Start is called before the first frame update
    void Start()
    {
        visualEffect = GetComponent<VisualEffect>();
        eventAttribute = visualEffect.CreateVFXEventAttribute();
        chainPointPositionsTexture = new Texture2D(16, 16, TextureFormat.RGBAFloat,0,true);
        chainPointPositionsTexture.filterMode = FilterMode.Point;

        visualEffect.SetTexture("ChainPositionsTextureHIDDEN", chainPointPositionsTexture);
        visualEffect.SetInt("ChainCountHIDDEN", chainPoints.Length - 1);
        visualEffect.SetFloat("AutoScaleHIDDEN", autoScaleValue);

        eventAttribute.SetVector3("BranchedHitPosition0", Vector3.zero);
        eventAttribute.SetVector3("BranchedHitPosition1", Vector3.zero);
        eventAttribute.SetVector3("BranchedHitPosition2", Vector3.zero);
        eventAttribute.SetVector3("HitPosition", new Vector3(1f, 1f, 1f));

        ResetVFXParameters();
    }

    // Update is called once per frame
    void Update()
    {
        if(vfxEnabled == true)
            {
            ProcessSpeedVariation();
            ProcessAutoScale();
            SpawnLightningEvent();

            for (int i = 0; i < chainPoints.Length; i++)
            {
                col.r = chainPoints[i].position.x;
                col.g = chainPoints[i].position.y;
                col.b = chainPoints[i].position.z;
                chainPointPositionsTexture.SetPixel(i % 16,Mathf.FloorToInt((float)i / 16f), col);
            }
            chainPointPositionsTexture.Apply();

            visualEffect.SetInt("ChainCountHIDDEN", chainPoints.Length - 1);
            visualEffect.SetFloat("AutoScaleHIDDEN", autoScaleValue);
            visualEffect.SetBool("VFXEnabledHIDDEN", vfxEnabled);
        }
        else
        {
            visualEffect.SetBool("VFXEnabledHIDDEN", vfxEnabled);
        }
    }

    // Spawning a Lightning Event for each connecter chain points
    void SpawnLightningEvent()
    {
        if (timerCurrent >= 1f)
        {
            if (chainPoints.Length > 1)
            {
                int numberOfMainStrips = Random.Range(minNumberOfMainStrips, maxNumberOfMainStrips + 1);

                eventAttribute.SetInt("NumberOfMainStrips", numberOfMainStrips);
                eventAttribute.SetFloat("AutoScaleValue", autoScaleValue);
                eventAttribute.SetFloat("TrueEandom", Random.Range(0f, 1f));

                //
                // Workaround for Instantiation in Unity 2022 and above. Can't use GPU events, so the branch separation map should be dispatched along with all other attributes.
                //

                ProcessBonusBranchProbability();
                int count = 0;
                if (bonusBranchProbabilityResult == 1)
                {
                    eventAttribute.SetBool("ArcIsPresent", true);
                    count++;
                }
                else
                {
                    eventAttribute.SetBool("ArcIsPresent", false);
                }
                if (numberOfMainStrips > 1)
                {
                    count++;
                }
                if (numberOfMainStrips > 2)
                {
                    count++;
                }

                eventAttribute.SetInt("howManyTimesVFXWasTriggered", howManyTimesVFXWasTriggered);
                visualEffect.SetInt("HowManyTimesVFXWasTriggeredHIDDEN", howManyTimesVFXWasTriggered);
                howManyTimesVFXWasTriggeredPerTrigger = count;
                visualEffect.SetInt("HowManyTimesVFXWasTriggeredPerTriggerHIDDEN", howManyTimesVFXWasTriggeredPerTrigger);
                visualEffect.SetInt("TotalBranchedParticleCountHIDDEN", count * 50);

                visualEffect.SendEvent("CreateLightning", eventAttribute);
                howManyTimesVFXWasTriggered = howManyTimesVFXWasTriggered + (count * (chainPoints.Length - 1));

                //
                // End of workaround solution
                //
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

    // Calculating additive bonus branch probability
    void ProcessBonusBranchProbability()
    {
        if (Random.Range(0f, 1f) > (1f - bonusBranchProbability))
        {
            bonusBranchProbabilityResult = 1;
        }
        else
        {
            bonusBranchProbabilityResult = 0;
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

    // Processing Auto Scale, there are two modes, automatic and standard
    // In Automatic Mode the VFX uses one Transform as an Anchor
    // Standard Mode is used when you need to scale the VFX separately in real-time
    void ProcessAutoScale()
    {
        if(autoScaleEnabled == true)
        {
            autoScaleValue = (autoScaleAnchor.lossyScale.x + autoScaleAnchor.lossyScale.y + autoScaleAnchor.lossyScale.z) / 3f * autoScaleMultiply;
        }
        else
        {
            autoScaleValue = masterScale;
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false && previewChainPointsInEditor == true)
        {
            foreach (Transform tp in chainPoints)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(tp.position, 0.125f);
            }
        }            
    }
    #endif
}
