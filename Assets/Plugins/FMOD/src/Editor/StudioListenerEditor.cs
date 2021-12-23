using UnityEditor;

namespace FMODUnity
{
    [CustomEditor(typeof(StudioListener))]
    [CanEditMultipleObjects]
    public class StudioListenerEditor : Editor
    {
        public SerializedProperty attenuationObject;

        private void OnEnable()
        {
            attenuationObject = serializedObject.FindProperty("attenuationObject");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginDisabledGroup(true);
            var index = serializedObject.FindProperty("ListenerNumber");
            EditorGUILayout.IntSlider(index, 0, FMOD.CONSTANTS.MAX_LISTENERS - 1, "Listener Index");
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(attenuationObject);
            serializedObject.ApplyModifiedProperties();
        }
    }
}