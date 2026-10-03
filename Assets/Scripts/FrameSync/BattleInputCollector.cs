using GameProtocol;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>只采集本机输入。位移必须等服务器广播，不在这里移动角色。</summary>
[DefaultExecutionOrder(-50)]
public class BattleInputCollector : MonoBehaviour
{
    private void OnEnable()
    {
        CharacterInputSystem.Instance.inputActions.Player.Walk.started += OnWalkStart;
        CharacterInputSystem.Instance.inputActions.Player.Dash.started += OnDashStart;
        CharacterInputSystem.Instance.inputActions.Player.Movement.canceled += OnMovementCanceled;
        CharacterInputSystem.Instance.inputActions.Player.Movement.performed += OnMovementPerformed;
        CharacterInputSystem.Instance.inputActions.Player.CameraLook.started += OnMouseMovementStarted;
    }

    private void OnDisable()
    {
        CharacterInputSystem.Instance.inputActions.Player.Walk.started -= OnWalkStart;
        CharacterInputSystem.Instance.inputActions.Player.Dash.started -= OnDashStart;
        CharacterInputSystem.Instance.inputActions.Player.Movement.canceled -= OnMovementCanceled;
        CharacterInputSystem.Instance.inputActions.Player.Movement.performed -= OnMovementPerformed;
        CharacterInputSystem.Instance.inputActions.Player.CameraLook.started -= OnMouseMovementStarted;
    }

        private void OnMovementCanceled(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void OnDashStart(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void OnWalkStart(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }
    private void OnMouseMovementStarted(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void OnMovementPerformed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }
}
