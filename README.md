# WEMP — Windows Environment Management Platform

Windows 环境管理平台：一个面向 Windows 的集成化环境管理工具，将系统优化、游戏模式、开发环境配置、软件包管理、系统备份恢复、日志管理六大能力统一到一个平台中。

> 状态：骨架阶段（Phase 1 进行中），仅基础框架与数据库，尚无业务功能。

## 功能

- 系统优化：启动项、服务、磁盘、网络、电源优化，附带安全建议知识库
- 游戏模式：一键进入/退出游戏优化状态，自动检测游戏进程
- 开发环境：YAML 模板一键部署 Python / Node.js / Java / Git / Docker / VS Code 等工具链
- 软件包管理：聚合 winget / Chocolatey / Scoop 的统一软件管理
- 备份与恢复：还原点、文件增量备份、定时策略
- 日志中心：操作审计、Windows 事件日志聚合、异常检测

## 快速开始

### 环境要求

- Windows 10 22H2+ / Windows 11
- .NET 8 SDK（`winget install Microsoft.DotNet.SDK.8`）

### 构建

```powershell
dotnet restore
dotnet build WEMP.sln
dotnet test
```

### 运行

```powershell
dotnet run --project src/WEMP.App
```

## 技术栈

- .NET 8 / C# 12
- WPF + CommunityToolkit.Mvvm
- SQLite + EF Core
- Serilog

## 目录结构

```
docs/        文档（架构、用户手册、开发者指南）
src/         源代码（按模块组织）
tests/       单元测试
assets/      静态资源（图标、截图、内置模板）
scripts/     构建与发布脚本
plugins/     插件目录
database/    EF Core 迁移与种子数据
```

## 贡献

请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 与 [docs/dev-guide](docs/dev-guide) 后提交 PR。

## License

[MIT](LICENSE)
