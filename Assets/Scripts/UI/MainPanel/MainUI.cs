using GameProtocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public Button btn_match;
    public Button btn_practice;
    public GameObject tips_matching;

    private CharacterNameList currentRole = CharacterNameList.Xingjianya;


    private void OnEnable()
    {
        TcpMessageDispatcher.Instance.mes_request_match_result += OnResponseRequestMatch;
    }

    private void OnResponseRequestMatch(TcpResponseRequestMatch message)
    {
        if (!tips_matching.activeSelf) {
            tips_matching.SetActive(true);
        }
    }

    private void OnDisable()
    {
        TcpMessageDispatcher.Instance.mes_request_match_result -= OnResponseRequestMatch;
    }
    // Start is called before the first frame update
    void Start()
    {
        if (tips_matching.activeSelf) {
            tips_matching.SetActive(false);
        }
        GameBlackboard.Instance.SetGameData<CharacterNameList>(GameConfig.ROLE_CURRENTNAME,currentRole);

        btn_match.onClick.AddListener(() =>
        {
            MatchManager.Instance.SendRequestMatchRequest();
        });

        btn_practice.onClick.AddListener(() =>
        {

        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
