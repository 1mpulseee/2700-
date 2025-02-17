using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DecalGlobalRuntimeSettings : MonoBehaviour
{
	public enum Mode
	{
		CPU,
		CPUBurst,
		GPUWithReadback,
		FullGPU
	}

	public enum FullGPUShaderPass
	{
		ForwardBase,
		Deferred
	}

	public Mode mode;
	public FullGPUShaderPass shaderPass;
	public bool renderFullGPUModeWithBuiltInRenderPipeline = false;

#if UNITY_2018_1_OR_NEWER
	Vector3[] positions;
	SphericalHarmonicsL2[] shs;
	Vector4[] occlusions;
#endif
	Camera cam;
	bool deferred;
	int pass;

    void Awake()
    {
    	DecalManager.Cleanup();

		deferred = shaderPass == FullGPUShaderPass.Deferred;
		pass = deferred ? 2 : 0;
		if (mode == Mode.CPU)
		{
			DecalManager.SetPreferredMode(DecalUtils.Mode.CPU, false, pass);
			enabled = false;
		}
		else if (mode == Mode.CPUBurst)
		{
			DecalManager.SetPreferredMode(DecalUtils.Mode.CPUBurst, false, pass);
			enabled = false;
		}
		else if (mode == Mode.GPUWithReadback)
		{
			DecalManager.SetPreferredMode(DecalUtils.Mode.GPU, false, pass);
			enabled = false;
		}
		else
		{
			DecalManager.SetPreferredMode(DecalUtils.Mode.GPU, true, pass);

			if (renderFullGPUModeWithBuiltInRenderPipeline)
			{
#if UNITY_2018_1_OR_NEWER
				positions = new Vector3[1];
				shs = new SphericalHarmonicsL2[1];
				occlusions = new Vector4[1];
#endif
			}
			else
			{
				enabled = false;
			}
		}
    }

    void LateUpdate()
    {
    	if (mode != Mode.FullGPU) return;

		if (cam == null) cam = Camera.current;
		if (cam == null) cam = Camera.main;
		if (cam != null)
		{
			Plane[] planes6;
			planes6 = GeometryUtility.CalculateFrustumPlanes(cam);

			var evt = deferred ? CameraEvent.AfterGBuffer : CameraEvent.AfterForwardOpaque;

			foreach(var dtype in DecalSpawner.All)
			{
				foreach(var pair in dtype.staticGroups)
				{
					var group = pair.Value;
					if (group.drawCmd == null) continue;
					cam.RemoveCommandBuffer(evt, group.drawCmd);
					if (group.numDecals == 0) continue;
					if (!group.indirectDraw) continue;
					if (GeometryUtility.TestPlanesAABB(planes6, group.bounds))
					{
						cam.AddCommandBuffer(evt, group.drawCmd);
					}
				}
				foreach(var pair in dtype.movableGroups)
				{
					var group = pair.Value;
					if (group.drawCmd == null) continue;
					cam.RemoveCommandBuffer(evt, group.drawCmd);
					if (group.numDecals == 0) continue;
					if (!group.indirectDraw) continue;
					if (GeometryUtility.TestPlanesAABB(planes6, group.parent.bounds))
					{
						GPUDecalUtils.UpdateDrawIndirectCommandBuffer(group, group.parent.localToWorldMatrix, pass);

#if UNITY_2018_1_OR_NEWER
						positions[0] = group.parent.bounds.center;
						if (group.materialPropertyBlock == null) group.materialPropertyBlock = new MaterialPropertyBlock();
						LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, shs, occlusions);
						group.materialPropertyBlock.CopySHCoefficientArraysFrom(shs);
						group.materialPropertyBlock.CopyProbeOcclusionArrayFrom(occlusions);
#endif

						cam.AddCommandBuffer(evt, group.drawCmd);
					}
				}
			}
		}
    }
}
