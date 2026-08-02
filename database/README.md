# WEMP 数据库说明

WEMP 使用 SQLite 单库存储（EF Core 8 管理）。

## 数据文件

- 默认路径：`%LOCALAPPDATA%\WEMP\wemp.db`
- 连接字符串：`WEMP.Infrastructure.Data.WempDatabase.CreateConnectionString()`（可覆盖）

## 表清单（19 张核心表）

| 分类 | 表 |
|---|---|
| 用户配置 | `app_settings` |
| 系统信息 | `system_snapshots` |
| 优化记录 | `optimization_items`、`optimization_records` |
| 日志 | `audit_logs`、`system_events`、`log_anomalies` |
| 软件列表 | `installed_software`、`software_history`、`software_groups`、`software_group_items`、`package_operations`、`package_sources` |
| 开发环境 | `env_templates`、`env_instances`、`env_tools`、`env_envvars`、`env_snapshots`、`env_deploy_logs` |

## 迁移与种子

- 迁移代码：`src/WEMP.Infrastructure/Migrations/`（用法见 `database/migrations/README.md`）
- 种子数据：`database/seed/`（优化知识库、内置环境模板）

## 变更流程

1. 修改实体 → 2. 生成迁移 → 3. 更新种子 → 4. 运行测试
