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
            battle.mapMinX = battle.mapMinY = 0;
            battle.mapMaxX = battle.mapMaxY = 1000000;
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

            var combo = playerInfo.ComboData.comboData.lightCombo;
            check(combo.comboDatas.Count == 6, "Miyabi has six normal attack steps");
            for (int step = 1; step <= 6; step++)
            {
                var attack = combo.comboDatas[step - 1];
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"Assets/Resources/RootMotion/Avatar_Female_Size02_Unagi_Ani_Attack_{step:D2}_RootMotion.json");
                check(attack.comboName == $"Unagi_Normal_{step}" && json != null
                    && attack.rootMotion != null && attack.rootMotion.json == json,
                    $"Attack {step} binds its matching JSON");
                check(attack.rootMotion.TryGetClip(out _), $"Attack {step} data validates");
                var data = JsonUtility.FromJson<RootMotionJsonData>(json.text);
                role.CancelRootMotion();
                role.objShape.SetPosition(start);
                role.Logic_UpdateMoveDir(30);
                check(role.TryPlayRootMotion(attack.rootMotion) != 0, $"Attack {step} starts playback");
                for (int frame = 0; frame < data.frameCount; frame++) role.Logic_Move();
                check(!role.IsPlayingRootMotion
                    && role.objShape.GetPosition().Equals(start + new GameVector2(data.totalX, data.totalZ)),
                    $"Attack {step} applies exported displacement without input movement");
            }

            Debug.Log("PASS: " + checks + " root motion Unity integration checks");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            instanceField.SetValue(null, originalBattle);
        }
    }
}
