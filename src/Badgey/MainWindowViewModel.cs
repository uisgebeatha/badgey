using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Badgey.Hardware;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int GraphicRepeatGapColumns = 4;
    private const int TextRepeatGapColumns = BadgeProtocol.DisplayWidth;

    // Largest logical width whose packed bitmap still fits one protocol upload.
    private const int MaximumUploadBitmapWidth =
        ((BadgeProtocol.MaximumUploadSize - BadgeProtocol.HeaderSize)
            / BadgeBitmapPacker.RequiredHeight)
        * BadgeBitmapPacker.PixelsPerByte;

    private readonly BadgeController _controller;
    private readonly GraphicBitmapState _graphicState = new();
    private readonly AnimationBitmapState _animationState = new();
    private string _message = "Hello Badgey";
    private ContentOption _selectedContent;
    private ModeOption _selectedMode;
    private int _speed = 4;
    private MonochromeBitmap _textBitmap = CreateEmptyBitmap();
    private MonochromeBitmap _previewBitmap = CreateEmptyBitmap();
    private MonochromeAnimation? _previewAnimation;
    private string _selectedFileName = "No image selected";
    private string _selectedAnimationFileName = "No GIF selected";
    private string _statusText = "Ready.";
    private bool _isBusy;
    private bool _textPreviewIsValid;

    public MainWindowViewModel(BadgeController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        ContentTypes =
        [
            new ContentOption("Text", BadgeContentKind.Text),
            new ContentOption("Graphic", BadgeContentKind.Graphic),
            new ContentOption("Animation", BadgeContentKind.Animation),
        ];
        Modes =
        [
            new ModeOption("Scroll Left", BadgeDisplayMode.ScrollLeft),
            new ModeOption("Static Centered", BadgeDisplayMode.StaticCentered),
        ];
        Speeds = Enumerable.Range(1, 8).ToArray();
        _selectedContent = ContentTypes[0];
        _selectedMode = Modes[0];
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        UpdateTextBitmap();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ContentOption> ContentTypes { get; }

    public IReadOnlyList<ModeOption> Modes { get; }

    public IReadOnlyList<int> Speeds { get; }

    public AsyncRelayCommand SendCommand { get; }

    public ContentOption SelectedContent
    {
        get => _selectedContent;
        set
        {
            if (value is null || !SetField(ref _selectedContent, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsTextContent));
            OnPropertyChanged(nameof(IsGraphicContent));
            OnPropertyChanged(nameof(IsAnimationContent));
            OnPropertyChanged(nameof(IsTextOrGraphicContent));
            OnPropertyChanged(nameof(IsPreviewScrolling));
            OnPropertyChanged(nameof(PreviewRepeatGap));
            OnPropertyChanged(nameof(PreviewRepeatGapIsLit));
            UpdateActivePreview();
        }
    }

    public bool IsTextContent => SelectedContent.Value == BadgeContentKind.Text;

    public bool IsGraphicContent => SelectedContent.Value == BadgeContentKind.Graphic;

    public bool IsAnimationContent => SelectedContent.Value == BadgeContentKind.Animation;

    public bool IsTextOrGraphicContent => !IsAnimationContent;

    public bool IsGraphicInverted
    {
        get => _graphicState.IsInverted;
        set
        {
            if (_graphicState.Source is null || _graphicState.IsInverted == value)
            {
                return;
            }

            _graphicState.SetInverted(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewRepeatGapIsLit));

            if (IsGraphicContent)
            {
                UpdateActivePreview();
            }
        }
    }

    public int PreviewRepeatGap => IsGraphicContent
        ? GraphicRepeatGapColumns
        : TextRepeatGapColumns;

    public bool PreviewRepeatGapIsLit =>
        IsGraphicContent && _graphicState.IsInverted;

    public bool IsAnimationInverted
    {
        get => _animationState.IsInverted;
        set
        {
            if (_animationState.Source is null || _animationState.IsInverted == value)
            {
                return;
            }

            _animationState.SetInverted(value);
            OnPropertyChanged();

            if (IsAnimationContent)
            {
                UpdateActivePreview();
            }
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (!SetField(ref _message, value ?? string.Empty))
            {
                return;
            }

            UpdateTextBitmap();
        }
    }

    public ModeOption SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (value is null || !SetField(ref _selectedMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPreviewScrolling));

            if (IsGraphicContent)
            {
                UpdateActivePreview();
            }
        }
    }

    public bool IsPreviewScrolling =>
        !IsAnimationContent && SelectedMode.Value == BadgeDisplayMode.ScrollLeft;

    public int Speed
    {
        get => _speed;
        set => SetField(ref _speed, value);
    }

    public MonochromeBitmap PreviewBitmap
    {
        get => _previewBitmap;
        private set => SetField(ref _previewBitmap, value);
    }

    public MonochromeAnimation? PreviewAnimation
    {
        get => _previewAnimation;
        private set => SetField(ref _previewAnimation, value);
    }

    public string SelectedFileName
    {
        get => _selectedFileName;
        private set => SetField(ref _selectedFileName, value);
    }

    public string SelectedAnimationFileName
    {
        get => _selectedAnimationFileName;
        private set => SetField(ref _selectedAnimationFileName, value);
    }

    public string AnimationFrameCountText => _animationState.Source is { } animation
        ? animation.FrameCount == 1
            ? "1 frame"
            : $"{animation.FrameCount} frames"
        : "No frames";

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanLoadGraphic));
                OnPropertyChanged(nameof(CanInvertGraphic));
                OnPropertyChanged(nameof(CanLoadAnimation));
                OnPropertyChanged(nameof(CanInvertAnimation));
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanLoadGraphic => !IsBusy;

    public bool CanInvertGraphic => !IsBusy && _graphicState.Source is not null;

    public bool CanLoadAnimation => !IsBusy;

    public bool CanInvertAnimation => !IsBusy && _animationState.Source is not null;

    public void LoadGraphic(string path)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var bitmap = _controller.RenderImage(path);
            _graphicState.Load(bitmap);
            OnPropertyChanged(nameof(IsGraphicInverted));
            OnPropertyChanged(nameof(PreviewRepeatGapIsLit));
            OnPropertyChanged(nameof(CanInvertGraphic));
            SelectedFileName = Path.GetFileName(path);

            if (IsGraphicContent)
            {
                UpdateActivePreview();
            }
        }
        catch (Exception)
        {
            if (IsGraphicContent)
            {
                StatusText = "Could not load this image. Choose a PNG, JPEG, or BMP file.";
            }
        }

        SendCommand.RaiseCanExecuteChanged();
    }

    public void LoadAnimation(string path)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var animation = _controller.RenderAnimation(path);
            _animationState.Load(animation);
            OnPropertyChanged(nameof(IsAnimationInverted));
            OnPropertyChanged(nameof(AnimationFrameCountText));
            OnPropertyChanged(nameof(CanInvertAnimation));
            SelectedAnimationFileName = Path.GetFileName(path);

            if (IsAnimationContent)
            {
                UpdateActivePreview();
            }
        }
        catch (Exception)
        {
            if (IsAnimationContent)
            {
                StatusText = "Could not load this animation. Choose an animated GIF file.";
            }
        }

        SendCommand.RaiseCanExecuteChanged();
    }

    private bool CanSend()
    {
        if (IsBusy)
        {
            return false;
        }

        return SelectedContent.Value switch
        {
            BadgeContentKind.Text =>
                _textPreviewIsValid && !string.IsNullOrWhiteSpace(Message),
            BadgeContentKind.Graphic => _graphicState.Source is { Width: > 0 },
            BadgeContentKind.Animation => _animationState.Source is { FrameCount: > 0 },
            _ => false,
        };
    }

    private async Task SendAsync()
    {
        var contentKind = SelectedContent.Value;
        var bitmap = contentKind switch
        {
            BadgeContentKind.Text => _textBitmap,
            BadgeContentKind.Graphic => _graphicState.Current,
            _ => null,
        };
        var animation = contentKind == BadgeContentKind.Animation
            ? _animationState.Current
            : null;

        if (bitmap is null && animation is null)
        {
            return;
        }

        var mode = SelectedMode.Value;
        var repeatGapIsLit = contentKind == BadgeContentKind.Graphic
            && _graphicState.IsInverted;

        IsBusy = true;
        StatusText = "Connecting to badge…";

        try
        {
            if (contentKind == BadgeContentKind.Graphic
                && mode == BadgeDisplayMode.ScrollLeft)
            {
                // The firmware adds its own terminal display-width gap. Filling
                // the documented upload capacity with four-column-spaced copies
                // makes that unavoidable terminal gap occur much less often.
                bitmap = MonochromeBitmapRepeater.RepeatToMaximumWidth(
                    bitmap!,
                    GraphicRepeatGapColumns,
                    MaximumUploadBitmapWidth,
                    gapValue: repeatGapIsLit);
            }

            if (contentKind == BadgeContentKind.Animation)
            {
                await _controller.SendAnimationAsync(animation!, Speed);
            }
            else
            {
                await _controller.SendAsync(bitmap!, mode, Speed);
            }

            StatusText = contentKind switch
            {
                BadgeContentKind.Text => "Message sent successfully.",
                BadgeContentKind.Graphic => "Graphic sent successfully.",
                BadgeContentKind.Animation => "Animation sent successfully.",
                _ => "Content sent successfully.",
            };
        }
        catch (BadgeDeviceNotFoundException)
        {
            StatusText = "No compatible badge found.";
        }
        catch (BadgeDeviceOpenException)
        {
            StatusText = "Could not open the badge. Reconnect it and try again.";
        }
        catch (BadgeDeviceWriteException)
        {
            StatusText = contentKind switch
            {
                BadgeContentKind.Text => "Could not write the message to the badge.",
                BadgeContentKind.Graphic => "Could not write the graphic to the badge.",
                BadgeContentKind.Animation => "Could not write the animation to the badge.",
                _ => "Could not write to the badge.",
            };
        }
        catch (ArgumentException)
        {
            StatusText = contentKind switch
            {
                BadgeContentKind.Text => "The message is too long for the badge.",
                BadgeContentKind.Graphic => "The graphic is too wide for the badge.",
                BadgeContentKind.Animation => "The animation has too many frames for the badge.",
                _ => "The content is too large for the badge.",
            };
        }
        catch (Exception)
        {
            StatusText = "An unexpected error occurred while sending.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateTextBitmap()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            _textBitmap = CreateEmptyBitmap();
            _textPreviewIsValid = false;
        }
        else
        {
            try
            {
                _textBitmap = _controller.Render(Message);
                _textPreviewIsValid = true;
            }
            catch (Exception)
            {
                _textBitmap = CreateEmptyBitmap();
                _textPreviewIsValid = false;
            }
        }

        if (IsTextContent)
        {
            UpdateActivePreview();
        }
        else
        {
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    private void UpdateActivePreview()
    {
        if (IsAnimationContent)
        {
            PreviewBitmap = CreateEmptyBitmap();
            PreviewAnimation = _animationState.Current;

            if (!IsBusy)
            {
                StatusText = PreviewAnimation is null
                    ? "Load an animated GIF to send."
                    : "Ready.";
            }
        }
        else if (IsTextContent)
        {
            PreviewAnimation = null;
            PreviewBitmap = _textBitmap;

            if (!IsBusy)
            {
                StatusText = string.IsNullOrWhiteSpace(Message)
                    ? "Enter a message to send."
                    : _textPreviewIsValid
                        ? "Ready."
                        : "Could not render this message.";
            }
        }
        else if (_graphicState.Current is not { } graphicBitmap)
        {
            PreviewAnimation = null;
            PreviewBitmap = CreateEmptyBitmap();

            if (!IsBusy)
            {
                StatusText = "Load an image to send.";
            }
        }
        else
        {
            PreviewAnimation = null;
            PreviewBitmap = SelectedMode.Value == BadgeDisplayMode.StaticCentered
                ? MonochromeBitmapViewport.Create(
                    graphicBitmap,
                    BadgeProtocol.DisplayWidth,
                    centerNarrow: true)
                : graphicBitmap;

            if (!IsBusy)
            {
                StatusText = "Ready.";
            }
        }

        SendCommand.RaiseCanExecuteChanged();
    }

    private static MonochromeBitmap CreateEmptyBitmap() =>
        new(0, BadgeBitmapPacker.RequiredHeight);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum BadgeContentKind
{
    Text,
    Graphic,
    Animation,
}

public sealed record ContentOption(string Label, BadgeContentKind Value);

public sealed record ModeOption(string Label, BadgeDisplayMode Value);
