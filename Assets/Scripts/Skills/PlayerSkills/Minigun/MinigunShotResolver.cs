using System;
using Assets.Scripts.HealthSystem;
using Assets.Scripts.LayerMasks;
using Assets.Scripts.StatusEffects;
using Assets.Scripts.Skills.PlayerSkills.Minigun.Constants;
using UnityEngine;

namespace Assets.Scripts.Skills.PlayerSkills.Minigun
{
    public enum MinigunShotStopReason { Range, Terrain, Piercing }

    public readonly struct MinigunShotResult
    {
        public float Distance { get; }
        public int TargetCount { get; }
        public MinigunShotStopReason StopReason { get; }

        public MinigunShotResult(float distance, int targetCount, MinigunShotStopReason stopReason)
        {
            Distance = distance;
            TargetCount = targetCount;
            StopReason = stopReason;
        }
    }

    public readonly struct MinigunTargetHit
    {
        public IDamageable Target { get; }
        public IHealth Health { get; }
        public float Distance { get; }

        public MinigunTargetHit(IDamageable target, IHealth health, float distance)
        {
            Target = target;
            Health = health;
            Distance = distance;
        }
    }

    public sealed class MinigunShotResolver
    {
        private struct Contact
        {
            public Component Owner;
            public IDamageable Target;
            public IHealth Health;
            public float Distance;
            public EntityId InstanceId;
        }

        private readonly Collider[] _overlaps = new Collider[MinigunConstants.FAST_QUERY_CAPACITY];
        private readonly Collider[] _overflowOverlaps = new Collider[MinigunConstants.OVERFLOW_QUERY_CAPACITY];
        private readonly RaycastHit[] _hits = new RaycastHit[MinigunConstants.FAST_QUERY_CAPACITY];
        private readonly RaycastHit[] _overflowHits = new RaycastHit[MinigunConstants.OVERFLOW_QUERY_CAPACITY];
        private readonly Contact[] _contacts = new Contact[MinigunConstants.OVERFLOW_QUERY_CAPACITY * 2];
        private readonly Contact[] _selected = new Contact[MinigunConstants.MAX_TARGETS];
        private int _contactCount;
        private int _selectedCount;
        private int _applyingCount;
        private uint _resolutionVersion;

        public MinigunShotResult ResolveShot(Vector3 origin, Vector3 direction, float range, float width, int piercing)
        {
            Clear();
            ValidateGeometry(origin, direction, range, width);
            if (piercing < 0 || piercing >= MinigunConstants.MAX_TARGETS)
            {
                throw new InvalidOperationException("Minigun piercing exceeds preallocated target capacity.");
            }

            try
            {
                float terrainDistance = Query(origin, direction, range, width, TerrainLayers.Impassable, QueryTriggerInteraction.Ignore, false);
                Query(origin, direction, range, width, EntityLayers.Enemies, QueryTriggerInteraction.Collide, true);
                SortContacts();
                bool hasTerrain = terrainDistance <= range;
                float distance = Mathf.Min(terrainDistance, range);
                MinigunShotStopReason reason = hasTerrain ? MinigunShotStopReason.Terrain : MinigunShotStopReason.Range;
                int budget = piercing + 1;
                for (int i = 0; i < _contactCount; i++)
                {
                    Contact contact = _contacts[i];
                    if (hasTerrain && contact.Distance >= terrainDistance - MinigunConstants.QUERY_DEPTH)
                    {
                        break;
                    }
                    _selected[_selectedCount++] = contact;
                    if (_selectedCount == budget)
                    {
                        distance = contact.Distance;
                        reason = MinigunShotStopReason.Piercing;
                        break;
                    }
                }
                Array.Clear(_contacts, 0, _contactCount);
                _contactCount = 0;
                return new MinigunShotResult(distance, _selectedCount, reason);
            }
            catch
            {
                Clear();
                throw;
            }
        }

        public int SelectedCount => _selectedCount;

        public MinigunTargetHit GetSelectedHit(int index)
        {
            if (index < 0 || index >= _selectedCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            Contact contact = _selected[index];
            return new MinigunTargetHit(contact.Target, contact.Health, contact.Distance);
        }

        public void ApplyResolvedDamage(int damage)
        {
            if (damage < 0)
            {
                Clear();
                throw new InvalidOperationException("Minigun damage cannot be negative.");
            }
            int count = _selectedCount;
            uint version = _resolutionVersion;
            _applyingCount = count;
            _selectedCount = 0;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (version != _resolutionVersion) { break; }
                    Contact contact = _selected[i];
                    _selected[i] = default;
                    if (IsAlive(contact))
                    {
                        contact.Target.TakeDamage(damage);
                    }
                }
            }
            finally
            {
                Array.Clear(_selected, 0, count);
                _applyingCount = 0;
            }
        }

        public float GetTerrainLimitedLength(Vector3 origin, Vector3 direction, float range, float width)
        {
            ValidateGeometry(origin, direction, range, width);
            return Mathf.Min(range, Query(origin, direction, range, width, TerrainLayers.Impassable, QueryTriggerInteraction.Ignore, false));
        }

        public void Clear()
        {
            Array.Clear(_contacts, 0, _contactCount);
            Array.Clear(_selected, 0, Mathf.Max(_selectedCount, _applyingCount));
            _contactCount = 0;
            _selectedCount = 0;
            _resolutionVersion++;
        }

        public static void ValidateGeometry(Vector3 origin, Vector3 direction, float range, float width)
        {
            if (!IsFinite(origin.x) || !IsFinite(origin.y) || !IsFinite(origin.z)
                || !IsFinite(direction.x) || !IsFinite(direction.y) || !IsFinite(direction.z)
                || Mathf.Abs(direction.y) > MinigunConstants.QUERY_DEPTH
                || Mathf.Abs(direction.sqrMagnitude - 1f) > MinigunConstants.QUERY_DEPTH
                || !IsFinite(range) || range <= 0f || !IsFinite(width) || width <= 0f)
            {
                throw new InvalidOperationException("Minigun requires finite positive dimensions and a normalized horizontal direction.");
            }
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private float Query(Vector3 origin, Vector3 direction, float range, float width, int layers, QueryTriggerInteraction triggers, bool collectEnemies)
        {
            float depth = Mathf.Min(MinigunConstants.QUERY_DEPTH, range);
            Vector3 center = origin + direction * (depth * 0.5f);
            Vector3 extents = new Vector3(width * 0.5f, width * 0.5f, depth * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Collider[] overlaps = _overlaps;
            int overlapCount = Physics.OverlapBoxNonAlloc(center, extents, overlaps, rotation, layers, triggers);
            if (overlapCount == overlaps.Length)
            {
                Array.Clear(overlaps, 0, overlapCount);
                overlaps = _overflowOverlaps;
                overlapCount = Physics.OverlapBoxNonAlloc(center, extents, overlaps, rotation, layers, triggers);
            }
            RaycastHit[] hits = _hits;
            int hitCount = 0;
            try
            {
                if (overlapCount == overlaps.Length)
                {
                    throw new InvalidOperationException("Minigun overlap query saturated its overflow buffer; increase preallocated capacity.");
                }
                if (range > depth)
                {
                    hitCount = Physics.BoxCastNonAlloc(center, extents, direction, hits, rotation, range - depth, layers, triggers);
                    if (hitCount == hits.Length)
                    {
                        Array.Clear(hits, 0, hitCount);
                        hits = _overflowHits;
                        hitCount = Physics.BoxCastNonAlloc(center, extents, direction, hits, rotation, range - depth, layers, triggers);
                    }
                    if (hitCount == hits.Length)
                    {
                        throw new InvalidOperationException("Minigun sweep query saturated its overflow buffer; increase preallocated capacity.");
                    }
                }
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < overlapCount; i++)
                {
                    if (collectEnemies) { AddContact(overlaps[i], 0f); }
                    else { nearest = 0f; }
                }
                for (int i = 0; i < hitCount; i++)
                {
                    float distance = Mathf.Clamp(hits[i].distance + depth, 0f, range);
                    if (collectEnemies) { AddContact(hits[i].collider, distance); }
                    else { nearest = Mathf.Min(nearest, distance); }
                }
                return nearest;
            }
            finally
            {
                Array.Clear(overlaps, 0, overlapCount);
                Array.Clear(hits, 0, hitCount);
            }
        }

        private void AddContact(Collider collider, float distance)
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) { return; }
            IDamageable target = collider.GetComponent<IDamageable>() ?? collider.GetComponentInParent<IDamageable>();
            if (target == null) { return; }
            Component owner = target as Component;
            if (owner == null) { throw new InvalidOperationException("Minigun damageable must have a component owner."); }
            IHealth health = target is IHealthy healthy ? healthy.Health : owner.GetComponent<IHealth>();
            if (health == null) { throw new InvalidOperationException("Minigun enemy damageable has no health capability."); }
            Contact contact = new Contact { Owner = owner, Target = target, Health = health, Distance = distance, InstanceId = owner.GetEntityId() };
            if (!IsAlive(contact)) { return; }
            for (int i = 0; i < _contactCount; i++)
            {
                if (_contacts[i].Owner == owner)
                {
                    if (distance < _contacts[i].Distance) { _contacts[i] = contact; }
                    return;
                }
            }
            _contacts[_contactCount++] = contact;
        }

        private static bool IsAlive(Contact contact)
        {
            return contact.Owner != null && contact.Owner.gameObject.activeInHierarchy
                && (!(contact.Owner is Behaviour behaviour) || behaviour.isActiveAndEnabled)
                && (!(contact.Health is UnityEngine.Object healthObject) || healthObject != null)
                && contact.Health.IsAlive();
        }

        private void SortContacts()
        {
            for (int i = 1; i < _contactCount; i++)
            {
                Contact contact = _contacts[i];
                int j = i - 1;
                while (j >= 0 && (_contacts[j].Distance > contact.Distance
                    || (_contacts[j].Distance == contact.Distance && _contacts[j].InstanceId > contact.InstanceId)))
                {
                    _contacts[j + 1] = _contacts[j];
                    j--;
                }
                _contacts[j + 1] = contact;
            }
        }
    }
}
