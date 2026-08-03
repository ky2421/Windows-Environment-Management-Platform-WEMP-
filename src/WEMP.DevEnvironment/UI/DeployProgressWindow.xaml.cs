using System.Windows;
using Serilog;
using WEMP.DevEnvironment.Models;
using WEMP.DevEnvironment.Services;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.DevEnvironment.UI;

/// <summary>
/// 部署进度对话框：模态显示部署进度，部署流水线完成后自动关闭。
/// 传入部署委托（接收进度回调，返回部署完成的实例），Loaded 后异步执行。
/// </summary>
public partial class DeployProgressWindow : Window
{
    private readonly Func<IProgress<DeployProgressInfo>, Task<EnvInstance>> _deploy;

    public DeployProgressWindow(string deployTarget, Func<IProgress<DeployProgressInfo>, Task<EnvInstance>> deploy)
    {
        InitializeComponent();
        DeployTarget = deployTarget;
        _deploy = deploy;
        DataContext = this;
        Loaded += OnLoaded;
    }

    public string DeployTarget { get; }

    /// <summary>部署完成的实例；失败时为 null。</summary>
    public EnvInstance? DeployedInstance { get; private set; }

    /// <summary>部署异常消息；成功时为 null。</summary>
    public string? ErrorMessage { get; private set; }

    public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(
        nameof(ProgressPercent), typeof(int), typeof(DeployProgressWindow), new PropertyMetadata(0));

    public int ProgressPercent
    {
        get => (int)GetValue(ProgressPercentProperty);
        set => SetValue(ProgressPercentProperty, value);
    }

    public static readonly DependencyProperty ProgressTextProperty = DependencyProperty.Register(
        nameof(ProgressText), typeof(string), typeof(DeployProgressWindow), new PropertyMetadata("准备部署…"));

    public string ProgressText
    {
        get => (string)GetValue(ProgressTextProperty);
        set => SetValue(ProgressTextProperty, value);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        var progress = new Progress<DeployProgressInfo>(p =>
        {
            ProgressPercent = p.Percent;
            ProgressText = p.Message;
        });

        try
        {
            DeployedInstance = await _deploy(progress).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Log.Error(ex, "部署失败：{Target}", DeployTarget);
        }
        finally
        {
            DialogResult = true;
        }
    }
}
