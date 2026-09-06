using UnityEngine;

namespace Assets.ScriptableObjects.Skills
{
    [CreateAssetMenu(fileName = "SkillInfoSO", menuName = "Scriptable Objects/SkillInfoSO")]
    public class SkillInfoSO : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField, TextArea(5, 10)] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
    }
}
