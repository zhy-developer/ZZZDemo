using UnityEngine;

namespace ZZZ
{
    // Presentation-only clock. Animator advances between snapshots; nothing is fed back to combat.
    public sealed class PlayerAttackPlayback
    {
        const float CorrectionSeconds = .12f;
        readonly Animator animator;
        bool active;
        long sequence;
        int stateHash;
        string segment;
        float endTime;

        public PlayerAttackPlayback(Animator animator) { this.animator = animator; }
        public void Reset() { active = false; }

        public void Apply(PlayerRenderFrame frame)
        {
            if (!animator || string.IsNullOrEmpty(frame.Animation) || frame.ClipLength <= 0 || frame.PlaybackSpeed <= 0) return;
            int hash = Animator.StringToHash(frame.Animation);
            if (!animator.HasState(0, hash)) return;
            bool entering = !active || sequence != frame.CastSequence || stateHash != hash || segment != frame.AnimationSegment;
            active = true; sequence = frame.CastSequence; stateHash = hash; segment = frame.AnimationSegment;
            // Hold before the loop boundary while waiting for the authoritative successor/exit.
            endTime = Mathf.Clamp(frame.EndNormalizedTime, 0, .99999f);
            float target = Mathf.Clamp(frame.NormalizedTime, 0, endTime);
            var current = animator.GetCurrentAnimatorStateInfo(0);
            float driftSeconds = Mathf.Abs(Mathf.Min(current.normalizedTime, endTime) - target) * frame.ClipLength / frame.PlaybackSpeed;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            if (entering || current.fullPathHash != hash || animator.IsInTransition(0) || driftSeconds > CorrectionSeconds)
            {
                // Seek only for a new cast/segment or a large clock discontinuity, not every logic tick.
                animator.Play(hash, 0, target);
                animator.Update(0);
                current = animator.GetCurrentAnimatorStateInfo(0);
            }
            // Account for any speed multiplier authored on the Animator state itself.
            float stateSpeed = current.speed * current.speedMultiplier;
            animator.speed = current.normalizedTime >= endTime || stateSpeed <= 0 ? 0 : frame.PlaybackSpeed / stateSpeed;
            ClampEnd();
        }

        public void ClampEnd()
        {
            if (!active || !animator) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash != stateHash || current.normalizedTime < endTime) return;
            animator.speed = 0;
            if (current.normalizedTime > endTime)
            {
                animator.Play(stateHash, 0, endTime);
                animator.Update(0);
            }
        }
    }
}
