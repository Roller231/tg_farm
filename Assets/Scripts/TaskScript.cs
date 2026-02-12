using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class TaskScript : MonoBehaviour
{
    [Header("Refs")]
    public GameManager gm;

    [Header("Task setup")]
    [Tooltip("Уникальный ID задания для локального сохранения")]
    public string taskId = "task_unique_id";

    public WhatTask whatTask;
    public float countToCheck = 0;

    [Header("Reward setup")]
    public WhatRewardMoney whatReward;
    public float rewardCount = 0;

    [Header("UI")]
    [SerializeField] private Text rewardText;
    [SerializeField] private Text taskText;

    [SerializeField] private Image iconImage;

    [Header("Icons by task type (optional)")]
    [SerializeField] private bool hideIcon;
    [SerializeField] private Sprite iconMoney;
    [SerializeField] private Sprite iconBezoz;
    [SerializeField] private Sprite iconLvl;
    [SerializeField] private Sprite iconCell;
    [SerializeField] private Sprite iconChannel;

    public string reward; // текст награды
    public string task;   // текст условия

    public enum WhatRewardMoney { MONEY, BEZOZ, LVL, TON }
    public enum WhatTask        { MONEY, BEZOZ, LVL, CELL, CHANNEL }

    [Header("Channel (only for CHANNEL type)")]
    public string channelUrl;  // ссылка для пользователя
    public int backendTaskId;  // id задания на бэке (для проверки подписки)

    private string PlayerPrefsKey => $"task_done_{taskId}";

    private bool initialized;
    private bool channelOpenedOnce;

    private void ApplyTextsAndHandleCompleted()
    {
        if (rewardText) rewardText.text = reward;
        if (taskText) taskText.text = task;

        ApplyIconByType();

        if (IsAlreadyCompleted())
        {
            Destroy(gameObject);
        }

    }

    private void ApplyIconByType()
    {
        if (!iconImage) return;
        if (hideIcon)
        {
            iconImage.gameObject.SetActive(false);
            return;
        }

        Sprite target = null;
        switch (whatTask)
        {
            case WhatTask.MONEY: target = iconMoney; break;
            case WhatTask.BEZOZ: target = iconBezoz; break;
            case WhatTask.LVL: target = iconLvl; break;
            case WhatTask.CELL: target = iconCell; break;
            case WhatTask.CHANNEL: target = iconChannel; break;
        }

        if (target)
        {
            iconImage.sprite = target;
            iconImage.gameObject.SetActive(true);
        }
        else
        {
            iconImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Инициализация из кода (TaskManager вызывает после Instantiate).
    /// </summary>
    public void Init(GameManager gameManager, int id, string title, string description,
        WhatTask taskType, float check, WhatRewardMoney rewType, float rewAmount,
        string chanUrl = null, int bTaskId = 0)
    {
        initialized = true;
        gm = gameManager;
        taskId = $"task_srv_{id}";
        whatTask = taskType;
        countToCheck = check;
        whatReward = rewType;
        rewardCount = rewAmount;
        task = title;
        reward = description;
        channelUrl = chanUrl;
        backendTaskId = bTaskId;

        ApplyTextsAndHandleCompleted();
    }

    private void Start()
    {
        if (initialized) return;
        ApplyTextsAndHandleCompleted();
    }

    /// Назначь на кнопку «Получить/Проверить».
    public void Check()
    {
        if (!gm || IsAlreadyCompleted()) { Destroy(gameObject); return; }

        // Для канала — 2 клика: 1) открыть ссылку, 2) выдать награду (без обязательной проверки)
        if (whatTask == WhatTask.CHANNEL)
        {
            if (!channelOpenedOnce)
            {
                channelOpenedOnce = true;
                OpenChannel();
                return;
            }

            StartCoroutine(ClaimRewardCoroutine());
            return;
        }

        if (!IsConditionMet()) return;
        StartCoroutine(ClaimRewardCoroutine());
    }

    /// Открыть ссылку на канал (назначить на кнопку «Перейти»)
    public void OpenChannel()
    {
        if (!string.IsNullOrEmpty(channelUrl))
            Application.OpenURL(channelUrl);
    }

    private IEnumerator CheckChannelAndClaim()
    {
        string url = $"{ApiConfig.BaseUrl}/tasks/check-channel/{backendTaskId}?user_id={gm.userID}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[TASK] Channel check error: {req.error}");
                yield break;
            }

            string json = req.downloadHandler.text;
            // простой парсинг {"subscribed": true/false}
            bool subscribed = json.Contains("true");
            if (!subscribed)
            {
                Debug.Log("[TASK] Пользователь не подписан на канал");
                yield break;
            }
        }

        yield return ClaimRewardCoroutine();
    }

    private IEnumerator ClaimRewardCoroutine()
    {
        // 1) Выдать локально
        bool moneyChanged = false, bezozChanged = false, lvlChanged = false, tonChanged = false;

        switch (whatReward)
        {
            case WhatRewardMoney.MONEY:
                gm.money += rewardCount;
                gm.currentUser.coin = gm.money;
                moneyChanged = true;
                break;

            case WhatRewardMoney.BEZOZ:
                gm.bezoz += rewardCount;
                gm.currentUser.bezoz = gm.bezoz;
                bezozChanged = true;
                break;

            case WhatRewardMoney.LVL:
                {
                    int addLevels = Mathf.Max(0, Mathf.RoundToInt(rewardCount));
                    if (addLevels > 0)
                    {
                        gm.lvl += addLevels;
                        gm.currentUser.lvl = gm.lvl;
                        // сбрасывать/трогать lvl_upgrade не будем
                        lvlChanged = true;
                    }
                }
                break;

            case WhatRewardMoney.TON:
                gm.currentUser.ton += rewardCount;
                tonChanged = true;
                break;
        }

        // 2) Синхронизировать на сервере (последовательно)
        // ВАЖНО: используем InvariantCulture, чтобы точки не превратились в запятые
        if (moneyChanged)
            yield return gm.PatchUserField("coin", gm.currentUser.coin.ToString(CultureInfo.InvariantCulture));

        if (bezozChanged)
            yield return gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString(CultureInfo.InvariantCulture));

        if (lvlChanged)
            yield return gm.PatchUserField("lvl", gm.currentUser.lvl.ToString(CultureInfo.InvariantCulture));

        if (tonChanged)
            yield return gm.PatchUserField("ton", gm.currentUser.ton.ToString(CultureInfo.InvariantCulture));

        // 3) Обновить локальный UI
        gm.ApplyUserData();

        // 4) Пометить выполненным и снести карточку
        MarkCompleted();
        Destroy(gameObject);
    }

    // ---------------- helpers ----------------
    private bool IsAlreadyCompleted() => PlayerPrefs.GetInt(PlayerPrefsKey, 0) == 1;

    private void MarkCompleted()
    {
        PlayerPrefs.SetInt(PlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    private bool IsConditionMet()
    {
        switch (whatTask)
        {
            case WhatTask.MONEY:   return gm.money >= countToCheck;
            case WhatTask.BEZOZ:   return gm.bezoz >= countToCheck;
            case WhatTask.LVL:     return gm.lvl   >= countToCheck;
            case WhatTask.CELL:    return gm.currentUser != null && gm.currentUser.grid_count >= countToCheck;
            case WhatTask.CHANNEL: return false; // проверяется через CheckChannelAndClaim
            default:               return false;
        }
    }
}
