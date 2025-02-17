using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DecalExampleProjector : MonoBehaviour
{
	public DecalType decalType;
	public float width = 0.1f;
	public float height = 0.1f;
	public float opacity = 1.0f;
	public float distance = 1.0f;
	public float angleClip = 0.5f;
	public int maxTrisTotal = 4096;
	public int maxTrisInDecal = 1024;
	public bool useInterval = false;
	public float interval = 0.1f;
	
	DecalSpawner ds;

	float timer;

	void Start()
	{
		ds = DecalManager.GetSpawner(decalType.decalSettings, maxTrisTotal, maxTrisInDecal);
	}

	void Update()
	{
		if (Time.time > timer)
		{
			RaycastHit hit;
			if (Physics.Raycast(transform.position, transform.forward, out hit, distance))
			{
				Transform rootObject = null;
				if (hit.rigidbody != null) rootObject = hit.rigidbody.transform;
				ds.AddDecal(transform.position, transform.rotation, hit.collider.gameObject, width, height, distance, opacity, angleClip, rootObject);

				timer = Time.time + interval;
				if (!useInterval) enabled = false;
			}
		}
	}
}
