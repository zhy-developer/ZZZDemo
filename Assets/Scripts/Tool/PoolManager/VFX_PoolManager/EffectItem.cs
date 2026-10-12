
using UnityEngine;

public class EffectItem : PoolItemBase
{
    //不具备参考性：因为会受到慢动作影响，所以loop取消掉最好
    [SerializeField,Header("特效播放时间")] private float playTime;

    [SerializeField, Header("特效播放的速度")] private float playSpeed;

    private ParticleSystem[] ParticleSystem;
    private GameTimer recycleTimer;
    private TimerManager timerOwner;
    private VFXManager vfxOwner;

    private void Awake()
    {
        ParticleSystem =GetComponentsInChildren<ParticleSystem>();
        vfxOwner = VFXManager.Instance;
       
        for (int i = 0; i < ParticleSystem.Length; i++) 
        {
            vfxOwner.AddVFX(ParticleSystem[i], playSpeed);
        }
       
    }
    protected override void Spawn()
    {
        PlayFromPool();
    }

    public void PlayFromPool()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        StartPlay();
    }

    private void StartPlay()
    {
        CancelRecycle();

        for (int i = 0;i < ParticleSystem.Length;i++) 
        {
            ParticleSystem[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem[i].Play(true);
        }
      
        timerOwner = TimerManager.Instance;
        recycleTimer = timerOwner.GetTimer(playTime, StartReCycle);
    }
    private void StartReCycle()
    { 
       // Release the reference before the pooled timer can be rented by another effect.
       recycleTimer = null;
       if (this) gameObject.SetActive(false);
    }
    protected override void ReSycle()
    {
        CancelRecycle();
        for (int i = 0; i < ParticleSystem.Length; i++)
        {
            if (ParticleSystem[i]) ParticleSystem[i].Stop();
        }
    }
    private void CancelRecycle()
    {
        var timer = recycleTimer; recycleTimer = null;
        if (timerOwner) timerOwner.UnregisterTimer(timer);
    }
    private void OnDestroy()
    {
        CancelRecycle();
        if (vfxOwner && ParticleSystem != null)
            foreach (var particle in ParticleSystem) vfxOwner.RemoveVFX(particle);
    }
}
