using UnityEditor;

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