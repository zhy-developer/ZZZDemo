using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FrameSync.RootMotion;

[RequireComponent(typeof(ShapeCircle))]
public class RoleBase : MonoBehaviour {
	private const int DirectionTableScale = 100; // Desktopspeed.txt basis-vector precision.

	public int moveSpeed;

	[Header("头顶血条")]
	[SerializeField, Tooltip("可选：角色预制体内的头顶挂点；留空时使用角色根节点加下方偏移")]
	private Transform healthBarAnchor;
	[SerializeField, Tooltip("未指定挂点时，相对角色根节点的世界坐标偏移")]
	private Vector3 healthBarOffset = new Vector3(0f, 2.2f, 0f);

	[SerializeField, Min(0.01f), Tooltip("位置平滑时间（秒），越小跟随越快")]
	private float positionSmoothTime = 0.08f;
	private Vector3 currentMoveVelocity;
	[SerializeField, Min(0.01f), Tooltip("转向平滑时间（秒），越小转向越快")]
	private float rotationSmoothTime = 0.1f;
	private float currentRotationVelocity;

	private Transform modleParent;
	private ZZZ.Player player;
	public ZZZ.Player NetworkPlayer => player;
	public ZZZ.SkillRuntime.CombatFighter Combat { get; private set; }
	private ZZZ.SkillRuntime.CombatAnimationView combatView;
	private ZZZ.SkillRuntime.CombatPresentationView combatEffects;
	private ZZZ.SkillRuntime.CombatHitFeedbackView combatHitFeedback;
	private readonly List<StateBarUI> combatHealthBars = new List<StateBarUI>();
	private int combatDisplayedHealth = -1;
	private bool combatPoseInitialized;
	private bool combatPresentationFrozen;
	private bool combatRenderFailed;

	// 调用方负责选择 UI；本类只把该角色的血量推送给已注册视图。
	public void RegisterHealthBar(StateBarUI bar)
	{
		if (bar == null || combatHealthBars.Contains(bar)) return;
		combatHealthBars.Add(bar);
		if (Combat != null)
			bar.UpdateBlood((float)Combat.Health / Combat.Library.Profile.maxHealth, true);
	}

	public void UnregisterHealthBar(StateBarUI bar)
	{
		combatHealthBars.Remove(bar);
	}

	public void BindFrameCombat(ZZZ.SkillRuntime.CombatFighter fighter)
	{
		ReleaseFrameCombat(); CancelRootMotion();
		Combat = fighter;
		combatPoseInitialized = false;
		combatDisplayedHealth = -1;
		combatRenderFailed = false;
		#if UNITY_EDITOR
		// 旧 ShapeCircle 只继续保存逻辑位置；受击范围由新配置决定，用战斗 Gizmos 显示。
		objShape.closeLine = true;
		#endif
		combatView = new ZZZ.SkillRuntime.CombatAnimationView(player);
		combatEffects = new ZZZ.SkillRuntime.CombatPresentationView(player.characterAnimator.transform);
		combatHitFeedback = new ZZZ.SkillRuntime.CombatHitFeedbackView(transform);
		SyncFrameCombat();
	}

	public void SyncFrameCombat(bool matchEnded = false)
	{
		if (Combat == null) return;
		objShape.SetPosition(new GameVector2(Combat.Position.x, Combat.Position.y));
		renderPosition = objShape.GetPositionVec3();
		var forward = ZZZ.SkillRuntime.CombatMath.Direction(Combat.Facing);
		renderDir = Quaternion.LookRotation(new Vector3(forward.x, 0, forward.y));
		if (!combatPoseInitialized)
		{
			// 仅开局直接定位；后续网络帧只更新目标，不重置当前显示位置和追赶速度。
			transform.position = renderPosition; modleParent.rotation = renderDir;
			currentMoveVelocity = Vector3.zero; currentRotationVelocity = 0f;
			combatPoseInitialized = true;
		}
		// 顿帧暂时停用，命中不再冻结显示追赶；仅死亡或对局结束保持当前显示位置。
		combatPresentationFrozen = matchEnded || Combat.State == ZZZ.SkillRuntime.CombatState.Dead;
		if (combatPresentationFrozen)
		{
			currentMoveVelocity = Vector3.zero; currentRotationVelocity = 0f;
		}
		// 战斗坐标保存在 Combat/Shape；Transform 是显示结果，不回写逻辑。
		combatView.SyncState(Combat, matchEnded);
		combatEffects.SyncState(Combat, matchEnded);
		combatHitFeedback.SyncState(Combat, matchEnded);
		// 生命变化是逻辑通知；红条缓动由 StateBarUI.Update 自行按渲染帧推进。
		if (combatDisplayedHealth != Combat.Health)
		{
			bool initialHealth = combatDisplayedHealth < 0;
			combatDisplayedHealth = Combat.Health;
			foreach (var bar in combatHealthBars)
				if (bar != null) bar.UpdateBlood((float)Combat.Health / Combat.Library.Profile.maxHealth, initialHealth);
		}
		player.currentComboState = Combat.State.ToString();
		player.currentMovementState = Combat.State == ZZZ.SkillRuntime.CombatState.Free && Combat.Moving ? "Moving" : "Stopped";
	}

	public void ReleaseFrameCombat()
	{
		combatHitFeedback?.Dispose(); combatHitFeedback = null;
		combatEffects?.Dispose(); combatEffects = null;
		combatView?.Dispose(); combatView = null; Combat = null;
		combatPoseInitialized = false;
		combatPresentationFrozen = false;
		currentMoveVelocity = Vector3.zero; currentRotationVelocity = 0f;
	}

	private void LateUpdate()
	{
		if (Combat == null || combatView == null || combatRenderFailed) return;
		// 动画播放、移动参数和混合过渡按渲染帧推进；网络帧仅提供状态通知。
		try
		{
			combatView.Render(Time.deltaTime);
			combatEffects?.Render(Time.deltaTime);
			combatHitFeedback?.Render(Time.deltaTime);
		}
		catch (System.Exception exception)
		{
			// 表现错误只报告一次，不重试刷屏，也不能反向中断已经确认的战斗逻辑。
			combatRenderFailed = true;
			combatHitFeedback?.Dispose();
			combatEffects?.Dispose();
			Debug.LogError("角色动画显示失败，请检查动画配置：" + exception, this);
		}
	}
	// Keep input buffered, but do not let it move or turn an active attack/skill.
	private bool IsInputMovementBlocked => player != null
		&& (player.comboStateMachine.currentState.Value == player.comboStateMachine.ATKIngState
			|| player.comboStateMachine.currentState.Value == player.comboStateMachine.SkillState);

	private Vector3 renderPosition;  // 渲染位置
	private Quaternion renderDir;
	private GameVector2 logicSpeed;
	private int roleDirection;//角色朝向
	private int inputDirection = 121;
	private int? actionDirection;
	private readonly RootMotionPlayback rootMotion = new RootMotionPlayback();
	private int rootMotionHandle;
	private System.Action rootMotionCompleted;
	public bool IsPlayingRootMotion => rootMotion.IsPlaying;
	[HideInInspector]
	public ShapeBase objShape; // 角色的shapeBase
	public void InitData(GameObject _ui,GameObject _modle,int _roleID,GameVector2 _logicPos){
		combatHealthBars.Clear();
		if (_ui != null) combatHealthBars.AddRange(_ui.GetComponentsInChildren<StateBarUI>(true));
		if (_ui != null && combatHealthBars.Count == 0)
			Debug.LogWarning("RoleUI 缺少 StateBarUI，请在血条 Canvas 根节点添加并绑定红/绿血条。", _ui);
		objShape = GetComponent<ShapeBase> ();
		logicSpeed = GameVector2.zero;
		modleParent = transform.Find ("Modle");

		_modle.transform.SetParent (modleParent);
		_modle.transform.localPosition = new Vector3(0,0,0);
		player = _modle.GetComponentInChildren<ZZZ.Player>(true);
		player?.BindMotionRole(this);
		CancelRootMotion();
		inputDirection = 121;

		objShape.InitSelf (ObjectType.role,_roleID);
		objShape.SetPosition (_logicPos);
		renderPosition = objShape.GetPositionVec3 ();
		transform.position = renderPosition;
		currentMoveVelocity = Vector3.zero;

		roleDirection = 0;
		renderDir = Quaternion.LookRotation (new Vector3(1f,0f,0f));
		modleParent.rotation = renderDir;
		currentRotationVelocity = 0f;

		foreach (var bar in combatHealthBars)
		{
			// 挂在 UI 上，随角色一起销毁；跟随显示 Transform，不读取离散逻辑坐标。
			var follow = bar.GetComponent<BloodPos>();
			if (follow == null) follow = bar.gameObject.AddComponent<BloodPos>();
			follow.Bind(bar, healthBarAnchor != null ? healthBarAnchor : transform,
				healthBarAnchor != null ? Vector3.zero : healthBarOffset);
		}

	}

	void Update () {
		if (player != null && player.UsesFrameCombat && (Combat == null || combatPresentationFrozen)) return;
		// 沿用原表现方式：每个渲染帧从当前显示位置追赶最新目标，网络帧不重置进度。
		transform.position = Vector3.SmoothDamp(
			transform.position,
			renderPosition,
			ref currentMoveVelocity,
			positionSmoothTime);

		float nextAngle = Mathf.SmoothDampAngle(
			modleParent.eulerAngles.y,
			renderDir.eulerAngles.y,
			ref currentRotationVelocity,
			rotationSmoothTime);
		modleParent.eulerAngles = Vector3.up * nextAngle;
	}

	/// <summary>
	/// 逻辑帧更新角色方向
	/// </summary>
	/// <param name="_dir"></param>
	public virtual void Logic_UpdateMoveDir(int _dir){
		player?.ApplyNetworkMovement(_dir);
		inputDirection = _dir;
		if (_dir < 0 || _dir > 120) {
			logicSpeed = GameVector2.zero;
		} else
		{			
			int direction = (_dir % 120) * 3;
			logicSpeed = moveSpeed * BattleData.Instance.GetSpeed(direction);
			if (!rootMotion.IsPlaying && !IsInputMovementBlocked) SetLogicalFacing(direction);
		}
	}


	/// <summary>
	/// 逻辑帧更新角色位移
	/// </summary>
	public virtual void Logic_Move(){
		if (rootMotion.TryAdvance(out int deltaX, out int deltaZ))
		{
			ApplyLogicDisplacement(new GameVector2(deltaX, deltaZ));
			if (!rootMotion.IsPlaying)
			{
				var completed = rootMotionCompleted;
				rootMotionCompleted = null;
				RestoreInputFacing();
				completed?.Invoke();
			}
			return; // Including the final sample: never add ordinary movement on this tick.
		}
		// Check after actions have been applied: movement input is processed first in a frame.
		if (IsInputMovementBlocked) return;
		if (logicSpeed != GameVector2.zero) { // 如果逻辑速度不等于0
			RestoreInputFacing();
			ApplyLogicDisplacement(logicSpeed);
		}
	}

	public void Logic_Tick() {
		player?.LogicTick(); 
	}

	/// <summary>
	/// 逻辑帧应用角色动作
	/// </summary>
	/// <param name="operation"></param>
	public void Logic_ApplyAction(GameProtocol.PlayerOperation operation)
	{
		int direction = operation.operationValue1;
		actionDirection = direction >= 0 && direction <= 120 ? (int?)((direction % 120) * 3) : null;
		try
		{
			if (player != null && player.ApplyNetworkAction(operation) && actionDirection.HasValue)
				SetLogicalFacing(actionDirection.Value);
		}
		finally { actionDirection = null; }
	}

	/// <summary>Call from a confirmed logical action. Returns an ownership handle, or 0 on failure.</summary>
	public int TryPlayRootMotion(RootMotionSettings settings, System.Action completed = null)
	{
		if (settings == null || !settings.TryGetClip(out var clip)) return 0;
		int facingDirection = actionDirection ?? roleDirection;
		GameVector2 facing = BattleData.Instance.GetSpeed(facingDirection);
		// Desktopspeed.txt stores a unit direction with magnitude approximately 100.
		if (!rootMotion.TryStart(clip, settings.endFrameExclusive, settings.distancePermille,
			facing.x, facing.y, DirectionTableScale))
		{
			Debug.LogError("Invalid root motion range, scale or facing for " + settings.json.name, this);
			return 0;
		}
		rootMotionCompleted = completed;
		rootMotionHandle = rootMotionHandle == int.MaxValue ? 1 : rootMotionHandle + 1;
		SetLogicalFacing(facingDirection);
		return rootMotionHandle;
	}

	/// <summary>Stale action exits cannot cancel newer playback. Cancellation never invokes completion.</summary>
	public void StopRootMotion(int handle)
	{
		if (handle == 0 || handle != rootMotionHandle) return;
		CancelRootMotion();
		RestoreInputFacing();
	}

	public void CancelRootMotion()
	{
		rootMotion.Stop();
		rootMotionCompleted = null;
	}

	private void SetLogicalFacing(int direction)
	{
		roleDirection = direction;
		renderDir = Quaternion.LookRotation(ToolGameVector.ChangeGameVectorToVector3(BattleData.Instance.GetSpeed(direction)));
	}

	private void RestoreInputFacing()
	{
		if (!IsInputMovementBlocked && inputDirection >= 0 && inputDirection <= 120)
			SetLogicalFacing((inputDirection % 120) * 3);
	}

	private void OnDisable() { CancelRootMotion(); ReleaseFrameCombat(); }

	private void ApplyLogicDisplacement(GameVector2 delta)
	{
		UpdateLogicPosition(objShape.GetPosition() + delta);
		renderPosition = objShape.GetPositionVec3();
	}

	/// <summary>
	/// 更新逻辑位置
	/// </summary>
	/// <param name="_logicPos"></param>
	// Shared position resolver for normal and baked motion. Override to add swept obstacle collision.
	protected virtual void UpdateLogicPosition(GameVector2 _logicPos){
		 
		objShape.SetPosition (BattleData.Instance.GetMapLogicPosition(_logicPos));
	}

}
