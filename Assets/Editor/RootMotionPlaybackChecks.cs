using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using FrameSync.RootMotion;
using UnityEditor;
using UnityEngine;

/// <summary>Editor integration checks without entering a network battle. Never modifies scene assets.</summary>
public static class RootMotionPlaybackChecks
{
    [MenuItem("Tools/Root Motion/Verify Playback Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play mode.");
        int checks = 0;
        Action<bool, string> check = (ok, name) => {
            if (!ok) throw new InvalidOperationException(name);
            checks++;
        };
        var instanceField = typeof(BattleData).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object originalBattle = instanceField.GetValue(null);
        GameObject root = null;
        try
        {
            // Isolate only the map/direction services RoleBase uses; do not start networking or loading coroutines.
            var battle = (BattleData)FormatterServices.GetUninitializedObject(typeof(BattleData));
            battle.mapWidth = battle.mapHeigh = 1000000;
            var directions = new Dictionary<int, GameVector2>();
            foreach (string line in File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "Desktopspeed.txt")))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] values = line.Split(',');
                directions.Add(int.Parse(values[0]), new GameVector2(int.Parse(values[1]), int.Parse(values[2])));
            }
            typeof(BattleData).GetField("dic_speed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, directions);
            instanceField.SetValue(null, battle);

            var playerInfo = AssetDatabase.LoadAssetAtPath<ZZZ.PlayerSO>(
                "Assets/ScriptableObject/PlayerInfo/PlayerInfo（星见雅）.asset");
            var settings = playerInfo.movementData.dashData.frontRootMotion;
            check(settings != null && settings.TryGetClip(out _), "Miyabi front dodge binding loads");
            check(settings.endFrameExclusive == 0 && settings.distancePermille == 1000, "Use baked window and distance");
            check(playerInfo.movementData.dashData.backRootMotion.json == null, "No fabricated back dodge data");

            root = new GameObject("RootMotionPlaybackCheck") { hideFlags = HideFlags.HideAndDontSave };
            var modelParent = new GameObject("Modle");
            modelParent.transform.SetParent(root.transform);
            var model = new GameObject("TestModel");
            model.transform.SetParent(modelParent.transform);
            var role = root.AddComponent<RoleBase>();
            var start = new GameVector2(100000, 100000);
            role.InitData(null, model, 1, start);
            role.moveSpeed = 30;
            role.Logic_UpdateMoveDir(30); // +Z
            int completed = 0;
            int first = role.TryPlayRootMotion(settings, () => completed++);
            check(first != 0, "Start configured motion");
            role.Logic_Move();
            check(role.objShape.GetPosition().Equals(new GameVector2(99997, 99204)), "First sample has no ordinary move added");
            role.Logic_UpdateMoveDir(0); // Cache input but keep the original +Z motion direction.
            for (int i = 1; i < 23; i++) role.Logic_Move();
            check(role.objShape.GetPosition().Equals(new GameVector2(98622, 159191)), "All 23 samples keep captured facing");
            check(completed == 1 && !role.IsPlayingRootMotion, "Complete once on last sample");
            role.Logic_Move();
            check(role.objShape.GetPosition().Equals(new GameVector2(101622, 159191)), "Resume latest input next tick");

            role.Logic_UpdateMoveDir(30);
            int oldHandle = role.TryPlayRootMotion(settings, () => completed += 100);
            role.Logic_Move();
            int newHandle = role.TryPlayRootMotion(settings, () => completed += 10);
            role.StopRootMotion(oldHandle);
            check(role.IsPlayingRootMotion && newHandle != oldHandle, "Stale handle cannot stop replacement");
            role.Logic_UpdateMoveDir(121); // Released input should resume to stationary.
            role.StopRootMotion(newHandle);
            var stopped = role.objShape.GetPosition();
            role.Logic_Move();
            check(stopped.Equals(role.objShape.GetPosition()) && completed == 1, "Cancellation discards tail without callback");
            check(role.TryPlayRootMotion(new RootMotionSettings()) == 0, "Missing data preserves legacy path");

            role.Logic_UpdateMoveDir(120); // 360 degrees is equivalent to zero.
            role.TryPlayRootMotion(settings);
            stopped = role.objShape.GetPosition();
            role.Logic_Move();
            check(role.objShape.GetPosition().Equals(stopped + new GameVector2(-796, 3)), "Direction 120 wraps correctly");
            // Ordinary MonoBehaviours do not run their normal lifecycle in Edit mode.
            typeof(RoleBase).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(role, null);
            check(!role.IsPlayingRootMotion, "Disable handler cancels motion");
            role.Logic_UpdateMoveDir(30);
            role.objShape.SetPosition(new GameVector2(100000, 999990));
            role.TryPlayRootMotion(settings);
            for (int i = 0; i < 23; i++) role.Logic_Move();
            check(role.objShape.GetPosition().y <= battle.mapHeigh, "Baked displacement uses map resolver");

            Debug.Log("PASS: " + checks + " root motion Unity integration checks");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            instanceField.SetValue(null, originalBattle);
        }
    }
}
