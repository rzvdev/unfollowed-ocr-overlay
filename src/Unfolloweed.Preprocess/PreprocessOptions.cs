namespace Unfollowed.Preprocess;

public sealed record PreprocessOptions(
    PreprocessProfile Profile = PreprocessProfile.Default,
    float Contrast = 1.0f,
    float Sharpen = 0.0f
);

