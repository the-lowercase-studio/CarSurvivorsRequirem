using Assets.ScriptableObjects.Skills;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.UI.Skills
{
    public interface ISkillsVisualPresenter
    {
        void ShowSkillVisualBasedOnSkillInfo(SkillInfoSO skillInfoSO);
        void HideAll();
    }

    public class SkillsVisualPresenter : MonoBehaviour, ISkillsVisualPresenter
    {
        [SerializeField] private GameObject[] _skillsVisuals;

        public void ShowSkillVisualBasedOnSkillInfo(SkillInfoSO skillInfoSO)
        {
            GameObject skillVisual = _skillsVisuals.FirstOrDefault(s => s.name == skillInfoSO.Name);
            if (skillVisual == null)
            {
                Debug.LogWarning($"Skill visual for {skillInfoSO.Name} was not found.", this);
                return;
            }

            skillVisual.SetActive(true);
        }

        public void HideAll()
        {
            foreach (GameObject skillVisual in _skillsVisuals)
            {
                skillVisual.SetActive(false);
            }
        }
    }
}
