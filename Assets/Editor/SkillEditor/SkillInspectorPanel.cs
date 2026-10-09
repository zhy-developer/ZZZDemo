using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillInspectorPanel
    {
        Vector2 scroll;
        static readonly string[] EventIds = { "presentation.marker", "combo.preinput", "combo.attack-ready", "combo.link", "combo.move-interrupt" };
        public void Draw(ComboData owner, CharacterSkillCatalog catalog, string selection, Action<string> select)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinWidth(285), GUILayout.MaxWidth(380));
            EditorGUILayout.LabelField("配置 Inspector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("整数帧 [Start, End)；位置精度 10000，角度为毫度。配置尚未接入战斗。", MessageType.None);
            if (selection == "@catalog") DrawCatalog(catalog);
            else if (owner && owner.skill != null && owner.skill.registered)
            {
                var so = new SerializedObject(owner); so.Update();
                var item = SkillTimelineAdapters.Find(owner, selection);
                SerializedProperty target = item != null ? so.FindProperty(item.path) : null;
                if (selection != null && selection.StartsWith("track:")) target = Find(so, "skill.tracks", selection.Substring(6));
                if (selection != null && selection.StartsWith("group:")) target = Find(so, "skill.hitGroups", selection.Substring(6));
                bool animation = item != null && item.animation;
                if (target != null)
                {
                    if (item != null) EditorGUILayout.LabelField(item.label ?? "Item", EditorStyles.boldLabel);
                    if (item != null && !item.animation)
                    {
                        TrackKind kind = item.path.Contains(".hitBoxes.") ? TrackKind.HitBox : item.path.Contains(".invincibility.") ? TrackKind.Invincibility : item.path.Contains(".interrupts.") ? TrackKind.Interrupt : item.path.Contains(".vfx.") ? TrackKind.Vfx : item.path.Contains(".sfx.") ? TrackKind.Sfx : TrackKind.Event;
                        ReferencePopup(target.FindPropertyRelative("trackId"), owner.skill.tracks.Where(t => t != null && t.kind == kind).Select(t => t.id).ToArray(), "所属轨道");
                    }
                    if (item != null && item.path.Contains(".events.")) EventType(target);
                    if (item != null && item.path.Contains(".hitBoxes.")) ReferencePopup(target.FindPropertyRelative("hitGroupId"), owner.skill.hitGroups.Select(g => g.id).ToArray(), "HitGroup");
                    DrawChildren(target, animation, catalog);
                    if (so.hasModifiedProperties) SkillEditCommands.ApplyProperties(so, animation);
                    if (animation)
                    {
                        EditorGUILayout.HelpBox("修改 Trim（微秒）或 SpeedPermille 后重排动画主轨；其他轨道保持原帧。原播放器不支持任意时间重映射。", MessageType.Info);
                        if (GUILayout.Button("Trim 设为完整 Clip")) SkillEditCommands.Apply(owner, "Reset clip trim", () => {
                            var a = owner.skill.animations.First(x => x.id == item.id);
                            if (a.clip) { a.trimStartUs = 0; a.trimEndUs = (long)Math.Round(a.clip.length * 1000000.0); SkillEditCommands.Reflow(owner); }
                        });
                    }
                    if (selection.StartsWith("group:") && target.FindPropertyRelative("damageSource").enumValueIndex == (int)DamageSource.ComboBaseDamage)
                        EditorGUILayout.LabelField("基础伤害（读取原字段）", owner.comboDamage.ToString());
                    if (GUILayout.Button("删除所选"))
                    {
                        if (item != null) SkillEditCommands.DeleteItem(owner, item.id);
                        else if (selection.StartsWith("track:"))
                        {
                            if (!EditorUtility.DisplayDialog("删除轨道", "将同时删除此轨道所有条目，可 Undo。", "删除", "取消")) { EditorGUILayout.EndScrollView(); return; }
                            SkillEditCommands.DeleteTrack(owner, selection.Substring(6));
                        }
                        else if (selection.StartsWith("group:"))
                        {
                            if (!EditorUtility.DisplayDialog("删除命中组", "将同时删除引用此组的攻击盒，可 Undo。", "删除", "取消")) { EditorGUILayout.EndScrollView(); return; }
                            SkillEditCommands.DeleteGroup(owner, selection.Substring(6));
                        }
                        select("");
                    }
                }
                else
                {
                    foreach (string name in new[] { "registered", "skillId", "displayName", "category", "skillType" })
                    {
                        using (new EditorGUI.DisabledScope(name == "skillId" || name == "registered")) EditorGUILayout.PropertyField(so.FindProperty("skill." + name));
                    }
                    EditorGUILayout.Space(); EditorGUILayout.LabelField("影响导出的原有字段", EditorStyles.boldLabel);
                    foreach (string field in new[] { "_comboName", "_comboDamage", "rootMotion" })
                    { var p = so.FindProperty(field); if (p != null) EditorGUILayout.PropertyField(p, true); }
                    if (so.hasModifiedProperties) SkillEditCommands.ApplyProperties(so, false);
                    if (GUILayout.Button("在原 Inspector 查看全部旧字段")) Selection.activeObject = owner;
                }
                EditorGUILayout.Space(); EditorGUILayout.LabelField("共享 HitGroups", EditorStyles.boldLabel);
                foreach (var g in owner.skill.hitGroups.ToArray()) if (g != null && GUILayout.Button(g.id)) select("group:" + g.id);
                if (GUILayout.Button("+ HitGroup")) select("group:" + SkillEditCommands.AddGroup(owner));
            }
            else EditorGUILayout.HelpBox("选择已登记技能；旧资产可在原 Inspector 登记。", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        static SerializedProperty Find(SerializedObject so, string path, string id)
        {
            var list = so.FindProperty(path);
            for (int i = 0; i < list.arraySize; i++) { var p = list.GetArrayElementAtIndex(i); if (p.FindPropertyRelative("id").stringValue == id) return p; }
            return null;
        }
        static void DrawCatalog(CharacterSkillCatalog catalog)
        {
            if (!catalog) return;
            var so = new SerializedObject(catalog); so.Update();
            foreach (string field in new[] { "characterId", "completeness", "migrationNotes", "animatorController", "hurtBoxConfigured", "hurtBox", "skills", "resources" })
                EditorGUILayout.PropertyField(so.FindProperty(field), true);
            if (so.hasModifiedProperties) SkillEditCommands.ApplyProperties(so, false);
        }
        static void EventType(SerializedProperty p)
        {
            var id = p.FindPropertyRelative("eventId"); int index = Array.IndexOf(EventIds, id.stringValue);
            int next = EditorGUILayout.Popup("事件类型", index, EventIds);
            if (next < 0 || next == index) return;
            id.stringValue = EventIds[next]; p.FindPropertyRelative("version").intValue = 1;
            p.FindPropertyRelative("execution").enumValueIndex = next == 0 ? 0 : 1;
            p.FindPropertyRelative("parameterKind").enumValueIndex = next == 0 ? 1 : 2;
            p.FindPropertyRelative("marker").FindPropertyRelative("text").stringValue = "";
            p.FindPropertyRelative("comboGate").FindPropertyRelative("enabled").boolValue = false;
        }
        static void DrawChildren(SerializedProperty root, bool animation, CharacterSkillCatalog catalog)
        {
            var p = root.Copy(); var end = p.GetEndProperty(); bool enter = true;
            while (p.NextVisible(enter) && !SerializedProperty.EqualContents(p, end))
            {
                enter = false;
                if (p.name == "hitGroupId" || p.name == "trackId") continue;
                if (root.FindPropertyRelative("eventId") != null)
                {
                    if (new[] { "eventId", "parameterKind", "execution" }.Contains(p.name)) continue;
                    bool marker = root.FindPropertyRelative("parameterKind").enumValueIndex == 1;
                    if ((p.name == "marker" && !marker) || (p.name == "comboGate" && marker)) continue;
                }
                if (root.FindPropertyRelative("lifetime") != null)
                {
                    bool auto = root.FindPropertyRelative("lifetime").enumValueIndex == (int)VfxLifetime.AutoComplete;
                    if ((p.name == "endFrame" && auto) || (p.name == "maxLifetimeFrames" && !auto)) continue;
                }
                bool baseDamage = root.FindPropertyRelative("damageSource")?.enumValueIndex == (int)DamageSource.ComboBaseDamage;
                if (p.name == "explicitDamage" && baseDamage) continue;
                if (p.name == "damageSource")
                {
                    EditorGUI.BeginChangeCheck(); EditorGUILayout.PropertyField(p);
                    if (EditorGUI.EndChangeCheck() && p.enumValueIndex == (int)DamageSource.ComboBaseDamage) root.FindPropertyRelative("explicitDamage").intValue = 0;
                    continue;
                }
                using (new EditorGUI.DisabledScope(p.name == "id" || p.name == "trackId" || (animation && (p.name == "startFrame" || p.name == "endFrame"))))
                {
                    if (catalog && (p.name == "resourceId" || p.name == "hitVfxResourceId" || p.name == "hitSfxResourceId"))
                    {
                        bool audio = p.name == "hitSfxResourceId" || root.propertyPath.Contains(".sfx.");
                        ReferencePopup(p, catalog.resources.Where(r => r != null && r.kind == (audio ? ResourceKind.AudioClip : ResourceKind.VfxPrefab)).Select(r => r.id).ToArray(), p.displayName);
                    }
                    else EditorGUILayout.PropertyField(p, true);
                }
            }
        }
        static void ReferencePopup(SerializedProperty p, string[] choices, string label)
        {
            string current = p.stringValue;
            var values = new[] { "" }.Concat(choices).Distinct().ToList();
            if (!values.Contains(current)) values.Add(current);
            var labels = values.Select(v => string.IsNullOrEmpty(v) ? "(未指定)" : choices.Contains(v) ? v : "Missing: " + v).ToArray();
            int index = EditorGUILayout.Popup(label, values.IndexOf(current), labels);
            if (index >= 0) p.stringValue = values[index];
        }
    }
}
