using System;
using System.IO;
using System.Linq;
using System.Text;
using SkillConfig;
using SkillConfig.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Phase4BResourceAudit
{
    // Read-only inventory of the real Player, Animator and clip events. No migration.
    public static void Run()
    {
        try
        {
            var b = new StringBuilder();
            var player = Resources.Load<GameObject>("BattleScene/Role/Character").GetComponentInChildren<ZZZ.Player>(true);
            var data = player.playerSO.ComboData.comboData;
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterSkillCatalog>(SkillPilotSetup.CatalogPath);
            var controller = catalog.animatorController as AnimatorController;
            var states = SkillAssetValidator.States(controller);
            var all = data.lightCombo.comboDatas.Concat(new[] { data.lightCombo.DodgeATKData, data.lightCombo.BackDodgeATKData, data.skillCombo, data.finishSkillCombo }).Where(c => c).Distinct();
            b.AppendLine("Real Player: " + AssetDatabase.GetAssetPath(player));
            b.AppendLine("Container: " + AssetDatabase.GetAssetPath(data.lightCombo));
            b.AppendLine("Normal order: " + string.Join(" -> ", data.lightCombo.comboDatas.Select(c => c.name)));
            b.AppendLine("DodgeATK=" + data.lightCombo.DodgeATKData + " BackDodgeATK=" + data.lightCombo.BackDodgeATKData);
            foreach (var c in all)
            {
                b.AppendLine("\nASSET " + AssetDatabase.GetAssetPath(c) + " GUID=" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c)));
                b.AppendLine("state=" + c.comboName + " SkillID=" + c.skill?.skillId + " registered=" + c.skill?.registered + " damage=" + c.comboDamage + " distance=" + c.attackDistance + " offset=" + c.comboOffset);
                b.AppendLine("root=" + AssetDatabase.GetAssetPath(c.rootMotion.json));
                // Include the actual entry plus named multi-state FinishSkill sequence for inspection only.
                foreach (var pair in states.Where(p => p.Key.EndsWith("." + c.comboName, StringComparison.Ordinal) || (c == data.finishSkillCombo && p.Key.IndexOf("FinishSkill", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    b.AppendLine("STATE " + pair.Key + " motion=" + pair.Value);
                    if (!(pair.Value is AnimationClip clip)) continue;
                    b.AppendLine("CLIP " + clip.name + " length=" + clip.length.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " path=" + AssetDatabase.GetAssetPath(clip));
                    foreach (var e in AnimationUtility.GetAnimationEvents(clip).OrderBy(e => e.time))
                    {
                        long us = (long)Math.Round(e.time * 1000000.0, MidpointRounding.AwayFromZero);
                        b.AppendLine("EVENT us=" + us + " k=" + ((us + 32999) / 33000) + " fn=" + e.functionName + " int=" + e.intParameter + " float=" + e.floatParameter.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " string=" + e.stringParameter + " object=" + AssetDatabase.GetAssetPath(e.objectReferenceParameter));
                    }
                }
            }
            Directory.CreateDirectory("Logs/Phase4B"); File.WriteAllText("Logs/Phase4B/ResourceAudit.txt", b.ToString());
            Debug.Log("PHASE4B_RESOURCE_AUDIT_COMPLETE readonly=true path=Logs/Phase4B/ResourceAudit.txt");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
