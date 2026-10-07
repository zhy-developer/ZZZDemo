# 根位移接入与扩展

星见雅的前闪已绑定 `Assets/Resources/RootMotion/Avatar_Female_Size02_Unagi_Ani_Evade_Front_RootMotion.json`。
当前文件烘焙至动画第 45 帧，750ms，共 23 个逻辑帧；每次收到服务器逻辑帧消费一条数据，约 759ms 完成。
最后 24ms 采样的位移仍只应用一次，不拉伸也不再乘时间。原始距离约 5.9191 Unity 单位，保留动画的轻微横移和起手后退。

## 在 Unity 中试用

1. 打开 `Assets/ScriptableObject/PlayerInfo/PlayerInfo（星见雅）.asset`。
2. 展开 Movement Data → Dash Data → Front Root Motion。
3. Json 已绑定；End Frame Exclusive 为 0，表示播放 JSON 内全部采样（不是原动画全部时长）。Distance Permille 为 1000，表示原始距离，500 为半距。
4. 进入现有对战流程，使用星见雅，按方向前闪。松开方向仍应完成闪避；结束后按最新输入恢复普通移动。
5. 检查连续前闪、闪避接攻击/技能、移动输入换向、模型禁用、地图边缘。本次没有新增无敌帧或障碍物碰撞。

后闪以及其他角色未绑定 JSON，保留原行为。更换 JSON 后重新进入 Play 模式，重建共享数据缓存。
若需要更短的位移窗口，优先在烘焙器调整结束动画帧；也可以将 End Frame Exclusive 配为逻辑帧数量，例如 10 表示 [0,10)，不是动画第 10 帧。
动画播放速度须与烘焙速度一致（当前 1 倍）；修改动作速度应重新烘焙，不能只调 Animator.speed。

## 模块职责

- `RootMotionClip`：校验新 JSON 格式、33ms 间隔、10000 精度、采样时刻、累计位置、总位移；复制为不可变水平位移数据。
- `RootMotionSettings`：可嵌入任意动作配置的 JSON 引用、结束帧和距离倍率；共享解析缓存，每个资产只解析一次。
- `RootMotionPlayback`：纯 C# 整数播放游标，独立于 Unity、网络和动画。初始局部 +Z 沿锁定的逻辑朝向，+X 向右；累计偏移旋转后再取差，保留取整余量。
- `RoleBase`：每个角色自己的播放器；提供带句柄的启动/取消接口，统一提交逻辑位移、地图限制和显示位置。
- `PlayerDodgingState`：动作适配层，逻辑完成时恢复普通移动并淡入 Movement；退出状态时停止自己的位移，动画退出回调不提前结束逻辑播放。

## 接攻击突进或技能位移

在对应动作数据里增加一个 `RootMotionSettings` 字段，绑定该动作的烘焙 JSON。在**已确认的逻辑动作**中调用：

```csharp
// 在状态实例保存这个句柄；不要放进共享 ScriptableObject。
motionHandle = player.TryPlayRootMotion(actionMotionSettings, OnMotionFinished);

// 动作退出/受击/被新指令打断时：
player.StopRootMotion(motionHandle);
motionHandle = 0;
```

`TryPlayRootMotion` 返回 0 表示未配置或配置无效，由动作决定回退行为。成功启动替换该角色的旧播放，不调用旧完成回调。
旧句柄无法取消新播放；取消不调用完成回调。自然完成在最后一帧位移提交之后调用一次回调。
不要在渲染 Update、动画事件、本地输入捕获时直接启动，以免各客户端起始逻辑帧不同。
角色禁用时会清空播放。新动作仍需在自己的状态退出或确定的逻辑打断点取消句柄。

播放期间普通移动不叠加，但会保存最新输入，下一逻辑帧恢复。根位移本身不计算技能命中、无敌、冷却、动作优先级或位移叠加。
动画根运动仍不能直接更新角色 Transform，否则会双重位移。

## 碰撞与表现边界

`RoleBase.UpdateLogicPosition` 是普通移动与根位移共用的可重写位置解析入口；当前只沿用地图范围限制。
未来添加障碍检测时，应从当前逻辑位置到目标位置做扫掠检测，并保持帧同步确定性。被阻挡的位移不会累计补偿到下一帧。
当前继续使用原有 SmoothDamp 显示平滑，可能使视觉位移略落后于动作；这是可调表现参数，不影响整数逻辑位移。
只消费 X/Z；Y、根旋转、多段并行位移和可变播放速率不在本次支持范围。

## 验证

```powershell
dotnet run --project Tests/RootMotion/RootMotion.Tests.csproj
dotnet run --project Tests/ActionSync/ActionSync.Tests.csproj
```

Unity 非 Play 模式下菜单：`Tools → Root Motion → Verify Playback Integration`。
它使用临时对象和隔离的方向表/地图服务验证实际 RoleBase 接入，执行后恢复现场，不连接服务器或保存场景。
也可批处理执行 `-executeMethod RootMotionPlaybackChecks.Run`。
