namespace Unfollowed.Ocr;

public sealed record OcrOptions(
    string LanguageTag = "en",
    float MinTokenConfidence = 0.0f,
    string? CharacterWhitelist = "abcdefghijklmnopqrstuvwxyz0123456789._@"
);
