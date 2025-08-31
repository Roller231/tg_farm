using System;
using UnityEngine;
using UnityEngine.UI;

public class StorageItemScript : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public Text nameText;
    public Text priceText;
    public Text countText;
    public Button sellBtn;

    [Header("Runtime")]
    private GameManager.ProductDto product;
    private int count;
    private GameManager gm;

    public void Init(GameManager manager, GameManager.ProductDto prod, int amount)
    {
        gm = manager;
        product = prod;
        count = amount;

        // Загружаем картинку
        if (!string.IsNullOrEmpty(prod.image_ready_link))
            StartCoroutine(LoadImage(prod.image_ready_link));

        nameText.text = prod.name;
        priceText.text = $"{prod.sell_price} мон.";
        countText.text = $"x{amount}";

        sellBtn.onClick.RemoveAllListeners();
        sellBtn.onClick.AddListener(OnSellClicked);
    }

    private void OnSellClicked()
    {
        if (count <= 0) return;

        int totalCoins = Mathf.RoundToInt(product.sell_price * count);

        for (int i = 0; i < count; i++)
        {
            StartCoroutine( gm.AddLvl(product.exp));
        }
        
        // Обновляем деньги у игрока
        gm.money += totalCoins;
        gm.currentUser.coin = gm.money;

        // Обнуляем рыбу в storage_count
        var storage = gm.ParseSeeds(gm.currentUser.storage_count);
        storage[product.id] = 0;
        gm.currentUser.storage_count = gm.ToJson(storage);


        
        // Отправляем на сервер
        gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        gm.StartCoroutine(gm.PatchUserField("storage_count", gm.currentUser.storage_count));

        // Обновляем интерфейс
        count = 0;
        countText.text = "x0";
        sellBtn.interactable = false;

        gm.ApplyUserData();
        
        Destroy(gameObject);
    }

    private System.Collections.IEnumerator LoadImage(string url)
    {
        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                if (icon) icon.sprite = sp;
            }
            else
                Debug.LogError($"[StorageItemScript] Ошибка загрузки иконки: {req.error}");
        }
    }
}
