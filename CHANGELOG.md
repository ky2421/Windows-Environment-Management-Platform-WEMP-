# Changelog

本项目变更记录遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [1.0.0] - 2026-08-03

首个正式发布：六大核心模块（系统优化 / 游戏模式 / 开发环境 / 软件包管理 / 备份恢复 / 日志中心）全部实现，附带安装包、卸载程序与发布文档。

### 新增
- 项目初始化：解决方案与项目骨架（Core / Infrastructure / App / Service / CLI）
- 数据库设计落地：SQLite + EF Core，覆盖用户配置、系统快照、优化记录、审计日志、软件清单、开发环境六类数据
- WPF 应用基础框架：MVVM、模块加载机制、Serilog 日志
- 开发环境准备：`.editorconfig`、`Directory.Build.props`、贡献指南
- 系统检测模块（WEMP.SystemInfo）：WMI 采集操作系统/CPU/GPU/内存/磁盘，子进程探测开发工具，硬件评分与导出，结果持久化到 system_snapshots
- 系统优化模块（WEMP.Optimization）：知识库驱动的一键优化 / 自定义优化，20 类执行器（服务/注册表/启动项/网络/磁盘/电源/内存/游戏/Appx/后台应用/BIOS/设备/GPU/引导/HAGS/页面文件/计划任务/计时器/视觉/Windows 功能），统一 备份-应用-回滚 三阶段，结果与审计写入 optimization_records / audit_logs
- 优化风险分级：知识库条目按 safe / medium / advanced 三级标注，UI 分级展示与确认
- 系统还原点：优化前调用 Windows System Restore API（SRSetRestorePoint）创建还原点，序列号持久化到优化记录（restore_point_id），非管理员或未启用时自动降级
- 游戏模式模块（WEMP.GameMode）：前台窗口轮询检测游戏会话，进入自动切换高性能电源并释放后台进程、退出自动恢复；会话计时与历史持久化到 game_sessions；支持自定义游戏库（手动添加游戏路径）
- 软件包管理模块（WEMP.PackageManagement）：winget 适配层（探测/列表/升级/安装/卸载）、已装软件同步到 installed_software（含图标解析）、一键升级与操作记录持久化到 package_operations、软件分组（SoftwareGroup/SoftwareGroupItem）批量安装
- 开发环境模块（WEMP.DevEnvironment）：YAML 模板库（内置 30+ 语言/工具链模板：Node.js / Python / Java / Go / Rust / Docker / Git / VS Code / 数据库 / 嵌入式等）与一键部署流水线（工具安装 → 环境变量 → 配置文件 → 验证 → 快照），部署日志与快照持久化到 env_deploy_logs / env_snapshots，支持重新验证、回滚与部署进度窗口
- 日志中心模块（WEMP.Logging）：审计日志查询（模块/级别/关键字过滤、分页）与统计聚合、Windows Application/System 事件日志聚合（去重入库 system_events）、异常规则扫描（应用崩溃 / 意外关机 / 单源错误风暴 / 审计失败率过高，24h 去重写 log_anomalies）与异常处置
- 备份恢复模块（WEMP.Backup）：任务化全量 / 增量文件备份（glob 包含/排除过滤、到期自动备份），备份记录与文件条目持久化到 backup_records / backup_file_entries，支持按记录还原（默认回源路径或自定义目标）
- 测试体系：单元测试（276 项）、UI 冒烟测试、系统级真实环境兼容性测试工具（tools/real-env-test，覆盖管理员/普通用户 × HKLM/HKCU 矩阵）
- 数据库迁移：`AddBackup`、`AddGameSessions`、`AddDevEnvSnapshot`、`AddOptimizationRiskLevel`、`AddCustomGames`、`AddInstalledSoftwareIcon`
- 安装包与卸载程序：Inno Setup 打包（WEMP-1.0.0-setup.exe），卸载入口注册到 Windows 控制面板

### 变更
- 项目版本号 0.1.0 → 1.0.0（首个正式发布，SemVer）
- README 更新为正式版：功能清单、安装与卸载说明、构建与测试指引
- 发布流程脚本：`scripts/publish.ps1`（clean + publish 到 dist/wemp）、`scripts/package.ps1`（Inno Setup 打包）、`scripts/build.ps1`（构建 + 测试）

### 修复
- 优化回滚失败：备份数据经 JSON 序列化往返后类型变化（int → JsonElement）导致 `RegistryKey.SetValue` 类型不匹配，恢复时按 `RegistryValueKind` 还原为正确 .NET 类型（RegistryAction）
- 真实环境兼容性验证（管理员/普通用户 × HKLM/HKCU 四组矩阵）全部通过

## [Unreleased]

### 新增
- 无

### 变更
- 无

### 修复
- 无
