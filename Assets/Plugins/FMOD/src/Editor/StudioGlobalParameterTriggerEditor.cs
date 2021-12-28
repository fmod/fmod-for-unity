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

        static GUIContent NotFoundWarning;

        string currentPath;

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
            if (NotFoundWarning == null)
            {
                Texture warningIcon = EditorUtils.LoadImage("NotFound.png");
                NotFoundWarning = new GUIContent("Parameter Not Found", warningIcon);
            }

            EditorGUILayout.PropertyField(trigger, new GUIContent("Trigger"));
            if (trigger.enumValueIndex >= (int)EmitterGameEvent.TriggerEnter && trigger.enumValueIndex <= (int)EmitterGameEvent.TriggerExit2D)
            {
                tag.stringValue = EditorGUILayout.TagField("Collision Tag", tag.stringValue);
            }

            EditorGUILayout.PropertyField(param, new GUIContent("Parameter"));

            if (param.stringValue != currentPath)
            {
                currentPath = param.stringValue;

                if (string.IsNullOrEmpty(param.stringValue))
                {
                    editorParamRef = null;
                }
                else
                {
                    editorParamRef = EventManager.ParamFromPath(param.stringValue);
                    value.floatValue = Mathf.Clamp(value.floatValue, editorParamRef.Min, editorParamRef.Max);
                }
            }

            if (editorParamRef != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Override Value");
                    value.floatValue = EditorUtils.DrawParameterValueLayout(value.floatValue, editorParamRef);
                }
            }
            else
            {
                Rect rect = EditorGUILayout.GetControlRect();
                rect.xMin += EditorGUIUtility.labelWidth;

                GUI.Label(rect, NotFoundWarning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}