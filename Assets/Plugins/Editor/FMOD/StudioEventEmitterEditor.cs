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
    public class StudioEventEmitterEditor : Editor
    {
        public void OnSceneGUI()
        {
            var emitter = target as StudioEventEmitter;

            EditorEventRef editorEvent = EventManager.EventFromPath(emitter.Event);
            if (editorEvent != null && editorEvent.Is3D)
            {
                EditorGUI.BeginChangeCheck();
                float minDistance = emitter.OverrideAttenuation ? emitter.OverrideMinDistance : editorEvent.MinDistance;
                float maxDistance = emitter.OverrideAttenuation ? emitter.OverrideMaxDistance : editorEvent.MaxDistance;
                minDistance = Handles.RadiusHandle(Quaternion.identity, emitter.transform.position, minDistance);
                maxDistance = Handles.RadiusHandle(Quaternion.identity, emitter.transform.position, maxDistance);
                if (EditorGUI.EndChangeCheck() && emitter.OverrideAttenuation)
                {
                    Undo.RecordObject(emitter, "Change Emitter Bounds");
                    emitter.OverrideMinDistance = Mathf.Clamp(minDistance, 0, emitter.OverrideMaxDistance);
                    emitter.OverrideMaxDistance = Mathf.Max(emitter.OverrideMinDistance, maxDistance);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var begin = serializedObject.FindProperty("PlayEvent");
            var end = serializedObject.FindProperty("StopEvent");
            var tag = serializedObject.FindProperty("CollisionTag");
            var ev = serializedObject.FindProperty("Event");
            var param = serializedObject.FindProperty("Params");
            var fadeout = serializedObject.FindProperty("AllowFadeout");
            var once = serializedObject.FindProperty("TriggerOnce");
            var preload = serializedObject.FindProperty("Preload");
            var overrideAtt = serializedObject.FindProperty("OverrideAttenuation");
            var minDistance = serializedObject.FindProperty("OverrideMinDistance");
            var maxDistance = serializedObject.FindProperty("OverrideMaxDistance");

            EditorGUILayout.PropertyField(begin, new GUIContent("Play Event"));
            EditorGUILayout.PropertyField(end, new GUIContent("Stop Event"));

            if ((begin.enumValueIndex >= 3 && begin.enumValueIndex <= 6) ||
            (end.enumValueIndex >= 3 && end.enumValueIndex <= 6))
            {
                tag.stringValue = EditorGUILayout.TagField("Collision Tag", tag.stringValue);
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(ev, new GUIContent("Event"));

            EditorEventRef editorEvent = EventManager.EventFromPath(ev.stringValue);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtils.UpdateParamsOnEmitter(serializedObject, ev.stringValue);
                if (editorEvent != null)
                {
                    overrideAtt.boolValue = false;
                    minDistance.floatValue = editorEvent.MinDistance;
                    maxDistance.floatValue = editorEvent.MaxDistance;
                }
            }

            // Attenuation
            if (editorEvent != null)
            {
                {
                    EditorGUI.BeginDisabledGroup(editorEvent == null || !editorEvent.Is3D);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Override Attenuation");
                    EditorGUI.BeginChangeCheck();
                    overrideAtt.boolValue = EditorGUILayout.Toggle(overrideAtt.boolValue, GUILayout.Width(20));
                    if (EditorGUI.EndChangeCheck() ||
                        (minDistance.floatValue == -1 && maxDistance.floatValue == -1) // never been initialiased
                        )
                    {
                        minDistance.floatValue = editorEvent.MinDistance;
                        maxDistance.floatValue = editorEvent.MaxDistance;
                    }
                    EditorGUI.BeginDisabledGroup(!overrideAtt.boolValue);
                    EditorGUIUtility.labelWidth = 30;
                    minDistance.floatValue = EditorGUILayout.FloatField("Min", minDistance.floatValue);
                    minDistance.floatValue = Mathf.Clamp(minDistance.floatValue, 0, maxDistance.floatValue);
                    maxDistance.floatValue = EditorGUILayout.FloatField("Max", maxDistance.floatValue);
                    maxDistance.floatValue = Mathf.Max(minDistance.floatValue, maxDistance.floatValue);
                    EditorGUIUtility.labelWidth = 0;
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                }

                param.isExpanded = EditorGUILayout.Foldout(param.isExpanded, "Initial Parameter Values");
                if (ev.hasMultipleDifferentValues)
                {
                    if (param.isExpanded)
                    {
                        GUILayout.Box("Cannot change parameters when different events are selected", GUILayout.ExpandWidth(true));
                    }
                }
                else
                {
                    var eventRef = EventManager.EventFromPath(ev.stringValue);
                    if (param.isExpanded && eventRef != null)
                    {
                        foreach (var paramRef in eventRef.Parameters)
                        {
                            bool set;
                            float value;
                            bool matchingSet, matchingValue;
                            CheckParameter(paramRef.Name, out set, out matchingSet, out value, out matchingValue);

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PrefixLabel(paramRef.Name);
                            EditorGUI.showMixedValue = !matchingSet;
                            EditorGUI.BeginChangeCheck();
                            bool newSet = EditorGUILayout.Toggle(set, GUILayout.Width(20));
                            EditorGUI.showMixedValue = false;

                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObjects(serializedObject.isEditingMultipleObjects ? serializedObject.targetObjects : new UnityEngine.Object[] { serializedObject.targetObject }, "Inspector");
                                if (newSet)
                                {
                                    AddParameterValue(paramRef.Name, paramRef.Default);
                                }
                                else
                                {
                                    DeleteParameterValue(paramRef.Name);
                                }
                                set = newSet;
                            }

                            EditorGUI.BeginDisabledGroup(!newSet);
                            if (set)
                            {
                                EditorGUI.showMixedValue = !matchingValue;
                                EditorGUI.BeginChangeCheck();
                                value = EditorGUILayout.Slider(value, paramRef.Min, paramRef.Max);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObjects(serializedObject.isEditingMultipleObjects ? serializedObject.targetObjects : new UnityEngine.Object[] { serializedObject.targetObject }, "Inspector");
                                    SetParameterValue(paramRef.Name, value);
                                }
                                EditorGUI.showMixedValue = false;
                            }
                            else
                            {
                                EditorGUI.showMixedValue = !matchingValue;
                                EditorGUILayout.Slider(paramRef.Default, paramRef.Min, paramRef.Max);
                                EditorGUI.showMixedValue = false;
                            }
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.EndHorizontal();
                        }

                    }
                }

                fadeout.isExpanded = EditorGUILayout.Foldout(fadeout.isExpanded, "Advanced Controls");
                if (fadeout.isExpanded)
                {
                    EditorGUILayout.PropertyField(preload, new GUIContent("Preload Sample Data"));
                    EditorGUILayout.PropertyField(fadeout, new GUIContent("Allow Fadeout When Stopping"));
                    EditorGUILayout.PropertyField(once, new GUIContent("Trigger Once"));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void CheckParameter(string name, out bool set, out bool matchingSet, out float value, out bool matchingValue)
        {
            value = 0;
            set = false;
            if (serializedObject.isEditingMultipleObjects)
            {
                bool first = true;
                matchingValue = true;
                matchingSet = true;
                foreach (var obj in serializedObject.targetObjects)
                {
                    var emitter = obj as StudioEventEmitter;
                    var param = emitter.Params != null ? emitter.Params.FirstOrDefault((x) => x.Name == name) : null;
                    if (first)
                    {
                        set = param != null;
                        value = set ? param.Value : 0;
                        first = false;
                    }
                    else
                    {
                        if (set)
                        {
                            if (param == null)
                            {
                                matchingSet = false;
                                matchingValue = false;
                                return;
                            }
                            else
                            {
                                if (param.Value != value)
                                {
                                    matchingValue = false;
                                }
                            }
                        }
                        else
                        {
                            if (param != null)
                            {
                                matchingSet = false;
                            }
                        }
                    }
                }
            }
            else
            {
                matchingSet = matchingValue = true;

                var emitter = serializedObject.targetObject as StudioEventEmitter;
                var param = emitter.Params != null ? emitter.Params.FirstOrDefault((x) => x.Name == name) : null;
                if (param != null)
                {
                    set = true;
                    value = param.Value;
                }
            }
        }

        void SetParameterValue(string name, float value)
        {            
            if (serializedObject.isEditingMultipleObjects)
            {
                foreach (var obj in serializedObject.targetObjects)
                {
                    SetParameterValue(obj, name, value);
                }
            }
            else
            {
                SetParameterValue(serializedObject.targetObject, name, value);
            }
        }

        void SetParameterValue(UnityEngine.Object obj, string name, float value)
        {
            var emitter = obj as StudioEventEmitter;
            var param = emitter.Params != null ? emitter.Params.FirstOrDefault((x) => x.Name == name) : null;
            if (param != null)
            {
                param.Value = value;
            }
        }


        void AddParameterValue(string name, float value)
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                foreach (var obj in serializedObject.targetObjects)
                {
                    AddParameterValue(obj, name, value);
                }
            }
            else
            {
                AddParameterValue(serializedObject.targetObject, name, value);
            }
        }

        void AddParameterValue(UnityEngine.Object obj, string name, float value)
        {
            var emitter = obj as StudioEventEmitter;
            var param = emitter.Params != null ? emitter.Params.FirstOrDefault((x) => x.Name == name) : null;
            if (param == null)
            {
                int end = emitter.Params.Length;
                Array.Resize<ParamRef>(ref emitter.Params, end + 1);
                emitter.Params[end] = new ParamRef();
                emitter.Params[end].Name = name;
                emitter.Params[end].Value = value;
            }
        }

        void DeleteParameterValue(string name)
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                foreach (var obj in serializedObject.targetObjects)
                {
                    DeleteParameterValue(obj, name);
                }
            }
            else
            {
                DeleteParameterValue(serializedObject.targetObject, name);
            }
        }

        void DeleteParameterValue(UnityEngine.Object obj, string name)
        {
            var emitter = obj as StudioEventEmitter;
            int found = -1;
            for (int i = 0; i < emitter.Params.Length; i++)
            {
                if (emitter.Params[i].Name == name)
                {
                    found = i;
                }
            }
            if (found >= 0)
            {
                int end = emitter.Params.Length - 1;
                emitter.Params[found] = emitter.Params[end];
                Array.Resize<ParamRef>(ref emitter.Params, end);
            }
        }
    }

}
