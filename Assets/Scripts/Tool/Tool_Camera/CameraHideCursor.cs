using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraHideCursor : MonoBehaviour
{
    /// <summary>
    /// 用于隐藏鼠标指针
    /// </summary>
    private void Start()
    {
        UpdateCorcur();
    }

    private void UpdateCorcur()
    {
        Cursor.visible=false;
        Cursor.lockState= CursorLockMode.Locked;
    }
}
