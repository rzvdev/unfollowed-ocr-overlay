namespace Unfollowed.Core.Normalization;

public sealed class UsernameNormalizationOptions
{
    public bool ToLower = true;
    public bool StripLeadingAt = true;
    public int MinLength = 1;
    public int MaxLength = 30;
    public string AllowedChars = "abcdefghijklmnopqrstuvwxyz0123456789._";
}
