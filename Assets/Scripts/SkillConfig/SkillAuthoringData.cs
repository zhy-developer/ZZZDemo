using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillConfig
{
    public enum SkillType { Unspecified, Normal, Ultimate }
    public enum CatalogCompleteness { PartialPilot, Complete }
    public enum RootBindingSource { None, LegacyCombo, Explicit }
    public enum MotionTailPolicy { Unverified, HoldAtCoverageEnd }
    public enum ResourceKind { VfxPrefab, AudioClip }

    [Serializable]
    public sealed class AnimationSegment
    {
        public string id;
        public int startFrame, endFrame;
        public AnimationClip clip;
        public List<string> statePaths = new List<string>();
        public long trimStartUs, trimEndUs;
        public int speedPermille = 1000;
        public RootBindingSource rootSource;
        public TextAsset additionalRootMotion;
        public int additionalEndFrameExclusive;
        public int additionalDistancePermille = 1000;
        public MotionTailPolicy tailPolicy;
    }

    [Serializable]
    public sealed class SkillAuthoringData
    {
        public bool registered;
        public string skillId, displayName, category;
        public SkillType skillType;
        public List<AnimationSegment> animations = new List<AnimationSegment>();
        public List<SkillTrack> tracks = new List<SkillTrack>();
        public List<HitGroup> hitGroups = new List<HitGroup>();
        public List<HitBox> hitBoxes = new List<HitBox>();
        public List<FrameWindow> invincibility = new List<FrameWindow>();
        public List<FrameWindow> interrupts = new List<FrameWindow>();
        public List<SkillEvent> events = new List<SkillEvent>();
        public List<VfxCue> vfx = new List<VfxCue>();
        public List<SfxCue> sfx = new List<SfxCue>();
    }
}
