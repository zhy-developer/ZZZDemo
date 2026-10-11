using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using SkillConfig;

namespace SkillRuntime
{
    public sealed class HitCandidate : IComparable<HitCandidate>
    {
        public long Frame { get; }
        public CastKey Cast { get; }
        public EntityKey Attacker => Cast.Entity;
        public EntityKey Target { get; }
        public string SkillId { get; }
        public string GroupId { get; }
        public string HitBoxId { get; }
        internal RuntimeGroup Group { get; }
        internal HitCandidate(long frame, CastFrame cast, EntityKey target, AttackShape box)
        { Frame = frame; Cast = cast.Key; Target = target; SkillId = cast.Skill.SkillId; GroupId = box.GroupId; HitBoxId = box.Id; Group = cast.Skill.Group(box.GroupId); }
        public int CompareTo(HitCandidate b)
        {
            int c = Frame.CompareTo(b.Frame); if (c != 0) return c;
            c = Cast.CompareTo(b.Cast); if (c != 0) return c;
            c = StringComparer.Ordinal.Compare(GroupId, b.GroupId); if (c != 0) return c;
            c = Target.CompareTo(b.Target); return c != 0 ? c : StringComparer.Ordinal.Compare(HitBoxId, b.HitBoxId);
        }
        public override string ToString() => Frame.ToString(CultureInfo.InvariantCulture) + "|" + Cast + "|" + SkillId + "|" + GroupId + "|" + Target + "|" + HitBoxId;
    }
    public sealed class EventOccurrence
    {
        public long Frame { get; }
        public CastKey Cast { get; }
        public string SkillId { get; }
        public RuntimeEvent Event { get; }
        internal EventOccurrence(long frame, CastFrame cast, RuntimeEvent e) { Frame = frame; Cast = cast.Key; SkillId = cast.Skill.SkillId; Event = e; }
        public override string ToString() => Frame.ToString(CultureInfo.InvariantCulture) + "|" + Cast + "|" + SkillId + "|" + Event.Order.ToString(CultureInfo.InvariantCulture) + "|" + Event.Id;
    }
    public static class HitCandidateCollector
    {
        // No ledger, defense, HP, damage or effects. Input positions are explicitly post-move snapshots.
        public static ReadOnlyCollection<HitCandidate> Collect(long frame, IEnumerable<CastFrame> casts, IEnumerable<EntityGeometrySnapshot> snapshots)
        {
            if (frame <= 0) throw new ArgumentOutOfRangeException(nameof(frame));
            var entities = snapshots.ToArray(); var active = casts.ToArray();
            if (entities.Length != 2 || entities.Any(e => e == null) || entities[0].Entity.BattleId == entities[1].Entity.BattleId
                || entities[0].Entity.Session != entities[1].Entity.Session || entities[0].Entity.Round != entities[1].Entity.Round)
                throw new ArgumentException("Exactly two distinct PvP entities in one session/round are required.");
            if (active.Any(c => c == null) || active.Select(c => c.Key.Entity).Distinct().Count() != active.Length) throw new ArgumentException("At most one active cast per entity.");
            var result = new Dictionary<Tuple<CastKey, string, EntityKey>, HitCandidate>();
            foreach (var cast in active)
            {
                if (cast.StartFrame + cast.SkillFrame != frame || cast.SkillFrame < 0 || cast.SkillFrame >= cast.Skill.Duration) throw new ArgumentException("Cast frame does not match snapshot frame.");
                var source = entities.SingleOrDefault(e => e.Entity.Equals(cast.Key.Entity));
                if (source == null) throw new ArgumentException("Cast owner missing from snapshot.");
                var target = entities.Single(e => !e.Entity.Equals(source.Entity));
                foreach (var box in cast.Skill.Shapes)
                {
                    if (cast.SkillFrame < box.StartFrame || cast.SkillFrame >= box.EndFrame || !XzGeometry.Intersects(box, source.Pose, target.Hurt, target.Pose)) continue;
                    var key = Tuple.Create(cast.Key, box.GroupId, target.Entity);
                    if (!result.TryGetValue(key, out var existing) || StringComparer.Ordinal.Compare(box.Id, existing.HitBoxId) < 0)
                        result[key] = new HitCandidate(frame, cast, target.Entity, box);
                }
            }
            return Array.AsReadOnly(result.Values.OrderBy(c => c).ToArray());
        }
    }

    internal sealed class HitGroupLedger
    {
        readonly Dictionary<Tuple<CastKey, string, EntityKey>, long> records = new Dictionary<Tuple<CastKey, string, EntityKey>, long>();
        internal bool Eligible(HitCandidate c)
        {
            if (!records.TryGetValue(Tuple.Create(c.Cast, c.GroupId, c.Target), out long last)) return true;
            return c.Group.Mode == HitMode.Repeat && c.Frame > last && c.Frame - last >= c.Group.Interval;
        }
        internal bool Commit(HitCandidate c)
        {
            if (!Eligible(c)) return false;
            records[Tuple.Create(c.Cast, c.GroupId, c.Target)] = c.Frame; return true;
        }
        internal void Clear(CastKey cast) { foreach (var key in records.Keys.Where(k => k.Item1.Equals(cast)).ToArray()) records.Remove(key); }
        internal string Describe() => string.Join(";", records.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2, StringComparer.Ordinal).ThenBy(p => p.Key.Item3)
            .Select(p => p.Key.Item1 + "/" + p.Key.Item2 + "/" + p.Key.Item3 + "=" + p.Value.ToString(CultureInfo.InvariantCulture)));
        internal int Count => records.Count;
        internal HitGroupLedger Copy()
        {
            var copy = new HitGroupLedger(); foreach (var pair in records) copy.records.Add(pair.Key, pair.Value); return copy;
        }
    }
    public enum FrameBeginResult { Started, Duplicate, Gap, PreviousFrameOpen }
    public sealed class CastExit
    {
        public CastKey Cast { get; }
        public CastExitReason Reason { get; }
        public ReadOnlyCollection<string> ReleasedHandles { get; }
        internal CastExit(CastKey cast, CastExitReason reason, string[] handles) { Cast = cast; Reason = reason; ReleasedHandles = Array.AsReadOnly(handles); }
    }

    // An explicit frame transaction for isolated hosts. This class never pulls network data or drives Unity Update.
    public sealed class SkillFrameRuntime : IDisposable
    {
        sealed class LiveCast
        {
            internal CastKey Key; internal RuntimeSkill Skill; internal long Start;
            internal CastFrame At(long f) => new CastFrame(Key, Skill, Start, f);
        }
        readonly Dictionary<EntityKey, long> sequences = new Dictionary<EntityKey, long>();
        readonly Dictionary<EntityKey, LiveCast> active = new Dictionary<EntityKey, LiveCast>();
        readonly Dictionary<string, CastKey> handles = new Dictionary<string, CastKey>(StringComparer.Ordinal);
        HitGroupLedger ledger = new HitGroupLedger();
        List<HitCandidate> commits = new List<HitCandidate>();
        readonly List<CastExit> exits = new List<CastExit>();
        Dictionary<CastKey, CastExitReason> deferredExits = new Dictionary<CastKey, CastExitReason>();
        bool open, captured, committing, batchCommitted;
        long frame;
        public long LastCompletedFrame { get; private set; }
        public bool IsDisposed { get; private set; }
        public long? OpenFrame => open ? (long?)frame : null;
        public int RecordCount => ledger.Count;
        public ReadOnlyCollection<EntityGeometrySnapshot> GeometrySnapshots { get; private set; } = Array.AsReadOnly(new EntityGeometrySnapshot[0]);
        public ReadOnlyCollection<HitCandidate> Candidates { get; private set; } = Array.AsReadOnly(new HitCandidate[0]);
        public ReadOnlyCollection<EventOccurrence> Events { get; private set; } = Array.AsReadOnly(new EventOccurrence[0]);
        public ReadOnlyCollection<HitCandidate> Committed => Array.AsReadOnly(commits.OrderBy(c => c).ToArray());
        public ReadOnlyCollection<CastExit> Exits => Array.AsReadOnly(exits.OrderBy(e => e.Cast).ToArray());
        public SkillFrameRuntime(EntityKey first, EntityKey second, long lastCompletedFrame = 0)
        {
            if (first.Session <= 0 || first.Session != second.Session || first.Round != second.Round || first.BattleId == second.BattleId || lastCompletedFrame < 0)
                throw new ArgumentException("Two unique entities in one session/round and a nonnegative baseline frame are required.");
            sequences.Add(first, 0); sequences.Add(second, 0); LastCompletedFrame = lastCompletedFrame;
        }
        public FrameBeginResult TryBeginFrame(long next)
        {
            RequireNotCommitting();
            if (IsDisposed) throw new ObjectDisposedException(nameof(SkillFrameRuntime));
            if (next <= LastCompletedFrame || (open && next == frame)) return FrameBeginResult.Duplicate;
            if (open) return FrameBeginResult.PreviousFrameOpen;
            if (LastCompletedFrame == long.MaxValue || next != LastCompletedFrame + 1) return FrameBeginResult.Gap;
            frame = next; open = true; captured = false; batchCommitted = false; commits.Clear(); exits.Clear(); deferredExits.Clear();
            GeometrySnapshots = Array.AsReadOnly(new EntityGeometrySnapshot[0]);
            Candidates = Array.AsReadOnly(new HitCandidate[0]); Events = Array.AsReadOnly(new EventOccurrence[0]);
            return FrameBeginResult.Started;
        }
        public CastKey StartAccepted(EntityKey owner, RuntimeSkill skill)
        {
            RequireEditable();
            if (skill == null || !sequences.ContainsKey(owner)) throw new ArgumentException("Registered owner and validated skill required.");
            long sequence = checked(sequences[owner] + 1); // validate overflow before replacing the old cast.
            if (active.TryGetValue(owner, out var old)) ExitNow(old.Key, CastExitReason.Replaced);
            var key = new CastKey(owner, sequence);
            sequences[owner] = sequence; active.Add(owner, new LiveCast { Key = key, Skill = skill, Start = frame }); return key;
        }
        public CastFrame Current(EntityKey entity) => active.TryGetValue(entity, out var cast) ? cast.At(open ? frame : LastCompletedFrame) : null;
        public void OwnHandle(CastKey cast, string handle)
        {
            RequireEditable();
            if (!IsCurrent(cast) || string.IsNullOrWhiteSpace(handle)) throw new ArgumentException("Only the current cast can own a nonempty handle.");
            if (handles.TryGetValue(handle, out var owner) && !owner.Equals(cast)) throw new InvalidOperationException("Handle already belongs to another active cast.");
            handles[handle] = cast;
        }
        public bool OwnsHandle(CastKey cast, string handle) => handles.TryGetValue(handle, out var owner) && owner.Equals(cast);
        public bool Exit(CastKey cast, CastExitReason reason)
        {
            RequireNotCommitting();
            if (!Enum.IsDefined(typeof(CastExitReason), reason)) throw new ArgumentException("Unknown exit reason.");
            if (!IsCurrent(cast)) return false;
            if (!open) throw new InvalidOperationException("Exit must belong to an explicit frame transaction.");
            if (!captured) return ExitNow(cast, reason);
            if (batchCommitted) throw new InvalidOperationException("The committed batch owns exits until CompleteFrame.");
            // Product priority is now explicitly approved: Death > Interrupted > Completed.
            if (deferredExits.TryGetValue(cast, out var previous))
            { var selected = ResolveExit(previous, reason); if (selected == previous) return false; deferredExits[cast] = selected; return true; }
            deferredExits.Add(cast, reason); return true;
        }
        public void CapturePostMove(IEnumerable<EntityGeometrySnapshot> snapshots)
        {
            RequireEditable();
            var values = snapshots.ToArray();
            if (values.Length != 2 || values.Any(v => v == null || !sequences.ContainsKey(v.Entity)) || values.Select(v => v.Entity).Distinct().Count() != 2)
                throw new ArgumentException("Both registered entities must provide post-move snapshots.");
            var casts = active.Values.Select(c => c.At(frame)).ToArray();
            var candidates = HitCandidateCollector.Collect(frame, casts, values); // failure leaves frame editable, no partial mutation.
            var events = casts.SelectMany(c => c.Skill.Events.Where(e => e.Frame == c.SkillFrame).Select(e => new EventOccurrence(frame, c, e)))
                .OrderBy(e => e.Cast).ThenBy(e => e.Event.Order).ThenBy(e => e.Event.Id, StringComparer.Ordinal).ToArray();
            GeometrySnapshots = Array.AsReadOnly(values.OrderBy(v => v.Entity).ToArray());
            Candidates = candidates; Events = Array.AsReadOnly(events); captured = true;
        }
        public bool CanAccept(HitCandidate candidate) => open && captured && candidate != null && candidate.Frame == frame
            && Candidates.Contains(candidate) && ledger.Eligible(candidate);
        // External Phase 3C policy calls this only for accepted candidates. No damage/stun predicate exists here.
        public bool TryCommitAccepted(HitCandidate candidate)
        {
            RequireNotCommitting();
            if (batchCommitted) return false;
            if (!CanAccept(candidate) || !ledger.Commit(candidate)) return false;
            commits.Add(candidate); return true;
        }
        public void CompleteFrame()
        {
            RequireNotCommitting();
            if (!open || !captured) throw new InvalidOperationException("Capture and resolve the frame before completion.");
            foreach (var exit in deferredExits.OrderBy(p => p.Key)) ExitNow(exit.Key, exit.Value);
            foreach (var cast in active.Values.Where(c => frame - c.Start == c.Skill.Duration - 1L).OrderBy(c => c.Key).ToArray()) ExitNow(cast.Key, CastExitReason.Completed);
            LastCompletedFrame = frame; open = false;
            // Committed outputs survive end-of-cast ledger cleanup until the next frame begins.
        }
        bool IsCurrent(CastKey key) => active.TryGetValue(key.Entity, out var cast) && cast.Key.Equals(key);
        bool ExitNow(CastKey key, CastExitReason reason)
        {
            if (!IsCurrent(key)) return false;
            var owned = handles.Where(p => p.Value.Equals(key)).Select(p => p.Key).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            foreach (var handle in owned) handles.Remove(handle);
            ledger.Clear(key); active.Remove(key.Entity); exits.Add(new CastExit(key, reason, owned)); return true;
        }
        void RequireNotCommitting() { if (committing) throw new InvalidOperationException("Combat adapter cannot reenter or mutate the runtime during atomic apply."); }
        void RequireEditable() { RequireNotCommitting(); if (!open || captured) throw new InvalidOperationException("Begin a frame; edit casts before capturing the common snapshot."); }
        public void Dispose()
        {
            RequireNotCommitting();
            if (IsDisposed) return;
            foreach (var cast in active.Values.OrderBy(c => c.Key).ToArray()) ExitNow(cast.Key, CastExitReason.Destroyed);
            deferredExits.Clear(); open = false; IsDisposed = true;
        }
        internal void ValidateCombatBaseline(CombatFrameSnapshot baseline)
        {
            RequireNotCommitting();
            if (!open || !captured || batchCommitted || commits.Count != 0 || deferredExits.Count != 0 || baseline.Frame != frame)
                throw new InvalidOperationException("Combat batch requires a captured, unresolved frame; do not mix single-hit commits and batches.");
            foreach (var state in baseline.Entities)
            {
                var geometry = GeometrySnapshots.SingleOrDefault(g => g.Entity.Equals(state.Entity));
                var cast = Current(state.Entity);
                if (!ReferenceEquals(geometry, state.Geometry) || !Nullable.Equals(state.Cast, cast == null ? (CastKey?)null : cast.Key))
                    throw new ArgumentException("Combat identity/cast/geometry baseline differs from the captured post-move frame.");
            }
        }
        static CastExitReason ResolveExit(CastExitReason a, CastExitReason b)
        {
            if (a == b) return a;
            int pa = ExitPriority(a), pb = ExitPriority(b);
            if (pa < 0 || pb < 0) throw new InvalidOperationException("Replacement/destruction must occur outside hit resolution.");
            return pa > pb ? a : b;
        }
        static int ExitPriority(CastExitReason r) => r == CastExitReason.Death ? 3 : r == CastExitReason.Interrupted ? 2 : r == CastExitReason.Completed ? 1 : -1;
        internal bool TryCommitBatch(CombatBatch batch, ICombatStateAdapter adapter, out CombatCommitReceipt receipt, out string error)
        {
            receipt = null; error = null;
            RequireNotCommitting();
            try
            {
                if (adapter == null || !ReferenceEquals(batch.Owner, this)) throw new ArgumentException("Batch owner/adapter mismatch.");
                ValidateCombatBaseline(batch.Baseline);
                if (!StringComparer.Ordinal.Equals(batch.RuntimeBaseline, DescribeState())) throw new InvalidOperationException("Runtime baseline changed; resolve again.");
                // All fallible runtime validation/allocation is performed before the host's atomic publish.
                var nextLedger = ledger.Copy(); var nextCommits = new List<HitCandidate>(batch.Accepted);
                var nextExits = new Dictionary<CastKey, CastExitReason>(deferredExits);
                foreach (var hit in batch.Accepted)
                    if (!CanAccept(hit) || !nextLedger.Commit(hit)) throw new InvalidOperationException("Hit eligibility changed.");
                foreach (var target in batch.Targets.Where(t => t.ExitReason.HasValue))
                {
                    var key = target.Expected.Cast.Value; var reason = target.ExitReason.Value;
                    nextExits[key] = nextExits.TryGetValue(key, out var old) ? ResolveExit(old, reason) : reason;
                }
                var preparedReceipt = new CombatCommitReceipt(batch);
                committing = true;
                if (!adapter.TryApply(batch.Baseline, batch.Targets, out error)) { error = error ?? "Host rejected the complete baseline."; return false; }
                // Non-throwing reference swaps only. No user callbacks, arithmetic or allocations after host apply.
                ledger = nextLedger; commits = nextCommits; deferredExits = nextExits; batchCommitted = true;
                receipt = preparedReceipt; return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
            finally { committing = false; }
        }
        // Canonical text, not Dictionary iteration/GetHashCode. Caller can hash this plus its own positions/HP.
        public string DescribeState()
        {
            string sequencesText = string.Join(";", sequences.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture)));
            string castsText = string.Join(";", active.Values.OrderBy(c => c.Key).Select(c => c.Key + "/" + c.Skill.CharacterId + "/" + c.Skill.SkillId + "/" + c.Start.ToString(CultureInfo.InvariantCulture)));
            string handlesText = string.Join(";", handles.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key.Length.ToString(CultureInfo.InvariantCulture) + ":" + p.Key + "=" + p.Value));
            return LastCompletedFrame.ToString(CultureInfo.InvariantCulture) + "|" + (open ? frame.ToString(CultureInfo.InvariantCulture) : "closed") + "|" + captured + "|" + IsDisposed + "|batch=" + batchCommitted + "\n" + sequencesText + "\n" + castsText + "\n" + ledger.Describe() + "\n" + handlesText
                + "\n" + string.Join(";", deferredExits.OrderBy(p => p.Key).Select(p => p.Key + "=" + (int)p.Value))
                + "\n" + string.Join(";", Candidates.Select(c => c.ToString())) + "\n" + string.Join(";", Committed.Select(c => c.ToString()))
                + "\n" + string.Join(";", Events.Select(e => e.ToString())) + "\n" + string.Join(";", Exits.Select(e => e.Cast + "=" + (int)e.Reason));
        }
    }
}
