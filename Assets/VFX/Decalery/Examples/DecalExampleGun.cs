using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecalExampleGun : MonoBehaviour
{
	public DecalType holeConcreteDecal;
	public DecalType holeMetalDecal;
	public float spread = 0.1f;
	public float rotationSpeed = 36.0f;
	public float rate = 0.1f;
	public float bulletSpeed = 1.0f;
	public float holeSize = 0.2f;
	public int maxTrisTotal = 1024;
	public int maxTrisInDecal = 256;

	Transform tform;
	Quaternion cur, target;
	Vector3 fwd;
	float nextRotationTime, nextShootTime;

	GameObject bullet;

	DecalSpawner dsConcrete, dsMetal;

	struct Bullet
	{
		public Transform tform;
		public float endTime;
	}

	List<Bullet> bullets = new List<Bullet>();

    void Start()
    {
    	tform = transform;
    	cur = tform.rotation;
    	fwd = tform.forward;
    	bullet = tform.Find("bullet").gameObject;
    	dsConcrete = DecalManager.GetSpawner(holeConcreteDecal.decalSettings, maxTrisTotal, maxTrisInDecal);
    	dsMetal = DecalManager.GetSpawner(holeMetalDecal.decalSettings, maxTrisTotal, maxTrisInDecal);
    }

    void Shoot()
    {
    	var bg = Instantiate(bullet);
    	bg.SetActive(true);

    	var b = new Bullet();
    	b.tform = bg.transform;
    	b.tform.position = bullet.transform.position;
    	b.tform.rotation = bullet.transform.rotation;
    	b.endTime = Time.time + 5.0f;
    	bullets.Add(b);
    }

	void AddBulletHole(Vector3 origin, Quaternion rotation, RaycastHit hit)
	{
		float distance = 1.0f;
		float opacity = 1.0f;
		float angleClip = 0.0f;
		Transform rootObject = null;
		if (hit.rigidbody != null) rootObject = hit.rigidbody.transform;

		int materialID = 0;
		var mat = hit.collider.sharedMaterial;
		if (mat != null)
		{
			materialID = (int)Mathf.Round((mat.bounciness - Mathf.Floor(mat.bounciness)) * 100); // funny way to encode material ID in physicmaterial properties' fractional part
		}

		(materialID == 1 ? dsMetal : dsConcrete).AddDecalToQueue(origin, rotation, hit.collider.gameObject, holeSize, holeSize, distance, opacity, angleClip, rootObject);
	}

    void Update()
    {
		dsConcrete.UpdateQueue(1);
		dsMetal.UpdateQueue(1);

        if (Time.time > nextRotationTime)
        {
        	target = Quaternion.LookRotation(fwd + Random.onUnitSphere * spread);
        	nextRotationTime = Time.time + 0.1f;
        }

        cur = Quaternion.RotateTowards(cur, target, Time.deltaTime * rotationSpeed);
        tform.rotation = cur;

        if (Time.time > nextShootTime)
        {
        	Shoot();
        	nextShootTime = Time.time + rate;
        }

        RaycastHit hit;

    	bullets.RemoveAll(b => b.tform == null);

    	for(int i=0; i<bullets.Count; i++)
    	{
    		var btform = bullets[i].tform;

    		if (Time.time > bullets[i].endTime)
    		{
    			Destroy(btform.gameObject);
    			continue;
    		}

	    	var prevPos = btform.position;
	    	btform.position += btform.forward * (bulletSpeed * Time.deltaTime);
	    	var curPos = btform.position;

	    	var dir = curPos - prevPos;
	    	if (Physics.Raycast(prevPos, dir.normalized, out hit, dir.magnitude))
	    	{
	    		AddBulletHole(prevPos, Quaternion.AngleAxis(Random.value*360, dir) * Quaternion.LookRotation(dir), hit);

	    		Destroy(btform.gameObject);
	    	}
    	}

    }
}
