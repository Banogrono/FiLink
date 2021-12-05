using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FiLink.ViewModels;

namespace FiLink.Views
{
    public class SettingsWindow : Window
    {
        public SettingsWindowViewModel ViewModel;

        public SettingsWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ToggleButton_OnChecked(object? sender, RoutedEventArgs e)
        {
            ViewModel.Encryption = true;
        }

        private void Save_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.ApplySettings();
            ViewModel.SaveSettings();
        }

        private void Apply_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.ApplySettings();
        }

        private void ToggleButton_OnUnchecked(object? sender, RoutedEventArgs e)
        {
            ViewModel.Encryption = false;
        }
    }
}