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

测试项目的 `ProjectReference` 关闭复制 Mod、PCK 导出、manifest 依赖同步和 RitsuLib 自动部署，
直接 `dotnet test` 也使用这些设置。测试源码、资源和构建产物均从
主项目构建项排除；`tests/.gdignore` 防止 Godot 导入测试目录。

测试项目参与解决方案默认构建，支持根目录 `dotnet test` 和 IDE Test Explorer。
根目录 `dotnet build` 同时编译模组和测试项目；只编译模组时指定 `RandomForeseer.csproj`。
日常构建使用以下参数关闭部署、PCK 导出和 manifest 依赖同步：

```powershell
dotnet build /p:CopyModOnBuild=false /p:RunPckExport=false /p:SyncManifestDependenciesOnBuild=false
```

## 开发工作流

1. 修改已有预测行为时，在对应测试类补充能够复现问题的最小场景，先运行筛选后的用例。
2. 修改共享 simulator、hook dispatch、预测状态或 fixture 时，提交前运行完整测试项目。
3. 游戏版本适配后运行完整测试；fixture 对新增原版 API 的调用会明确失败，按实际需要扩展。
4. 新增领域在测试项目下建目录，测试类按行为命名；独立输入用 `[Theory]`，不要将全部场景串进一个测试。
5. 涉及场景、动画、真实 listener 枚举、完整出牌/死亡生命周期的变更仍需游戏内验证。

各测试类的 XML doc 说明测试对象、覆盖范围和特殊隔离边界，使用 `see cref` 引用被测类型或方法。
具体场景和预期结果由测试方法名称表达；局部注释补充特殊前提。覆盖说明随测试代码维护，本文仅记录共享规则。

测试是开发工具，不写入用户 README 或 changelog。游戏程序集、publicized 产物、`local.props`、
`bin/`、`obj/`、`TestResults/` 不提交，不上传到 GitHub；不在缺少游戏程序集的 GitHub CI 中运行。

## Fixture 与扩展边界

- `Infrastructure/GameTestBase` 为每个游戏测试初始化模型库、清空战斗历史，安装属于该用例的
  Harmony patch，并在释放时撤销自己的 patch、清理模型库和历史。构造失败也撤销已安装的 patch。
- `ModelDb`、`CombatManager.Instance.History` 和 Harmony 是进程全局状态。相关测试类标记
  `[Collection(GameTestCollection.Name)]` 并继承 `GameTestBase`；该集合内部串行，且不与其他集合并行。
  纯逻辑测试保留 xUnit 默认并行行为。不得自行保留跨测试可变数据或 patch。
- 会安装 patch 的初始化放在 `GameTestBase` 的异常清理范围内，避免派生类构造失败后遗留全局状态。
- `TestCombat` 创建最小玩家、战斗和预测牌。原版提供 `ModelDb.Inject`，因此新增模型一般不需要
  修改集中式模型清单；只按用例需要注入。添加通用新建 power 的场景时，初始化该 power 所需的模型。
- `CombatStateProxy` 只提供模拟实际用到的 `ICombatState` 成员；未知成员抛异常，不能添加返回默认值的兜底。
- 默认隔离仅涉及 Godot `StringName` 原生构造、兼容性配置过滤和 run listener 来源。
  数值 mirror、预测状态和 hook facade 保持实际实现。
- 用例启用额外的命令观察或替换时，在对应测试类的 XML doc 中说明隔离边界。
- 对项目内部 API 使用类型检查的直接调用。只有无法正常赋值的原版 readonly/编译器生成字段、
  Harmony 目标和私有资源支付入口使用集中管理的反射。

## 已知限制与待修复测试

尚未支持行为的期望测试标记为 `Category=KnownLimitation`，通过对应测试的 `Skip` 和注释说明具体限制。
修复后移除 `Skip`，验证期望行为。完整运行结果中的
跳过数量必须单独报告；不要将“其余测试通过”表述为完整原版一致性保证。

测试框架接入参考：[Microsoft 的 .NET 测试指南](https://learn.microsoft.com/en-us/dotnet/core/testing/)。
