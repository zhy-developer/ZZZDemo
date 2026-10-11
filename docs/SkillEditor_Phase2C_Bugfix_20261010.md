# Phase 2C 预览修复

沿用用户重命名目录 `Preview(场景预览)` 和 `SceneHitBox`。不修改用户 ComboData/JSON、战斗代码、原动画及 Root Motion 播放器。

## 两个根因

1. `SkillShapeHandles.Draw` 在原生 Handles 处理之后才检查 MouseUp；Unity 2022.3 的 Event.Use() 同时将 type/rawType 改成 Used。实测 MouseUp/MouseUp → Used/Used，临时事务未提交。现改为进入绘制时保存释放标记，再在处理完控件后提交。
2. `SkillAnimationPreview.Sample` 禁用 applyRootMotion，造成动画根平移保留在 Bip001 骨骼，随后又加整数表。Normal1 第15帧骨骼 Z 平移2.513625，整数表2.506800，重复叠加。开启根运动提取后骨骼仅保留0.006859的姿态偏移；提取的动画根位置被恢复，逻辑根只应用整数表。无战斗组件/事件参与。

## 证据

- `Logs/SkillEditor-Phase2C-DragRed.log`：修复前未提交，以及 MouseUp 被消费的证据。
- `Logs/SkillEditor-Phase2C-RootRed.log`：修复前骨骼含重复位移的失败断言。
- `Logs/SkillEditor-Phase2C-BugGreen.log`：Normal1/2 与 Unity 根运动提取后的姿态一致；原生 SceneView 鼠标拖动、释放提交、Undo/Redo 通过。拖动时不写 SO。
- `Logs/SkillEditor-Phase2C-BugRegression.log`：原 Phase2C 集成回归。

新增 `Assets/Editor/SkillEditor/Tests/SkillPreviewBugChecks.cs`；测试执行器清理隔离工程内因用户目录重命名遗留的重复源码，范围仅 Temp/SkillEditorPhase2C/Assets/Editor/SkillEditor。正式工程目录不清理。

手工复验：关闭再打开技能预览，Normal1/2 拖到相同 source 时间比较姿态；角色应不再远离逻辑根一整段位移。拖动攻击盒后松开，Inspector 回显整数参数；Undo/Redo、保存重开。Unity 内置预览相机可能跟随角色，应比较世界距离/逻辑根与骨骼距离，不以屏幕像素距离作为位移尺度。

原有 JSON 来源元数据 Warning、非恒等时间映射限制保持不变。未运行正式对战场景。本次是 Phase2C 缺陷修复，未进入 Phase2D。

## 攻击动作 FBX 作为视觉源的补充修复

用户随后截图使用 `雅_攻击动作.fbx`。该源的 Animator.avatar 为空，但 ModelImporter.sourceAvatar 显式指向星见雅模型 Avatar。旧代理仅复制前者，导致根运动提取仍失效。现在在 Animator.avatar 为空时读取导入器的显式来源；缺少有效 Avatar 的根曲线采样报诊断，不继续错误显示。

`SkillPreviewBugChecks.VisualSources` 实测 Normal1 第9帧：修复前攻击 FBX 骨骼世界 Z=4.858354；修复后为2.434026，与默认模型一致；逻辑根 Z=2.424300。第0/9/15帧两个源均一致。日志：`Logs/SkillEditor-Phase2C-VisualSources.log`（修复前测量）、`Logs/SkillEditor-Phase2C-VisualSourcesFix.log`（修复后）、`Logs/SkillEditor-Phase2C-AvatarFixRegression.log`（集成回归）。绿色线为整段整数位移轨迹，不是攻击盒或角色骨骼轨迹。
