# 真实游戏输入预检与证据校验

[返回工具索引](../README.md)

本目录提供 **#5/#6 的最小环境预检与证据校验切片**。它只检查操作者明确提供的输入和证据记录；**不运行游戏、不读取 Steam 认证/日常档、不证明游戏程序集或 Mod 兼容**。合成夹具只验证工具本身。

## 命令

```sh
python3 tools/preflight/preflight.py check-inputs \
  --allowed-root /path/to/operator-inputs manifest.json

python3 tools/preflight/preflight.py check-evidence \
  --allowed-root /path/to/operator-inputs [--manifest manifest.json] record.json

python3 tools/preflight/test_preflight.py
```

`check-inputs` 成功时输出 `status: PASS`，但 `layer` 固定为 `preflight-tool`、`gameExecution` 为 `UNCOVERED`。缺失、路径越界、符号链接逃逸或摘要不符时输出 `status: ENVIRONMENT_INVALID` 并以非零退出；不会用桩类型、空报告或 skip 放绿。

`check-evidence` 校验记录的分层和字段。只有 `layer: game-scenario` 且 `status: PASS` 的完整记录会得到 `gamePass: true`；`PRODUCT_FAIL`、`ENVIRONMENT_INVALID`、`UNCOVERED` 记录可以合法保存，但不会变成游戏 PASS。

## 输入清单（check-inputs）

路径全部限制在操作者给的 `--allowed-root` 内；`inputRoot` 必须解析到同一目录。所有相对路径拒绝绝对路径、`..`、反斜杠和符号链接逃逸。

```json
{
  "schemaVersion": 1,
  "inputRoot": "/path/to/operator-inputs",
  "sourceSha": "47a3f6a8360309718990cb15ef64b1fb229d4cc1",
  "gameBuildId": "1794536281",
  "expectedGameBuildId": "1794536281",
  "platform": "linux-proton",
  "protonVersion": "9.0-4",
  "referenceRoot": "game-reference",
  "references": [
    {"path": "Managed/Assembly-CSharp.dll", "sha256": "<64 hex>"}
  ],
  "candidatePackage": {"path": "candidate/package.zip", "sha256": "<64 hex>"},
  "testSave": {
    "root": "test-save",
    "isolatedCopy": true,
    "cloudSyncDisabled": true,
    "dailySaveUsed": false
  }
}
```

- `gameBuildId` 必须与 `expectedGameBuildId` 一致；`linux-proton` 必须提供 `protonVersion`。
- `testSave` 必须声明隔离副本、Cloud 关闭且未使用日常档；这些是操作者声明，不是本工具对真实环境的证明。
- 引用和候选包必须实际存在且 SHA-256 匹配。

## 证据记录（check-evidence）

记录分层：`pure-core`、`preflight-tool`、`real-integration`、`game-scenario`。状态：`PASS`、`PRODUCT_FAIL`、`ENVIRONMENT_INVALID`、`UNCOVERED`。

`game-scenario`/`real-integration` 的 `PASS` 必须包含源码 SHA、候选包 SHA-256、游戏 build、平台/Proton、夹具 ID/摘要/隔离/Cloud、`expectedSource`、步骤、实际结果、`evidence` 映射和执行者。

- `fixture.cloud` 只接受 `disabled`/`off`/`none`；`fixture.isolation` 只接受 `private-test-prefix`/`isolated`/`isolated-test-prefix`/`disposable-test-prefix`。`cloud="enabled"` 或 `isolation="shared-host"` 会得到 `ENVIRONMENT_INVALID`，不会 `gamePass:true`。
- `steps` 必须覆盖 [scenarios.json](scenarios.json) 中对应场景的全部步骤；`evidence` 必须为每条 `requiredEvidence` 提供 `{path, sha256}`，路径在 `--allowed-root` 内且摘要匹配。
- `expectedSource` 必须指向场景合同的对应 ID、真实存在的 `docs/` 文件，或带数字编号的本仓 Issue/PR；不能等于 `actual`，也不能用不存在的文档或空编号冒充来源。

场景合同见 [scenarios.json](scenarios.json)：

- `vanilla-smoke`：原版无 Mod 主菜单→设置打开→返回，不改设置、不加载日常档。
- `single-character-native-flow`：单角色原生事件→保存→跨进程→两槽隔离→禁用后原版读档；D02/D03/D06/D08 未定的玩法断言不在本合同中。

`_safe_join()` 的检查与读取之间仍有 TOCTOU 窗口，当前威胁模型是“操作者独占且检查期间不可变的只读输入根”。如果要在共享 runner 上运行，需改为逐段 `O_NOFOLLOW` 打开或补充等价的隔离措施。

## 边界

- 路径只读操作者提供的输入根；不扫描其他工作树、Steam 认证、日常档或公共 `/tmp`。
- 真实游戏构建/程序集/测试档/Cloud 状态必须由物料 Owner 提供和核验；本工具只做格式、路径、摘要和声明检查。
- 预检通过 ≠ 游戏环境就绪；证据记录通过 ≠ 独立复核或产品验收。
- 真实执行仍按 [开发测试指南](https://github.com/sagitrs/ps-mod-kourindou/blob/main/docs/05-validation.md) 和 #5/#6 的场景合同推进；纯核心 CI 只覆盖游戏无关部分。
