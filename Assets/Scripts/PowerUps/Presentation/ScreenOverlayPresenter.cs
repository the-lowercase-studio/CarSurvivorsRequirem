using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.PowerUps.Presentation
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenOverlayPresenter : MonoBehaviour
    {
        [SerializeField] private Image _overlayImage;

        private CanvasGroup _canvasGroup;

        public CanvasGroup CanvasGroup => _canvasGroup;
        public Image OverlayImage => _overlayImage;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void SetAlpha(float alpha)
        {
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void ResetOverlay()
        {
            _canvasGroup.alpha = 0f;
        }
    }
}
