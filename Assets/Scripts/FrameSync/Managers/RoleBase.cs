using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ShapeCircle))]
public class RoleBase : MonoBehaviour {

	public int moveSpeed;

	[SerializeField, Min(0.01f), Tooltip("位置平滑时间（秒），越小跟随越快")]
	private float positionSmoothTime = 0.08f;
	private Vector3 currentMoveVelocity;
	[SerializeField, Min(0.01f), Tooltip("转向平滑时间（秒），越小转向越快")]
	private float rotationTime = 0.1f;
	private float currentRotationVelocity;

	private Transform modleParent;

	private Vector3 renderPosition;  // 渲染位置
	private Quaternion renderDir;
	private GameVector2 logicSpeed;
	private int roleDirection;//角色朝向
	[HideInInspector]
	public ShapeBase objShape; // 角色的shapeBase
	public void InitData(GameObject _ui,GameObject _modle,int _roleID,GameVector2 _logicPos){
		objShape = GetComponent<ShapeBase> ();
		logicSpeed = GameVector2.zero;
		modleParent = transform.Find ("Modle");

		_modle.transform.SetParent (modleParent);
		_modle.transform.localPosition = new Vector3(0,0,0);

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
			rotationTime);
		modleParent.eulerAngles = Vector3.up * nextAngle;
	}

	public virtual void Logic_UpdateMoveDir(int _dir){
		if (_dir > 120) { 
			logicSpeed = GameVector2.zero;
		} else
		{			
			roleDirection = _dir * 3;
			logicSpeed = moveSpeed * BattleData.Instance.GetSpeed (roleDirection);
			Vector3 _renderDir = ToolGameVector.ChangeGameVectorToVector3 (logicSpeed);																																																																		//ani.speed *= !!ReplayCon.Instance ? ReplayCon.Instance.narmalSpeed : 1;
			renderDir = Quaternion.LookRotation (_renderDir);
		}
	}


	/// <summary>
	/// 逻辑帧位移
	/// </summary>
	public virtual void Logic_Move(){

      //  Debug.Log("Logic_Move  "  + Time.realtimeSinceStartup);
		if (logicSpeed != GameVector2.zero) { // 如果逻辑速度不等于0
			GameVector2 _targetPos = objShape.GetPosition () + logicSpeed; // 计算目标位置
			UpdateLogicPosition (_targetPos); //更新逻辑位置， 
			renderPosition = objShape.GetPositionVec3 (); // 更新渲染位置。 使用算法平滑处理。
		}
	}

	/// <summary>
	/// 更新逻辑位置
	/// </summary>
	/// <param name="_logicPos"></param>
	void UpdateLogicPosition(GameVector2 _logicPos){
		 
		objShape.SetPosition (BattleData.Instance.GetMapLogicPosition(_logicPos));
	}

}
