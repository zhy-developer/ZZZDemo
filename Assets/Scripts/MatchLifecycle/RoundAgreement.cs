using System;
using System.Linq;

namespace MatchLifecycle
{
    public enum Outcome { None = 0, Winner = 1, Draw = 2 }
    public enum AgreementStatus { Waiting = 0, Confirmed = 1, Conflict = 2, Retired = 3, Rejected = 4 }
    public sealed class FinalResult : IEquatable<FinalResult>
    {
        public readonly string Match, Rules, Config, Hash;
        public readonly long Session, Frame;
        public readonly int Round, Protocol, Winner;
        public readonly Outcome Result;
        public string ConfirmationId => Match + "/" + Round + "/result";
        public FinalResult(string match, long session, int round, long frame, Outcome result, int winner,
            string hash, string rules, string config, int protocol = 1)
        {
            if (!Guid.TryParseExact(match, "N", out _) || session <= 0 || round != 1 || frame <= 0 || protocol != 1 ||
                (result != Outcome.Draw && result != Outcome.Winner) || (result == Outcome.Draw ? winner != 0 : winner != 1 && winner != 2) ||
                !Digest(hash) || !Digest(config) || string.IsNullOrEmpty(rules) || rules.Length > 128)
                throw new ArgumentException("Invalid final result identity, outcome or digest.");
            Match = match; Session = session; Round = round; Frame = frame; Result = result; Winner = winner;
            Hash = hash; Rules = rules; Config = config; Protocol = protocol;
        }
        public static bool Digest(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public bool Equals(FinalResult b) => b != null && Match == b.Match && Session == b.Session && Round == b.Round &&
            Frame == b.Frame && Result == b.Result && Winner == b.Winner && Hash == b.Hash && Rules == b.Rules && Config == b.Config && Protocol == b.Protocol;
        public override bool Equals(object obj) => Equals(obj as FinalResult);
        public override int GetHashCode() => ConfirmationId.GetHashCode();
    }

    // One server-owned single-match round. The transport authenticates the slot; callers cannot choose another member.
    // Confirmation means two matching reports, NOT independently simulated server combat.
    public sealed class RoundAgreement
    {
        readonly string match;
        readonly long session;
        readonly string[] ready = new string[2];
        readonly FinalResult[] reports = new FinalResult[2];
        readonly bool[] retired = new bool[2];
        string rules, config;
        public bool Started { get; private set; }
        public string Diagnostic { get; private set; }
        public FinalResult Confirmed { get; private set; }
        public int ConfirmationCount { get; private set; }
        public int Mode { get; private set; } = -1;
        public bool AllRetired => retired[0] && retired[1];
        public RoundAgreement(string match, long session) { this.match = match; this.session = session; }
        public AgreementStatus Ready(int slot, int mode, int protocol, string rule, string digest)
        {
            if (slot < 1 || slot > 2 || protocol != 1 || mode < 0 || mode > 1 ||
                mode == 1 && (!FinalResult.Digest(digest) || string.IsNullOrEmpty(rule))) return AgreementStatus.Rejected;
            string value = mode + "|" + protocol + "|" + rule + "|" + digest;
            if (ready[slot - 1] != null && ready[slot - 1] != value) { Diagnostic = "ready changed within match"; return AgreementStatus.Conflict; }
            if (Diagnostic != null) return AgreementStatus.Conflict;
            ready[slot - 1] = value;
            if (ready[0] == null || ready[1] == null) return AgreementStatus.Waiting;
            if (ready[0] != ready[1]) { Diagnostic = "mode/rules/config mismatch"; return AgreementStatus.Conflict; }
            Mode = mode; rules = rule; config = digest; Started = true;
            return AgreementStatus.Confirmed;
        }
        public AgreementStatus Report(int slot, FinalResult result, long lastEmittedFrame)
        {
            if (slot < 1 || slot > 2 || !Started || Mode != 1 || result == null || result.Match != match || result.Session != session ||
                result.Round != 1 || result.Frame > lastEmittedFrame || result.Rules != rules || result.Config != config)
                return AgreementStatus.Rejected;
            // A conflicting late request cannot create a second confirmation or replace the immutable first result.
            if (Confirmed != null) return Confirmed.Equals(result) ? AgreementStatus.Confirmed : AgreementStatus.Conflict;
            if (Diagnostic != null) return AgreementStatus.Conflict;
            if (reports[slot - 1] != null && !reports[slot - 1].Equals(result))
            { Diagnostic = "member changed its terminal report"; return AgreementStatus.Conflict; }
            reports[slot - 1] = result;
            if (reports[0] == null || reports[1] == null) return AgreementStatus.Waiting;
            if (!reports[0].Equals(reports[1])) { Diagnostic = "terminal frame/outcome/state mismatch"; return AgreementStatus.Conflict; }
            Confirmed = reports[0]; ConfirmationCount++;
            return AgreementStatus.Confirmed;
        }
        public AgreementStatus Retire(int slot, string confirmation)
        {
            if (slot < 1 || slot > 2 || Confirmed == null || confirmation != Confirmed.ConfirmationId) return AgreementStatus.Rejected;
            retired[slot - 1] = true;
            return AgreementStatus.Retired;
        }
    }

    // Early confirmations are staged, never used to skip frames or write HP.
    public sealed class ClientAgreement
    {
        public FinalResult Local { get; private set; }
        public FinalResult Confirmed { get; private set; }
        FinalResult pending;
        public string Diagnostic { get; private set; }
        public int ApplyCount { get; private set; }
        public void LockLocal(FinalResult result)
        {
            if (Local != null && !Local.Equals(result)) throw new InvalidOperationException("Local terminal result is immutable.");
            Local = result ?? throw new ArgumentNullException(nameof(result)); Reconcile();
        }
        public void Receive(FinalResult result)
        {
            if (pending != null && !pending.Equals(result)) { Diagnostic = "conflicting confirmations"; return; }
            pending = result; Reconcile();
        }
        void Reconcile()
        {
            if (Diagnostic != null || Local == null || pending == null || Confirmed != null) return;
            if (!Local.Equals(pending)) { Diagnostic = "server confirmation differs from local terminal snapshot"; return; }
            Confirmed = Local; ApplyCount++;
        }
    }
}
