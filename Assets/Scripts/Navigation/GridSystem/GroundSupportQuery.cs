using Assets.Scripts.LayerMasks;
using Assets.Scripts.Navigation.Constants;
using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class GroundSupportQuery
    {
        public static bool TryGetSupportedPosition(Vector3 candidate, out Vector3 position)
        {
            return TryGetSupportedPosition(candidate, out position, out _);
        }

        public static bool TryGetSupportedPosition(Vector3 candidate, out Vector3 position, out Collider supportCollider)
        {
            position = candidate;
            supportCollider = null;
            Vector3 origin = candidate + Vector3.up * GridConstants.GROUND_PROBE_ORIGIN_HEIGHT;
            // Reject embedded origins instead of letting a ray skip the containing surface.
            if (Physics.CheckSphere(origin, GridConstants.ROOT_GROUND_CLEARANCE,
                TerrainLayers.All, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                GridConstants.GROUND_PROBE_DISTANCE, TerrainLayers.Walkable, QueryTriggerInteraction.Ignore)
                || hit.normal.y <= GridConstants.MIN_SUPPORT_NORMAL_Y
                || float.IsNaN(hit.point.y) || float.IsInfinity(hit.point.y))
            {
                return false;
            }

            position.y = hit.point.y + GridConstants.ROOT_GROUND_CLEARANCE;
            supportCollider = hit.collider;
            return true;
        }

        public static bool IsWithinWorldBounds(Grid grid, Vector3 position)
        {
            return grid != null && position.x >= 0f && position.z >= 0f
                && position.x < grid.Width * grid.CellSize && position.z < grid.Height * grid.CellSize;
        }
    }
}
