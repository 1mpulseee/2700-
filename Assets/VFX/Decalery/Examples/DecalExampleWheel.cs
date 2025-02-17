using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecalExampleWheel : MonoBehaviour
{
	public DecalType decalTrack;
	public float rayLength = 0.6f;
	public float trackWidth = 0.1f;
	public float fadeOutSpeed = 0.1f;
	public int maxTrisTotal = 4096;
	public int maxTrisInDecal = 1024;

	Transform tform;
	DecalSpawner dsTrack;
	float opacity;

	const float splatRadiusSq = 0.5f * 0.5f;

    // Start is called before the first frame update
    void Start()
    {
    	tform = transform;
        dsTrack = DecalManager.CreateUniqueSpawner(decalTrack.decalSettings, maxTrisTotal, maxTrisInDecal);
    }

    // Update is called once per frame
    void Update()
    {	
    	var pos = tform.position;
    	for(int i=0; i<DecalExampleDroplet.numLastSplatPositions; i++)
    	{
    		if ((DecalExampleDroplet.lastSplatPositions[i] - pos).sqrMagnitude < splatRadiusSq)
    		{
    			opacity = 1.0f;
    			break;
    		}
    	}

    	if (opacity > 0)
    	{
	    	RaycastHit hit;
	    	if (Physics.Raycast(pos, tform.forward, out hit, rayLength))
	    	{
	    		float angleClip = 0.0f;
				Transform rootObject = null;
				if (hit.rigidbody != null) rootObject = hit.rigidbody.transform;
	        	dsTrack.AddDecal(pos, tform.rotation, hit.collider.gameObject, trackWidth, trackWidth, rayLength, opacity, angleClip, rootObject);
	        }
	    }

        opacity -= Time.deltaTime * fadeOutSpeed;
        if (opacity < 0) opacity = 0;
    }
}
