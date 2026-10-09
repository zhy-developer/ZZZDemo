# Phase 2C 交付与验收（2026-10-09）

本批实现 Scene View 形状编辑与安全技能预览；未改动战斗运行时、JSON Schema、Root Motion 格式或播放器。进入 Phase 2D 前等待用户验收。原有已暂存 Phase 2B 文件和用户 Normal2 修改均保留，未提交 Git commit。

## 使用入口与数据约定

菜单 `Tools > Skill Editor > Skill Editor`，旧 Phase 2B 菜单仍保留。选择星见雅技能，点击“打开 Scene 预览”。默认视觉源为 `Assets/Art/Model/雅/Model/星见雅.fbx`；其他角色可选择自己的模型或 Prefab 资产。视觉源 GUID 仅保存在 Library 工作区设置中，不进入技能 JSON。

工具栏提供播放/暂停、前后逐帧、0.25×/0.5×/1×/2×、循环、HurtBox 编辑、诊断及位移表。Timeline 指针直接 Seek；右侧 Inspector 仍使用原 Phase 2B 数据编辑命令。

- 帧 F 表示第 F 次 tick 前的边界，累计位移为 `[0,F)`；单帧 Δ 为从 F 到 F+1。区间生效条件为 `Start <= F < End`，技能结束边界无生效攻击盒。
- 源动画采样秒数为 `(TrimStartUs + (F - StartFrame) × 33000 × SpeedPermille / 1000) / 1000000`，限制在 Trim 范围内。多 Clip 边界直接切换，不模拟 Animator Transition 混合。
- 预览倍速仅改变编辑器推进频率，不写入 PlaybackSpeed，不改变 33ms 权威帧定义。
- 角色逻辑根面向 +Z；XZ 为移动平面，Y 为高度。位置/尺寸精度 10000，角度为毫度。Capsule 沿 Y，height 表示含两端半球的总高度。
- Handles 拖动使用临时数据，释放鼠标后通过 SkillEditCommands 一次提交；Escape 取消。保存值经整数转换后在 Inspector 回显，支持 Undo/Redo、自动保存和待导出标记；不会自动导出 JSON。

## 实现与隔离

PreviewSceneStage 拥有独立预览 Scene。视觉代理逐个创建 Transform、MeshFilter、MeshRenderer、SkinnedMeshRenderer 和新 Animator，映射骨骼和 Avatar；不实例化原 GameObject，不复制任意组件，不加载完整玩家后禁用脚本。控制器为空，fireEvents/applyRootMotion 关闭。

AnimationClip 临时副本移除 AnimationEvent、对象引用曲线及非允许绑定。每次 Seek 恢复初始姿态并建立手动求值 PlayableGraph，采样后恢复动画根，最后对独立逻辑根应用整数位移。原 Clip、Prefab、Scene 不写入。图、临时 Clip、视觉对象、灯光、回调随会话释放。

Root Motion 恒等映射直接使用未改动的 RootMotionPlayback 计算整数前缀和；任意 Seek 使用该前缀，表提前结束后保持位置。缺少来源证据显示 Warning。需要精确重映射时禁用整条位移轨迹并提示，动画仍可按 Trim/Speed 单独采样；不把原表当作正确重映射结果。

模拟打断会冻结指定帧、停止播放和图、隐藏攻击区间并限制后续 Seek；重置恢复到 0。没有 VFX/SFX 或实际技能事件执行，因此本批不会产生未来音效、粒子或伤害。

## 本轮真实文件清单

新增（各 Unity 文件/目录均含对应 .meta）：

- `Assets/Editor/SkillEditor/Preview/SkillPreviewStage.cs`：独立 Stage 生命周期。
- `Assets/Editor/SkillEditor/Preview/SkillVisualProxyBuilder.cs`：白名单视觉代理。
- `Assets/Editor/SkillEditor/Preview/SkillAnimationPreview.cs`：安全、无状态动画采样。
- `Assets/Editor/SkillEditor/Preview/SkillRootMotionPreview.cs`：整数前缀轨迹与映射诊断。
- `Assets/Editor/SkillEditor/Preview/SkillPreviewSession.cs`：Seek、播放、打断、集中清理。
- `Assets/Editor/SkillEditor/Preview/SkillPreviewPanel.cs`：窗口控件和 Scene 桥接。
- `Assets/Editor/SkillEditor/Scene/SkillShapeEdit.cs`：量化和临时编辑事务。
- `Assets/Editor/SkillEditor/Scene/SkillShapeHandles.cs`：五种攻击盒和 HurtBox 绘制/编辑。
- `Assets/Editor/SkillEditor/Tests/SkillEditorPhase2CChecks.cs`：真实资源、命令、GUI 集成检查。
- `Assets/Editor/SkillEditor/Tests/SkillPreviewSideEffectProbe.cs`：测试专用副作用哨兵。
- `Assets/Editor/SkillEditor/Tests/SkillPreviewLifecycleChecks.cs`：实际重载/Play Mode 清理检查。
- `Tests/SkillEditor/RunPhase2CChecks.ps1`：隔离 Unity 工程执行器。
- `docs/SkillEditor_Phase2C_Ledger.md`、本报告、`docs/SkillEditor_Phase2C_Results.txt`。

修改：

- `Assets/Editor/SkillEditor/SkillEditorWindow.cs`：预览面板与关闭/切换入口。
- `Assets/Editor/SkillEditor/SkillTimelineView.cs`：帧指针变更通知。
- `Assets/Editor/SkillEditor/SkillEditorWorkspaceState.cs`：视觉源 Library 绑定。
- `Tests/SkillEditor/RunUnityChecks.ps1`：隔离工程补入已有播放器源码依赖。

Git 工作区中 `ComboDataSkillInspector.cs` 和 `Unagi_Normal_2.asset` 的已有差异不是本批新增改动。Normal2 SHA256 前后相同：`E7D96C2FFAE83C7857F2D486FCC0296C4EF9A787603EB8BEDC21A72F40D1EE2B`。Runtime、试点资产、正式 Prefab、场景、Root Motion JSON 均无本批改动。

## 已实际执行的验收

Unity 版本：2022.3.62f2c1。测试在 `Temp/SkillEditorPhase2C` / `Temp/SkillEditorPhase2B` 副本中运行，使用真实模型、AnimationClip、ComboData 引用及原校验/导出/加载代码。这是真实 Unity 执行，但不是完整游戏启动或真人 UI 验收。

| 测试 | 实际结果 / 证据 |
|---|---|
| Normal1/2 真实 Clip、骨骼姿态变化、随机 Seek 重复姿态一致 | 通过；Phase2C Integration 日志 |
| 多 Clip 边界、Trim/Speed 时间映射、暂停/步进/循环/预览倍速 | 通过；Integration |
| 恒等映射逐帧与旧播放器一致、Seek/顺序累计相同、静止尾段、拒绝不支持映射 | 通过；Integration |
| 五种形状临时编辑、量化、Undo/Redo、取消、HurtBox Undo | 通过；Integration；不等于实际鼠标拖动测试 |
| 几何数据保存、原 Phase1 导出/Loader 回读、新进程重开 | 通过；Integration、Reopen |
| 原资产不变、白名单代理、恶意测试组件生命周期未执行、攻击 AnimationEvent 哨兵未触发 | 通过；Integration |
| 无效 Clip 隐藏代理后，修复并 Seek 恢复准确姿态 | 通过；Integration（曾发现并修复先激活后采样的问题） |
| Scene/Inspector 五种 Handles 与 HurtBox 的 Layout/Repaint | 通过；Integration、Reopen；非鼠标交互验收 |
| 替换会话、关闭活动窗口、主动离开 Stage | 通过；Integration、Lifecycle |
| 实际脚本重载、进入 Play Mode、返回 Edit Mode 无会话/Stage/临时对象残留 | 通过；Lifecycle；空测试场景，无正式战斗 |
| Phase2B 原编辑、Undo、保存、待导出、非法导出保护 | 35 项通过；Phase2B-Phase2CRegression |
| Phase2B 新进程重开 / GUI 冒烟 | 4 项 / 1 项通过；Phase2CReopen / Phase2CGui |
| 独立 .NET SkillConfig / RootMotion / ActionSync | 56 / 47 / 31 项通过；不能替代 Unity 或战斗回归 |

日志均在项目 `Logs/`：`SkillEditor-Phase2C-Integration.log`、`SkillEditor-Phase2C-Reopen.log`、`SkillEditor-Phase2C-Lifecycle.log`、`SkillEditor-Phase2B-Phase2CRegression.log`、`SkillEditor-Phase2B-Phase2CReopen.log`、`SkillEditor-Phase2B-Phase2CGui.log`、`SkillEditor-Phase2C-DotNetSkillConfig.log`。计数、执行命令与差异摘要见 Results.txt。

执行过程中的预期 RED 编译失败用于先测后实现；最终日志单独保存。独立数据测试最初的原子文件替换被沙箱阻止，授权重跑后通过。全工作区 `git diff --check` 仍报告用户 Normal2 YAML 已有尾空格；未擅自修改用户资产。Phase2C 修改文件单独检查。

## 必须由用户执行的手工验收

1. 打开正式工程菜单，分别选择 Normal1/Normal2，开启 Scene 预览；检查材质、外观、姿态与预期一致。Normal2 当前有用户创建的空 Clip 段 `[18,19)`，到该段应明确报错并隐藏代理；本批未删除。多 Clip 自动测试使用正确配置的副本。
2. 对有效动画段拖动 Timeline、反复 Seek 同一帧、逐帧前后，切换四种速度/循环；检查当前片段/source 时间和位移表。调整 Trim/Speed 后应看到位移不支持提示而非错误轨迹；Undo 恢复。
3. 在测试副本中创建五种 HitBox，分别拖位置/尺寸、Box/Sector 旋转及 Sector 张角；拖动中 Escape 应取消，释放后 Inspector 显示整数值。每次 Undo/Redo、保存重开、导出确认结果。竖直 Capsule HurtBox 同样验证。
4. Inspector 改几何参数后检查 Scene；Timeline 改区间后检查当前帧激活颜色。Scene 改参数后检查 Inspector、待导出标记及 JSON；不手动 Export 时 JSON 不应变化。
5. 播放时指定打断帧，确认姿态冻结、攻击盒消失、后续 Seek 被限制；重置恢复。关闭窗口、切换技能、离开 Stage、触发重编译、进入 Play Mode 后检查无残留。
6. 在正式游戏场景验证角色进入游戏、Normal1/2、完整普攻连招、Animator 切换及旧 Root Motion，Console 无新增 MissingReference/NullReference/技能异常；新 JSON 不启用时原行为不变。**本项未执行，不报告通过。**

## 限制与回退

本批不模拟 Animator 混合、脚本驱动外观、Cloth/约束、完整 VFX/SFX、碰撞或网络。只支持非 Legacy AnimationClip；Humanoid 需要有效 Avatar。预览最大 100000 帧以限制缓存内存。数据合法性和导出继续以原 Validator 为准；预览不能证明旧表与源动画的历史烘焙一致性。

当前自动测试均通过；真实鼠标 Handles、正式工程外观和旧战斗回归未执行。不能据此声称所有人工验收完成。

回退先关闭编辑器窗口退出预览：删除本批 Preview/Scene、新增 Phase2C 测试与执行器、对应 .meta；仅撤销上述 3 个 Phase2B 编辑器文件及测试执行器中的本批差异。不要整体 reset，避免丢失已暂存 Phase2B 和用户 Normal2 修改。Library 中 visualBindings 可保留或清除，不影响正式技能内容；没有运行时或 Schema 回退迁移。
