using System.Windows;
using WEMP.DevEnvironment.Models;

namespace WEMP.DevEnvironment.UI;

/// <summary>部署工具选择弹窗：让用户勾选要安装的工具后确认部署。</summary>
public partial class TemplateToolPickerWindow : Window
{
    private readonly EnvTemplateSpec _spec;
    private readonly List<ToolChoiceItem> _items;

    public TemplateToolPickerWindow(EnvTemplateSpec spec)
    {
        InitializeComponent();
        _spec = spec;
        TemplateName = spec.Name;
        _items = spec.Tools
            .Select(t => new ToolChoiceItem
            {
                Name = t.Name,
                Version = FormatVersion(t.Version),
                IsOptional = t.Optional,
                IsSelected = !t.Optional,
            })
            .ToList();
        Tools = _items;
        DataContext = this;
    }

    public string TemplateName { get; }

    public List<ToolChoiceItem> Tools { get; }

    /// <summary>用户勾选确认后的工具名列表；取消时为 null。</summary>
    public IReadOnlyList<string>? SelectedToolNames { get; private set; }

    private void OnDeployClick(object sender, RoutedEventArgs e)
    {
        // 模板声明了部署警告（如 GitHub 下载源需加速器）时先弹确认框
        if (!string.IsNullOrWhiteSpace(_spec.DeployWarning))
        {
            var confirm = MessageBox.Show(
                this,
                $"{_spec.DeployWarning}\n\n是否继续部署？",
                "部署提醒",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return; // 用户拒绝，保持弹窗打开
            }
        }

        SelectedToolNames = _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
        DialogResult = true;
    }

    private static string FormatVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "latest";
        }

        var trimmed = version.Trim();
        return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? trimmed : $"v{trimmed}";
    }
}

/// <summary>工具选择行数据。</summary>
public sealed class ToolChoiceItem
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public bool IsOptional { get; init; }
    public bool IsSelected { get; init; }
}
