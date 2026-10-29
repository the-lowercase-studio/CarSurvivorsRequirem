using System;
using Assets.Scripts.Enemies.Bosses;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.HUD
{
    public class BossHealthBarView : MonoBehaviour
    {
        [SerializeField] private Image _mainBackground;
        [SerializeField] private Image _normalFillBackground;
        [SerializeField] private Image _enragedFillBackground;
        [SerializeField] private Image _normalFill;
        [SerializeField] private Image _enragedFill;
        [SerializeField] private RectTransform _enragedFillMotion;
        [Tooltip("Vertical punch displacement in Canvas units.")]
        [SerializeField] private Vector2 _enragePunchOffset = new Vector2(0f, 2f);
        [SerializeField] private float _enragePunchDuration = 0.28f;
        [SerializeField] private float _enragePunchInterval = 0.18f;
        [SerializeField] private int _enragePunchVibrato = 8;
        [SerializeField] private float _enragePunchElasticity = 0.35f;

        private Sequence _punchSequence;
        private Vector2 _motionBaseline;
        private bool _hasBaseline;

        private void OnDisable()
        {
            StopPunch();
        }

        private void OnDestroy()
        {
            StopPunch();
        }

        public void ValidateLayout(BossHealthBarKind kind)
        {
            string prefix = kind == BossHealthBarKind.Golem ? "GOLEM_BOSS_HP_BAR_" : "TOWER_HP_BAR_";
            ValidateImage(_mainBackground, prefix + "MAIN_BACKGROUND", false);
            ValidateImage(_normalFillBackground, prefix + "FILL_BACKGROUND_PART_NORMAL", false);
            ValidateImage(_enragedFillBackground, prefix + "FILL_BACKGROUND_PART_ENRAGED", false);
            ValidateImage(_normalFill, prefix + "FILL_PART_NORMAL", true);
            ValidateImage(_enragedFill, prefix + "FILL_PART_ENRAGED", true);
            if (_enragedFillMotion == null || _enragedFill.transform.parent != _enragedFillMotion
                || _enragePunchDuration <= 0f || _enragePunchInterval < 0f
                || _enragePunchVibrato < 1 || _enragePunchOffset.x != 0f)
            {
                throw new InvalidOperationException(name + ": invalid boss fill motion configuration.");
            }
            if (!_hasBaseline)
            {
                _motionBaseline = _enragedFillMotion.anchoredPosition;
                _hasBaseline = true;
            }
        }

        public void Render(float healthRatio, float threshold, bool isEnraged)
        {
            _normalFill.fillAmount = Mathf.Clamp01((healthRatio - threshold) / (1f - threshold));
            _enragedFill.fillAmount = isEnraged ? Mathf.Clamp01(healthRatio / threshold) : 1f;
        }

        public void SetEnraged(bool isEnraged)
        {
            if (!isEnraged)
            {
                StopPunch();
                return;
            }
            if (_punchSequence != null && _punchSequence.IsActive())
            {
                return;
            }
            _punchSequence = DOTween.Sequence();
            _punchSequence.Append(_enragedFillMotion.DOPunchAnchorPos(_enragePunchOffset,
                _enragePunchDuration, _enragePunchVibrato, _enragePunchElasticity));
            _punchSequence.AppendInterval(_enragePunchInterval);
            _punchSequence.SetLoops(-1, LoopType.Restart).SetUpdate(false);
        }

        public void ResetView()
        {
            StopPunch();
            _normalFill.fillAmount = 1f;
            _enragedFill.fillAmount = 1f;
            gameObject.SetActive(false);
        }

        private void StopPunch()
        {
            if (_punchSequence != null)
            {
                _punchSequence.Kill();
                _punchSequence = null;
            }
            if (_hasBaseline && _enragedFillMotion != null)
            {
                _enragedFillMotion.anchoredPosition = _motionBaseline;
            }
        }

        private void ValidateImage(Image image, string spriteName, bool isFill)
        {
            if (image == null || image.sprite == null || image.sprite.name != spriteName
                || image.raycastTarget || image.color != Color.white
                || image.type != (isFill ? Image.Type.Filled : Image.Type.Simple)
                || (isFill && (image.fillMethod != Image.FillMethod.Horizontal || image.fillOrigin != 0)))
            {
                throw new InvalidOperationException(name + ": expected authored boss image " + spriteName + ".");
            }
        }
    }
}
