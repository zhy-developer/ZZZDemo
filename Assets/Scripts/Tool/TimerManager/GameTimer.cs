using System;
using UnityEngine;
public enum TimerStation
{ 
    /// <summary>
    /// 未工作
    /// </summary>
    NotWorked,
    /// <summary>
    /// 工作中
    /// </summary>
    DoWorking,
    /// <summary>
    /// 工作完成
    /// </summary>
    DoneWorked
}
public class GameTimer
{
    /// <summary>
    /// 这里的_StartTime是计时器的时间，_task是计时器完成后要执行的任务，_timerStation是计时器的状态，_isStopTime是计时器是否停止，_isRealTime是计时器是否使用真实时间
    /// 计时器的状态有三种，分别是未工作、工作中和工作完成，计时器的时间是计时器的倒计时时间，计时器的任务是计时器完成后要执行的任务，计时器是否停止是计时器是否停止计时，计时器是否使用真实时间是计时器是否使用真实时间
    /// </summary>
    private float _startTime;
    private Action _task;
    private TimerStation _timerStation;
    private bool _isStopTime;
    private bool _isRealTime;

    public TimerStation TimerStation => _timerStation;
    public bool IsRealTime => _isRealTime;
    public GameTimer()//new初始化
    {
        InitTimer();
    }
    public void StartTimer(bool isRealTime,float startTime,Action task)
    {
        //是否使用真实时间，不受time.timeScale影响
        _isRealTime = isRealTime;
        _startTime =startTime;
        _task = task;
        _isStopTime = false;
        _timerStation = TimerStation.DoWorking;
    }

    public void UpdateTimer()
    {
        if (_isRealTime) { return; }
        if (_isStopTime == true) { return; }

        _startTime-=Time.deltaTime;
        if (_startTime <= 0)
        { 
           _task?.Invoke();
           _isStopTime = true;
            _timerStation = TimerStation.DoneWorked;
        }
    }
    /// <summary>
    /// 不会受到ScaleTime影响
    /// </summary>
    public void UpdateRealTimer()
    {
        if (!_isRealTime) { return; }
        if (_isStopTime == true) { return; }

        _startTime -= Time.unscaledDeltaTime;
        if (_startTime <= 0)
        {
            _task?.Invoke();
            _isStopTime = true;
            _timerStation = TimerStation.DoneWorked;
        }
    }

    /// <summary>
    /// 恢复计时器状态
    /// </summary>
    public void InitTimer() 
    {
        _startTime = 0;
        _task = null;
        _isStopTime = true;
        _timerStation= TimerStation.NotWorked;
        _isRealTime = false;

    }
 

}
