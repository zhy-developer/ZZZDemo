# Phase 1 技能数据配置交付与验收

> 验收状态修订（2026-10-09）：Phase 1 尚未整体验收通过。最新矩阵以 [SkillConfig_Phase1_Acceptance_Revision.md](SkillConfig_Phase1_Acceptance_Revision.md) 为准。数据自动检查不能替代 Inspector 人工操作及旧战斗 Play 模式回归；这两部分完成前不申请 Phase 2。下文实现说明和历史测试证据保留。

仅实现数据、校验、安全 JSON 导出与只读加载。未接入战斗，未进入 Phase 2。

## 使用入口

- 目录：`Assets/ScriptableObject/SkillConfigs/MiyabiPhase1.asset`。
- 菜单：`Tools > Skill Config > Phase 1 Validation and Export`，选择目录后校验、保存并导出。
- 原 ComboData Inspector 保留原字段；下方是“Phase 1 技能数据（未接入战斗）”。未登记资产不会自动分配 ID。
- `Tools > Skill Config > Register Miyabi Normal 1-2 Pilot` 是显式登记入口，不覆盖已登记配置。
- 保存 SO 不自动导出。导出窗口响应 Undo/项目变化并定期比较完整 DTO 和依赖摘要。
- 只读入口 `SkillConfigLoader.TryLoad/TryParse`；快照 `CopyData()` 返回独立副本。

## 真实文件清单

修改现有文件：

1. `Assets/Scripts/ScriptableObjectVO/ComboData/ComboData.cs`：仅增加可选 skill 字段。
2. `Assets/ScriptableObject/ComboData/星见雅/Unagi_Normal_1.asset`。
3. `Assets/ScriptableObject/ComboData/星见雅/Unagi_Normal_2.asset`。

新增运行数据代码，位于 `Assets/Scripts/SkillConfig/`：

| 文件 | 职责 |
|---|---|
| SkillAuthoringData.cs | SO 内嵌配置、多 Clip/State、位移绑定选择 |
| SkillTrackData.cs | 轨道、组、形状、窗口、VFX/SFX、HurtBox |
| SkillEventData.cs | 显式参数类型 Registry，无业务执行器 |
| CharacterSkillCatalog.cs | 角色关联、完整性、资源与 HurtBox |
| SkillConfigDto.cs | Schema v1 DTO、诊断、伤害引用解析 |
| SkillConfigValidation.cs | 结构、身份、区间、引用与映射校验 |
| SkillConfigLoader.cs | 版本检查、只读快照、Resources 引用检查 |
| SkillConfigHash.cs | SHA-256 |

新增编辑器代码，位于 `Assets/Editor/SkillConfig/`：

| 文件 | 职责 |
|---|---|
| ComboDataSkillInspector.cs | 旧字段兼容、登记、保存和新 ID 复制 |
| SkillAssetValidator.cs | Clip/State、资源、目录归属、全局 ID、源摘要 |
| RootMotionBindingValidator.cs | 复用旧 RootMotionClip 校验，不调用播放器 |
| SkillConfigFileStore.cs | 稳定序列化、临时文件回读、原子替换 |
| SkillConfigExporter.cs | 单角色/批量导出、待导出状态 |
| SkillValidationWindow.cs | 诊断、定位与导出入口 |
| SkillPilotSetup.cs | 两个真实普攻的显式登记 |
| SkillConfigPhase1Checks.cs | 实际 Unity 资源、序列化、导出与重开检查 |

新增资产：`Assets/ScriptableObject/SkillConfigs/MiyabiPhase1.asset`、`Assets/Resources/SkillConfigs/Character_miyabi_Skills.json`，以及新增 Unity 文件/目录的 .meta。已有 SO、Controller、Clip、旧位移表 GUID 不变。

新增验证：`Tests/SkillConfig/SkillConfig.Tests.csproj`、`Program.cs`、`UnityStubs.cs`、`PrepareUnityHarness.ps1`。文档为本报告、同目录 TestResults.txt 及 `docs/superpowers/plans/2026-10-08-skill-config-phase1.md`。

## 字段与兼容

ComboData.skill 是唯一 SO 内的新数据区，不创建平行 SkillDefinition SO。registered 为显式登记开关；SkillID 在登记时生成/赋值，改名不改变。复制按钮生成新身份；原生复制带来重复身份则报错，不自动重写旧 ID。

SkillType 为 Unspecified=0、Normal=1、Ultimate=2。旧 AttackStyle 保留旧语义，不自动映射；组织分类不决定类型。

animations 支持多个连续片段，每片段关联多个完整 State 路径。初始片段包含旧 comboName 入口，后续片段不受一一对应限制。逻辑区间为整数帧 [start,end)，源时间为微秒，速度为千分比。时长按源 Trim/速度向上取整到 Tick。

HitGroup 选择基础伤害时读取旧 _comboDamage，不存另一份手工同步数值。显式组伤害属于另一模式。Phase 1 拒绝负值、非整数、NaN/Infinity 和超 int 的基础伤害，不静默舍入。

目录只保存关联及角色级数据。PartialPilot=0、battleReady=false 防止把两技能当成完整角色配置。即使设为 Complete，Phase 1 也不声称可战斗；requireBattleReady=true 必须失败。

Unity 内联序列化不能可靠以 null 表示可选项，故增加 hurtBoxConfigured、rootMotionBound、EventParameterKind。缺省参数对象不会错误地激活功能。事件类型化参数有注册/版本验证，无事件执行。

旧字段、新轨道、组、事件、目录关联及资源依赖均参与待导出检查。CharacterID 冲突不区分大小写，避免 Windows 文件覆盖。导出只保存相关目录和技能，不自动保存其他资产。

## 真实试点关联

| 项目 | Normal1 | Normal2 |
|---|---|---|
| SkillID | miyabi-normal-1 | miyabi-normal-2 |
| SO GUID | 3608c4587e7146d4b8b9f78174009635 | e79851970ca93f64f9a3ebb0c13b8c3e |
| State | Base Layer.Combo.Normal Attack.Unagi_Normal_1 | Base Layer.Combo.Normal Attack.Unagi_Normal_2 |
| Clip local ID | 572028686729525778 | -3972385943832332197 |
| FBX GUID | 3d9757a43f4c1d14ebf43cbec50dcda5 | 同左 |
| Unity 导入长度 | 657253微秒 | 583333微秒 |
| 动画区间 | [0,20) | [0,18) |
| 位移 JSON GUID | e01b382748b39154580dfff8466b6701 | 3cd07edeb3b770a459a80511451ed102 |
| 位移长度/样本 | 657ms / 20 | 583ms / 18 |
| 速度/Trim/距离 | 1倍/完整 Clip/1000‰ | 同左 |
| 新类型 | Normal | Normal |

Clip 仍由 Animator 引用，不在 Resources。真实 Unity API 已验证 State 的 Motion、Clip 身份及旧表。亚毫秒差异按1ms容差处理。20/18是新数据试点时间轴，不改变现有 Animator 实际退出/切招规则。

没有猜测命中盒、受击盒、命中帧、连招窗口或特效音效参数；相关配置为空并输出明确 Warning。

## Root Motion

旧 JSON 字段、数据和播放器未变。分别记录 sourceClipLengthMs、coverageEndMs、旧 endFrameExclusive；短覆盖可对应静止尾段，不要求位移总时长等于完整动画。

有效覆盖超过源动画可用时间（超过1ms容差）、不同源 Clip、非法样本/倍率/结束帧是 Error。缺少烘焙源 GUID/版本/缩放证据是 Warning。短覆盖且尾段用途未确认时另报 Warning。

Trim/速度变化产生 requiresTimeRemap=true 和 MOTION_REMAP_REQUIRED，且配置禁止战斗消费；绝不声称旧播放器已支持。后续需在获批阶段按旧表累计位置/采样时间做整数重映射：逻辑 Tick 映射源时间，前后累计位置求差，统一舍入、余数、末样本和多 Clip 拼接。保留旧格式；样本间插值不声称恢复原曲线全部细节。

## 导出示例与诊断

完整文件见 `Assets/Resources/SkillConfigs/Character_miyabi_Skills.json`。以下仅为字段摘录：

```json
{
  "schemaVersion": 1,
  "characterId": "miyabi",
  "logicFrameIntervalMs": 33,
  "precision": 10000,
  "completeness": 0,
  "battleReady": false,
  "hurtBoxConfigured": false,
  "skills": [
    { "skillId": "miyabi-normal-1", "skillType": 1, "baseDamage": 20, "durationFrames": 20 },
    { "skillId": "miyabi-normal-2", "skillType": 1, "baseDamage": 20, "durationFrames": 18 }
  ]
}
```

试点导出为0 Error、6 Warning：PARTIAL_CATALOG 1项、HURTBOX_UNCONFIGURED 1项、NO_HITBOX 2项、MOTION_METADATA 2项。Warning 随 JSON 保存。Error 不触碰旧正式文件；临时文件回读验证后才原子替换，没有先删旧文件再写的降级路径。

## 测试结果与限制

执行证据摘要见 `docs/SkillConfig_Phase1_TestResults.txt`。

| 项目 | 状态 | 执行方式 |
|---|---|---|
| 结构、区间、五形状参数、多 Clip/State、事件 | Pass | .NET 自动化；是配置校验，不是命中检测 |
| SkillID 改名、保存、重新导入 | Pass | 真实 Unity 隔离工程 |
| 重复 ID、大小写角色目录冲突 | Pass | 真实 Unity 隔离工程 |
| 基础伤害不复制、整数上界拒绝 | Pass | 独立检查及 Unity SerializedObject |
| 旧伤害/入口/位移参数/新字段待导出 | Pass | 真实 Unity 隔离工程 |
| 两技能 SO → State → Clip → 旧 JSON | Pass | 原始 GUID 资源副本、真实 Unity API |
| 丢失 Clip/Root Motion 引用 | Pass | 真实 Unity 校验器 |
| Warning 导出、非法导出保护 | Pass | 独立检查及真实 Unity |
| 写入/回读/替换失败保护 | Pass | .NET 对实际临时文件注入异常、逐字节比较 |
| SchemaVersion、损坏 JSON、数据回读 | Pass | 独立检查及 Unity JsonUtility |
| Resources 加载、部分目录拒绝战斗消费 | Pass | 真实 Unity 隔离工程 |
| 新进程重开 SO、ID、事件、无位移配置 | Pass | 第二个 Unity 进程从磁盘读取 |
| 原完整工程编译/Inspector 人工交互 | Blocked | 原工程在用户实例打开；没有关闭用户编辑器 |
| 旧战斗进入、普攻/连招、位移、异常、JSON 未启用行为 | Blocked / 待手工 | Phase 1 必要回归门槛，必须在原完整工程 Play 模式验证；没有战斗接入修改不等于无需回归 |
| 新命中系统双端一致性 | 未执行 | 新系统尚未实现，不冒充旧战斗回归结果 |

Unity 版本2022.3.62f2c1。隔离工程复制本阶段源码、原 ComboData、控制器、FBX/Avatar 和旧表；只为无关 Cinemachine 命名空间、精度常量、声音枚举提供编译依赖，不运行战斗。独立检查的 UnityStubs 不作为 Unity 引擎测试证据。

测试曾实际发现并修复 Unity 空对象序列化问题。隔离实例成功输出检查结果后停在批处理退出流程，只结束本任务隔离进程，再启动新进程验证持久化；正常交互关闭流程未测试。

源摘要包含 Unity 资源依赖 Hash。原完整工程首次导入后，若因依赖环境不同显示待导出，请用菜单校验再导出；不会绕过检查假报最新。

## 回退和阶段边界

移除新增 SkillConfig 目录/JSON/测试及对应 meta，恢复 ComboData 新字段和两个试点资产的 skill 数据即可。原伤害等字段值、SO GUID、容器引用、Animator 和旧位移表保留。不要对工作区整体 reset/clean，以免清除用户原有未提交文件。本轮未创建提交。

停止在 Phase 1。Phase 2 Timeline/Graph/Scene View、战斗事件执行、命中判定、对象池、播放器适配均未开始。
