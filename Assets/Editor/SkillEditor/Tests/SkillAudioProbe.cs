using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
namespace SkillConfig.Editor.Tests
{
    public static class SkillAudioProbe
    {
        static GameObject go; static AudioClip clip; static PlayableGraph graph; static double start; static int phase; static AudioSource a,b;
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Temp/SkillEditorPhase2C/")) throw new Exception("Harness only");
            var type=typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Where(m=>m.Name.Contains("Preview"))) Debug.Log("AUDIO_API "+method);
            go=EditorUtility.CreateGameObjectWithHideFlags("AudioProbe",HideFlags.HideAndDontSave);
            a=go.AddComponent<AudioSource>();b=go.AddComponent<AudioSource>();
            clip=AudioClip.Create("silent-probe",44100*4,1,44100,false); clip.SetData(new float[44100*4],0);
            a.clip=clip; a.volume=.1f; a.Play();
            graph=PlayableGraph.Create("AudioProbe");graph.SetTimeUpdateMode(DirectorUpdateMode.DSPClock);
            var playable=AudioClipPlayable.Create(graph,clip,true); AudioPlayableOutput.Create(graph,"out",b).SetSourcePlayable(playable); graph.Play();
            start=EditorApplication.timeSinceStartup;phase=0;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate(); double time=EditorApplication.timeSinceStartup-start;
            if(time < (phase+1)*.5) return;
            Debug.Log("AUDIO_PROBE phase="+phase+" A="+a.isPlaying+" samples="+a.timeSamples+" B="+b.isPlaying+" samples="+b.timeSamples+" graph="+graph.GetRootPlayable(0).GetTime());
            if(phase==0) { a.Pause();graph.Stop(); b.Pause(); }
            if(phase==1) { a.UnPause();graph.Play();b.UnPause();a.volume=.25f; }
            if(++phase<3) return;
            EditorApplication.update-=Tick;a.Stop();b.Stop();graph.Destroy();UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(clip);EditorApplication.Exit(0);
        }
    }
}
