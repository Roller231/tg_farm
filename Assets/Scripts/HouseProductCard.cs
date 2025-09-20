using System;
using UnityEngine;
using UnityEngine.UI;

public class HouseProductCard : MonoBehaviour
{
    [Header("UI")]
    public Image productImage;
    public Text rewardText;
    public Text name;
    public Text timerText;
    public Button upgradeBtn;

    [Header("Runtime")]
    public int productId;
    public int houseId;

    private int leftSec;
    private int lvl;
    private GameManager.ProductDto product;

    private GameManager gm;
    private float acc;      // накопитель времени
    private float syncAcc;  // накопитель для синхронизации

    // Инициализация карточки
    public void Init(GameManager gameManager, int houseId, GameManager.ProductDto product, int leftSeconds, int lvl)
    {
        gm = gameManager;
        this.houseId = houseId;
        this.productId = product.id;
        this.product = product;
        this.lvl = lvl;
        leftSec = leftSeconds;

        // слушатель кнопки
        upgradeBtn.onClick.RemoveAllListeners();
        upgradeBtn.onClick.AddListener(() =>
        {
            gm.UpgradeProductInHouseButton(houseId, product.id);
            SyncWithGameManager();  // подтянем актуальное с сервера

        });

        if (productImage && !string.IsNullOrEmpty(product.image_ready_link))
        {
            StartCoroutine(LoadImage(product.image_ready_link));
        }

        RefreshUI();            // сразу обновляем UI
        SyncWithGameManager();  // подтянем актуальное с сервера
        UpdateTimerText();
    }

    private void Update()
    {
        if (leftSec > 0)
        {
            acc += Time.deltaTime;
            if (acc >= 1f)
            {
                int sec = Mathf.FloorToInt(acc);
                acc -= sec;
                leftSec -= sec;
                if (leftSec < 0) leftSec = 0;
                UpdateTimerText();
            }
        }

        // каждые 3 сек синхронизируем с сервером
        syncAcc += Time.deltaTime;
        if (syncAcc >= 3f)
        {
            syncAcc = 0f;
            SyncWithGameManager();
        }
    }

    private void SyncWithGameManager()
    {
        var houses = gm.GetType()
                       .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                       .Invoke(gm, null) as GameManager.HousesWrapper;

        if (houses == null) return;
        var house = houses.items.Find(x => x.id == houseId);
        if (house == null || house.timers == null) return;

        var timer = house.timers.Find(t => t.pid == productId);
        if (timer == null) return;

        leftSec = timer.left; 
        lvl = timer.lvl;   // 👈 теперь уровень подтягиваем с сервера
        RefreshUI();
        UpdateTimerText();
    }

    private void RefreshUI()
    {
        if (product == null || gm == null) return;

        // Название + уровень
        if (name) 
            name.text = $"{product.name} (lvl {lvl})";

        // Награда
        if (rewardText)
        {
            if (lvl < 4)
            {
                float rewardCoin = product.sell_price * 1.5f * lvl;
                rewardText.text = $"+{rewardCoin:0} COIN";
            }
            else
            {
                rewardText.text = $"+{product.sell_price /100} TON";
            }
        }

        // Кнопка улучшения
        if (upgradeBtn)
        {
            if (lvl >= 4)
            {
                upgradeBtn.interactable = false;
                upgradeBtn.GetComponentInChildren<Text>().text = "MAX";
            }
            else
            {
                // пример формулы стоимости апгрейда
                float upgradeCost = product.price * (lvl + 1) * 2f;
                bool canAfford = gm.currentUser.coin >= upgradeCost;

                upgradeBtn.interactable = canAfford;
                upgradeBtn.GetComponentInChildren<Text>().text =
                    $"Улучшить ({upgradeCost:0} монет)";
            }
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        TimeSpan ts = TimeSpan.FromSeconds(leftSec);
        timerText.text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private System.Collections.IEnumerator LoadImage(string url)
    {
        using (var www = new UnityEngine.Networking.UnityWebRequest(url))
        {
            www.downloadHandler = new UnityEngine.Networking.DownloadHandlerTexture();
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D tex = ((UnityEngine.Networking.DownloadHandlerTexture)www.downloadHandler).texture;
                productImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
