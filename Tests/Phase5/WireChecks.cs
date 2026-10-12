using System.Net;
using System.Net.Sockets;
using ProtoBuf;
using GameProtocol;
using MatchLifecycle;
using Result = MatchLifecycle.FinalResult;

// Live server protocol adversarial checks, synthetic reports. NOT combat simulation or Unity clients.
static class WireChecks
{
    static int checks;
    static string config = new string('a', 64), hash = new string('b', 64);
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("WIRE_PASS " + name); }
    static byte[] Pack<T>(byte id, T value) { using var ms = new MemoryStream(); Serializer.Serialize(ms, value); var bytes = ms.ToArray(); return new[] { id, (byte)((bytes.Length + 3) & 255), (byte)((bytes.Length + 3) >> 8) }.Concat(bytes).ToArray(); }
    static T Read<T>(byte[] bytes) { using var ms = new MemoryStream(bytes, 3, bytes.Length - 3); return Serializer.Deserialize<T>(ms); }
    sealed class Peer : IDisposable
    {
        public TcpClient Tcp = new(); public UdpClient Udp = new(0); public int Uid, Slot; public TcpEnterBattle Match;
        public Peer(string ip) { Tcp.Connect(ip, 13001); Tcp.ReceiveTimeout = 5000; Udp.Client.ReceiveTimeout = 5000; SendTcp(CSID.TCP_LOGIN, new TcpLogin { token = Guid.NewGuid().ToString("N") }); var login = ReceiveTcp<TcpResponseLogin>(SCID.TCP_RESPONSE_LOGIN); Uid = login.uid; Udp.Connect(ip, login.udpPort); }
        public void SendTcp<T>(CSID id, T value) => Tcp.GetStream().Write(Pack((byte)id, value));
        public void Send<T>(CSID id, T value) { var bytes = Pack((byte)id, value); Udp.Send(bytes, bytes.Length); }
        public T ReceiveTcp<T>(SCID id) { while (true) { byte[] head = new byte[3]; Tcp.GetStream().ReadExactly(head); int size = head[1] | head[2] << 8; byte[] packet = new byte[size]; Array.Copy(head, packet, 3); Tcp.GetStream().ReadExactly(packet.AsSpan(3)); if (head[0] == (byte)id) return Read<T>(packet); } }
        public T Receive<T>(SCID id, Func<T,bool> predicate = null) { var end = DateTime.UtcNow.AddSeconds(5); while (DateTime.UtcNow < end) { IPEndPoint remote = null; var bytes = Udp.Receive(ref remote); if (bytes[0] != (byte)id) continue; var value = Read<T>(bytes); if (predicate == null || predicate(value)) return value; } throw new TimeoutException(id.ToString()); }
        public void Enter() { Match = ReceiveTcp<TcpEnterBattle>(SCID.TCP_ENTER_BATTLE); Slot = Match.battleUserInfo.Single(x => x.uid == Uid).battleID; }
        public void Ready(int mode = 1, string rules = "wire-test-only", string digest = null, int protocol = 1) => Send(CSID.UDP_BATTLE_READY, new UdpBattleReady { uid = Uid, battleID = Slot, matchId = Match.matchId, roundId = 1, protocolVersion = protocol, joinTicket = Match.joinTicket, authorityMode = mode, rules = mode == 1 ? rules : "legacy", configHash = mode == 1 ? digest ?? config : "legacy" });
        public void Input() => Send(CSID.UDP_UP_PLAYER_OPERATIONS, new UdpUpPlayerOperations { matchId = Match.matchId, roundId = 1, mesID = 1, operation = new PlayerOperation { battleID = Slot, move = 121, rightOperation = RightOpType.noop } });
        public Result Result(long frame = 1, int winner = 1, string state = null, string match = null) => new Result(match ?? Match.matchId, Match.sessionKey, 1, frame, Outcome.Winner, winner, state ?? hash, "wire-test-only", config);
        public void Report(Result result, int? slot = null) => Send(CSID.UDP_UP_GAME_OVER, new UdpUpGameOver { battleID = slot ?? Slot, result = ResultWire.Write(result) });
        public UdpDownGameOver Response(AgreementStatus status) => Receive<UdpDownGameOver>(SCID.UDP_DOWN_GAME_OVER, x => x.status == (int)status);
        public void Dispose() { Tcp.Dispose(); Udp.Dispose(); }
    }
    public static void Main(string[] args)
    {
        string ip = args.Length == 0 ? "192.168.1.5" : args[0];
        foreach (string scenario in new[] { "retry", "frame", "outcome", "hash", "mode", "rules", "config", "protocol", "legacy" })
        {
            using var a = new Peer(ip); using var b = new Peer(ip);
            a.SendTcp(CSID.TCP_REQUEST_MATCH, new TcpRequestMatch { uid = a.Uid, roleId = 1 });
            a.SendTcp(CSID.TCP_REQUEST_MATCH, new TcpRequestMatch { uid = a.Uid, roleId = 1 }); // queued duplicate
            b.SendTcp(CSID.TCP_REQUEST_MATCH, new TcpRequestMatch { uid = b.Uid, roleId = 1 }); a.Enter(); b.Enter();
            Check(a.Match.matchId == b.Match.matchId && a.Slot != b.Slot && a.Match.joinTicket != b.Match.joinTicket, scenario + " authenticated distinct members and tickets");
            a.Ready(scenario == "legacy" ? 0 : 1); b.Ready(scenario == "mode" || scenario == "legacy" ? 0 : 1, rules: scenario == "rules" ? "wrong-version" : "wire-test-only", digest: scenario == "config" ? new string('d',64) : null, protocol: scenario == "protocol" ? 99 : 1);
            if (scenario == "mode" || scenario == "rules" || scenario == "config" || scenario == "protocol") { Check(!string.IsNullOrEmpty(b.Receive<UdpBattleStart>(SCID.UDP_BATTLE_START).error), scenario + " rejected before frame start"); continue; }
            a.Receive<UdpBattleStart>(SCID.UDP_BATTLE_START); b.Receive<UdpBattleStart>(SCID.UDP_BATTLE_START); a.Input(); b.Input();
            a.Receive<UdpDownFrameOperations>(SCID.UDP_DOWN_FRAME_OPERATIONS, x => x.frameID >= 3);
            if (scenario == "legacy") {
                a.Send(CSID.UDP_UP_GAME_OVER, new UdpUpGameOver { battleID = a.Slot });
                Check(a.Receive<UdpDownGameOver>(SCID.UDP_DOWN_GAME_OVER).result == null, "current-version Legacy old GameOver acknowledgment retained"); continue;
            }
            if (scenario == "retry") {
                a.Report(a.Result(match: new string('f',32))); a.Report(a.Result(), b.Slot);
                // First A report deliberately never delivered. Wrong-scope/member packets cannot substitute for it.
                b.Report(b.Result()); Check(b.Response(AgreementStatus.Waiting).status == 0, "dropped first report / wrong scope / wrong member do not confirm");
                a.Report(a.Result()); var first = a.Response(AgreementStatus.Confirmed);
                b.Response(AgreementStatus.Confirmed); // deliberately consume/drop business confirmation
                b.Report(b.Result()); var retry = b.Response(AgreementStatus.Confirmed);
                Check(first.confirmationId == retry.confirmationId && ResultWire.Read(first.result).Equals(ResultWire.Read(retry.result)), "lost confirmation and report retries preserve exact business identity");
                a.Report(a.Result(frame:2)); var late = a.Response(AgreementStatus.Conflict);
                Check(late.confirmationId == first.confirmationId && late.result.terminalFrame == 1, "late conflicting frame cannot replace confirmed result");
                foreach (var p in new[] { a,b }) {
                    var retire = new UdpUpGameOver { battleID = p.Slot, result = first.result, retire = true, confirmationId = first.confirmationId };
                    p.Send(CSID.UDP_UP_GAME_OVER, retire); p.Response(AgreementStatus.Retired);
                    p.Send(CSID.UDP_UP_GAME_OVER, retire); Check(p.Response(AgreementStatus.Retired).confirmationId == first.confirmationId, "duplicate retire acknowledged idempotently");
                }
            } else {
                a.Report(a.Result()); a.Response(AgreementStatus.Waiting);
                b.Report(b.Result(frame: scenario == "frame" ? 2 : 1, winner: scenario == "outcome" ? 2 : 1, state: scenario == "hash" ? new string('c',64) : null));
                Check(a.Response(AgreementStatus.Conflict).confirmationId == "" && b.Response(AgreementStatus.Conflict).confirmationId == "", scenario + " conflict never confirms");
            }
        }
        Console.WriteLine("PHASE5_WIRE_COMPLETE checks=" + checks + " live server TCP/UDP; synthetic reports; NOT combat clients");
    }
}

