using System;
using System.Globalization;
using Assets.Scripts.Skills;
using Assets.Scripts.Stats;
using Assets.Scripts.UI.Constants;

namespace Assets.Scripts.UI.Skills
{
    public static class SkillStatValueFormatter
    {
        public static string Format(IUpgradeableStat stat)
        {
            if (stat == null)
            {
                throw new InvalidOperationException("A displayed skill stat must be assigned.");
            }

            double value = stat.CurrentValue;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException("A displayed skill stat must have a finite current value.");
            }

            string format = stat.IsIntegerUpgradeRange
                ? SkillsStatsConstants.INTEGER_FORMAT
                : SkillsStatsConstants.FLOAT_FORMAT;
            string number = value.ToString(format, CultureInfo.InvariantCulture);
            if (number == "-0")
            {
                number = "0";
            }

            string suffix = stat.Unit.ToDisplayString();
            if (stat.Unit == StatsUnits.None)
            {
                return number;
            }

            return number + (stat.Unit == StatsUnits.Percentage ? "" : " ") + suffix;
        }
    }
}
