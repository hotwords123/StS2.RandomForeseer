# 遥测

本文面向维护遥测采集和查询线上事件的开发者，说明客户端事件结构、查询约定与过滤规则。线上查询及服务端操作前，先阅读 [网关文档](../services/docs/telemetry-gateway.md) 的当前部署状态和“多个 PostHog CLI 身份”，选择对应项目的查询入口；部署、项目切换和验收也以该文档为准。

## 事件结构与查询约定

Random Foreseer 作为 RitsuLib 的遥测申请方，通过 PostHog batch adapter 的 `/batch/` 上报普通事件。查询使用 `events` 表及其 `event` 字段，并优先过滤 `properties['applicant_id'] = 'RandomForeseer'`；异常也按普通事件查询，无需使用 PostHog Error Tracking issue 接口。

所有事件都带有 RitsuLib 信封通用属性，包括匿名安装 ID、telemetry session ID、RitsuLib/Mod 版本、游戏版本、平台、操作系统、架构、.NET runtime 和游戏语言，以及 `schema`、`applicant_id`、`owner_mod_id`、`request_id`、`category` 和 `random_foreseer_version`。结构化数据统一位于 `properties['payload']`；下文中的 `payload` 均指该属性。

| 事件 | 授权请求 | 主要结构化数据 |
| --- | --- | --- |
| `mod_inventory` | `mod_inventory` | `payload.applicant_payload.captured_at_utc`、`payload.base_payload.mods` / `loaded_mods`、`payload.private_contributions.RandomForeseer.settings_snapshot` |
| `exception` | `diagnostics` | `payload.applicant_payload.exception`，包含 `type`、`message`、`stack_trace` 和可递归的 `inner`，并附带 `framework_runtime` 快照 |
| `godot_engine_error` | `diagnostics` | `payload.applicant_payload.engine_error`，其中 `script_backtrace` 和 `code` 用于调用链相关性判断 |

`mod_inventory` 在首次主菜单建立启动快照后向已授权申请方回放一次，包含公共运行信息、完整 Mod 清单及本 Mod 设置，字段路径见上表。每个 Mod 条目包含 `id`、`name`、`version`、`state`、`source`、`affects_gameplay`、程序集信息、错误数量、安全化错误列表及可选的 `workshop_id`。

启动快照的扁平属性包括 `capture_source = 'startup_snapshot'`、`startup_snapshot_at_utc`、`registered_mod_count`、`loaded_mod_count` 和 `gameplay_mod_count`；清单还提供 `disabled_mod_count`、`failed_mod_count` 和 `added_at_runtime_mod_count`。

v0.15.1 及以前通过 `session_start` 单独发送基础使用信息和设置快照；v0.15.2 起合并至 `mod_inventory`，不再发送 `session_start`。

### 诊断事件属性

本项目通过 `ModTelemetry.CaptureException` 主动上报的异常带有 `capture_mode = 'manual'`、`capture_source = 'random_foreseer/<subsystem>'`、`subsystem`、`operation`，以及可选的序列化安全 `exception_context`。RitsuLib 也会把 vanilla Sentry、未处理异常等自动诊断镜像为同名 `exception` 事件，这类事件没有 `capture_mode = 'manual'`，可能带有 `payload_kind = 'exception'`、`capture_source` 和 `exception_type`。

`godot_engine_error` 由运行时 RitsuLib 0.6.0+ 采集，带有 `capture_source = 'godot_logger'` 和 `payload_kind = 'engine_error'`。

### 对象图复制失败诊断

对象图复制异常通过 `ModTelemetry.CaptureException` 主动上报时，附加结构化属性 `properties.object_graph_failure`，包装异常的 `InnerException` 链中包含复制失败时也保留该诊断。

- `path`：人类可读的失败路径。
- `nodes`：从根到失败位置的路径节点，每个节点的 `path` 是该位置的完整路径，`type` 和 `assembly` 是该节点实际类型及程序集完整身份。

## 诊断遥测过滤

`ModTelemetry.Register` 为 `diagnostics` 申请项注册 `ModTelemetryFilter.ShouldCapture`，使用 RitsuLib 0.5.14 新增的 `TelemetryRequest.WithCaptureFilter`。筛选发生在诊断 payload 构建和事件入队前；`ModTelemetryAdapter` 仅补充 `random_foreseer_version` 并转发，不再筛选发送队列。

### 过滤规则

- `exception`：本项目 `ModTelemetry.CaptureException` 设置 `capture_source = random_foreseer/<subsystem>`，该来源直接放行。原生上下文没有 `capture_mode`，因此使用与上报方法共享的来源前缀识别主动上报；原有 `capture_mode = manual` 属性仍保留。自动诊断通过 `TelemetryCaptureContext.Exception` 检查异常及其 `InnerException` 链，任一 `StackTrace` 包含大小写敏感的 `RandomForeseer.` 即保留。
- `godot_engine_error`：原生筛选上下文的 `SourceData` 是已脱敏的 `engine_error` `JsonObject`，不是完整信封。`script_backtrace` 或 `code` 字符串中任一包含 `RandomForeseer.` 即保留。
