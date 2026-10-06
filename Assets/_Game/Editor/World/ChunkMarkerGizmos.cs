using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.World
{
    /// <summary>
    /// R-DBG-SPAWN (doc 11: authoring markers are editor metadata, never runtime objects): draws a chunk's egg spawn,
    /// animal spawns, points of interest, wander area and obstacle volumes in the Scene view. Editor only.
    /// </summary>
    public static class ChunkMarkerGizmos
    {
        private static readonly Color Egg = new Color(1f, 0.85f, 0.3f);
        private static readonly Color Animal = new Color(0.35f, 0.85f, 0.45f);
        private static readonly Color Interest = new Color(0.4f, 0.7f, 1f);
        private static readonly Color Eat = new Color(1f, 0.6f, 0.3f);
        private static readonly Color Wander = new Color(0.35f, 0.85f, 0.45f, 0.5f);
        private static readonly Color Obstacle = new Color(0.95f, 0.35f, 0.35f, 0.7f);

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void Draw(WorldChunk chunk, GizmoType type)
        {
            bool selected = (type & GizmoType.Selected) != 0;
            var origin = chunk.transform.position;

            if (chunk.EggSpawn != null)
            {
                Gizmos.color = Egg;
                Gizmos.DrawWireSphere(chunk.EggSpawn.position + Vector3.up * 0.25f, 0.25f);
                Handles.Label(chunk.EggSpawn.position + Vector3.up * 0.7f, "Egg");
            }
            Gizmos.color = Animal;
            foreach (var spawn in chunk.AnimalSpawns)
            {
                if (spawn == null)
                    continue;
                Gizmos.DrawWireCube(spawn.position + Vector3.up * 0.15f, new Vector3(0.3f, 0.3f, 0.3f));
                Gizmos.DrawLine(spawn.position + Vector3.up * 0.15f, spawn.position + Vector3.up * 0.15f + spawn.forward * 0.4f);
            }
            for (int i = 0; i < chunk.InterestCount; i++)
            {
                var point = chunk.GetInterest(i);
                Gizmos.color = point.kind == InterestKind.Eat ? Eat : Interest;
                Gizmos.DrawWireSphere(point.position, 0.12f);
                if (selected)
                    Handles.Label(point.position + Vector3.up * 0.3f, point.kind.ToString());
            }

            if (!selected)
                return;
            var area = chunk.WanderArea;
            Handles.color = Wander;
            Handles.DrawWireDisc(area.Center, Vector3.up, area.Radius);
            Handles.color = Obstacle;
            for (int i = 0; i < chunk.ObstacleCount; i++)
            {
                var o = chunk.GetObstacle(i);
                var a = new Vector3(o.a.x + origin.x, origin.y + o.baseY + o.bottom, o.a.y + origin.z);
                var b = new Vector3(o.b.x + origin.x, origin.y + o.baseY + o.bottom, o.b.y + origin.z);
                Handles.DrawWireDisc(a, Vector3.up, o.radius);
                Handles.DrawWireDisc(b, Vector3.up, o.radius);
                Handles.DrawLine(a, b);
                var up = Vector3.up * (o.top - o.bottom);
                Handles.DrawWireDisc(a + up, Vector3.up, o.radius);
                Handles.DrawLine(a, a + up);
            }
        }
    }
}
