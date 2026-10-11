# Phase 2C 验收复核与遗留清单

日期：2026-10-10。本轮开始时只读复核代码、既有日志和用户反馈，没有为复核修改运行时、角色资产、场景或 Prefab。未发现已确认、需立即阻断 2D-1 的严重数据保存或安全预览问题；这不等于所有资源与人工场景均已通过。

## 已实现

- 独立 PreviewSceneStage / Preview Session，白名单视觉代理，不实例化完整战斗角色；AnimationEvent、游戏脚本、控制器和网络逻辑不执行。
- 33ms 整数帧、多 Clip、源 Trim/PlaybackSpeed 采样、随机 Seek、播放/暂停/步进、四档预览速度和循环。
- 旧整数位移表逐帧/累计/轨迹预览、静止尾段、来源元数据 Warning、不支持重映射时拒绝位移预览。
- 五种 HitBox、竖直 Y Capsule HurtBox 可视化与 Handles；W/E/R 单工具模式、临时拖动事务、整数回存、Undo/Redo。
- 打断视觉模拟；关闭、切换、退出 Stage、重载及 Play Mode 的资源清理。
- 已修复原生 Handles 吞 MouseUp、骨骼根平移重复应用、动画 FBX 外部 Avatar 漏读及测试 Tools 命名冲突。

## 证据分级

| 项目 | 可证明的状态 | 证据/边界 |
|---|---|---|
| Scene 动画、HitBox/HurtBox、Root Motion 基本效果 | 用户反馈“基本达到预期” | 本轮用户消息；不推定逐项人工清单全部通过 |
| 原隔离/真实动画采样/整数位移/Undo/保存/GUI 集成 | 既有 Unity 自动检查通过 | `Logs/SkillEditor-Phase2C-Integration.log` 和对应 Results；仅隔离工程 |
| Stage 退出、脚本重载、进入/退出 Play Mode | 既有实际 Unity 生命周期检查通过 | `Logs/SkillEditor-Phase2C-Lifecycle.log`；空测试场景，不等同正式对战 |
| 原生 Scene 鼠标拖动提交及 Undo/Redo | 自动事件测试通过 | `Logs/SkillEditor-Phase2C-MoveToolGreen.log` |
| 两种视觉源的位移一致性 | 自动测量通过 | `Logs/SkillEditor-Phase2C-VisualSourcesFix.log`；只覆盖星见雅指定资源/采样帧 |
| 编译中的 Tools 命名冲突 | 修复后 Unity 检查通过 | `Logs/SkillEditor-Phase2C-ToolsNamespaceFix.log` |
| 2D-1 后的预览回归 | 本轮隔离 Unity 120 项断言通过 | `Logs/SkillEditor-Phase2C-Phase2D1PreviewRegression.log` |

## 必须保留的待办

| ID | 待办/限制 | 后续验收责任 |
|---|---|---|
| C-01 | 正式游戏中的角色进入、普攻、完整连招、Animator、Root Motion、无新增异常 | 用户正式场景人工回归；本轮未执行，不标通过 |
| C-02 | 五形状各方向真实鼠标、不同窗口布局/缩放/多个 Scene View、Inspector 双向同步完整人工矩阵 | 用户人工验收；已有自动事件/绘制结果只能证明覆盖路径 |
| C-03 | 所有角色、Avatar、导入格式、材质、复杂 Prefab 的兼容性 | 后续按真实资源扩充；当前首批星见雅，不宣称全覆盖 |
| C-04 | 任意 Trim/PlaybackSpeed 对旧 Root Motion 的精确重映射 | 后续批准的时间映射工作；当前明确拒绝错误位移预览 |
| C-05 | 缺少来源 GUID/烘焙元数据的旧位移表 | 保留 Warning；逐帧播放器一致不等于历史动画来源已证明 |
| C-06 | 用户资产里的非法片段/资源绑定 | 保留诊断，用户修正；不为进入下一阶段自动清理 |
| C-07 | 完整 VFX/SFX 预览 | 仅在获准 2D-3 后实施，不作为本轮已完成项 |

固定能力边界：不模拟原 Animator Transition 混合，不执行真实技能事件，不模拟权威碰撞/网络/战斗；预览最大100000帧。复杂脚本外观/Cloth/约束不在白名单视觉代理内。以上不会因为进入 2D-1 而默认为已解决。
