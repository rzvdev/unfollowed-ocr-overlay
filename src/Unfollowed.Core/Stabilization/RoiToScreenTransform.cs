using Unfollowed.Core.Models;

namespace Unfollowed.Core.Stabilization;

public sealed record RoiToScreenTransform(
  float RoiX, float RoiY, float RoiW, float RoiH,
  float FrameW, float FrameH
)
{
    public RectF ToScreen(RectF roiRect)
    {
        var scaleX = RoiW / FrameW;
        var scaleY = RoiH / FrameH;

        return new RectF(
            RoiX + roiRect.X * scaleX,
            RoiY + roiRect.Y * scaleY,
            roiRect.W * scaleX,
            roiRect.H * scaleY
        );
    }
}
