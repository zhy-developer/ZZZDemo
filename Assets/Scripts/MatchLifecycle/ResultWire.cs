using Wire = GameProtocol;
namespace MatchLifecycle
{
    public static class ResultWire
    {
        public static FinalResult Read(Wire.FinalResult value) => value == null ? null : new FinalResult(value.matchId, value.sessionKey,
            value.roundId, value.terminalFrame, (Outcome)value.outcome, value.winnerSlot, value.stateHash, value.rules, value.configHash, value.protocolVersion);
        public static Wire.FinalResult Write(FinalResult value) => value == null ? null : new Wire.FinalResult {
            matchId = value.Match, sessionKey = value.Session, roundId = value.Round, terminalFrame = checked((int)value.Frame),
            outcome = (Wire.FinalOutcome)value.Result, winnerSlot = value.Winner, stateHash = value.Hash,
            rules = value.Rules, configHash = value.Config, protocolVersion = value.Protocol };
    }
}
