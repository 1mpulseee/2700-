using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DecalManager
{
	static Dictionary<DecalSpawner.InitData, DecalSpawner> spawners = new Dictionary<DecalSpawner.InitData, DecalSpawner>();
	static Dictionary<string, DecalSpawner.InitData> typeNameToData = new Dictionary<string, DecalSpawner.InitData>();
	static DecalUtils.Mode preferredMode = DecalUtils.Mode.CPU;
	static bool preferredIndirect = false;
	static int preferredShaderPass = 0;

	public static void Cleanup()
	{
		DecalSpawner.Cleanup();
		GPUDecalUtils.Cleanup();
		spawners = new Dictionary<DecalSpawner.InitData, DecalSpawner>();
		typeNameToData = new Dictionary<string, DecalSpawner.InitData>();
	}

	public static DecalSpawner GetSpawner(DecalSpawner.InitData initData, int maxTrisTotal, int maxTrisInDecal)
	{
		DecalSpawner d;
		if (spawners.TryGetValue(initData, out d))
		{
			return d;
		}
		d = new DecalSpawner();
		
		d.initData = initData;
		d.Init(maxTrisTotal, maxTrisInDecal, preferredMode, preferredIndirect, preferredShaderPass);

		spawners[initData] = d;
		return d;
	}

	public static DecalSpawner GetSpawner(string name, int maxTrisTotal, int maxTrisInDecal)
	{
		DecalSpawner.InitData initData = null;
		if (!typeNameToData.TryGetValue(name, out initData))
		{
			var decalType = Resources.Load("DecalTypes/"+name, typeof(DecalType)) as DecalType;
			if (decalType == null)
			{
				Debug.LogError("Can't find Resources/DecalTypes/" + name);
				return null;
			}
			initData = decalType.decalSettings;
			typeNameToData[name] = initData;
		}
		return GetSpawner(initData, maxTrisTotal, maxTrisInDecal);
	}

	public static void SetPreferredMode(DecalUtils.Mode mode, bool drawIndirect, int shaderPass)
	{
		preferredMode = mode;
		preferredIndirect = drawIndirect;
		preferredShaderPass = shaderPass;
	}

	public static DecalSpawner CreateUniqueSpawner(DecalSpawner.InitData initData, int maxTrisTotal, int maxTrisInDecal)
	{
		var d = new DecalSpawner();
		
		d.initData = initData;
		d.Init(maxTrisTotal, maxTrisInDecal, preferredMode, preferredIndirect, preferredShaderPass);

		return d;
	}

	public static DecalSpawner CreateUniqueSpawner(string name, int maxTrisTotal, int maxTrisInDecal)
	{
		DecalSpawner.InitData initData = null;
		if (!typeNameToData.TryGetValue(name, out initData))
		{
			var decalType = Resources.Load("DecalTypes/"+name, typeof(DecalType)) as DecalType;
			if (decalType == null)
			{
				Debug.LogError("Can't find Resources/DecalTypes/" + name);
				return null;
			}
			initData = decalType.decalSettings;
			typeNameToData[name] = initData;
		}

		var d = new DecalSpawner();
		d.initData = initData;
		d.Init(maxTrisTotal, maxTrisInDecal, preferredMode, preferredIndirect, preferredShaderPass);

		return d;
	}
}

