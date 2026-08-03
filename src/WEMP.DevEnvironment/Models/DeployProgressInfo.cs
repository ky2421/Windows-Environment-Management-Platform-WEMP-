namespace WEMP.DevEnvironment.Models;

/// <summary>部署进度回报：0-100 百分比与当前步骤描述。</summary>
public sealed record DeployProgressInfo(int Percent, string Message);
