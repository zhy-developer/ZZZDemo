using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillConfig.Editor
{
    public sealed class SkillComboGraphView
    {
        readonly SkillComboGraphDraft draft = new SkillComboGraphDraft();
        SkillEditorWorkspaceState.GraphLayout layout;
        readonly Dictionary<ComboData, Rect> bounds = new Dictionary<ComboData, Rect>();
        ComboData focus, dragging, linkFrom;
        bool dodgeLink, panning, layoutDirty, initialized;
        Vector2 startMouse, startPosition;
        string error;
        public bool Contains(ComboData value) => draft.Target && draft.Nodes.Contains(value);
        public void SaveLayout() { if (layoutDirty) { SkillEditorWorkspaceState.instance.Persist(); layoutDirty = false; } }
        public void ResetDraft() { error = null; if (draft.Target) draft.Load(draft.Target); linkFrom = null; dodgeLink = false; dragging = null; }
        public void Dispose() { SaveLayout(); ResetDraft(); }
        public void Focus(ComboData value)
        {
            focus = value;
            if (layout != null && value) { var p = Position(value, 0); layout.pan = new Vector2(60, 110) - p * layout.zoom; layoutDirty = true; }
        }
        void Load(ComboContainerData value)
        {
            SaveLayout(); draft.Load(value); error = null; dragging = linkFrom = null; dodgeLink = false;
            layout = null;
            if (!value) { SkillEditorWorkspaceState.instance.graphContainerGuid = ""; SkillEditorWorkspaceState.instance.Persist(); return; }
            var workspace = SkillEditorWorkspaceState.instance;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            layout = workspace.graphLayouts.FirstOrDefault(l => l.containerGuid == guid);
            if (layout == null) { layout = new SkillEditorWorkspaceState.GraphLayout { containerGuid = guid }; workspace.graphLayouts.Add(layout); }
            workspace.graphContainerGuid = guid; workspace.Persist();
        }
        Vector2 Position(ComboData node, int index)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node));
            var p = layout.nodes.FirstOrDefault(n => n.skillGuid == guid);
            if (p == null) { p = new SkillEditorWorkspaceState.GraphNode { skillGuid = guid, position = new Vector2((index % 3) * 255, 120 + (index / 3) * 145) }; layout.nodes.Add(p); layoutDirty = true; }
            return p.position;
        }
        void SetPosition(ComboData node, Vector2 value)
        {
            Position(node, 0); string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node));
            layout.nodes.First(n => n.skillGuid == guid).position = value; layoutDirty = true;
        }
        public void Draw(CharacterSkillCatalog catalog, ComboData owner, Action<ComboData, bool> select)
        {
            if (!initialized)
            {
                initialized = true;
                var saved = AssetDatabase.LoadAssetAtPath<ComboContainerData>(AssetDatabase.GUIDToAssetPath(SkillEditorWorkspaceState.instance.graphContainerGuid ?? ""));
                if (!saved && catalog) saved = AssetDatabase.FindAssets("t:ComboContainerData").Select(g => AssetDatabase.LoadAssetAtPath<ComboContainerData>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c => c.comboDatas != null && c.comboDatas.Any(s => catalog.skills.Contains(s)));
                if (saved) Load(saved);
            }
            if (draft.Target && !draft.Dirty && draft.Conflict) Load(draft.Target);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var target = (ComboContainerData)EditorGUILayout.ObjectField(draft.Target, typeof(ComboContainerData), false, GUILayout.MinWidth(130));
                if (target != draft.Target && (!draft.Dirty || EditorUtility.DisplayDialog("切换容器", "放弃当前未应用的连招草稿？正式资产尚未修改。", "放弃并切换", "取消"))) Load(target);
                GUILayout.Label(layoutDirty ? "布局待保存" : "布局已保存", GUILayout.Width(80));
                if (GUILayout.Button("保存布局", EditorStyles.toolbarButton)) SaveLayout();
                if (GUILayout.Button("定位", EditorStyles.toolbarButton)) Focus(owner);
            }
            if (!draft.Target) { EditorGUILayout.HelpBox("选择现有 ComboContainerData。不会新建运行时 Graph 数据。", MessageType.Info); return; }
            EditorGUILayout.LabelField(draft.Dirty ? "关系草稿未应用；原容器未写入" : (EditorUtility.IsDirty(draft.Target) ? "容器未保存" : "容器已保存"));
            EditorGUILayout.LabelField("Graph 不导出到技能 JSON；滚轮缩放 / 中键平移 / 双击 Timeline；关闭或重载丢弃未应用草稿", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!draft.Dirty || draft.Conflict))
                    if (GUILayout.Button("预览变更并应用"))
                    {
                        error = draft.Validate();
                        if (error == null && EditorUtility.DisplayDialog("确认修改连招原资产", draft.Describe(), "应用到原资产", "取消")) error = draft.ApplyConfirmed();
                    }
                if (GUILayout.Button("取消草稿 / 重载")) ResetDraft();
                if (GUILayout.Button("闪避入口连线 → 节点")) { dodgeLink = true; linkFrom = null; }
                var dodge = (ComboData)EditorGUILayout.ObjectField(draft.Dodge, typeof(ComboData), false);
                if (dodge != draft.Dodge) draft.Dodge = dodge;
            }
            string structuralError = draft.Validate();
            if (structuralError != null && !draft.Conflict) EditorGUILayout.HelpBox(structuralError, MessageType.Error);
            if (owner && !Contains(owner)) EditorGUILayout.HelpBox("当前技能不在所选容器的引用中，Graph 不会虚构关联节点。", MessageType.Info);
            if (draft.Conflict) EditorGUILayout.HelpBox("容器已被外部修改，草稿禁止应用；请取消草稿 / 重载。", MessageType.Error);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!draft.Dodge) EditorGUILayout.HelpBox("DodgeATKData 缺失：旧运行时闪避普攻会报错；请配置有效入口。", MessageType.Warning);
            EditorGUILayout.LabelField("BackDodgeATKData: " + SkillComboGraphDraft.Label(draft.Target.BackDodgeATKData) + "（无调用，虚线仅表示字段引用）", EditorStyles.miniLabel);
            if (linkFrom || dodgeLink) EditorGUILayout.HelpBox(dodgeLink ? "点击节点将其设为闪避第0段；Escape取消。" : "点击另一普通节点，将其移到源节点之后；这是重排，不是新增分支。Escape取消。", MessageType.Info);
            Rect canvas = GUILayoutUtility.GetRect(200, 260, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(canvas, GUIContent.none);
            var nodes = draft.Nodes.ToList();
            for (int i = 0; i < nodes.Count; ++i) Position(nodes[i], i);
            bounds.Clear();
            foreach (var node in nodes) bounds[node] = new Rect(canvas.position + layout.pan + Position(node, 0) * layout.zoom, new Vector2(225, 95) * layout.zoom);
            // Draw only clipped graph content. Positions remain in window coordinates for hit tests.
            GUI.BeginGroup(canvas);
            foreach (var pair in bounds.ToArray()) bounds[pair.Key] = new Rect(pair.Value.position - canvas.position, pair.Value.size);
            var oldColor = Handles.color;
            Handles.BeginGUI();
            for (int i = 1; i < draft.Steps.Count; ++i) Edge(draft.Steps[i - 1], draft.Steps[i], Color.cyan, false);
            if (draft.Steps.Count > 0 && draft.Steps[0] && draft.Steps[draft.Steps.Count - 1]) Edge(draft.Steps[draft.Steps.Count - 1], draft.Steps[0], Color.gray, true);
            if (draft.Dodge && draft.Steps.Count > 1) Edge(draft.Dodge, draft.Steps[1], Color.yellow, false);
            Entry("普通起手 / 断连回0", draft.Steps.FirstOrDefault(), new Vector2(12,62), Color.cyan, false);
            Entry("闪避或疾跑普攻 / 替换0", draft.Dodge, new Vector2(245,62), Color.yellow, false);
            Entry("Back 字段（无调用）", draft.Target.BackDodgeATKData, new Vector2(490,62), Color.gray, true);
            Handles.EndGUI(); Handles.color = oldColor;
            GUI.Label(new Rect(8, 5, canvas.width - 16, 40), "青线=下一索引；灰虚线=末段回0（均需旧输入窗口允许）\n黄线=闪避第0段之后接索引1；无任意条件分支", EditorStyles.miniLabel);
            foreach (var node in nodes)
            {
                var r = bounds[node];
                var old = GUI.backgroundColor; GUI.backgroundColor = owner == node ? new Color(.35f,.7f,1) : Color.white;
                GUI.Box(r, GUIContent.none, EditorStyles.helpBox); GUI.backgroundColor = old;
                string slots = string.Join(",", draft.Steps.Select((s,i) => new { s,i }).Where(x => x.s == node).Select(x => x.i.ToString()));
                string status = catalog && catalog.skills.Contains(node) ? "目录内" : "目录外 / 不自动迁移";
                GUI.Label(new Rect(r.x + 5, r.y + 4, r.width - 10, r.height - 8), new GUIContent(SkillComboGraphDraft.Label(node) + "\n索引: " + slots + (node == draft.Dodge ? " | 闪避第0段" : "") + "\n" + status, AssetDatabase.GetAssetPath(node)), new GUIStyle(EditorStyles.wordWrappedMiniLabel));
            }
            GUI.EndGroup();
            foreach (var pair in bounds.ToArray()) bounds[pair.Key] = new Rect(pair.Value.position + canvas.position, pair.Value.size);
            if (focus != owner) { focus = owner; if (owner && bounds.ContainsKey(owner)) Focus(owner); }
            HandleInput(canvas, owner, select);
        }
        void Entry(string label, ComboData target, Vector2 origin, Color color, bool dotted)
        {
            GUI.Label(new Rect(origin.x,origin.y-18,230,20),label,EditorStyles.miniLabel);
            if(!target || !bounds.ContainsKey(target)) return;
            Vector2 end = new Vector2(bounds[target].center.x,bounds[target].y);
            Handles.color=color;
            if(dotted) Handles.DrawDottedLine(origin,end,5);
            else Handles.DrawBezier(origin,end,origin+Vector2.up*30,end-Vector2.up*30,color,null,2);
            Handles.DrawAAPolyLine(2,new Vector3(end.x-4,end.y-8),new Vector3(end.x,end.y),new Vector3(end.x+4,end.y-8));
        }
        void Edge(ComboData from, ComboData to, Color color, bool dotted)
        {
            if (!from || !to || !bounds.ContainsKey(from) || !bounds.ContainsKey(to)) return;
            Vector2 a = bounds[from].center + Vector2.right * bounds[from].width * .5f;
            Vector2 b = bounds[to].center - Vector2.right * bounds[to].width * .5f;
            Handles.color = color;
            if (dotted) { a += Vector2.up * 45; b += Vector2.up * 45; Handles.DrawDottedLine(a, b, 5); }
            else Handles.DrawBezier(a,b,a + Vector2.right*35,b-Vector2.right*35,color,null,2);
            Handles.DrawAAPolyLine(2, new Vector3(b.x+8,b.y-4), new Vector3(b.x,b.y), new Vector3(b.x+8,b.y+4));
        }
        void HandleInput(Rect canvas, ComboData owner, Action<ComboData,bool> select)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            { if (dragging) SetPosition(dragging,startPosition); dragging = linkFrom = null; dodgeLink = panning = false; e.Use(); }
            if (e.rawType == EventType.MouseUp) { dragging = null; panning = false; }
            if (!canvas.Contains(e.mousePosition) && !dragging && !panning) return;
            if (e.type == EventType.ScrollWheel)
            {
                Vector2 logical = (e.mousePosition - canvas.position - layout.pan) / layout.zoom;
                layout.zoom = Mathf.Clamp(layout.zoom * Mathf.Pow(1.1f,-e.delta.y),.4f,1.6f);
                layout.pan = e.mousePosition - canvas.position - logical * layout.zoom; layoutDirty = true; e.Use();
            }
            if (e.type == EventType.MouseDown && e.button == 2) { panning = true; e.Use(); }
            if (e.type == EventType.MouseDrag && panning) { layout.pan += e.delta; layoutDirty = true; e.Use(); }
            if (e.type == EventType.MouseDrag && dragging) { SetPosition(dragging,startPosition+(e.mousePosition-startMouse)/layout.zoom); e.Use(); }
            if (e.type != EventType.MouseDown || e.button == 2) return;
            var node = bounds.Where(p => p.Value.Contains(e.mousePosition)).Select(p => p.Key).LastOrDefault();
            if (!node)
            {
                if (e.button == 1)
                    for (int i=1;i<draft.Steps.Count;i++)
                    {
                        var from=draft.Steps[i-1]; var to=draft.Steps[i];
                        if(!from || !to || !bounds.ContainsKey(from) || !bounds.ContainsKey(to)) continue;
                        var a=bounds[from].center + Vector2.right*bounds[from].width*.5f;
                        var b=bounds[to].center - Vector2.right*bounds[to].width*.5f;
                        if(HandleUtility.DistancePointBezier(e.mousePosition,a,b,a+Vector2.right*35,b-Vector2.right*35)>9) continue;
                        var menu=new GenericMenu();
                        menu.AddItem(new GUIContent("重接此边到另一普通节点（重排）"),false,()=>{linkFrom=from;dodgeLink=false;});
                        menu.AddItem(new GUIContent("移除目标段并连接前后（待应用）"),false,()=>draft.Steps.Remove(to));
                        menu.ShowAsContext(); e.Use(); break;
                    }
                return;
            }
            if (e.button == 1)
            {
                var menu = new GenericMenu(); int index = draft.Steps.IndexOf(node);
                if (index >= 0)
                {
                    menu.AddItem(new GUIContent("连线/将另一普通节点接在此后（重排）"),false,()=> {linkFrom=node; dodgeLink=false;});
                    if (index > 0) menu.AddItem(new GUIContent("顺序前移"),false,()=>draft.Move(index,index-1));
                    if (index < draft.Steps.Count-1) menu.AddItem(new GUIContent("顺序后移"),false,()=>draft.Move(index,index+1));
                    menu.AddItem(new GUIContent("移除此普通段（前后自动相接）"),false,()=>draft.Steps.Remove(node));
                }
                menu.AddItem(new GUIContent("设为闪避第0段"),false,()=>draft.Dodge=node);
                menu.AddItem(new GUIContent("定位原资产"),false,()=>EditorGUIUtility.PingObject(node));
                menu.ShowAsContext(); e.Use(); return;
            }
            if (dodgeLink) { draft.Dodge=node; dodgeLink=false; }
            else if (linkFrom) { error=draft.ConnectAfter(linkFrom,node); linkFrom=null; }
            else
            {
                focus = node; select(node,e.clickCount==2);
                if(e.clickCount==1) { dragging=node; startMouse=e.mousePosition; startPosition=Position(node,0); }
            }
            e.Use();
        }
    }
}
