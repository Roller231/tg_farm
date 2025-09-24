using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MineUIController : MonoBehaviour
{
    [Header("Links")]
    public GameManager gm;
    public int mineId = 4; // id шахты
    public Text headerTitle;
    public Button startMiningBtn;
    public Text timerText;

    [Header("Runtime")]
    private GameManager.ProductDto mineProduct; // выбранный продукт для майнинга
    private int leftSec;
    private bool isMining;
    private float acc;
    private float syncAcc; // накопитель для синхронизации

    public void Start()
    {
        if (headerTitle) headerTitle.text = "Шахта";
        if (startMiningBtn)
        {
            startMiningBtn.onClick.RemoveAllListeners();
            startMiningBtn.onClick.AddListener(StartMining);
        }

        SyncFromJson(); // подтягиваем состояние шахты из JSON
    }

    private void Update()
    {
        if (isMining)
        {
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
                    StartCoroutine(MinePayout());
                    StopMining();
                }
            }
        }

        // синхронизация раз в 3 сек
        syncAcc += Time.deltaTime;
        if (syncAcc >= 3f)
        {
            syncAcc = 0f;
            SyncFromJson();
        }
    }

    // Синхронизация с JSON домов
    private void SyncFromJson()
    {
        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var mine = houses?.items.Find(x => x.id == mineId);
        if (mine == null) return;

        if (mine.timers != null && mine.timers.Count > 0)
        {
            var t = mine.timers[0];
            if (gm.productById.TryGetValue(t.pid, out var p))
            {
                mineProduct = p;
                leftSec = t.left;   // 👈 тянем оставшееся время из JSON
                isMining = true;
                startMiningBtn.gameObject.SetActive(false);
                UpdateTimerText();
            }
        }
        else
        {
            isMining = false;
            timerText.text = "--:--:--";
            startMiningBtn.gameObject.SetActive(true);
        }
        
        gm.ApplyUserData();

    }

    // Кнопка: начать майнинг
    private void StartMining()
    {
        if (gm == null || gm.currentUser == null) return;

        mineProduct = gm.mineProducts.Count > 0 ? gm.mineProducts[0] : null;
        if (mineProduct == null)
        {
            Debug.LogError("[MINE] Нет продуктов типа mine");
            return;
        }

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var mine = houses.items.Find(x => x.id == mineId);
        if (mine == null) return;

        // если уже есть таймер → не сбрасываем!
        if (mine.timers != null && mine.timers.Count > 0)
        {
            Debug.Log("[MINE] Уже идёт майнинг");
            return;
        }

        // первый запуск
        leftSec = mineProduct.time;
        isMining = true;
        startMiningBtn.gameObject.SetActive(false);

        mine.timers.Clear();
        mine.timers.Add(new GameManager.HouseTimer { pid = mineProduct.id, left = leftSec });

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));

        UpdateTimerText();
    }

    // Остановка майнинга (по завершению)
    private void StopMining()
    {
        isMining = false;
        startMiningBtn.gameObject.SetActive(true);
        timerText.text = "--:--:--";

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var mine = houses.items.Find(x => x.id == mineId);
        mine.timers.Clear();

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));
    }

    // Выплата награды
    private IEnumerator MinePayout()
    {
        if (gm == null || gm.currentUser == null || mineProduct == null) yield break;

        System.Random rnd = new System.Random();
        int roll = rnd.Next(0, 100);

        if (roll < 80)
        {
            int rewardCoin = rnd.Next(0, Mathf.CeilToInt(mineProduct.sell_price));
            gm.currentUser.coin += rewardCoin;
            yield return gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));
            Debug.Log($"[MINE] Выдано {rewardCoin} монет");
        }
        else
        {
            int rewardBezoz = rnd.Next(0, Mathf.Max(1, Mathf.CeilToInt(mineProduct.sell_price / 100f)));
            gm.currentUser.bezoz += rewardBezoz;
            yield return gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));
            Debug.Log($"[MINE] Выдано {rewardBezoz} BEZOZ");
        }

        gm.ApplyUserData();
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        TimeSpan ts = TimeSpan.FromSeconds(leftSec);
        timerText.text = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
