using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Загружает задания с бэкенда (GET /tasks) и спавнит карточки заданий.
/// Повесь на объект в сцене, назначь prefab и parent.
/// </summary>
public class TaskManager : MonoBehaviour
{
    [Header("Refs")]
    public GameManager gm;

    [Header("UI")]
    [Tooltip("Префаб карточки задания (на нём должен висеть TaskScript)")]
    public GameObject taskPrefab;

    [Tooltip("Родительский контейнер, куда спавнятся карточки")]
    public Transform parentContainer;

    private string TasksUrl => ApiConfig.BaseUrl + "/tasks";

    [Serializable]
    private class TaskDto
    {
        public int id;
        public string title;
        public string description;
        public string type;            // money / bezoz / lvl / cell / channel
        public float count_to_check;
        public string reward_type;     // coin / bezoz / lvl / ton
        public float reward_amount;
        public string channel_url;
        public string channel_id;
    }

    [Serializable]
    private class TaskListWrapper
    {
        public TaskDto[] items;
    }

    private void OnEnable()
    {
        StartCoroutine(LoadAndSpawnTasks());
    }

    public void Refresh()
    {
        StartCoroutine(LoadAndSpawnTasks());
    }

    private IEnumerator LoadAndSpawnTasks()
    {
        // Ждём пока GameManager загрузит пользователя
        while (gm == null || gm.currentUser == null)
            yield return new WaitForSeconds(0.5f);

        // Чистим старые карточки
        if (parentContainer)
        {
            for (int i = parentContainer.childCount - 1; i >= 0; i--)
                Destroy(parentContainer.GetChild(i).gameObject);
        }

        using (UnityWebRequest req = UnityWebRequest.Get(TasksUrl))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[TaskManager] GET error: {req.responseCode} {req.error}");
                yield break;
            }

            string json = "{\"items\":" + req.downloadHandler.text + "}";
            TaskListWrapper wrapper = null;
            try { wrapper = JsonUtility.FromJson<TaskListWrapper>(json); }
            catch (Exception ex) { Debug.LogError("[TaskManager] Parse error: " + ex.Message); }

            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogWarning("[TaskManager] Задания пустые");
                yield break;
            }

            int index = 1;
            foreach (var dto in wrapper.items)
            {
                SpawnTask(dto, index);
                index++;
            }

            Debug.Log($"[TaskManager] Загружено заданий: {wrapper.items.Length}");
        }
    }

    private void SpawnTask(TaskDto dto, int index)
    {
        if (taskPrefab == null || parentContainer == null) return;

        GameObject go = Instantiate(taskPrefab, parentContainer);
        TaskScript ts = go.GetComponent<TaskScript>();
        if (ts == null)
        {
            Debug.LogError("[TaskManager] На префабе нет TaskScript!");
            Destroy(go);
            return;
        }

        var taskType = ParseTaskType(dto.type);
        var rewType = ParseRewardType(dto.reward_type);

        string numberedTitle = $"{index}. {dto.title}";

        ts.Init(
            gameManager: gm,
            id: dto.id,
            title: numberedTitle,
            description: dto.description,
            taskType: taskType,
            check: dto.count_to_check,
            rewType: rewType,
            rewAmount: dto.reward_amount,
            chanUrl: dto.channel_url,
            bTaskId: dto.id
        );
    }

    private static TaskScript.WhatTask ParseTaskType(string s)
    {
        if (string.IsNullOrEmpty(s)) return TaskScript.WhatTask.MONEY;
        switch (s.ToLower())
        {
            case "money": case "coin": return TaskScript.WhatTask.MONEY;
            case "bezoz":              return TaskScript.WhatTask.BEZOZ;
            case "lvl":   case "level":return TaskScript.WhatTask.LVL;
            case "cell":               return TaskScript.WhatTask.CELL;
            case "channel":            return TaskScript.WhatTask.CHANNEL;
            default:                   return TaskScript.WhatTask.MONEY;
        }
    }

    private static TaskScript.WhatRewardMoney ParseRewardType(string s)
    {
        if (string.IsNullOrEmpty(s)) return TaskScript.WhatRewardMoney.MONEY;
        switch (s.ToLower())
        {
            case "coin": case "money": return TaskScript.WhatRewardMoney.MONEY;
            case "bezoz":              return TaskScript.WhatRewardMoney.BEZOZ;
            case "lvl":  case "level": return TaskScript.WhatRewardMoney.LVL;
            case "ton":                return TaskScript.WhatRewardMoney.TON;
            default:                   return TaskScript.WhatRewardMoney.MONEY;
        }
    }
}
