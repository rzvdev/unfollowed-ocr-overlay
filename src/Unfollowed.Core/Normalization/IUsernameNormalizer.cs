namespace Unfollowed.Core.Normalization
{
    public interface IUsernameNormalizer
    {
        string Normalize(string raw);
    }
}
