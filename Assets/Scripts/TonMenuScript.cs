using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TonMenuScript : MonoBehaviour
{
    public GameManager GameManager;
    public AdminDataManager AdminDataManager;

    [Header("UI")] 
    public Text tonBalance;

    public Text adress;
    public Text MEME;

    private void OnEnable()
    {
        tonBalance.text = GameManager.currentUser.ton.ToString() + " TON";

        adress.text = AdminDataManager.Instance.GetValueById(1);
        MEME.text = GameManager.userID;
    }
}
