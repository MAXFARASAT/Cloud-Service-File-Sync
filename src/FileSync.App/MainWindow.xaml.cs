using System.Windows;
using FileSync.Core;
using Forms = System.Windows.Forms;
using System.IO;

namespace FileSync.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            await vm.HandleDropAsync(files);
        }
    }

    private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void InitializeSyncRoot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.InitializeSyncRoot();
        }
    }

    private async void HydrateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.SelectedFile is null)
        {
            vm.LastMessage = "Select a file first to hydrate.";
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select destination folder for hydrated file",
            UseDescriptionForTitle = true,
            SelectedPath = vm.SyncRootPath
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            var selected = vm.SelectedFile;
            await vm.HydrateSelectedAsync();

            if (selected is null || selected.Status != SyncStatus.Downloaded || string.IsNullOrWhiteSpace(selected.LocalPath) || !File.Exists(selected.LocalPath))
            {
                return;
            }

            var destinationPath = Path.Combine(dialog.SelectedPath, selected.FileName);
            File.Copy(selected.LocalPath, destinationPath, overwrite: true);
            vm.LastMessage = $"Hydrated and copied to: {destinationPath}";
        }
    }

    private void UnregisterSyncRoot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.UnregisterSyncRoot();
        }
    }

    private void DeleteSelectedEntry_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.DeleteSelectedEntry();
        }
    }

    private void ClearFailedEntries_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ClearFailedEntries();
        }
    }
}
