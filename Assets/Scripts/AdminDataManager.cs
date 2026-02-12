using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AdminDataManager : MonoBehaviour
{
    private string BackendUrl => ApiConfig.BaseUrl + "/admindata"; // эндпоинт на бэке

    [Serializable]
    public class AdminDataDto
    {
        public int id;
        public string name;
        public string value;
    }

    [Serializable]
    private class AdminDataListWrapper
    {
        public AdminDataDto[] items;
    }

    // Доступ по id или по name
    private Dictionary<int, AdminDataDto> dataById = new();
    private Dictionary<string, AdminDataDto> dataByName = new();

    public static AdminDataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject); // чтобы менеджер не удалялся при смене сцен
    }

    private void Start()
    {
        StartCoroutine(FetchAllAdminData());
    }

    private IEnumerator FetchAllAdminData()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(BackendUrl))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = "{\"items\":" + req.downloadHandler.text + "}";
                var wrapper = JsonUtility.FromJson<AdminDataListWrapper>(json);

                if (wrapper != null && wrapper.items != null)
                {
                    dataById.Clear();
                    dataByName.Clear();

                    foreach (var entry in wrapper.items)
                    {
                        dataById[entry.id] = entry;
                        dataByName[entry.name] = entry;
                    }

                    Debug.Log($"[AdminData] Загружено {wrapper.items.Length} записей");
                }
            }
            else
            {
                Debug.LogError($"[AdminData] Ошибка GET {req.responseCode} {req.error}");
            }
        }
    }

    // Получить значение по id
    public string GetValueById(int id)
    {
        return dataById.ContainsKey(id) ? dataById[id].value : null;
    }

    // Получить значение по name
    public string GetValueByName(string name)
    {
        return dataByName.ContainsKey(name) ? dataByName[name].value : null;
    }
}
