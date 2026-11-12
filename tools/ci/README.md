# 纯核心 CI 候选

[返回工具索引](../README.md)

本目录是 V1-03 游戏无关核心的本地验证入口与 CI 候选。它**不引用游戏程序集**，不证明 Mod 可加载、角色已到场、真实存档兼容或任何 AC01—AC20 通过。

## 本地命令

```sh
./tools/ci/verify-pure-core.sh
./tools/ci/verify-pure-core.sh --skip-restore
python3 tools/ci/test_verify_pure_core.py
```

`verify-pure-core.sh` 依次执行 restore、build、test；每次 test 在新建的 `mktemp` 目录中生成**新鲜 TRX 报告**，再由 `check_trx.py` 核验。任一步失败或报告不完整时，脚本原样返回非零，不会把失败吞成绿。

`check_trx.py` 要求：报告存在且非空、`total>0`、`executed>0`、`passed>0`、`executed==total`、`passed==total`，且 `failed/error/timeout/aborted/inconclusive/notExecuted/disconnected` 全为 0。缺报告、0 执行、全部跳过、失败或旧报告都不能算绿。

`test_verify_pure_core.py` 使用假 `dotnet` 可执行文件验证 restore/build/test 非零传播，以及缺报告、0 测试、全部跳过、失败报告、缺工具、参数错误等负控；不需要游戏材料。

## GitHub Actions 候选

[github-actions/pure-core.yml](github-actions/pure-core.yml) 是**未启用**候选：

- `runs-on: ubuntu-24.04`，固定 SDK `10.0.404`；
- job 硬上界 `timeout-minutes: 10`；
- `taskset -c 0,1`（2 CPU）、`DOTNET_PROCESSOR_COUNT=2`、`DOTNET_GCHeapHardLimit=0x80000000`（2 GiB）与 `-m:2`；
- `actions/checkout` 与 `actions/setup-dotnet` 已固定完整提交 SHA，保留 `contents: read`，不使用 `pull_request_target`、游戏引用、私有材料或共享 self-hosted 主机。

当前仓没有已启用的 CI；候选文件存在不等于远程检查已执行。

## 接线状态与权限边界

- 2026-11-11 本 Agent 尝试自行推送 `.github/workflows/pure-core.yml` 时被 GitHub 拒绝：
  `refusing to allow a GitHub App to create or update workflow '.github/workflows/pure-core.yml' without 'workflows' permission`
- 2026-11-12 的 W2 决定改为：由 W2 使用自己的**单仓短期 App 令牌**一次性协作用 `.github/workflows/pure-core.yml` 接线；不把 W2 令牌交给本 Agent，不改变 Paseo 长期签发策略，不授予跨仓/主机/保护/秘密/actions-write 权限。
- 因此本 Agent 只能在 `tools/ci/` 提供可审候选；当前**未接线、未启用、无远端 checks**。接线前 W2 仍需核候选和启用风险；本候选不直接启用。
- 若 W2 的接线贡献进入交付，W2 对受影响交件披露共同贡献并回避计数 APP/最终验收；正常独立评审与合法合入要求不免。

## 与被审 PR 的关系

本目录位于 PR #18，基于 PR #17 的 `v1/6-core-slice` 堆叠。PR #18 的 checks（接线后）覆盖的是“PR17 已审核心 + 本 CI 候选”的组合，不能自动当作 PR17 准确 head 的远端 CI 绿；子分支绿不无说明挪给父 PR。PR #17 合入后，该工作流可在目标分支继续覆盖同一核心。

## 边界

- 只覆盖游戏无关核心；不能替代 #5 真实适配或 #14 游戏内验收。
- 不包含 Steam/Unity/游戏 SDK、游戏 DLL、真实存档或凭据。
- 测试夹具中的任意参数不是产品默认值。
