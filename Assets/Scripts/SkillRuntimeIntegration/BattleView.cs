using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZZZ;

namespace SkillRuntime.Integration
{
    // Render state only. No reference to BattleWorld or a mutable combat adapter.
    public sealed class BattleView : IDisposable
    {
        sealed class ActorView
        {
            internal RoleBase Role;
            internal CharacterHealthBase Health;
            internal long HitFrame = -1;
            internal float HitLength;
            internal CastKey? CinematicCast;
        }
        readonly BattleProfileSnapshot config;
        readonly Dictionary<int, ActorView> actors;
        readonly Queue<BattleFrame> pending = new Queue<BattleFrame>();
        readonly BattlePresentation effects;
        BattleFrame latest;
        long acceptedFrame = -1, effectFrame;
        double tailSeconds;
        bool disposed;
        public BattleView(BattleProfileSnapshot configuration, IEnumerable<RoleBase> roles)
        {
            config = configuration;
            actors = roles.ToDictionary(r => r.Player.BattleID, r => new ActorView { Role = r,
                Health = r.Player.GetComponent<CharacterHealthBase>() ?? r.Player.gameObject.AddComponent<CharacterHealthBase>() });
            foreach (var actor in actors.Values)
            {
                actor.Role.Player.BindBattleView();
                actor.Health.InitializeBattleHealth(config.HP);
            }
            effects = new BattlePresentation(config, actors.Values.Select(a => a.Role));
        }
        public void Enqueue(BattleFrame frame)
        {
            if (disposed || frame == null || frame.Frame <= acceptedFrame) return;
            acceptedFrame = frame.Frame;
            // Rendering can be paused without retaining unbounded combat history.
            if (pending.Count == 64) pending.Dequeue();
            pending.Enqueue(frame);
        }
        public void Render(float deltaSeconds)
        {
            if (disposed) return;
            while (pending.Count > 0)
            {
                latest = pending.Dequeue(); effectFrame = latest.Frame;
                foreach (var row in latest.Actors)
                {
                    var view = actors[row.Entity.BattleId];
                    if (!view.Role || !view.Role.Player) continue;
                    try
                    {
                        view.Role.ApplySnapshot(row);
                        view.Health.ApplySnapshot(row.HP, row.MaxHP);
                        view.Role.Player.BattleRoundClosed = latest.Result != RoundResult.None;
                        Present(view, row, latest);
                    }
                    catch (Exception e) { Debug.LogException(e); }
                }
                try
                {
                    effects.Tick(latest.Frame, latest.CapturedCasts, latest.Exits, latest.Feedback);
                    if (latest.Result != RoundResult.None)
                        foreach (var cast in latest.CapturedCasts) effects.Exit(cast.Key, CastExitReason.Interrupted, latest.Frame);
                }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (latest == null || latest.Result == RoundResult.None) return;
            tailSeconds += Math.Max(0, Math.Min(deltaSeconds, .25f));
            while (tailSeconds >= .033)
            {
                tailSeconds -= .033;
                try { effects.Tick(++effectFrame, new CastFrame[0], new CastExit[0], new HitFeedback[0]); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
        void Present(ActorView view, BattleActorSnapshot row, BattleFrame frame)
        {
            var player = view.Role.Player;
            var visual = new PlayerRenderFrame { IdleState = config.Idle, Moving = row.Moving };
            if (row.IsDead)
            { visual.Action = PlayerRenderAction.Dead; visual.Animation = config.Death; visual.NormalizedTime = .95f; view.HitFrame = -1; }
            else if (row.Cast != null)
            {
                view.HitFrame = -1; var cast = row.Cast;
                var skill = config.Skills.Single(s => s.Skill.SkillId == cast.Skill.SkillId);
                var segment = skill.Animations.FirstOrDefault(s => s.Time.Start <= cast.SkillFrame && cast.SkillFrame < s.Time.End);
                visual.Action = skill.Skill.Type == SkillConfig.SkillType.Dodge ? PlayerRenderAction.Dodge
                    : skill.Skill.Type == SkillConfig.SkillType.Skill || skill.Skill.Type == SkillConfig.SkillType.Ultimate ? PlayerRenderAction.Skill : PlayerRenderAction.Attack;
                visual.Animation = segment?.State ?? skill.State;
                visual.CastSequence = cast.Key.Sequence;
                visual.AnimationSegment = segment?.Time.Id;
                visual.ClipLength = segment?.Clip.length ?? skill.Clip.length;
                visual.PlaybackSpeed = segment == null ? 1 : segment.Time.SpeedPermille / 1000f;
                visual.EndNormalizedTime = segment == null ? 1 : (float)(segment.Time.TrimEndUs / 1000000.0 / segment.Clip.length);
                visual.NormalizedTime = segment == null ? (float)(cast.SkillFrame * .033 / skill.Clip.length)
                    : (float)(segment.Time.SourceNs(cast.SkillFrame) / 1000000000.0 / segment.Clip.length);
            }
            else
            {
                if (frame.Feedback.Any(f => f.Source.Target.Equals(row.Entity)))
                {
                    view.HitFrame = frame.Frame;
                    player.ApplyRenderFrame(new PlayerRenderFrame { Action = PlayerRenderAction.Hit, Animation = config.Hit, IdleState = config.Idle });
                    view.HitLength = player.characterAnimator.GetCurrentAnimatorStateInfo(0).length;
                }
                double elapsed = (frame.Frame - view.HitFrame) * .033;
                if (view.HitFrame >= 0 && view.HitLength > 0 && (elapsed < view.HitLength || row.Stun.Contains(frame.Frame)))
                { visual.Action = PlayerRenderAction.Hit; visual.Animation = config.Hit; visual.NormalizedTime = Mathf.Min(.99f, (float)(elapsed / view.HitLength)); }
            }
            player.ApplyRenderFrame(visual);
            var cinematic = row.IsDead || frame.Result != RoundResult.None || row.Cast == null ? null
                : config.Skills.Single(s => s.Skill.SkillId == row.Cast.Skill.SkillId);
            bool usesCamera = cinematic != null && (cinematic.Source.attackStyle == AttackStyle.Skill || cinematic.Source.attackStyle == AttackStyle.FinishSkill);
            CastKey? key = usesCamera ? row.Cast.Key : (CastKey?)null;
            if (!Nullable.Equals(view.CinematicCast, key))
            {
                player.StopSkillPresentation(); view.CinematicCast = key;
                if (usesCamera) player.StartSkillPresentation(cinematic.Source.attackStyle, row.Cast.SkillFrame * .033);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; pending.Clear(); effects.Dispose();
            foreach (var actor in actors.Values)
                if (actor.Role && actor.Role.Player) { actor.Role.Player.StopSkillPresentation(); actor.Role.Player.StopBattlePresentation(); }
        }
    }
}
