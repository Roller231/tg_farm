using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridController : MonoBehaviour
{
    public GameManager gameManager;

    public GameObject menuBuy;
    public Text priceText;
    public Button buyBtn;
    
    public void OpenBuyMenu(int GridId)
    {
        menuBuy.SetActive(true);
        priceText.text = gameManager.cells[GridId].priceGrid.ToString();
        if (gameManager.money < gameManager.cells[GridId].priceGrid)
        {
            priceText.color = Color.red;
        }
        else if (gameManager.money >= gameManager.cells[GridId].priceGrid)
        {
            priceText.color = Color.white;
        }

        if (gameManager.lvl < gameManager.cells[GridId].needLvl)
        {
            priceText.text = "Вам нужен уровень " + gameManager.cells[GridId].needLvl + "lvl" + " для покупки";
            priceText.color = Color.red;
        }

        
        // Сначала очищаем кнопку
        buyBtn.onClick.RemoveAllListeners();
        
        buyBtn.onClick.AddListener(() =>
        {
            gameManager.cells[GridId].BuyGridFunc();
            menuBuy.SetActive(false);
        });

    }

    public void StartGrid()
    {
        for (int i = 0; i < gameManager.currentUser.grid_count; i++)
        {
            gameManager.cells[i].OpenGrid();
        }
    }
}
