using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using ZZZ;

public class CameraHitFeel : MonoSingleton<CameraHitFeel>
{
    //*****依赖CharacterManager和特效管理器VFXManager*****//

    [SerializeField] private Animator currentCharacterAnimator;
    [SerializeField] private Animator currentEnemyAnimator;
    [SerializeField] private float slowMotionResetSpeed;
    [SerializeField] private Dictionary<CharacterNameList,Animator> characterAnimator=new Dictionary<CharacterNameList, Animator>();
    [SerializeField] private Dictionary<Transform, Animator> enemiesAnimator = new Dictionary<Transform, Animator>();
    [SerializeField] private CinemachineImpulseSource cinemachineImpulseSource;
   // [SerializeField] private Camera_ZoomController zoomController;
    
    private void Start()
    {
        Init();
    }

    private void Init()
    {


    }

    Coroutine PauseFrameCoroutine;
    Coroutine SlowMotionCoroutine;


    public void PF(float time)
    {
        if (time == 0) { Debug.Log("顿帧时间为0退出");return; }
        currentEnemyAnimator= GetEnemyAnimator();
        currentCharacterAnimator=GetCurrentCharacterAnimator();

        if (currentCharacterAnimator == null )
        {
            Debug.Log("currentCharacterAnimator is null!");
            return;
        }
        if (currentEnemyAnimator == null)
        {
            Debug.Log("currentEnemyAnimator is null!");
            return;
        }
   

        if (PauseFrameCoroutine != null)
        { 
        Debug.Log($"[AttackDebug] Stop previous pause frame coroutine before starting a new one. timeScale={Time.timeScale}");
        StopCoroutine(PauseFrameCoroutine);
        }
        Debug.Log($"[AttackDebug] Request pause frame. pauseTime={time}, timeScale={Time.timeScale}, characterAnimatorSpeed={currentCharacterAnimator.speed}, enemyAnimatorSpeed={currentEnemyAnimator.speed}");
        PauseFrameCoroutine = StartCoroutine(PauseFrameOnAnimation(time));
    }
    /// <summary>
    /// 动画特效的慢速
    /// </summary>
    /// <param name="time"></param>
    /// <param name="speedMult"></param>
    public void SlowMotion(float time,float speedMult)
    {
        currentEnemyAnimator = GetEnemyAnimator();
        currentCharacterAnimator = GetCurrentCharacterAnimator();
        if (currentCharacterAnimator == null || currentEnemyAnimator == null)
        {
            Debug.LogWarning("Animator is null!");
            return;
        }
        if (SlowMotionCoroutine != null)
        { StopCoroutine(SlowMotionCoroutine); }
        SlowMotionCoroutine = StartCoroutine(SlowMotionOnAnimation(time, speedMult));
    }
    public void StartSlowTime(float timeScale)
    { 
       Debug.Log($"[AttackDebug] StartSlowTime. from={Time.timeScale}, to={timeScale}, unscaledTime={Time.unscaledTime}");
       Time.timeScale=timeScale;
    }
    public void EndSlowTime()
    {
        Debug.Log($"[AttackDebug] EndSlowTime. from={Time.timeScale}, to=1, unscaledTime={Time.unscaledTime}");
        Time.timeScale = 1;
    }
    IEnumerator SlowMotionOnAnimation(float time,float speedMult)
    {
        float currentSpeed = speedMult;
        currentCharacterAnimator.speed = currentSpeed;
        currentEnemyAnimator.speed = currentSpeed;
        VFXManager.Instance.SetVFXSpeed(currentSpeed);
        yield return new WaitForSeconds(time);
        float minValue = 0.001f;
        while (Mathf.Abs(currentSpeed-1)>minValue)
        {
            currentSpeed = Mathf.Lerp(currentSpeed,1,Time.deltaTime* slowMotionResetSpeed);
            currentCharacterAnimator.speed = currentSpeed;
            currentEnemyAnimator.speed = currentSpeed;
            VFXManager.Instance.SetVFXSpeed(currentSpeed);

            yield return null;

        }
        currentSpeed = 1;
        currentCharacterAnimator.speed = currentSpeed;
        currentEnemyAnimator.speed = currentSpeed;
        VFXManager.Instance.SetVFXSpeed(currentSpeed);

    }
    IEnumerator PauseFrameOnAnimation(float time)
    {
        float startUnscaledTime = Time.unscaledTime;
        Debug.LogWarning($"[AttackDebug] Pause frame start. pauseTime={time}, timeScale={Time.timeScale}, characterAnimatorSpeedBefore={currentCharacterAnimator.speed}, enemyAnimatorSpeedBefore={currentEnemyAnimator.speed}, unscaledTime={startUnscaledTime}");
        currentCharacterAnimator.speed = 0f;
        currentEnemyAnimator.speed = 0f;
        VFXManager.Instance.PauseVFX();
        yield return new WaitForSeconds(time);
        VFXManager.Instance.ResetVXF();
        currentCharacterAnimator.speed = 1f;
        currentEnemyAnimator.speed = 1f;
        Debug.LogWarning($"[AttackDebug] Pause frame end. pauseTime={time}, realElapsed={Time.unscaledTime - startUnscaledTime}, timeScale={Time.timeScale}, characterAnimatorSpeedAfter={currentCharacterAnimator.speed}, enemyAnimatorSpeedAfter={currentEnemyAnimator.speed}");
    }
    

    private Animator GetEnemyAnimator()
    {
        Transform enemy;
        enemy = GameBlackboard.Instance.GetEnemy();
        if (enemy == null) { return null; }   
        if (enemiesAnimator.TryGetValue(enemy, out var A))
        {
            return A;
        }
        return null;
    }
    private Animator GetCurrentCharacterAnimator()
    {
        CharacterNameList characterName = SwitchCharacter.Instance.newCharacterName.Value;
        if (characterAnimator.TryGetValue(characterName, out var A))
        {
            return A;
        }
        return null;

    }
    
    #region 震屏
    public void CameraShake(float shakeForce)
    {
        if (shakeForce == 0) { return; }
        cinemachineImpulseSource.GenerateImpulseWithForce(shakeForce);
    }


    #endregion

    #region ZoomIn
    //float currentZoom;
    //public void ZoomIn(float distance)
    //{
    //    currentZoom = zoomController.currentDistance;
    //    zoomController.SetZoom(distance,100);

    //}
    //public void ResetZoom()
    //{
    //    zoomController.SetZoom(currentZoom, 50);
    //}

    #endregion
}
