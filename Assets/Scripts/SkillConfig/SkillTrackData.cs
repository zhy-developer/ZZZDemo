using System;

namespace SkillConfig
{
    public enum TrackKind { HitBox, Invincibility, Interrupt, Event, Vfx, Sfx }
    public enum ShapeKind { Box, Sphere, Capsule, Sector, Cylinder }
    public enum HitMode { Single, Repeat }
    public enum DamageSource { ComboBaseDamage, Explicit }
    public enum VfxClass { Precast, AttackCritical }
    public enum VfxLifetime { TimelineControlled, AutoComplete }
    public enum AttachmentMode { Root, Bone, World }
    public enum SoundMode { OneShot, Loop }
    public enum SoundInterrupt { StopImmediately, PlayToEnd, FadeOut }
    [Serializable] public struct Int3 { public int x, y, z; }
    [Serializable] public sealed class HurtBoxData { public Int3 offset; public int radius, height; }
    [Serializable] public sealed class SkillTrack { public string id, name, group; public TrackKind kind; public int displayOrder; }
    [Serializable] public sealed class FrameWindow { public string id, trackId; public int startFrame, endFrame; }
    [Serializable] public sealed class HitGroup
    {
        public string id;
        public HitMode mode;
        public int repeatIntervalFrames = 1;
        public DamageSource damageSource;
        public int explicitDamage, hitStunFrames;
        public Int3 knockback;
        public int knockbackFrames;
        public string hitVfxResourceId, hitSfxResourceId;
    }
    [Serializable] public sealed class HitBox
    {
        public string id, trackId, hitGroupId;
        public int startFrame, endFrame;
        public ShapeKind shape;
        public Int3 offset, size;
        public int yawMilliDegrees, radius, height, sectorAngleMilliDegrees;
    }
    [Serializable] public sealed class VfxCue
    {
        public string id, trackId, resourceId, bone;
        public int startFrame, endFrame;
        public VfxClass classification;
        public VfxLifetime lifetime;
        public AttachmentMode attachment;
        public Int3 offset, rotationMilliDegrees;
        public int scalePermille = 1000;
        // Required explicit bound for looping/unknown prefab lifetimes; no pool is invoked in Phase 1.
        public int maxLifetimeFrames;
    }
    [Serializable] public sealed class SfxCue
    {
        public string id, trackId, resourceId;
        public int startFrame, endFrame;
        public SoundMode mode;
        public SoundInterrupt onInterrupt;
        public int volumePermille = 1000, fadeInFrames, fadeOutFrames;
    }
}
