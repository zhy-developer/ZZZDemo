using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoSingleton<VFXManager>
{
    [SerializeField] private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
    [SerializeField, Header("特效播放倍率")] private float SpeedMult = 1f;

    public void AddVFX(ParticleSystem particleSystem, float speedMult)
    {
        particleSystems.Add(particleSystem);
        var main = particleSystem.main;
        main.simulationSpeed = speedMult > 0f ? speedMult : SpeedMult;
    }

    public List<ParticleSystem> allParticleSystems => particleSystems;

    public void PauseVFX()
    {
        foreach (var particleSystem in allParticleSystems)
        {
            var main = particleSystem.main;
            main.simulationSpeed = 0f;
        }
    }

    public void SetVFXSpeed(float speedMult)
    {
        foreach (var particleSystem in allParticleSystems)
        {
            var main = particleSystem.main;
            main.simulationSpeed = speedMult;
        }
    }

    public void ResetVFX()
    {
        foreach (var particleSystem in allParticleSystems)
        {
            var main = particleSystem.main;
            main.simulationSpeed = SpeedMult;
        }
    }
}
