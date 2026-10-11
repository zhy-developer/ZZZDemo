using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    // Editor-only transaction; never used to execute combat or export skill JSON.
    public sealed class SkillComboGraphDraft
    {
        public ComboContainerData Target { get; private set; }
        public List<ComboData> Steps { get; private set; }
        public ComboData Dodge;
        ComboData[] baseline;
        ComboData baselineDodge, baselineBack;
        public bool Dirty => Target && (!Steps.SequenceEqual(baseline) || Dodge != baselineDodge);
        public bool Conflict => !Target || Target.comboDatas == null || !Target.comboDatas.SequenceEqual(baseline)
            || Target.DodgeATKData != baselineDodge || Target.BackDodgeATKData != baselineBack;
        public void Load(ComboContainerData target)
        {
            Target = target; baseline = target && target.comboDatas != null ? target.comboDatas.ToArray() : Array.Empty<ComboData>();
            Steps = baseline.ToList(); Dodge = baselineDodge = target ? target.DodgeATKData : null;
            baselineBack = target ? target.BackDodgeATKData : null;
        }
        public IEnumerable<ComboData> Nodes => Steps.Concat(new[] { Dodge, baselineBack }).Where(x => x).Distinct();
        public static string Label(ComboData value)
        {
            if (!value) return "<Missing>";
            bool registered = value.skill != null && value.skill.registered && !string.IsNullOrWhiteSpace(value.skill.skillId);
            return value.name + " [" + (registered ? value.skill.skillId : "Legacy") + "]";
        }
        public void Move(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Steps.Count || to >= Steps.Count) return;
            var item = Steps[from]; Steps.RemoveAt(from); Steps.Insert(to, item);
        }
        public string ConnectAfter(ComboData source, ComboData target)
        {
            int a = Steps.IndexOf(source), b = Steps.IndexOf(target);
            if (a < 0 || b < 0 || a == b) return "仅支持已有普通段之间的顺序重排；不支持任意条件分支。";
            Steps.RemoveAt(b); Steps.Insert(Steps.IndexOf(source) + 1, target); return null;
        }
        public string Validate()
        {
            if (!Target || !AssetDatabase.Contains(Target)) return "请选择持久化 ComboContainerData。";
            if (Conflict) return "原容器已被外部修改或 Undo/Redo；请取消草稿并重新加载，禁止覆盖新数据。";
            if (Steps.Count == 0 || Steps.Any(x => !x)) return "普通连招至少保留一段，不能包含 Missing 引用。";
            if (Steps.Distinct().Count() != Steps.Count) return "普通段包含重复 ComboData；本版禁止有歧义的重复节点编辑。";
            if (Steps.Any(x => !AssetDatabase.Contains(x) || string.IsNullOrWhiteSpace(x.comboName))) return "普通段必须引用已保存且动画入口非空的 ComboData。";
            if (Dodge && (!AssetDatabase.Contains(Dodge) || string.IsNullOrWhiteSpace(Dodge.comboName))) return "闪避入口必须是已保存且动画入口非空的 ComboData。";
            // Null Dodge would make SwitchDodgeATK log an error and reuse/reset an unexpected slot.
            if (!Dodge && Dodge != baselineDodge) return "不能移除已接入的 DodgeATKData；旧运行时对空引用会报错。可替换为有效技能。";
            if (Nodes.Any(x => x.skill != null && x.skill.registered && string.IsNullOrWhiteSpace(x.skill.skillId))) return "已登记节点缺少稳定 SkillID；先修复技能身份。";
            var ids = Nodes.Where(x => x.skill != null && x.skill.registered && !string.IsNullOrWhiteSpace(x.skill.skillId)).Select(x => x.skill.skillId);
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count()) return "不同节点使用重复 SkillID；先修复技能身份。";
            return null;
        }
        public string Describe()
        {
            return "资产: " + AssetDatabase.GetAssetPath(Target)
                + "\n之前: " + string.Join(" → ", baseline.Select(Label))
                + "\n之后: " + string.Join(" → ", Steps.Select(Label))
                + "\n闪避入口之前: " + Label(baselineDodge) + "\n闪避入口之后: " + Label(Dodge)
                + "\nBackDodgeATKData 保持不变（源码无调用，仅查看）。"
                + "\n顺序决定网络攻击段索引；首段变更影响起手，末段仍循环回第0段。"
                + "\n闪避会替换第0段，后续可接第1段；不是在普通连招上增加任意分支。"
                + "\n涉及 Legacy: " + (baseline.Concat(Nodes).Any(x => x && (x.skill == null || !x.skill.registered)) ? "是" : "否")
                + "\n不会迁移旧技能，不修改 Animator/事件/网络；关系不包含在技能 JSON。";
        }
        // Caller must present Describe() and obtain an explicit Apply click.
        public string ApplyConfirmed()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Play Mode 中禁止修改连招容器。";
            string error = Validate(); if (error != null || !Dirty) return error;
            var target = Target; var next = Steps.ToList(); var dodge = Dodge;
            SkillEditCommands.Apply(target, "Apply combo graph relations", () => { target.comboDatas = next; target.DodgeATKData = dodge; });
            SkillEditCommands.Flush(); Load(target); return null;
        }
    }
}
