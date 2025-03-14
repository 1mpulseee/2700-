/*
   Copyright (c) 2023 Léo Chaumartin
   All rights reserved.
*/

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements; 
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing.Controls;


namespace Seamless.SGExtension
{
    internal class SeamlessExportButtonControlAttribute : Attribute, IControlAttribute
    {
        public SeamlessExportButtonControlAttribute()
        {

        }

        VisualElement IControlAttribute.InstantiateControl(AbstractMaterialNode node, PropertyInfo propertyInfo)
        {
            if (!(node is ExportNode))
                throw new ArgumentException("Property must be a Seamless Export Node.", "node");

            return new SeamlessExportButtonControlView((ExportNode)node);
        }
    }

    internal class SeamlessExportButtonControlView : VisualElement
    {
        ExportNode m_Node;
        Button m_Button;

        public SeamlessExportButtonControlView(ExportNode node)
        {
            m_Node = node;
            m_Button = new Button(Callback);
            m_Node.m_button = m_Button;
            m_Node.UpdateAttributes();
            Image icon = new Image();
            Label label = new Label("Seamless Export Node v1.3");
            label.style.flexGrow = new StyleFloat(1);
            label.style.fontSize = 6;
            icon.image = Resources.Load<Texture2D>("SeamlessSGELogo");
            icon.style.width = new StyleLength(32);
            icon.style.height = new StyleLength(32);
            label.style.alignItems = Align.FlexEnd;
            m_Button.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_Button.style.height = new StyleLength(40);
            
            label.style.backgroundImage = new StyleBackground(icon.image as Texture2D);
            label.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            label.style.height = new StyleLength(44);
            label.style.unityTextAlign = TextAnchor.LowerCenter;
            
            style.flexDirection = FlexDirection.Row;
            Add(label);
            Add(m_Button);
        }


        void Callback()
        {            
            m_Node.BakeButtonCallback();
        }
    }
}