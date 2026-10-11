using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkillConfig.Editor
{
    public enum PasteGroups { Copy, ReuseEquivalent }
    // Transient clipboard/validation drafts only. Authoritative data remains ComboData.skill.
    public static class SkillTimelineBatch
    {
        public sealed class Clipboard
        {
            internal ComboData source;
            internal SkillAuthoringData data;
            internal FrameSync.RootMotion.RootMotionSettings motion;
            internal HashSet<string> ids;
            internal int first;
        }
        public static Clipboard Buffer { get; private set; }
        public static T Clone<T>(T value) => ReferenceEquals(value, null) ? default : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static string ListName(SkillTimelineItem item) => item.path.Split('.')[1];
        static IList List(SkillAuthoringData data, string name) => (IList)typeof(SkillAuthoringData).GetField(name).GetValue(data);
        static string Id(object value) => (string)value.GetType().GetField("id").GetValue(value);
        static void Set(object value, string name, object next) => value.GetType().GetField(name).SetValue(value, next);
        static void Shift(object value, int delta)
        {
            var type = value.GetType(); var point = type.GetField("frame");
            if (point != null) { point.SetValue(value, checked((int)point.GetValue(value) + delta)); return; }
            foreach (var field in new[] { "startFrame", "endFrame" }) { var f = type.GetField(field); f.SetValue(value, checked((int)f.GetValue(value) + delta)); }
        }
        public static void Copy(ComboData owner, IEnumerable<string> ids)
        {
            var selected = new HashSet<string>(ids);
            var items = SkillTimelineAdapters.Items(owner).Where(i => selected.Contains(i.id)).ToArray();
            if (items.Length == 0) throw new ArgumentException("先选择 Timeline 条目。");
            if (items.Length != selected.Count) throw new ArgumentException("选择包含已失效条目，请重新选择。");
            Buffer = new Clipboard { source = owner, data = Clone(owner.skill), motion = Clone(owner.rootMotion), ids = new HashSet<string>(items.Select(i => i.id)), first = items.Min(i => i.start) };
        }
        public static bool IsCrossSkill(ComboData target) => Buffer != null && Buffer.source != target;
        public static string ReorderTrack(ComboData owner, CharacterSkillCatalog catalog, string id, int offset)
        {
            return Commit(owner, catalog, "Reorder displayed tracks", draft => {
                var ordered = draft.skill.tracks.OrderBy(t => t.displayOrder).GroupBy(t => t.group ?? "").SelectMany(g => g).ToList();
                int at = ordered.FindIndex(t => t.id == id), target = at + offset;
                if (at < 0 || target < 0 || target >= ordered.Count) throw new ArgumentException("已到轨道边界。");
                if ((ordered[at].group ?? "") != (ordered[target].group ?? "")) throw new ArgumentException("已到组内边界；跨组请修改轨道 Group 属性。");
                var value = ordered[at]; ordered.RemoveAt(at); ordered.Insert(target, value);
                for (int i = 0; i < ordered.Count; i++) ordered[i].displayOrder = i;
            });
        }
        public static string Move(ComboData owner, CharacterSkillCatalog catalog, IEnumerable<string> ids, int delta)
        {
            var set = new HashSet<string>(ids);
            return Commit(owner, catalog, "Move selected Timeline items", draft => {
                RequireSelection(draft, set);
                foreach (var item in SkillTimelineAdapters.Items(draft).Where(i => set.Contains(i.id)).ToArray())
                {
                    var value = List(draft.skill, ListName(item)).Cast<object>().First(x => Id(x) == item.id);
                    Shift(value, delta);
                }
            });
        }
        public static string Delete(ComboData owner, CharacterSkillCatalog catalog, IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids);
            return Commit(owner, catalog, "Delete selected Timeline items", draft => {
                RequireSelection(draft, set);
                foreach (var field in typeof(SkillAuthoringData).GetFields().Where(f => typeof(IList).IsAssignableFrom(f.FieldType)))
                {
                    if (field.Name == "tracks" || field.Name == "hitGroups") continue;
                    var list = (IList)field.GetValue(draft.skill);
                    for (int i = list.Count - 1; i >= 0; i--) if (set.Contains(Id(list[i]))) list.RemoveAt(i);
                }
                SkillEditCommands.Reflow(draft);
            });
        }
        static string GroupSignature(HitGroup group) { var copy = Clone(group); copy.id = ""; return JsonUtility.ToJson(copy); }
        static void RequireSelection(ComboData owner, HashSet<string> ids)
        {
            if (ids.Count == 0 || SkillTimelineAdapters.Items(owner).Count(i => ids.Contains(i.id)) != ids.Count)
                throw new ArgumentException("选择为空或包含已失效条目，请重新选择。");
        }
        public static string Paste(ComboData target, CharacterSkillCatalog catalog, int frame, PasteGroups policy, out string[] added)
        {
            var result = new List<string>(); added = Array.Empty<string>(); var buffer = Buffer;
            if (buffer == null) return "剪贴板为空。";
            string error = Commit(target, catalog, "Paste Timeline items", draft => {
                int delta = checked(frame - buffer.first);
                var data = Clone(buffer.data); var groups = new Dictionary<string, string>(); var tracks = new Dictionary<string, string>();
                foreach (var box in data.hitBoxes.Where(b => buffer.ids.Contains(b.id)))
                {
                    if (groups.ContainsKey(box.hitGroupId)) continue;
                    var group = data.hitGroups.FirstOrDefault(g => g.id == box.hitGroupId);
                    if (group == null) throw new ArgumentException("剪贴板 HitBox 引用了不存在的 HitGroup。");
                    var same = target == buffer.source ? draft.skill.hitGroups.FirstOrDefault(g => g.id == group.id) : null;
                    if (same == null && policy == PasteGroups.ReuseEquivalent)
                        same = draft.skill.hitGroups.FirstOrDefault(g => GroupSignature(g) == GroupSignature(group));
                    if (same == null)
                    {
                        if (policy == PasteGroups.ReuseEquivalent) throw new ArgumentException("目标没有等价 HitGroup；请选择复制组，或先在 Inspector 配置等价组。");
                        same = Clone(group); same.id = SkillEditCommands.NewId(); draft.skill.hitGroups.Add(same);
                    }
                    groups[group.id] = same.id;
                }
                foreach (var field in typeof(SkillAuthoringData).GetFields().Where(f => typeof(IList).IsAssignableFrom(f.FieldType)))
                {
                    if (field.Name == "tracks" || field.Name == "hitGroups") continue;
                    var values = ((IList)field.GetValue(data)).Cast<object>().Where(x => buffer.ids.Contains(Id(x))).ToArray();
                    if (values.Length == 0) continue;
                    int animationIndex = -1;
                    if (field.Name == "animations")
                    {
                        animationIndex = draft.skill.animations.FindIndex(a => a.startFrame == frame);
                        if (animationIndex < 0 && frame == SkillTimelineAdapters.Duration(draft)) animationIndex = draft.skill.animations.Count;
                        if (animationIndex < 0) throw new ArgumentException("动画只能粘贴到已有片段边界；不会覆盖现有片段。");
                        int span = values.Cast<AnimationSegment>().Sum(a => a.endFrame - a.startFrame);
                        foreach (var a in draft.skill.animations.Skip(animationIndex)) Shift(a, span);
                    }
                    foreach (var value in values)
                    {
                        string id = SkillEditCommands.NewId(); Set(value, "id", id); Shift(value, delta); result.Add(id);
                        var trackField = value.GetType().GetField("trackId");
                        if (trackField != null)
                        {
                            string oldTrack = (string)trackField.GetValue(value);
                            if (!tracks.TryGetValue(oldTrack, out string trackId))
                            {
                                var original = data.tracks.FirstOrDefault(t => t.id == oldTrack);
                                if (original == null) throw new ArgumentException("剪贴板包含无效轨道引用。");
                                var existing = target == buffer.source ? draft.skill.tracks.FirstOrDefault(t => t.id == oldTrack && t.kind == original.kind) : null;
                                if (existing == null) { existing = Clone(original); existing.id = SkillEditCommands.NewId(); existing.displayOrder = draft.skill.tracks.Select(t => t.displayOrder).DefaultIfEmpty(-1).Max() + 1; draft.skill.tracks.Add(existing); }
                                trackId = existing.id; tracks[oldTrack] = trackId;
                            }
                            trackField.SetValue(value, trackId);
                        }
                        if (value is HitBox hit) hit.hitGroupId = groups[hit.hitGroupId];
                        if (value is AnimationSegment animation)
                        {
                            if (target != buffer.source && animation.rootSource == RootBindingSource.LegacyCombo)
                            { animation.rootSource = RootBindingSource.Explicit; animation.additionalRootMotion = buffer.motion.json; animation.additionalEndFrameExclusive = buffer.motion.endFrameExclusive; animation.additionalDistancePermille = buffer.motion.distancePermille; }
                            draft.skill.animations.Insert(animationIndex++, animation);
                        }
                        else List(draft.skill, field.Name).Add(value);
                    }
                }
            });
            if (error == null) added = result.ToArray(); return error;
        }
        // No asset/Undo/dirty changes until the full target catalog validates successfully.
        static string Commit(ComboData owner, CharacterSkillCatalog catalog, string label, Action<ComboData> mutation)
        {
            if (!owner || !catalog || !catalog.skills.Contains(owner)) return "需要选中包含该技能的角色目录。";
            var draft = Object.Instantiate(owner);
            try
            {
                mutation(draft);
                SkillAssetValidator.BuildDraft(catalog, owner, draft, out var diagnostics);
                var errors = diagnostics.Where(d => d.severity == DiagnosticSeverity.Error).ToArray();
                if (errors.Length > 0) return "操作未提交（原数据未改变）：\n" + string.Join("\n", errors.Take(12).Select(d => d.ToString()));
                var next = Clone(draft.skill);
                SkillEditCommands.Apply(owner, label, () => owner.skill = next);
                return null;
            }
            catch (Exception e) { return "操作未提交：" + e.Message; }
            finally { Object.DestroyImmediate(draft); }
        }
    }
}
