using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Settings.Resolution
{
    public static class ScreenSerializableResolutionHelper
    {
        private static IEnumerable<SerializableResolution> _availableResolutions;

        public static IEnumerable<SerializableResolution> GetAvailableResolutions()
        {
            if (_availableResolutions is null)
            {
                var resolutions = Screen.resolutions;

                var currentRefreshRatio = Screen.currentResolution.refreshRateRatio;

                for (int i = resolutions.Length - 1; i >= 0; i--)
                {
                    resolutions[i].refreshRateRatio = currentRefreshRatio;
                }

                _availableResolutions = resolutions
                    .Distinct()
                    .Select(r => SerializableResolution.FromUnityResolution(r))
                    .Reverse();
            }

            return _availableResolutions;
        }

        public static void SetResolution(SerializableResolution resolution, FullScreenMode fullScreen)
        {
            if (resolution.Equals(default(SerializableResolution)))
            {
                throw new ArgumentException("Invalid resolution provided.");
            }

            Screen.SetResolution(resolution.Width, resolution.Height, fullScreen, new RefreshRate()
            {
                numerator = resolution.RefreshRateNumerator,
                denominator = resolution.RefreshRateDenominator
            });
        }
    }
}
