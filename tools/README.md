# 工具索引

[返回入口](../README.md)

本仓库的唯一工具根为 `tools/`，当前包含两个子目录：

| 子目录 | 用途 | 索引 |
|---|---|---|
| `tools/docs/` | 离线文档/Issue 规划结构检查与自测；不运行游戏，不是产品验收 | [文档检查](docs/README.md) |
| `tools/ci/` | 游戏无关核心的验证脚本、TRX 执行守卫与 GitHub Actions 候选 | [纯核心 CI 候选](ci/README.md) |

边界：工具根只覆盖文档结构检查与游戏无关核心验证；不包含游戏程序集、Steam/Unity/游戏运行、真实存档或产品 AC01—AC20 验收。本仓已启用纯核心 `pure-core` CI（`.github/workflows/pure-core.yml`），运行文档检查/自测、CI 守卫自测与纯核心 wrapper；该 CI 绿不等于游戏或产品验收通过。
