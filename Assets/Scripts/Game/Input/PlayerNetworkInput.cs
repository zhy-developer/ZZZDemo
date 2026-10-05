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
            capturedActions.Enqueue(new PlayerActionCommand(0, (int)action, facing, moving));
        }

        private void FlushCapturedAction()
        {
            // Resolve only after the preceding action is echoed and applied locally.
            // A single pre-input waits for its animation window; remote clients never revalidate that window.
            if (BattleData.Instance.PendingActionCount != 0 || capturedActions.Count == 0) return;
            var command = capturedActions.Peek();
            var result = player.PrepareNetworkAction((RightOpType)command.Type, command.Value2, out int payload);
            if (result == ActionPreparation.Wait) return;
            capturedActions.Dequeue();
            if (result == ActionPreparation.Ready)
                BattleData.Instance.UpdateRightOperation((RightOpType)command.Type, command.Value1, payload);
        }

        public void Clear() { capturedActions.Clear(); }
    }
}
