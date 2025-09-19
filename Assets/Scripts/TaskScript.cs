using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
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

    public string reward; // текст награды
    public string task;   // текст условия

    public enum WhatRewardMoney { MONEY, BEZOZ, LVL, TON }
    public enum WhatTask        { MONEY, BEZOZ, LVL, CELL }

    private string PlayerPrefsKey => $"task_done_{taskId}";

    private void Awake()
    {
        if (rewardText) rewardText.text = reward;
        if (taskText)   taskText.text   = task;

        if (IsAlreadyCompleted())
        {
            Destroy(gameObject);
            return;
        }

        if (IsConditionMet())
        {
            // если условие уже выполнено — можно сразу забирать, но обычно ждём нажатие
            // MarkCompleted(); Destroy(gameObject);
        }
    }

    /// Назначь на кнопку «Получить/Проверить».
    public void Check()
    {
        if (!gm || IsAlreadyCompleted()) { Destroy(gameObject); return; }
        if (!IsConditionMet()) return;

        StartCoroutine(ClaimRewardCoroutine());
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
            case WhatTask.MONEY: return gm.money >= countToCheck;
            case WhatTask.BEZOZ: return gm.bezoz >= countToCheck;
            case WhatTask.LVL:   return gm.lvl   >= countToCheck;
            case WhatTask.CELL:  return gm.currentUser != null && gm.currentUser.grid_count >= countToCheck;
            default:             return false;
        }
    }
}
