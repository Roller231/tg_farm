using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UTeleApp;

public class FriendManager : MonoBehaviour
{
    [Tooltip("Полный URL до endpoint-а списка пользователей, например https://example.com/users")]
    private string UsersApiUrl => ApiConfig.BaseUrl + "/users";

    [Header("Current user")]
    [Tooltip("Ваш Telegram ID как строка (должен совпадать с полем refId у рефералов)")]
    public string myTelegramId = "";   // установи извне (из GameManager) перед запуском загрузки
    public string countToAdd;   // установи извне (из GameManager) перед запуском загрузки
    public Text refLink;   // установи извне (из GameManager) перед запуском загрузки

    [Header("Result (runtime)")]
    public List<string> referralUsernames = new();   // сюда положим имена рефералов
    
    
    [Header("UI")]
    public GameObject friendPrefab;    // перфаб друга (на нём висит FriendList)
    public Transform parentContainer;  // объект, куда будут спавниться префабы

    /// <summary>Вызывается после успешной загрузки и фильтрации.</summary>
    public event Action<List<string>> OnReferralsLoaded;

    /// <summary>Вызывается при ошибке загрузки.</summary>
    public event Action<string> OnLoadError;


    private void OnEnable()
    {
        TelegramWebApp.Ready();

        GameManager gm = GameObject.Find("GameManager").GetComponent<GameManager>();

        countToAdd = gm.gameObject.GetComponent<AdminDataManager>().GetValueById(2);
        
       myTelegramId =  gm.userID;
       
       
       refLink.text = gm.gameObject.GetComponent<AdminDataManager>().GetValueById(3) + gm.userID;
       
       
       LoadReferrals();
    }


    [Serializable]
    public class UserDto
    {
        public string id;       // user id (строка)
        public string name;     // username / name
        public string refId;    // чей реферал (tgId пригласившего)
        // Остальные поля нам не нужны для этой задачи, можно не включать
    }

    [Serializable]
    private class UsersWrapper
    {
        public UserDto[] items;
    }

    /// <summary>
    /// Запуск загрузки. Перед вызовом убедись, что myTelegramId уже установлен.
    /// </summary>
    public void LoadReferrals()
    {
        referralUsernames.Clear();
        // перед загрузкой почистим старые префабы
        foreach (Transform child in parentContainer)
            Destroy(child.gameObject);
        StartCoroutine(LoadReferralsCoroutine());
    }

    private IEnumerator LoadReferralsCoroutine()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(UsersApiUrl))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ReferralManager] Ошибка GET {req.responseCode} {req.error}");
                yield break;
            }

            string wrapped = "{\"items\":" + req.downloadHandler.text + "}";
            UsersWrapper wrapper = JsonUtility.FromJson<UsersWrapper>(wrapped);

            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogWarning("[ReferralManager] users пустые");
                yield break;
            }

            string myId = myTelegramId.Trim();
            foreach (var u in wrapper.items)
            {
                string refId = (u.refId ?? "").Trim();
                if (refId == myId)
                {
                    string username = string.IsNullOrEmpty(u.name) ? u.id : u.name;
                    referralUsernames.Add(username);

                    // Спавн перфаба
                    if (friendPrefab && parentContainer)
                    {
                        GameObject go = Instantiate(friendPrefab, parentContainer);
                        FriendList fl = go.GetComponent<FriendList>();
                        if (fl != null && fl.usernameText != null)
                        {
                            fl.usernameText.text = username;
                            fl.rewardText.text = countToAdd;
                        }
                    }
                }
            }

            Debug.Log($"[ReferralManager] Найдено рефералов: {referralUsernames.Count}");
        }
    }
}
