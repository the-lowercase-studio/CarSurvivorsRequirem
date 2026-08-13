using System.Text.RegularExpressions;

namespace Assets.Scripts.Extensions
{
    public static class StringExtensions
    {
        public static string PascalCaseToWords(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string result = Regex.Replace(input, "(?<!^)([A-Z])", " $1");
            return result.ToLower();
        }
    }
}
