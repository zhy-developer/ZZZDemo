# Phase 2B 交付与验收

范围：IMGUI Skill EditorWindow、技能树、基础 Timeline、Inspector、Undo/保存、复用 Phase 1 校验与导出。没有实现 Scene View、动画/音效/特效预览、Graph、命中检测或战斗接入。

## 使用入口

Unity 菜单：`Tools > Skill Editor > Skill Editor (Phase 2B)`。

默认选择 MiyabiPhase1 目录。左侧可选择 Normal1、Normal2；也可从旧 ComboData Inspector 的“打开 Skill Editor（Phase 2B）”进入。角色目录 ObjectField 可以切换其他目录。

- 选技能：右侧显示新配置元数据、原 `_comboName`、`_comboDamage`、`rootMotion`。
- “角色配置”：编辑 Animator、资源映射、HurtBox、目录技能列表及完整性状态。
- Timeline 顶部“+ 数据轨道”：创建六类数据轨道；轨道右侧 `+` 创建对应条目。
- Animation 是唯一主轨，`+` 添加片段。指定 Clip、完整 State 路径，使用“Trim 设为完整 Clip”初始化源时间。Trim 单位为微秒，速度 1000 表示 1 倍。
- 点击条目显示其全部基础配置。HitBox 可选择同类型轨道和共享 HitGroup。资源通过角色资源列表选择。
- 拖动数据条目主体移动；拖动边缘裁剪；Escape 或窗口失焦取消。动画主体仅选择，边缘改变源 Trim，后续动画连续重排。
- Custom Event 为单帧标记；AutoComplete VFX 显示安全尾段，右侧编辑 maxLifetimeFrames，不能把它误当技能内 EndFrame。
- Ctrl+S 保存 SO；保存、自动保存和关闭窗口均不会 Export。明确点击 Export JSON 才写 JSON。
- 删除技能是从目录移除，保留 SO；复制产生新资产和新 SkillID。Undo 目录添加不会销毁新资产，避免丢失编辑内容。

## 数据与事务保证

`SkillEditCommands.Apply` 在变更之前执行 `Undo.RegisterCompleteObjectUndo`；Inspector 的 SerializedProperty 暂存变更在该事务内应用。拖动只修改临时数值，鼠标释放统一提交。自动保存等待拖动结束，Undo/Redo 刷新视图和诊断，并将涉及的资产纳入保存。

Timeline 所有内容仍存放在原 `ComboData.skill`。新建轨道使用原 TrackKind，动画使用原 animations 列表。基础伤害模式直接使用旧字段，切回此模式时在同一事务清空非激活的 explicitDamage。

动画长度变化不重定时数据轨道。超过合法触发帧的事件保持原值并由 Phase 1 Validator 报错。合法 AutoComplete 尾迹与 SFX 尾段不按普通区间截断。

Workspace 只保存资产 GUID、缩放、滚动、折叠；路径为 `Library/SkillEditor/Workspace.asset`。删除它不会删除技能内容。

## 验收矩阵

| 验收项 | 自动验证覆盖 | 仍需人工验证 |
|---|---|---|
| 菜单与独立窗口 | 图形设备下创建窗口、逐类型 IMGUI Layout/Repaint 冒烟 | 菜单操作、停靠与实际视觉布局 |
| Normal1/Normal2 | Phase 1 实际 Clip/State/Root Motion 引用回归 | 左树切换和显示 |
| 整数帧、动画主轨 | 动画插入连续性、速度重排、越界事件错误 | 帧标尺与缩放滚动体验 |
| 全部数据类型 | 六类轨道条目创建/删除/Undo；动画、HitGroup；包含真实资源的综合配置 | 每类字段录入及资源下拉操作 |
| 拖动与裁剪 | SetRange 修改及 Undo；普通区间与尾段边界 | 鼠标拖动、边缘裁剪、滚动后拖动、Escape、中途失焦 |
| 保存与关闭 | 保存资产、关闭生命周期；新 Unity 进程回读 | 带未提交文本的窗口关闭和重开 |
| Undo/Redo | 实际 Unity Undo、删除轨道/组的引用恢复 | 快捷键、多次交错编辑后的选中同步 |
| 待导出 | 原字段、目录、Root Motion 回归；Undo 恢复导出一致性 | 界面标记及时刷新 |
| 正常导出 | Warning 可导出；Resources Loader 回读 | 按钮提示和预期目标文件 |
| 非法导出 | 多种非法配置、旧 JSON 字节与 meta/GUID 保护 | UI 诊断可见、可修复 |
| 原 Inspector | 仅增加打开窗口入口；真实 Unity 编译 | 旧字段及多选编辑体验 |
| 原战斗 | 独立 ActionSync/RootMotion 回归，运行时代码无变更 | 正式 Play 模式完整回归，不能由独立检查替代 |

## 人工验收步骤

请先在副本技能上操作，避免测试改变正式战斗参数。

1. 打开窗口，依次选择 Normal1/Normal2；确认原动画分别为 20/18 帧，Clip 和 State 引用正确。
2. 复制 Normal1。选择副本，创建 HitBox、Invincibility、Interrupt、VFX、SFX、Custom Event 轨道。配置资源、组关联、事件参数；通过角色配置注册所需资源。
3. 在 HitBox Inspector 输入区间，再拖动主体和两端；查看 Inspector 更新。拖动到一半按 Escape，确认值不变。横向/纵向滚动后重复。Undo/Redo 应同时恢复两侧视图。
4. HitGroup 切换 Explicit 并输入伤害，再切 ComboBaseDamage；应显示旧基础伤害，导出不出现 DUPLICATE_DAMAGE。
5. 新建第二动画片段并绑定实际 Clip/State。修改第一段 Trim/Speed；后段动画应连续移动，数据事件帧不能自动变化。将事件留在缩短后的合法范围外，必须出现 Error。
6. 将 AutoComplete VFX 起点放在技能内、生命周期延续至结束后；有效资源及其他字段正确时不得仅因尾迹报 Error。把起点移到技能结束帧，必须报 Error。
7. 保存，关闭窗口，再打开；配置、SkillID、引用应保持。观察“SO 已保存”和“待导出”是分开的状态。
8. 点击 Export，保存旧 JSON；设置 Speed=0、无效 HitGroup 或非法区间，再 Export，确认旧文件内容未变。Undo 修复，再 Export。
9. 新建/复制后 Undo 目录添加，确认窗口切回目录内技能，未登记副本不会冒充当前角色导出内容。
10. 打开原 Inspector，检查旧字段、多选和复制功能。
11. 正式 Play 模式验证：角色进入、Normal1/Normal2、完整普攻连招、Animator 切换、旧 Root Motion；Console 无新增 MissingReference/NullReference/技能异常；未接入 JSON 时行为不变。

## 执行方式与限制

`Tests/SkillEditor/RunUnityChecks.ps1` 使用 Unity 2022.3.62f2c1，在 `Temp/SkillEditorPhase2B` 运行。沿用 Phase 1 隔离工程准备脚本：复制真实技能源码、资源和 GUID，但用替代声明满足无关编译依赖。因此通过隔离编译不代表原完整工程或 Play 模式通过。

入口：`SkillEditorPhase2BChecks.Run`、`VerifyReopen`、`GuiSmoke`。GuiSmoke 使用 `-Graphics`，实际发出 IMGUI Layout/Repaint；不把它记为鼠标交互通过。无图形的数据批次创建窗口时有预期的 “No graphic device” 提示，图形批次单独检查窗口绘制。

自动执行结果与日志见 `SkillEditor_Phase2B_Results.txt`。结果按断言计数，不把断言数量当作完整用户场景数量。

当前边界：框选/批量移动、跨技能剪贴板、高级快捷键和 Graph 留到 2D；Scene View、安全采样和 Root Motion 预览留到 2C。仅保留 ISkillPreviewSession 接口，不创建任何预览实例。进入 2C 前必须先验证 PreviewSceneStage、视觉代理、AnimationEvent 隔离与释放流程。

## 变更与回退

新增：`Assets/Editor/SkillEditor/` 下 6 个编辑器实现文件、1 个 Unity 检查文件及其 .meta；`Tests/SkillEditor/RunUnityChecks.ps1`；本验收报告、结果记录和实施 ledger。

修改：`Assets/Editor/SkillConfig/ComboDataSkillInspector.cs` 仅增加一个打开窗口按钮。

没有修改 Runtime 模型、Schema、校验规则、Exporter、Loader、正式试点资产、JSON、Animator、网络、伤害、Root Motion 播放器或对象池。

回退本批代码时，应同时撤回旧 Inspector 的入口引用和新增编辑器目录。Library 工作区可丢弃。开发测试产物在 Temp 隔离工程，正式技能未被测试改写。以后使用编辑器保存的 SO 配置或显式导出的 JSON，应按用户自己的版本记录单独回退，不能用代码回退冒充资产回退。

Phase 2B 交付后停止，等待人工验收，不进入 2C。
