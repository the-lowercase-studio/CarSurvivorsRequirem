using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Enemies.Bosses;
using Assets.Scripts.Enemies.Bosses.Golem;
using Assets.Scripts.Enemies.Bosses.Golem.Config;
using Assets.Scripts.Enemies.Bosses.Towers.MortarTower;
using Assets.ScriptableObjects.Enemies.Bosses;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.UI.HUD;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Editor.Tests
{
    public class BossHUDPresenterTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private BossHUDPresenter _presenter;
        private GameObject _visual;
        private BossHealthBarView _tower;
        private BossHealthBarView _golem;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Boss HUD test", typeof(RectTransform)));
            root.SetActive(false);
            _presenter = root.AddComponent<BossHUDPresenter>();
            _visual = Child(root.transform, "Visual");
            _tower = View(_visual.transform, BossHealthBarKind.Tower);
            _golem = View(_visual.transform, BossHealthBarKind.Golem);
            var title = Child(_visual.transform, "Title").AddComponent<TextMeshProUGUI>();
            Set(_presenter, "_visual", _visual);
            Set(_presenter, "_towerHealthBar", _tower);
            Set(_presenter, "_golemHealthBar", _golem);
            Set(_presenter, "_bossTitleText", title);
            root.SetActive(true);
            Call(_presenter, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Hide();
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [TestCase(1f, false, 1f, 1f)]
        [TestCase(0.65f, false, 0.5f, 1f)]
        [TestCase(0.4f, false, 0.142857f, 1f)]
        [TestCase(0.3f, true, 0f, 1f)]
        [TestCase(0.15f, true, 0f, 0.5f)]
        public void ShowMapsCurrentHealthWithoutFullHealthFlash(float hp, bool enraged, float normal, float rage)
        {
            var source = new Source(BossHealthBarKind.Golem, hp, enraged);
            _presenter.Show(source, "Renamed designer boss");
            Assert.That(Fill(_golem, "_normalFill"), Is.EqualTo(normal).Within(0.0001f));
            Assert.That(Fill(_golem, "_enragedFill"), Is.EqualTo(rage).Within(0.0001f));
            Assert.That(_golem.gameObject.activeSelf, Is.True);
            Assert.That(_tower.gameObject.activeSelf, Is.False);
            Assert.That(Get<TextMeshProUGUI>(_presenter, "_bossTitleText").text, Is.EqualTo("Renamed designer boss"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CrossingThresholdConvergesForEitherCallbackOrder(bool healthFirst)
        {
            var source = new Source(BossHealthBarKind.Tower, 1f, false);
            _presenter.Show(source, "Tower");
            source.Data.Current = 15f;
            if (healthFirst)
            {
                source.Data.Changed();
                Assert.That(Fill(_tower, "_enragedFill"), Is.EqualTo(1f));
                source.IsEnraged = true;
                source.Notify();
            }
            else
            {
                source.IsEnraged = true;
                source.Notify();
                source.Data.Changed();
            }
            Tween tween = Get<Tween>(_presenter, "_healthTween");
            tween.Goto(0.05f);
            Assert.That(Fill(_tower, "_enragedFill"), Is.EqualTo(1f));
            tween.Goto(0.2f);
            Assert.That(Fill(_tower, "_normalFill"), Is.Zero);
            Assert.That(Fill(_tower, "_enragedFill"), Is.EqualTo(0.5f).Within(0.0001f));
            Sequence punch = Get<Sequence>(_tower, "_punchSequence");
            source.Notify();
            Assert.That(Get<Sequence>(_tower, "_punchSequence"), Is.SameAs(punch));
            Assert.That(Get<Tween>(_presenter, "_healthTween"), Is.SameAs(tween));
        }

        [Test]
        public void ReplacementDetachesOldSourceAndRejectsInFlightCallback()
        {
            var old = new Source(BossHealthBarKind.Tower, 0.15f, true);
            _presenter.Show(old, "Tower");
            Action stale = old.Listeners;
            Sequence punch = Get<Sequence>(_tower, "_punchSequence");
            punch.Goto(0.04f);
            var next = new Source(BossHealthBarKind.Golem, 0.65f, false);
            _presenter.Show(next, "Golem");
            stale();
            old.Data.Current = 0f;
            old.Data.Changed();
            Assert.That(old.Listeners, Is.Null);
            Assert.That(old.Data.ChangedListeners, Is.Null);
            Assert.That(old.Data.DeathListeners, Is.Null);
            Assert.That(Get<RectTransform>(_tower, "_enragedFillMotion").anchoredPosition, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(Get<Sequence>(_tower, "_punchSequence"), Is.Null);
            Assert.That(Fill(_golem, "_normalFill"), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(_visual.activeSelf, Is.True);
            _presenter.Show(old, "Tower again");
            Assert.That(next.Listeners, Is.Null);
        }

        [Test]
        public void DeathAndDisableHideImmediatelyAndRepeatedHideIsSafe()
        {
            var source = new Source(BossHealthBarKind.Tower, 1f, false);
            _presenter.Show(source, "Tower");
            source.Data.Current = 0f;
            source.Data.Die();
            Assert.That(_visual.activeSelf, Is.False);
            Assert.That(Get<Sequence>(_tower, "_punchSequence"), Is.Null);
            _presenter.Hide();
            _presenter.Hide();
            source.Data.Current = 100f;
            _presenter.Show(source, "Tower");
            source.IsActive = false;
            source.Notify();
            Assert.That(_visual.activeSelf, Is.False);
            source.IsActive = true;
            _presenter.Show(source, "Tower");
            Call(_presenter, "OnDisable");
            Assert.That(source.Listeners, Is.Null);
            Assert.That(_visual.activeSelf, Is.False);
        }

        [Test]
        public void ConfiguredThresholdAndCurrentMaximumDriveMapping()
        {
            var source = new Source(BossHealthBarKind.Golem, 0.1f, true) { EnrageHealthPercent = 0.2f };
            source.Data.MaxHealth = 50f;
            _presenter.Show(source, "Boss");
            Assert.That(Fill(_golem, "_enragedFill"), Is.EqualTo(1f));
            source.Data.MaxHealth = 100f;
            source.Notify();
            Get<Tween>(_presenter, "_healthTween").Goto(0.2f);
            Assert.That(Fill(_golem, "_enragedFill"), Is.EqualTo(0.5f));
            source.Data.Current = 60f;
            source.Data.Changed();
            Get<Tween>(_presenter, "_healthTween").Goto(0.2f);
            Assert.That(Get<Sequence>(_golem, "_punchSequence"), Is.Not.Null);
            Assert.That(Fill(_golem, "_enragedFill"), Is.EqualTo(1f));
        }

        [Test]
        public void RapidDamageRetargetsFromDisplayedHealthWithoutBacklog()
        {
            var source = new Source(BossHealthBarKind.Tower, 1f, false);
            _presenter.Show(source, "Tower");
            source.Data.Current = 65f;
            source.Data.Changed();
            var first = Get<Tween>(_presenter, "_healthTween");
            first.Goto(0.05f);
            float displayed = Get<float>(_presenter, "_displayedRatio");
            source.Data.Current = 40f;
            source.Data.Changed();
            Assert.That(Get<Tween>(_presenter, "_healthTween"), Is.SameAs(first));
            Assert.That(Get<float>(_presenter, "_displayedRatio"), Is.EqualTo(displayed));
            Get<Tween>(_presenter, "_healthTween").Goto(0.2f);
            Assert.That(Fill(_tower, "_normalFill"), Is.EqualTo(0.142857f).Within(0.0001f));
        }

        [Test]
        public void ScaledPauseFreezesHealthAndPunchAndViewDisableRestoresBaseline()
        {
            var source = new Source(BossHealthBarKind.Golem, 1f, false);
            _presenter.Show(source, "Boss");
            source.IsEnraged = true;
            source.Data.Current = 15f;
            source.Notify();
            var health = Get<Tween>(_presenter, "_healthTween");
            var punch = Get<Sequence>(_golem, "_punchSequence");
            health.SetUpdate(UpdateType.Manual);
            punch.SetUpdate(UpdateType.Manual);
            DOTween.ManualUpdate(0f, 0.1f);
            Assert.That(Get<float>(_presenter, "_displayedRatio"), Is.EqualTo(1f));
            Assert.That(punch.Elapsed(), Is.Zero);
            DOTween.ManualUpdate(0.05f, 0.05f);
            Assert.That(Get<float>(_presenter, "_displayedRatio"), Is.LessThan(1f));
            Assert.That(punch.Elapsed(), Is.GreaterThan(0f));
            Call(_golem, "OnDisable");
            Assert.That(Get<Sequence>(_golem, "_punchSequence"), Is.Null);
            Assert.That(Get<RectTransform>(_golem, "_enragedFillMotion").anchoredPosition, Is.EqualTo(new Vector2(2f, 3f)));
        }

        [Test]
        public void CompletedHealthTweenCannotKillAnUnrelatedRecycledTween()
        {
            var source = new Source(BossHealthBarKind.Tower, 1f, false);
            _presenter.Show(source, "Tower");
            source.Data.Current = 65f;
            source.Data.Changed();
            Get<Tween>(_presenter, "_healthTween").Complete();
            float otherValue = 0f;
            var other = DOTween.To(() => { return otherValue; }, value => { otherValue = value; }, 1f, 1f)
                .SetAutoKill(false);
            try
            {
                _presenter.Hide();
                Assert.That(other.IsActive(), Is.True);
            }
            finally
            {
                other.Kill();
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(float.NaN)]
        public void InvalidThresholdFailsVisibly(float threshold)
        {
            var source = new Source(BossHealthBarKind.Tower, 1f, false) { EnrageHealthPercent = threshold };
            Assert.Throws<InvalidOperationException>(() => { _presenter.Show(source, "Invalid"); });
            Assert.That(_visual.activeSelf, Is.False);
        }

        [Test]
        public void MigratedAndNewConfigurationsExposeOnlyTwoPhases()
        {
            var authored = AssetDatabase.LoadAssetAtPath<GolemBossConfigSO>("Assets/ScriptableObjects/Enemy/Bosses/Golem/GolemBossConfig.asset");
            Assert.That(authored.Phase2HealthPercent, Is.EqualTo(0.3f));
            Assert.That(authored.Phase2CooldownMultiplier, Is.EqualTo(0.4f));
            Assert.That(authored.Phase2SpeedMultiplier, Is.EqualTo(1.35f));
            Assert.That(authored.Phase2ArmSpeedMultiplier, Is.EqualTo(1.6f));
            Assert.That(authored.SkyBarrageCyclesPhase1, Is.EqualTo(2));
            Assert.That(authored.SkyBarrageCyclesPhase2, Is.EqualTo(5));
            var fresh = Track(ScriptableObject.CreateInstance<GolemBossConfigSO>());
            Assert.That(fresh.Phase2HealthPercent, Is.EqualTo(0.3f));
            Assert.That(fresh.SkyBarrageCyclesPhase2, Is.EqualTo(3));
            Assert.That(typeof(GolemBossConfigSO).GetProperty("Phase3HealthPercent"), Is.Null);
            Assert.That(new SerializedObject(authored).FindProperty("_skyBarrageCyclesPhase3"), Is.Null);
            var tower = AssetDatabase.LoadAssetAtPath<MortarTowerConfigSO>("Assets/ScriptableObjects/Enemy/Towers/MortarTowerConfig.asset");
            Assert.That(tower.EnrageHealthPercent, Is.EqualTo(0.3f));
            Assert.That(Track(ScriptableObject.CreateInstance<MortarTowerConfigSO>()).EnrageHealthPercent, Is.EqualTo(0.3f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BossOwnersEnrageOnceAtThirtyPercentAndRetainLatchWhenHealed(bool golem)
        {
            var go = Track(new GameObject("Inactive boss test"));
            go.SetActive(false);
            MonoBehaviour boss;
            if (golem)
            {
                boss = go.AddComponent<GolemBoss>();
                Set(boss, "_config", Track(ScriptableObject.CreateInstance<GolemBossConfigSO>()));
            }
            else
            {
                boss = go.AddComponent<MortarTowerBoss>();
                Set(boss, "_config", Track(ScriptableObject.CreateInstance<MortarTowerConfigSO>()));
            }
            var health = new TestHealth { Current = 60f };
            boss.GetType().GetProperty("Health").SetValue(boss, health);
            var source = (IBossHUDSource)boss;
            int events = 0;
            source.OnPresentationChanged += () => { events++; };
            health.Current = 0f;
            CallHealth(boss);
            Assert.That(source.IsEnraged, Is.False, "A lethal hit must never commit enrage.");
            health.Current = 60f;
            CallHealth(boss);
            health.Current = 40f;
            CallHealth(boss);
            Assert.That(source.IsEnraged, Is.False);
            if (golem) Assert.That(((GolemBoss)boss).CurrentPhase, Is.EqualTo(1));
            health.Current = 30f;
            CallHealth(boss);
            Assert.That(source.IsEnraged, Is.True);
            if (golem)
            {
                var owner = (GolemBoss)boss;
                Assert.That(owner.CurrentPhase, Is.EqualTo(2));
                Assert.That(owner.CurrentCooldownMultiplier, Is.EqualTo(0.4f));
                Assert.That(owner.CurrentSpeedMultiplier, Is.EqualTo(1.35f));
                Assert.That(owner.CurrentArmSpeedMultiplier, Is.EqualTo(1.6f));
                var barrage = new Assets.Scripts.Enemies.Bosses.Golem.StateMachine.States.GolemSkyBarrageState(owner, null);
                Assert.That((int)barrage.GetType().GetMethod("GetTotalCyclesForCurrentPhase", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(barrage, null), Is.EqualTo(owner.Config.SkyBarrageCyclesPhase2));
            }
            health.Current = 90f;
            CallHealth(boss);
            Assert.That(source.IsEnraged, Is.True);
            Assert.That(events, Is.EqualTo(1));
        }

        private void CallHealth(object boss)
        {
            boss.GetType().GetMethod("Health_OnHealthChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(boss, new object[] { null, EventArgs.Empty });
        }

        private BossHealthBarView View(Transform parent, BossHealthBarKind kind)
        {
            var root = Child(parent, kind.ToString());
            var view = root.AddComponent<BossHealthBarView>();
            string prefix = kind == BossHealthBarKind.Golem ? "GOLEM_BOSS_HP_BAR_" : "TOWER_HP_BAR_";
            string sheet = kind == BossHealthBarKind.Golem ? "BOSS" : "TOWER";
            var sprites = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Imported/Images/UI/Boss/" + sheet + "_HP_BAR_SPRITESHEET.png"))
                if (asset is Sprite sprite) sprites.Add(sprite.name, sprite);
            foreach (var pair in new[] { new[] { "_mainBackground", "MAIN_BACKGROUND" }, new[] { "_normalFillBackground", "FILL_BACKGROUND_PART_NORMAL" }, new[] { "_enragedFillBackground", "FILL_BACKGROUND_PART_ENRAGED" }, new[] { "_normalFill", "FILL_PART_NORMAL" }, new[] { "_enragedFill", "FILL_PART_ENRAGED" } })
            {
                var image = Child(root.transform, pair[0]).AddComponent<Image>();
                image.sprite = sprites[prefix + pair[1]];
                image.color = Color.white;
                image.raycastTarget = false;
                image.type = pair[0].EndsWith("Fill") ? Image.Type.Filled : Image.Type.Simple;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
                Set(view, pair[0], image);
            }
            var motion = (RectTransform)Child(root.transform, "Motion").transform;
            motion.anchoredPosition = new Vector2(2f, 3f);
            Get<Image>(view, "_enragedFill").transform.SetParent(motion, false);
            Set(view, "_enragedFillMotion", motion);
            return view;
        }

        private GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            _objects.Add(obj);
            return obj;
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static T Get<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void Call(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static float Fill(BossHealthBarView view, string field)
        {
            return Get<Image>(view, field).fillAmount;
        }

        private class Source : IBossHUDSource
        {
            public TestHealth Data { get; } = new TestHealth();
            public IHealth Health => Data;
            public BossHealthBarKind HealthBarKind { get; }
            public float EnrageHealthPercent { get; set; } = 0.3f;
            public bool IsEnraged { get; set; }
            public bool IsActive { get; set; } = true;
            public Action Listeners { get; private set; }
            public event Action OnPresentationChanged { add { Listeners += value; } remove { Listeners -= value; } }
            public Source(BossHealthBarKind kind, float ratio, bool enraged)
            {
                HealthBarKind = kind;
                Data.Current = ratio * 100f;
                IsEnraged = enraged;
            }
            public void Notify() { Listeners?.Invoke(); }
        }

        private class TestHealth : IHealth
        {
            public float Current { get; set; } = 100f;
            public float CurrentHealth => Current;
            public float MaxHealth { get; set; } = 100f;
            public EventHandler ChangedListeners { get; private set; }
            public EventHandler DeathListeners { get; private set; }
            public event EventHandler OnHealthChanged { add { ChangedListeners += value; } remove { ChangedListeners -= value; } }
            public event EventHandler OnNoHealth { add { DeathListeners += value; } remove { DeathListeners -= value; } }
            public event EventHandler OnHealthDecreased { add { } remove { } }
            public event EventHandler OnHealthIncreased { add { } remove { } }
            public bool IsAlive() { return Current > 0f; }
            public void DecreaseHealth(float value) { Current -= value; Changed(); }
            public void IncreaseHealth(float value) { Current += value; Changed(); }
            public void Changed() { ChangedListeners?.Invoke(this, EventArgs.Empty); }
            public void Die() { DeathListeners?.Invoke(this, EventArgs.Empty); }
        }
    }
}
