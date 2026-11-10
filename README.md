# ps-mod-kourindou
kourindou mod of game probably stolen.

## 香霖堂：东方经营扩展（Mod显示名暂定）

面向 **Probably Stolen — Cyberpunk Shopkeeper Sim**（Steam App 4348910）的设计与开发计划。代码仓为本仓；Mod ID、发布版本号及许可证尚未确定。**V1表示首个交付范围，不等于已决定发布1.0.0。**

**当前是设计/计划和离线文档校验，不是可安装Mod。** [主Issue #1](https://github.com/sagitrs/ps-mod-kourindou/issues/1)已创建；其余16项仍是仓内草稿，未开票或派工。没有产品代码、游戏编译或游戏内验收。

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
| [入仓与开票](docs/08-repository-handoff.md) | 当前主票与后续子Issue发布边界 |
| [Issue规划目录](planning/README.md) | 17项规划：1张真实主Issue、16份未发布草稿 |

## 技术现状

原生Mod支持C#、自定义UI、按局ModData及夜间地点等。新角色身份/立绘、白天背景和确定性来访仍需目标游戏版本验证；不能把换头像当成新增角色。

推荐Linux侧构建/逻辑测试、Windows正版客户端验收。游戏版本、SDK/框架与合法程序集尚未锁定。不把游戏DLL、游戏素材、真实存档或凭据放入代码仓和分发包。

## 文档校验

Python 3.11或更高版本，仅标准库；从仓根运行：

```sh
python3 tools/docs/check_docs.py
python3 tools/docs/test_check_docs.py
```

本仓测试工具根为 `tools/docs/`，使用方式、断言范围、负例和失效条件见[工具索引](tools/docs/README.md)。这些命令只检查文档/规划结构，不运行游戏、不完成20项产品验收，不证明设计决策已经获得批准。此笔不添加CI工作流，不能把没有远端检查写成CI通过。

<!-- test-tool-root: tools/docs/ -->
<!-- test-tool-index: tools/docs/README.md -->
