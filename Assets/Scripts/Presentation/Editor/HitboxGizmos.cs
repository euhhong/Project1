using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>씬 뷰(에디터, Play 아님)에서도 Hurtbox_*·Hitbox_* BoxCollider를 부위 색으로 그린다.</summary>
    public static class HitboxGizmos
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void Draw(BoxCollider box, GizmoType type)
        {
            if (!(box.name.StartsWith(BodyPartColliderPostprocessor.HurtPrefix) || box.name.StartsWith(BodyPartColliderPostprocessor.HitPrefix))) return;
            Gizmos.color = HitboxDebugOverlay.ColorOf(box);
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
