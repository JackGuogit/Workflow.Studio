# Workflow.Studio

`Workflow.Studio` 是一个基于 WPF 的可视化工作流编辑器原型项目，围绕“节点 + 连线 + 执行引擎 + 插件能力”构建。项目当前提供一个可运行的桌面端工作台，支持在画布中拖拽节点、连接端口、编辑节点参数、执行 DAG 工作流，并实时观察节点状态与全局变量变化。

项目适合作为以下方向的参考实现：

- WPF + MVVM 的工作流设计器
- 基于 `Nodify` 的节点编辑器封装
- 节点定义、执行引擎、插件能力解耦
- 桌面端可扩展工作流平台原型

## 项目目标

当前实现重点在于验证一套清晰的工作流架构：

- 桌面端负责应用启动、依赖注入和壳层界面
- Workbench 负责画布交互、节点库、参数编辑和执行反馈
- Core 负责节点模型、执行模型、插件契约和执行引擎
- Nodes 负责具体节点实现
- Plugins 负责抽离可复用能力，让节点依赖能力接口而不是具体实现
- Theme / Nodify 负责视觉风格与第三方编辑器能力整合

## 技术栈

- `.NET 10`
- `WPF`
- `CommunityToolkit.Mvvm`
- `Autofac`
- `Nodify 7.3.0`

## 解决方案结构

```text
Workflow.Studio
├─ Workflow.Studio.Core        // 核心模型、节点抽象、插件契约、执行引擎
├─ Workflow.Studio.Desktop     // WPF 启动项目，Autofac 注册与主窗口
├─ Workflow.Studio.Nodes       // 内置节点定义与节点设置视图
├─ Workflow.Studio.Plugins     // 内置插件实现
├─ Workflow.Studio.Workbench   // 工作流编辑器 UI、视图模型、参数编辑窗口
├─ Workflow.Studio.Theme       // 主题资源与明暗色切换
└─ Workflow.Studio.Nodify      // 对 Nodify 的封装与交互扩展
```

## 当前能力

当前工作台已经具备以下可见功能：

- 左侧节点库展示所有已注册节点
- 支持把节点拖拽到画布中创建实例
- 支持输出端口连接输入端口
- 支持 `Ctrl + 拖动已连接输入端口` 对连线进行重定向
- 支持双击节点打开参数编辑窗口
- 支持删除选中节点、删除选中连线、断开端口连接
- 支持执行工作流、重置工作流状态
- 支持将当前工作流保存为 JSON 文件并从文件重新加载
- 支持浅色 / 深色主题切换
- 支持在右侧和顶部面板中查看状态消息、图摘要、视口信息、选择信息和全局变量

## 默认演示流程

应用启动后，工作台会自动加载一个内置 Demo 工作流：

```text
输入节点 -> 转换节点 -> 预览节点
```

对应逻辑如下：

1. `输入节点` 输出一段文本
2. `转换节点` 通过插件能力把文本转换为大写
3. `预览节点` 通过插件能力生成预览结果
4. 顶部状态区和左侧 `Global Variables` 面板展示执行结果

这个 Demo 非常适合用来理解项目中的几个核心概念：

- 节点定义负责描述端口、设置和执行逻辑
- 工作流执行引擎按照 DAG 拓扑批次执行节点
- 插件提供独立能力，节点在执行阶段按接口调用
- UI 通过事件总线同步节点状态与端口数据变化

## 内置节点

当前已注册的内置节点包括：

| 节点 | 类型 | 说明 |
| --- | --- | --- |
| `输入节点` | Source | 生成初始文本输出，可编辑文本内容 |
| `CSV读取节点` | Source | 从文件系统读取 CSV 文本 |
| `转换节点` | Transform | 通过插件能力将文本转换为大写 |
| `CSV转TSV节点` | Transform | 将 CSV 文本转换为 TSV 文本 |
| `预览节点` | Output | 通过插件能力生成预览内容 |
| `TSV保存节点` | Output | 将 TSV 文本保存到文件 |

## 内置插件

当前内置了两个演示插件：

| 插件 | 能力 | 用途 |
| --- | --- | --- |
| `UppercaseTransformPlugin` | `ITextTransformPlugin` | 为转换节点提供大写文本转换能力 |
| `PreviewPlugin` | `IPreviewPlugin` | 为预览节点提供预览内容生成能力 |

这说明项目的扩展方式是“节点”和“能力插件”双层解耦：

- 节点决定工作流图中的结构和执行入口
- 插件负责真正的独立能力实现
- 节点通过接口依赖插件，而不是直接依赖具体类

## 核心执行模型

`Workflow.Studio.Core` 中定义了整套工作流领域模型：

- `WorkflowData` 表示一个工作流图
- `NodeData` 表示节点实例
- `PortData` 表示输入 / 输出端口
- `ConnectionData` 表示节点间连接
- `NodeExecutionRequest` / `NodeExecutionResult` 表示执行输入输出
- `ExecutionContext` 保存执行历史、全局变量和端口快照

执行引擎 `WorkflowEngine` 的行为特征：

- 执行前重置节点与端口状态
- 使用拓扑排序构建 DAG 执行批次
- 同一批次中的节点并发执行
- 节点执行结果会自动传播到下游输入端口
- 如果工作流中存在环，会直接抛出异常
- 通过 `WorkflowEventHub` 向 UI 发布节点状态变化和端口值变化

## 节点扩展方式

新增一个节点通常需要以下步骤：

1. 在 `Workflow.Studio.Nodes` 中创建一个继承 `WorkflowNodeDefinition<TSettings>` 的节点类
2. 声明节点 `Descriptor`
3. 在 `BuildNode` 中定义输入 / 输出端口
4. 在 `ExecuteAsync` 中实现节点执行逻辑
5. 如果需要参数编辑，在节点设置类型之外补充一个 WPF 设置视图
6. 在 `Workflow.Studio.Desktop/ApplicationModule.cs` 中注册节点类型

如果节点依赖某种独立能力，推荐做法是：

1. 在 `Workflow.Studio.Core` 中定义插件能力接口
2. 在 `Workflow.Studio.Plugins` 中实现对应插件
3. 通过依赖注入把插件能力传给节点构造函数
4. 在 `ApplicationModule` 中同时注册插件和节点

这种方式更符合面向对象和职责分离的设计，也更方便后续替换能力实现。

## UI 交互说明

工作台中的主要交互约定如下：

- 从左侧节点库拖拽到右侧画布即可创建节点
- 从输出端口拖到输入端口即可创建连线
- 双击节点可打开设置窗口并保存参数
- 选中节点或连线后可直接删除
- 点击顶部按钮可执行工作流或重置状态
- 点击主题按钮可切换浅色 / 深色主题

## 构建与运行

### 环境要求

- Windows
- .NET SDK 10
- 支持 WPF 的开发环境
- 推荐使用 Visual Studio 打开桌面项目进行调试

### 命令行构建

在仓库根目录 `Workflow.Studio` 下执行：

```powershell
dotnet build .\Workflow.Studio.slnx
```

当前解决方案可以成功构建。

### 运行桌面端

```powershell
dotnet run --project .\Workflow.Studio.Desktop\Workflow.Studio.Desktop.csproj
```

启动项目为：

- `Workflow.Studio.Desktop`

## 开发建议

如果你准备继续扩展这个项目，建议优先完善以下方向：

- 工作流文件格式版本化、向后兼容迁移与最近文件管理
- 插件发现与动态加载机制
- 更完整的数据类型校验与端口兼容性约束
- 节点分组、缩放导航、框选体验优化
- 执行日志、断点调试、错误高亮
- 单元测试与集成测试补充

## 当前已知情况

目前项目处于原型演进阶段，已有较好的架构骨架，但仍有一些明显可继续完善的点：

- 核心模型中存在若干可空性警告，构建不失败但还可以继续收敛
- 已支持基础 JSON 持久化，但还缺少版本迁移、兼容性校验与文档管理能力
- 插件注册仍以应用启动时手工注入为主
- 当前更偏向 Demo / 架构样板，而不是完整产品

## 适合阅读的入口

如果你第一次阅读这个项目，推荐按下面顺序看代码：

1. `Workflow.Studio.Desktop/App.xaml.cs`
2. `Workflow.Studio.Desktop/ApplicationModule.cs`
3. `Workflow.Studio.Workbench/ViewModels/WorkflowWorkbenchViewModel.cs`
4. `Workflow.Studio.Core/Services/WorkflowEngine.cs`
5. `Workflow.Studio.Nodes/*`
6. `Workflow.Studio.Plugins/BuiltIn/DemoWorkflowPlugins.cs`

这样可以从“应用启动 -> 依赖注入 -> UI 工作台 -> 执行引擎 -> 节点实现 -> 插件能力”逐层理解整个项目。
