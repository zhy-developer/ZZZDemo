using System;
using UnityEditor;
using UnityEngine;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    internal enum SkillEffectTool { Move, Rotate, Scale }

    /// <summary>只编辑选中特效事件的局部偏移/旋转/缩放；不改动 Prefab 或预览实例的持久数据。</summary>
    internal sealed class SkillEffectSceneHandles
    {
        public SkillEffectTool Tool;

        public void Draw(SkillDefinition skill, SkillPresentationCue cue, Matrix4x4 bindingMatrix,
            Quaternion bindingRotation, Action changed)
        {
            Vector3 position = bindingMatrix.MultiplyPoint3x4(cue.localOffset);
            Quaternion rotation = bindingRotation * Quaternion.Euler(cue.localEulerAngles);
            Color previous = Handles.color;
            try
            {
                Handles.color = Color.cyan;
                Handles.DrawDottedLine(bindingMatrix.MultiplyPoint3x4(Vector3.zero), position, 4);
                string name = cue.effectPrefab != null ? cue.effectPrefab.name : "未绑定资源";
                string toolName = Tool == SkillEffectTool.Move ? "移动" : Tool == SkillEffectTool.Rotate ? "旋转" : "缩放";
                Handles.Label(position + Vector3.up * HandleUtility.GetHandleSize(position) * .15f,
                    "特效事件：" + name + "（" + toolName + " XYZ）");
                EditorGUI.BeginChangeCheck();
                Vector3 nextPosition = position;
                Quaternion nextRotation = rotation;
                Vector3 nextScale = cue.localScale;
                if (Tool == SkillEffectTool.Move)
                    nextPosition = Handles.PositionHandle(position, bindingRotation);
                else if (Tool == SkillEffectTool.Rotate)
                    nextRotation = Handles.RotationHandle(rotation, position);
                else
                    nextScale = Handles.ScaleHandle(cue.localScale, position, rotation, HandleUtility.GetHandleSize(position));
                if (!EditorGUI.EndChangeCheck()) return;

                Undo.RecordObject(skill, toolName + "技能特效");
                // 位置使用完整矩阵，包含绑定节点及父层级缩放；旋转只回写事件额外旋转，保留 Prefab 自身朝向。
                if (Tool == SkillEffectTool.Move) cue.localOffset = bindingMatrix.inverse.MultiplyPoint3x4(nextPosition);
                else if (Tool == SkillEffectTool.Rotate) cue.localEulerAngles = (Quaternion.Inverse(bindingRotation) * nextRotation).eulerAngles;
                // 手柄编辑的是事件局部倍数，不能把绑定节点或 Prefab 原有缩放重复写入配置。
                else cue.localScale = Vector3.Max(nextScale, Vector3.one * .01f);
                EditorUtility.SetDirty(skill);
                changed();
            }
            finally { Handles.color = previous; }
        }
    }
}
