using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FrameSync.RootMotion;

[RequireComponent(typeof(ShapeCircle))]
public class RoleBase : MonoBehaviour {
	private const int DirectionTableScale = 100; // Desktopspeed.txt basis-vector precision.

	public int moveSpeed;

	[SerializeField, Min(0.01f), Tooltip("位置平滑时间（秒），越小跟随越快")]
	private float positionSmoothTime = 0.08f;
	private Vector3 currentMoveVelocity;
	[SerializeField, Min(0.01f), Tooltip("转向平滑时间（秒），越小转向越快")]
	private float rotationSmoothTime = 0.1f;
	private float currentRotationVelocity;

	private Transform modleParent;
	private ZZZ.Player player;

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

	}

	void Update () {
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
			if (!rootMotion.IsPlaying) SetLogicalFacing(direction);
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
		if (logicSpeed != GameVector2.zero) { // 如果逻辑速度不等于0
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
		if (inputDirection >= 0 && inputDirection <= 120) SetLogicalFacing((inputDirection % 120) * 3);
	}

	private void OnDisable() { CancelRootMotion(); }

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
