using System;
using System.Collections.Generic;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.Skills.PlayerSkills.Minigun;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Scripts.Editor.Tests
{
    public class MinigunShotResolverTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly Vector3 _origin = new Vector3(10000f, 100f, 10000f);
        private MinigunShotResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _resolver = new MinigunShotResolver();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects) { UnityEngine.Object.DestroyImmediate(obj); }
            _objects.Clear();
            _resolver.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void EmptyLaneReachesRange()
        {
            Assert.That(Resolve().Distance, Is.EqualTo(10f));
        }

        [TestCase(0, 1, 1.5f)]
        [TestCase(1, 2, 3.5f)]
        [TestCase(2, 3, 5.5f)]
        [TestCase(3, 3, 10f)]
        public void PiercingSelectsOrderedDistinctTargets(int piercing, int count, float distance)
        {
            Target(6f); Target(2f); Target(4f);
            MinigunShotResult result = Resolve(piercing);
            Assert.That(result.TargetCount, Is.EqualTo(count));
            Assert.That(result.Distance, Is.EqualTo(distance).Within(0.015f));
            _resolver.ApplyResolvedDamage(5);
            int hits = 0;
            foreach (GameObject obj in _objects)
            {
                MinigunTestTarget target = obj.GetComponent<MinigunTestTarget>();
                if (target != null) { hits += target.Hits; }
            }
            Assert.That(hits, Is.EqualTo(count));
            _resolver.ApplyResolvedDamage(5);
            int repeatedHits = 0;
            foreach (GameObject obj in _objects)
            {
                MinigunTestTarget target = obj.GetComponent<MinigunTestTarget>();
                if (target != null) { repeatedHits += target.Hits; }
            }
            Assert.That(repeatedHits, Is.EqualTo(count));
        }

        [Test]
        public void DuplicateCollidersConsumeOneSlot()
        {
            MinigunTestTarget target = Target(2f);
            GameObject child = Box(2f, LayerMask.NameToLayer("Enemy"));
            child.transform.SetParent(target.transform, true);
            Target(4f);
            Assert.That(Resolve(1).TargetCount, Is.EqualTo(2));
            _resolver.ApplyResolvedDamage(5);
            Assert.That(target.Hits, Is.EqualTo(1));
        }

        [Test]
        public void WallStopsEntireWideCorridor()
        {
            Target(2f); MinigunTestTarget behind = Target(6f);
            GameObject wall = Box(4f, LayerMask.NameToLayer("Impassable"));
            wall.transform.position += Vector3.right * 0.8f;
            MinigunShotResult result = Resolve(16, 2f);
            Assert.That(result.TargetCount, Is.EqualTo(1));
            Assert.That(result.Distance, Is.EqualTo(3.5f).Within(0.015f));
            Assert.That(_resolver.GetTerrainLimitedLength(_origin, Vector3.forward, 10f, 2f), Is.EqualTo(result.Distance));
            _resolver.ApplyResolvedDamage(5);
            Assert.That(behind.Hits, Is.Zero);
        }

        [Test]
        public void WallWinsEqualPlaneAndMuzzleOverlap()
        {
            Target(0f);
            Box(0f, LayerMask.NameToLayer("Impassable"));
            Assert.That(Resolve(16).TargetCount, Is.Zero);
            Assert.That(Resolve(16).Distance, Is.Zero);
        }

        [Test]
        public void EnemyMuzzleOverlapHasZeroDistance()
        {
            Target(0f);
            Assert.That(Resolve().Distance, Is.Zero);
            Assert.That(Resolve().TargetCount, Is.EqualTo(1));
        }

        [Test]
        public void DeadTargetsAndTargetsInvalidatedAfterCaptureAreSkipped()
        {
            MinigunTestTarget dead = Target(2f);
            dead.TestHealth.DecreaseHealth(100f);
            MinigunTestTarget living = Target(4f);
            Assert.That(Resolve().Distance, Is.EqualTo(3.5f).Within(0.015f));
            living.TestHealth.DecreaseHealth(100f);
            _resolver.ApplyResolvedDamage(5);
            Assert.That(dead.Hits + living.Hits, Is.Zero);
        }

        [Test]
        public void KillingStoppingTargetDoesNotExtendCapturedShot()
        {
            MinigunTestTarget front = Target(2f);
            MinigunTestTarget behind = Target(4f);
            MinigunShotResult result = Resolve();
            _resolver.ApplyResolvedDamage(100);
            Assert.That(front.Hits, Is.EqualTo(1));
            Assert.That(behind.Hits, Is.Zero);
            Assert.That(result.Distance, Is.EqualTo(1.5f).Within(0.015f));
        }

        [Test]
        public void ExactDistanceTiesUseOwnerIdentity()
        {
            MinigunTestTarget first = Target(2f);
            MinigunTestTarget second = Target(2f);
            Resolve();
            _resolver.ApplyResolvedDamage(5);
            MinigunTestTarget selected = first.GetEntityId() < second.GetEntityId() ? first : second;
            Assert.That(selected.Hits, Is.EqualTo(1));
        }

        [Test]
        public void OutsideWidthHeightAndBehindOriginAreExcluded()
        {
            Target(-2f);
            Target(12f);
            Target(3f).transform.position += Vector3.right * 2f;
            Target(4f).transform.position += Vector3.up * 2f;
            Assert.That(Resolve(16).TargetCount, Is.Zero);
        }

        [Test]
        public void FullFastSweepRetriesCompleteOverflowQuery()
        {
            for (int i = 0; i < 80; i++) { Target(2f + i * 0.05f); }
            MinigunShotResult result = Resolve();
            Assert.That(result.TargetCount, Is.EqualTo(1));
            Assert.That(result.Distance, Is.EqualTo(1.5f).Within(0.015f));
        }

        [Test]
        public void FullOverflowRejectsDamageAndClearsEarlierCapture()
        {
            MinigunTestTarget previous = Target(5f);
            Resolve();
            for (int i = 0; i < 2048; i++) { Box(0f, LayerMask.NameToLayer("Enemy")); }
            Assert.Throws<InvalidOperationException>(() => Resolve());
            _resolver.ApplyResolvedDamage(10);
            Assert.That(previous.Hits, Is.Zero);
        }

        [Test]
        public void MissingHealthFailsBeforeDamage()
        {
            GameObject obj = Box(2f, LayerMask.NameToLayer("Enemy"));
            obj.AddComponent<MinigunTestUnhealthyTarget>();
            Assert.Throws<InvalidOperationException>(() => Resolve());
        }

        [Test]
        public void CleanupDuringDamageCallbackCancelsRemainingTargets()
        {
            MinigunTestTarget first = Target(2f);
            MinigunTestTarget second = Target(4f);
            first.AfterDamage = _resolver.Clear;
            Resolve(1);
            _resolver.ApplyResolvedDamage(5);
            Assert.That(first.Hits, Is.EqualTo(1));
            Assert.That(second.Hits, Is.Zero);
        }

        [Test]
        public void TerrainTriggerDoesNotBlockAndExactPlaneWallWins()
        {
            Target(4f);
            Box(2f, LayerMask.NameToLayer("Impassable")).GetComponent<BoxCollider>().isTrigger = true;
            Assert.That(Resolve(16).TargetCount, Is.EqualTo(1));
            Box(4f, LayerMask.NameToLayer("Impassable"));
            Assert.That(Resolve(16).TargetCount, Is.Zero);
        }

        [Test]
        public void FullFastOverlapRetriesAndIncludesMuzzleEnemy()
        {
            MinigunTestTarget target = Target(0f);
            for (int i = 0; i < 80; i++) { Box(0f, LayerMask.NameToLayer("Enemy")); }
            Assert.That(Resolve().TargetCount, Is.EqualTo(1));
            _resolver.ApplyResolvedDamage(5);
            Assert.That(target.Hits, Is.EqualTo(1));
        }

        [Test]
        public void SelectedHitsExposeOrderedTargetAndHealthDetails()
        {
            MinigunTestTarget targetFar = Target(5f);
            MinigunTestTarget targetNear = Target(2f);
            MinigunShotResult result = Resolve(1);

            Assert.That(result.TargetCount, Is.EqualTo(2));
            Assert.That(_resolver.SelectedCount, Is.EqualTo(2));

            MinigunTargetHit first = _resolver.GetSelectedHit(0);
            MinigunTargetHit second = _resolver.GetSelectedHit(1);

            Assert.That(first.Target, Is.EqualTo(targetNear));
            Assert.That(first.Health, Is.EqualTo(targetNear.Health));
            Assert.That(first.Distance, Is.EqualTo(1.5f).Within(0.015f));

            Assert.That(second.Target, Is.EqualTo(targetFar));
            Assert.That(second.Health, Is.EqualTo(targetFar.Health));
            Assert.That(second.Distance, Is.EqualTo(4.5f).Within(0.015f));

            Assert.Throws<ArgumentOutOfRangeException>(() => _resolver.GetSelectedHit(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _resolver.GetSelectedHit(2));
        }

        private MinigunShotResult Resolve(int piercing = 0, float width = 1f)
        {
            Physics.SyncTransforms();
            return _resolver.ResolveShot(_origin, Vector3.forward, 10f, width, piercing);
        }

        private MinigunTestTarget Target(float distance)
        {
            GameObject obj = Box(distance, LayerMask.NameToLayer("Enemy"));
            obj.GetComponent<BoxCollider>().isTrigger = true;
            return obj.AddComponent<MinigunTestTarget>();
        }

        private GameObject Box(float distance, int layer)
        {
            GameObject obj = new GameObject("MinigunResolverTest");
            _objects.Add(obj);
            obj.layer = layer;
            obj.transform.position = _origin + Vector3.forward * distance;
            obj.AddComponent<BoxCollider>();
            return obj;
        }
    }

    public class MinigunTestUnhealthyTarget : MonoBehaviour, IDamageable
    {
        public void TakeDamage(float damage) { }
        public void TakeFullHpDamage() { }
    }

    public class MinigunTestTarget : MonoBehaviour, IDamageable, IHealthy
    {
        public MinigunTestHealth TestHealth { get; } = new MinigunTestHealth();
        public IHealth Health => TestHealth;
        public int Hits { get; private set; }
        public Action AfterDamage { get; set; }
        public void TakeDamage(float damage) { Hits++; TestHealth.DecreaseHealth(damage); AfterDamage?.Invoke(); }
        public void TakeFullHpDamage() { TakeDamage(Health.MaxHealth); }
    }

    public class MinigunTestHealth : IHealth
    {
        public float CurrentHealth { get; private set; } = 100f;
        public float MaxHealth { get; set; } = 100f;
        public event EventHandler OnHealthChanged { add { } remove { } }
        public event EventHandler OnHealthDecreased { add { } remove { } }
        public event EventHandler OnHealthIncreased { add { } remove { } }
        public event EventHandler OnNoHealth { add { } remove { } }
        public bool IsAlive() { return CurrentHealth > 0; }
        public void DecreaseHealth(float value) { CurrentHealth = Mathf.Max(0f, CurrentHealth - value); }
        public void IncreaseHealth(float value) { CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + value); }
    }
}
