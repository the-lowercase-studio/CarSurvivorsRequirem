using UnityEngine;

namespace Assets.Scripts.Utils
{
    public static class RandomUtility
    {
        public static Vector3 GetRandomPointInSphere(Vector3 center, float radius)
        {
            return OffsetPositionForSphere(center, radius, Random.insideUnitSphere);
        }

        public static Vector3 GetRandomPointOnSphereSurface(Vector3 center, float radius)
        {
            return OffsetPositionForSphere(center, radius, Random.onUnitSphere);
        }

        private static Vector3 OffsetPositionForSphere(Vector3 center, float radius, Vector3 randomSpherePos)
        {
            return center + (randomSpherePos * radius);
        }

        public static Vector3 GetRandomPointOnHemisphereSurface(Vector3 center, float radius)
        {
            return OffsetPositionForHemisphere(center, radius, Random.onUnitSphere);
        }

        private static Vector3 OffsetPositionForHemisphere(Vector3 center, float radius, Vector3 randomSpherePos)
        {
            if (randomSpherePos.y < 0)
            {
                randomSpherePos.y = -randomSpherePos.y;
            }

            return center + (randomSpherePos * radius);
        }
    }
}
