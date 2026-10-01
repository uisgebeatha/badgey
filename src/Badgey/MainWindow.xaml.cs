using System.Windows;
using Badgey.Rendering;
using Microsoft.Win32;

namespace Badgey;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(
            new BadgeController(
                new WindowsTextRenderer(),
                new WindowsImageRenderer(),
                new WindowsGifAnimationRenderer()));
    }

    private void LoadImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load badge graphic",
            Filter =
                "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|" +
                "PNG images (*.png)|*.png|" +
                "JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                "Bitmap images (*.bmp)|*.bmp",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == true
            && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.LoadGraphic(dialog.FileName);
        }
    }

    private void LoadGifButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load badge animation",
            Filter = "GIF animations (*.gif)|*.gif",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == true
            && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.LoadAnimation(dialog.FileName);
        }
    }
}
