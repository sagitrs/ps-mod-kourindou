# ps-mod-kourindou
kourindou mod of game probably stolen.

## 香霖堂：东方经营扩展（Mod显示名暂定）

面向 **Probably Stolen — Cyberpunk Shopkeeper Sim**（Steam App 4348910）的设计与开发计划。代码仓为本仓；开发期 Mod ID 为 `sagitrs.kourindou`，C# namespace 为 `Sagitrs.Kourindou`，开发标识 `0.1.0-dev`；原创新代码与自有文档采用 [MIT](LICENSE)，正式发布版本号仍未确定。**V1表示首个交付范围，不等于已决定发布1.0.0。**

**当前是设计/计划、离线文档校验与游戏无关核心开发，不是可安装Mod。** 已建立原生Issue层级：[主票 #1](https://github.com/sagitrs/ps-mod-kourindou/issues/1) → [V1总票 #3](https://github.com/sagitrs/ps-mod-kourindou/issues/3) → 12张开发子票（#4—#15）。其余3项长期任务仍为草稿；已派工V1-03游戏无关核心切片，没有可加载Mod、游戏编译或游戏内验收。

## 产品范围

玩家扮演森近霖之助，初始店名为「香霖堂」。首版只新增三人，不用她们替换掉原NPC：

| 原城区 | 东方地区设定 | 新增角色 |
|---|---|---|
| 下城区 | 魔法森林 | 爱丽丝 |
| 中城区 | 人里 | 魔理沙 |
| 上城区 | 雾之湖 | 咲夜 |

V1保留其他NPC、警局和革命军等其他派系及主体剧情，允许世界观混搭。同时保留：城区派系好感100后搬迁换背景并提高当地角色来访概率；三人独立好感、100后的夜间付费帮助和每日来访模式；买店后开放100—200，150/200分别解锁恋人/结婚CG。

**最终目标另算：所有NPC随机替换为东方角色，并清理赛博设定。首版暂留原内容不等于终版永久保留，也不把终版的大改强塞给V1。**

## 阅读入口

| 文档 | 用途 |
|---|---|
| [产品设计](docs/01-product-design.md) | 已定需求、首版边界和玩家流程 |
| [架构与存档](docs/02-architecture.md) | 模块职责、适配、身份和保存安全 |
| [待定决策](docs/03-decisions.md) | 已确认输入与未定玩法，不替用户批准推荐值 |
| [美术与内容](docs/04-content.md) | 三人内容、12项基础美术、版权与制作规范 |
| [验收方案](docs/05-validation.md) | 游戏功能边界与真实验收要求，当前未执行 |
| [开发计划](docs/06-development-plan.md) | 阶段、依赖与长期路线 |
| [接口证据](docs/07-evidence.md) | 公开资料、版本差异与未证明能力 |
| [入仓与开票](docs/08-repository-handoff.md) | 已创建层级与后续发布边界 |
| [Issue规划目录](planning/README.md) | 17项规划：14张真实Issue、3份未发布草稿 |

## 技术现状

原生Mod支持C#、自定义UI、按局ModData及夜间地点等。新角色身份/立绘、白天背景和确定性来访仍需目标游戏版本验证；不能把换头像当成新增角色。

推荐Linux侧构建/逻辑测试、Windows正版客户端验收。纯核心已固定 .NET 10 SDK（见 `global.json`）；目标游戏版本、Unity/游戏 SDK 与合法程序集仍未锁定。不把游戏DLL、游戏素材、真实存档或凭据放入代码仓和分发包。

## 游戏无关核心（开发中）

`src/Kourindou.Core` 与 `tests/Kourindou.Core.Tests` 是不依赖游戏程序集的纯核心/单元测试切片，目前覆盖稳定角色身份、按槽状态版本边界、坏数据诊断与序列化保留。它**不是可安装Mod**，不证明原生 `IMod` 已加载、角色已到场、真档兼容或任何产品AC通过。

构建/测试使用 `global.json` 固定的 .NET SDK 10.0.404：

```sh
dotnet build Kourindou.sln
dotnet test Kourindou.sln
```

游戏真实适配仍由后续任务在合法固定构建、程序集引用和测试客户端到位后执行。

## 文档校验

Python 3.11或更高版本，仅标准库；从仓根运行：

```sh
python3 tools/docs/check_docs.py
python3 tools/docs/test_check_docs.py
```

本仓测试工具根为 `tools/`，使用方式、断言范围、负例和失效条件见[工具索引](tools/README.md)。这些命令只检查文档/规划结构或游戏无关核心，不运行游戏、不完成20项产品验收，不证明设计决策已经获得批准。当前没有已启用的远端CI，不能把没有远端检查写成CI通过。

<!-- test-tool-root: tools/ -->
<!-- test-tool-index: tools/README.md -->
