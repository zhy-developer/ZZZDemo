using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ybh
{
    public class Test : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (CharacterInputSystem.Instance.Jump)
            {
                GameEventsManager.Instance.CallEvent("角色跳跃");
            }
        }
        private void OnEnable()
        {
            GameEventsManager.Instance.AddEventListening("角色跳跃", SendText);
        }
        private void OnDisable()
        {
            GameEventsManager.Instance.ReMoveEvent("角色跳跃", SendText);
        }
        private void SendText()
        {
            Debug.Log("事件成功被调用");
        }

    }
}
