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
    class StudioListenerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            StudioListener listener = target as StudioListener;
            FMODStudioListenerMode newMode = (FMODStudioListenerMode)EditorGUILayout.EnumPopup("mode", listener.mode);
            if (listener.mode != newMode)
            {
                listener.mode = newMode;
                EditorUtility.SetDirty(listener);
            }
        }
    }
}