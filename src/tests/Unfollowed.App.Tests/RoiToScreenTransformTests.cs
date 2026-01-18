using Unfollowed.Core.Models;
using Unfollowed.Core.Stabilization;

namespace Unfollowed.App.Tests;

public sealed class RoiToScreenTransformTests
{
    [Fact]
    public void ToScreen_OffsetsAndScalesRectangle()
    {
        var transform = new RoiToScreenTransform(100, 50, 200, 100, 400, 200);

        var screen = transform.ToScreen(new RectF(40, 20, 60, 30));

        Assert.Equal(new RectF(120, 60, 30, 15), screen);
    }

    [Fact]
    public void ToScreen_UsesNonZeroOriginAndScale()
    {
        var transform = new RoiToScreenTransform(10, 5, 90, 45, 180, 90);

        var screen = transform.ToScreen(new RectF(18, 9, 36, 18));

        Assert.Equal(new RectF(19, 9.5f, 18, 9), screen);
    }
}
