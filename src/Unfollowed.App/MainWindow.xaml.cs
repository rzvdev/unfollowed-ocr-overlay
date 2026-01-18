using System.Windows;
using Unfollowed.App.ViewModels;

namespace Unfollowed.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
