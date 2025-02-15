using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleManualVFXSpawn : MonoBehaviour
{

    public LightningSystemMeshRaycast lightningSystemMeshRaycast;
    public LightningSystem lightningSystem;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Example how to manually spawn a Lightning VFX.
        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if(lightningSystem != null && lightningSystem.isActiveAndEnabled == true)
                {
                    if (lightningSystem.gameObject.activeSelf == true)
                    {
                        lightningSystem.ManualSpawnLightningEventFromRandomVertex(hit.point, 3);
                        Debug.Log("lightningSystem Manual VFX Spawned");
                    }
                }
                if(lightningSystemMeshRaycast != null && lightningSystemMeshRaycast.isActiveAndEnabled == true)
                {
                    if (lightningSystemMeshRaycast.gameObject.activeSelf == true)
                    {
                        lightningSystemMeshRaycast.ManualSpawnLightningEventFromRandomVertex(hit.point, 3);
                        Debug.Log("lightningSystemMeshRaycast Manual VFX Spawned");
                    }
                }
            }
        }
    }
}
