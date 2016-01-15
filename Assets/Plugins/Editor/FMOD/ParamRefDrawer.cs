using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FMODUnity
{
    [CustomPropertyDrawer(typeof(ParamRef))]
    class ParamRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty nameProperty = property.FindPropertyRelative("Name");
            SerializedProperty valueProperty = property.FindPropertyRelative("Value");

            EditorGUI.BeginProperty(position, label, property);

            EditorGUILayout.Slider(valueProperty, 0, 1.0f, nameProperty.stringValue);
            EditorGUI.EndProperty();
        }
    }
}
