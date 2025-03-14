/*
   Copyright (c) 2023 Léo Chaumartin
   All rights reserved.
*/

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.ShaderGraph;


namespace Seamless.SGExtension
{
    static internal class TextureBaker
    {
        static string path = "Assets/SeamlessSGExtension/Export";

        static internal void Bake(ExportNode node)
        {
            if (node.OutputTexture == null)
            {
                string absPath = EditorUtility.SaveFilePanel("Save Path", path.Substring(0, path.IndexOf(path.Split('/')[path.Split('/').Length - 1])), path.Split('/')[path.Split('/').Length - 1], node.TextureType == TextureType.Raw ? "raw" : "png");

                if (absPath.Contains(Application.dataPath))
                {
                    path = absPath.Substring(Application.dataPath.Length);
                    if (path.StartsWith("/"))
                        path = path.Substring(1);
                    path = "Assets/" + path;
                }
                else
                {
                    if (absPath != "")
                        UnityEngine.Debug.LogWarning("Invalid path: " + absPath + ". Please save the file under the Assets/ folder");
                }
            }
            else
            {
                path = AssetDatabase.GetAssetPath(node.OutputTexture);
            }

            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            Material material = GetPreviewMaterial(node, materialPropertyBlock, node.Alpha.isOn);
            if (material == null)
                return;

            Vector2Int textureSize = node.GetResolution();

            string lastSavedFilePath = string.Empty;

            UnityEditor.EditorUtility.DisplayProgressBar("Baking...", string.Empty, 0);

            Texture2D texture = GetPreviewTexture(material, materialPropertyBlock, textureSize.x, textureSize.y, false, node.TextureType);
            if (texture != null)
            {
                string savePath = path;

                byte[] bytes; 
                if(node.TextureType == TextureType.Raw)
                    bytes = texture.GetRawTextureData();
                else
                    bytes = texture.EncodeToPNG();
                File.WriteAllBytes(savePath, bytes);
                GameObject.DestroyImmediate(texture);

                AssetDatabase.Refresh();

                UpdateImportSettings(savePath, node.TextureType);

                lastSavedFilePath = savePath;
            }

            UnityEditor.EditorUtility.ClearProgressBar();


            UnityEngine.Object lastSavedFile = AssetDatabase.LoadAssetAtPath(lastSavedFilePath, typeof(Texture));

            if (node.OutputTexture == null)
                UnityEditor.EditorGUIUtility.PingObject(lastSavedFile);
            node.OutputTexture = lastSavedFile as Texture;

            //Cleanup           
            GameObject.DestroyImmediate(material.shader);
            GameObject.DestroyImmediate(material);

            Resources.UnloadUnusedAssets();
        }
        
        static Texture2D GetPreviewTexture(Material material, MaterialPropertyBlock materialPropertyBlock, int width, int height, bool mipChain, TextureType textureType)
        {
            TextureFormat textureFormat;
            RenderTextureFormat renderTextureFormat;
            switch (textureType)
            {
                case TextureType.Raw:
                    textureFormat = TextureFormat.R16;
                    renderTextureFormat = RenderTextureFormat.R16;
                    break;
                case TextureType.Default:
                case TextureType.Normal:
                default:
                    textureFormat = TextureFormat.ARGB32;
                    renderTextureFormat = RenderTextureFormat.ARGB32;
                    break;

            }

            RenderTextureReadWrite colorSpace = textureType == TextureType.Normal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 16, renderTextureFormat, colorSpace);
            Texture2D texture = new Texture2D(width, height, textureFormat, mipChain);
            RenderTexture.active = null;

            // In version 1.0, We just used Graphics.Blit to bake the texture, although it wasn't allowing to pass the materialPropertyBlock, leading to eventually missing external texture
            // This approach is a bit more intrusive, but works in any case
            GameObject camGO = new GameObject();
            camGO.transform.position = Vector3.back;
            camGO.hideFlags = HideFlags.HideAndDontSave;
            
            Camera cam = camGO.AddComponent<Camera>();
            cam.hideFlags = HideFlags.HideAndDontSave;
            cam.enabled = false;
            cam.cameraType = CameraType.Preview;
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.farClipPlane = 10.0f;
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = Color.clear;
            cam.renderingPath = RenderingPath.Forward;
            cam.useOcclusionCulling = false;
            cam.allowMSAA = false;
            cam.allowHDR = true;

            int previewLayer = 31;
            cam.cullingMask = 1 << previewLayer;

            cam.targetTexture = renderTexture;
            Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
            quadMesh.hideFlags = HideFlags.HideAndDontSave;
            
            Graphics.DrawMesh(quadMesh, Matrix4x4.identity, material, previewLayer, cam, 0, materialPropertyBlock, ShadowCastingMode.Off, false, null, false);
            cam.Render();

            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            GameObject.DestroyImmediate(camGO);
            RenderTexture.ReleaseTemporary(renderTexture);
            
            return texture;
        }

        static Material GetPreviewMaterial(ExportNode exportNode, MaterialPropertyBlock materialPropertyBlock, bool includeAlpha)
        {
            var originalAsyncCompilationValue = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = true;

            HashSet<AbstractMaterialNode> sourceNodes = new HashSet<AbstractMaterialNode>() { exportNode };
            HashSet<AbstractMaterialNode> nodesToDraw = new HashSet<AbstractMaterialNode>();
            PreviewManager.PropagateNodes(sourceNodes, PreviewManager.PropagationDirection.Upstream, nodesToDraw);

            PooledList<PreviewProperty> perMaterialPreviewProperties = PooledList<PreviewProperty>.Get();
            PreviewManager.CollectPreviewProperties(exportNode.owner, nodesToDraw, perMaterialPreviewProperties, materialPropertyBlock);

            Generator generator = new Generator(exportNode.owner, exportNode, GenerationMode.ForReals, $"hidden/preview/{exportNode.GetVariableNameForNode()}", null);
            string generatedShader = generator.generatedShader;

            if (includeAlpha)
            {
                ShaderStringBuilder shaderStringBuilder = new ShaderStringBuilder();
                exportNode.GenerateNodeCode(shaderStringBuilder, GenerationMode.Preview);
                string nodeCode = shaderStringBuilder.ToString();
                nodeCode = nodeCode.Substring(0, nodeCode.IndexOf(';'));
                if (nodeCode.Contains("$precision4 "))
                {
                    string variableName = nodeCode.Substring(nodeCode.IndexOf(" ") + 1);
                    generatedShader = generatedShader.Replace($"{variableName}.z, 1.0", $"{variableName}.z, {variableName}.w");
                }
                else if (nodeCode.Contains("$precision "))
                {
                    string variableName = nodeCode.Substring(nodeCode.IndexOf(" ") + 1);
                    generatedShader = generatedShader.Replace($"{variableName}, 1.0", $"{variableName}, {variableName}");
                }
            }

            Material previewMaterial = null;
            Shader previewShader = ShaderUtil.CreateShaderAsset(generatedShader);
            if (previewShader != null && ShaderUtil.ShaderHasError(previewShader) == false)
            {
                previewMaterial = new Material(previewShader);
                PreviewManager.AssignPerMaterialPreviewProperties(previewMaterial, perMaterialPreviewProperties);
            }
            else
            {
                Debug.LogError("Failed to create shader.");
            }

            ShaderUtil.allowAsyncCompilation = originalAsyncCompilationValue;

            return previewMaterial;
        }

        static void UpdateImportSettings(string assetPath, TextureType textureType)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (textureImporter != null)
            {
                if (textureType == TextureType.Normal)
                {
                    textureImporter.textureType = TextureImporterType.NormalMap;
                    textureImporter.convertToNormalmap = false;
                }
                else if (textureType == TextureType.Raw)
                {
                    textureImporter.textureType = TextureImporterType.SingleChannel;
                }
                else
                {
                    textureImporter.textureType = TextureImporterType.Default;
                }
                textureImporter.textureShape = TextureImporterShape.Texture2D;
                textureImporter.SaveAndReimport();
            }
        }
    }
}
