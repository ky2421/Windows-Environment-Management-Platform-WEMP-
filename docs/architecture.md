# WEMP 架构设计

## 1. 概述

WEMP（Windows Environment Management Platform）是面向 Windows 的集成化环境管理平台，采用**模块化架构**：一个宿主（Shell）+ 多个独立编译的功能模块，通过统一契约组合。

## 2. 分层结构

| 层 | 项目 | 职责 |
|---|---|---|
| Shell | `WEMP.App`（WPF）/ `WEMP.Service` / `WEMP.CLI` | 应用入口、DI 容器、模块发现与生命周期管理、导航 |
| Core | `WEMP.Core` | 模块契约（`IModule`）、消息总线（`IMessageBus`）、模块宿主（`IModuleHost`） |
| Modules | `WEMP.Optimization` 等 6 个项目 | 业务功能实现，每个模块独立编译 |
| Infrastructure | `WEMP.Infrastructure` | EF Core + SQLite、实体、Windows API 封装（后续） |

## 3. 模块契约

```text
IModule
├── Name / DisplayName / Version
├── InitializeAsync()   // 注册服务、订阅消息
├── ActivateAsync()     // 用户进入时激活
├── ShutdownAsync()     // 释放资源
└── Pages               // 注册到主导航的页面
```

模块间通信：
- 同步查询：DI 接口注入（显式依赖）
- 异步通知：`IMessageBus.Publish/Subscribe`（松散耦合）

## 4. 模块与依赖关系

| 模块 | 职责 | 主要依赖 |
|---|---|---|
| SystemInfo（系统检测） | 硬件/系统/进程/开发环境信息采集与快照持久化 | Windows API、WMI、Infrastructure |
| Optimization（系统优化） | 知识库驱动的一键/自定义优化，备份-应用-回滚三阶段，操作审计 | Core、Infrastructure |
| GameMode（游戏模式） | 游戏会话检测与系统状态切换 | Core、Optimization |
| DevEnvironment（开发环境） | YAML 模板化工具链部署 | Core、PackageManagement |
| PackageManagement（软件包管理） | winget/choco/scoop 聚合 | Core |
| Backup（备份恢复，规划） | 还原点、文件增量备份 | Core |
| Logging（日志中心） | 审计日志、事件聚合、异常检测 | Core |

## 5. 数据层

SQLite 单库，EF Core 8。19 张核心表覆盖六类数据（用户配置 / 系统快照 / 优化记录 / 日志 / 软件清单 / 开发环境）。详见数据库设计文档与 `src/WEMP.Infrastructure/Data/`。

## 6. 技术栈

- .NET 8 / C# 12 / WPF / CommunityToolkit.Mvvm
- EF Core + SQLite
- Serilog（文件滚动日志）
- 测试：xUnit

## 7. 目录

```
src/WEMP.Core           共享内核与模块契约
src/WEMP.Infrastructure 数据层与基础设施
src/WEMP.App            WPF 桌面宿主
src/WEMP.Service        Windows 服务宿主
src/WEMP.CLI            命令行宿主
src/WEMP.*              6 个功能模块
database/               EF Core 迁移与种子数据
assets/templates/       内置环境模板
```
