using UnityEngine;

[CreateAssetMenu(fileName = "EnemyStatsSO", menuName = "Scriptable Objects/EnemyStatsSO")]
public class EnemyConfigSO : ScriptableObject
{
    public float MaxHealth;

    public float MovementSpeed;
    public float RotationSpeed;
    public float Acceleration;

    public float Damage;

    public byte DangerLevel;

    public float ExpForKill;

    public bool IsMovingByCrawling;

    [Tooltip("Enables the tier shiny shader effect via MaterialPropertyBlock on enemies using this configuration.")]
    [SerializeField] private bool _isShiny;

    public bool IsShiny => _isShiny;
}
