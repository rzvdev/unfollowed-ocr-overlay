using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Overlay.Win32.Infrastructure;

namespace Unfollowed.Overlay.Win32
{
    /// <summary>
    /// Renders highlight overlays on a dedicated WPF UI thread hosted by
    /// <see cref="WpfUiThreadHost"/>. All window operations are dispatched through that UI thread.
    /// </summary>
    public sealed class Win32OverlayRenderer : IOverlayRenderer
    {
        private WpfUiThreadHost? _ui;
        private OverlayWindow? _window;
        private OverlayOptions? _options;
        private RoiSelection? _roi;
        private double _dipScaleX = 1.0;
        private double _dipScaleY = 1.0;

        public Win32OverlayRenderer()
        {
            _ui = new WpfUiThreadHost();
        }

        /// <summary>
        /// Initializes the overlay window on the WPF UI thread and computes DIP scaling for the
        /// monitor that contains the ROI. ROI coordinates are in screen pixels and are scaled to
        /// DIPs when positioning the overlay window.
        /// </summary>
        public async Task InitializeAsync(RoiSelection roi, OverlayOptions options, CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));
            _options = options;
            _roi = roi;

            await ui.InvokeAsync(() =>
            {
                _window = new OverlayWindow(options.ClickThrough)
                {
                    Topmost = options.AlwaysOnTop
                };

                _window.Show();

                var dpi = VisualTreeHelper.GetDpi(_window);
                _dipScaleX = dpi.DpiScaleX == 0 ? 1.0 : 1.0 / dpi.DpiScaleX;
                _dipScaleY = dpi.DpiScaleY == 0 ? 1.0 : 1.0 / dpi.DpiScaleY;
                UpdateWindowBounds(roi);
            });
        }

        /// <summary>
        /// Renders highlight rectangles on the UI thread by mapping each highlight
        /// <see cref="Highlight.ScreenRect"/> from screen pixels into overlay-local DIPs using the
        /// ROI origin and computed DIP scale.
        /// </summary>
        public async Task RenderAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));
            var options = _options ?? throw new InvalidOperationException("Overlay renderer not initialized.");
            var roi = _roi ?? throw new InvalidOperationException("Overlay renderer not initialized.");
            var (strokeBrush, textBrush) = GetThemeBrushes(options.Theme);

            await ui.InvokeAsync(() =>
            {
                if (_window is null) return;

                var canvas = (Canvas)_window.Content;
                canvas.Children.Clear();
                var bounds = new Rect(0, 0, _window.Width, _window.Height);
                canvas.Clip = new RectangleGeometry(bounds);

                if (options.ShowRoiOutline)
                {
                    var outline = new Rectangle
                    {
                        Width = Math.Max(1, _window.Width - 2),
                        Height = Math.Max(1, _window.Height - 2),
                        StrokeThickness = 2,
                        Stroke = strokeBrush,
                        Fill = System.Windows.Media.Brushes.Transparent,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(outline, 1);
                    Canvas.SetTop(outline, 1);
                    canvas.Children.Add(outline);
                }

                if (options.ShowRoiOutline)
                {
                    var outline = new Rectangle
                    {
                        Width = Math.Max(1, _window.Width - 2),
                        Height = Math.Max(1, _window.Height - 2),
                        StrokeThickness = 2,
                        Stroke = strokeBrush,
                        Fill = System.Windows.Media.Brushes.Transparent,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(outline, 1);
                    Canvas.SetTop(outline, 1);
                    canvas.Children.Add(outline);
                }

                foreach (var h in highlights)
                {
                    var localX = (h.ScreenRect.X - roi.X) * _dipScaleX;
                    var localY = (h.ScreenRect.Y - roi.Y) * _dipScaleY;
                    var localW = h.ScreenRect.W * _dipScaleX;
                    var localH = h.ScreenRect.H * _dipScaleY;

                    var local = new Rect(
                        localX,
                        localY,
                        localW,
                        localH
                    );

                    if (!bounds.IntersectsWith(local))
                    {
                        continue;
                    }

                    local.Intersect(bounds);

                    var rect = new Rectangle
                    {
                        Width = (int)Math.Max(1, local.Width),
                        Height = (int)Math.Max(1, local.Height),
                        StrokeThickness = 2,
                        Stroke = strokeBrush,
                        Fill = System.Windows.Media.Brushes.Transparent,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(rect, local.X);
                    Canvas.SetTop(rect, local.Y);
                    canvas.Children.Add(rect);

                    if (options.ShowBadgeText)
                    {
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

                    if (!options.ShowOcrText || string.IsNullOrWhiteSpace(h.OcrText))
                        continue;

                    var ocrLabel = new TextBlock
                    {
                        Text = h.UsernameNormalized,
                        FontSize = 12,
                        Foreground = textBrush,
                        Background = System.Windows.Media.Brushes.Black,
                        Opacity = 0.75,
                        Padding = new Thickness(4, 2, 4, 2),
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(ocrLabel, local.X);
                    Canvas.SetTop(ocrLabel, local.Y + local.Height + 2);
                    canvas.Children.Add(ocrLabel);
                }
            });
        }

        /// <summary>
        /// Clears overlay visuals on the UI thread.
        /// </summary>
        public async Task ClearAsync(CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));

            await ui.InvokeAsync(() =>
            {
                if (_window is null) return;
                ((Canvas)_window.Content).Children.Clear();
            });
        }

        /// <summary>
        /// Disposes the overlay window and UI thread host. Must be called from any thread; work is
        /// marshaled onto the WPF UI thread.
        /// </summary>
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

        private void UpdateWindowBounds(RoiSelection roi)
        {
            if (_window is null)
            {
                return;
            }

            _window.Left = roi.X * _dipScaleX;
            _window.Top = roi.Y * _dipScaleY;
            _window.Width = roi.Width * _dipScaleX;
            _window.Height = roi.Height * _dipScaleY;
        }

        private static (System.Windows.Media.Brush Stroke, System.Windows.Media.Brush Text) GetThemeBrushes(OverlayTheme theme)
            => theme switch
            {
                OverlayTheme.Amber => (System.Windows.Media.Brushes.Gold, System.Windows.Media.Brushes.Gold),
                OverlayTheme.Cyan => (System.Windows.Media.Brushes.Cyan, System.Windows.Media.Brushes.Cyan),
                _ => (System.Windows.Media.Brushes.LimeGreen, System.Windows.Media.Brushes.LimeGreen)
            };
    }
}
