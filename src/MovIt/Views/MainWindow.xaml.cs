using System.Windows;
using MovIt.ViewModels;

namespace MovIt.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private bool CanAcceptDrop(DragEventArgs e) =>
        ViewModel.IsIdle && e.Data.GetDataPresent(DataFormats.FileDrop);

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
        if (e.Effects != DragDropEffects.None) DropOverlay.Visibility = Visibility.Visible;
    }

    private void OnDragOver(object sender, DragEventArgs e) => UpdateDragState(e);

    private void OnDragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (CanAcceptDrop(e) && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            ViewModel.AddPaths(paths);
        e.Handled = true;
    }

    private void UpdateDragState(DragEventArgs e)
    {
        e.Effects = CanAcceptDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
}
