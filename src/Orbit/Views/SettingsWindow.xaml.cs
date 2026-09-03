using System.Windows;
using Orbit.ViewModels;

namespace Orbit.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Owner = App.GetAltTabSuppressor();
        SourceInitialized += (s, e) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Helpers.NativeMethods.MakeToolWindow(hwnd);
        };

        _viewModel.RequestClose += (_, _) => Close();
        Closing += (_, _) => _viewModel.OnWindowClosing();
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TabRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (ServicesTabGrid == null || AppearanceTabGrid == null || SystemTabGrid == null) return;
        ServicesTabGrid.Visibility = TabServicesRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AppearanceTabGrid.Visibility = TabAppearanceRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SystemTabGrid.Visibility = TabSystemRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }
}
