using Assets.Scripts.Utils.Constants;

namespace Assets.Scripts.Utils
{
    public static class TimeConversionUtility
    {
        public static string FormatSecondsToTimeString(uint totalSeconds)
        {
            uint hours = totalSeconds / TimeConstants.SECONDS_PER_HOUR;
            uint minutes = (totalSeconds % TimeConstants.SECONDS_PER_HOUR) / TimeConstants.SECONDS_PER_MINUTE;
            uint seconds = totalSeconds % TimeConstants.SECONDS_PER_MINUTE;

            if (hours > 0)
            {
                return $"{hours}h {minutes}m {seconds}s";
            }

            if (minutes > 0)
            {
                return $"{minutes}m {seconds}s";
            }

            return $"{seconds}s";
        }
    }
}
