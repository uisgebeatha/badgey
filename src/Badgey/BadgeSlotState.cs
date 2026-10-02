using System.ComponentModel;
using System.Runtime.CompilerServices;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

/// <summary>
/// Retains the independent editor state for one physical badge message slot.
/// Rendering remains the responsibility of the view model/controller; this type
/// keeps the resulting logical content while the user moves between slots.
/// </summary>
public sealed class BadgeSlotState : INotifyPropertyChanged
{
    public const string NoGraphicFileName = "No image selected";
    public const string EditedGraphicFileName = "Edited 44 x 11 graphic";
    public const string NoAnimationFileName = "No GIF selected";

    private BadgeContentKind _contentKind = BadgeContentKind.Text;
    private string _message = string.Empty;
    private MonochromeBitmap _textBitmap = CreateEmptyBitmap();
    private bool _textPreviewIsValid;
    private GraphicBitmapState _graphicState = new();
    private AnimationBitmapState _animationState = new();
    private string _graphicFileName = NoGraphicFileName;
    private string _animationFileName = NoAnimationFileName;
    private BadgeDisplayMode _mode = BadgeDisplayMode.ScrollLeft;
    private int _speed = 4;
    private bool _isPopulated;

    public BadgeSlotState(int slotNumber)
    {
        if (slotNumber is < 1 or > BadgeProtocol.MaximumSlotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotNumber),
                slotNumber,
                $"Slot number must be from 1 to {BadgeProtocol.MaximumSlotCount}.");
        }

        SlotNumber = slotNumber;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SlotNumber { get; }

    public BadgeContentKind ContentKind
    {
        get => _contentKind;
        set
        {
            if (SetField(ref _contentKind, value))
            {
                RefreshPopulation();
            }
        }
    }

    public string Message
    {
        get => _message;
        internal set => SetField(ref _message, value ?? string.Empty);
    }

    public MonochromeBitmap TextBitmap
    {
        get => _textBitmap;
        internal set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetField(ref _textBitmap, value);
        }
    }

    public bool TextPreviewIsValid
    {
        get => _textPreviewIsValid;
        internal set => SetField(ref _textPreviewIsValid, value);
    }

    public GraphicBitmapState GraphicState
    {
        get => _graphicState;
        private set => SetField(ref _graphicState, value);
    }

    public AnimationBitmapState AnimationState
    {
        get => _animationState;
        private set => SetField(ref _animationState, value);
    }

    public string GraphicFileName
    {
        get => _graphicFileName;
        internal set => SetField(ref _graphicFileName, value ?? NoGraphicFileName);
    }

    public string AnimationFileName
    {
        get => _animationFileName;
        internal set => SetField(ref _animationFileName, value ?? NoAnimationFileName);
    }

    public BadgeDisplayMode Mode
    {
        get => _mode;
        set
        {
            if (value is not BadgeDisplayMode.ScrollLeft
                and not BadgeDisplayMode.StaticCentered)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A slot editor mode must be scroll-left or static-centred.");
            }

            SetField(ref _mode, value);
        }
    }

    public int Speed
    {
        get => _speed;
        set
        {
            if (value is < 1 or > 8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Speed must be from 1 to 8.");
            }

            SetField(ref _speed, value);
        }
    }

    public bool IsPopulated
    {
        get => _isPopulated;
        private set
        {
            if (SetField(ref _isPopulated, value))
            {
                OnPropertyChanged(nameof(SelectorLabel));
            }
        }
    }

    public string SelectorLabel => IsPopulated
        ? $"{SlotNumber} •"
        : SlotNumber.ToString();

    /// <summary>
    /// Atomically stores the latest result of rendering this slot's text.
    /// </summary>
    internal void SetTextRenderResult(
        string message,
        MonochromeBitmap bitmap,
        bool isValid)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        Message = message;
        TextBitmap = bitmap;
        TextPreviewIsValid = isValid;
        RefreshPopulation();
    }

    internal void LoadGraphic(MonochromeBitmap source, string fileName)
    {
        ArgumentNullException.ThrowIfNull(source);

        GraphicState.Load(source);
        GraphicFileName = fileName;
        OnPropertyChanged(nameof(GraphicState));
        RefreshPopulation();
    }

    internal void SetGraphicInverted(bool isInverted)
    {
        GraphicState.SetInverted(isInverted);
        OnPropertyChanged(nameof(GraphicState));
    }

    internal void LoadAnimation(MonochromeAnimation source, string fileName)
    {
        ArgumentNullException.ThrowIfNull(source);

        AnimationState.Load(source);
        AnimationFileName = fileName;
        OnPropertyChanged(nameof(AnimationState));
        RefreshPopulation();
    }

    internal void SetAnimationInverted(bool isInverted)
    {
        AnimationState.SetInverted(isInverted);
        OnPropertyChanged(nameof(AnimationState));
    }

    /// <summary>
    /// Re-evaluates whether the active content kind has sendable content.
    /// Call this after updating a rendered source or its validity.
    /// </summary>
    public void RefreshPopulation()
    {
        IsPopulated = ContentKind switch
        {
            BadgeContentKind.Text =>
                TextPreviewIsValid && !string.IsNullOrWhiteSpace(Message),
            BadgeContentKind.Graphic => GraphicState.Source is { Width: > 0 },
            BadgeContentKind.Animation => AnimationState.Source is { FrameCount: > 0 },
            _ => false,
        };
    }

    /// <summary>
    /// Clears every draft and restores the defaults for this slot.
    /// </summary>
    public void Clear()
    {
        ContentKind = BadgeContentKind.Text;
        Message = string.Empty;
        TextBitmap = CreateEmptyBitmap();
        TextPreviewIsValid = false;
        GraphicState = new GraphicBitmapState();
        AnimationState = new AnimationBitmapState();
        GraphicFileName = NoGraphicFileName;
        AnimationFileName = NoAnimationFileName;
        Mode = BadgeDisplayMode.ScrollLeft;
        Speed = 4;
        RefreshPopulation();
    }

    private static MonochromeBitmap CreateEmptyBitmap() =>
        new(0, BadgeBitmapPacker.RequiredHeight);

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
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
