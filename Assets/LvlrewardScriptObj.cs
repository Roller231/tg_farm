using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class LvlrewardScriptObj : MonoBehaviour
{
 
    public Text rewardType;
    public Text rewardCount;
    public Text level;
    public Image img;
    public GameObject galka;

    public int isPremium; // 0 = обычная, 1 = премиум
    public bool rewardIsPremium = false; // по умолчанию


    private void OnEnable()
    {
        
    }
}
