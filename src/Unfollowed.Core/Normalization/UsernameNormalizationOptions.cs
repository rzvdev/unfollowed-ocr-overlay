namespace Unfollowed.Core.Normalization;

public sealed class UsernameNormalizationOptions
{
    public bool ToLower = true;
    public bool StripLeadingAt = true;
    public int MinLenght = 1;
    public int MaxLenght = 30;
    public string AllowedChars = "abcdefghijklmnopqrstuvwxyz0123456789._";
}
