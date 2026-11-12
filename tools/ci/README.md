# 纯核心 CI

[返回工具索引](../README.md)

本目录是 V1-03 游戏无关核心的本地验证入口与已启用 CI 的支撑资产。它**不引用游戏程序集**，不证明 Mod 可加载、角色已到场、真实存档兼容或任何 AC01—AC20 通过。

## 本地命令

```sh
./tools/ci/verify-pure-core.sh
./tools/ci/verify-pure-core.sh --skip-restore
python3 tools/ci/test_verify_pure_core.py
```

`verify-pure-core.sh` 依次执行 restore、build、test；每次 test 在新建的 `mktemp` 目录中生成**新鲜 TRX 报告**，再由 `check_trx.py` 核验。任一步失败或报告不完整时，脚本原样返回非零，不会把失败吞成绿。

`check_trx.py` 要求：报告存在且非空、`total>0`、`executed>0`、`passed>0`、`executed==total`、`passed==total`，且 `failed/error/timeout/aborted/inconclusive/notExecuted/disconnected` 全为 0。缺报告、0 执行、全部跳过、失败或旧报告都不能算绿。

`test_verify_pure_core.py` 使用假 `dotnet` 可执行文件验证 restore/build/test 非零传播，以及缺报告、0 测试、全部跳过、失败报告、缺工具、参数错误等负控；不需要游戏材料。

## 已启用真实 CI

- **唯一维护入口**：`.github/workflows/pure-core.yml`。
- 工作流依次运行 `tools/docs/check_docs.py`、`tools/docs/test_check_docs.py`、`tools/ci/test_verify_pure_core.py`（14 项守卫自测），再运行 `tools/ci/verify-pure-core.sh`（28 项核心测试 + 新鲜 TRX 执行守卫）。
- 固定 `ubuntu-24.04`、SDK `10.0.404`、actions 完整提交 SHA、`timeout-minutes: 10`、`contents: read`；使用实际可用 CPU 中的 2 核与 2 GiB .NET GC 上限。
- 实例：PR17 整合头 `05a758aa2f1ff2788807ae785087ebdae0035ab9`，[push run 43166113417](https://github.com/sagitrs/ps-mod-kourindou/actions/runs/43166113417) 与 [PR run 43166118863](https://github.com/sagitrs/ps-mod-kourindou/actions/runs/43166118863) 均 success。
- 该 CI 只覆盖游戏无关核心/文档/守卫自测；**不运行游戏、不操作真实存档、不覆盖 AC01—AC20**。不能把 CI 绿写成游戏或产品验收通过。

## 维护约定

- 只编辑 `.github/workflows/pure-core.yml`。旧候选 `tools/ci/github-actions/pure-core.yml` 已在真实 CI 启用后删除，避免编辑错副本。
- 首次接线由 W2 使用单仓短期 App 令牌完成；本 Agent 未持有或借用该令牌。后续维护走本仓正常 workflow 权限。
- 若后续 workflow 编辑再次被 403 拒绝，保留脱敏原错误交 CIE 一次；不借他人 token、不自改签发策略、不伪造远程执行。
- PR18 已作为 CI 候选前向整合进 PR17（整合头 `05a758a`）；不要将其他 head 的 CI 绿无说明挪给 PR17。

## 边界

- 只覆盖游戏无关核心；不能替代 #5 真实适配或 #14 游戏内验收。
- 不包含 Steam/Unity/游戏 SDK、游戏 DLL、真实存档或凭据。
- 测试夹具中的任意参数不是产品默认值。
