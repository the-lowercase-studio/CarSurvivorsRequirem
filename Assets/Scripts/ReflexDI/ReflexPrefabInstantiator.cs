using System;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.ReflexDI
{
    /// <summary>
    /// Instantiates prefabs under an inactive staging transform so Reflex dependencies
    /// are injected into root MonoBehaviours before Awake and OnEnable execute.
    /// </summary>
    public static class ReflexPrefabInstantiator
    {
        /// <summary>
        /// Creates an inactive GameObject to hold newly instantiated prefabs before activation.
        /// </summary>
        public static Transform CreateInactiveStagingParent(string ownerName)
        {
            GameObject stagingObject = new GameObject($"{ownerName}_InjectionStaging");
            stagingObject.SetActive(false);
            return stagingObject.transform;
        }

        /// <summary>
        /// Instantiates a prefab relative to parent, injects root MonoBehaviours via InjectObject,
        /// optionally runs a pre-activation callback, and reparents it so Awake and OnEnable run after injection.
        /// </summary>
        public static T InstantiateInjectedObject<T>(
            T prefab,
            Transform parent,
            Container container,
            Transform stagingParent = null,
            Action<T> beforeActivate = null) where T : Component
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            GameObject instance = InstantiateInjectedObjectCore(
                prefab.gameObject,
                Vector3.zero,
                Quaternion.identity,
                parent,
                container,
                stagingParent,
                useExplicitWorldPose: false,
                WrapBeforeActivate(beforeActivate));

            return instance.GetComponent<T>();
        }

        /// <summary>
        /// Instantiates a component prefab at a world position and rotation, injects root MonoBehaviours
        /// via InjectObject, optionally runs a pre-activation callback, and reparents it so Awake and OnEnable run after injection.
        /// </summary>
        public static T InstantiateInjectedObject<T>(
            T prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            Container container,
            Transform stagingParent = null,
            Action<T> beforeActivate = null) where T : Component
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            GameObject instance = InstantiateInjectedObjectCore(
                prefab.gameObject,
                position,
                rotation,
                parent,
                container,
                stagingParent,
                useExplicitWorldPose: true,
                WrapBeforeActivate(beforeActivate));

            return instance.GetComponent<T>();
        }

        /// <summary>
        /// Instantiates a GameObject prefab at a world position and rotation, injects root MonoBehaviours
        /// via InjectObject, optionally runs a pre-activation callback, and reparents it so Awake and OnEnable run after injection.
        /// </summary>
        public static GameObject InstantiateInjectedObject(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            Container container,
            Transform stagingParent = null,
            Action<GameObject> beforeActivate = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            return InstantiateInjectedObjectCore(
                prefab,
                position,
                rotation,
                parent,
                container,
                stagingParent,
                useExplicitWorldPose: true,
                beforeActivate);
        }

        private static Action<GameObject> WrapBeforeActivate<T>(Action<T> beforeActivate) where T : Component
        {
            if (beforeActivate == null)
            {
                return null;
            }

            return instance =>
            {
                T component = instance.GetComponent<T>();
                if (component != null)
                {
                    beforeActivate.Invoke(component);
                }
            };
        }

        private static GameObject InstantiateInjectedObjectCore(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            Container container,
            Transform stagingParent,
            bool useExplicitWorldPose,
            Action<GameObject> beforeActivate)
        {
            bool createdTempStaging = false;
            Transform effectiveStagingParent = stagingParent;
            if (effectiveStagingParent == null)
            {
                effectiveStagingParent = CreateInactiveStagingParent(prefab.name);
                createdTempStaging = true;
            }

            try
            {
                GameObject instance = useExplicitWorldPose
                    ? Object.Instantiate(prefab, position, rotation, effectiveStagingParent)
                    : Object.Instantiate(prefab, effectiveStagingParent);

                if (container != null)
                {
                    GameObjectInjector.InjectObject(instance, container);
                }

                beforeActivate?.Invoke(instance);

                instance.transform.SetParent(parent, useExplicitWorldPose);
                return instance;
            }
            finally
            {
                if (createdTempStaging && effectiveStagingParent != null)
                {
                    Object.Destroy(effectiveStagingParent.gameObject);
                }
            }
        }
    }
}
