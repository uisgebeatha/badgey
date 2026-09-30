using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

public sealed class LedMatrixPreview : FrameworkElement
{
    public static readonly DependencyProperty BitmapProperty = DependencyProperty.Register(
        nameof(Bitmap),
        typeof(MonochromeBitmap),
        typeof(LedMatrixPreview),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnAnimationInputChanged));

    public static readonly DependencyProperty IsScrollingProperty = DependencyProperty.Register(
        nameof(IsScrolling),
        typeof(bool),
        typeof(LedMatrixPreview),
        new FrameworkPropertyMetadata(false, OnAnimationInputChanged));

    public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(
        nameof(Speed),
        typeof(int),
        typeof(LedMatrixPreview),
        new FrameworkPropertyMetadata(4, OnAnimationInputChanged),
        value => value is >= 1 and <= 8);

    private static readonly Brush BackgroundBrush = CreateBrush(0x10, 0x18, 0x12);
    private static readonly Brush UnlitBrush = CreateBrush(0x20, 0x30, 0x23);
    private static readonly Brush LitBrush = CreateBrush(0x69, 0xF0, 0x73);

    private readonly DispatcherTimer _scrollTimer;
    private int _scrollOffset;

    public LedMatrixPreview()
    {
        _scrollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = GetScrollInterval(speed: 4),
        };
        _scrollTimer.Tick += OnScrollTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public MonochromeBitmap? Bitmap
    {
        get => (MonochromeBitmap?)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public bool IsScrolling
    {
        get => (bool)GetValue(IsScrollingProperty);
        set => SetValue(IsScrollingProperty, value);
    }

    public int Speed
    {
        get => (int)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRoundedRectangle(
            BackgroundBrush,
            null,
            new Rect(0, 0, ActualWidth, ActualHeight),
            radiusX: 6,
            radiusY: 6);

        var cellSize = Math.Min(
            ActualWidth / BadgeProtocol.DisplayWidth,
            ActualHeight / BadgeProtocol.DisplayHeight);

        if (cellSize <= 0)
        {
            return;
        }

        var matrixWidth = cellSize * BadgeProtocol.DisplayWidth;
        var matrixHeight = cellSize * BadgeProtocol.DisplayHeight;
        var offsetX = (ActualWidth - matrixWidth) / 2;
        var offsetY = (ActualHeight - matrixHeight) / 2;
        var inset = Math.Max(0.7, cellSize * 0.12);
        var pixelRadius = Math.Max(0.5, cellSize * 0.12);

        for (var y = 0; y < BadgeProtocol.DisplayHeight; y++)
        {
            for (var x = 0; x < BadgeProtocol.DisplayWidth; x++)
            {
                var sourceX = GetSourceColumn(x);
                var isLit = Bitmap is not null
                    && sourceX >= 0
                    && sourceX < Bitmap.Width
                    && y < Bitmap.Height
                    && Bitmap[sourceX, y];
                var pixelRect = new Rect(
                    offsetX + (x * cellSize) + inset,
                    offsetY + (y * cellSize) + inset,
                    Math.Max(0, cellSize - (2 * inset)),
                    Math.Max(0, cellSize - (2 * inset)));

                drawingContext.DrawRoundedRectangle(
                    isLit ? LitBrush : UnlitBrush,
                    null,
                    pixelRect,
                    pixelRadius,
                    pixelRadius);
            }
        }
    }

    private int GetSourceColumn(int viewportX)
    {
        if (!IsScrolling || Bitmap is null)
        {
            return viewportX;
        }

        var cycleWidth = Bitmap.Width + BadgeProtocol.DisplayWidth;
        return (viewportX + _scrollOffset) % cycleWidth;
    }

    private void RestartAnimation()
    {
        _scrollTimer.Stop();
        _scrollOffset = 0;
        _scrollTimer.Interval = GetScrollInterval(Speed);

        if (IsLoaded && IsScrolling && Bitmap is { Width: > 0 })
        {
            _scrollTimer.Start();
        }

        InvalidateVisual();
    }

    private void OnScrollTick(object? sender, EventArgs e)
    {
        if (Bitmap is not { Width: > 0 })
        {
            RestartAnimation();
            return;
        }

        var cycleWidth = Bitmap.Width + BadgeProtocol.DisplayWidth;
        _scrollOffset = (_scrollOffset + 1) % cycleWidth;
        InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RestartAnimation();

    private void OnUnloaded(object sender, RoutedEventArgs e) => _scrollTimer.Stop();

    private static void OnAnimationInputChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e) =>
        ((LedMatrixPreview)dependencyObject).RestartAnimation();

    private static TimeSpan GetScrollInterval(int speed) =>
        TimeSpan.FromMilliseconds(320 - ((speed - 1) * 35));

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
