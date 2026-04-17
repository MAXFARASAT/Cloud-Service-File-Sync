using System.Windows;
using FileSync.Core;

namespace FileSync.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            await vm.HandleDropAsync(files);
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void InitializeSyncRoot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.InitializeSyncRoot();
        }
    }

    private void HydrateSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.HydrateSelected();
        }
    }
}
