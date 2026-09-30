using System.ComponentModel;
using System.Runtime.CompilerServices;
using Badgey.Hardware;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BadgeController _controller;
    private string _message = "Hello Badgey";
    private ModeOption _selectedMode;
    private int _speed = 4;
    private MonochromeBitmap _previewBitmap = new(0, BadgeBitmapPacker.RequiredHeight);
    private string _statusText = "Ready.";
    private bool _isBusy;
    private bool _previewIsValid;

    public MainWindowViewModel(BadgeController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        Modes =
        [
            new ModeOption("Scroll Left", BadgeDisplayMode.ScrollLeft),
            new ModeOption("Static Centered", BadgeDisplayMode.StaticCentered),
        ];
        Speeds = Enumerable.Range(1, 8).ToArray();
        _selectedMode = Modes[0];
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        UpdatePreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ModeOption> Modes { get; }

    public IReadOnlyList<int> Speeds { get; }

    public AsyncRelayCommand SendCommand { get; }

    public string Message
    {
        get => _message;
        set
        {
            if (!SetField(ref _message, value ?? string.Empty))
            {
                return;
            }

            UpdatePreview();
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    public ModeOption SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetField(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsPreviewScrolling));
            }
        }
    }

    public bool IsPreviewScrolling => SelectedMode.Value == BadgeDisplayMode.ScrollLeft;

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
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool CanSend() =>
        !IsBusy && _previewIsValid && !string.IsNullOrWhiteSpace(Message);

    private async Task SendAsync()
    {
        IsBusy = true;
        StatusText = "Connecting to badge…";

        try
        {
            await _controller.SendAsync(PreviewBitmap, SelectedMode.Value, Speed);
            StatusText = "Message sent successfully.";
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
            StatusText = "Could not write the message to the badge.";
        }
        catch (ArgumentException)
        {
            StatusText = "The message is too long for the badge.";
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

    private void UpdatePreview()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            PreviewBitmap = new MonochromeBitmap(0, BadgeBitmapPacker.RequiredHeight);
            _previewIsValid = false;

            if (!IsBusy)
            {
                StatusText = "Enter a message to send.";
            }

            return;
        }

        try
        {
            PreviewBitmap = _controller.Render(Message);
            _previewIsValid = true;

            if (!IsBusy)
            {
                StatusText = "Ready.";
            }
        }
        catch (Exception)
        {
            PreviewBitmap = new MonochromeBitmap(0, BadgeBitmapPacker.RequiredHeight);
            _previewIsValid = false;

            if (!IsBusy)
            {
                StatusText = "Could not render this message.";
            }
        }
    }

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

public sealed record ModeOption(string Label, BadgeDisplayMode Value);
