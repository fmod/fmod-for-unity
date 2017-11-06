using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FMODUnity
{
    [CustomEditor(typeof(StudioListener))]
    [CanEditMultipleObjects]
    public class StudioListenerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var index = serializedObject.FindProperty("ListenerNumber");
            EditorGUILayout.IntSlider(index, 0, FMOD.CONSTANTS.MAX_LISTENERS, "Listener Index");
            serializedObject.ApplyModifiedProperties();
        }
    }
}