# Phase 1 验收方案修订与执行记录

日期：2026-10-09。范围：只修订测试/验收文档，执行数据检查与只读审查；不增加编辑器功能，不改产品代码或资产，不进入 Phase 2。

**当前阶段结论：Phase 1 尚未整体验收通过。** 必须同时满足数据功能验收、原工程 Inspector 验收及旧战斗 Play 模式回归，才可申请下一阶段。Warning 数量、静态 diff、独立 .NET 检查都不能单独证明成功。

## 测试环境与证据范围

- 独立检查：`dotnet run --project Tests/SkillConfig/SkillConfig.Tests.csproj`。56 项断言通过，使用 UnityStubs，不覆盖 Unity 的原生序列化、GUID、Inspector、Resources 或 Play 模式。
- 真实 Unity：2022.3.62f2c1，隔离工程 `Temp/SkillConfigAcceptanceRevision`；复制真实源码、ComboData/Controller/Clip/Avatar/Root Motion 及原 GUID。只补无关编译依赖，不运行战斗场景。
- `SkillConfigPhase1Checks.RunAcceptanceRevision` 重新执行既有25项 Unity 检查，再执行57项补充断言；包含真实 AssetDatabase、JsonUtility、Resources 和文件写入。断言数量不等于独立用户场景数量。
- 另启动 Unity 进程执行 `SkillConfigPhase1Checks.VerifyReopen`，验证磁盘保存数据；不把单次反序列化当作关闭重开。
- 未控制用户原工程的 Unity 窗口，未启动它的 Play 模式。原工程编译、Inspector 按钮与真实战斗均需手工检查。
- 只在测试副本构造非法配置、复制测试资产和改动目录；finally 恢复并清理副本。没有把本轮测试导出的 JSON 或测试资产复制回原工程。

## 最新验收矩阵

状态为 Pass / Fail / Blocked。Blocked 表示尚未实际完成，不算通过。

| ID | 用例与预期 | 执行层 | 当前状态 / 证据 |
|---|---|---|---|
| N01 | 结构、版本、映射、组/事件参数、文件故障保护等56项检查 | 独立 .NET | Pass；DotNet 日志及摘要 |
| U01 | 本阶段源代码在 Unity 编译，真实两试点关联校验 | 隔离 Unity | Pass；PHASE1_PASS pilot real asset validation；不代表完整工程编译 |
| U02 | SO、SkillID、目录保存后在新进程读回 | 隔离 Unity 新进程 | Pass；PHASE1_REOPEN_PASS |
| U03 | 原 State/Clip、GUID/local ID、Root Motion 绑定；丢失 Clip/位移引用报错 | 隔离 Unity | Pass；real asset validation / missing actual Clip / Root Motion |
| U04 | Resources 加载，部分目录拒绝作为完整战斗配置 | 隔离 Unity | Pass；Resources load / battle configuration rejected |
| U05 | Unity JsonUtility 版本未知、损坏 JSON 拒绝；正常回读 | 隔离 Unity | Pass；unknown schema / malformed input / roundtrip |
| U06 | 类型化事件、可选无位移字段序列化，保存后新进程读回 | 隔离 Unity | Pass；typed event / no-motion / REOPEN |
| E01 | StartFrame = EndFrame → WINDOW Error，JSON/meta 不变 | 隔离 Unity 实际 Export | Pass；start-equals-end |
| E02 | StartFrame > EndFrame → WINDOW Error，JSON/meta 不变 | 隔离 Unity 实际 Export | Pass；start-after-end |
| E03 | PlaybackSpeed = 0 → ANIMATION_TIME Error，JSON/meta 不变 | 隔离 Unity 实际 Export | Pass；zero-speed |
| E04 | 目录内两个不同技能同 SkillID → SKILL_ID Error，JSON/meta 不变 | 隔离 Unity 实际 Export | Pass；duplicate-ID-inside-catalog |
| E05 | HitBox 指向不存在的 HitGroup → GROUP_REF Error，JSON/meta 不变 | 隔离 Unity 实际 Export | Pass；unknown-HitGroup |
| E06 | 已选 LegacyCombo 但必需位移引用为空 → ROOT_MISSING Error | 隔离 Unity 实际 Export | Pass；missing-required-motion，且 JSON/meta 不变 |
| E07 | VFX 事件引用不存在的必需资源 ID → RESOURCE_REF Error | 隔离 Unity 实际 Export | Pass；missing-required-VFX，且 JSON/meta 不变 |
| E08 | 已登记必需 VFX 的资源对象为空 → RESOURCE_TYPE Error | 隔离 Unity 实际 Export | Pass；invalid-required-resource-asset，且 JSON/meta 不变 |
| E09 | 有效配置带已知 Warning 能写出、回读并加载 | 隔离 Unity | Pass；依据实际导出/加载结果，不以 Warning 数量作为断言 |
| E10 | 写入、回读、替换注入故障均保留旧文件 | 独立 .NET + 实际文件系统 | Pass；不可描述为 Unity 引擎测试 |
| M01 | Normal1 的20→19专项：以实际源 Clip/Trim/速度算时长 | 隔离 Unity | Pass；当前资源产生 ANIMATION_DURATION；不是通用必错规则 |
| ID01 | 将另一份重复 ID 的 ComboData 资产加入目录后拒绝导出 | 隔离 Unity 实际 Export | Pass；duplicate-asset-added-to-catalog，JSON/meta 不变 |
| ID02 | 已登记但不在当前目录的重复资产仍被找到 | 隔离 Unity | Pass；registered duplicate outside catalog |
| ID03 | 未登记且在目录外的 ComboData 不参加全局 SkillID 比较 | 隔离 Unity | Pass；unregistered external asset intentionally outside ID scan |
| ID04 | 原技能改名、保存、重新导入/重开后 ID 不变 | 隔离 Unity | Pass；rename/save/reimport 与 REOPEN |
| ID05 | 实际点击“复制为新技能”，新 ID 不同且保存重开后稳定 | 原工程 Inspector | **Blocked / 待手工**；未把测试代码生成 GUID 等同于点击按钮 |
| S01 | 改旧 _comboDamage → 待导出，恢复 → 最新 | 隔离 Unity | Pass；legacy-base-damage，修改后配置仍合法 |
| S02 | 改旧 _comboName 为同一入口完整状态路径 → 待导出 | 隔离 Unity | Pass；legacy-entry-full-state-path，修改后配置仍合法 |
| S03 | 改旧 rootMotion.distancePermille → 待导出 | 隔离 Unity | Pass；legacy-root-distance，修改后配置仍合法 |
| S04 | 改旧 rootMotion.json 引用 → 待导出，恢复 → 最新 | 隔离 Unity | Pass；legacy-root-reference；错误源同时被校验拒绝 |
| S05 | 修改 CharacterSkillCatalog.skills 引用列表 → 待导出 | 隔离 Unity | Pass；catalog-reference-list，移除第二技能后部分目录仍合法 |
| S06 | 改新动画 EndFrame → 待导出，恢复 → 最新 | 隔离 Unity | Pass；new-animation-endpoint |
| I01 | 原完整工程编译、Inspector 旧字段及新字段交互正常 | 原工程 | **Blocked / 待手工** |
| I02 | 手工编辑 S01～S06 后界面提示、保存、Undo 和重新导出正确 | 原工程 Inspector/校验窗 | **Blocked / 待手工**；自动 IsStale 通过不等于 UI 已测 |
| B01 | 原角色正常进入游戏 | 原工程 Play | **Blocked / 待手工** |
| B02 | Normal1、Normal2 正常触发 | 原工程 Play | **Blocked / 待手工** |
| B03 | 原 Animator 切换及普攻连招与修改前基线一致 | 原工程 Play | **Blocked / 待手工** |
| B04 | 原 Root Motion 位移与修改前基线一致 | 原工程 Play | **Blocked / 待手工** |
| B05 | 无新增 MissingReference、NullReference、技能调用异常 | 原工程 Play/Console | **Blocked / 待手工** |
| B06 | 新技能 JSON 未接入时，原战斗行为保持不变 | 原工程 Play/A-B比较 | **Blocked / 待手工**；无调用方静态证据不能替代动态回归 |

本轮已执行检查无失败项；Blocked 项仍阻止 Phase 1 整体验收。

## 重复 SkillID 的准确范围

依据 `SkillAssetValidator.Build` 与 `SkillConfigValidation.Validate`：

1. 当前目录所有成功构造的 SkillDto 使用 Ordinal 比较 SkillID，缺失/重复报 SKILL_ID。同资产重复引用也会产生冲突。
2. `AssetDatabase.FindAssets("t:ComboData")` 不指定搜索文件夹，检查 AssetDatabase 能发现的该类型资产。仅将 `skill.registered=true` 且不在当前目录中的资产 ID，与当前目录 ID 比较，冲突报 GLOBAL_SKILL_ID。
3. 这不是任意两个无关目录外资产之间的全项目成对查重；它是“当前目录内部 + 当前目录对已登记外部资产”的校验。
4. 尚未保存到 AssetDatabase 的内存对象不在全局扫描内；未登记的目录外旧资产被跳过。未登记资产若加入当前目录，则报 UNREGISTERED，不被当作合法技能。
5. Inspector 复制按钮代码创建新 GUID，但按钮真实操作/保存对话框/副本重开必须按 ID05 手工验收，不能用静态代码证明通过。

## 非法导出步骤与专项区别

手工测试前备份正确 JSON 与 meta，记录 SHA-256；每次仅改一个条件，点击 Export 后核对预期 Error 和文件 Hash，再恢复配置。E01～E08 是明确非法结构/必需引用，不依赖视觉动画时长推断。

M01 独立处理：当前实际 Normal1 源长度约657253微秒，Trim 为0～657253、速度1000‰、Tick为33000微秒，ceil(657253/33000)=20。所以当前配置20→19产生 ANIMATION_DURATION。若同时修改 Trim、速度或资源，必须重新计算，不能继续套用“19必错”。该错误由动画时间映射规则产生，不应描述为已经证实 Root Motion 曲线不匹配；旧表657ms与源长度的亚毫秒差异在量化容差内。

## 必须手工执行的原工程检查

先保存现有工作。不要在 Play 中编辑试点数据；不要关闭或覆盖未保存场景。保持角色、起点、输入顺序和网络条件尽量相同，并记录所用版本。

### I01 / ID05 / I02

1. 等待原工程脚本编译结束，记录 Console 编译错误；没有新增错误才继续。
2. 打开 Normal1，确认旧伤害、动画入口和 rootMotion 可见，原字段未丢失，新区明确标为未接入战斗。
3. 点击“复制为新技能（新 SkillID）”，将副本存到专用测试目录；记录原 ID 与新 ID，二者必须不同。保存，重新打开 Unity，再检查新 ID 不变。验证后只清理这份测试副本，不能删除原技能。
4. 在校验窗选择 MiyabiPhase1，逐项验证 S01～S06：修改→待导出，恢复→最新。对合法改动另验证保存/导出后最新；对非法改动验证 Error 与旧 JSON 保护。用 Undo 恢复后也要核查状态。
5. 修改目录引用列表可临时移除 Normal2，再恢复。确认没有永久丢失两个试点引用。

### B01～B05：旧战斗回归

1. 按项目原有登录/进入对战流程运行。BuildSettings 中的现有场景为 LoginScene → MainScene → BattleScene；不要直接从未初始化的 BattleScene 启动并把初始化错误误判为本次回归。
2. 使用原来能正常运行的服务端/客户端环境进入游戏并选择星见雅。确认角色实例、显示、移动正常（B01）。
3. 单次普攻和连续按普攻，观察 Animator 当前状态，确认触发 Unagi_Normal_1、Unagi_Normal_2（B02）。保留截图或录屏。
4. 按修改前相同节奏连续攻击，比较 Animator 切换、连招衔接和结束回待机行为（B03）。未知旧问题应单列，不把它当成本轮新增，也不能在无基线时声称完全一致。
5. 固定起点和朝向，观察原普攻位移；与修改前录像/基线版本比较，不应出现新的不移动、重复位移或异常回跳（B04）。这不是确定性双端位移测试的替代。
6. 记录运行前已有 Console 问题，执行上述流程后检查新增 MissingReferenceException、NullReferenceException、缺失动画/技能调用错误（B05）。保存相关堆栈、Editor.log 和发生步骤；只观察 Console 没红字不代表已经比较过所有旧行为。

### B06：JSON 未接入的 A/B 检查

当前只读检索未发现 Game/FrameSync/Net 调用 SkillConfigLoader/SkillConfigDto，但仍要动态回归。先在新 JSON 存在、没有任何额外接线的状态执行 B01～B05。退出 Play 后，可在 Unity Project 中把**新角色技能 JSON**临时移到 Resources 之外的专用测试目录（使用 Unity 移动保留 meta），重走相同流程，再恢复原路径。不要移动任何旧 Root Motion JSON。

比较两次角色进入、Normal1/2、连招和位移，结果应一致。另与 Phase 1 前的已知正常版本/录像比较，避免“有无 JSON 两次都坏了”仍被当作兼容性通过。若没有旧基线，B06 的跨版本一致性结论保持待确认，不强行判 Pass。

手工记录建议：`ID | Pass/Fail/Blocked | Unity/项目版本 | 场景与操作 | 实际结果 | Console/录像路径`。出现异常后保留现场，不为通过本轮验收临时修改战斗代码。

## 日志与可复现入口

- 独立完整输出：`Logs/SkillConfig-AcceptanceRevision-DotNet.log`。
- 隔离 Unity 完整输出：`Logs/SkillConfig-AcceptanceRevision-Unity.log`。
- 新进程重开：`Logs/SkillConfig-AcceptanceRevision-Reopen.log`。
- 可保存分享的结果摘要：`docs/SkillConfig_Phase1_Acceptance_Revision_Results.txt`，只提取测试行，避免完整 Unity 日志中的许可信息。
- 测试命令入口：`SkillConfigPhase1Checks.RunAcceptanceRevision`，然后在新的 Unity 进程执行 `SkillConfigPhase1Checks.VerifyReopen`。使用 `Tests/SkillConfig/PrepareUnityHarness.ps1 -Destination D:/GitHub/ARPGDEMO/Temp/SkillConfigAcceptanceRevision` 准备隔离工程。
- 原工程已有用户实例打开，批处理不能同时打开同一路径。不要为自动测试关闭用户实例。隔离测试未加载完整游戏、原场景和全部第三方系统，不能作为原工程 Play 回归证据。

## 本轮实际差异

1. 修改 `Assets/Editor/SkillConfig/SkillConfigPhase1Checks.cs`：新增 RunAcceptanceRevision，112行，仅测试。
2. 修改 `docs/SkillConfig_Phase1_Acceptance.md`：纠正阶段验收门槛和旧战斗回归状态，保留历史实现说明。
3. 新增本修订矩阵和 `docs/SkillConfig_Phase1_Acceptance_Revision_Results.txt`。

本轮未改任何产品功能、ComboData/试点资产/正式 JSON。对本轮开始时保存的文件 SHA-256 比较，Phase 1 源码/资产范围中只有测试文件变化。隔离测试产物在 Temp/Logs 内，不复制回工程资产。

Git 相对 HEAD 的既存差异仍包含上一轮 ComboData 新字段及两个试点资产（合计68新增、8删除；资产删除行是 rootMotion 字段的序列化位置移动），并有上一轮尚未提交的新文件。不能将这些历史改动算成本轮新增。由于测试文件尚未被 Git 跟踪，本轮测试变化另以开始时副本执行 git diff --no-index，结果112新增、0删除。没有执行提交、reset 或 clean。

**验收门槛：I01、ID05、I02 和 B01～B06 未完成前，Phase 1 保持待验收，不申请进入 Phase 2。**
