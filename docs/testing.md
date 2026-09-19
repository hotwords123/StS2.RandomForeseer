# 本地回归测试

长期维护的无界面测试位于 `tests/RandomForeseer.Tests/`。测试直接引用当前项目，通过
`InternalsVisibleTo` 调用内部实现，运行时构建当前生产源码。
使用 xUnit 和 VSTest，支持 `dotnet test`、IDE Test Explorer、参数化用例和 TRX 结果。

## 环境与运行

- 安装 .NET 10 SDK（编译项目使用的 C# 14）和 .NET 9 runtime。
- 本机有合法游戏安装；按根目录 `local.props.template` 配置 `local.props` 中的
  `Sts2Dir` / `Sts2DataDir`。测试和主项目使用相同的游戏程序集与 RitsuLib 依赖。
- 首次运行需要还原 NuGet 包；不需要启动游戏或 Godot，也不需要配置 PCK 导出工具。

在根目录运行完整回归：

```powershell
dotnet test
```

命令构建当前源码并运行解决方案中的测试，失败时返回非零退出码。
可指定配置、筛选或逐项显示结果：

```powershell
dotnet test --filter 'FullyQualifiedName~MummifiedHandTests'
dotnet test --configuration Release
dotnet test --logger 'console;verbosity=normal'
```

需要 TRX 报告时，指定位于主项目和 Godot 排除目录内的输出路径：

```powershell
dotnet test --logger trx --results-directory tests/RandomForeseer.Tests/TestResults
```

也可直接运行独立项目：

```powershell
dotnet test tests/RandomForeseer.Tests/RandomForeseer.Tests.csproj
dotnet test tests/RandomForeseer.Tests/RandomForeseer.Tests.csproj --list-tests
```

测试项目通过普通 `ProjectReference` 复用主项目构建，并设置 `IsPublishable=false`。
测试源码、资源和构建产物均从主项目构建项排除；`tests/.gdignore` 防止 Godot 导入测试目录。

测试项目参与解决方案默认构建，支持根目录 `dotnet test` 和 IDE Test Explorer。
日常构建、导出部署和测试使用独立入口：

| 命令 | 行为 |
|---|---|
| `dotnet build` | 仅编译模组和测试项目 |
| `dotnet publish` | 使用 Release 配置编译模组、导出 PCK，并将模组自身的 DLL/PDB/现有 manifest/PCK 发布到游戏目录 |
| `dotnet publish -c Debug` | 使用 Debug 配置编译并发布模组 |
| `dotnet test` | 编译并执行测试 |

只编译模组时指定项目：

```powershell
dotnet build RandomForeseer.csproj
```

发布默认输出到 `$(Sts2Dir)/mods/RandomForeseer`，可为主项目指定输出目录：

```powershell
dotnet publish RandomForeseer.csproj -o ./artifacts/publish/RandomForeseer
```

manifest 依赖同步保持独立且默认关闭，由 `SyncManifestDependenciesOnBuild` 控制。
发布按编译、可选的 manifest 同步、PCK 导出、文件发布的顺序执行。
PCK 先生成到当前构建配置的中间目录，再随程序集和 manifest 一起发布。

## 开发工作流

1. 修改已有预测行为时，在对应测试类补充能够复现问题的最小场景，先运行筛选后的用例。
2. 修改共享 simulator、hook dispatch、预测状态或 fixture 时，提交前运行完整测试项目。
3. 游戏版本适配后运行完整测试；fixture 对新增原版 API 的调用会明确失败，按实际需要扩展。
4. 新增领域在测试项目下建目录，测试类按行为命名；独立输入用 `[Theory]`，不要将全部场景串进一个测试。
5. 涉及场景、动画、真实 listener 枚举、完整出牌/死亡生命周期的变更仍需游戏内验证。

各测试类的 XML doc 说明测试对象、覆盖范围和特殊隔离边界，使用 `see cref` 引用被测类型或方法。
具体场景和预期结果由测试方法名称表达；局部注释补充特殊前提。覆盖说明随测试代码维护，本文专注测试体系概览、运行方式和共享工作流。

测试是开发工具，不写入用户 README 或 changelog。游戏程序集、publicized 产物、`local.props`、
`bin/`、`obj/`、`TestResults/` 不提交，不上传到 GitHub；不在缺少游戏程序集的 GitHub CI 中运行。

## 测试架构与隔离边界

游戏测试基础设施位于 `Infrastructure/`：`GameTestBase` 管理每个用例的全局状态与 patch 生命周期，
`TestCombat` 分离源场景装配和预测执行会话，`CombatStateProxy` 提供无界面战斗环境。
具体 API 的调用方式、生命周期约束和扩展要求维护在对应类型及成员的 XML doc 中。

游戏测试依赖进程全局的模型库、战斗历史和 Harmony，因此统一在 `GameTestCollection` 内串行运行，
且不与其他集合并行；纯逻辑测试保留 xUnit 默认并行行为。各用例独立清理状态和 patch，避免跨测试污染。

默认隔离涉及 Godot 原生构造、兼容性配置过滤和 run listener 来源；数值 mirror、预测状态和 hook facade
仍使用实际实现。部分用例额外观察或替换命令，其覆盖范围和特殊隔离边界由对应测试类的 XML doc 说明。
无界面回归不能替代游戏内生命周期验证，也不代表完整对象图隔离或原版行为一致性。

## 已知限制与待修复测试

尚未支持行为的期望测试标记为 `Category=KnownLimitation`，通过对应测试的 `Skip` 和注释说明具体限制。
修复后移除 `Skip`，验证期望行为。完整运行结果中的
跳过数量必须单独报告；不要将“其余测试通过”表述为完整原版一致性保证。

测试框架接入参考：[Microsoft 的 .NET 测试指南](https://learn.microsoft.com/en-us/dotnet/core/testing/)。
