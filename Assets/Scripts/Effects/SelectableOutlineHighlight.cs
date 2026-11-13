using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Effects
{
    /// <summary>
    /// Applies explicit outline and fill visual highlights on selection and pointer hover.
    /// </summary>
    public class SelectableOutlineHighlight : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Selectable _selectable;
        [SerializeField] private Outline _outline;
        [SerializeField] private UISpriteSilhouetteOutline _silhouetteOutline;
        [SerializeField] private Graphic _fillGraphic;
        [SerializeField] private Color _normalOutlineColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] private Vector2 _normalOutlineDistance = new Vector2(3f, -3f);
        [SerializeField] private Color _highlightedOutlineColor = new Color(1f, 0.88f, 0.28f, 1f);
        [SerializeField] private Vector2 _highlightedOutlineDistance = new Vector2(6f, -6f);
        [SerializeField] private Color _normalFillColor = new Color(0.878f, 0f, 0f, 1f);
        [SerializeField] private Color _highlightedFillColor = new Color(1f, 0.62f, 0.12f, 1f);

        private bool _isSelected;
        private bool _isHovered;

        private void OnEnable()
        {
            _isSelected = EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject;
            _isHovered = false;
            ApplyVisualState();
        }

        private void OnDisable()
        {
            _isSelected = false;
            _isHovered = false;
            ApplyVisualState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            ApplyVisualState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            ApplyVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            bool isHighlighted = (_isSelected || _isHovered)
                && (_selectable == null || _selectable.IsInteractable());

            if (_outline != null)
            {
                _outline.effectColor = isHighlighted ? _highlightedOutlineColor : _normalOutlineColor;
                _outline.effectDistance = isHighlighted ? _highlightedOutlineDistance : _normalOutlineDistance;
                _outline.enabled = true;
            }

            if (_silhouetteOutline != null)
            {
                _silhouetteOutline.EffectColor = isHighlighted ? _highlightedOutlineColor : _normalOutlineColor;
                _silhouetteOutline.EffectWidth = Mathf.Abs(
                    isHighlighted ? _highlightedOutlineDistance.x : _normalOutlineDistance.x);
                _silhouetteOutline.enabled = true;
            }

            if (_fillGraphic != null)
            {
                _fillGraphic.color = isHighlighted ? _highlightedFillColor : _normalFillColor;
            }
        }
    }
}
