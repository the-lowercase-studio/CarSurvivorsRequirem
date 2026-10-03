using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BarrelExplosionConfig", menuName = "Scriptable Objects/Barrel Explosion Config")]
public class BarrelExplosionConfigSO : ScriptableObject
{
    [SerializeField] private float _explosionRadius = 2.5f;
    [SerializeField] private float _activationRange = 0.75f;

    public float ExplosionRadius { get { return _explosionRadius; } }
    public float ActivationRange { get { return _activationRange; } }

    public void ValidateConfiguration()
    {
        if (!IsPositiveFinite(_explosionRadius) || !IsPositiveFinite(_activationRange)
            || _activationRange > _explosionRadius)
        {
            throw new InvalidOperationException("Barrel explosion settings require finite positive ranges and activation range <= explosion radius.");
        }
    }

    private void OnValidate()
    {
        ValidateConfiguration();
    }

    private bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
