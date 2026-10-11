# Phase 2D-1 交付与验收

日期：2026-10-10。范围仅高级 Timeline。Phase 2C 复核/待办见 `SkillEditor_Phase2C_Checkpoint_20261010.md`。未实现连招 Graph、VFX/SFX 播放或 Phase 3 战斗功能；等待本批验收后再进入下一批。

## 关键实现

1. Timeline 维护临时选择集合：空白拖动框选、Ctrl/Cmd 点击增减、Shift 按当前可见显示顺序选取连续范围。隐藏/折叠的条目不会被 Shift 悄悄加入。
2. 多选拖动只在临时视图中改变整数帧；所有选中条目保持相对时间，左移限制最早条目不小于0。松开鼠标统一提交，Escape取消。单条目两端裁剪保留原2B行为；多选时只整体移动。
3. Ctrl+C/V、Delete/Backspace与右键菜单。快捷键要求 Timeline 已获得操作焦点、没有文本编辑/键盘字段焦点；Inspector 文本输入不被截获。
4. 剪贴板为当前 Unity 会话内的不可编辑数据快照，保存全部类型化参数和资源引用；不创建平行 SO/TimelineAsset。每次粘贴生成新的条目ID；新轨道/组也分配新ID，保存后稳定。
5. 同技能粘贴保持仍有效的 HitGroup；组已不存在时可从快照复制。跨技能必须确认“复制组”或“复用等价组”；找不到等价组则整体拒绝，不自动退回其他策略。多个攻击盒对同一个源组只生成/绑定一个目标组。复用比较完整组配置但排除ID。
6. 跨技能的基础伤害仍从目标 `_comboDamage` 读取；资源ID在目标目录解析，须事先正确配置。对话框明确提示这些语义。不会静默复制目录资源或改动源资产。跨技能 AnimationSegment 原 LegacyCombo 位移绑定转为指向原 JSON 的 Explicit 绑定，避免误用目标技能旧位移表；旧 JSON/播放器均不改。
7. 所有批量内容操作先在临时 ComboData 上执行，使用 `SkillAssetValidator.BuildDraft` 做完整目标目录校验；Error拒绝且正式SO/JSON不变，Warning允许。通过后仅一次 `SkillEditCommands.Apply`，修改前登记Undo，沿用Dirty/自动保存/待导出机制；JSON仍须主动Export。
8. 新增副本校验入口保留原资产/目录身份，防止把其自身误判为重复ID；其他全局重复SkillID和目录所有权仍检查。原 `Build` / Exporter / Loader / JSON Schema 保持兼容。
9. 轨道标题右键可组内上移/下移、隐藏、定位属性；名称与Group由既有Inspector编辑。分组折叠、轨道折叠和隐藏保存在Library；“显示全部轨道”恢复隐藏。排序只更新原 `displayOrder`，不重排事件列表、不改事件 `order`。

动画主轨的连续性优先于自由拖动：首片段必须从0开始、无空隙/重叠。粘贴动画必须落在已有片段边界或末尾；插入只后移后续动画，不移动其他事件。造成动画空隙、非连续片段复制或事件越界的操作由Validator拒绝；不会自动缩放其他轨道。

## 本批真实文件清单

新增：

- `Assets/Editor/SkillEditor/SkillTimelineBatch.cs`及.meta：快照、批量事务、粘贴引用策略。
- `Assets/Editor/SkillEditor/Tests/SkillEditorPhase2D1Checks.cs`及.meta：数据、Undo、资源、JSON、持久化检查。
- `Assets/Editor/SkillEditor/Tests/SkillTimelineInteractionChecks.cs`及.meta：真实Unity IMGUI事件检查。
- `docs/superpowers/plans/2026-10-10-phase2d1.md`。
- `docs/SkillEditor_Phase2C_Checkpoint_20261010.md`。
- `docs/SkillEditor_Phase2D1_Acceptance.md`、`docs/SkillEditor_Phase2D1_Results.txt`。

修改：

- `Assets/Editor/SkillEditor/SkillTimelineView.cs`：选择/框选/批量拖动/快捷键/菜单/显示管理。
- `Assets/Editor/SkillEditor/SkillEditorWindow.cs`：把当前角色目录传给Timeline。
- `Assets/Editor/SkillEditor/SkillEditorWorkspaceState.cs`：Library隐藏轨道状态。
- `Assets/Editor/SkillConfig/SkillAssetValidator.cs`：身份保留的临时副本校验入口。
- `Assets/Editor/SkillEditor/Tests/SkillEditorPhase2BChecks.cs`：旧测试仅清空自己的临时副本，兼容试点已有用户数据。
- `Tests/SkillEditor/RunPhase2CChecks.ps1`：同步本批校验器源码。
- `Tests/SkillEditor/RunUnityChecks.ps1`：同步校验器及清理隔离镜像内因目录重命名残留的重复源码。

原有 `Preview → Preview(场景预览)`、`Scene → SceneHitBox` 重命名、2C修复、Normal1/Normal2和角色JSON的已有差异均保留；不是本批修改清单。`Assets/Scripts` 无本批差异；正式场景、Prefab、技能资产、JSON未因测试改写。所有可变测试资产只生成在Temp下的隔离工程。

## 实际执行结果

| 类别 | 结果 | 证据 |
|---|---|---|
| 2D-1 Unity数据事务 | 37项断言通过 | `Logs/SkillEditor-Phase2C-Phase2D1.log` |
| 2D-1 Unity鼠标/键盘事件 | 10项断言通过 | `Logs/SkillEditor-Phase2C-Phase2D1Interaction.log` |
| 2D-1 新Unity进程重开 | 4项通过，包含稳定ID和Library状态 | `Logs/SkillEditor-Phase2C-Phase2D1Reopen.log` |
| Phase1原Unity检查 | 25+57项通过，由2B回归调用 | `Logs/SkillEditor-Phase2B-Phase2D1Regression.log` |
| Phase2B原编辑回归 | 35项通过 | 同上 |
| Phase2B重开 | 4项通过 | `Logs/SkillEditor-Phase2B-Phase2D1Reopen.log` |
| Phase2C预览集成回归 | 120项断言通过 | `Logs/SkillEditor-Phase2C-Phase2D1PreviewRegression.log` |
| 独立.NET数据/导出检查 | 56项通过；不覆盖Unity API | `Logs/SkillEditor-Phase2D1-DotNet.log` |

Unity实际版本2022.3.62f2c1，测试运行于隔离工程的Edit Mode，通过 `-executeMethod` 执行断言；不是伪装为Unity Test Runner/NUnit报告。鼠标键盘检查实际向IMGUI发送事件，但不是人工逐项点击验收。测试涵盖负帧/越界拒绝、原子性、相对时间、一次Undo/Redo、复制新ID、组策略、类型化参数/SFX/VFX尾段、缺失资源、全局重复ID、待导出/不自动导出、错误操作保留旧JSON、事件顺序与保存重开。

执行中曾出现：隔离目录缺失测试用角色目录、旧2B单HitBox假设失效、Temp里的重开预期文件被Unity清除。这些是测试夹具问题，已修正后重跑。副本身份误判则是批量设计需处理的真实校验问题，已增加BuildDraft及重复ID回归。最终结果与早期失败分开记录；不是将失败忽略。

未执行：正式项目的完整人工UI矩阵、操作系统剪贴板互通（不支持）、正式对战Play模式回归、所有角色资源覆盖。本批不以独立.NET或隔离Unity通过替代这些项目。2C遗留项继续保留。

## Unity人工验收步骤

1. 在正式工程打开 Skill Editor。先选择合法的测试技能副本或备份已有SO；批量操作会校验整个目录，其他技能已有Error也会阻止提交。
2. 在两个以上轨道放置至少3个合法条目。单击、Ctrl点击增减；Shift点击首末项；在内容空白处框选。检查选中高亮和Inspector主选项。折叠/隐藏轨道后Shift不选隐藏项。
3. 拖动所选整体2帧，拖动中SO不应写入；释放后保持相对时间。左移到起点应停在0；拖到非法范围释放应显示错误、数据不变。Escape取消。一次Undo/Redo恢复整个批次。
4. Ctrl+C，把指针移到合法帧，Ctrl+V。新条目ID与旧ID不同；再次粘贴再生成新ID。Delete删整组选中项，Undo恢复；Inspector文本框中输入/复制/粘贴/Delete不能影响Timeline条目。
5. 同技能粘贴HitBox，检查共享HitGroup引用。复制到另一个技能，分别测试“复制组”“复用等价组”和取消；不存在等价组时应不修改目标。源SO不变，目标Undo只撤销本次粘贴。检查基础伤害与资源使用目标目录的明确语义。
6. 复制AnimationSegment到合法边界/末尾，检查后续动画连续、其他事件原时间不变；跨技能原位移JSON引用保留。对非法插入位置/空隙应拒绝。VFX AutoComplete尾段可超过技能结束，但触发帧仍需合法。
7. 标题右键组内排序；Inspector改名称/Group；折叠组、折叠轨道、隐藏，再显示全部。确认事件列表及显式order未改。关闭/重开窗口，随后重启Unity，检查数据与Library显示状态。
8. 修改后应显示待导出，自动保存SO不应改JSON；点击Export后验证JSON。缺失资源、重复ID等Error下操作/导出受阻且旧JSON保留，Warning仍可导出。
9. 复测2C动画Seek、Handles、位移及关闭清理；执行C-01正式对战回归。记录未通过项，不据此自动进入2D-2。

## 已知边界和回退

- 剪贴板只在当前Unity会话内有效，脚本重载后需重新复制；不是系统文本剪贴板。
- 跨技能提供复制组/复用等价组两种明确策略；任意组重绑定可在现有Inspector里完成。本批不自动迁移角色目录资源。
- 校验范围为整个目标目录及全项目身份/所有权，预存Error须先修复；基础单项Inspector仍可用于修复非法配置。
- 移动只做水平时间偏移，不做跨轨道垂直移动；多选不批量裁剪。动画约束如上，不接受会破坏连续性的自由平移。
- 轨道上下移动限定同组；跨组修改Group。Library显示状态是本机工作区状态，不导出成运行时配置。

回退：关闭窗口；删除本批新增Batch和两个测试源码及.meta；仅撤销本批对TimelineView/Window/Workspace/Validator/测试执行器的差异。不要整体reset或恢复Normal资产，避免丢失用户数据和前面的2C修复。无需迁移或回退JSON Schema，已有SO数据仍由Phase1结构承载。Git未提交、未自动导出正式角色JSON。
