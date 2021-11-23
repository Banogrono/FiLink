using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FiLink.Models;
using FiLink.ViewModels;
namespace FiLink.Views
{
     public class MainWindow : Window
    {
        public MainWindowViewModel ViewModel = null!;

        public MainWindow()
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

        private void RefreshHosts_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.RefreshHosts();
        }

        private void OpenFile_OnClick(object? sender, RoutedEventArgs e)
        {
            OpenFileDialogAsync();
        }

        private void Exit_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SendButton_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.SendFiles();
        }

        private void SaveHost_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.SaveHosts();
        }

        private void RemoveHost_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.RemoveHosts();
        }

        private void ClearList_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.FileCollection.Clear();
        }

        private void InputElement_OnKeyUp(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1:
                {
                    OpenFileDialogAsync();
                    break;
                }
                case Key.F2:
                {
                    ViewModel.SendFiles();
                    break;
                }
                case Key.F3:
                {
                    ViewModel.FileCollection.Clear();
                    break;
                }
                case Key.F4:
                {
                    ViewModel.SaveHosts();
                    break;
                }
                case Key.F5:
                {
                    ViewModel.RefreshHosts();
                    break;
                }
            }
        }

        private async void OpenFileDialogAsync()
        {
            var dialog = new OpenFileDialog
            {
                AllowMultiple = true
            };

            var result = await dialog.ShowAsync(this);

            if (result != null)
            {
                foreach (var path in result)
                {
                    ViewModel.FileCollection.Add(path);
                }
            }
        }
        
        private async void OpenDirectoryDialogAsync()
        {
            var dialog = new OpenFolderDialog();
            
            var path = await dialog.ShowAsync(this);
            if (path != null)
            {
                if (!Directory.Exists(SettingsAndConstants.TempFilesDir))
                {
                    Directory.CreateDirectory(SettingsAndConstants.TempFilesDir);
                }
                
                new Task(() =>
                {
                    ViewModel.InfoLabel = "Preparing directory...";
                    var slash = UtilityMethods.IsUnix() ? "/" : @"\";
                    var compressedDirName =  Path.GetFileName(path) + ".zip";
                    var result = SettingsAndConstants.TempFilesDir + slash + compressedDirName;
                    ZipFile.CreateFromDirectory(path, result);
                    ViewModel.FileCollection.Add(result);
                    ViewModel.InfoLabel = "Directory prepared";
                }).Start(); // should fix issue #5
            }
        }

        private void OpenDownloads_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.OpenFolder();
        }

        private void Settings_OnClick(object? sender, RoutedEventArgs e)
        {
            ViewModel.OpenSettingsWindow();
        }

        private void Window_OnInitialized(object? sender, EventArgs e)
        {
            //ViewModel.RefreshHosts();
        }


        private void OpenFolder_OnClick(object? sender, RoutedEventArgs e)
        {
            OpenDirectoryDialogAsync();
        }
    }
}