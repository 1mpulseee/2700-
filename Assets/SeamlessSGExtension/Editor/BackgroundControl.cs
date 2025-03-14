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
    internal class BackgroundImageControlAttribute : Attribute, IControlAttribute
    {

        string m_IconPath;

        public BackgroundImageControlAttribute(string iconPath)
        {
            m_IconPath = iconPath;
        }

        VisualElement IControlAttribute.InstantiateControl(AbstractMaterialNode node, PropertyInfo propertyInfo)
        {
            if (!(node is ExportNode))
                throw new ArgumentException("Property must be an Export Node.", "node");

            return new BackGroundImageControlView(m_IconPath);
        }
    }

    internal class BackGroundImageControlView : VisualElement
    {
        Image m_Icon;
        
        public BackGroundImageControlView(string iconPath)
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            m_Icon = new Image();
            m_Icon.image = Resources.Load<Texture2D>(iconPath);
            
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if(parent != null)
            {
                parent.style.backgroundImage = new StyleBackground(m_Icon.image as Texture2D);
                parent.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                parent.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            }
        }
    }
}