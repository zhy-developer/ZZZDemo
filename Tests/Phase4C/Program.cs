using MatchLifecycle;
using ProtoBuf;
using System.IO;
int passed = 0;
void Check(bool test, string name) { if (!test) throw new Exception("FAIL " + name); passed++; Console.WriteLine("PASS " + name); }
string match = "11111111111111111111111111111111", config = new string('a',64), hash = new string('b',64);
FinalResult Result(Outcome outcome = Outcome.Winner, int winner = 1, long frame = 42, string state = null, string id = null, string rules = "rules") => new FinalResult(id ?? match, 88, 1, frame, outcome, winner, state ?? hash, rules, config);
RoundAgreement Ready(bool reverse = false)
{
    var s = new RoundAgreement(match, 88);
    Check(s.Ready(reverse ? 2 : 1, 1, 1, "rules", config) == AgreementStatus.Waiting, "one ready cannot begin");
    Check(s.Ready(reverse ? 1 : 2, 1, 1, "rules", config) == AgreementStatus.Confirmed, "both compatible ready begin"); return s;
}
foreach (var outcome in new[] { Result(), Result(winner:2), Result(Outcome.Draw,0) })
foreach (bool reverse in new[] { false, true })
{
    var server = Ready(reverse); int first = reverse ? 2 : 1, second = 3 - first;
    Check(server.Report(first,outcome,50) == AgreementStatus.Waiting && server.Confirmed == null, "one report remains pending");
    Check(server.Report(first,outcome,50) == AgreementStatus.Waiting, "retry same report after dropped packet");
    Check(server.Report(second,outcome,50) == AgreementStatus.Confirmed, "both agree " + outcome.Result + "/" + outcome.Winner);
    var id = server.Confirmed.ConfirmationId;
    Check(server.Report(first,outcome,60) == AgreementStatus.Confirmed && server.ConfirmationCount == 1 && server.Confirmed.ConfirmationId == id, "lost confirmation retries return identical identity");
    Check(server.Report(first,Result(frame:43),60) == AgreementStatus.Conflict && server.ConfirmationCount == 1 && server.Confirmed.Equals(outcome), "late conflicting frame never confirms twice");
    Check(server.Retire(1,id) == AgreementStatus.Retired && server.Retire(1,id) == AgreementStatus.Retired && !server.AllRetired, "duplicate single retire is idempotent");
    Check(server.Retire(2,id) == AgreementStatus.Retired && server.AllRetired, "both retired");
}
foreach (var other in new[] { Result(frame:43),Result(winner:2),Result(state:new string('c',64)) })
{
    var server = Ready(); server.Report(1,Result(),50);
    Check(server.Report(2,other,50) == AgreementStatus.Conflict && server.Confirmed == null, "different frame/outcome/hash conflicts");
    Check(server.Report(2,Result(),50) == AgreementStatus.Conflict, "conflict cannot be overwritten by retry");
}
var rejected = Ready();
Check(rejected.Report(3,Result(),50) == AgreementStatus.Rejected, "non-member rejected");
Check(rejected.Report(1,Result(frame:60),50) == AgreementStatus.Rejected, "unemitted future terminal rejected");
Check(rejected.Report(1,Result(id:new string('2',32)),50) == AgreementStatus.Rejected, "old match rejected");
Check(rejected.Report(1,Result(rules:"old-rules"),50) == AgreementStatus.Rejected, "rule mismatch rejected");
var incompatible = new RoundAgreement(match,88); incompatible.Ready(1,1,1,"rules",config);
Check(incompatible.Ready(2,0,1,"legacy","legacy") == AgreementStatus.Conflict && !incompatible.Started, "mixed mode cannot start");
Check(incompatible.Ready(2,1,2,"rules",config) == AgreementStatus.Rejected, "protocol mismatch rejected");
var client = new ClientAgreement(); client.Receive(Result());
Check(client.Confirmed == null && client.ApplyCount == 0, "early confirmation waits for actual terminal frame");
client.LockLocal(Result()); client.Receive(Result()); client.LockLocal(Result());
Check(client.ApplyCount == 1 && client.Confirmed.Equals(Result()), "duplicate confirmation applies once");
var mismatch = new ClientAgreement(); mismatch.Receive(Result()); mismatch.LockLocal(Result(winner:2));
Check(mismatch.Confirmed == null && mismatch.Diagnostic != null, "local mismatch never writes peer HP/outcome");
var secondMatch = new RoundAgreement(new string('2',32),99); secondMatch.Ready(1,1,1,"rules",config); secondMatch.Ready(2,1,1,"rules",config);
Check(secondMatch.Report(1,Result(),50) == AgreementStatus.Rejected && secondMatch.ConfirmationCount == 0, "fresh match cannot inherit old result");
// Real generated protobuf binary cross-namespace compatibility, not hand-built JSON.
var wire = new GameProtocol.UdpUpGameOver { battleID=1, result=ResultWire.Write(Result(Outcome.Draw,0)) };
using (var stream = new MemoryStream())
{
    Serializer.Serialize(stream,wire); stream.Position=0;
    var serverWire=Serializer.Deserialize<PBBattle.UdpUpGameOver>(stream);
    Check(serverWire.battleID==1 && serverWire.result.outcome==PBBattle.FinalOutcome.Draw && serverWire.result.terminalFrame==42 && serverWire.result.sessionKey==88, "generated client -> server Draw bytes");
    stream.SetLength(0); stream.Position=0;
    Serializer.Serialize(stream,new PBBattle.UdpDownGameOver {result=serverWire.result,status=1,matchId=match,roundId=1,confirmationId=Result().ConfirmationId}); stream.Position=0;
    var reply=Serializer.Deserialize<GameProtocol.UdpDownGameOver>(stream);
    Check(ResultWire.Read(reply.result).Equals(Result(Outcome.Draw,0)) && reply.confirmationId==Result().ConfirmationId, "generated server -> client confirmation bytes");
}
Console.WriteLine("PHASE4C_MODULE_COMPLETE checks="+passed+" .NET script assertions; not Unity or real transport");
