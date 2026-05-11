using System;

namespace Assets.Scripts.UI.Skills
{
    public struct ClickableButtonData
    {
        public string Text { get; set; }
        public Action OnClick { get; set; }
    }
}
