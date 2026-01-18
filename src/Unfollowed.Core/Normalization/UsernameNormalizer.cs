using System.Text;

namespace Unfollowed.Core.Normalization
{
    /// <summary>
    /// Normalizes raw OCR text into a username-style token.
    /// </summary>
    public sealed class UsernameNormalizer : IUsernameNormalizer
    {
        private readonly UsernameNormalizationOptions _options;

        public UsernameNormalizer(UsernameNormalizationOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Applies trimming, casing, and character filtering rules.
        /// </summary>
        public string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var s = raw.Trim();

            if (_options.StripLeadingAt && s.StartsWith("@", StringComparison.Ordinal))
                s = s[1..];

            if (_options.ToLower)
                s = s.ToLowerInvariant();

            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (_options.AllowedChars.IndexOf(ch) >= 0)
                    sb.Append(ch);
            }

            var normalized = sb.ToString();

            if (normalized.Length < _options.MinLength)
                return string.Empty;

            if (normalized.Length > _options.MaxLength)
                normalized = normalized[.._options.MaxLength];

            return normalized;
        }
    }
}
