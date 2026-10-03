
using System;
using ZZZ;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Mesh;
using System.Collections.Generic;

namespace ZZZ
{
    public class PlayerMovementState : IState
    {
        protected PlayerMovementStateMachine movementStateMachine { get; }
        protected Animator animator { get; }
        protected Transform playerTransform { get; }
        protected PlayerMovementData playerMovementData { get; }
        protected PlayerStateReusableData reusableData { get; }

        public PlayerMovementState(PlayerMovementStateMachine playerMovementStateMachine)
        {
            movementStateMachine=playerMovementStateMachine;
            if (playerTransform == null)
            {
                playerTransform = movementStateMachine.player.transform;
            }
            if (animator == null)
            {
                animator = movementStateMachine.player.characterAnimator;
            }
            if (playerMovementData == null)
            {
                playerMovementData = movementStateMachine.player.playerSO.movementData;
            }
            if (reusableData == null)
            {
                //因为只new了它一次类，实际上获取到的类及成员都是它的引用
                reusableData=movementStateMachine.reusableData;
            }
           
        }
        public virtual void Enter()
        {    
            AddInputActionCallBacks();
        }

       
        public virtual void Exit()
        {
            RemoveInputActionCallBacks();
        }

        public virtual void HandInput()
        {
            animator.SetFloat(AnimatorID.MovementID,  GetPlayerMovementInputDirection().sqrMagnitude* reusableData.inputMult, 0.35f,Time.deltaTime);
        }
        public virtual void Update()
        {
            CharacterRotation(GetPlayerMovementInputDirection());

        }
        public virtual void OnAnimationTranslateEvent(IState state)
        {
            movementStateMachine.ChangeState(state);
        }
        public virtual void OnAnimationExitEvent()
        {
            
        }

        /// <summary>
        /// 获取玩家移动输入方向
        /// </summary>
        /// <returns></returns>
        protected Vector2 GetPlayerMovementInputDirection()
        {
            return CharacterInputSystem.Instance.PlayerMove;
        }

        float currentVelocity=0;
        protected void CharacterRotation(Vector2 movementDirection)
        {
            //如果没有输入就不旋转
            if (GetPlayerMovementInputDirection() == Vector2.zero) {
                BattleData.Instance.StopMove();
                return; }

            reusableData.targetAngle= Mathf.Atan2(movementDirection.x, movementDirection.y) *Mathf.Rad2Deg+ movementStateMachine.player.camera.eulerAngles.y;

            //转化为世界坐标系的方向
            Vector3 worldMoveDir = Quaternion.Euler(0f, reusableData.targetAngle, 0f)*Vector3.forward;

            Vector2 direction = new Vector2(worldMoveDir.x,worldMoveDir.z);
            float angle = Mathf.Repeat(Vector2.SignedAngle(Vector2.right, direction),360f);

            // 每 3° 一档，编码范围 0～119。
            int upDir = (int)(angle / 3f);

            DeLogger.LogNoticeTrace("角色转向输入，转到更新玩家方向");
            BattleData.Instance.UpdateMoveDir(upDir);

            //表现层转向，需转到接受同步帧时执行
            //movementStateMachine.player.transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(movementStateMachine.player.transform.eulerAngles.y, reusableData.targetAngle, ref currentVelocity, reusableData.rotationTime);

        }
        #region 输入回调
        protected virtual void AddInputActionCallBacks()
        {
            //角色walk委托  
            //CharacterInputSystem.Instance.inputActions.Player.Walk.started += OnWalkStart;
            //CharacterInputSystem.Instance.inputActions.Player.Dash.started += OnDashStart;
            //CharacterInputSystem.Instance.inputActions.Player.Movement.canceled += OnMovementCanceled;
            //CharacterInputSystem.Instance.inputActions.Player.Movement.performed += OnMovementPerformed;
            //CharacterInputSystem.Instance.inputActions.Player.CameraLook.started += OnMouseMovementStarted;
        }
        protected virtual void RemoveInputActionCallBacks()
        {
            //CharacterInputSystem.Instance.inputActions.Player.Walk.started -= OnWalkStart;
            //CharacterInputSystem.Instance.inputActions.Player.Dash.started -= OnDashStart;
            //CharacterInputSystem.Instance.inputActions.Player.Movement.canceled -= OnMovementCanceled;
            //CharacterInputSystem.Instance.inputActions.Player.Movement.performed -= OnMovementPerformed;
            //CharacterInputSystem.Instance.inputActions.Player.CameraLook.started -= OnMouseMovementStarted;
        }
        #endregion
        protected virtual void OnWalkStart(InputAction.CallbackContext context)
        {
            Debug.Log(movementStateMachine.reusableData.shouldWalk);
            //为什么要用bool？ 确保在idle状态记录walk的切换
            reusableData.shouldWalk = !reusableData.shouldWalk; ;
        }
        private void OnDashStart(InputAction.CallbackContext context)
        {
            if (movementStateMachine.player.comboStateMachine.currentState.Value == movementStateMachine.player.comboStateMachine.SkillState) { return; }
            if (reusableData.canDash)
            {
                Debug.Log("进入闪避状态");
                if (CharacterInputSystem.Instance.PlayerMove != Vector2.zero)
                {
                    animator.CrossFadeInFixedTime(playerMovementData. dashData.frontDushAnimationName, playerMovementData. dashData.fadeTime);
                }
                else
                {
                    animator.CrossFadeInFixedTime(playerMovementData.dashData.backDushAnimationName, playerMovementData.dashData.fadeTime);
                }
            }
           
        }

        public virtual void ResetDash()
        {
           
          reusableData.canDash=true;
        }
        #region 相机的水平居中
        public void UpdateCameraRecenteringState(Vector2 movementInput)
        {
            if (movementInput == Vector2.zero) { return; }
            //如果玩家按住W，也取消水平居中
            if (movementInput == Vector2.up)
            {
                DisableCameraRecentering();
                return;
            }
            //根据相机的垂直角来设置居中的速度
            //根据玩家的输入的按键来设置是否居中相机
            //得到相机的垂直角度（上下角）
            float cameraVerticalAngle = movementStateMachine.player.camera.localEulerAngles.x;
            //欧拉角返回的都是一个正数-90=>270，所以减去
            if (cameraVerticalAngle > 270f)
            {
                cameraVerticalAngle -= 360f;
            }
            cameraVerticalAngle= Mathf.Abs(cameraVerticalAngle);

            if (movementInput == Vector2.down)
            {
                SetCameraRecentering(cameraVerticalAngle,playerMovementData.BackWardsCameraRecenteringData);
                return;
            }
            //执行到这里就是带有水平方向的输入了：
            SetCameraRecentering(cameraVerticalAngle, playerMovementData.SidewaysCameraRecenteringData);
        }

        protected void SetCameraRecentering(float cameraVerticalAngle,List<PlayerCameraRecenteringData> playerCameraRecenteringDates)
        {
         
            foreach (PlayerCameraRecenteringData recenteringData in playerCameraRecenteringDates)
            {
                if (!recenteringData.IsWithInAngle(cameraVerticalAngle))
                {
                    //直接退出这个元素，进行下一个元素
                    continue;
                }
                //如果在范围内：
                EnableCameraRecentering(recenteringData.waitingTime, recenteringData.recenteringTime);
                return;
            }

            //如果循环完了没有匹配的范围就关闭水平居中
            DisableCameraRecentering();
        }

        public void EnableCameraRecentering(float waitTime = -1f, float recenteringTime = -1)
        { 
         movementStateMachine.player.playerCameraUtility.EnableRecentering(waitTime, recenteringTime);
        }
        public void DisableCameraRecentering()
        {
            movementStateMachine.player.playerCameraUtility.DisableRecentering();
        }
        private void OnMovementCanceled(InputAction.CallbackContext context)
        {
            //输入停止则禁用居中（水平、垂直）
            DisableCameraRecentering();
        }
        //当有鼠标输入或者移动方向键时调用
        private void OnMouseMovementStarted(InputAction.CallbackContext context)
        {
            UpdateCameraRecenteringState(GetPlayerMovementInputDirection());
        }

        private void OnMovementPerformed(InputAction.CallbackContext context)
        {
            //这里直接用事件得到的最新值
            UpdateCameraRecenteringState(context.ReadValue<Vector2>());
        }

        #endregion
    }
} 
