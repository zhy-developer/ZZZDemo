# 怪兽对象池

## 直接使用

1. 把 `Assets/Resources/Prefabs/Enemy/EnemyPoolSetup.prefab` 拖入场景。
2. 在它的 `Enemy Spawner` 组件里，把玩家对象拖给 `Target`，把出生点拖给 `Spawn Point`；出生点留空时使用此对象的位置。
3. 确保出生点附近有与怪兽 NavMeshAgent 类型匹配的已烘焙 NavMesh。默认在 2 米范围内采样，失败会警告并返回 null。
4. 勾选 `Spawn On Start` 可在开始运行时生成一只。示例预制体默认关闭此项，避免未配置目标和位置就生成。
5. Play Mode 下也可使用组件菜单 `Spawn Enemy (Play Mode)` 和 `Return Last Enemy (Play Mode)` 测试。此入口不包含自动波次逻辑。

示例已配置怪兽预制体和 3 个预热实例。池是场景内组件；池创建的怪兽保持为其子对象，随场景一起释放。现有场景中手工摆放的怪兽不会自动归池。

## 接口

```csharp
[SerializeField] private EnemyPoolManager pool;
[SerializeField] private GameObject enemyPrefab;

void SpawnEnemy(Vector3 position, GameObject player)
{
    EnemyPoolItem enemy = pool.Spawn(
        enemyPrefab, position, Quaternion.identity, player);
    // enemy 为 null 表示生成失败，例如出生点没有兼容的 NavMesh。
}

void RemoveEnemy(EnemyPoolItem enemy)
{
    if (enemy != null) enemy.Despawn(); // 或 pool.Despawn(enemy)
}

void Warmup()
{
    pool.Prewarm(enemyPrefab, 5); // 确保至少 5 个空闲实例
}
```

按预制体引用分别建池。只有完成回收的实例才会再次分配；没有空闲实例时扩容。重复回收和交给其他管理器回收会返回 false。应使用 Despawn 回收，不要 Destroy 池内怪兽。直接禁用池内怪兽也会归还给所属池。

对象引用可能在回收后代表另一轮生成。外部延迟回调如要操作旧引用，应保存 `enemy.SpawnVersion`，执行前同时检查 `IsSpawned` 和版本一致。EnemySpawner 的“回收上一只”已内置此检查。

## 怪兽预制体配置

现有 `怪兽.prefab` 根对象已添加 `EnemyPoolItem`。其他预制体若未添加，池会在创建实例时补上；需要调整参数时请直接在预制体上添加此组件。

- `Corpse Delay`：死亡动画播完后保留尸体的秒数，默认 0。使用游戏时间，会受暂停/慢动作影响。
- `Death State Name`：死亡状态完整路径。当前已保存 Controller 的子状态机名称是 `Hit `（末尾有空格），所以配置为 **`Base Layer.Hit .Dead`**。
- `Death Cross Fade Duration`：死亡动画混合时长，默认 0.1 秒。
- `Idle State Name`：生成时回到的待机状态，默认 `Base Layer.Idle`。
- `Target Variable Name`：行为树中 GameObject 类型的目标变量，默认 `PlayerTarget`。每次生成写入调用方目标，回收时清空；没有此变量且传了目标时会警告。
- `Nav Mesh Sample Radius`：出生点附近的导航采样范围，默认 2 米。

每次生成先恢复生命值、清除旧攻击者，然后激活动画和导航、清除追击/受击/攻击状态及上一轮生命的攻击冷却，最后绑定目标并启动行为树。普通角色切换导致的启用/禁用不会自动回血。

池内怪兽由 EnemyPoolItem 在 LateUpdate 检测死亡并停止行为树、播放死亡动画，播完后再回收；**不要求现有行为树添加 Death 节点**。死亡播放以池组件的配置为准。Death 节点仍可报告动画完成，但给它配置不同的死亡状态或层不会覆盖池组件的设置。非池内怪兽继续使用原有 Death 行为。未配置可播放的死亡状态时会警告并按尸体延时回收，避免永久占用实例。

## 文件职责

- EnemyPoolManager.cs：预热、分配、归还和所属池检查。
- EnemyPoolItem.cs：每次生成/回收的状态重置、目标绑定、死亡动画与延迟回收。
- EnemySpawner.cs：Inspector 生成入口。
- CharacterHealthBase.cs / CharacterHealthInfo.cs：显式生命值重置，以及禁用后不丢失的数值监听。
- EnemyAIMovementController.cs / RandomAttack.cs：新一轮生命的移动和攻击状态重置。
- Death.cs：动画完成后请求延迟回收。

## 验证

在独立 PowerShell 7 进程运行：

```powershell
pwsh -NoProfile -File Tests/Run-EnemyChaseRegression.ps1
pwsh -NoProfile -File Tests/Run-EnemyHitRegression.ps1
pwsh -NoProfile -File Tests/Run-EnemyDeathRegression.ps1
pwsh -NoProfile -File Tests/Run-EnemyPoolHealthRegression.ps1
pwsh -NoProfile -File Tests/Run-EnemyPoolRegression.ps1
```

这些测试运行实际业务类，使用 Unity/Opsive 边界适配器；不等价于真实动画、导航或行为树调度的 Play Mode 验证。

Play Mode 建议检查：连续生成两只身份不同；击杀后完整播完死亡动画并隐藏；再次生成复用旧实例且满血、能够重新追击攻击；连续重复 3 次；Corpse Delay 设 2 后延时回收；手动回收后再生成不会被旧回收计时器隐藏；错误出生点不会遗留活跃怪兽。另检查当前场景的玩家目标、NavMesh 和行为树变量绑定。
