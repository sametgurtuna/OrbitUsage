using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Orbit.Models;
using Orbit.Services;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Orbit.Helpers;

public class EdgeDragController
{
    private readonly Border _target;
    private readonly Window _window;
    private readonly SettingsService? _settingsService;
    private readonly Action<double?, double?> _onPreviewOffset;
    private readonly Func<NotchLayout> _layoutProvider;

    private bool _isDragging;
    private Point _dragStartScreenPos;
    private double _dragStartOffsetX;
    private double _dragStartOffsetY;

    public EdgeDragController(
        Border target,
        Window window,
        SettingsService? settingsService,
        Func<NotchLayout> layoutProvider,
        Action<double?, double?> onPreviewOffset)
    {
        _target = target;
        _window = window;
        _settingsService = settingsService;
        _layoutProvider = layoutProvider;
        _onPreviewOffset = onPreviewOffset;

        _target.PreviewMouseLeftButtonDown += OnMouseDown;
        _target.PreviewMouseMove += OnMouseMove;
        _target.PreviewMouseLeftButtonUp += OnMouseUp;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _isDragging = true;
            _dragStartScreenPos = _window.PointToScreen(e.GetPosition(_window));
            _dragStartOffsetX = _settingsService?.Current.NotchOffsetX ?? 0;
            _dragStartOffsetY = _settingsService?.Current.NotchOffsetY ?? 0;
            _target.CaptureMouse();
            _window.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var currentScreenPos = _window.PointToScreen(e.GetPosition(_window));
            double deltaX = currentScreenPos.X - _dragStartScreenPos.X;
            double deltaY = currentScreenPos.Y - _dragStartScreenPos.Y;

            if (_layoutProvider() == NotchLayout.RightCenter)
            {
                double newOffsetY = Math.Clamp(_dragStartOffsetY + deltaY, -400, 400);
                _onPreviewOffset(null, newOffsetY);
            }
            else
            {
                double newOffsetX = Math.Clamp(_dragStartOffsetX + deltaX, -600, 600);
                _onPreviewOffset(newOffsetX, null);
            }
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _window.Cursor = Cursors.SizeAll;
        }
        else if (_window.Cursor == Cursors.SizeAll)
        {
            _window.Cursor = Cursors.Arrow;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _target.ReleaseMouseCapture();
            _window.Cursor = Cursors.Arrow;

            if (_settingsService != null)
            {
                var settings = _settingsService.Current;
                var currentScreenPos = _window.PointToScreen(e.GetPosition(_window));
                double deltaX = currentScreenPos.X - _dragStartScreenPos.X;
                double deltaY = currentScreenPos.Y - _dragStartScreenPos.Y;

                if (_layoutProvider() == NotchLayout.RightCenter)
                {
                    settings.NotchOffsetY = Math.Clamp(_dragStartOffsetY + deltaY, -400, 400);
                }
                else
                {
                    settings.NotchOffsetX = Math.Clamp(_dragStartOffsetX + deltaX, -600, 600);
                }
                _settingsService.Save(settings);
            }
            e.Handled = true;
        }
    }
}
