using Assets.ScriptableObjects.Skills;
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
            GameObject skillVisual = null;
            if (_skillsVisuals != null)
            {
                for (int i = 0; i < _skillsVisuals.Length; i++)
                {
                    if (_skillsVisuals[i] != null && _skillsVisuals[i].name == skillInfoSO.Name)
                    {
                        skillVisual = _skillsVisuals[i];
                        break;
                    }
                }
            }

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
