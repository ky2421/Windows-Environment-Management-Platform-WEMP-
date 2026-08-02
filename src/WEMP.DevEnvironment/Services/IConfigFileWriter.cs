namespace WEMP.DevEnvironment.Services;

/// <summary>
/// 配置文件写入抽象（INI 风格：key=value）。
/// 模板 config.files 的 strategy 支持 merge（合并现有）与 overwrite（覆盖）。
/// </summary>
public interface IConfigFileWriter
{
    /// <summary>将键值对写入配置文件（路径 %VAR% 展开，目录自动创建）。</summary>
    ConfigWriteResult Write(string path, IReadOnlyDictionary<string, string> values, string strategy);
}

/// <summary>配置文件写入结果。</summary>
public sealed record ConfigWriteResult(string Path, bool Created, int KeysWritten);
