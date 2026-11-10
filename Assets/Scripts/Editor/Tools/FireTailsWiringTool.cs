using System;
using System.IO;
using Assets.Scripts.Audio;
using Assets.Scripts.Player;
using Assets.Scripts.Player.Car;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Armor;
using Assets.Scripts.PowerUps.Collectibles;
using Assets.Scripts.PowerUps.Config;
using Assets.Scripts.PowerUps.Constants;
using Assets.Scripts.PowerUps.FireTails;
using Assets.Scripts.ReflexDI;
using Assets.Scripts.UI.DevConsole;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Scripts.Editor.Tools
{
    public static class FireTailsWiringTool
    {
        private const string CONFIG_PATH = "Assets/ScriptableObjects/Collectibles/PowerUps/FireTailsConfig.asset";
        private const string MATERIAL_PATH = "Assets/Materials/FireTailsTrail.mat";
        private const string NOISE_TEXTURE_PATH = "Assets/Textures/FlameNoise.png";
        private const string SHADER_NAME = "Custom/FireTailsTrail";
        private const string PREFAB_PATH = "Assets/Prefabs/Collectibles/FireTails.prefab";
        private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player/Player.prefab";
        private const string SCENE_PATH = "Assets/Scenes/RuinedBloodCity.unity";

        [MenuItem("Tools/Fire Tails/Wire All Fire Tails")]
        public static void WireAll()
        {
            EnsureConfigAsset();
            Material fireMat = EnsureFireMaterial();
            EnsureCollectiblePrefab(fireMat);
            WirePlayerPrefab(fireMat);
            WireRuinedBloodCityScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Fire Tails Power-Up fully wired and saved successfully.");
        }

        public static FireTailsConfigSO EnsureConfigAsset()
        {
            FireTailsConfigSO config = AssetDatabase.LoadAssetAtPath<FireTailsConfigSO>(CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FireTailsConfigSO>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
            }

            SerializedObject so = new SerializedObject(config);
            so.FindProperty("_durationSeconds").floatValue = 8.0f;
            so.FindProperty("_hazardLifetimeSeconds").floatValue = 3.5f;
            so.FindProperty("_trailFadeOutDuration").floatValue = 0.5f;
            so.FindProperty("_spawnIntervalDistance").floatValue = 1.2f;
            so.FindProperty("_hazardRadius").floatValue = 1.1f;
            so.FindProperty("_damagePerTick").floatValue = 25f;
            so.FindProperty("_tickIntervalSeconds").floatValue = 0.5f;
            so.FindProperty("_overlapCheckIntervalSeconds").floatValue = 0.1f;
            so.FindProperty("_slowFactor").floatValue = 0.6f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(config);
            return config;
        }

        public static Texture2D EnsureFlameNoiseTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(NOISE_TEXTURE_PATH);
            if (existing != null)
            {
                return existing;
            }

            const int size = 128;
            Texture2D noiseTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float n1 = SampleSeamlessPerlin(u, v, 3.0f, 17.13f);
                    float n2 = SampleSeamlessPerlin(u, v, 6.0f, 43.71f);
                    float noiseVal = Mathf.Clamp01(n1 * 0.7f + n2 * 0.3f);
                    pixels[y * size + x] = new Color(noiseVal, noiseVal, noiseVal, 1.0f);
                }
            }

            noiseTex.SetPixels(pixels);
            noiseTex.Apply();

            byte[] pngBytes = noiseTex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(noiseTex);

            string dir = Path.GetDirectoryName(NOISE_TEXTURE_PATH);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(NOISE_TEXTURE_PATH, pngBytes);
            AssetDatabase.ImportAsset(NOISE_TEXTURE_PATH, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(NOISE_TEXTURE_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(NOISE_TEXTURE_PATH);
        }

        private static float SampleSeamlessPerlin(float u, float v, float scale, float offset)
        {
            float x = u * scale;
            float y = v * scale;
            float w = scale;
            float h = scale;

            float s1 = Mathf.PerlinNoise(offset + x, offset + y);
            float s2 = Mathf.PerlinNoise(offset + x + w, offset + y);
            float s3 = Mathf.PerlinNoise(offset + x, offset + y + h);
            float s4 = Mathf.PerlinNoise(offset + x + w, offset + y + h);

            float top = Mathf.Lerp(s2, s1, u);
            float bottom = Mathf.Lerp(s4, s3, u);
            return Mathf.Lerp(bottom, top, v);
        }

        public static Material EnsureFireMaterial()
        {
            EnsureFlameNoiseTexture();
            Texture2D noiseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NOISE_TEXTURE_PATH);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "FireTailsTrail";
                AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.shader == shader && shader != null && shader.name == SHADER_NAME)
            {
                if (noiseTex != null)
                {
                    mat.SetTexture("_NoiseMap", noiseTex);
                }
                mat.SetColor("_CoreColor", new Color(2.5f, 2.0f, 0.8f, 1.0f));
                mat.SetColor("_OuterColor", new Color(2.0f, 0.45f, 0.05f, 1.0f));
                mat.SetVector("_FlowSpeed", new Vector4(0.0f, -3.0f, 0.0f, 0.0f));
                mat.SetVector("_NoiseTiling", new Vector4(2.0f, 4.0f, 0.0f, 0.0f));
                mat.SetFloat("_EmissionBoost", 3.0f);
                mat.SetFloat("_DissolveSoftness", 0.20f);
            }
            else
            {
                mat.color = new Color(1f, 0.45f, 0.05f, 1f);
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.05f, 1f));
                }
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(1.5f, 0.55f, 0.1f, 1f));
                    mat.EnableKeyword("_EMISSION");
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static GameObject EnsureCollectiblePrefab(Material fireMat)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existingPrefab != null)
            {
                return existingPrefab;
            }

            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Collectibles/BubbleShield.prefab");
            if (template == null)
            {
                template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Collectibles/Armor.prefab");
            }

            if (template == null)
            {
                throw new InvalidOperationException("Could not find BubbleShield or Armor collectible prefab to use as template.");
            }

            GameObject instance = UnityEngine.Object.Instantiate(template);
            instance.name = "FireTails";

            try
            {
                PowerUpCollectible collectible = instance.GetComponent<PowerUpCollectible>();
                if (collectible != null)
                {
                    SerializedObject collSo = new SerializedObject(collectible);
                    collSo.FindProperty("_type").enumValueIndex = (int)PowerUpType.FireTails;
                    collSo.ApplyModifiedPropertiesWithoutUndo();
                }

                Light pointLight = instance.GetComponentInChildren<Light>();
                if (pointLight != null)
                {
                    pointLight.color = new Color(1f, 0.45f, 0.1f);
                    pointLight.range = 3.5f;
                    pointLight.intensity = 2.5f;
                }

                MeshRenderer meshRenderer = instance.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer != null && fireMat != null)
                {
                    meshRenderer.sharedMaterial = fireMat;
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PREFAB_PATH);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        public static void WirePlayerPrefab(Material fireMat)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            try
            {
                CarVfxEffectsController vfxController = root.GetComponent<CarVfxEffectsController>();
                if (vfxController == null)
                {
                    throw new InvalidOperationException("CarVfxEffectsController not found on Player prefab.");
                }

                Transform speedTrailsParent = root.transform.Find("Visual/SpeedTrailRenderers");
                if (speedTrailsParent == null)
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "SpeedTrailRenderers")
                        {
                            speedTrailsParent = t;
                            break;
                        }
                    }
                }

                if (speedTrailsParent == null)
                {
                    throw new InvalidOperationException("SpeedTrailRenderers child not found on Player prefab.");
                }

                // 1. Ensure Fire Trail Renderers
                TrailRenderer leftFireTrail = GetOrCreateTrailRenderer(speedTrailsParent, "LeftFireDriftDownTrailRenderer", new Vector3(-0.58f, -0.568f, -0.935f), fireMat);
                TrailRenderer rightFireTrail = GetOrCreateTrailRenderer(speedTrailsParent, "RightFireDriftDownTrailRenderer", new Vector3(0.58f, -0.568f, -0.935f), fireMat);

                // 2. Ensure Constant Rear Wheel Flame Particles
                ParticleSystem leftConstantPs = GetOrCreateFlameParticleSystem(speedTrailsParent, "LeftRearWheelConstantFireParticles", new Vector3(-0.58f, -0.4f, -0.935f), fireMat, false);
                ParticleSystem rightConstantPs = GetOrCreateFlameParticleSystem(speedTrailsParent, "RightRearWheelConstantFireParticles", new Vector3(0.58f, -0.4f, -0.935f), fireMat, false);

                // 3. Ensure Lingering World-Space Drift Fire Trail Particles
                ParticleSystem leftDriftPs = GetOrCreateFlameParticleSystem(speedTrailsParent, "LeftRearDriftFireTrailParticles", new Vector3(-0.58f, -0.568f, -0.935f), fireMat, true);
                ParticleSystem rightDriftPs = GetOrCreateFlameParticleSystem(speedTrailsParent, "RightRearDriftFireTrailParticles", new Vector3(0.58f, -0.568f, -0.935f), fireMat, true);

                // 4. Wire Serialized Properties on CarVfxEffectsController
                SerializedObject vfxSo = new SerializedObject(vfxController);

                SerializedProperty fireTrailsProp = vfxSo.FindProperty("_rearFireDriftTrailRenderers");
                fireTrailsProp.arraySize = 2;
                fireTrailsProp.GetArrayElementAtIndex(0).objectReferenceValue = leftFireTrail;
                fireTrailsProp.GetArrayElementAtIndex(1).objectReferenceValue = rightFireTrail;

                SerializedProperty constantPsProp = vfxSo.FindProperty("_rearWheelConstantFireParticles");
                constantPsProp.arraySize = 2;
                constantPsProp.GetArrayElementAtIndex(0).objectReferenceValue = leftConstantPs;
                constantPsProp.GetArrayElementAtIndex(1).objectReferenceValue = rightConstantPs;

                SerializedProperty driftPsProp = vfxSo.FindProperty("_rearDriftFireTrailParticles");
                driftPsProp.arraySize = 2;
                driftPsProp.GetArrayElementAtIndex(0).objectReferenceValue = leftDriftPs;
                driftPsProp.GetArrayElementAtIndex(1).objectReferenceValue = rightDriftPs;

                vfxSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TrailRenderer GetOrCreateTrailRenderer(Transform parent, string name, Vector3 localPos, Material mat)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(name, typeof(TrailRenderer));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = existing.gameObject;
            }

            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one;

            TrailRenderer tr = go.GetComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.widthMultiplier = 1.0f;
            tr.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.60f),
                new Keyframe(1f, 0.42f)
            );
            tr.time = 3.5f;
            tr.emitting = false;
            tr.minVertexDistance = 0.15f;
            tr.autodestruct = false;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f),
                    new GradientColorKey(new Color(0.85f, 0.1f, 0f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            tr.colorGradient = gradient;

            EditorUtility.SetDirty(tr);
            EditorUtility.SetDirty(go);
            return tr;
        }

        private static ParticleSystem GetOrCreateFlameParticleSystem(Transform parent, string name, Vector3 localPos, Material mat, bool isWorldDrift)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(name, typeof(ParticleSystem));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = existing.gameObject;
            }

            go.transform.localPosition = localPos;
            go.transform.localRotation = isWorldDrift ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = Vector3.one;

            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;

            if (isWorldDrift)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 120;
                main.startLifetime = 3.5f;
                main.startSpeed = 0.35f;
                main.startSize = 0.48f;
                main.startColor = new Color(1f, 0.8f, 0.2f, 1.0f);

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 25f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.18f;

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.y = new ParticleSystem.MinMaxCurve(0.75f);

                var noise = ps.noise;
                noise.enabled = true;
                noise.frequency = 0.5f;
                noise.strength = 0.35f;
                noise.octaveCount = 1;

                var col = ps.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                        new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f),
                        new GradientColorKey(new Color(0.2f, 0.05f, 0.05f), 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0f),
                        new GradientAlphaKey(0.85f, 0.7f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                col.color = grad;
            }
            else
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 50;
                main.startLifetime = 0.20f;
                main.startSpeed = 2.2f;
                main.startSize = 0.32f;
                main.startColor = new Color(2.5f, 2.0f, 1.0f, 1.0f);

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 30f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 8f;
                shape.radius = 0.14f;

                var col = ps.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(2.5f, 2.0f, 1.0f), 0f),
                        new GradientColorKey(new Color(1.8f, 0.6f, 0.1f), 0.6f),
                        new GradientColorKey(new Color(0.8f, 0.15f, 0.02f), 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0f),
                        new GradientAlphaKey(1.0f, 0.6f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                col.color = grad;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(ps);
            EditorUtility.SetDirty(go);
            return ps;
        }

        public static void WireRuinedBloodCityScene()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            FireTailsService fireTailsService = UnityEngine.Object.FindAnyObjectByType<FireTailsService>();
            if (fireTailsService == null)
            {
                GameObject ftsGo = new GameObject("FireTailsService");
                fireTailsService = ftsGo.AddComponent<FireTailsService>();

                ArmorService armorService = UnityEngine.Object.FindAnyObjectByType<ArmorService>();
                if (armorService != null)
                {
                    ftsGo.transform.SetParent(armorService.transform.parent, false);
                    ftsGo.transform.SetSiblingIndex(armorService.transform.GetSiblingIndex() + 1);
                }
            }

            FireTailsConfigSO configAsset = AssetDatabase.LoadAssetAtPath<FireTailsConfigSO>(CONFIG_PATH);
            if (configAsset == null)
            {
                configAsset = EnsureConfigAsset();
            }

            PlayerManager player = UnityEngine.Object.FindAnyObjectByType<PlayerManager>();
            AudioClipPlayer audioClipPlayer = null;
            if (player != null)
            {
                audioClipPlayer = player.GetComponentInChildren<AudioClipPlayer>();
            }
            if (audioClipPlayer == null)
            {
                audioClipPlayer = UnityEngine.Object.FindAnyObjectByType<AudioClipPlayer>();
            }

            SerializedObject ftsSo = new SerializedObject(fireTailsService);
            ftsSo.FindProperty("_config").objectReferenceValue = configAsset;
            if (audioClipPlayer != null)
            {
                ftsSo.FindProperty("_audioClipPlayer").objectReferenceValue = audioClipPlayer;
            }
            ftsSo.ApplyModifiedPropertiesWithoutUndo();

            DefaultGameplaySceneInstaller installer = UnityEngine.Object.FindAnyObjectByType<DefaultGameplaySceneInstaller>();
            if (installer == null)
            {
                throw new InvalidOperationException("DefaultGameplaySceneInstaller not found in RuinedBloodCity scene.");
            }

            SerializedObject instSo = new SerializedObject(installer);
            instSo.FindProperty("_fireTailsService").objectReferenceValue = fireTailsService;
            instSo.ApplyModifiedPropertiesWithoutUndo();

            GameplayDevCommandsRegistrar devCommands = UnityEngine.Object.FindAnyObjectByType<GameplayDevCommandsRegistrar>();
            if (devCommands != null)
            {
                GameObject fireTailsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
                SerializedObject devSo = new SerializedObject(devCommands);
                devSo.FindProperty("_fireTailsPrefab").objectReferenceValue = fireTailsPrefab;
                devSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
