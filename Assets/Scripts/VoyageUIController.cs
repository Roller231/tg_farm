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

    [Header("Runtime")]
    private GameManager.ProductDto voyageProduct;
    private int leftSec;
    private bool isVoyaging;
    private string activeCurrency; // какая валюта выбрана
    private float acc;

    [SerializeField] private Text count1;
    [SerializeField] private Text count2;
    [SerializeField] private Text count3;

    private void Start()
    {
        if (headerTitle) headerTitle.text = "Поход";

        if (startWithCoinBtn) startWithCoinBtn.onClick.AddListener(() => StartVoyage("coin"));
        if (startWithBezozBtn) startWithBezozBtn.onClick.AddListener(() => StartVoyage("bezoz"));
        if (startWithTonBtn) startWithTonBtn.onClick.AddListener(() => StartVoyage("ton"));
    }

    // 👇 вызывается из GameManager.ApplyUserData()
    public void InitAfterUserLoaded()
    {
        SyncFromJson();
    }

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
                StartCoroutine(VoyagePayout());
                StopVoyage();
            }
        }
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

        voyageProduct = gm.voyageProducts.Count > 0 ? gm.voyageProducts[0] : null;
        if (voyageProduct == null)
        {
            Debug.LogError("[VOYAGE] Нет продуктов типа voyage");
            return;
        }

        // проверка валюты
        if (currency == "coin" && gm.currentUser.coin < voyageProduct.price) { Debug.Log("Нет монет"); return; }
        if (currency == "bezoz" && gm.currentUser.bezoz < voyageProduct.price / 100) { Debug.Log("Нет BEZOZ"); return; }
        if (currency == "ton" && gm.currentUser.ton < voyageProduct.price / 1000) { Debug.Log("Нет TON"); return; }

        // списываем
        if (currency == "coin") gm.currentUser.coin -= voyageProduct.price;
        if (currency == "bezoz") gm.currentUser.bezoz -= voyageProduct.price / 100;
        if (currency == "ton") gm.currentUser.ton -= voyageProduct.price / 1000;

        gm.StartCoroutine(gm.PatchUserField(currency,
            (currency == "ton" ? gm.currentUser.ton.ToString("F2") :
            (currency == "coin" ? gm.currentUser.coin.ToString() : gm.currentUser.bezoz.ToString()))));

        // запуск таймера
        leftSec = voyageProduct.time;
        activeCurrency = currency;
        isVoyaging = true;
        SetButtonsInteractable(false);

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var voyage = houses.items.Find(x => x.id == voyageId);
        voyage.timers.Clear();
        voyage.timers.Add(new GameManager.HouseTimer { pid = voyageProduct.id, left = leftSec, currency = currency });

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));

        UpdateTimerText();
    }

    private void StopVoyage()
    {
        isVoyaging = false;
        SetButtonsInteractable(true);
        timerText.text = "--:--:--";

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

        if (activeCurrency == "coin")
        {
            int rewardCoin = rnd.Next(1, Mathf.CeilToInt(voyageProduct.sell_price * 2));
            gm.currentUser.coin += rewardCoin;
            yield return gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));
            Debug.Log($"[VOYAGE] Получено {rewardCoin} монет за монеты");
        }
        else if (activeCurrency == "bezoz")
        {
            int rewardBezoz = rnd.Next(1, Mathf.CeilToInt(voyageProduct.sell_price / 50f));
            gm.currentUser.bezoz += rewardBezoz;
            yield return gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));
            Debug.Log($"[VOYAGE] Получено {rewardBezoz} BEZOZ за BEZOZ");
        }
        else if (activeCurrency == "ton")
        {
            float rewardTon = Mathf.Max(0.01f, voyageProduct.sell_price / 500f);
            gm.currentUser.ton += rewardTon;
            yield return gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString("F2")));
            Debug.Log($"[VOYAGE] Получено {rewardTon:F2} TON за TON");
        }

        gm.ApplyUserData();
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
