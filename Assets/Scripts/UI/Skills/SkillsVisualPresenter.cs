using Assets.ScriptableObjects.Skills;
using UnityEngine;

namespace Assets.Scripts.UI.Skills
{
    public enum SkillVisualContext
    {
        StatUpgrade = 0,
        NewSkillUnlocked = 1
    }

    public interface ISkillsVisualPresenter
    {
        void SetContext(SkillVisualContext context);
        void ShowSkillVisual(SkillInfoSO skillInfoSO, int slotIndex = 0);
        void ShowSkillVisualBasedOnSkillInfo(SkillInfoSO skillInfoSO);
        void HideAll();
    }

    public class SkillsVisualPresenter : MonoBehaviour, ISkillsVisualPresenter
    {
        [Header("Cameras")]
        [Tooltip("Primary preview camera rendering slot 0.")]
        [SerializeField] private Camera _primaryCamera;
        [Tooltip("Secondary preview camera rendering slot 1 for dual skill choices.")]
        [SerializeField] private Camera _secondaryCamera;

        [Header("Context Background Colors")]
        [Tooltip("Camera background solid color used during skill stat upgrade flow.")]
        [SerializeField] private Color _statUpgradeBackgroundColor = Color.white;
        [Tooltip("Camera background solid color used during new skill unlock choice flow.")]
        [SerializeField] private Color _newSkillUnlockedBackgroundColor = Color.white;

        [Header("Visuals")]
        [SerializeField] private GameObject[] _skillsVisuals;
        [SerializeField] private GameObject[] _secondarySkillsVisuals;

        public void SetContext(SkillVisualContext context)
        {
            Color targetColor = GetContextColor(context);
            SetCameraBackgroundColor(_primaryCamera, targetColor);
            SetCameraBackgroundColor(_secondaryCamera, targetColor);
        }

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

        private Color GetContextColor(SkillVisualContext context)
        {
            switch (context)
            {
                case SkillVisualContext.StatUpgrade:
                {
                    return _statUpgradeBackgroundColor;
                }
                case SkillVisualContext.NewSkillUnlocked:
                {
                    return _newSkillUnlockedBackgroundColor;
                }
                default:
                {
                    return Color.white;
                }
            }
        }

        private void SetCameraBackgroundColor(Camera targetCamera, Color color)
        {
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = color;
        }
    }
}
