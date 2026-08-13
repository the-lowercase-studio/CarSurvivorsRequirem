using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Settings.Resolution
{
    public static class ScreenSerializableResolutionHelper
    {
        private static IReadOnlyList<SerializableResolution> _availableResolutions;

        public static IReadOnlyList<SerializableResolution> GetAvailableResolutions()
        {
            if (_availableResolutions is null)
            {
                var resolutions = Screen.resolutions;

                var currentRefreshRatio = Screen.currentResolution.refreshRateRatio;

                for (int i = resolutions.Length - 1; i >= 0; i--)
                {
                    resolutions[i].refreshRateRatio = currentRefreshRatio;
                }

                var list = new List<SerializableResolution>();
                for (int i = resolutions.Length - 1; i >= 0; i--)
                {
                    var sr = SerializableResolution.FromUnityResolution(resolutions[i]);
                    if (!list.Contains(sr))
                    {
                        list.Add(sr);
                    }
                }
                _availableResolutions = list;
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
