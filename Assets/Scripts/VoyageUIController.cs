using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VoyageUIController : MonoBehaviour
{
    [Header("Links")]
    public GameManager gm;
    public int voyageId = 5; // id похода
    public Text headerTitle;
    public Button startWithCoinBtn;
    public Button startWithBezozBtn;
    public Button startWithTonBtn;
    public Text timerText;
    public Text rewardText;
    public Image rewardPanel;
    public Button collectBtn;
    public Text chanceText; // 👈 новый текст для отображения шансов

    [Header("Runtime")]
    private GameManager.ProductDto voyageProduct;
    private int leftSec;
    private bool isVoyaging;
    private string activeCurrency;
    private float acc;
    private bool isCollecting;
    private bool isStarting;

    [SerializeField] private Text count1;
    [SerializeField] private Text count2;
    [SerializeField] private Text count3;

    private void Start()
    {
        if (headerTitle) headerTitle.text = "Поход";

        if (startWithCoinBtn) startWithCoinBtn.onClick.AddListener(() => StartVoyage("coin"));
        if (startWithBezozBtn) startWithBezozBtn.onClick.AddListener(() => StartVoyage("bezoz"));
        if (startWithTonBtn) startWithTonBtn.onClick.AddListener(() => StartVoyage("ton"));

        if (collectBtn)
        {
            collectBtn.onClick.AddListener(() => StartCoroutine(CollectVoyageReward()));
            collectBtn.gameObject.SetActive(false);
        }
    }

    private IEnumerator CollectVoyageReward()
    {
        if (isCollecting) yield break;
        isCollecting = true;
        if (collectBtn) collectBtn.interactable = false;

        yield return StartCoroutine(VoyagePayout());
        StopVoyage();
        if (collectBtn) collectBtn.gameObject.SetActive(false);

        isCollecting = false;
    }

    private void OnEnable() => SyncFromJson();
    public void InitAfterUserLoaded() => SyncFromJson();

    private void Update()
    {
        if (!isVoyaging) return;

        acc += Time.deltaTime;
        if (acc >= 1f)
        {
            int sec = Mathf.FloorToInt(acc);
            acc -= sec;
            leftSec -= sec;
            if (leftSec < 0) leftSec = 0;
            UpdateTimerText();

            if (leftSec <= 0)
            {
                leftSec = 1;
                UpdateTimerText();
                StartCoroutine(ShowCollectBtnAfterDelay());
                isVoyaging = false;
            }
        }
    }

    private IEnumerator ShowCollectBtnAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        if (timerText) timerText.gameObject.SetActive(false);
        if (collectBtn) collectBtn.gameObject.SetActive(true);
    }

    private void SyncFromJson()
    {
        if (gm == null || gm.currentUser == null) return;

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var voyage = houses?.items.Find(x => x.id == voyageId);
        if (voyage == null) return;

        if (voyage.timers != null && voyage.timers.Count > 0)
        {
            var t = voyage.timers[0];
            if (gm.productById.TryGetValue(t.pid, out var p))
            {
                voyageProduct = p;
                leftSec = t.left;
                activeCurrency = t.currency;
                isVoyaging = true;
                SetButtonsInteractable(false);
                UpdateTimerText();
                gm.ApplyUserData();

                // 👇 показываем шансы при активном походе
                if (chanceText)
                {
                    if (activeCurrency == "coin")
                        chanceText.text = "🎲 Шансы награды:\nМонеты 90% / BEZOZ 10%";
                    else if (activeCurrency == "bezoz")
                        chanceText.text = "🎲 Шансы награды:\nМонеты 70% / BEZOZ 30%";
                    else if (activeCurrency == "ton")
                        chanceText.text = "🎲 Шансы награды:\nМонеты 50% / BEZOZ 35% / TON 15%";
                }
            }
        }

        else
        {
            isVoyaging = false;
            timerText.text = "--:--:--";
            SetButtonsInteractable(true);
        }

        voyageProduct = gm.voyageProducts.Count > 0 ? gm.voyageProducts[0] : null;

        if (voyageProduct != null)
        {
            count1.text = "Поход за " + voyageProduct.price;
            count2.text = "Поход за " + (voyageProduct.price / 100);
            count3.text = "Поход за " + (voyageProduct.price / 1000f).ToString("F2");
        }
    }

    private void StartVoyage(string currency)
    {
        if (gm == null || gm.currentUser == null) return;

        if (isStarting) return;
        isStarting = true;
        SetButtonsInteractable(false);

        voyageProduct = gm.voyageProducts.Count > 0 ? gm.voyageProducts[0] : null;
        if (voyageProduct == null)
        {
            Debug.LogError("[VOYAGE] Нет продуктов типа voyage");
            isStarting = false;
            SetButtonsInteractable(true);
            return;
        }

        // Проверка валюты
        if (currency == "coin" && gm.currentUser.coin < voyageProduct.price) { Debug.Log("Нет монет"); isStarting = false; SetButtonsInteractable(true); return; }
        if (currency == "bezoz" && gm.currentUser.bezoz < voyageProduct.price / 100) { Debug.Log("Нет BEZOZ"); isStarting = false; SetButtonsInteractable(true); return; }
        if (currency == "ton" && gm.currentUser.ton < voyageProduct.price / 1000) { Debug.Log("Нет TON"); isStarting = false; SetButtonsInteractable(true); return; }

        // Списание
        if (currency == "coin") gm.currentUser.coin -= voyageProduct.price;
        if (currency == "bezoz") gm.currentUser.bezoz -= voyageProduct.price / 100;
        if (currency == "ton") gm.currentUser.ton -= voyageProduct.price / 1000;

        gm.StartCoroutine(gm.PatchUserField(currency,
            (currency == "ton" ? gm.currentUser.ton.ToString("F2") :
            (currency == "coin" ? gm.currentUser.coin.ToString() : gm.currentUser.bezoz.ToString()))));

        // Настройка таймера
        leftSec = voyageProduct.time;
        activeCurrency = currency;
        isVoyaging = true;

        // Отображаем шансы
        if (chanceText)
        {
            if (currency == "coin")
                chanceText.text = "🎲 Шансы награды:\nМонеты 90% / BEZOZ 10%";
            else if (currency == "bezoz")
                chanceText.text = "🎲 Шансы награды:\nМонеты 70% / BEZOZ 30%";
            else if (currency == "ton")
                chanceText.text = "🎲 Шансы награды:\nМонеты 50% / BEZOZ 35% / TON 15%";
        }

        // Обновляем данные домов
        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var voyage = houses.items.Find(x => x.id == voyageId);
        voyage.timers.Clear();
        voyage.timers.Add(new GameManager.HouseTimer { pid = voyageProduct.id, left = leftSec, currency = currency });

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));

        UpdateTimerText();
        gm.ApplyUserData();

        if (timerText) timerText.gameObject.SetActive(true);
        if (collectBtn) collectBtn.gameObject.SetActive(false);

        isStarting = false;
    }

    private void StopVoyage()
    {
        isVoyaging = false;
        SetButtonsInteractable(true);
        timerText.text = "--:--:--";
        timerText.gameObject.SetActive(true);

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var voyage = houses.items.Find(x => x.id == voyageId);
        voyage.timers.Clear();

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));
    }

    private IEnumerator VoyagePayout()
    {
        if (gm == null || gm.currentUser == null || voyageProduct == null) yield break;

        System.Random rnd = new System.Random();
        float roll = (float)rnd.NextDouble() * 100f;
        string rewardType = "";

        // Определяем тип награды по шансам (по тексту на UI)
        if (activeCurrency == "coin")
        {
            if (roll < 90f) rewardType = "coin";
            else rewardType = "bezoz";
        }
        else if (activeCurrency == "bezoz")
        {
            if (roll < 70f) rewardType = "coin";
            else rewardType = "bezoz";
        }
        else if (activeCurrency == "ton")
        {
            if (roll < 50f) rewardType = "coin";
            else if (roll < 85f) rewardType = "bezoz";
            else rewardType = "ton";
        }

        // Выдача награды
        if (rewardType == "coin")
        {
            int minReward = Mathf.CeilToInt(voyageProduct.price);
            int maxReward = Mathf.CeilToInt(voyageProduct.sell_price * 2);
            int reward = rnd.Next(minReward, maxReward + 1);
            gm.currentUser.coin += reward;
            yield return gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));
            StartCoroutine(RewardMenuShow("SunCoin", reward));
        }
        else if (rewardType == "bezoz")
        {
            int reward = rnd.Next(1, Mathf.CeilToInt(voyageProduct.sell_price / 50f));
            gm.currentUser.bezoz += reward;
            yield return gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));
            StartCoroutine(RewardMenuShow("BEZOZ", reward));
        }
        else if (rewardType == "ton")
        {
            float reward = Mathf.Max(0.01f, voyageProduct.sell_price / 500f);
            gm.currentUser.ton += reward;
            yield return gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString("F2")));
            StartCoroutine(RewardMenuShow("TON", reward));
        }

        gm.ApplyUserData();
    }


    private IEnumerator RewardMenuShow(string type, float rew)
    {
        rewardPanel.gameObject.SetActive(true);
        rewardText.text = $"Вы получили: {rew} {type}";
        yield return new WaitForSeconds(2f);
        rewardPanel.gameObject.SetActive(false);
    }

    private void SetButtonsInteractable(bool active)
    {
        if (startWithCoinBtn) startWithCoinBtn.interactable = active;
        if (startWithBezozBtn) startWithBezozBtn.interactable = active;
        if (startWithTonBtn) startWithTonBtn.interactable = active;
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        TimeSpan ts = TimeSpan.FromSeconds(leftSec);
        timerText.text = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
