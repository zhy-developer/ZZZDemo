using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using ZZZ.SkillAuthoring;

namespace ZZZ.SkillEditor
{
    /// <summary>独立的 DSP 时钟试听图。只在触发时定位，不用编辑器绘制间隔推进声音，不停止其他窗口音频。</summary>
    internal sealed class SkillPreviewAudio : IDisposable
    {
        private GameObject root;
        private PlayableGraph graph;

        public SkillPreviewAudio(SkillPresentationCue cue, Transform binding, double age)
        {
            try
            {
                root = new GameObject("技能音效预览") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, binding.gameObject.scene);
                root.transform.SetParent(binding, false); root.transform.localPosition = cue.localOffset;
                if (!cue.followBinding) root.transform.SetParent(null, true);
                var source = root.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0; source.volume = cue.volume;
                // 制作试听使用原速干声，排除跟随骨骼的位置变化和场景音频效果对听感的影响。
                source.pitch = 1; source.dopplerLevel = 0; source.spatialize = false;
                source.reverbZoneMix = 0;
                source.bypassEffects = true; source.bypassListenerEffects = true; source.bypassReverbZones = true;
                graph = PlayableGraph.Create("SkillAudioPreview");
                graph.SetTimeUpdateMode(DirectorUpdateMode.DSPClock);
                var clip = AudioClipPlayable.Create(graph, cue.audioClip, false);
                clip.SetSpeed(1);
                var output = AudioPlayableOutput.Create(graph, "技能音效", source);
                output.SetSourcePlayable(clip); output.SetWeight(1);
                graph.Play();
                // 只在开始或重新定位时调度；后续交给 DSP 时钟，不能反复 Evaluate(editorDelta)。
                clip.Seek(age, 0, Math.Max(0, cue.audioClip.length - age));
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }
    }
}
