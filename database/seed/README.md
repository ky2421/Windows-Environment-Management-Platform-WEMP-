# database/seed

本目录存放 WEMP 数据库的种子数据（静态数据源），由启动时或迁移后导入。

## 文件约定

| 文件 | 对应表 | 说明 |
|---|---|---|
| `optimization-items.json` | `optimization_items` | 优化知识库条目（来自已确认的知识库 v1.0） |
| `env-templates/` | `env_templates` | 内置环境模板 YAML（与 `assets/templates/` 同步） |

## 导入规则

- 种子数据带 `kb_version` 字段，升级时增量比对 `code` 唯一键更新，不重复插入。
- 用户修改过的条目（`enabled = false`）在种子升级时不被覆盖。
