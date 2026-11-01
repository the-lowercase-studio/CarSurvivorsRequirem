using Assets.Scripts.PowerUps.Constants;
using DG.Tweening;

namespace Assets.Scripts.PowerUps.Freeze
{
    public static class TimeFreezeExtensions
    {
        public static T AsFreezable<T>(this T tween) where T : Tween
        {
            tween.SetId(TimeFreezeConstants.FREEZABLE_TWEEN_ID);
            return tween;
        }
    }
}
