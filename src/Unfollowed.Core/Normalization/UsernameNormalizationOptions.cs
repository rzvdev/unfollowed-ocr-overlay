namespace Unfollowed.Core.Normalization
{
    public sealed record UsernameNormalizationOptions(
        bool ToLower = true,
        bool StripLeadingAt = true,
        int MinLenght = 1,
        int MaxLenght = 30,
        string AllowedChars = "abcdefghijklmnopqrstuvwxyz0123456789._"
    );
}
