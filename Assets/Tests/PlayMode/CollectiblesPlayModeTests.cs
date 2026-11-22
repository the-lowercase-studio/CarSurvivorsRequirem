using System.Collections;
using Assets.Scripts.Effects;
using Assets.Scripts.Enemies;
using Assets.Scripts.LayerMasks.Constants;
using Assets.Scripts.PowerUps;
using Assets.Scripts.PowerUps.Collectibles;
using Assets.Scripts.Skills.ObjectsImpactingSkills.Crate;
using DG.Tweening;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.TestTools;

namespace Assets.Tests.PlayMode
{
    public class CollectiblesPlayModeTests
    {
        private const string SKIL_CRATE_PREFAB_PATH = "Assets/Prefabs/Collectibles/SkilCrate.prefab";
        private const string BUBBLE_SHIELD_PREFAB_PATH = "Assets/Prefabs/Collectibles/BubbleShield.prefab";
        private const string ARMOR_PREFAB_PATH = "Assets/Prefabs/Collectibles/Armor.prefab";
        private const string DROP_CONFIG_ASSET_PATH = "Assets/ScriptableObjects/Collectibles/DropAnimationConfiguration.asset";

        private PlayModeTestHarness _harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _harness = new PlayModeTestHarness();
            _harness.BeginIsolatedScene("CollectiblesPlayMode");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_harness != null)
            {
                yield return _harness.CleanupAsync();
            }
        }

        [UnityTest]
        public IEnumerator SkillCrate_PlayerTriggerOverlap_RaisesOnCollectedAndCanBeReleasedOnce()
        {
            GameObject cratePrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(SKIL_CRATE_PREFAB_PATH);
            GameObject crateGo = _harness.InstantiateTrackedPrefab(cratePrefab, Vector3.zero, Quaternion.identity);
            SkillCrate crate = crateGo.GetComponent<SkillCrate>();
            Assert.IsNotNull(crate);
            crate.OnGet();

            int collectedCount = 0;
            int releaseCount = 0;
            crate.OnCollected += (sender, args) => { collectedCount++; };
            crate.OnCanBeReleased += (sender, args) => { releaseCount++; };

            GameObject player = PlayModeTestHarness.CreateBoxColliderBody(
                _harness,
                "PlayerProbe",
                LayerMaskConstants.PLAYER,
                new Vector3(0f, 0f, 5f),
                Vector3.one * 2f,
                isTrigger: false,
                addKinematicRigidbody: true);

            yield return PlayModeTestHarness.WaitForFixedUpdates(2);
            Assert.AreEqual(0, collectedCount);
            Assert.AreEqual(0, releaseCount);

            player.transform.position = Vector3.zero;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => collectedCount == 1 && releaseCount == 1,
                2f,
                "Expected SkillCrate OnTriggerEnter to raise OnCollected and OnCanBeReleased.");

            yield return PlayModeTestHarness.WaitForFixedUpdates(2);
            Assert.AreEqual(1, collectedCount);
            Assert.AreEqual(1, releaseCount);

            crate.OnGet();
            player.transform.position = new Vector3(0f, 0f, 5f);
            yield return PlayModeTestHarness.WaitForFixedUpdates(2);
            player.transform.position = Vector3.zero;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => collectedCount == 2 && releaseCount == 2,
                2f,
                "Expected SkillCrate OnGet to reset collection guard for pool reuse.");
        }

        [UnityTest]
        public IEnumerator SkillCrate_NonPlayerTriggerOverlap_DoesNotCollect()
        {
            GameObject cratePrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(SKIL_CRATE_PREFAB_PATH);
            GameObject crateGo = _harness.InstantiateTrackedPrefab(cratePrefab, Vector3.zero, Quaternion.identity);
            SkillCrate crate = crateGo.GetComponent<SkillCrate>();
            Assert.IsNotNull(crate);
            crate.OnGet();

            int collectedCount = 0;
            crate.OnCollected += (sender, args) => { collectedCount++; };

            PlayModeTestHarness.CreateBoxColliderBody(
                _harness,
                "EnemyProbe",
                LayerMaskConstants.ENEMY,
                Vector3.zero,
                Vector3.one * 2f,
                isTrigger: false,
                addKinematicRigidbody: true);

            yield return PlayModeTestHarness.WaitForFixedUpdates(3);
            Assert.AreEqual(0, collectedCount);
        }

        [UnityTest]
        public IEnumerator PowerUpCollectible_WithScaleToAnimationOnEnable_OnReleaseKillsTweensForPoolReuse()
        {
            GameObject powerUpPrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(BUBBLE_SHIELD_PREFAB_PATH);
            GameObject go = _harness.InstantiateTrackedPrefab(powerUpPrefab, Vector3.zero, Quaternion.identity);
            PowerUpCollectible collectible = go.GetComponent<PowerUpCollectible>();
            ScaleToAnimationOnEnable scaleAnim = go.GetComponent<ScaleToAnimationOnEnable>();
            Assert.IsNotNull(collectible);
            Assert.IsNotNull(scaleAnim);
            Assert.AreEqual(PowerUpType.BubbleShield, collectible.Type);

            PlayModeTestHarness.SetPrivateField(typeof(ScaleToAnimationOnEnable), scaleAnim, "_scalingDuration", 2.0f);
            go.SetActive(false);
            go.SetActive(true);

            int releasedCount = 0;
            collectible.OnCanBeReleased += (sender, args) =>
            {
                releasedCount++;
                collectible.OnRelease();
                go.SetActive(false);
            };

            yield return null;
            Assert.That(DOTween.TweensByTarget(go.transform, true), Is.Not.Null);
            Assert.That(DOTween.TweensByTarget(go.transform, true).Count, Is.GreaterThan(0),
                "ScaleToAnimationOnEnable should create active scale tween on enable.");

            GameObject playerProbe = PlayModeTestHarness.CreateBoxColliderBody(
                _harness,
                "PlayerProbe",
                LayerMaskConstants.PLAYER,
                Vector3.zero,
                Vector3.one * 2f,
                isTrigger: false,
                addKinematicRigidbody: true);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => releasedCount == 1,
                2f,
                "Expected PowerUpCollectible trigger contact to invoke OnCanBeReleased.");

            var remainingTweens = DOTween.TweensByTarget(go.transform, false);
            Assert.IsTrue(remainingTweens == null || remainingTweens.Count == 0,
                "OnRelease must kill all active DOTween tweens on transform.");
            Assert.IsFalse(go.activeSelf);

            playerProbe.SetActive(false);
            collectible.OnGet();
            go.SetActive(true);
            yield return null;
            Assert.IsTrue(go.activeSelf);
            var restartedTweens = DOTween.TweensByTarget(go.transform, true);
            Assert.That(restartedTweens, Is.Not.Null);
            Assert.That(restartedTweens.Count, Is.GreaterThan(0),
                "Re-enabling pooled PowerUpCollectible should restart ScaleToAnimationOnEnable cleanly.");
        }

        [UnityTest]
        public IEnumerator CollectibleDropNotifier_SpawnsAndScattersRealCollectibles_DispatchesEventsAndReturnsToPool()
        {
            DropAnimationConfiguration dropConfig =
                PlayModeTestHarness.LoadEditorAsset<DropAnimationConfiguration>(DROP_CONFIG_ASSET_PATH);
            GameObject cratePrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(SKIL_CRATE_PREFAB_PATH);
            GameObject armorPrefab = PlayModeTestHarness.LoadEditorAsset<GameObject>(ARMOR_PREFAB_PATH);

            Container container = _harness.BuildContainer(builder =>
            {
                builder.AddSingleton(dropConfig);
            });

            GameObject itemsParent = _harness.TrackGameObject("CollectiblesParent");
            GameObject notifierGo = _harness.TrackGameObject("CollectibleDropNotifier", false);
            CollectibleDropNotifier notifier = notifierGo.AddComponent<CollectibleDropNotifier>();
            PlayModeTestHarness.SetPrivateField(
                typeof(CollectibleDropNotifier),
                notifier,
                "_collectibleItemsParent",
                itemsParent.transform);
            AttributeInjector.Inject(notifier, container);
            notifierGo.SetActive(true);

            int skillCrateCollected = 0;
            PowerUpType? collectedPowerUp = null;
            notifier.OnSkillUpgradeCollectibleCollected += (sender, args) => { skillCrateCollected++; };
            notifier.OnPowerUpCollected += type => { collectedPowerUp = type; };

            Vector3 crateTarget = new Vector3(1.5f, 0.25f, 0f);
            Vector3 armorTarget = new Vector3(-1.5f, 0.25f, 0f);
            notifier.SpawnCollectible(cratePrefab, new Vector3(0f, 0.5f, 0f), crateTarget);
            notifier.SpawnCollectible(armorPrefab, new Vector3(0f, 0.5f, 0f), armorTarget);

            Assert.AreEqual(2, itemsParent.transform.childCount);
            Transform spawnedCrate = itemsParent.transform.GetChild(0);
            Transform spawnedArmor = itemsParent.transform.GetChild(1);
            Assert.IsTrue(spawnedCrate.gameObject.activeSelf);
            Assert.IsTrue(spawnedArmor.gameObject.activeSelf);

            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => Vector3.Distance(spawnedCrate.position, crateTarget) < 0.05f
                    && Vector3.Distance(spawnedArmor.position, armorTarget) < 0.05f
                    && Mathf.Approximately(spawnedCrate.localScale.x, 1f),
                3f,
                "Expected DOTween drop scatter + scale sequence to land collectibles cleanly at targets.");

            GameObject player = PlayModeTestHarness.CreateBoxColliderBody(
                _harness,
                "PlayerCollector",
                LayerMaskConstants.PLAYER,
                new Vector3(0f, 0.25f, 10f),
                new Vector3(1.5f, 2f, 1.5f),
                isTrigger: false,
                addKinematicRigidbody: true);

            yield return PlayModeTestHarness.WaitForFixedUpdates(2);
            player.transform.position = crateTarget;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => skillCrateCollected == 1 && !spawnedCrate.gameObject.activeSelf,
                2f,
                "Expected player driving onto scattered SkillCrate to collect and return it to pool.");

            player.transform.position = armorTarget;
            yield return PlayModeTestHarness.WaitUntilWithTimeout(
                () => collectedPowerUp == PowerUpType.Armor && !spawnedArmor.gameObject.activeSelf,
                2f,
                "Expected player driving onto scattered Armor pickup to dispatch Armor and return it to pool.");

            Assert.IsNull(DOTween.TweensByTarget(spawnedCrate, true));
            Assert.IsNull(DOTween.TweensByTarget(spawnedArmor, true));
        }
    }
}
