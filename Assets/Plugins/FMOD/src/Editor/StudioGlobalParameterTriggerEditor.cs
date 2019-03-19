using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FMODUnity
{
    [CustomEditor(typeof(StudioGlobalParameterTrigger))]
    public class StudioGlobalParameterTriggerEditor : Editor
    {
        SerializedProperty param;
        SerializedProperty trigger;
        SerializedProperty tag;
        SerializedProperty value;

        SerializedProperty data1, data2;

        [SerializeField]
        EditorParamRef editorParamRef;

        void OnEnable()
        {
            param = serializedObject.FindProperty("parameter");
            trigger = serializedObject.FindProperty("TriggerEvent");
            tag = serializedObject.FindProperty("CollisionTag");
            value = serializedObject.FindProperty("value");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(trigger, new GUIContent("Trigger"));
            if (trigger.enumValueIndex >= (int)EmitterGameEvent.TriggerEnter && trigger.enumValueIndex <= (int)EmitterGameEvent.TriggerExit2D)
            {
                tag.stringValue = EditorGUILayout.TagField("Collision Tag", tag.stringValue);
            }

            EditorGUI.BeginChangeCheck();

            var oldParam = param.stringValue;
            EditorGUILayout.PropertyField(param, new GUIContent("Parameter"));

            if (!String.IsNullOrEmpty(param.stringValue))
            {
                if (!editorParamRef || param.stringValue != oldParam)
                {
                    editorParamRef = EventManager.ParamFromPath(param.stringValue);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Override Value");
                value.floatValue = EditorGUILayout.Slider(value.floatValue, editorParamRef.Min, editorParamRef.Max);
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}