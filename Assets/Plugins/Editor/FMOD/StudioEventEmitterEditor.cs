using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FMODUnity
{
    [CustomEditor(typeof(StudioEventEmitter))]
    [CanEditMultipleObjects]
    class StudioEventEmitterEditor : Editor
    {
        bool showAdvanced;
        bool showParameters;

        public override void OnInspectorGUI()
        {
            var begin = serializedObject.FindProperty("PlayEvent");
            var end = serializedObject.FindProperty("StopEvent");
            var tag = serializedObject.FindProperty("CollisionTag");
            var ev = serializedObject.FindProperty("Event");
            var param = serializedObject.FindProperty("Params");

            EditorGUILayout.PropertyField(begin, new GUIContent("Play Event"));
            EditorGUILayout.PropertyField(end, new GUIContent("Stop Event"));

            if (begin.enumValueIndex == 3 || begin.enumValueIndex == 4 ||
                end.enumValueIndex == 3 || end.enumValueIndex == 4)
            {
                tag.stringValue = EditorGUILayout.TagField("Collision Tag", tag.stringValue);
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(ev, new GUIContent("Event"));
                        
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtils.UpdateParamsOnEmmitter(serializedObject);
            }

            showParameters = EditorGUILayout.Foldout(showParameters, "Parameters");
            if (showParameters && param.arraySize > 0)
            {
                var eventRef = EventManager.EventFromPath(ev.stringValue);
                for (int i = 0; i < param.arraySize; i++)
                {
                    var parami = param.GetArrayElementAtIndex(i);
                    var nameProperty = parami.FindPropertyRelative("Name");
                    var valueProperty = parami.FindPropertyRelative("Value");

                    var paramRef = eventRef.Parameters.Find(x => x.Name == nameProperty.stringValue);
                    if (paramRef == null)
                    {
                        param.DeleteArrayElementAtIndex(i);
                        i--;
                        continue;
                    }
                    
                    EditorGUILayout.Slider(valueProperty, paramRef.Min, paramRef.Max, nameProperty.stringValue);
                }
            }            

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Controls");
            if (showAdvanced)
            {
                var fadout = serializedObject.FindProperty("AllowFadeout");
                EditorGUILayout.PropertyField(fadout, new GUIContent("Allow Fadeout When Stopping"));
                var once = serializedObject.FindProperty("TriggerOnce");
                EditorGUILayout.PropertyField(once, new GUIContent("Trigger Once"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
