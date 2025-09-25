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
    public float amount;
    public int isPremium;
}


[Serializable]
public class AdminDataDto
{
    public int id;
    public string name;
    public string value;
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
    public Text BuyBtnText;
    public Button BuyBtn;
    public float premPrice;

    public void Onbgtt()
    {
     
        StartCoroutine(GetRewardsFromServer());
        premPrice = float.Parse( AdminDataManager.Instance.GetValueById(4));
        BuyBtnText.text = "Купить премиум за " + premPrice;

    }

    // private void OnEnable()
    // {
    //     UpdateUI();
    // }

    IEnumerator GetRewardsFromServer()
    {
// Удаляем все дочерние объекты из parent
        foreach (Transform child in parent.transform)
        {
            Destroy(child.gameObject);
        }
        
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
            if (a.rewardCount != null) a.rewardCount.text = reward.amount.ToString();
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
        
        


        if (gm.currentUser.ton < premPrice )
        {
            BuyBtn.interactable = false;
            BuyBtnText.text = $"Нужно {premPrice} TON";
        }
        else if (gm.currentUser.isPremium == 1)
        {
            BuyBtn.interactable = false;
            BuyBtnText.text = $"Премиум куплен";
        }
        else
        {
            BuyBtn.interactable = true;
            BuyBtnText.text = "Купить премиум за " + premPrice;
        }
    }


    public void GiveReward(int lvl)
    {
        if (gm.currentUser.isPremium == 1)
        {
            //index >= 0 && index < myArray.Length
            if (lvl-1 >= 0 && lvl-1 < normalRewards.Count)
            {
                if (normalRewards[lvl - 1].type == "coin")
                {
                    gm.currentUser.coin += normalRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));

                }
                else if (normalRewards[lvl - 1].type == "bezoz")
                {
                    gm.currentUser.bezoz += normalRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));

                }
                else if (normalRewards[lvl - 1].type == "ton")
                {
                    gm.currentUser.ton +=  normalRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString()));
                }
            }

            if (lvl-1 >= 0 && lvl-1 < premiumRewards.Count)
            {
                if (premiumRewards[lvl - 1].type == "coin")
                {
                    gm.currentUser.coin += premiumRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));

                }
                else if (premiumRewards[lvl - 1].type == "bezoz")
                {
                    gm.currentUser.bezoz += premiumRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));

                }
                else if (premiumRewards[lvl - 1].type == "ton")
                {
                    gm.currentUser.ton += premiumRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString()));
                }
            }
        }
        else
        {
            if (lvl-1 >= 0 && lvl-1 < premiumRewards.Count)
            {
                if (normalRewards[lvl - 1].type == "coin")
                {
                    gm.currentUser.coin += normalRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));

                }
                else if (normalRewards[lvl - 1].type == "bezoz")
                {
                    gm.currentUser.bezoz += normalRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));

                }
                else if (normalRewards[lvl - 1].type == "ton")
                {
                    gm.currentUser.ton += normalRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString()));
                }
            }
        }
        
        gm.ApplyUserData();
        UpdateUI();
    }
    
     public void GiveRewardPremium(int lvl)
    {
       
            


            if (lvl-1 >= 0 && lvl-1 < premiumRewards.Count)
            {
                Debug.Log("BUY PREM" + lvl);
                if (premiumRewards[lvl - 1].type == "coin")
                {
                    gm.currentUser.coin += premiumRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString()));

                }
                else if (premiumRewards[lvl - 1].type == "bezoz")
                {
                    gm.currentUser.bezoz += premiumRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString()));

                }
                else if (premiumRewards[lvl - 1].type == "ton")
                {
                    gm.currentUser.ton += premiumRewards[lvl - 1].amount;
                    gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString()));
                }
            }
        
      
        
        
        gm.ApplyUserData();
        UpdateUI();
    }



    public void BuyPremium()
    {
        if(gm.currentUser.ton < premPrice) return;

        gm.currentUser.ton -= premPrice;
        gm.currentUser.isPremium = 1;
        
        gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString()));
        gm.StartCoroutine(gm.PatchUserField("isPremium", gm.currentUser.isPremium.ToString()));
        for (int i = 1; i <= gm.currentUser.lvl; i++)
        {
            GiveRewardPremium(i);
        }
    }











}
