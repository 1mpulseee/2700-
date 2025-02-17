#if UNITY_EDITOR

// Disable 'obsolete' warnings
#pragma warning disable 0618
#pragma warning disable 0612

using System;
using UnityEngine;

namespace UnityEditor
{
    public class DecalerySimpleShaderGUI : ShaderGUI
    {
        public enum BlendMode
        {
            Opaque,
            Alpha,
            PremultipliedAlpha,
            Multiply,
            Multiply2x,
            Additive,
            Screen,
            Min,
            Max
        }

        public static readonly string[] blendNames = Enum.GetNames(typeof(BlendMode));

        MaterialProperty pColor, pMainTex, pBlendSrc, pBlendDst, pBlendOp, pFogBlend, pBias;

        MaterialEditor m_MaterialEditor;

        public void FindProperties(MaterialProperty[] props)
        {
            pColor = FindProperty("_Color", props);
            pMainTex = FindProperty("_MainTex", props);
            pBlendSrc = FindProperty("_BlendSrc", props);
            pBlendDst = FindProperty("_BlendDst", props);
            pBlendOp = FindProperty("_BlendOp", props);
            pFogBlend = FindProperty("_FogBlend", props);
            pBias = FindProperty("_Bias", props);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            FindProperties(props);
            m_MaterialEditor = materialEditor;
            Material material = materialEditor.target as Material;

            ShaderPropertiesGUI(material);
        }

        public void ShaderPropertiesGUI(Material material)
        {
            m_MaterialEditor.ShaderProperty(pBias, new GUIContent("Depth offset"));
            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();
            {
                m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Color (RGB), Opacity (A)"), pMainTex, pColor);
                EditorGUILayout.Space();
                BlendModePopup();

            }
            if (EditorGUI.EndChangeCheck())
            {
            }

            int prevQueue = material.renderQueue;
            int newQueue = EditorGUILayout.IntField("Render Queue", prevQueue);//, 1000, 5000);
            if (prevQueue != newQueue)
            {
                material.renderQueue = newQueue;
            }
        }

        void BlendModePopup()
        {
            EditorGUI.showMixedValue = pBlendSrc.hasMixedValue || pBlendDst.hasMixedValue || pBlendOp.hasMixedValue;
            
            BlendMode mode;
            if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.SrcAlpha && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Add)
            {
                mode = BlendMode.Alpha;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Add)
            {
                mode = BlendMode.PremultipliedAlpha;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.DstColor && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.Zero && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Add)
            {
                mode = BlendMode.Multiply;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.DstColor && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.SrcColor && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Add)
            {
                mode = BlendMode.Multiply2x;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Add)
            {
                mode = BlendMode.Additive;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.OneMinusDstColor && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Add)
            {
                mode = BlendMode.Screen;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Min)
            {
                mode = BlendMode.Min;
            }
            else if (pBlendSrc.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendDst.floatValue == (int)UnityEngine.Rendering.BlendMode.One && pBlendOp.floatValue == (int)UnityEngine.Rendering.BlendOp.Max)
            {
                mode = BlendMode.Max;
            }
            else
            {
                mode = BlendMode.Opaque;
            }

            EditorGUI.BeginChangeCheck();
            mode = (BlendMode)EditorGUILayout.Popup("Blend mode", (int)mode, blendNames);
            if (EditorGUI.EndChangeCheck())
            {
                if (mode == BlendMode.Alpha)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.SrcAlpha;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = -1.0f;
                }
                else if (mode == BlendMode.PremultipliedAlpha)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = -1.0f;
                }
                else if (mode == BlendMode.Multiply)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.DstColor;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.Zero;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = 1.0f;
                }
                else if (mode == BlendMode.Multiply2x)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.DstColor;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.SrcColor;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = 0.5f;
                }
                else if (mode == BlendMode.Additive)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = 0.0f;
                }
                else if (mode == BlendMode.Screen)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.OneMinusDstColor;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = 0.0f;
                }
                else if (mode == BlendMode.Min)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Min;
                    pFogBlend.floatValue = -1.0f;
                }
                else if (mode == BlendMode.Max)
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Max;
                    pFogBlend.floatValue = -1.0f;
                }
                else
                {
                    pBlendSrc.floatValue = (int)UnityEngine.Rendering.BlendMode.One;
                    pBlendDst.floatValue = (int)UnityEngine.Rendering.BlendMode.Zero;
                    pBlendOp.floatValue = (int)UnityEngine.Rendering.BlendOp.Add;
                    pFogBlend.floatValue = -1.0f;
                }
            }

            EditorGUI.showMixedValue = false;
        }
    }
}

#endif
