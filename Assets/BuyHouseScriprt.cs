using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class BuyHouseScriprt : MonoBehaviour
{
    public GameManager gm;

    public Text count;
    public GameObject menuBuy;
    public Button buyBtn;
    public int houseNumber;




    [Header("House")] 
    public GameObject houseObj;
    public GameObject buyObj;
    


    private void Start()
    {
        StartCoroutine(WaitForUserData());
    }

    
    private IEnumerator WaitForUserData()
    {
        while (gm.currentUser == null || string.IsNullOrEmpty(gm.currentUser.houses))
            yield return null; // ждём один кадр, пока GameManager загрузит данные

        gm.CheckHousesAndDo(houseNumber, house =>
        {
            Color c = houseObj.GetComponent<Image>().color;
            c.a = 1f;
            houseObj.GetComponent<Image>().color = c;

            houseObj.GetComponent<Button>().interactable = true;
            buyObj.gameObject.SetActive(false);
        });
    }
    
    public void SetCount()
    {
        gm.SetCount(houseNumber,count,menuBuy,buyBtn);
        
        buyBtn.onClick.RemoveAllListeners();
        buyBtn.onClick.AddListener(BuyHouseBTN);
    }


    public void BuyHouseBTN()
    {

            gm.currentUser.coin -= Int16.Parse(count.text);
            gm.money = gm.currentUser.coin;
            gm.ApplyUserData();
            
            StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString(CultureInfo.InvariantCulture)));

            gm.BuyHouseButton(houseNumber);
            menuBuy.SetActive(false);
            
            
            Color c =  houseObj.GetComponent<Image>().color;  // получаем текущий цвет
            c.a = 1f;                  // меняем альфу (0 = полностью прозрачный, 1 = полностью видимый)
            houseObj.GetComponent<Image>().color = c;

            houseObj.GetComponent<Button>().interactable = true;
            buyObj.gameObject.SetActive(false);

    }
}
