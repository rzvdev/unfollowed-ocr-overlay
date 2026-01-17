namespace Unfollowed.Overlay;

public sealed record OverlayOptions(
   bool AlwaysOnTop = true,
   bool ClickThrough = true,
   bool ShowBadgeText = true
);
