using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public static class CellCameraVisibilityChecker
    {
        public static bool IsCellVisibleFromCamera(Vector3 cellPosition, Camera camera)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(cellPosition);
            return viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
                   viewportPoint.y >= 0 && viewportPoint.y <= 1 &&
                   viewportPoint.z > 0;
        }
    }
}
