# 纯核心 CI 候选

本目录是 V1-03 游戏无关核心的本地验证入口与 CI 候选。它**不引用游戏程序集**，不证明 Mod 可加载、角色已到场、真实存档兼容或任何 AC01—AC20 通过。

## 本地命令

```sh
./tools/ci/verify-pure-core.sh
./tools/ci/verify-pure-core.sh --skip-restore
python3 tools/ci/test_verify_pure_core.py
```

`verify-pure-core.sh` 依次执行 restore、build、test，并在任一步失败时原样返回该步退出码；不会把失败吞成绿。`test_verify_pure_core.py` 使用假 `dotnet` 可执行文件验证 restore/build/test 的非零传播、缺工具和参数错误，不需要游戏材料。

## GitHub Actions 候选

[github-actions/pure-core.yml](github-actions/pure-core.yml) 是**未启用**的候选工作流：只在纯核心上运行 `verify-pure-core.sh`，使用固定 `10.0.404` SDK，不引用游戏。候选中的 action 使用主版本标签，正式启用前应改为完整提交 SHA 并按仓规则审查。

当前仓没有已启用的 CI；候选文件存在不等于远程检查已执行。若因 workflow 写入权限或 CI 策略无法启用，保留本候选并由有权方决定入口，不伪造远程绿灯。

## 边界

- 只覆盖游戏无关核心；不能替代 #5 真实适配或 #14 游戏内验收。
- 不包含 Steam/Unity/游戏 SDK、游戏 DLL、真实存档或凭据。
- 测试夹具中的任意参数不是产品默认值。
