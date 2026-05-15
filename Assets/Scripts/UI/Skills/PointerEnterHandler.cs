using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Skills
{
    public class PointerEnterHandler : MonoBehaviour, IPointerEnterHandler
    {
        public Action OnPointerEnterAction;

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterAction?.Invoke();
        }
    }
}
