# 接口证据与未证明事项

本文记录2026-11-10整理时已读取的公开资料，不是对未来游戏版本的永久兼容保证。公开示例没有在本任务中执行、编译或导入游戏。

## 1. 目标游戏

- [Steam商店](https://store.steampowered.com/app/4348910/)：Probably Stolen — Cyberpunk Shopkeeper Sim，App 4348910。
- [Steam详情接口](https://store.steampowered.com/api/appdetails?appids=4348910&l=english&cc=US)：正式列Windows平台，不据第三方报告称官方支持Linux。
- [100-Rev13官方公告](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/1848664643353154)：描述计划于2026-12-10完整合入的Mod安全更新。实施时须再核实际构建与新公告，不能把计划当作已发生。

## 2. 游戏Mod指南

阅读入口：[制作指南](https://github.com/dragonpan2/probably-stolen-modding)。本次使用的固定Git blob如下；blob摘要不是commit摘要。

| 文件 | 固定取源 |
|---|---|
| 中文制作指南 | [f49e20926a3d2a492284313e0858f1924d5077a0](https://api.github.com/repos/dragonpan2/probably-stolen-modding/git/blobs/f49e20926a3d2a492284313e0858f1924d5077a0) |
| 中文API变化 | [4429305a644a3cc7a6799f2ec9a47ebacd1f8df6](https://api.github.com/repos/dragonpan2/probably-stolen-modding/git/blobs/4429305a644a3cc7a6799f2ec9a47ebacd1f8df6) |
| 中文行为准则 | [5a36bacd1e2b7df603765ae92cb922a8f8e9062f](https://api.github.com/repos/dragonpan2/probably-stolen-modding/git/blobs/5a36bacd1e2b7df603765ae92cb922a8f8e9062f) |
| LearnerMod项目文件 | [74480f233e489034f2b451150fea86880c69e827](https://api.github.com/repos/dragonpan2/probably-stolen-modding/git/blobs/74480f233e489034f2b451150fea86880c69e827) |

文档支持的设计依据：
- 原生`IMod`、manifest、游戏自身加载；正式版路线不应照搬Demo/MelonLoader。
- ModData按局保存，主菜单阶段不可当成已有存档；关闭Mod通常保留其数据。
- 自定义UI有进度条、图片、选项等，夜间地点有扩展入口。
- 顾客生成钩子交付StoreClientManager；不因此自动拥有新阵营/立绘的稳定注册API。
- 文档明确新顾客精灵图、新阵营、改既有UI/核心布局不在稳定支持面。Harmony能尝试游戏代码适配，但版本维护责任在Mod作者。
- 游戏文件助手、ModData可减少权限需求；不用扫描绕过、动态代码或外部反射来弥补设计缺口。

安全过渡中被描述将退出的能力包括`dynamic-code`、`native`、`reflection-external`、`threads`。指南可能提前描述尚未在用户构建出现的方法，必须实测。扫描通过也不证明玩法正确或形成操作系统安全沙箱。

## 3. 社区参考，不是本项目验收

[Portrait Framework](https://github.com/dbfoxtw/probably-stolen-mods/tree/main/mods/portrait-framework)提供既有立绘替换参考：
- [README固定blob](https://api.github.com/repos/dbfoxtw/probably-stolen-mods/git/blobs/a356d8d297b41b386278a2f68685b28236894ea4)
- [Patches.cs固定blob](https://api.github.com/repos/dbfoxtw/probably-stolen-mods/git/blobs/5468cc00aa5c3d40fd9af8b8674fd99d16e54153)

它通过SpriteDict相关方法换图，声明支持REV6/REV9；本项目没有据此确认REV13或用户构建兼容。它自身不支持简单续编号增加脸，也不提供独立好感、派系或每日计划。其图包仍需manifest和轻量入口DLL，不是任意PNG目录都自动可加载。

指南/LearnerMod示例目标为netstandard2.1，另一已读社区项目使用net48。这个差异是需要解决的输入，不是同时承诺两套目标都支持。

[ProtonDB](https://www.protondb.com/app/4348910)的其他玩家报告只支持考虑独立Linux运行试验，不证明本服务器、当前构建或本Mod可运行。本计划默认优先Windows客户端验收。

## 4. 二创边界

[东方官方二创指南](https://touhou-project.news/guidelines_en/)要求明确二创性质，并限制原作提取内容及未经许可使用其他同人作品。游戏Mod准则也禁止重新分发本体代码/素材。代码许可证与资源许可分开核验；平台可上传不代表所有权利已满足。

## 5. 当前证据缺口

没有目标游戏程序集、SDK/框架锁定、实际编译、扫描输出、店面对象验证、三角色来访、买店/日程事件、存档停用测试或正式美术。不能把本文件、社区作者运行声明或逻辑桩测试当作上述缺口已经补齐。

各缺口由V1-02及后续功能票交真实证据。若目标版本不提供所需接口，交用户选择适配、等待或明确调整范围，而不是静默删除需求。
