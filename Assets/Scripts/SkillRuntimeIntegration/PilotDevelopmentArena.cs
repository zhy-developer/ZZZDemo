using System;
using System.Collections;
using System.Linq;
using GameProtocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkillRuntime.Integration
{
    // Explicit development loopback SERVER fixture. Production battles use BattleManager's UDP frame stream.
    public sealed class PilotDevelopmentArena : MonoBehaviour
    {
        public PilotBattleProfile profile;
        public BattleAuthorityMode mode = BattleAuthorityMode.DeterministicPilot;
        public bool produceLoopbackFrames = true;
        public bool automatedChecks;
        public RoleManager Roles { get; private set; }
        public RoleBase[] Players { get; private set; }
        public PilotConfiguration Configuration { get; private set; }
        public bool Ready { get; private set; }
        public string StartupError { get; private set; }
        Transform instanceRoot;
        double serverAccumulator;
        int secondOp;
        bool secondAttackPending;
        IEnumerator Start()
        {
            // StreamingAssetService initializes the existing integer direction table asynchronously.
            var data = BattleData.Instance;
            yield return null;
            try { Configuration = profile.ValidateAndFreeze(); Restart(mode); }
            catch (Exception ex) { StartupError = ex.ToString(); Debug.LogError("[PilotStartupRejected] " + StartupError); }
#if UNITY_EDITOR
            if (automatedChecks)
            {
                if (!Ready) { UnityEditor.EditorApplication.Exit(1); yield break; }
                yield return PilotSceneVerification.Run(this);
            }
#endif
        }
        public void Restart(BattleAuthorityMode newMode)
        {
            if (!Debug.isDebugBuild) throw new InvalidOperationException("Development arena only.");
            Ready = false;
            if (Roles) { Roles.Pilot?.Dispose(); Roles.gameObject.SetActive(false); Destroy(Roles.gameObject); }
            if (instanceRoot) { instanceRoot.gameObject.SetActive(false); Destroy(instanceRoot.gameObject); }
            var data = BattleData.Instance; data.ClearData();
            data.UpdateBattleInfo(41001, new System.Collections.Generic.List<BattleUserInfo> {
                new BattleUserInfo { uid = NetGlobal.Instance.userUid, battleID = 1, roleID = 1 },
                new BattleUserInfo { uid = NetGlobal.Instance.userUid + 1, battleID = 2, roleID = 1 } });
            data.InitMapBounds(new Bounds(Vector3.zero, new Vector3(40, 1, 40)));
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Pilot Camera", typeof(Camera), typeof(AudioListener)); cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0, 5, -8); cameraObject.transform.LookAt(Vector3.up);
            }
            instanceRoot = new GameObject("Pilot Match Instances").transform;
            Players = new RoleBase[2];
            var rolePrefab = Resources.Load<GameObject>("BattleScene/Role/RoleBase");
            var modelPrefab = Resources.Load<GameObject>("BattleScene/Role/Character");
            if (!rolePrefab || !modelPrefab) throw new InvalidOperationException("Actual role/model prefabs missing.");
            for (int i = 0; i < 2; i++)
            {
                var root = Instantiate(rolePrefab, instanceRoot); root.name = "Pilot Real Player " + (i + 1);
                var model = Instantiate(modelPrefab); var player = model.GetComponentInChildren<ZZZ.Player>(true);
                player.InitializeNetworkRole(i + 1, i == 0);
                foreach (var cameras in model.GetComponentsInChildren<CharacterSkillCameraGroup>(true)) cameras.Initialize(false);
                Players[i] = root.GetComponent<RoleBase>();
                Players[i].InitData(null, model, i + 1, new GameVector2(i == 0 ? -15000 : 15000, 0));
            }
            Roles = new GameObject("Pilot Role Manager").AddComponent<RoleManager>();
            Roles.InitializeDevelopmentRoles(Players, newMode == BattleAuthorityMode.DeterministicPilot ? Configuration : null);
            mode = newMode; secondOp = 0; secondAttackPending = false; serverAccumulator = 0; Ready = true;
        }
        public bool Deliver(int frame, AllPlayerOperation operations)
        {
            BattleData.Instance.AddNewFrameData(frame, operations);
            return ConsumeNext();
        }
        public bool ConsumeNext()
        {
            var data = BattleData.Instance;
            if (!data.TryGetNextPlayerOp(out var op) || !Roles.ConsumeFrame(data.NextFrameID, op)) return false;
            data.RunOpSucces(); return true;
        }
        public static PlayerOperation Copy(PlayerOperation p) => new PlayerOperation { battleID = p.battleID, move = p.move,
            rightOperation = p.rightOperation, operationID = p.operationID, operationValue1 = p.operationValue1, operationValue2 = p.operationValue2 };
        void Update()
        {
            if (!Ready || !produceLoopbackFrames || automatedChecks) return;
            serverAccumulator += Time.unscaledDeltaTime;
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) secondAttackPending = true;
            while (serverAccumulator >= NetConfig.frameTime / 1000.0)
            {
                serverAccumulator -= NetConfig.frameTime / 1000.0;
                bool secondAttack = secondAttackPending;
                if (secondAttack) secondOp++;
                var op = new AllPlayerOperation(); op.operations.Add(Copy(BattleData.Instance.selfOperation));
                op.operations.Add(new PlayerOperation { battleID = 2, move = 121, rightOperation = secondAttack ? RightOpType.rop1 : RightOpType.noop,
                    operationID = secondAttack ? secondOp : 0, operationValue1 = 60 });
                secondAttackPending = false;
                if (!Deliver(BattleData.Instance.NextFrameID, op)) break;
            }
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 680, 220), GUI.skin.box);
            GUILayout.Label("Phase 4A DEVELOPMENT / loopback server / " + mode + " (not a two-client test)");
            if (!Ready) GUILayout.Label(StartupError ?? "Loading...");
            else
            {
                GUILayout.Label("Player 1: existing movement + mouse attack. Player 2: Enter attacks. Test HP/HurtBox only.");
                if (Roles.Pilot != null)
                {
                    for (int i = 1; i <= 2; i++) { var h = Roles.Pilot.Health(i); GUILayout.Label("P" + i + " HP " + h.DisplayHP + "/" + h.DisplayMaxHP + (h.IsDead ? " KO" : "")); }
                    GUILayout.Label("F=" + Roles.Pilot.Runtime.LastCompletedFrame + " Result=" + Roles.Pilot.TerminalResult + " Feedback=" + Roles.Pilot.FeedbackCount);
                    GUILayout.Label(Roles.Pilot.Faulted ? Roles.Pilot.Fault : Roles.Pilot.LastStateHash);
                }
                if (GUILayout.Button("Exit match and start fresh Legacy")) Restart(BattleAuthorityMode.Legacy);
                if (GUILayout.Button("Exit match and start fresh DeterministicPilot")) Restart(BattleAuthorityMode.DeterministicPilot);
            }
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            if (Roles) { Roles.Pilot?.Dispose(); Destroy(Roles.gameObject); }
            if (instanceRoot) Destroy(instanceRoot.gameObject);
        }
#if UNITY_EDITOR
        internal void UseTestConfiguration(PilotConfiguration testConfig) { Configuration = testConfig; Restart(BattleAuthorityMode.DeterministicPilot); }
#endif
    }
}
