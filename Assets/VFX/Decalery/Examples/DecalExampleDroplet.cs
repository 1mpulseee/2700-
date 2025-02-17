using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecalExampleDroplet : MonoBehaviour
{
	public DecalType splatDecal;
	public float dropletRadius = 1.0f;
	public float initialVelocity = 0.0f;
	public float interval = 1.0f;
	public int numSpheres = 3;
	public float splashDropletRadius = 0.7f;
	public float splashImpulse = 1.0f;
	public float splatSize = 2.0f;
	public int maxTrisTotal = 1024;
	public int maxTrisInDecal = 1024;
	public int maxDecalsPerFrame = 1;

	const int maxSplatsRemembered = 50;
	public static Vector3[] lastSplatPositions;
	public static int numLastSplatPositions;
	static int nextSplatIndex;

	Vector3 velocity;

	Transform tform;
	Vector3 origPos;
	float timer;

	GameObject sphere;

 	DecalSpawner dsSplat;

	class SplashDroplet
	{
		public Transform tform;
		public Vector3 velocity;
	}

	List<SplashDroplet> spheres;

	void Start()
	{
		tform = transform;
		origPos = tform.position;
		velocity = -Vector3.up * initialVelocity;
		sphere = tform.Find("Sphere").gameObject;
		sphere.transform.parent = null;
		spheres = new List<SplashDroplet>();
		lastSplatPositions = new Vector3[maxSplatsRemembered];
		dsSplat = DecalManager.GetSpawner(splatDecal.decalSettings, maxTrisTotal, maxTrisInDecal);
	}

	void Splat(Vector3 origin, Quaternion rotation, RaycastHit hit)
	{
		float distance = 1.0f;
		float opacity = 1.0f;
		float angleClip = 0.0f;
		Transform rootObject = null;
		if (hit.rigidbody != null) rootObject = hit.rigidbody.transform;
		//dsSplat.AddDecal(origin, rotation, hit.collider.gameObject, splatSize, splatSize, distance, opacity, angleClip, rootObject);
		dsSplat.AddDecalToQueue(origin, rotation, hit.collider.gameObject, splatSize, splatSize, distance, opacity, angleClip, rootObject);

		lastSplatPositions[nextSplatIndex] = hit.point;
		nextSplatIndex++;
		numLastSplatPositions = System.Math.Max(nextSplatIndex, numLastSplatPositions);
		if (nextSplatIndex == maxSplatsRemembered)
		{
			nextSplatIndex = 0;
		}
	}

	void Update()
	{
		dsSplat.UpdateQueue(maxDecalsPerFrame);
	}

    void FixedUpdate()
    {
    	Vector3 prevPos, curPos;
    	RaycastHit hit;

    	spheres.RemoveAll(s => s.tform == null);

    	for(int i=0; i<spheres.Count; i++)
    	{
    		var stform = spheres[i].tform;
	    	prevPos = stform.position;

	    	spheres[i].velocity += Physics.gravity * Time.deltaTime;
	    	stform.position += spheres[i].velocity * Time.deltaTime;

	    	curPos = stform.position;
	    	if (curPos.y < -100)
	    	{
	    		Destroy(stform.gameObject);
	    		continue;
	    	}
	    	var dir = curPos - prevPos;
	    	if (Physics.Raycast(prevPos, dir.normalized, out hit, dir.magnitude))
	    	{
	    		Splat(prevPos, Quaternion.AngleAxis(Random.value*360, dir) * Quaternion.LookRotation(dir), hit);

	    		Destroy(stform.gameObject);
	    	}
    	}

    	if (Time.time < timer) return;

    	prevPos = tform.position;

    	velocity += Physics.gravity * Time.deltaTime;
    	tform.position += velocity * Time.deltaTime;

    	curPos = tform.position;

    	if (Physics.SphereCast(prevPos, dropletRadius, -Vector3.up, out hit, (curPos - prevPos).magnitude))
    	{
    		//var rndXZ = Random.onUnitSphere * dropletRadius;

    		//Splat(prevPos + new Vector3(rndXZ.x, 0, rndXZ.y), Quaternion.AngleAxis(Random.value*360, Vector3.up) * Quaternion.LookRotation(-Vector3.up), hit);

    		SpawnSpheres(hit.point + hit.normal * dropletRadius);
    		tform.position = origPos;
    		velocity = -Vector3.up * initialVelocity;
    		timer += interval;
    	}
    }

    void SpawnSpheres(Vector3 pos)
    {
    	for(int i=0; i<numSpheres; i++)
    	{
    		var newSphere = new SplashDroplet();
	    	var s = Instantiate(sphere);
	    	var dir = Random.insideUnitSphere;
	    	newSphere.tform = s.transform;
	    	newSphere.tform.position = pos + (dropletRadius - splashDropletRadius) * dir;
	    	newSphere.velocity = dir.normalized * Random.value * splashImpulse;
	    	s.SetActive(true);
	    	spheres.Add(newSphere);
	    }
    }
}
