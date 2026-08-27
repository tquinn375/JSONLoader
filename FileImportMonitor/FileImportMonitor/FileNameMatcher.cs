using System.Text;
using System.Text.RegularExpressions;

namespace FileImportMonitor
{
    /// <summary>
    /// Matches file names against DOS-style wildcard masks (e.g. "INV*.TXT",
    /// "ORD???.CSV") such as those typically stored in
    /// LOCAL_IMPORTFILEVALIDMASKS. Matching is case-insensitive, matching
    /// Windows filesystem semantics.
    /// </summary>
    internal static class FileNameMatcher
    {
        public static bool IsMatch(string fileName, string mask)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(mask))
            {
                return false;
            }

            var pattern = WildcardToRegexPattern(mask.Trim());
            return Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase);
        }

        private static string WildcardToRegexPattern(string mask)
        {
            var sb = new StringBuilder("^");
            foreach (char c in mask)
            {
                switch (c)
                {
                    case '*':
                        sb.Append(".*");
                        break;
                    case '?':
                        sb.Append('.');
                        break;
                    default:
                        sb.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }
            sb.Append('$');
            return sb.ToString();
        }
    }
}
