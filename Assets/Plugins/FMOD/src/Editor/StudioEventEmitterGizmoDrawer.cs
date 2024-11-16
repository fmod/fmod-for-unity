using UnityEngine;
using UnityEditor;

namespace FMODUnity
{
    public class StudioAudioEventEmitterGizmosDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        private static void DrawGizmo(StudioAudioEventEmitter studioEmitter, GizmoType gizmoType)
        {
            Gizmos.DrawIcon(studioEmitter.transform.position, "AudioSource Gizmo", true, Color.yellow);
        }
    }
}
