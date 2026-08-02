using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WEMP.Backup.UI;

/// <summary>备份恢复页面。</summary>
public partial class BackupPage : UserControl
{
    public BackupPage(BackupPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private async void OnTaskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is BackupPageViewModel vm && vm.SelectedTask is not null)
        {
            await vm.SelectTaskCommand.ExecuteAsync(null);
        }
    }

    private async void OnRecordSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is BackupPageViewModel vm)
        {
            await vm.SelectRecordCommand.ExecuteAsync(null);
        }
    }

    private void OnBrowseSource(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BackupPageViewModel vm)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = "选择备份源目录" };
        if (dialog.ShowDialog() == true)
        {
            vm.SourcePath = dialog.FolderName;
        }
    }

    private void OnBrowseDestination(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BackupPageViewModel vm)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = "选择备份目标目录" };
        if (dialog.ShowDialog() == true)
        {
            vm.DestinationPath = dialog.FolderName;
        }
    }
}
