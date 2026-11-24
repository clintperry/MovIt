using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using MovIt.Services;
using MovIt.ViewModels;

namespace MovIt.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(new PlatformService(this));
        DataContext = ViewModel;

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public MainViewModel ViewModel { get; }

    private bool CanAcceptDrop(DragEventArgs e) => ViewModel.IsIdle && e.DataTransfer.Contains(DataFormat.File);

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDragState(e);
        DropOverlay.IsVisible = e.DragEffects != DragDropEffects.None;
    }

    private void OnDragOver(object? sender, DragEventArgs e) => UpdateDragState(e);

    private void OnDragLeave(object? sender, DragEventArgs e) => DropOverlay.IsVisible = false;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        if (CanAcceptDrop(e))
        {
            var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
            if (paths is { Count: > 0 })
                ViewModel.AddPaths(paths);
        }
        e.Handled = true;
    }

    private void UpdateDragState(DragEventArgs e)
    {
        e.DragEffects = CanAcceptDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
}
