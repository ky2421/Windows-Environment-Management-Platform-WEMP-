# database/

WEMP 数据库相关目录。

## 结构

| 路径 | 说明 |
|---|---|
| `migrations/` | 迁移说明（实际迁移代码位于 `src/WEMP.Infrastructure/Migrations/`，见下） |
| `seed/` | 种子数据（优化知识库、内置模板等） |

## 迁移位置说明

EF Core 迁移代码（C#）位于 `src/WEMP.Infrastructure/Migrations/`，必须保留在项目内以参与编译。
本目录仅存放使用说明与文档。

## 常用命令

```powershell
# 新增迁移
dotnet-ef migrations add <Name> --project src/WEMP.Infrastructure --startup-project src/WEMP.Infrastructure

# 应用迁移（创建/升级数据库）
dotnet-ef database update --project src/WEMP.Infrastructure --startup-project src/WEMP.Infrastructure

# 撤销最近迁移（不应用）
dotnet-ef migrations remove --project src/WEMP.Infrastructure --startup-project src/WEMP.Infrastructure
```

## 数据库位置

- 默认：`%LOCALAPPDATA%\WEMP\wemp.db`
- 通过 `WEMP.Infrastructure.Data.WempDatabase` 统一解析，可用连接字符串覆盖。
