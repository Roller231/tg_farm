using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class ShopItemScript : MonoBehaviour
{
    [Header("UI References")]
    public Text idText;
    public Text nameText;
    public Text priceText;
    public Text sellPriceText;
    public Text speedPriceText;
    public Text lvlForBuyText;
    public Text timeText;
    public Text seedCountText; 

    [Header("Image UI References")]
    public Image seedImage;
    public Image readyImage;

    [Header("Debug Links (optional)")]
    public Text imageSeedLinkText;
    public Text imageReadyLinkText;

    [Header("Buy Button")]
    public Button buyButton;

    private GameManager gameManager;
    private ProductDto product;

    // DTO продукта
    [System.Serializable]
    public class ProductDto
    {
        public int id;
        public string name;
        public float price;
        public float sell_price;
        public float speed_price;
        public int lvl_for_buy;
        public int time;
        public string image_seed_link;
        public string image_ready_link;
    }

    // Назначение продукта + геймменеджера
    public void SetProduct(ProductDto p, GameManager gm)
    {
        product = p;
        gameManager = gm;
        ApplyToUI();

        if (buyButton)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuySeed);
        }
    }

    private void ApplyToUI()
    {
        if (product == null) return;

        if (idText) idText.text = product.id.ToString();
        if (nameText) nameText.text = product.name;
        if (priceText) priceText.text = $"Цена: {product.price}";
        if (sellPriceText) sellPriceText.text = $"Продажа: {product.sell_price}";
        if (speedPriceText) speedPriceText.text = $"Ускорение: {product.speed_price}";
        if (lvlForBuyText) lvlForBuyText.text = $"Нужен уровень: {product.lvl_for_buy}";
        if (timeText) timeText.text = $"Время роста: {product.time} сек.";

        if (imageSeedLinkText) imageSeedLinkText.text = product.image_seed_link;
        if (imageReadyLinkText) imageReadyLinkText.text = product.image_ready_link;

        if (seedImage && !string.IsNullOrEmpty(product.image_seed_link))
            StartCoroutine(LoadImage(product.image_seed_link, seedImage));

        if (readyImage && !string.IsNullOrEmpty(product.image_ready_link))
            StartCoroutine(LoadImage(product.image_ready_link, readyImage));

        // Показываем количество семян у игрока
        if (seedCountText && gameManager != null && gameManager.currentUser != null)
        {
            var seeds = gameManager.ParseSeeds(gameManager.currentUser.seed_count);
            int count = seeds.ContainsKey(product.id) ? seeds[product.id] : 0;
            seedCountText.text = $"Есть: {count}";
        }

        // 🔒 Проверка уровня игрока
        if (buyButton && gameManager != null && gameManager.currentUser != null)
        {
            if (gameManager.currentUser.lvl < product.lvl_for_buy)
            {
                buyButton.interactable = false;
                buyButton.GetComponentInChildren<Text>().text = $"Нужен уровень {product.lvl_for_buy}";
            }
            else
            {
                buyButton.interactable = true;
                buyButton.GetComponentInChildren<Text>().text = "Купить";
            }
        }
    }


    private IEnumerator LoadImage(string url, Image targetImage)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f)
                );
                targetImage.sprite = sprite;
            }
            else
            {
                Debug.LogError($"Ошибка загрузки картинки {url}: {req.error}");
            }
        }
    }

    // ⚡️ Функция покупки семени
    private void BuySeed()
    {
        if (gameManager == null || product == null) return;
        gameManager.StartCoroutine(gameManager.BuySeedCoroutine(product));
        ApplyToUI(); // обновим количество в UI
    }

}
