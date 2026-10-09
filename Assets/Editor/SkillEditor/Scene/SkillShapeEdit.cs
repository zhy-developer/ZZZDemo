using System;
using System.Linq;
using UnityEngine;

namespace SkillConfig.Editor
{
    // Only gesture-local geometry. This object is never serialized as a skill source.
    public sealed class SkillShapeEdit : IDisposable
    {
        ComboData owner;
        CharacterSkillCatalog catalog;
        public HitBox Box { get; private set; }
        public HurtBoxData Hurt { get; private set; }
        public bool Active => Box != null || Hurt != null;
        public static int Quantize(float value, int precision = 10000)
        {
            double scaled = Math.Round((double)value * precision, MidpointRounding.AwayFromZero);
            if (double.IsNaN(scaled) || scaled > int.MaxValue || scaled < int.MinValue) throw new ArgumentException("Geometry exceeds integer range.");
            return (int)scaled;
        }
        public static Int3 Quantize(Vector3 v) => new Int3 { x = Quantize(v.x), y = Quantize(v.y), z = Quantize(v.z) };
        public static Vector3 World(Int3 v) => new Vector3(v.x / 10000f, v.y / 10000f, v.z / 10000f);
        public void BeginBox(ComboData target, string id)
        {
            Cancel(); owner = target; Box = JsonUtility.FromJson<HitBox>(JsonUtility.ToJson(target.skill.hitBoxes.First(b => b.id == id))); SkillEditCommands.BeginGesture();
        }
        public void BeginHurt(CharacterSkillCatalog target)
        {
            Cancel(); catalog = target; Hurt = JsonUtility.FromJson<HurtBoxData>(JsonUtility.ToJson(target.hurtBox)); SkillEditCommands.BeginGesture();
        }
        public void Commit()
        {
            var target = owner; var character = catalog; var box = Box; var hurt = Hurt; Cancel();
            if (target && box != null)
            {
                var actual = target.skill.hitBoxes.FirstOrDefault(b => b.id == box.id); if (actual == null) return;
                SkillEditCommands.Apply(target, "Edit HitBox geometry", () => {
                    actual.offset = box.offset; actual.size = box.size; actual.yawMilliDegrees = box.yawMilliDegrees;
                    actual.radius = box.radius; actual.height = box.height; actual.sectorAngleMilliDegrees = box.sectorAngleMilliDegrees;
                });
            }
            else if (character && hurt != null) SkillEditCommands.Apply(character, "Edit HurtBox geometry", () => { character.hurtBox.offset = hurt.offset; character.hurtBox.radius = hurt.radius; character.hurtBox.height = hurt.height; });
        }
        public void Cancel() { if (Active) SkillEditCommands.EndGesture(); owner = null; catalog = null; Box = null; Hurt = null; }
        public void Dispose() => Cancel();
    }
}
