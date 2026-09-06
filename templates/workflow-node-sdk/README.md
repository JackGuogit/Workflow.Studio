# Workflow Studio Node SDK 模板

安装（本机）：

```powershell
dotnet new install P:\CSharp\WPF\DevWorkflow.Studio\Workflow.Studio\templates\workflow-node-sdk
```

使用：

```powershell
dotnet new workflow-node -n MyFirstNode -o MyFirstNode
```

要点：

- 单 DLL：执行逻辑 + 设置 POCO（`[WorkflowSetting]`）同库，不依赖 WPF，可被无头宿主与桌面端同时加载；
- `smoke/` 是无头自检控制台（验证"字段元数据提取 + 执行"），与主工程同目录但不参与其编译；
- 把 DLL 放到某个"扩展节点目录"后，可由 `ExternalNodeLoader.LoadFromDirectory` 通过 `[WorkflowNodeType]` 自动发现注册；
- 自定义 UI（可选）：后续把程序集改为 `net10.0-windows` 并加入 `Views/`，宿主通过 SettingsViewTypeName 查找。
