#if UNITY_2017_1_OR_NEWER

using UnityEditor;
using UnityEngine;
using FMODUnity;
using System;
using System.Linq;

[CustomEditor(typeof(FMODEventPlayable))]
public class FMODEventPlayableEditor : Editor
{
    private FMODEventPlayable eventPlayable;
    private EditorEventRef editorEventRef;

    public void OnEnable()
    {
        eventPlayable = target as FMODEventPlayable;
        if (eventPlayable && !string.IsNullOrEmpty(eventPlayable.eventName))
        {
            editorEventRef = EventManager.EventFromPath(eventPlayable.eventName);
            eventPlayable.UpdateEventDuration(editorEventRef.IsOneShot ? editorEventRef.Length : float.PositiveInfinity);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var ev = serializedObject.FindProperty("eventName");
        var stopType = serializedObject.FindProperty("stopType");

        EditorGUILayout.PropertyField(ev, new GUIContent("Event"));

        var eventRef = EventManager.EventFromPath(ev.stringValue);

        if (eventRef != null && eventRef.Parameters.Count > 0)
        {
            foreach (var paramRef in eventRef.Parameters)
            {
                bool set;
                float value;
                CheckParameter(paramRef.Name, out set, out value);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(paramRef.Name);
                EditorGUI.BeginChangeCheck();
                bool newSet = EditorGUILayout.Toggle(set, GUILayout.Width(20));

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { serializedObject.targetObject }, "Inspector");
                    if (newSet)
                    {
                        AddParameterValue(serializedObject.targetObject, paramRef.Name, paramRef.Default);
                    }
                    else
                    {
                        DeleteParameterValue(serializedObject.targetObject, paramRef.Name);
                    }
                    set = newSet;
                }

                if (set)
                {
                    EditorGUI.BeginChangeCheck();
                    value = EditorGUILayout.Slider(value, paramRef.Min, paramRef.Max);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObjects(new UnityEngine.Object[] { serializedObject.targetObject }, "Inspector");
                        SetParameterValue(serializedObject.targetObject, paramRef.Name, value);
                    }
                }
                else
                {
                    EditorGUILayout.Slider(paramRef.Default, paramRef.Min, paramRef.Max);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.PropertyField(stopType, new GUIContent("Stop Mode"));

        if (eventPlayable)
        {
            eventPlayable.OnValidate();
        }
        serializedObject.ApplyModifiedProperties();
    }

    void CheckParameter(string name, out bool set, out float value)
    {
        value = 0;
        set = false;

        var playable = serializedObject.targetObject as FMODEventPlayable;
        var param = playable.parameters != null ? playable.parameters.FirstOrDefault((x) => x.Name == name) : null;
        if (param != null)
        {
            set = true;
            value = param.Value;
        }
    }

    void SetParameterValue(UnityEngine.Object obj, string name, float value)
    {
        var playable = obj as FMODEventPlayable;
        var param = playable.parameters != null ? playable.parameters.FirstOrDefault((x) => x.Name == name) : null;
        if (param != null)
        {
            param.Value = value;
        }
    }

    void AddParameterValue(UnityEngine.Object obj, string name, float value)
    {
        var playable = obj as FMODEventPlayable;
        var param = playable.parameters != null ? playable.parameters.FirstOrDefault((x) => x.Name == name) : null;
        if (param == null)
        {
            int end = playable.parameters.Length;
            Array.Resize<ParamRef>(ref playable.parameters, end + 1);
            playable.parameters[end] = new ParamRef();
            playable.parameters[end].Name = name;
            playable.parameters[end].Value = value;
        }
    }

    void DeleteParameterValue(UnityEngine.Object obj, string name)
    {
        var emitter = obj as FMODEventPlayable;
        int found = -1;
        for (int i = 0; i < emitter.parameters.Length; i++)
        {
            if (emitter.parameters[i].Name == name)
            {
                found = i;
            }
        }
        if (found >= 0)
        {
            int end = emitter.parameters.Length - 1;
            emitter.parameters[found] = emitter.parameters[end];
            Array.Resize<ParamRef>(ref emitter.parameters, end);
        }
    }
}

#endif //UNITY_2017_1_OR_NEWER