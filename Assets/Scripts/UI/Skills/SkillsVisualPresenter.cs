using Assets.ScriptableObjects.Skills;
using UnityEngine;

namespace Assets.Scripts.UI.Skills
{
    public interface ISkillsVisualPresenter
    {
        void ShowSkillVisual(SkillInfoSO skillInfoSO, int slotIndex = 0);
        void ShowSkillVisualBasedOnSkillInfo(SkillInfoSO skillInfoSO);
        void HideAll();
    }

    public class SkillsVisualPresenter : MonoBehaviour, ISkillsVisualPresenter
    {
        [SerializeField] private GameObject[] _skillsVisuals;
        [SerializeField] private GameObject[] _secondarySkillsVisuals;

        public void ShowSkillVisual(SkillInfoSO skillInfoSO, int slotIndex = 0)
        {
            if (skillInfoSO == null)
            {
                return;
            }

            GameObject[] targetVisuals = (slotIndex == 1 && _secondarySkillsVisuals != null && _secondarySkillsVisuals.Length > 0)
                ? _secondarySkillsVisuals
                : _skillsVisuals;

            GameObject skillVisual = null;
            if (targetVisuals != null)
            {
                for (int i = 0; i < targetVisuals.Length; i++)
                {
                    if (targetVisuals[i] != null && targetVisuals[i].name == skillInfoSO.Name)
                    {
                        skillVisual = targetVisuals[i];
                        break;
                    }
                }
            }

            if (skillVisual == null)
            {
                Debug.LogWarning($"Skill visual for {skillInfoSO.Name} at slot {slotIndex} was not found.", this);
                return;
            }

            skillVisual.SetActive(true);
        }

        public void ShowSkillVisualBasedOnSkillInfo(SkillInfoSO skillInfoSO)
        {
            ShowSkillVisual(skillInfoSO, 0);
        }

        public void HideAll()
        {
            if (_skillsVisuals != null)
            {
                for (int i = 0; i < _skillsVisuals.Length; i++)
                {
                    if (_skillsVisuals[i] != null)
                    {
                        _skillsVisuals[i].SetActive(false);
                    }
                }
            }

            if (_secondarySkillsVisuals != null)
            {
                for (int i = 0; i < _secondarySkillsVisuals.Length; i++)
                {
                    if (_secondarySkillsVisuals[i] != null)
                    {
                        _secondarySkillsVisuals[i].SetActive(false);
                    }
                }
            }
        }
    }
}
