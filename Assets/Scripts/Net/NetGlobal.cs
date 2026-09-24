using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 网络线程只负责收数据和解析数据，真正涉及 Unity 对象的逻辑丢给主线程执行。
/// </summary>
public class NetGlobal : Singleton<NetGlobal>
{
    private List<Action> list_action = new List<Action>();
    /// <summary>
    /// mutex: 互斥锁，目的：防止多个线程同时修改list_action
    /// </summary>
    private Mutex mutex_actionList = new Mutex();

    //对于这种场景（网络线程不断塞任务，Unity主线程不断取任务）（list<Action> + Mutex） 更推荐使用ConcurrentQueue<T> ----- 线程安全队列
    private readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();

    public string serverIP;
    public int udpSendPort;
    public int userUid;

    protected override void Initialize() {
        base.Initialize();
        GameObject obj = new GameObject("NetGlobal");
        obj.AddComponent<NetUpdate>();
    }

    //public void AddAction(Action _action)
    //{
    //    //如果没人使用，我要拿到这个锁
    //    mutex_actionList.WaitOne();
    //    //加入人物
    //    list_action.Add(_action);
    //    //释放锁（告诉其他人，这个锁我操作完了，你们可以用了）
    //    mutex_actionList.ReleaseMutex();
    //}

    public void AddAction(Action action) {
        actions.Enqueue(action);
    }

    public void DoForAction() {
        while (actions.TryDequeue(out Action action))
        {
            try { action?.Invoke(); }
            catch (Exception e) {
                Debug.LogException(e);
            }
        }
    }

    //下面这个方法可能会因为list_action中的一个任务执行过程过长而导致网络线程长期被锁
    //public void DoForAction()
    //{
    //    //1.拿锁，这样执行期间网络线程无法继续修改这个list
    //    mutex_actionList.WaitOne();
    //    for (int i = 0; i < list_action.Count; i++)
    //    {
    //        list_action[i]();
    //    }
    //    //执行完成后清理任务
    //    list_action.Clear();
    //    //释放锁
    //    mutex_actionList.ReleaseMutex();
    //}

}

public class NetUpdate : MonoBehaviour {
    private void Update()
    {
        NetGlobal.Instance.DoForAction();
    }

    private void OnApplicationQuit()
    {
        
    }
}
