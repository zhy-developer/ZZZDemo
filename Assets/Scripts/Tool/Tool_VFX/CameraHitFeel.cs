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

    Coroutine SlowMotionCoroutine;

    /// <summary>
    /// 旧顿帧入口：暂时停用，保留签名兼容旧连招调用。
    /// </summary>
    /// <param name="time"></param>
    public void PF(float time)
    {
        // 用户要求暂时关闭全部顿帧：不暂停双方 Animator，也不暂停 VFX。
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
       Time.timeScale=timeScale;
    }
    public void EndSlowTime()
    {
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
        var player = BattleManager.roleManage != null ? BattleManager.roleManage.LocalPlayer : null;
        return player != null ? player.characterAnimator : null;

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
