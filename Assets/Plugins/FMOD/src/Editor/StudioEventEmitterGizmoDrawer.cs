using UnityEngine;
using UnityEditor;

namespace FMODUnity
{
    public class StudioEventEmitterGizoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        static void DrawGizmo(StudioEventEmitter studioEmitter, GizmoType gizmoType)
        {
            Gizmos.DrawIcon(studioEmitter.transform.position, "FMOD/FMODEmitter.tiff", true);
        }
    }
}
