using System.Runtime.InteropServices;
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
        private const uint MONITOR_DEFAULTTONEAREST = 2;
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

        public async Task InitializeAsync(RoiSelection roi, OverlayOptions options, CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));
            _options = options;
            _roi = roi;
            (_dipScaleX, _dipScaleY) = GetDipScale(roi);

            await ui.InvokeAsync(() =>
            {
                _window = new OverlayWindow(options.ClickThrough)
                {
                    Left = roi.X * _dipScaleX,
                    Top = roi.Y * _dipScaleY,
                    Width = roi.Width * _dipScaleX,
                    Height = roi.Height * _dipScaleY,
                    Topmost = options.AlwaysOnTop
                };

                _window.Show();
            });
        }

        public async Task RenderAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct)
        {
            var ui = _ui ?? throw new ObjectDisposedException(nameof(Win32OverlayRenderer));
            var options = _options ?? throw new InvalidOperationException("Overlay renderer not initialized.");
            var roi = _roi ?? throw new InvalidOperationException("Overlay renderer not initialized.");

            await ui.InvokeAsync(() =>
            {
                if (_window is null) return;

                var canvas = (Canvas)_window.Content;
                canvas.Children.Clear();

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

        private static (double scaleX, double scaleY) GetDipScale(RoiSelection roi)
        {
            var point = new POINT { X = roi.X, Y = roi.Y };
            var monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return (1.0, 1.0);
            }

            if (GetDpiForMonitor(monitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) != 0)
            {
                return (1.0, 1.0);
            }

            var scaleX = 96.0 / dpiX;
            var scaleY = 96.0 / dpiY;
            return (scaleX, scaleY);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(
            IntPtr hmonitor,
            MonitorDpiType dpiType,
            out uint dpiX,
            out uint dpiY);
    }
}
