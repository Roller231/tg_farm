using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;

public class PlantMenuItemScript : MonoBehaviour
{
    [Header("UI")]
    public Text idText;
    public Text nameText;
    public Text timeText;
    public Text needLvlText;      // отображаем требуемый уровень
    public Text haveCountText;    // сколько семян есть у игрока
    public Text expText;    // сколько семян есть у игрока
    public Button plantButton;

    [Header("Images (optional)")]
    public Image seedImage;       // можно подать ту же ссылку, что и в магазине

    private GameManager gameManager;
    private ProductDto product;

    [System.Serializable]
    public class ProductDto
    {
        public int id;
        public string name;
        public int time;              // сек
        public float exp;   // ← новое поле
        public int lvl_for_buy;       // требуется для покупки/посадки
        public string image_seed_link;
    }

    /// <summary>
    /// Назначить данные карточке посадки.
    /// </summary>
    public void SetProduct(ProductDto p, GameManager gm)
    {
        product = p;
        gameManager = gm;

        if (plantButton)
        {
            plantButton.onClick.RemoveAllListeners();
            plantButton.onClick.AddListener(OnPlantClicked);
        }

        ApplyUI();

        // (опционально) подтянуть картинку семени
        if (seedImage && !string.IsNullOrEmpty(product.image_seed_link))
            StartCoroutine(LoadImage(product.image_seed_link, seedImage));
    }

    private void ApplyUI()
    {
        if (product == null || gameManager == null || gameManager.currentUser == null) return;

        if (idText) idText.text = product.id.ToString();
        if (nameText) nameText.text = product.name;
        if (timeText) timeText.text = $"Время роста: {product.time} сек.";
        if (needLvlText) needLvlText.text = $"Нужен уровень: {product.lvl_for_buy}";
        Debug.Log(product.exp);
        if (expText) expText.text = $"{product.exp * 100} XP";


        // доступные семена
        var seeds = gameManager.ParseSeeds(gameManager.currentUser.seed_count);
        int have = seeds.ContainsKey(product.id) ? seeds[product.id] : 0;
        if (haveCountText) haveCountText.text = $"Есть: {have}";

        // блокировка кнопки: нет семян или не хватает уровня
        bool levelOk = gameManager.currentUser.lvl >= product.lvl_for_buy;
        bool hasSeeds = have > 0;

        if (plantButton)
        {
            if (!levelOk)
            {
                plantButton.interactable = false;
                plantButton.GetComponentInChildren<Text>().text = $"Нужен уровень {product.lvl_for_buy}";
            }
            else if (!hasSeeds)
            {
                plantButton.interactable = false;
                plantButton.GetComponentInChildren<Text>().text = "Нет семян";
            }
            else
            {
                plantButton.interactable = true;
                plantButton.GetComponentInChildren<Text>().text = "Посадить";
            }
        }
    }

    public GameObject menuRoot; // корневой объект окна посадки

    private void OnPlantClicked()
    {
        if (gameManager == null || product == null) return;
        // посадить в выбранную клетку
        gameManager.StartCoroutine(gameManager.PlantInSelectedCell(product.id));
        // закрыть меню
        if (menuRoot) menuRoot.SetActive(false);
    }


    /// <summary>
    /// Уменьшаем seed_count по productId и сохраняем на бэкенд.
    /// Здесь не трогаем монеты — только инвентарь семян.
    /// </summary>
    private IEnumerator PlantCoroutine(int productId)
    {
        // Парсим текущее состояние
        Dictionary<int, int> seeds = gameManager.ParseSeeds(gameManager.currentUser.seed_count);
        if (!seeds.ContainsKey(productId) || seeds[productId] <= 0)
        {
            Debug.Log("Нет семян для посадки");
            ApplyUI();
            yield break;
        }

        // Списываем одно семя
        seeds[productId] = Mathf.Max(0, seeds[productId] - 1);
        gameManager.currentUser.seed_count = gameManager.ToJson(seeds);

        // (опционально) здесь можешь записать старт времени посадки в time_farm/другую структуру

        // Отправляем на сервер обновлённый seed_count
        string url = $"{gameManager.backendUsersUrl}/users/{gameManager.currentUser.id}";
        var bodyObj = new UserUpdateMinimal { seed_count = gameManager.currentUser.seed_count };
        string json = JsonUtility.ToJson(bodyObj);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Успех: обновим интерфейс карточки
                ApplyUI();
                Debug.Log($"Посадка семени id={productId} выполнена. Осталось: {seeds[productId]}");
            }
            else
            {
                Debug.LogError($"Ошибка сохранения посадки: {req.responseCode} {req.error}");
                // на ошибке откатим локальное уменьшение (чтобы не расходились с сервером)
                seeds[productId] += 1;
                gameManager.currentUser.seed_count = gameManager.ToJson(seeds);
                ApplyUI();
            }
        }
    }

    [System.Serializable]
    private class UserUpdateMinimal
    {
        public string seed_count;
    }

    private IEnumerator LoadImage(string url, Image targetImage)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                targetImage.sprite = sprite;
            }
            else
            {
                Debug.LogError($"Ошибка загрузки картинки {url}: {req.error}");
            }
        }
    }
}
