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
    public Button collectRewardBtn; // 👈 новая кнопка "Собрать добычу"
    public Text timerText;

    [Header("Runtime")]
    private GameManager.ProductDto mineProduct; // выбранный продукт для майнинга
    private int leftSec;
    private bool isMining;
    private float acc;
    private float syncAcc; // накопитель для синхронизации
    private bool isRewardReady;
    private bool isCollecting;
    private bool isStarting;

    public void Start()
    {
        if (headerTitle) headerTitle.text = "Шахта";

        if (startMiningBtn)
        {
            startMiningBtn.onClick.RemoveAllListeners();
            startMiningBtn.onClick.AddListener(StartMining);
        }

        if (collectRewardBtn)
        {
            collectRewardBtn.onClick.RemoveAllListeners();
            collectRewardBtn.onClick.AddListener(() => StartCoroutine(CollectReward()));
            collectRewardBtn.gameObject.SetActive(false);
        }

        SyncFromJson();
    }

    private void Update()
    {
        if (isMining && !isRewardReady)
        {
            acc += Time.deltaTime;
            if (acc >= 1f)
            {
                int sec = Mathf.FloorToInt(acc);
                acc -= sec;
                leftSec -= sec;
                if (leftSec < 0) leftSec = 0;
                UpdateTimerText();

                if (leftSec <= 3)
                {
                    // стопаем на 1 сек
                    leftSec = 1;
                    UpdateTimerText();

                    isRewardReady = true;
                    isMining = false;

                    timerText.gameObject.SetActive(false);
                    collectRewardBtn.gameObject.SetActive(true);
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

    private void SyncFromJson()
    {
        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        
        
        var mine = houses?.items.Find(x => x.id == mineId);
        if (mine == null) return;
        startMiningBtn.interactable = true;

        if (mine.timers != null && mine.timers.Count > 0)
        {
            var t = mine.timers[0];
            if (gm.productById.TryGetValue(t.pid, out var p))
            {
                mineProduct = p;
                leftSec = t.left;

                if (leftSec > 3) // ещё идёт добыча
                {
                    isMining = true;
                    isRewardReady = false;

                    startMiningBtn.gameObject.SetActive(false);
                    collectRewardBtn.gameObject.SetActive(false);
                    timerText.gameObject.SetActive(true);

                    UpdateTimerText();
                }
                else // добыча завершена, награда готова
                {
                    isMining = false;
                    isRewardReady = true;

                    startMiningBtn.gameObject.SetActive(false);
                    collectRewardBtn.gameObject.SetActive(true);
                    timerText.gameObject.SetActive(false);
                }

                Debug.Log($"SyncFromJson: isRewardReady={isRewardReady}, isMining={isMining}, leftSec={leftSec}");
            }
        }
        else
        {
            // вообще нет активного таймера
            if (!isRewardReady) 
            {
                isMining = false;
                timerText.text = "--:--:--";
                startMiningBtn.gameObject.SetActive(true);
                collectRewardBtn.gameObject.SetActive(false);
                timerText.gameObject.SetActive(true);

                Debug.Log($"SyncFromJson: no timers, isRewardReady={isRewardReady}");
            }
        }

        gm.ApplyUserData();
    }

    private void StartMining()
    {
        if (gm == null || gm.currentUser == null) return;

        if (isStarting) return;
        isStarting = true;
        if (startMiningBtn) startMiningBtn.interactable = false;

        mineProduct = gm.mineProducts.Count > 0 ? gm.mineProducts[0] : null;
        if (mineProduct == null)
        {
            Debug.LogError("[MINE] Нет продуктов типа mine");
            isStarting = false;
            if (startMiningBtn) startMiningBtn.interactable = true;
            return;
        }

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var mine = houses.items.Find(x => x.id == mineId);
        if (mine == null) return;

        if (mine.timers != null && mine.timers.Count > 0)
        {
            Debug.Log("[MINE] Уже идёт майнинг");
            isStarting = false;
            return;
        }

        leftSec = mineProduct.time;
        isMining = true;
        isRewardReady = false;

        startMiningBtn.gameObject.SetActive(false);
        collectRewardBtn.gameObject.SetActive(false);
        timerText.gameObject.SetActive(true);

        mine.timers.Clear();
        mine.timers.Add(new GameManager.HouseTimer { pid = mineProduct.id, left = leftSec });

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));

        UpdateTimerText();

        isStarting = false;
    }

    private IEnumerator CollectReward()
    {
        if (isCollecting) yield break;
        isCollecting = true;
        if (collectRewardBtn) collectRewardBtn.interactable = false;

        yield return MinePayout();
        StopMining();

        isCollecting = false;
    }

    private void StopMining()
    {
        isMining = false;
        isRewardReady = false;

        startMiningBtn.gameObject.SetActive(true);
        collectRewardBtn.gameObject.SetActive(false);
        timerText.text = "--:--:--";
        timerText.gameObject.SetActive(true);

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        var mine = houses.items.Find(x => x.id == mineId);
        mine.timers.Clear();

        gm.RefreshHousesFromJson(JsonUtility.ToJson(houses));
        gm.StartCoroutine(gm.PatchUserField("houses", gm.currentUser.houses));
    }

    private IEnumerator MinePayout()
    {
        if (gm == null || gm.currentUser == null || mineProduct == null) yield break;

        System.Random rnd = new System.Random();
        int roll = rnd.Next(0, 100);

        if (roll < 80)
        {
            int maxCoinInclusive = Mathf.Max(1, Mathf.CeilToInt(mineProduct.sell_price));
            int rewardCoin = rnd.Next(1, maxCoinInclusive + 1);
            gm.currentUser.coin += rewardCoin;
            yield return gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));
            Debug.Log($"[MINE] Выдано {rewardCoin} монет");
            StartCoroutine(RewardMenuShow("SunCoin", rewardCoin));

        }
        else
        {
            int maxBezozInclusive = Mathf.Max(1, Mathf.CeilToInt(mineProduct.sell_price / 100f));
            int rewardBezoz = rnd.Next(1, maxBezozInclusive + 1);
            gm.currentUser.bezoz += rewardBezoz;
            yield return gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));
            Debug.Log($"[MINE] Выдано {rewardBezoz} BEZOZ");
            StartCoroutine(RewardMenuShow("bezoz", rewardBezoz));

        }

        gm.ApplyUserData();
    }
    
    public Text rewardText;
    public Image rewardPanel;
    
    private IEnumerator RewardMenuShow(string type, float rew)
    {
        
        Debug.Log("dsadasdasd");
        rewardPanel.gameObject.SetActive(true);
        rewardText.text = $"Вы получили: {rew} {type}";

        yield return new WaitForSeconds(2f);
        rewardPanel.gameObject.SetActive(false);

    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        TimeSpan ts = TimeSpan.FromSeconds(leftSec);
        timerText.text = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
