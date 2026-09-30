using System.Windows;
using Badgey.Rendering;

namespace Badgey;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(
            new BadgeController(new WindowsTextRenderer()));
    }
}
