using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeLogPanel : MonoBehaviour
{
    [SerializeField] private Text logText;

    // 最多保留多少条日志
    [SerializeField] private int maxLogCount = 100;

    private readonly Queue<string> logs = new Queue<string>();

    private void OnEnable()
    {
        Application.logMessageReceived += OnLogMessageReceived;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        string prefix = "";

        switch (type)
        {
            case LogType.Log:
                prefix = "[Log] ";
                break;

            case LogType.Warning:
                prefix = "[Warning] ";
                break;

            case LogType.Error:
                prefix = "[Error] ";
                break;

            case LogType.Exception:
                prefix = "[Exception] ";
                break;

            case LogType.Assert:
                prefix = "[Assert] ";
                break;
        }

        logs.Enqueue(prefix + condition);

        while (logs.Count > maxLogCount)
        {
            logs.Dequeue();
        }

        logText.text = string.Join("\n", logs);
    }
}