using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Badgey.Hardware;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int SlotCount = BadgeProtocol.MaximumSlotCount;
    private const int GraphicRepeatGapColumns =
        BadgeMultiSlotUploadComposer.GraphicRepeatGapColumns;
    private const int TextRepeatGapColumns = BadgeProtocol.DisplayWidth;

    private readonly BadgeController _controller;
    private BadgeSlotState _selectedSlot;
    private PreparedBadgeUpload? _preparedUpload;
    private MonochromeBitmap _previewBitmap = CreateEmptyBitmap();
    private MonochromeAnimation? _previewAnimation;
    private EditableMonochromeBitmap? _activePixelEditor;
    private PixelPaintTool _pixelEditorTool = PixelPaintTool.Draw;
    private string _uploadSummaryText = "0 of 8 slots • Nothing to send";
    private string _statusText = "Ready.";
    private bool _isBusy;

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
        Slots = Enumerable.Range(1, SlotCount)
            .Select(slotNumber => new BadgeSlotState(slotNumber))
            .ToArray();
        _selectedSlot = Slots[0];
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);

        RenderTextIntoSlot(_selectedSlot, "Hello Badgey");
        UpdateActivePreview();
        RefreshPreparedUpload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<BadgeSlotState> Slots { get; }

    public IReadOnlyList<ContentOption> ContentTypes { get; }

    public IReadOnlyList<ModeOption> Modes { get; }

    public IReadOnlyList<int> Speeds { get; }

    public AsyncRelayCommand SendCommand { get; }

    public BadgeSlotState SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            if (IsGraphicEditing && !ReferenceEquals(value, _selectedSlot))
            {
                StatusText = "Apply or cancel the graphic edit before changing slots.";
                return;
            }

            if (value is null
                || !Slots.Contains(value)
                || !SetField(ref _selectedSlot, value))
            {
                return;
            }

            RaiseSelectedSlotPropertiesChanged();
            UpdateActivePreview();
        }
    }

    public ContentOption SelectedContent
    {
        get => ContentTypes.First(option => option.Value == SelectedSlot.ContentKind);
        set
        {
            if (IsGraphicEditing
                || value is null
                || SelectedSlot.ContentKind == value.Value)
            {
                return;
            }

            SelectedSlot.ContentKind = value.Value;
            RaiseContentPropertiesChanged();
            OnPropertyChanged(nameof(CanClearSlot));
            UpdateActivePreview();
            RefreshPreparedUpload();
        }
    }

    public bool IsTextContent => SelectedSlot.ContentKind == BadgeContentKind.Text;

    public bool IsGraphicContent => SelectedSlot.ContentKind == BadgeContentKind.Graphic;

    public bool IsAnimationContent => SelectedSlot.ContentKind == BadgeContentKind.Animation;

    public bool IsTextOrGraphicContent => !IsAnimationContent;

    public EditableMonochromeBitmap? ActivePixelEditor
    {
        get => _activePixelEditor;
        private set
        {
            if (!SetField(ref _activePixelEditor, value))
            {
                return;
            }

            RaisePixelEditingPropertiesChanged();
        }
    }

    public bool IsGraphicEditing => ActivePixelEditor is not null;

    public bool IsNotGraphicEditing => !IsGraphicEditing;

    public PixelPaintTool PixelEditorTool
    {
        get => _pixelEditorTool;
        set
        {
            if (SetField(ref _pixelEditorTool, value))
            {
                OnPropertyChanged(nameof(IsDrawToolSelected));
                OnPropertyChanged(nameof(IsEraseToolSelected));
            }
        }
    }

    public bool IsDrawToolSelected
    {
        get => PixelEditorTool == PixelPaintTool.Draw;
        set
        {
            if (value)
            {
                PixelEditorTool = PixelPaintTool.Draw;
            }
        }
    }

    public bool IsEraseToolSelected
    {
        get => PixelEditorTool == PixelPaintTool.Erase;
        set
        {
            if (value)
            {
                PixelEditorTool = PixelPaintTool.Erase;
            }
        }
    }

    public bool IsGraphicInverted
    {
        get => SelectedSlot.GraphicState.IsInverted;
        set
        {
            if (IsGraphicEditing
                || SelectedSlot.GraphicState.Source is null
                || SelectedSlot.GraphicState.IsInverted == value)
            {
                return;
            }

            SelectedSlot.SetGraphicInverted(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewRepeatGapIsLit));
            OnPropertyChanged(nameof(CanClearSlot));

            if (IsGraphicContent)
            {
                UpdateActivePreview();
            }

            RefreshPreparedUpload();
        }
    }

    public int PreviewRepeatGap => IsGraphicContent
        ? GraphicRepeatGapColumns
        : TextRepeatGapColumns;

    public bool PreviewRepeatGapIsLit =>
        IsGraphicContent && SelectedSlot.GraphicState.IsInverted;

    public bool IsAnimationInverted
    {
        get => SelectedSlot.AnimationState.IsInverted;
        set
        {
            if (SelectedSlot.AnimationState.Source is null
                || SelectedSlot.AnimationState.IsInverted == value)
            {
                return;
            }

            SelectedSlot.SetAnimationInverted(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClearSlot));

            if (IsAnimationContent)
            {
                UpdateActivePreview();
            }

            RefreshPreparedUpload();
        }
    }

    public string Message
    {
        get => SelectedSlot.Message;
        set
        {
            var message = value ?? string.Empty;

            if (SelectedSlot.Message == message)
            {
                return;
            }

            RenderTextIntoSlot(SelectedSlot, message);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClearSlot));

            if (IsTextContent)
            {
                UpdateActivePreview();
            }

            RefreshPreparedUpload();
        }
    }

    public ModeOption SelectedMode
    {
        get => Modes.First(option => option.Value == SelectedSlot.Mode);
        set
        {
            if (IsGraphicEditing
                || value is null
                || SelectedSlot.Mode == value.Value)
            {
                return;
            }

            SelectedSlot.Mode = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPreviewScrolling));
            OnPropertyChanged(nameof(CanClearSlot));

            if (IsGraphicContent)
            {
                UpdateActivePreview();
            }

            RefreshPreparedUpload();
        }
    }

    public bool IsPreviewScrolling =>
        !IsAnimationContent && SelectedSlot.Mode == BadgeDisplayMode.ScrollLeft;

    public int Speed
    {
        get => SelectedSlot.Speed;
        set
        {
            if (IsGraphicEditing || SelectedSlot.Speed == value)
            {
                return;
            }

            SelectedSlot.Speed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClearSlot));
            RefreshPreparedUpload();
        }
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

    public string SelectedFileName => SelectedSlot.GraphicFileName;

    public string SelectedAnimationFileName => SelectedSlot.AnimationFileName;

    public string AnimationFrameCountText => SelectedSlot.AnimationState.Source is { } animation
        ? animation.FrameCount == 1
            ? "1 frame"
            : $"{animation.FrameCount} frames"
        : "No frames";

    public string UploadSummaryText
    {
        get => _uploadSummaryText;
        private set => SetField(ref _uploadSummaryText, value);
    }

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
            if (!SetField(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanLoadGraphic));
            OnPropertyChanged(nameof(CanInvertGraphic));
            OnPropertyChanged(nameof(CanEditGraphic));
            OnPropertyChanged(nameof(CanLoadAnimation));
            OnPropertyChanged(nameof(CanInvertAnimation));
            OnPropertyChanged(nameof(CanClearSlot));
            OnPropertyChanged(nameof(IsSlotSelectionEnabled));
            OnPropertyChanged(nameof(IsContentSelectionEnabled));
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsSlotSelectionEnabled => !IsBusy && !IsGraphicEditing;

    public bool IsContentSelectionEnabled => !IsBusy && !IsGraphicEditing;

    public bool CanLoadGraphic => !IsBusy && !IsGraphicEditing;

    public bool CanInvertGraphic =>
        !IsBusy
        && !IsGraphicEditing
        && SelectedSlot.GraphicState.Source is not null;

    public bool CanEditGraphic =>
        !IsBusy
        && !IsGraphicEditing
        && IsGraphicContent
        && SelectedSlot.GraphicState.Current is not null;

    public bool CanLoadAnimation => !IsBusy && !IsGraphicEditing;

    public bool CanInvertAnimation =>
        !IsBusy
        && !IsGraphicEditing
        && SelectedSlot.AnimationState.Source is not null;

    public bool CanClearSlot =>
        !IsBusy && !IsGraphicEditing && SelectedSlotHasStoredState();

    public void LoadGraphic(string path)
    {
        if (IsBusy || IsGraphicEditing)
        {
            return;
        }

        try
        {
            var bitmap = _controller.RenderImage(path);
            SelectedSlot.LoadGraphic(bitmap, Path.GetFileName(path));
            OnPropertyChanged(nameof(IsGraphicInverted));
            OnPropertyChanged(nameof(SelectedFileName));
            OnPropertyChanged(nameof(CanInvertGraphic));
            OnPropertyChanged(nameof(CanEditGraphic));
            OnPropertyChanged(nameof(CanClearSlot));

            if (IsGraphicContent)
            {
                UpdateActivePreview();
            }

            RefreshPreparedUpload();
        }
        catch (Exception)
        {
            if (IsGraphicContent)
            {
                StatusText = "Could not load this image. Choose a PNG, JPEG, or BMP file.";
            }
        }
    }

    public void LoadAnimation(string path)
    {
        if (IsBusy || IsGraphicEditing)
        {
            return;
        }

        try
        {
            var animation = _controller.RenderAnimation(path);
            SelectedSlot.LoadAnimation(animation, Path.GetFileName(path));
            OnPropertyChanged(nameof(IsAnimationInverted));
            OnPropertyChanged(nameof(SelectedAnimationFileName));
            OnPropertyChanged(nameof(AnimationFrameCountText));
            OnPropertyChanged(nameof(CanInvertAnimation));
            OnPropertyChanged(nameof(CanClearSlot));

            if (IsAnimationContent)
            {
                UpdateActivePreview();
            }

            RefreshPreparedUpload();
        }
        catch (Exception)
        {
            if (IsAnimationContent)
            {
                StatusText = "Could not load this animation. Choose an animated GIF file.";
            }
        }
    }

    public void ClearSelectedSlot()
    {
        if (IsBusy || IsGraphicEditing)
        {
            return;
        }

        SelectedSlot.Clear();
        RaiseSelectedSlotPropertiesChanged();
        UpdateActivePreview();
        RefreshPreparedUpload();
    }

    public void BeginGraphicEdit()
    {
        if (!CanEditGraphic || SelectedSlot.GraphicState.Current is not { } bitmap)
        {
            return;
        }

        ActivePixelEditor = EditableMonochromeBitmap.FromGraphicViewport(bitmap);
        PixelEditorTool = PixelPaintTool.Draw;
        StatusText = SelectedSlot.GraphicState.IsInverted
            ? "Editing the displayed inverted graphic. Apply adopts these pixels with Invert off."
            : "Editing a 44 x 11 copy. Apply to replace the graphic, or Cancel to discard it.";
    }

    public void ClearPixelEditor() => ActivePixelEditor?.ClearAll();

    public void FillPixelEditor() => ActivePixelEditor?.FillAll();

    public void InvertPixelEditor() => ActivePixelEditor?.Invert();

    public void ApplyGraphicEdit()
    {
        if (ActivePixelEditor is not { } editor)
        {
            return;
        }

        var editedBitmap = editor.CreateBitmap();
        SelectedSlot.LoadGraphic(
            editedBitmap,
            BadgeSlotState.EditedGraphicFileName);
        ActivePixelEditor = null;
        OnPropertyChanged(nameof(IsGraphicInverted));
        OnPropertyChanged(nameof(SelectedFileName));
        OnPropertyChanged(nameof(CanInvertGraphic));
        OnPropertyChanged(nameof(CanEditGraphic));
        OnPropertyChanged(nameof(CanClearSlot));
        OnPropertyChanged(nameof(PreviewRepeatGapIsLit));
        UpdateActivePreview();
        RefreshPreparedUpload();
    }

    public void CancelGraphicEdit()
    {
        if (!IsGraphicEditing)
        {
            return;
        }

        ActivePixelEditor = null;
        UpdateActivePreview();
    }

    private bool CanSend() =>
        !IsBusy
        && !IsGraphicEditing
        && _preparedUpload is { PopulatedSlotCount: > 0 };

    private async Task SendAsync()
    {
        var preparedUpload = _preparedUpload;

        if (preparedUpload is null)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Connecting to badge…";

        try
        {
            await _controller.SendPreparedUploadAsync(preparedUpload);
            StatusText = preparedUpload.PopulatedSlotCount == 1
                ? "1 slot sent successfully."
                : $"{preparedUpload.PopulatedSlotCount} slots sent successfully.";
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
            StatusText = "Could not write the configured slots to the badge.";
        }
        catch (ArgumentException)
        {
            StatusText = "The configured slots are too large for the badge.";
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

    private void RenderTextIntoSlot(BadgeSlotState slot, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            slot.SetTextRenderResult(message, CreateEmptyBitmap(), isValid: false);
            return;
        }

        try
        {
            slot.SetTextRenderResult(
                message,
                _controller.Render(message),
                isValid: true);
        }
        catch (Exception)
        {
            slot.SetTextRenderResult(message, CreateEmptyBitmap(), isValid: false);
        }
    }

    private void UpdateActivePreview()
    {
        // Clear both sources first so switching slots always restarts whichever
        // preview timer becomes active, even when two slots share equal settings.
        PreviewAnimation = null;
        PreviewBitmap = CreateEmptyBitmap();

        if (IsAnimationContent)
        {
            PreviewAnimation = SelectedSlot.AnimationState.Current;

            if (!IsBusy)
            {
                StatusText = PreviewAnimation is null
                    ? "Load an animated GIF to send."
                    : "Ready.";
            }
        }
        else if (IsTextContent)
        {
            PreviewBitmap = SelectedSlot.TextBitmap;

            if (!IsBusy)
            {
                StatusText = string.IsNullOrWhiteSpace(Message)
                    ? "Enter a message to send."
                    : SelectedSlot.TextPreviewIsValid
                        ? "Ready."
                        : "Could not render this message.";
            }
        }
        else if (SelectedSlot.GraphicState.Current is not { } graphicBitmap)
        {
            if (!IsBusy)
            {
                StatusText = "Load an image to send.";
            }
        }
        else
        {
            PreviewBitmap = SelectedSlot.Mode == BadgeDisplayMode.StaticCentered
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
    }

    private void RefreshPreparedUpload()
    {
        var slotContents = Slots
            .Select(CreateSlotContent)
            .ToArray();
        var populatedSlotCount = slotContents.Count(content => content is not null);

        if (populatedSlotCount == 0)
        {
            _preparedUpload = null;
            UploadSummaryText = "0 of 8 slots • Nothing to send";
            SendCommand.RaiseCanExecuteChanged();
            return;
        }

        try
        {
            _preparedUpload = _controller.PrepareSlots(slotContents);
            UploadSummaryText =
                $"{_preparedUpload.PopulatedSlotCount} of 8 slots • " +
                $"{_preparedUpload.UploadSize:N0} / {BadgeProtocol.MaximumUploadSize:N0} bytes";
        }
        catch (ArgumentException)
        {
            _preparedUpload = null;
            UploadSummaryText = $"{populatedSlotCount} of 8 slots • Too large for badge";
        }

        SendCommand.RaiseCanExecuteChanged();
    }

    private static BadgeSlotContent? CreateSlotContent(BadgeSlotState slot) =>
        slot.ContentKind switch
        {
            BadgeContentKind.Text when slot.TextPreviewIsValid
                && !string.IsNullOrWhiteSpace(slot.Message) =>
                new BadgeSlotContent(
                    slot.TextBitmap,
                    Animation: null,
                    slot.Mode,
                    slot.Speed,
                    RepeatGraphic: false,
                    RepeatGapIsLit: false),
            BadgeContentKind.Graphic when slot.GraphicState.Current is { } bitmap =>
                new BadgeSlotContent(
                    bitmap,
                    Animation: null,
                    slot.Mode,
                    slot.Speed,
                    RepeatGraphic: slot.Mode == BadgeDisplayMode.ScrollLeft,
                    RepeatGapIsLit: slot.GraphicState.IsInverted),
            BadgeContentKind.Animation when slot.AnimationState.Current is { } animation =>
                new BadgeSlotContent(
                    Bitmap: null,
                    animation,
                    BadgeDisplayMode.Animation,
                    slot.Speed,
                    RepeatGraphic: false,
                    RepeatGapIsLit: false),
            _ => null,
        };

    private bool SelectedSlotHasStoredState() =>
        !string.IsNullOrEmpty(SelectedSlot.Message)
        || SelectedSlot.GraphicState.Source is not null
        || SelectedSlot.AnimationState.Source is not null
        || SelectedSlot.ContentKind != BadgeContentKind.Text
        || SelectedSlot.Mode != BadgeDisplayMode.ScrollLeft
        || SelectedSlot.Speed != 4;

    private void RaiseSelectedSlotPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedContent));
        RaiseContentPropertiesChanged();
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(SelectedMode));
        OnPropertyChanged(nameof(IsPreviewScrolling));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(IsGraphicInverted));
        OnPropertyChanged(nameof(IsAnimationInverted));
        OnPropertyChanged(nameof(SelectedFileName));
        OnPropertyChanged(nameof(SelectedAnimationFileName));
        OnPropertyChanged(nameof(AnimationFrameCountText));
        OnPropertyChanged(nameof(CanInvertGraphic));
        OnPropertyChanged(nameof(CanEditGraphic));
        OnPropertyChanged(nameof(CanInvertAnimation));
        OnPropertyChanged(nameof(CanClearSlot));
    }

    private void RaiseContentPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsTextContent));
        OnPropertyChanged(nameof(IsGraphicContent));
        OnPropertyChanged(nameof(IsAnimationContent));
        OnPropertyChanged(nameof(IsTextOrGraphicContent));
        OnPropertyChanged(nameof(IsPreviewScrolling));
        OnPropertyChanged(nameof(PreviewRepeatGap));
        OnPropertyChanged(nameof(PreviewRepeatGapIsLit));
        OnPropertyChanged(nameof(CanEditGraphic));
    }

    private void RaisePixelEditingPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsGraphicEditing));
        OnPropertyChanged(nameof(IsNotGraphicEditing));
        OnPropertyChanged(nameof(IsSlotSelectionEnabled));
        OnPropertyChanged(nameof(IsContentSelectionEnabled));
        OnPropertyChanged(nameof(CanLoadGraphic));
        OnPropertyChanged(nameof(CanInvertGraphic));
        OnPropertyChanged(nameof(CanEditGraphic));
        OnPropertyChanged(nameof(CanLoadAnimation));
        OnPropertyChanged(nameof(CanInvertAnimation));
        OnPropertyChanged(nameof(CanClearSlot));
        SendCommand.RaiseCanExecuteChanged();
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

public enum BadgeContentKind
{
    Text,
    Graphic,
    Animation,
}

public sealed record ContentOption(string Label, BadgeContentKind Value);

public sealed record ModeOption(string Label, BadgeDisplayMode Value);
