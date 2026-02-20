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

        bool isNextInOrder = GridId == gameManager.currentUser.grid_count;

        if (!isNextInOrder)
        {
            priceText.text = "Сначала купите предыдущую клетку";
            priceText.color = Color.red;
            buyBtn.onClick.RemoveAllListeners();
            buyBtn.interactable = false;
            return;
        }

        buyBtn.interactable = true;
        priceText.text = gameManager.cells[GridId].priceGrid.ToString();
        priceText.color = gameManager.money >= gameManager.cells[GridId].priceGrid ? Color.white : Color.red;

        if (gameManager.lvl < gameManager.cells[GridId].needLvl)
        {
            priceText.text = "Вам нужен уровень " + gameManager.cells[GridId].needLvl + "lvl" + " для покупки";
            priceText.color = Color.red;
        }

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
