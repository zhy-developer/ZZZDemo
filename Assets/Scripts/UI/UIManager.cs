using HuHu;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] public StateBarUI stateBarUI;
    [SerializeField] public SwitchTimeUI switchTimeUI;
     GameObject uIRoot;
    private Dictionary<Type, IUI> uiDictionary = new Dictionary<Type, IUI>();
    public void RegisterUI<T>(IUI uI) where T : IUI
    { 
        Type type = typeof(T);
        if (!uiDictionary.ContainsKey(type))
        {
            uiDictionary.Add(type, uI);
        }
        else
        {
            uiDictionary[type] = uI;
        }
    }
    public T Get<T>() where T : class,IUI
    { 
        Type t = typeof(T);
        if (uiDictionary.TryGetValue(t,out var uI))
        {
            return uI as T;
        }
        return default(T);
    }
    protected override void Awake()
    {
        base.Awake();
        if(stateBarUI == null || switchTimeUI == null)
        {
            uIRoot = GameObject.Find("UIRoot");
            if (uIRoot != null)
            {
                stateBarUI = uIRoot.transform.Find("State Bar").GetComponent<StateBarUI>();
                switchTimeUI = uIRoot.transform.Find("Switch Time").GetComponent<SwitchTimeUI>();
            }
        }
    }

}
