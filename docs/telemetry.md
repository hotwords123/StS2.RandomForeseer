# 遥测

## 启动快照与设置

当前注册 `mod_inventory` 和 `diagnostics` 两个申请项，用户通过 RitsuLib 按申请方整体同意或拒绝。

- `mod_inventory`：获授权后发送一次启动快照，包含游戏与 Mod 版本、运行环境、匿名安装 ID、会话 ID、启动快照时间、Mod 数量统计、完整 Mod 清单及本 Mod 设置；设置位于 `payload.private_contributions.RandomForeseer.settings_snapshot`。
- `exception`：发送与本 Mod 相关的异常消息、堆栈及诊断上下文。
- `godot_engine_error`：发送与本 Mod 相关的 Godot 引擎错误及堆栈。

v0.15.1 及以前通过 `session_start` 单独发送基础使用信息和设置快照；v0.15.2 起合并至 `mod_inventory`，不再发送 `session_start`。

## 诊断遥测过滤

`ModTelemetry.Register` 为 `diagnostics` 申请项注册 `ModTelemetryFilter.ShouldCapture`，使用 RitsuLib 0.5.14 新增的 `TelemetryRequest.WithCaptureFilter`。筛选发生在诊断 payload 构建和事件入队前；`ModTelemetryAdapter` 仅补充 `random_foreseer_version` 并转发，不再筛选发送队列。

## 过滤规则

- `exception`：本项目 `ModTelemetry.CaptureException` 设置 `capture_source = random_foreseer/<subsystem>`，该来源直接放行。原生上下文没有 `capture_mode`，因此使用与上报方法共享的来源前缀识别主动上报；原有 `capture_mode = manual` 属性仍保留。自动诊断通过 `TelemetryCaptureContext.Exception` 检查异常及其 `InnerException` 链，任一 `StackTrace` 包含大小写敏感的 `RandomForeseer.` 即保留。异常消息、序列化后的同名字段不用于判断。
- `godot_engine_error`：由运行时 RitsuLib 0.6.0+ 采集；其 `SourceData` 是已脱敏的 `engine_error` `JsonObject`，不是完整信封。`script_backtrace` 或 `code` 字符串中任一包含 `RandomForeseer.` 即保留。Godot 可能把 C# 异常栈写入 `code`；两个字段都需检查，与网关规则一致。缺失、空或非字符串字段不匹配；不扫描消息、文件、方法、其他元数据，不对主动来源额外放行。
- 按 RitsuLib 固定的小写事件名分发；其他诊断事件继续放行。筛选器只同步读取传入数据，不修改或保留来源对象，不进行网络或磁盘操作。

## 发送队列

诊断筛选只作用于新采集，不会重新筛选已持久化的队列，也不删除已入库事件。启动事件合并则移除了 `basic_usage` 请求，RitsuLib 发送前会丢弃该请求的旧队列事件；已排队的旧 `mod_inventory` 不会补入设置快照。

命名空间标记只能证明调用链涉及本模组，不证明错误由本模组引起。缺少堆栈或堆栈被截断时，相关错误也可能被过滤。RitsuLib 自身的诊断授权、去重与限流仍生效。

纯逻辑回归位于 `tests/RandomForeseer.Tests/Telemetry/TelemetryFilterTests.cs`，覆盖原生请求筛选器挂接、异常链、主动来源、两种引擎堆栈、缺失字段和来源数据不可变性。运行方式见 [本地回归测试](testing.md)。

## 对象图复制失败诊断

对象图复制异常通过 `ModTelemetry.CaptureException` 主动上报时，附加结构化属性 `properties.object_graph_failure`，包装异常的 `InnerException` 链中包含复制失败时也保留该诊断。

- `path`：人类可读的失败路径。
- `nodes`：从根到失败位置的路径节点，每个节点的 `path` 是该位置的完整路径，`type` 和 `assembly` 是该节点实际类型及程序集完整身份。
