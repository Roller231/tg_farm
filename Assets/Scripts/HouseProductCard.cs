using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HouseProductCard : MonoBehaviour
{
    [Header("UI")]
    public Image productImage;
    public Text rewardText;
    public Text name;
    public Text timerText;
    public Text percentText;
    public Button upgradeBtn;

    [Header("Runtime")]
    public int productId;
    public int houseId;

    private int leftSec;
    private int lvl;
    private bool needEat; // локальный флаг
    private GameManager.ProductDto product;

    private GameManager gm;
    private float acc;      // накопитель времени
    private float syncAcc;  // накопитель для синхронизации

    private bool uiActionLocked;
    private Coroutine unlockCo;

    // Инициализация карточки
    public void Init(GameManager gameManager, int houseId, GameManager.ProductDto product, int leftSeconds, int lvl)
    {
        gm = gameManager;
        this.houseId = houseId;
        this.productId = product.id;
        this.product = product;
        this.lvl = lvl;
        leftSec = leftSeconds;
        needEat = false;

        upgradeBtn.onClick.RemoveAllListeners();
        upgradeBtn.onClick.AddListener(() =>
        {
            upgradeBtn.interactable = false; // 🔴 сразу блокируем
            LockUiAction();
            gm.UpgradeProductInHouseButton(houseId, product.id);
            SyncWithGameManager();
        });

        if (productImage && !string.IsNullOrEmpty(product.image_ready_link))
        {
            StartCoroutine(LoadImage(product.image_ready_link));
        }

        RefreshUI();
        SyncWithGameManager();
        UpdateTimerText();
    }

    public void SetButtonToCollect()
    {
        GetComponentInChildren<Text>().text = "Собрать ресурсы";
        timerText.gameObject.SetActive(false);

        upgradeBtn.onClick.RemoveAllListeners();
        upgradeBtn.onClick.AddListener(() =>
        {
            upgradeBtn.interactable = false; // 🔴 сразу блокируем
            LockUiAction();
            gm.CollectHouseProductButton(houseId, productId);
            SyncWithGameManager();
        });
    }

    private void LockUiAction()
    {
        uiActionLocked = true;

        if (upgradeBtn != null)
            upgradeBtn.interactable = false;

        if (unlockCo != null)
            StopCoroutine(unlockCo);

        unlockCo = StartCoroutine(UnlockUiActionAfterDelay());
    }

    private IEnumerator UnlockUiActionAfterDelay()
    {
        yield return new WaitForSeconds(3.5f); // 🔴 3.5 секунды

        uiActionLocked = false;
        unlockCo = null;

        RefreshUI(); // вернет кнопку в корректное состояние
    }

    private void Update()
    {
        RefreshUI();

        // пока needEat = true — таймер не идёт
        if (!needEat && leftSec > 0)
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

        // каждые 3 сек подтягиваем актуал
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
        lvl = timer.lvl;
        needEat = !string.IsNullOrEmpty(timer.needEat) && timer.needEat.Equals("true", StringComparison.OrdinalIgnoreCase);

        RefreshUI();
        UpdateTimerText();
    }

    private void RefreshUI()
    {
        if (product == null || gm == null) return;

        if (upgradeBtn && uiActionLocked)
        {
            upgradeBtn.interactable = false;
        }

        // === Процент успеха улучшения ===
        if (lvl == 1)
            percentText.text = "Успех улучшения: 50%";
        else if (lvl == 2)
            percentText.text = "Успех улучшения: 25%";
        else if (lvl == 3)
            percentText.text = "Успех улучшения: 10%";
        else if (lvl >= 4)
            percentText.text = "Уровень максимальный";

        // === Название продукта ===
        if (name)
            name.text = $"{product.name} (lvl {lvl})";

        // === Награда ===
        if (rewardText)
        {
            if (lvl < 4)
            {
                float rewardCoin = product.sell_price * 1.5f * lvl;
                rewardText.text = $"+{rewardCoin:0} COIN";
            }
            else
            {
                rewardText.text = $"+{product.sell_price / 100f} TON";
            }
        }

        if (!upgradeBtn) return;

        // === Если требуется «кормление» ===
        if (needEat)
        {
            timerText.gameObject.SetActive(false);

            if (lvl == 4)
            {
                float restoreBezozCost = 50f;
                if (houseId == 2)
                    restoreBezozCost = 75f;
                else if (houseId == 3)
                    restoreBezozCost = 100f;

                upgradeBtn.interactable = !uiActionLocked && (gm.currentUser.bezoz >= restoreBezozCost);
                upgradeBtn.GetComponentInChildren<Text>().text = $"Восстановить ({restoreBezozCost:0} безосов)";
            }
            else
            {
                float restoreCost = Mathf.Max(1f, product.price / 100f);
                upgradeBtn.interactable = !uiActionLocked && (gm.currentUser.coin >= restoreCost);
                upgradeBtn.GetComponentInChildren<Text>().text = $"Восстановить ({restoreCost:0})";
            }

            upgradeBtn.onClick.RemoveAllListeners();
            upgradeBtn.onClick.AddListener(() =>
            {
                upgradeBtn.interactable = false;
                LockUiAction();
                gm.RestoreHouseProductButton(houseId, productId);
                SyncWithGameManager();
            });

            gm.ApplyUserData();
            return;
        }

        // === Если таймер закончился — можно собрать ===
        if (leftSec <= 0)
        {
            timerText.gameObject.SetActive(false);

            upgradeBtn.interactable = !uiActionLocked;
            upgradeBtn.GetComponentInChildren<Text>().text = "Собрать ресурсы";

            upgradeBtn.onClick.RemoveAllListeners();
            upgradeBtn.onClick.AddListener(() =>
            {
                upgradeBtn.interactable = false;
                LockUiAction();
                gm.CollectHouseProductButton(houseId, productId);
                SyncWithGameManager();
            });

            gm.ApplyUserData();
            return;
        }

        // === Обычный режим — апгрейд ===
        float upgradeCost = product.price * (lvl + 1) * 2f; // 💰 SunCoin
        float upgradeBezosCost = product.speed_price * (1.5f + lvl * 0.5f); // ⚡ Безосы

        // Апгрейд на 4 уровень (3 -> 4): особые цены для home2 и home3
        int nextLvl = lvl + 1;
        if (nextLvl == 4)
        {
            if (houseId == 2)
            {
                upgradeCost = 30000f;
                upgradeBezosCost = 250f;
            }
            else if (houseId == 3)
            {
                upgradeCost = 40000f;
                upgradeBezosCost = 350f;
            }
            else
            {
                upgradeBezosCost *= 10f; // для остальных домов (home1, mine, voyage)
            }
        }

        bool canAffordCoins = gm.currentUser.coin >= upgradeCost;
        bool canAffordBezos = gm.currentUser.bezoz >= upgradeBezosCost;
        bool canAfford = canAffordCoins && canAffordBezos;

        timerText.gameObject.SetActive(true);
        upgradeBtn.interactable = !uiActionLocked && canAfford;

        if (lvl >= 4)
        {
            upgradeBtn.interactable = false;
            upgradeBtn.GetComponentInChildren<Text>().text = "MAX";
        }
        else
        {
            upgradeBtn.GetComponentInChildren<Text>().text =
                $"Улучшить ({upgradeCost:0} монет, {upgradeBezosCost:0} безосов)";

            upgradeBtn.onClick.RemoveAllListeners();
            upgradeBtn.onClick.AddListener(() =>
            {
                upgradeBtn.interactable = false; // 🔴 сразу блокируем
                LockUiAction();
                gm.UpgradeProductInHouseButton(houseId, product.id);
                SyncWithGameManager();
            });
        }

        gm.ApplyUserData();
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        if (needEat)
        {
            timerText.text = "--:--";
            return;
        }

        TimeSpan ts = TimeSpan.FromSeconds(Mathf.Max(0, leftSec));
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
