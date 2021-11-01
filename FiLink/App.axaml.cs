using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FiLink.ViewModels;
using FiLink.Views;

namespace FiLink
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var viewModel = new MainWindowViewModel();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel,
                    ViewModel = viewModel, 
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}