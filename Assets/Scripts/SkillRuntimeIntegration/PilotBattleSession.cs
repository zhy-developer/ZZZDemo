using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using GameProtocol;
using SkillConfig;
using UnityEngine;

namespace SkillRuntime.Integration
{
    // One match-level scheduler, driven ONLY by consumed network frames. No Update/Time clock here.
    public sealed class PilotBattleSession : ICombatStateAdapter, IDisposable
    {
        sealed class Actor
        {
            internal EntityKey Key;
            internal RoleBase Role;
            internal CharacterHealthBase Health;
            internal StunInterval Stun;
            internal KnockbackPlan Knockback;
            internal int LastOp, Move = 121, PendingIndex = -1, PendingFacing;
            internal CastKey? PendingOwner;
            // Presentation-only counters, never read by eligibility, movement, snapshots or state hashes.
            internal long VisualHitStart = -1;
            internal float VisualHitSeconds;
        }
        readonly Actor[] actors;
        readonly PilotConfiguration config;
        readonly PilotPresentation presentation;
        CombatFrameSnapshot baseline;
        long revision;
        bool disposed;
        readonly string networkMatch;
        public MatchLifecycle.FinalResult TerminalSnapshot { get; private set; }
        public string TerminalState { get; private set; }
        double tailSeconds;
        long tailFrame;
        public readonly SkillFrameRuntime Runtime;
        public bool Faulted { get; private set; }
        public string Fault { get; private set; }
        public string Digest => config.Digest;
        public CombatCommitReceipt LastReceipt { get; private set; }
        public RoundResult TerminalResult { get; private set; }
        public EntityKey? TerminalWinner { get; private set; }
        public string LastStateHash { get; private set; }
        public int FeedbackCount { get; private set; }
        public int ActiveVfx => presentation?.ActiveVfx ?? 0;
        public int ActiveSfx => presentation?.ActiveSfx ?? 0;
        public IPilotFrameDiagnostics Diagnostics { get; set; }
        public bool VerboseFrameLogging { get; set; } = true;
        void Observe(Action<IPilotFrameDiagnostics> action)
        {
            if (Diagnostics == null) return;
            try { action(Diagnostics); }
            catch (Exception e) { Diagnostics = null; Debug.LogError("[PilotDiagnostics] observer detached: " + e); }
        }
        public PilotBattleSession(PilotConfiguration configuration, IEnumerable<RoleBase> roles, long sessionKey, string networkMatchId = null)
        {
            networkMatch = networkMatchId;
            config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            var ordered = roles.OrderBy(r => r.Player.BattleID).ToArray();
            if (ordered.Length != 2 || ordered.Any(r => !r || !r.Player || r.Player.IsDeterministicPilot)
                || ordered[0].Player.BattleID != 1 || ordered[1].Player.BattleID != 2) throw new InvalidOperationException("Two fresh real Players with battle IDs 1/2 required.");
            foreach (var r in ordered)
            {
                if (r.Player.playerSO.ComboData.comboData.lightCombo != config.Container) throw new InvalidOperationException("Real Player combo container differs from validated pilot.");
                foreach (string state in config.Skills.SelectMany(s => s.Animations.Count == 0 ? new[] { s.State } : s.Animations.Select(a => a.State)).Concat(new[] { config.Idle, config.Hit, config.Death }))
                    if (string.IsNullOrEmpty(state) || !r.Player.characterAnimator.HasState(0, Animator.StringToHash(state))) throw new InvalidOperationException("Required Animator state missing: " + state);
                foreach (var skill in config.Skills)
                    foreach (var cue in skill.CopyPresentation().vfx.Where(c => c.attachment == AttachmentMode.Bone))
                    {
                        var root = r.Player.characterAnimator.transform;
                        if (!root.Find(cue.bone) && (string.IsNullOrEmpty(cue.bone) || cue.bone.Contains("/") || root.GetComponentsInChildren<Transform>(true).Count(t => t.name == cue.bone) != 1))
                            throw new InvalidOperationException(skill.Skill.SkillId + "/" + cue.id + ": missing/ambiguous VFX bone " + cue.bone);
                    }
            }
            actors = ordered.Select(r => new Actor { Key = new EntityKey(sessionKey, 1, r.Player.BattleID, 1), Role = r }).ToArray();
            Runtime = new SkillFrameRuntime(actors[0].Key, actors[1].Key);
            foreach (var a in actors)
            {
                a.Role.CancelRootMotion(); a.Role.Player.BindDeterministicPilot();
                a.Role.Player.BindPilotOpponent(actors.Single(other => !other.Key.Equals(a.Key)).Role);
                a.Health = a.Role.Player.GetComponent<CharacterHealthBase>() ?? a.Role.Player.gameObject.AddComponent<CharacterHealthBase>();
                a.Health.InitializePilotHealth(config.HP);
                a.Role.PilotFace(a.Key.BattleId == 1 ? 0 : 60);
                a.Role.Player.PilotDestroyed += Dispose;
            }
            presentation = new PilotPresentation(config, ordered);
            Debug.Log("[PilotMatch] mode=DeterministicPilot rules=" + PilotBattleProfile.Rules + " digest=" + Digest + " entity=" + actors[0].Key + "," + actors[1].Key);
        }
        public CharacterHealthBase Health(int battleId) => actors.Single(a => a.Key.BattleId == battleId).Health;
        public CastFrame Cast(int battleId) => Runtime.Current(actors.Single(a => a.Key.BattleId == battleId).Key);
        bool Restricted(Actor a, long frame) => a.Health.ReadPilotHealth() == 0 || a.Stun.Contains(frame)
            || (a.Knockback != null && frame >= a.Knockback.StartFrame && frame < a.Knockback.EndFrame);
        PilotSkill Binding(CastFrame cast) => config.Skills.Single(s => ReferenceEquals(s.Skill, cast.Skill));
        void Start(Actor a, int index, int facing)
        {
            var previous = Runtime.Current(a.Key);
            if (previous != null) a.Role.PilotStopMotion(previous.Key);
            var skill = config.Skills[index]; a.Role.PilotFace(facing);
            var key = Runtime.StartAccepted(a.Key, skill.Skill);
            Runtime.OwnHandle(key, "root/" + key);
            if (skill.TimelineMotion != null) a.Role.PilotStartMotion(key, skill.TimelineMotion);
            else a.Role.PilotStartMotion(key, skill.Motion, skill.MotionEnd, skill.DistancePermille);
            a.PendingIndex = -1; a.PendingOwner = null;
            Debug.Log("[PilotCast] F=" + Runtime.OpenFrame + " " + key + " skill=" + skill.Skill.SkillId + " k=0");
        }
        void Attack(Actor a, int facing)
        {
            long frame = Runtime.OpenFrame.Value;
            if (Restricted(a, frame)) { Reject(a, "dead/stun/knockback"); return; }
            var cast = Runtime.Current(a.Key);
            if (cast == null) { Start(a, 0, facing); return; }
            var bound = Binding(cast); int k = cast.SkillFrame;
            if (k < bound.PreInput) { Reject(a, "pre-input closed"); return; }
            int index = k < bound.LinkEnd ? (config.Skills.IndexOf(bound) + 1) % config.ContainerCount : 0;
            if (index >= config.Skills.Count) { Reject(a, "next combo is outside pilot allowlist"); return; }
            if (k >= bound.Ready) Start(a, index, facing);
            else if (a.PendingIndex < 0) { a.PendingIndex = index; a.PendingFacing = facing; a.PendingOwner = cast.Key; }
        }
        static void Reject(Actor a, string why) => Debug.Log("[PilotReject] entity=" + a.Key + " reason=" + why);
        public bool Advance(long frame, AllPlayerOperation operations)
        {
            if (disposed || Faulted || networkMatch != null && TerminalSnapshot != null) return false;
            try
            {
                if (operations == null || operations.operations.Any(o => o == null || o.battleID < 1 || o.battleID > 2)
                    || operations.operations.GroupBy(o => o.battleID).Any(g => g.Count() != 1)) throw new InvalidOperationException("Malformed whole-match operation set.");
                if (Runtime.TryBeginFrame(frame) != FrameBeginResult.Started) return false;
                if (Diagnostics != null) Observe(d => d.Begin(frame));
                foreach (var op in operations.operations.OrderBy(o => o.battleID))
                {
                    var a = actors[op.battleID - 1]; a.Move = op.move >= 0 && op.move <= 120 ? op.move % 120 : 121; a.Role.PilotInput(a.Move);
                    if (op.rightOperation == RightOpType.noop || op.operationID <= a.LastOp || op.operationID <= 0) continue;
                    a.LastOp = op.operationID;
                    // Acknowledge the reliable packet even if the logical action is rejected. Never clear raw/outbound queues.
                    BattleData.Instance.UpdateRightOperationID(op.battleID, op.operationID, op.rightOperation);
                    if (op.rightOperation != RightOpType.rop1 || op.operationValue1 < 0 || op.operationValue1 > 120) Reject(a, "unsupported action/facing");
                    else Attack(a, op.operationValue1 % 120);
                }
                foreach (var a in actors)
                {
                    var cast = Runtime.Current(a.Key);
                    if (a.PendingIndex >= 0)
                    {
                        if (cast == null || !Nullable.Equals(a.PendingOwner, cast.Key) || Restricted(a, frame)) { a.PendingIndex = -1; a.PendingOwner = null; }
                        else if (cast.SkillFrame >= Binding(cast).Ready) Start(a, a.PendingIndex, a.PendingFacing);
                    }
                    cast = Runtime.Current(a.Key);
                    if (cast != null && a.Move < 120 && cast.SkillFrame >= Binding(cast).MoveCancel && !Restricted(a, frame))
                    {
                        Runtime.Exit(cast.Key, CastExitReason.Interrupted); a.Role.PilotStopMotion(cast.Key); a.PendingIndex = -1; a.PendingOwner = null;
                    }
                    a.Role.PilotMove(frame, Runtime.Current(a.Key) == null && !Restricted(a, frame), a.Knockback);
                }
                var geometry = actors.Select(a => new EntityGeometrySnapshot(a.Key,
                    new LogicPose(a.Role.LogicalPosition.x, 0, a.Role.LogicalPosition.y, a.Role.LogicalFacing * 1000), config.Hurt)).ToArray();
                Runtime.CapturePostMove(geometry);
                var capturedCasts = actors.Select(a => Runtime.Current(a.Key)).Where(c => c != null).ToArray();
                baseline = new CombatFrameSnapshot(frame, revision, actors.Select((a, i) => new CombatantSnapshot(geometry[i], Runtime.Current(a.Key)?.Key, a.Health.ReadPilotHealth(), a.Stun, a.Knockback)));
                var batch = CombatResolver.Resolve(Runtime, baseline);
                if (!batch.TryCommit(this, out var receipt, out var error)) throw new InvalidOperationException("Atomic combat commit refused: " + error);
                LastReceipt = receipt;
                if (receipt.Round != RoundResult.None) { TerminalResult = receipt.Round; TerminalWinner = receipt.Winner; } // Never send legacy game-over.
                Runtime.CompleteFrame();
                // Freeze immediately after the complete transaction, before host cleanup or presentation callbacks.
                if (networkMatch != null && TerminalResult != RoundResult.None && TerminalSnapshot == null)
                {
                    TerminalState = DescribeState() + "\naccepted=" + string.Join(";", Runtime.Committed.Select(c => c.ToString())) +
                        "\nexits=" + string.Join(";", Runtime.Exits.Select(e => e.Cast + ":" + e.Reason));
                    TerminalSnapshot = new MatchLifecycle.FinalResult(networkMatch, actors[0].Key.Session, 1, frame,
                        TerminalResult == RoundResult.Draw ? MatchLifecycle.Outcome.Draw : MatchLifecycle.Outcome.Winner,
                        TerminalWinner?.BattleId ?? 0, SkillConfigHash.Sha256(TerminalState), PilotBattleProfile.Rules, Digest);
                }
                foreach (var exit in Runtime.Exits)
                {
                    var a = actors.Single(v => v.Key.Equals(exit.Cast.Entity)); a.Role.PilotStopMotion(exit.Cast);
                    if (Nullable.Equals(a.PendingOwner, exit.Cast)) { a.PendingIndex = -1; a.PendingOwner = null; }
                }
                LastStateHash = SkillConfigHash.Sha256(DescribeState());
                if (VerboseFrameLogging)
                {
                Debug.Log("[PilotFrame] F=" + frame + " hash=" + LastStateHash + " " + DescribePlayers());
                Debug.Log("[PilotSettlement] F=" + frame + " records=" + Runtime.RecordCount + " accepted=" + string.Join(";", Runtime.Committed.Select(c => c.ToString()))
                    + " exits=" + string.Join(";", Runtime.Exits.Select(e => e.Cast + ":" + e.Reason)) + " result=" + TerminalResult + " winner=" + TerminalWinner);
                Debug.Log("[PilotTimeline] F=" + frame + " clips=" + string.Join(";", actors.Select(a => {
                    var c = Runtime.Current(a.Key); return a.Key + ":" + (c == null ? "none" : c.Key + "/" + c.Skill.SegmentAt(c.SkillFrame).Id + "/" + c.SkillFrame);
                })) + " events=" + string.Join(";", Runtime.Events.Select(e => e.ToString()))
                    + " feedback=" + string.Join(";", receipt.Feedback.Select(h => h.Source.ToString())));
                }
                if (Diagnostics != null) Observe(d => d.Committed(frame, operations, this));
                // All authority writes, ledger and exit cleanup are complete before any external presentation callback.
                foreach (var a in actors)
                {
                    try { Present(a, receipt); } catch (Exception ex) { Debug.LogException(ex); }
                }
                try { presentation.Tick(frame, capturedCasts, Runtime.Exits, receipt.Feedback); if (VerboseFrameLogging) Debug.Log("[PilotEffects] F=" + frame + " requests=" + presentation.LastRequests); }
                catch (Exception ex) { Debug.LogWarning("[PilotPresentation] Frame=" + frame + " " + ex.Message + "; authority already committed."); }
                if (TerminalSnapshot != null)
                {
                    foreach (var cast in capturedCasts) presentation.Exit(cast.Key, CastExitReason.Interrupted, frame);
                    foreach (var a in actors)
                    { a.PendingIndex = -1; a.PendingOwner = null; a.Role.CancelRootMotion(); a.Role.Player.PilotRoundClosed = true; }
                    Runtime.Dispose(); tailFrame = frame;
                    Debug.Log("[RoundLocal] match=" + networkMatch + " round=1 F=" + frame + " result=" + TerminalSnapshot.Result +
                        " winner=" + TerminalSnapshot.Winner + " hash=" + TerminalSnapshot.Hash);
                }
                if (Diagnostics != null) Observe(d => d.End(frame));
                return true;
            }
            catch (Exception ex) { Faulted = true; Fault = ex.ToString(); Debug.LogError("[PilotHalt] No legacy fallback; reinitialize match. " + Fault); return false; }
        }
        // Presentation tail clock only. Authority and terminal snapshot never advance here.
        public void UpdateTerminalPresentation(double elapsedSeconds)
        {
            if (disposed || TerminalSnapshot == null) return;
            tailSeconds += Math.Max(0, Math.Min(elapsedSeconds, .25));
            while (tailSeconds >= .033)
            {
                tailSeconds -= .033;
                presentation.Tick(++tailFrame, new CastFrame[0], new CastExit[0], new HitFeedback[0]);
            }
        }
        public bool TryApply(CombatFrameSnapshot expected, ReadOnlyCollection<TargetResolution> results, out string error)
        {
            error = null;
            if (disposed || Faulted || !ReferenceEquals(expected, baseline) || expected.Revision != revision || Runtime.OpenFrame != expected.Frame || results.Count != 2)
            { error = "Frame/revision/session baseline invalid"; return false; }
            var ordered = results.OrderBy(r => r.Target).ToArray(); long nextRevision = checked(revision + 1);
            for (int i = 0; i < actors.Length; i++)
            {
                var a = actors[i]; var row = ordered[i]; var e = expected.Entities[i]; var pos = a.Role ? a.Role.LogicalPosition : default(GameVector2);
                if (!a.Role || !a.Role.Player || !a.Health || !row.Target.Equals(a.Key) || !ReferenceEquals(row.Expected, e)
                    || a.Health.ReadPilotHealth() != e.HP || !Nullable.Equals(Runtime.Current(a.Key)?.Key, e.Cast)
                    || pos.x != e.Geometry.Pose.X || pos.y != e.Geometry.Pose.Z || a.Role.LogicalFacing * 1000 != e.Geometry.Pose.FacingMilliDegrees
                    || a.Stun.Start != e.Stun.Start || a.Stun.End != e.Stun.End || !ReferenceEquals(a.Knockback, e.Knockback)
                    || row.FinalHP < 0 || row.FinalHP > 16777216)
                { error = "Whole player baseline invalid at " + a.Key; return false; }
            }
            for (int i = 0; i < actors.Length; i++)
            {
                actors[i].Health.WritePilotHealth(ordered[i].FinalHP);
                actors[i].Stun = ordered[i].Stun; actors[i].Knockback = ordered[i].Knockback;
            }
            revision = nextRevision; return true;
        }
        void Present(Actor a, CombatCommitReceipt receipt)
        {
            var row = receipt.Targets.Single(r => r.Target.Equals(a.Key));
            if (row.ExpectedOldHP != row.FinalHP) a.Health.PublishPilotHealth();
            var cast = Runtime.Current(a.Key); var hit = receipt.Feedback.Any(f => f.Source.Target.Equals(a.Key));
            FeedbackCount += receipt.Feedback.Count(f => f.Source.Target.Equals(a.Key));
            if (a.Health.IsDead) { a.VisualHitStart = -1; a.Role.Player.PilotVisual(config.Death, .95f); }
            else if (cast != null)
            {
                a.VisualHitStart = -1; var skill = Binding(cast);
                var segment = skill.Animations.FirstOrDefault(s => s.Time.Start <= cast.SkillFrame && cast.SkillFrame < s.Time.End);
                if (segment == null) a.Role.Player.PilotVisual(skill.State, (float)(cast.SkillFrame * NetConfig.frameTime / 1000.0 / skill.Clip.length));
                else a.Role.Player.PilotVisual(segment.State, (float)(segment.Time.SourceNs(cast.SkillFrame) / 1000000000.0 / segment.Clip.length));
            }
            else
            {
                if (hit)
                {
                    a.VisualHitStart = receipt.Frame; a.Role.Player.PilotVisual(config.Hit, 0);
                    a.VisualHitSeconds = a.Role.Player.characterAnimator.GetCurrentAnimatorStateInfo(0).length;
                }
                double elapsed = (receipt.Frame - a.VisualHitStart) * NetConfig.frameTime / 1000.0;
                if (a.VisualHitStart >= 0 && a.VisualHitSeconds > 0 && (elapsed < a.VisualHitSeconds || a.Stun.Contains(receipt.Frame)))
                    a.Role.Player.PilotVisual(config.Hit, Mathf.Min(.99f, (float)(elapsed / a.VisualHitSeconds)));
                else a.Role.Player.PilotVisual(config.Idle, 0);
            }
        }
        string DescribePlayers() => string.Join(";", actors.Select(a => FormattableString.Invariant($"{a.Key}:pos={a.Role.LogicalPosition.x},{a.Role.LogicalPosition.y};facing={a.Role.LogicalFacing};hp={a.Health.ReadPilotHealth()};cast={Runtime.Current(a.Key)?.Key};k={Runtime.Current(a.Key)?.SkillFrame};stun={a.Stun.Start},{a.Stun.End}")));
        public string DescribeState()
        {
            var b = new StringBuilder(config.Digest).Append('\n').Append(Runtime.DescribeState()).Append('\n').Append(DescribePlayers()).Append('\n').Append(TerminalResult).Append('/').Append(TerminalWinner);
            foreach (var a in actors)
            {
                b.Append(FormattableString.Invariant($"\n{a.Key}/{a.LastOp}/{a.Move}/{a.PendingIndex}/{a.PendingFacing}/{a.PendingOwner}"));
                if (a.Knockback != null) b.Append(FormattableString.Invariant($"/{a.Knockback.Source}/{a.Knockback.StartFrame}/{a.Knockback.Duration}/{a.Knockback.WorldTotal.X}/{a.Knockback.WorldTotal.Z}"));
            }
            return b.ToString();
        }
        public SortedDictionary<string, string> DescribeDiagnosticFields()
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal) {
                ["config"] = Digest, ["rules"] = PilotBattleProfile.Rules,
                ["revision"] = revision.ToString(CultureInfo.InvariantCulture),
                ["result"] = TerminalResult + "/" + TerminalWinner };
            string[] names = { "clock", "sequences", "casts", "ledger", "handles", "deferredExits", "candidates", "committed", "events", "exits" };
            var lines = Runtime.DescribeState().Split('\n');
            for (int i = 0; i < lines.Length; i++) fields["runtime." + names[i]] = lines[i];
            foreach (var a in actors)
            {
                string key = "entity." + a.Key.BattleId.ToString(CultureInfo.InvariantCulture) + ".";
                var cast = Runtime.Current(a.Key);
                fields[key + "identity"] = a.Key.ToString();
                fields[key + "position"] = FormattableString.Invariant($"{a.Role.LogicalPosition.x},{a.Role.LogicalPosition.y}");
                fields[key + "facing"] = a.Role.LogicalFacing.ToString(CultureInfo.InvariantCulture);
                fields[key + "hp"] = a.Health.ReadPilotHealth().ToString(CultureInfo.InvariantCulture);
                fields[key + "cast"] = cast == null ? "none" : FormattableString.Invariant($"{cast.Key}/{cast.Skill.SkillId}/{cast.SkillFrame}");
                fields[key + "stun"] = FormattableString.Invariant($"{a.Stun.Start},{a.Stun.End}");
                fields[key + "input"] = FormattableString.Invariant($"{a.LastOp}/{a.Move}/{a.PendingIndex}/{a.PendingFacing}/{a.PendingOwner}");
                fields[key + "motion"] = a.Role.DescribePilotMotion();
                fields[key + "knockback"] = a.Knockback == null ? "none" : FormattableString.Invariant($"{a.Knockback.Source}/{a.Knockback.StartFrame}/{a.Knockback.Duration}/{a.Knockback.WorldTotal.X}/{a.Knockback.WorldTotal.Z}");
            }
            fields["map"] = FormattableString.Invariant($"{BattleData.Instance.mapMinX},{BattleData.Instance.mapMaxX},{BattleData.Instance.mapMinY},{BattleData.Instance.mapMaxY}");
            return fields;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Runtime.Dispose();
            presentation?.Dispose();
            foreach (var a in actors)
            {
                if (!a.Role) continue;
                a.Role.CancelRootMotion(); if (a.Role.Player) { a.Role.Player.PilotDestroyed -= Dispose; a.Role.Player.BindPilotOpponent(null); }
            }
        }
    }
}
