using System;
using System.Collections.Generic;
using System.Linq;
using GameProtocol;
using MatchLifecycle;
using SkillConfig;
using FinalResult = MatchLifecycle.FinalResult;

namespace SkillRuntime.Integration
{
    // Network/result adapter only. No scene objects, animation or presentation consumer required.
    public sealed class PilotBattleSession : IDisposable
    {
        readonly BattleWorld world;
        readonly string match, rules;
        readonly long session;
        public SkillFrameRuntime Runtime => world.Runtime;
        public BattleFrame Latest => world.Latest;
        public FinalResult TerminalSnapshot { get; private set; }
        public string TerminalState => world.TerminalState;
        public bool Faulted => world.Faulted;
        public string Fault => world.Fault;
        public string Digest { get; }
        public CombatCommitReceipt LastReceipt => world.LastReceipt;
        public RoundResult TerminalResult => world.Result;
        public EntityKey? TerminalWinner => world.Winner;
        public string LastStateHash => Latest.StateHash;
        public PilotBattleSession(BattleConfiguration configuration, IEnumerable<BattleSpawn> spawns, BattleBounds bounds,
            IEnumerable<XzDelta> directions, long sessionKey, string networkMatchId, string ruleVersion)
        {
            Digest = configuration.Digest; session = sessionKey; match = networkMatchId; rules = ruleVersion;
            world = new BattleWorld(configuration, spawns, bounds, directions, sessionKey);
        }
        public bool Advance(long frame, AllPlayerOperation operations)
        {
            if (operations == null || operations.operations.Any(o => o == null)) return false;
            if (!world.Advance(frame, operations.operations.Select(o => new BattleInput(o.battleID, o.move, (int)o.rightOperation, o.operationID, o.operationValue1, o.operationValue2)))) return false;
            if (world.Result != RoundResult.None && match != null && TerminalSnapshot == null)
                TerminalSnapshot = new FinalResult(match, session, 1, frame,
                    world.Result == RoundResult.Draw ? Outcome.Draw : Outcome.Winner, world.Winner?.BattleId ?? 0,
                    SkillConfigHash.Sha256(world.TerminalState), rules, Digest);
            return true;
        }
        public string DescribeState() => world.DescribeState();
        public SortedDictionary<string, string> DescribeDiagnosticFields() => world.DescribeDiagnosticFields();
        public void Dispose() => world.Dispose();
    }
}
