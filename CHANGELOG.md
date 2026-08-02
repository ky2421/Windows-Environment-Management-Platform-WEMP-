# Changelog

本项目变更记录遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 新增
- 项目初始化：解决方案与项目骨架（Core / Infrastructure / App / Service / CLI）
- 数据库设计落地：SQLite + EF Core，覆盖用户配置、系统快照、优化记录、审计日志、软件清单、开发环境六类数据
- WPF 应用基础框架：MVVM、模块加载机制、Serilog 日志
- 开发环境准备：`.editorconfig`、`Directory.Build.props`、贡献指南
- 系统检测模块（WEMP.SystemInfo）：WMI 采集操作系统/CPU/GPU/内存/磁盘，子进程探测开发工具，结果持久化到 system_snapshots
- 系统优化模块（WEMP.Optimization）：知识库驱动的一键优化 / 自定义优化，8 类执行器（服务/注册表/启动项/网络/磁盘/电源/内存/游戏），统一 备份-应用-回滚 三阶段，结果与审计写入 optimization_records / audit_logs
- 数据库迁移：`AddDevEnvSnapshot`（system_snapshots 增加 dev_env_json）

### 变更
- 无

### 修复
- 无
