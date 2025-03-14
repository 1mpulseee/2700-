/*
   Copyright (c) 2023 Léo Chaumartin
   All rights reserved.
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditor;

namespace Seamless.SGExtension
{
        public enum TextureType { Default, Normal, Raw }

    [Title("Seamless", "Export")]
    class ExportNode : AbstractMaterialNode, IGeneratesBodyCode, IGeneratesFunction
    {
        public override string documentationURL => "https://leochaumartin.com/wiki/index.php/Seamless_Shadergraph_Extension";
         
        public override bool hasPreview { get { return true; } }
        

        const int InputSlotId = 0;       
        const int OutputSlotId = 1;
        const string kInputSlotName = "Input";  
        const string kOutputSlotName = "Out";


        [SerializeField] Vector2 m_Resolution = Vector2.one * 512;

        public string lastSavedFilePath = string.Empty;

        [MultiFloatControl("Resolution ", "W", "H")]
        public Vector2 Resolution
        {
            get { return m_Resolution; }
            set { m_Resolution = new Vector2(Mathf.Round(value.x), Mathf.Round(value.y)); }
        }

        [SerializeField]
        private TextureType m_TextureType = TextureType.Default;
        [EnumControl("Type")]
        public TextureType TextureType
        {
            get { return m_TextureType; }
            set
            {
                if (m_TextureType == value)
                    return;

                m_TextureType = value;
                UpdateAttributes();
                Dirty(ModificationScope.Topological);
            }
        }

        [SerializeField]
        private ToggleData m_Alpha = new ToggleData(true);

        [ToggleControl()]
        public ToggleData Alpha
        {
            get { return m_Alpha; }
            set
            {
                if (m_Alpha.isOn == value.isOn)
                    return;

                m_Alpha = value;
                UpdateAttributes();
                Dirty(ModificationScope.Node);
            }
        }


        [BackgroundImageControl("SeamlessExportNodeBackground")]
        string label { get; set; }
        public UnityEngine.UIElements.Label m_Label;

        [SerializeField]
        private SerializableTexture m_OutputTexture = new SerializableTexture();

        [TextureControl("")]
        public Texture OutputTexture
        {
            get
            { 
                return m_OutputTexture.texture;
            }

            set
            {

                Dirty(ModificationScope.Node);
                m_OutputTexture.texture = value;
                UpdateAttributes();   
            }
        }

        [SeamlessExportButtonControl()]
        int buttonControl { get; set; }
        public UnityEngine.UIElements.Button m_button;

        
        public ExportNode()
        {
            name = "Export";
            m_PreviewMode = PreviewMode.Preview2D;
            synonyms = new string[] { "bake" };
            UpdateNodeAfterDeserialization();


        }

        public Vector2Int GetResolution()
        {
            return new Vector2Int(Mathf.RoundToInt(m_Resolution.x), Mathf.RoundToInt(m_Resolution.y));
        }

        public override void ValidateNode()
        {
            base.ValidateNode();

            UpdateAttributes();
        }
        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new DynamicVectorMaterialSlot(InputSlotId, kInputSlotName, kInputSlotName, SlotType.Input, Vector3.zero));
            AddSlot(new DynamicVectorMaterialSlot(OutputSlotId, kOutputSlotName, kOutputSlotName, SlotType.Output, Vector3.zero, ShaderStageCapability.All, true));

            List<int> slotIDs = new List<int> { InputSlotId, OutputSlotId };

            RemoveSlotsNameNotMatching(slotIDs.ToArray(), true);

        }
        string GetFunctionName()
        {
            return $"SeamlessExport_{FindSlot<MaterialSlot>(InputSlotId).concreteValueType.ToShaderString()}";
        }
        public void GenerateNodeCode(ShaderStringBuilder sb, GenerationMode generationMode)
        {
            var inputValue = GetSlotValue(InputSlotId, generationMode);
            var outputValue = GetVariableNameForSlot(OutputSlotId);
            sb.AppendLine("{0} {1};", FindOutputSlot<MaterialSlot>(OutputSlotId).concreteValueType.ToShaderString(), outputValue);
            sb.AppendLine("{0}({1}, {2});", GetFunctionName(), inputValue, outputValue);
        }
        public void GenerateNodeFunction(FunctionRegistry registry, GenerationMode generationMode)
        {
            registry.ProvideFunction(GetFunctionName(), s =>
            {
                s.AppendLine("void {0}({1} In, out {2} Out)",
                    GetFunctionName(),
                    FindInputSlot<MaterialSlot>(InputSlotId).concreteValueType.ToShaderString(),
                    FindOutputSlot<MaterialSlot>(OutputSlotId).concreteValueType.ToShaderString());
                using (s.BlockScope())
                {
                    s.AppendLine(GetNodeFunctionBody());
                }
            });
        }
        

        string GetNodeFunctionBody()
        {
            if (TextureType == TextureType.Normal)
            {
                switch (FindInputSlot<MaterialSlot>(InputSlotId).concreteValueType)
                {
                    case ConcreteSlotValueType.Vector3:
                    case ConcreteSlotValueType.Vector4:                    
                        {
                            return "{Out = float4(In.r * 0.5 + 0.5, In.g * 0.5 + 0.5, 1, 1);}";
                        }
                }
            }
            if (TextureType == TextureType.Raw)
                return "{Out = In.r;}";
            return "{Out = In;}";
        }
         
        public void UpdateAttributes()
        {
            if (m_button != null)
            {
                bool buttonEnabled = true;

                if ((TextureType == TextureType.Default || TextureType == TextureType.Raw) ||
                    FindInputSlot<MaterialSlot>(InputSlotId).concreteValueType == ConcreteSlotValueType.Vector3 ||
                    FindInputSlot<MaterialSlot>(InputSlotId).concreteValueType == ConcreteSlotValueType.Vector4)
                {
                    m_button.text = m_OutputTexture.texture == null ? "Export" : "Update";
                }
                else // A Normal map needs at least 3 channels
                {
                    buttonEnabled = false;
                    m_button.text = "Invalid input";
                }

                m_button.SetEnabled(buttonEnabled);
            }

            if(TextureType == TextureType.Default)
            {
                m_Alpha.isEnabled = true;
            }
            else
            {
                m_Alpha.isEnabled = false;
            }

            if(TextureType == TextureType.Raw)
            {
                m_OutputTexture.texture = null;
            }
        }



        public void BakeButtonCallback()
        {
            if (TextureType == TextureType.Normal && 
                (FindInputSlot<MaterialSlot>(InputSlotId).concreteValueType != ConcreteSlotValueType.Vector3 &&
                    FindInputSlot<MaterialSlot>(InputSlotId).concreteValueType != ConcreteSlotValueType.Vector4))
            {
                Debug.LogError("Invalid Input");
                return;
            }

            {
                TextureBaker.Bake(this);
            }
        }
    }
}
