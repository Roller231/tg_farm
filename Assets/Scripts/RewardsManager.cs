using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UTeleApp;

[Serializable]
public class Reward
{
    public int id;
    public int level;
    public string type;
    public string amount;
    public int isPremium;
}

public class RewardsManager : MonoBehaviour
{
    public string rewardsUrl = "http://0.0.0.0:8015/rewards"; // твой FastAPI endpoint

    public List<Reward> premiumRewards = new List<Reward>();
    public List<Reward> normalRewards = new List<Reward>();

    public GameManager gm;

    [Header("Create Prefabs")] public List<Sprite> icons; // 0=coin, 1=bezoz, 2=ton
    public GameObject parent;
    public GameObject prefab;

    void Start()
    {
     
        StartCoroutine(GetRewardsFromServer());
    }

    private void OnEnable()
    {
        UpdateUI();
    }

    IEnumerator GetRewardsFromServer()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(rewardsUrl))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError("Ошибка запроса: " + request.error);
                yield break;
            }

            string json = request.downloadHandler.text;
            Debug.Log("JSON: " + json);

            try
            {
                // Разбираем JSON-массив через JsonHelper
                Reward[] rewards = JsonHelper.FromJson<Reward>(json);

                premiumRewards.Clear();
                normalRewards.Clear();

                foreach (var reward in rewards)
                {
                    if (reward.isPremium == 1)
                        premiumRewards.Add(reward);
                    else
                        normalRewards.Add(reward);
                }

                CreatePrefabs();

                Debug.Log($"Премиум: {premiumRewards.Count}, Обычные: {normalRewards.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError("Ошибка разбора JSON: " + e.Message);
            }
        }
    }

    public void CreatePrefabs()
    {
        // Определяем максимальный уровень среди всех наград
        int maxLevel = 0;
        foreach (var r in normalRewards)
            if (r.level > maxLevel)
                maxLevel = r.level;
        foreach (var r in premiumRewards)
            if (r.level > maxLevel)
                maxLevel = r.level;

        for (int lvl = 1; lvl <= maxLevel; lvl++)
        {
            Reward normal = normalRewards.Find(r => r.level == lvl);
            Reward premium = premiumRewards.Find(r => r.level == lvl);

            // обычная награда или пустышка
            if (normal != null)
                CreatePrefabForReward(normal);
            else
                CreateEmptyPrefab();

            // премиум награда или пустышка
            if (premium != null)
                CreatePrefabForReward(premium);
            else
                CreateEmptyPrefab();
        }
        
        UpdateUI();
    }

    private void CreatePrefabForReward(Reward reward)
    {
        if (reward == null)
        {
            CreateEmptyPrefab();
            return;
        }

        GameObject instance = Instantiate(prefab, parent.transform);
        LvlrewardScriptObj a = instance.GetComponent<LvlrewardScriptObj>();

        if (a != null)
        {
            int iconIndex = reward.type switch
            {
                "coin" => 0,
                "bezoz" => 1,
                "ton" => 2,
                _ => -1
            };

            if (iconIndex >= 0 && iconIndex < icons.Count && a.img != null)
                a.img.sprite = icons[iconIndex];

            if (a.rewardType != null) a.rewardType.text = reward.type;
            if (a.rewardCount != null) a.rewardCount.text = reward.amount;
            if (a.level != null) a.level.text = reward.level.ToString();

            a.isPremium = reward.isPremium; // <-- добавили
            // сохраняем, премиум ли эта награда
            a.rewardIsPremium = reward.isPremium == 1;
        }
    }



    private void CreateEmptyPrefab()
    {
        GameObject instance = Instantiate(prefab, parent.transform);

        // делаем все дочерние объекты неактивными
        foreach (Transform t in instance.transform)
            t.gameObject.SetActive(false);

        // основной объект прозрачный
        var img = instance.GetComponent<Image>();
        if (img != null)
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);

        // помечаем как пустышку
        instance.tag = "EmptyReward";
    }

   



// Вспомогательный класс для разбора массива JSON
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{ \"array\": " + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }


    public void UpdateUI()
    {
        int userLvl = gm.currentUser.lvl;
        bool hasPremium = gm.currentUser.isPremium == 1;

        foreach (Transform child in parent.transform)
        {
            if (child.CompareTag("EmptyReward")) continue;

            LvlrewardScriptObj a = child.GetComponent<LvlrewardScriptObj>();
            if (a == null) continue;

            int rewardLvl = 0;
            int.TryParse(a.level.text, out rewardLvl);

            if (a.galka != null)
            {
                if (rewardLvl <= userLvl)
                {
                    // Галка на премиум ставится только если пользователь премиум
                    if (a.rewardIsPremium)
                        a.galka.SetActive(hasPremium);
                    else
                        a.galka.SetActive(true); // обычная награда всегда галка
                }
                else
                {
                    a.galka.SetActive(false);
                }
            }
        }
    }







    
    
    

}
