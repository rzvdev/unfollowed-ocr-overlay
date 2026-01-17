using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Overlay.Win32.Infrastructure;

namespace Unfollowed.Overlay.Win32
{
    public sealed class Win32OverlayRenderer : IOverlayRenderer
    {
        private WpfUiThreadHost? _ui;
        private OverlayWindow? _window;
        private OverlayOptions? _options;

        public Win32OverlayRenderer()
        {
            _ui = new WpfUiThreadHost();
        }

        public async Task InitializeAsync(RoiSelection roi, OverlayOptions options, CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));
            _options = options;

            await ui.InvokeAsync(() =>
            {
                _window = new OverlayWindow(options.ClickThrough)
                {
                    Left = roi.X,
                    Top = roi.Y,
                    Width = roi.Width,
                    Height = roi.Height,
                    Topmost = options.AlwaysOnTop
                };

                _window.Show();
            });
        }

        public async Task RenderAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));
            var options = _options ?? throw new InvalidOperationException("Overlay renderer not initialized.");

            await ui.InvokeAsync(() =>
            {
                if (_window is null) return;

                var canvas = (Canvas)_window.Content;
                canvas.Children.Clear();

                foreach (var h in highlights)
                {
                    var local = new Rect(
                        h.ScreenRect.X - (float)_window.Left,
                        h.ScreenRect.Y - (float)_window.Top,
                        h.ScreenRect.W,
                        h.ScreenRect.H
                    );

                    var rect = new Rectangle
                    {
                        Width = (int)Math.Max(1, local.Width),
                        Height = (int)Math.Max(1, local.Height),
                        StrokeThickness = 2,
                        Stroke = System.Windows.Media.Brushes.LimeGreen,
                        Fill = System.Windows.Media.Brushes.Transparent,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(rect, local.X);
                    Canvas.SetTop(rect, local.Y);
                    canvas.Children.Add(rect);

                    if (!options.ShowBadgeText)
                        continue;

                    var label = new TextBlock
                    {
                        Text = h.UsernameNormalized,
                        FontSize = 12,
                        Foreground = System.Windows.Media.Brushes.LimeGreen,
                        Background = System.Windows.Media.Brushes.Black,
                        Opacity = 0.8,
                        Padding = new Thickness(4, 2, 4, 2),
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(label, local.X);
                    Canvas.SetTop(label, Math.Max(0, local.Y - 18));
                    canvas.Children.Add(label);
                }
            });
        }

        public async Task ClearAsync(CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));

            await ui.InvokeAsync(() =>
            {
                if (_window is null) return;
                ((Canvas)_window.Content).Children.Clear();
            });
        }

        public async ValueTask DisposeAsync()
        {
            // If Dispose is called multiple times, make it idempotent.
            var ui = _ui;
            if (ui is null)
                return;

            try
            {
                await ui.InvokeAsync(() =>
                {
                    if (_window is not null)
                    {
                        try
                        {
                            _window.Close();
                        }
                        catch
                        {
                            // ignore shutdown exceptions
                        }
                        finally
                        {
                            _window = null;
                        }
                    }
                });
            }
            finally
            {
                ui.Dispose();
                _ui = null;
            }
        }
    }
}
