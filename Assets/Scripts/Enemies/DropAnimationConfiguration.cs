using UnityEngine;

namespace Assets.Scripts.Enemies
{
    [CreateAssetMenu(fileName = "DropAnimationConfiguration", menuName = "Scriptable Objects/Drops/DropAnimationConfiguration")]
    public class DropAnimationConfiguration : ScriptableObject
    {
        [SerializeField] private float _scatterRadius = 2f;
        [SerializeField] private float _scaleDuration = 0.4f;
        [SerializeField] private float _scatterDuration = 0.5f;
        [SerializeField] private float _minJumpPower = 1.2f;
        [SerializeField] private float _maxJumpPower = 1.8f;
        [SerializeField] private float _minDurationMultiplier = 0.9f;
        [SerializeField] private float _maxDurationMultiplier = 1.1f;

        public float ScatterRadius => _scatterRadius;
        public float ScaleDuration => _scaleDuration;
        public float ScatterDuration => _scatterDuration;
        public float MinJumpPower => _minJumpPower;
        public float MaxJumpPower => _maxJumpPower;
        public float MinDurationMultiplier => _minDurationMultiplier;
        public float MaxDurationMultiplier => _maxDurationMultiplier;
    }
}
