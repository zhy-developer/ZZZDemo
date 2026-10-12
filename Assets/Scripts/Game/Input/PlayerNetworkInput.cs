using GameProtocol;
using UnityEngine;

namespace ZZZ
{
    // Captures intent only. Eligibility and combo selection belong to the logical world.
    public sealed class PlayerNetworkInput
    {
        readonly Player player;
        readonly PlayerActionBuffer capturedActions = new PlayerActionBuffer();
        int nextInputId;
        public Vector3 WorldMoveDirection { get; private set; }
        public PlayerNetworkInput(Player player) { this.player = player; }
        public void Capture()
        {
            if (!player.IsLocalPlayer || !player.isActiveAndEnabled || !player.IsBattleBound) return;
            var actions = CharacterInputSystem.Instance.inputActions.Player;
            Vector2 input = actions.Movement.ReadValue<Vector2>();
            Vector3 direction = Quaternion.Euler(0, player.camera.eulerAngles.y, 0) * new Vector3(input.x, 0, input.y);
            WorldMoveDirection = direction;
            BattleData.Instance.UpdateMoveDir(input == Vector2.zero ? 121 : EncodeDirection(direction));
            int facing = EncodeDirection(input == Vector2.zero ? player.BattleInputAim() : direction);
            int moving = input == Vector2.zero ? 0 : 1;
            // Capture intent only; cooldowns/cancellation/eligibility are decided after server echo.
            // Stable simultaneous-input order lets Dash + Attack resolve as a dodge attack.
            if (actions.Dash.WasPressedThisFrame()) CaptureAction(RightOpType.rop4, facing, moving);
            if (actions.FinishSkill.WasPressedThisFrame()) CaptureAction(RightOpType.rop3, facing, moving);
            if (actions.Skill.WasPressedThisFrame()) CaptureAction(RightOpType.rop2, facing, moving);
            if (actions.L_AtK.WasPressedThisFrame()) CaptureAction(RightOpType.rop1, facing, moving);
            if (capturedActions.Count == 0 || BattleData.Instance.PendingActionCount != 0) return;
            var raw = capturedActions.Dequeue();
            BattleData.Instance.UpdateRightOperation((RightOpType)raw.Type, raw.Value1, raw.Value2);
        }
        void CaptureAction(RightOpType type, int facing, int moving) => capturedActions.Enqueue(new PlayerActionCommand(++nextInputId, (int)type, facing, moving));
        static int EncodeDirection(Vector3 direction)
        {
            float angle = Mathf.Repeat(Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg, 360f);
            return Mathf.Min(119, (int)(angle / 3f));
        }
        public void Clear() => capturedActions.Clear();
    }
}
