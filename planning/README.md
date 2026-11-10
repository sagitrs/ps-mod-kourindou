# Issue规划与草稿目录

[返回入口](../README.md) · [开发计划](../docs/06-development-plan.md) · [入仓步骤](../docs/08-repository-handoff.md)

17项规划：长期主票GOAL已创建为[Issue #1](https://github.com/sagitrs/ps-mod-kourindou/issues/1)；V1总票、12项首版任务、3项后续任务仍是16份未发布草稿。规划ID不等于平台Issue号；所有产品验收框未勾选。

机读关系见[issues.json](issues.json)。父子关系用于范围归属，依赖用于先后条件；两种图都不表示工作已经开始。

| ID | 标题 | 父票 | 依赖 | 阶段 |
|---|---|---|---|---|
| [GOAL](issues/GOAL.md) | [requirement][umbrella] 香霖堂Mod总目标与V1三角色开发计划 | — | — | tracking |
| [V1](issues/V1.md) | [requirement][umbrella] V1：三角色香霖堂经营与好感养成完整交付 | GOAL | — | tracking |
| [V1-01](issues/V1-01.md) | [requirement][V1-01] 固定首版设计边界与决策记录 | V1 | — | v1-planned |
| [V1-02](issues/V1-02.md) | [requirement][V1-02] 锁定游戏版本并验证高风险适配原型 | V1 | V1-01 | v1-planned |
| [V1-03](issues/V1-03.md) | [requirement][V1-03] 建立工程、按局状态与生命周期基础 | V1 | V1-02 | v1-planned |
| [V1-04](issues/V1-04.md) | [requirement][V1-04] 新增爱丽丝、魔理沙、咲夜及霖之助店铺设定 | V1 | V1-03 | v1-planned |
| [V1-05](issues/V1-05.md) | [requirement][V1-05] 实现独立好感、进度条与买店上限解锁 | V1 | V1-04 | v1-planned |
| [V1-06](issues/V1-06.md) | [requirement][V1-06] 实现地区搬迁背景与实际客流加权 | V1 | V1-04 | v1-planned |
| [V1-07](issues/V1-07.md) | [requirement][V1-07] 实现三人的夜间付费帮助 | V1 | V1-05 | v1-planned |
| [V1-08](issues/V1-08.md) | [requirement][V1-08] 实现每日买家、每日卖家与自然模式 | V1 | V1-05, V1-06 | v1-planned |
| [V1-09](issues/V1-09.md) | [requirement][V1-09] 实现150恋人CG与200结婚CG解锁流程 | V1 | V1-05 | v1-planned |
| [V1-10](issues/V1-10.md) | [requirement][V1-10] 交付三人正式立绘、地区背景、CG与文本 | V1 | V1-01, V1-02 | v1-planned |
| [V1-11](issues/V1-11.md) | [requirement][V1-11] 完成三角色全流程集成与兼容性验收 | V1 | V1-06, V1-07, V1-08, V1-09, V1-10 | v1-planned |
| [V1-12](issues/V1-12.md) | [requirement][V1-12] 制作首版候选包与安装维护交接 | V1 | V1-11 | v1-planned |
| [F-01](issues/F-01.md) | [requirement][后续] 全NPC随机东方化与存档一致性 | GOAL | V1-12 | future-unallocated |
| [F-02](issues/F-02.md) | [requirement][后续] 清理赛博设定并替换为东方世界观内容 | GOAL | V1-12 | future-unallocated |
| [F-03](issues/F-03.md) | [requirement][后续] 最终东方化版本组合验收与交接 | GOAL | F-01, F-02 | future-unallocated |

阶段说明：`tracking`是范围总票；`v1-planned`是首版待实施；`future-unallocated`是后续待分解、未排期。它们不是已创建的远端状态标签。

## 使用方式

主Issue已创建，不重复发布。其他16项先审文档与未决规则，再按获准范围、规范和实际已有工作决定是否创建。发布前将正文相对链接换成目标仓固定提交版本链接，将规划父票/依赖换成真实Issue链接；不要把本地草稿直接当作远端页面已存在。

每个功能票随实现交相称测试和真实游戏复现；独立审阅/需求验收安排由目标仓明确。全量最终目标不计入V1完成条件，V1成功也不结算最终目标。
