using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Badgey.Rendering;

namespace Badgey;

/// <summary>
/// Reusable WPF surface for an <see cref="EditableMonochromeBitmap"/>. The
/// bitmap model owns all mutations; this control only translates pointer input.
/// </summary>
public sealed class PixelEditorControl : FrameworkElement
{
    private static readonly Brush UnlitBrush = CreateBrush(28, 34, 30);
    private static readonly Brush LitBrush = CreateBrush(76, 238, 112);
    private static readonly Brush EditorBackgroundBrush = CreateBrush(18, 22, 19);
    private static readonly Pen GridPen = CreatePen(57, 67, 60);

    public static readonly DependencyProperty EditorProperty =
        DependencyProperty.Register(
            nameof(Editor),
            typeof(EditableMonochromeBitmap),
            typeof(PixelEditorControl),
            new FrameworkPropertyMetadata(null, OnEditorChanged));

    public static readonly DependencyProperty ToolProperty =
        DependencyProperty.Register(
            nameof(Tool),
            typeof(PixelPaintTool),
            typeof(PixelEditorControl),
            new FrameworkPropertyMetadata(PixelPaintTool.Draw));

    private (int X, int Y)? _lastPaintedCell;

    public PixelEditorControl()
    {
        Focusable = true;
        SnapsToDevicePixels = true;
    }

    public EditableMonochromeBitmap? Editor
    {
        get => (EditableMonochromeBitmap?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public PixelPaintTool Tool
    {
        get => (PixelPaintTool)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(
            double.IsInfinity(availableSize.Width) ? 528 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 132 : availableSize.Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(EditorBackgroundBrush, null, new Rect(RenderSize));

        if (Editor is not { } editor)
        {
            return;
        }

        var layout = GetCellLayout();

        for (var y = 0; y < editor.Height; y++)
        {
            for (var x = 0; x < editor.Width; x++)
            {
                var cell = new Rect(
                    layout.Left + (x * layout.CellWidth),
                    layout.Top + (y * layout.CellHeight),
                    layout.CellWidth,
                    layout.CellHeight);
                drawingContext.DrawRectangle(
                    editor[x, y] ? LitBrush : UnlitBrush,
                    GridPen,
                    cell);
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        _lastPaintedCell = null;
        PaintAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            PaintAt(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (IsMouseCaptured)
        {
            PaintAt(e.GetPosition(this));
            ReleaseMouseCapture();
        }

        _lastPaintedCell = null;
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _lastPaintedCell = null;
    }

    private void PaintAt(Point point)
    {
        if (Editor is not { } editor || !TryGetCell(point, out var cell))
        {
            return;
        }

        var isLit = Tool == PixelPaintTool.Draw;

        if (_lastPaintedCell is { } previous)
        {
            editor.PaintLine(previous.X, previous.Y, cell.X, cell.Y, isLit);
        }
        else
        {
            editor.SetPixel(cell.X, cell.Y, isLit);
        }

        _lastPaintedCell = cell;
    }

    private bool TryGetCell(Point point, out (int X, int Y) cell)
    {
        var layout = GetCellLayout();
        var relativeX = point.X - layout.Left;
        var relativeY = point.Y - layout.Top;

        if (relativeX < 0
            || relativeY < 0
            || relativeX >= layout.CellWidth * EditableMonochromeBitmap.CanvasWidth
            || relativeY >= layout.CellHeight * EditableMonochromeBitmap.CanvasHeight)
        {
            cell = default;
            return false;
        }

        cell = (
            (int)(relativeX / layout.CellWidth),
            (int)(relativeY / layout.CellHeight));
        return true;
    }

    private CellLayout GetCellLayout()
    {
        var cellWidth = ActualWidth / EditableMonochromeBitmap.CanvasWidth;
        var cellHeight = ActualHeight / EditableMonochromeBitmap.CanvasHeight;
        return new CellLayout(cellWidth, cellHeight, Left: 0, Top: 0);
    }

    private static void OnEditorChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (PixelEditorControl)dependencyObject;

        if (e.OldValue is EditableMonochromeBitmap oldEditor)
        {
            oldEditor.Changed -= control.OnEditorPixelsChanged;
        }

        if (e.NewValue is EditableMonochromeBitmap newEditor)
        {
            newEditor.Changed += control.OnEditorPixelsChanged;
        }

        control._lastPaintedCell = null;
        control.InvalidateVisual();
    }

    private void OnEditorPixelsChanged(object? sender, EventArgs e) => InvalidateVisual();

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(byte red, byte green, byte blue)
    {
        var pen = new Pen(CreateBrush(red, green, blue), 1);
        pen.Freeze();
        return pen;
    }

    private readonly record struct CellLayout(
        double CellWidth,
        double CellHeight,
        double Left,
        double Top);
}

public enum PixelPaintTool
{
    Draw,
    Erase,
}
