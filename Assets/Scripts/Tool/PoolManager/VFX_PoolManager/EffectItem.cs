
using UnityEngine;

public class EffectItem : PoolItemBase
{
    //不具备参考性：因为会受到慢动作影响，所以loop取消掉最好
    [SerializeField,Header("特效播放时间")] private float playTime;

    [SerializeField, Header("特效播放的速度")] private float playSpeed;

    private ParticleSystem[] ParticleSystem;
    private GameTimer recycleTimer;

    private void Awake()
    {
        ParticleSystem =GetComponentsInChildren<ParticleSystem>();
       
        for (int i = 0; i < ParticleSystem.Length; i++) 
        {
            VFXManager.Instance.AddVFX(ParticleSystem[i], playSpeed);
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
        TimerManager.Instance.UnregisterTimer(recycleTimer);

        for (int i = 0;i < ParticleSystem.Length;i++) 
        {
            ParticleSystem[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem[i].Play(true);
        }
      
        recycleTimer = TimerManager.Instance.GetTimer(playTime, StartReCycle);
    }
    private void StartReCycle()
    { 
       this.gameObject.SetActive(false);  
    }
    protected override void ReSycle()
    {

        for (int i = 0; i < ParticleSystem.Length; i++)
        {
            ParticleSystem[i].Stop();
        }
    }
}
