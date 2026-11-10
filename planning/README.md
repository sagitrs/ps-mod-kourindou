# Issue规划与草稿目录

[返回入口](../README.md) · [开发计划](../docs/06-development-plan.md) · [入仓记录](../docs/08-repository-handoff.md)

17项规划中，GOAL、V1及12项V1开发任务已对应14张真实Issue；F-01/F-02/F-03仍是未发布草稿。规划ID不等于平台Issue号；所有产品验收框未勾选。

原生层级为 **GOAL #1 → V1 #3 → #4—#15**。父票接口及两层子票列表已回读，见[主票建票记录](https://github.com/sagitrs/ps-mod-kourindou/issues/1#issuecomment-6763459847)。本次只建票，不派工；原设计PR合入不意味着V1或各子票已完成。

机读关系见[issues.json](issues.json)。父子关系用于范围归属，依赖用于先后条件；两种图都不表示工作已经开始。

| ID/规划正文 | 真实Issue | 标题 | 父票 | 依赖 | 阶段 |
|---|---|---|---|---|---|
| [GOAL](issues/GOAL.md) | [#1](https://github.com/sagitrs/ps-mod-kourindou/issues/1) | 总目标与V1三角色开发计划 | — | — | tracking |
| [V1](issues/V1.md) | [#3](https://github.com/sagitrs/ps-mod-kourindou/issues/3) | 三角色香霖堂经营与好感养成完整交付 | GOAL | — | tracking |
| [V1-01](issues/V1-01.md) | [#4](https://github.com/sagitrs/ps-mod-kourindou/issues/4) | 固定首版设计边界与决策记录 | V1 | — | v1-planned |
| [V1-02](issues/V1-02.md) | [#5](https://github.com/sagitrs/ps-mod-kourindou/issues/5) | 锁定游戏版本并验证高风险适配原型 | V1 | V1-01 | v1-planned |
| [V1-03](issues/V1-03.md) | [#6](https://github.com/sagitrs/ps-mod-kourindou/issues/6) | 建立工程、按局状态与生命周期基础 | V1 | V1-02 | v1-planned |
| [V1-04](issues/V1-04.md) | [#7](https://github.com/sagitrs/ps-mod-kourindou/issues/7) | 新增三角色及霖之助店铺设定 | V1 | V1-03 | v1-planned |
| [V1-05](issues/V1-05.md) | [#8](https://github.com/sagitrs/ps-mod-kourindou/issues/8) | 独立好感、进度条与买店上限解锁 | V1 | V1-04 | v1-planned |
| [V1-06](issues/V1-06.md) | [#9](https://github.com/sagitrs/ps-mod-kourindou/issues/9) | 地区搬迁背景与实际客流加权 | V1 | V1-04 | v1-planned |
| [V1-07](issues/V1-07.md) | [#10](https://github.com/sagitrs/ps-mod-kourindou/issues/10) | 三人的夜间付费帮助 | V1 | V1-05 | v1-planned |
| [V1-08](issues/V1-08.md) | [#11](https://github.com/sagitrs/ps-mod-kourindou/issues/11) | 每日买家、每日卖家与自然模式 | V1 | V1-05, V1-06 | v1-planned |
| [V1-09](issues/V1-09.md) | [#12](https://github.com/sagitrs/ps-mod-kourindou/issues/12) | 150恋人CG与200结婚CG解锁流程 | V1 | V1-05 | v1-planned |
| [V1-10](issues/V1-10.md) | [#13](https://github.com/sagitrs/ps-mod-kourindou/issues/13) | 正式立绘、地区背景、CG与文本 | V1 | V1-01, V1-02 | v1-planned |
| [V1-11](issues/V1-11.md) | [#14](https://github.com/sagitrs/ps-mod-kourindou/issues/14) | 全流程集成与兼容性验收 | V1 | V1-06, V1-07, V1-08, V1-09, V1-10 | v1-planned |
| [V1-12](issues/V1-12.md) | [#15](https://github.com/sagitrs/ps-mod-kourindou/issues/15) | 首版候选包与安装维护交接 | V1 | V1-11 | v1-planned |
| [F-01](issues/F-01.md) | 未创建 | 全NPC随机东方化与存档一致性 | GOAL（规划） | V1-12 | future-unallocated |
| [F-02](issues/F-02.md) | 未创建 | 赛博设定清理与东方世界观替换 | GOAL（规划） | V1-12 | future-unallocated |
| [F-03](issues/F-03.md) | 未创建 | 最终版本组合验收与交接 | GOAL（规划） | F-01, F-02 | future-unallocated |

阶段说明：`tracking`是范围总票；`v1-planned`是首版待实施；`future-unallocated`是后续待分解、未排期。它们不是远端状态标签，也不是完成比例。

## 使用方式

已有14张Issue不重复发布；后续进展以各真实Issue为准，本文档保留规划与编号导航。远端正文的父票、依赖均使用真实Issue链接，设计文档链接固定到开票时的已合入提交。

F三项仍需后续授权和详细分解；发布前核已有工作、规则与权限，不把本地草稿当作远端页面已存在。每个功能票随实现交相称测试和真实游戏复现；独立审阅/需求验收安排由本仓明确。全量最终目标不计入V1完成条件，V1成功也不结算最终目标。
