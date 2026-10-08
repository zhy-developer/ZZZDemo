using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillConfig
{
    [Serializable] public sealed class SkillResourceBinding
    {
        public string id;
        public ResourceKind kind;
        public UnityEngine.Object asset;
    }
    [CreateAssetMenu(fileName = "CharacterSkills", menuName = "Skill Config/Character Catalog (Phase 1)")]
    public sealed class CharacterSkillCatalog : ScriptableObject
    {
        public string characterId;
        public CatalogCompleteness completeness = CatalogCompleteness.PartialPilot;
        public string migrationNotes;
        public RuntimeAnimatorController animatorController;
        public bool hurtBoxConfigured;
        public HurtBoxData hurtBox = new HurtBoxData();
        public List<ComboData> skills = new List<ComboData>();
        public List<SkillResourceBinding> resources = new List<SkillResourceBinding>();
        // Phase 1 can never declare a battle-ready catalog, even if all assets have been registered.
    }
}
