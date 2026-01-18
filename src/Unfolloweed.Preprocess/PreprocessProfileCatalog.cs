namespace Unfollowed.Preprocess;

public static class PreprocessProfileCatalog
{
    public static PreprocessOptions Resolve(
        string? selectedProfile,
        IReadOnlyDictionary<string, PreprocessOptions>? profiles,
        PreprocessOptions fallback)
    {
        if (string.IsNullOrWhiteSpace(selectedProfile) || profiles is null || profiles.Count == 0)
        {
            return fallback;
        }

        if (profiles.TryGetValue(selectedProfile, out var options))
        {
            return options;
        }

        foreach (var (name, profile) in profiles)
        {
            if (string.Equals(name, selectedProfile, StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        return fallback;
    }
}
