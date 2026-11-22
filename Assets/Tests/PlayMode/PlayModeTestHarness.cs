using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Tests.PlayMode
{
    public sealed class PlayModeTestHarness
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Container> _createdContainers = new();
        private readonly List<Scene> _createdScenes = new();

        public Scene ActiveTestScene { get; private set; }

        public Scene BeginIsolatedScene(string scenePrefix = "PlayModeTestScene")
        {
            DOTween.KillAll();
            DOTween.Clear(true);
            Time.timeScale = 1f;

            string sceneName = scenePrefix + "_" + Guid.NewGuid().ToString("N");
            Scene scene = SceneManager.CreateScene(sceneName);
            _createdScenes.Add(scene);
            SceneManager.SetActiveScene(scene);
            ActiveTestScene = scene;
            return scene;
        }

        public GameObject TrackGameObject(string name, bool active = true)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.SetActive(active);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        public T TrackScriptableObject<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        public T TrackObject<T>(T instance) where T : UnityEngine.Object
        {
            if (instance != null)
            {
                _createdObjects.Add(instance);
            }

            return instance;
        }

        public GameObject InstantiateTrackedPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Container container = null, Transform parent = null)
        {
            GameObject stagingParent = new GameObject("PlayModePrefabStaging");
            stagingParent.SetActive(false);

            try
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, stagingParent.transform);
                _createdObjects.Add(instance);

                if (container != null)
                {
                    GameObjectInjector.InjectRecursive(instance, container);
                }

                instance.transform.SetParent(parent, true);
                instance.SetActive(true);
                return instance;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stagingParent);
            }
        }

        public Container BuildContainer(Action<ContainerBuilder> configure)
        {
            ContainerBuilder builder = new ContainerBuilder().SetName("PlayModeTestContainer");
            if (configure != null)
            {
                configure(builder);
            }

            Container container = builder.Build();
            _createdContainers.Add(container);
            return container;
        }

        public IEnumerator CleanupAsync()
        {
            DOTween.KillAll();
            DOTween.Clear(true);
            Time.timeScale = 1f;

            for (int i = 0; i < _createdContainers.Count; i++)
            {
                Container container = _createdContainers[i];
                if (container != null)
                {
                    container.Dispose();
                }
            }

            _createdContainers.Clear();

            for (int i = 0; i < _createdObjects.Count; i++)
            {
                UnityEngine.Object obj = _createdObjects[i];
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _createdObjects.Clear();

            Scene? fallbackScene = FindRemainingLoadedScene();
            for (int i = 0; i < _createdScenes.Count; i++)
            {
                Scene scene = _createdScenes[i];
                if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount <= 1)
                {
                    continue;
                }

                if (SceneManager.GetActiveScene() == scene && fallbackScene.HasValue)
                {
                    SceneManager.SetActiveScene(fallbackScene.Value);
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        yield return null;
                    }
                }
            }

            _createdScenes.Clear();
            ActiveTestScene = default;
            yield return null;
        }

        public static GameObject CreateBoxColliderBody(
            PlayModeTestHarness harness,
            string name,
            string layerName,
            Vector3 position,
            Vector3 size,
            bool isTrigger,
            bool addKinematicRigidbody)
        {
            GameObject go = harness.TrackGameObject(name, false);
            int layer = LayerMask.NameToLayer(layerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), $"Layer '{layerName}' must exist in TagManager.");
            go.layer = layer;
            go.transform.position = position;

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = isTrigger;

            if (addKinematicRigidbody)
            {
                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            go.SetActive(true);
            return go;
        }

        public static T LoadEditorAsset<T>(string assetPath) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.IsNotNull(asset, $"Required asset was not found at '{assetPath}'.");
            return asset;
#else
            Assert.Inconclusive($"Editor asset loading is unavailable outside UNITY_EDITOR for '{assetPath}'.");
            return null;
#endif
        }

        public static IEnumerator WaitForFixedUpdates(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        public static IEnumerator WaitUntilWithTimeout(Func<bool> predicate, float timeoutSeconds, string failureMessage)
        {
            float elapsed = 0f;
            while (!predicate())
            {
                if (elapsed >= timeoutSeconds)
                {
                    Assert.Fail(failureMessage + $" (timed out after {timeoutSeconds:F2}s)");
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public static T FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static void FindAllInScene<T>(Scene scene, List<T> buffer) where T : Component
        {
            buffer.Clear();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T[] found = roots[i].GetComponentsInChildren<T>(true);
                for (int j = 0; j < found.Length; j++)
                {
                    buffer.Add(found[j]);
                }
            }
        }

        public static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on '{ownerType.Name}'.");
            field.SetValue(target, value);
        }

        public static T GetPrivateField<T>(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on '{ownerType.Name}'.");
            return (T)field.GetValue(target);
        }

        private Scene? FindRemainingLoadedScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.IsValid() && candidate.isLoaded && !_createdScenes.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
