using GameProtocol;
using UnityEngine;

namespace ZZZ
{
    // The only player-action code allowed to read this client's keyboard/camera.
    public sealed class PlayerNetworkInput
    {
        private readonly Player player;
        public Vector3 WorldMoveDirection { get; private set; }
        private readonly PlayerActionBuffer capturedActions = new PlayerActionBuffer();
        private int nextInputId;
        private int blockedInputId;
        public PlayerNetworkInput(Player player) { this.player = player; }

        public void Capture()
        {
            if (!player.IsLocalPlayer || !player.isActiveAndEnabled) return;
            var actions = CharacterInputSystem.Instance.inputActions.Player;
            Vector2 input = actions.Movement.ReadValue<Vector2>();
            Vector3 direction = Quaternion.Euler(0, player.camera.eulerAngles.y, 0)
                * new Vector3(input.x, 0, input.y);
            WorldMoveDirection = direction;
            int move = input == Vector2.zero ? 121 : EncodeDirection(direction);
            BattleData.Instance.UpdateMoveDir(move);

            // Freeze the action's facing at input time; remote players never use our target blackboard.
            Vector3 aim = direction;
            if (input == Vector2.zero)
                aim = player.enemy != null ? player.enemy.position - player.transform.position : player.transform.forward;
            int facing = EncodeDirection(aim);
            int moving = input == Vector2.zero ? 0 : 1;
            if (actions.Walk.WasPressedThisFrame()) Queue(RightOpType.rop5, facing, moving);
            if (actions.Dash.WasPressedThisFrame()) Queue(RightOpType.rop4, moving != 0 ? facing : EncodeDirection(player.transform.forward), moving);
            if (actions.L_AtK.WasPressedThisFrame()) Queue(RightOpType.rop1, facing, moving);
            if (actions.Skill.WasPressedThisFrame()) Queue(RightOpType.rop2, facing, moving);
            if (actions.FinishSkill.WasPressedThisFrame()) Queue(RightOpType.rop3, facing, moving);
            FlushCapturedAction();
        }

        private static int EncodeDirection(Vector3 direction)
        {
            float angle = Mathf.Repeat(Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg, 360f);
            return Mathf.Min(119, (int)(angle / 3f));
        }

        private void Queue(RightOpType action, int facing, int moving)
        {
            int inputId = ++nextInputId;
            player.TraceCombo("INPUT", $"input={inputId} type={action} facing={facing} moving={moving} queued={capturedActions.Count}");
            if (action == RightOpType.rop4 && capturedActions.Count > 0)
                player.TraceCombo("DODGE_CANCEL", "removing unsent attack inputs");
            capturedActions.Enqueue(new PlayerActionCommand(inputId, (int)action, facing, moving));
        }

        private void FlushCapturedAction()
        {
            // Resolve only after the preceding action is echoed and applied locally.
            // A single pre-input waits for its animation window; remote clients never revalidate that window.
            if (capturedActions.Count == 0) return;
            var head = capturedActions.Peek();
            if (BattleData.Instance.PendingActionCount != 0)
            {
                if (blockedInputId != head.Id)
                {
                    blockedInputId = head.Id;
                    player.TraceCombo("WAIT_ECHO", $"input={head.Id} pendingOp={BattleData.Instance.selfOperation.operationID}");
                }
                return;
            }
            var combo = player.comboStateMachine;
            int before = capturedActions.Count;
            if (!capturedActions.TryPrepareHead(PrepareCapturedAction, combo.ReusableData.canATK,
                combo.Combo.CanBaseComboInput(), out var command))
            {
                if (capturedActions.Count < before)
                    player.TraceCombo("DROP", $"input={head.Id} attack eligibility failed (see flags/tags and PREPARE)");
                return;
            }
            capturedActions.Dequeue();
            BattleData.Instance.UpdateRightOperation((RightOpType)command.Type, command.Value1, command.Value2);
            player.TraceCombo("SEND_QUEUED", $"input={command.Id} op={BattleData.Instance.selfOperation.operationID} type={command.Type} payload={command.Value2} index={command.Value2 >> 2}");
        }

        private ActionPreparation PrepareCapturedAction(PlayerActionCommand command, out int payload)
        {
            var result = player.PrepareNetworkAction((RightOpType)command.Type, command.Value2, out payload);
            player.TraceCombo("PREPARE", $"input={command.Id} type={command.Type} result={result} payload={payload} selectedIndex={(result == ActionPreparation.Discard ? -1 : payload >> 2)} "
                + $"hit={player.IsPlayingAnimationTag("Hit")} parry={player.IsPlayingAnimationTag("Parry")} skill={player.IsPlayingAnimationTag("Skill")} execute={player.IsPlayingAnimationTag("Execute")}");
            return result;
        }

        public void Clear()
        {
            if (capturedActions.Count > 0) player.TraceCombo("CLEAR_INPUT", $"queued={capturedActions.Count}");
            capturedActions.Clear();
        }
    }
}
