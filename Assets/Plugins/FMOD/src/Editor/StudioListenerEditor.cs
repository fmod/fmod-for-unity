using UnityEditor;

namespace FMODUnity
{
    [CustomEditor(typeof(StudioListener))]
    [CanEditMultipleObjects]
    public class StudioListenerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);
            var index = serializedObject.FindProperty("ListenerNumber");
            EditorGUILayout.IntSlider(index, 0, FMOD.CONSTANTS.MAX_LISTENERS - 1, "Listener Index");
            EditorGUI.EndDisabledGroup();

        }
    }
}