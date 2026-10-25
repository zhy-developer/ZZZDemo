using System;
using System.Collections.Generic;
using GameProtocol;
using UnityEngine;

namespace ZZZ.SkillRuntime
{
    /// <summary>网络/场景适配层。负责开局绑定、操作去重确认和状态显示；命中计算交给 CombatWorld。</summary>
    public sealed class FrameCombatSession : IDisposable
    {
        public CombatWorld World { get; private set; }
        private readonly List<RoleBase> roles;
        private readonly List<CombatSkillLibrary> libraries = new List<CombatSkillLibrary>();
        private readonly List<CombatCommand> commands = new List<CombatCommand>();
        private readonly List<PlayerOperation> orderedOperations = new List<PlayerOperation>();

        public FrameCombatSession(IEnumerable<RoleBase> sceneRoles)
        {
            roles = new List<RoleBase>(sceneRoles);
            roles.Sort((a, b) => a.NetworkPlayer.BattleID.CompareTo(b.NetworkPlayer.BattleID));
            try
            {
                var fighters = new List<CombatFighter>();
                foreach (var role in roles)
                {
                    var library = new CombatSkillLibrary(role.NetworkPlayer.playerSO.frameCombat);
                    libraries.Add(library);
                    var pos = role.objShape.GetPosition();
                    fighters.Add(new CombatFighter(role.NetworkPlayer.BattleID, library, new Vector2Int(pos.x, pos.y),
                        role.NetworkPlayer.BattleID == 1 ? 0 : 180));
                }
                var data = BattleData.Instance;
                World = new CombatWorld(fighters, data.mapMinX, data.mapMaxX, data.mapMinY, data.mapMaxY);
                for (int i = 0; i < roles.Count; i++)
                {
                    roles[i].BindFrameCombat(fighters[i]);
                    roles[i].NetworkPlayer.enemy = roles[1 - i].NetworkPlayer.transform;
                }
            }
            catch { Dispose(); throw; }
        }

        public void Step(AllPlayerOperation frame)
        {
            commands.Clear(); orderedOperations.Clear();
            if (frame != null) foreach (var operation in frame.operations)
                if (operation != null && roles.Exists(r => r.NetworkPlayer.BattleID == operation.battleID)) orderedOperations.Add(operation);
            orderedOperations.Sort((a, b) => a.battleID != b.battleID ? a.battleID.CompareTo(b.battleID) : a.operationID.CompareTo(b.operationID));
            foreach (var operation in orderedOperations)
            {
                int action = 0;
                if (operation.rightOperation != RightOpType.noop && BattleData.Instance.IsValidRightOp(operation.battleID, operation.operationID))
                {
                    action = (int)operation.rightOperation;
                    // 即使受击/顿帧时拒绝动作，也必须确认该序号，否则重传会在恢复后补放旧输入。
                    BattleData.Instance.UpdateRightOperationID(operation.battleID, operation.operationID, operation.rightOperation);
                }
                commands.Add(new CombatCommand(operation.battleID, operation.move, action, operation.operationValue1, operation.operationValue2));
            }
            bool wasFinished = World.Finished;
            World.Step(commands);
            // 结束帧仍显示最终姿势；后续仅确认网络输入，不再推进移动控制器或表现混合。
            if (wasFinished) return;
            foreach (var role in roles) role.SyncFrameCombat(World.Finished);
        }

        public void Dispose()
        {
            foreach (var role in roles) if (role != null) role.ReleaseFrameCombat();
            foreach (var library in libraries) library.Dispose();
            libraries.Clear(); World = null;
        }
    }
}
