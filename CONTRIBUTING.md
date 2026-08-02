# Contributing to WEMP

感谢你愿意为 WEMP 贡献代码。请先阅读以下约定。

## 开发环境

1. Windows 10/11
2. 安装 .NET 8 SDK
3. 克隆仓库后执行 `dotnet build WEMP.sln` 验证环境

## 分支策略

| 分支 | 用途 |
|---|---|
| `main` | 稳定分支，仅接受 `release/*` 合并 |
| `develop` | 集成分支，功能开发的基础 |
| `feature/*` | 新功能，如 `feature/system-optimization` |
| `fix/*` | 缺陷修复 |
| `release/*` | 发布准备 |

## Commit 规范

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/)：

```
<type>(<scope>): <subject>

type: feat | fix | docs | style | refactor | perf | test | build | ci | chore
scope: core | app | infrastructure | optimization | devenv | pkg | backup | logging | ...
```

示例：`feat(devenv): add python environment template`

## PR 流程

1. 从 `develop` 切出 `feature/*` 分支
2. 提交时遵循上述 Commit 规范
3. 提交 PR 前本地执行：`dotnet build` + `dotnet test` + `dotnet format --verify-no-changes`
4. PR 标题使用 `type(scope): subject` 格式
5. 至少 1 人 Review 通过后合并

## 代码规范

- 遵循 `.editorconfig`（缩进 4 空格、UTF-8、CRLF）
- 遵循 `docs/dev-guide/module-guide.md` 的模块开发约定
- 新功能必须附带测试

## Issue 规范

- Bug 报告请提供：Windows 版本、复现步骤、期望行为、实际行为
- 功能请求请说明使用场景与收益
