using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public Button btn_match;
    public Button btn_practice;

    private CharacterNameList currentRole = CharacterNameList.Xingjianya;
    // Start is called before the first frame update
    void Start()
    {
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
