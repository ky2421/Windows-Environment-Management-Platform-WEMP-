using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.Backup.Services;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Backup.UI;

/// <summary>备份恢复页面视图模型：任务管理 + 备份执行 + 记录还原。</summary>
public partial class BackupPageViewModel : ObservableObject
{
    private readonly IBackupService _service;

    public ObservableCollection<BackupTask> Tasks { get; } = [];
    public ObservableCollection<BackupRecord> Records { get; } = [];
    public ObservableCollection<BackupFileEntry> RecordEntries { get; } = [];

    [ObservableProperty]
    private BackupTask? _selectedTask;

    [ObservableProperty]
    private BackupRecord? _selectedRecord;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "就绪";

    // 编辑表单字段
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _sourcePath = "";

    [ObservableProperty]
    private string _destinationPath = "";

    [ObservableProperty]
    private string _mode = "full";

    [ObservableProperty]
    private string _includePatterns = "";

    [ObservableProperty]
    private string _excludePatterns = "";

    [ObservableProperty]
    private bool _autoBackup;

    [ObservableProperty]
    private int _autoIntervalHours = 24;

    public IReadOnlyList<string> Modes { get; } = ["full", "incremental"];

    public BackupPageViewModel(IBackupService service)
    {
        _service = service;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await LoadTasksAsync();
            Status = "已就绪";
        }
        catch (Exception ex)
        {
            Status = $"初始化失败：{ex.Message}";
            Log.Error(ex, "备份页面初始化失败");
        }
    }

    [RelayCommand]
    private async Task SaveTaskAsync()
    {
        IsBusy = true;
        try
        {
            var task = new BackupTask
            {
                Name = Name,
                SourcePath = SourcePath,
                DestinationPath = DestinationPath,
                Mode = Mode,
                IncludePatterns = string.IsNullOrWhiteSpace(IncludePatterns) ? null : IncludePatterns,
                ExcludePatterns = string.IsNullOrWhiteSpace(ExcludePatterns) ? null : ExcludePatterns,
                AutoBackup = AutoBackup,
                AutoIntervalHours = AutoIntervalHours,
            };

            if (SelectedTask?.Id > 0)
            {
                task.Id = SelectedTask.Id;
                task.Enabled = SelectedTask.Enabled;
                var updated = await _service.UpdateTaskAsync(task);
                Status = updated is null ? "更新失败：任务不存在" : $"已更新任务：{updated.Name}";
            }
            else
            {
                var created = await _service.CreateTaskAsync(task);
                Status = $"已创建任务：{created.Name}";
            }

            ResetForm();
            await LoadTasksAsync();
        }
        catch (InvalidOperationException ex)
        {
            Status = ex.Message;
        }
        catch (Exception ex)
        {
            Status = $"保存失败：{ex.Message}";
            Log.Error(ex, "备份任务保存失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void EditTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        Name = SelectedTask.Name;
        SourcePath = SelectedTask.SourcePath;
        DestinationPath = SelectedTask.DestinationPath;
        Mode = SelectedTask.Mode;
        IncludePatterns = SelectedTask.IncludePatterns ?? "";
        ExcludePatterns = SelectedTask.ExcludePatterns ?? "";
        AutoBackup = SelectedTask.AutoBackup;
        AutoIntervalHours = SelectedTask.AutoIntervalHours;
    }

    [RelayCommand]
    private void ResetForm()
    {
        Name = "";
        SourcePath = "";
        DestinationPath = "";
        Mode = "full";
        IncludePatterns = "";
        ExcludePatterns = "";
        AutoBackup = false;
        AutoIntervalHours = 24;
    }

    [RelayCommand]
    private async Task ToggleTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        SelectedTask.Enabled = !SelectedTask.Enabled;
        await _service.UpdateTaskAsync(SelectedTask);
        Status = SelectedTask.Enabled ? $"已启用：{SelectedTask.Name}" : $"已停用：{SelectedTask.Name}";
    }

    [RelayCommand]
    private async Task DeleteTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"删除任务“{SelectedTask.Name}”？备份文件将保留在磁盘。",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await _service.DeleteTaskAsync(SelectedTask.Id);
        Status = $"已删除任务：{SelectedTask.Name}";
        ResetForm();
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task RunBackupAsync()
    {
        if (SelectedTask is null)
        {
            Status = "请先选择任务";
            return;
        }

        IsBusy = true;
        try
        {
            var record = await _service.RunBackupAsync(SelectedTask.Id);
            Status = record.Status == "success"
                ? $"{SelectedTask.Name}：备份成功（{record.FileCount} 个文件）"
                : $"{SelectedTask.Name}：备份失败 - {record.Error}";
            await LoadTasksAsync();
            await LoadRecordsAsync(SelectedTask.Id);
        }
        catch (Exception ex)
        {
            Status = $"备份失败：{ex.Message}";
            Log.Error(ex, "备份执行失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        await LoadRecordsAsync(SelectedTask.Id);
    }

    [RelayCommand]
    private async Task SelectRecordAsync()
    {
        if (SelectedRecord is null)
        {
            RecordEntries.Clear();
            return;
        }

        await LoadEntriesAsync(SelectedRecord.Id);
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (SelectedRecord is null)
        {
            Status = "请先选择备份记录";
            return;
        }

        var confirm = MessageBox.Show(
            "将备份内容恢复到源路径（同名文件将被覆盖），继续？",
            "确认还原",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = await _service.RestoreAsync(SelectedRecord.Id);
            Status = $"还原完成：{result.FileCount} 个文件 → {result.TargetPath}";
        }
        catch (Exception ex)
        {
            Status = $"还原失败：{ex.Message}";
            Log.Error(ex, "备份还原失败");
        }
    }

    [RelayCommand]
    private async Task DeleteRecordAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await _service.DeleteRecordAsync(SelectedRecord.Id);
        Status = "已删除记录（备份文件保留）";
        RecordEntries.Clear();
        await LoadRecordsAsync(SelectedTask?.Id ?? 0);
    }

    private async Task LoadTasksAsync()
    {
        var tasks = await _service.GetTasksAsync();
        Tasks.Clear();
        foreach (var task in tasks)
        {
            Tasks.Add(task);
        }

        if (Tasks.Count > 0 && SelectedTask is null)
        {
            SelectedTask = Tasks[0];
            await LoadRecordsAsync(SelectedTask.Id);
        }
    }

    private async Task LoadRecordsAsync(long taskId)
    {
        var records = await _service.GetRecordsAsync(taskId);
        Records.Clear();
        foreach (var record in records)
        {
            Records.Add(record);
        }

        RecordEntries.Clear();
    }

    private async Task LoadEntriesAsync(long recordId)
    {
        var entries = await _service.GetRecordEntriesAsync(recordId);
        RecordEntries.Clear();
        foreach (var entry in entries)
        {
            RecordEntries.Add(entry);
        }
    }
}
