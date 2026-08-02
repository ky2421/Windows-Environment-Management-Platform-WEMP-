# plugins/

WEMP 插件目录：第三方开发者通过实现 `IModule` 接口扩展平台能力。

## 插件如何工作

- 插件编译为独立程序集（.dll），放入此目录或其子目录
- WEMP 启动时自动扫描 `plugins/` 并加载实现 `WEMP.Core.Abstractions.IModule` 的类型
- 插件清单 `PluginManifest.yaml` 声明依赖与权限（尚未启用，预留）

## 示例插件

```
plugins/
└── ExamplePlugin/
    ├── ExamplePlugin.csproj      # 引用 WEMP.Core
    ├── PluginManifest.yaml       # 插件元数据
    └── ExampleModule.cs          # 实现 IModule
```

开发指南见 `docs/dev-guide/plugin-guide.md`（随插件系统开发阶段补充）。
