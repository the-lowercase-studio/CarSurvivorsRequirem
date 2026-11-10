using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.HealthSystem
{
    [RequireComponent(typeof(RectTransform))]
    public class ArmorPlateItemView : MonoBehaviour
    {
        [SerializeField, Tooltip("Background image of the armor plate mini-bar.")]
        private Image _backgroundImage;

        [SerializeField, Tooltip("Filled foreground image representing current armor health.")]
        private Image _fillImage;

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        public void SetupLayout(Vector2 anchoredPosition, Vector2 size)
        {
            SetupLayout(anchoredPosition, size, new Vector2(1f, 0.5f));
        }

        public void SetupLayout(Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = pivot;
            _rectTransform.anchoredPosition = anchoredPosition;
            _rectTransform.sizeDelta = size;
            _rectTransform.localPosition = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);

            if (_fillImage != null)
            {
                _fillImage.fillOrigin = (int)Image.OriginHorizontal.Right;
            }
        }

        public void SetColor(Color color)
        {
            if (_fillImage != null)
            {
                _fillImage.color = color;
            }
        }

        public void SetFillAmount(float fillAmount)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = Mathf.Clamp01(fillAmount);
            }
        }

        public void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }
    }
}
