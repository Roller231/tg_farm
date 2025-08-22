using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UTeleApp;

public class GameManager : MonoBehaviour
{
    [Header("Backends")]
    public string backendUsersUrl = "http://127.0.0.1:8000/";
    public string backendProductsUrl = "http://127.0.0.1:8008/";

    [Header("User (runtime)")]
    public string userID = "1";
    public string username = "Unknown";
    public float money = 0f;
    public float bezoz = 0f;
    public int lvl = 0;
    public float lvl_up = 0f;
    public string id = "";


    [Header("User Interface")]
    [SerializeField] private Text usernameText;
    [SerializeField] private Text refCountText;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text bezozText;
    [SerializeField] private Text lvlText;
    [SerializeField] private Text lvl_up_Text;
    [SerializeField] private Text id_text;
    
    
    
    public FarmCell SelectedCell;       // сюда клетка пишет себя при клике
    public List<FarmCell> cells = new(); // закинь вручную в инспекторе

    private Coroutine heartbeatCo;

    // ---------------- DTO ----------------
    [Serializable]
    public class UserDto
    {
        public string id;
        public string name;
        public float ton;
        public float lvl_upgrade;
        public int lvl;
        public float coin;
        public float bezoz;
        public int ref_count;
        public string time_farm;
        public string seed_count;
        public string storage_count;
    }

    [Serializable]
    public class ProductDto
    {
        public int id;
        public string name;
        public float price;        // заменили decimal → float
        public float sell_price;   // заменили decimal → float
        public float speed_price;  // заменили decimal → float
        public int lvl_for_buy;
        public int time;
        public string image_seed_link;
        public string image_ready_link;
    }


    public UserDto currentUser;
    public List<ProductDto> allProducts = new();

    private void Awake()
    {
        TelegramWebApp.Ready();
        TelegramWebApp.Expand();

        userID = GetUserIdFromInitData(TelegramWebApp.InitData).ToString();
        username = GetUsernameFromInitData(JsonUtility.ToJson(TelegramWebApp.InitDataUnsafe));

        if (usernameText) usernameText.text = username;

        StartCoroutine(EnsureUserExists());
        StartCoroutine(FetchAllProducts());
        
        
    }
    
    private void Start()
    {
        foreach (var c in cells)
            if (c != null) c.Init(this);

        // раз в 5 секунд шлём текущее время в БД
        heartbeatCo = StartCoroutine(HeartbeatCoroutine());
    }

    private void OnDestroy()
    {
        if (heartbeatCo != null) StopCoroutine(heartbeatCo);
    }
    
    
    private IEnumerator HeartbeatCoroutine()
{
    while (true)
    {
        yield return new WaitForSeconds(5f);
        if (currentUser == null) continue;

        long now = UnixNow();
        string url = $"{backendUsersUrl}/users/{currentUser.id}";
        string json = JsonUtility.ToJson(new HeartbeatBody { time_farm = now.ToString() });
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type","application/json");
            yield return req.SendWebRequest();
            // при желании: лог ошибок
        }
    }
}
[Serializable] private class HeartbeatBody { public string time_farm; }

private static long UnixNow() =>
    (long)(System.DateTime.UtcNow - new System.DateTime(1970,1,1)).TotalSeconds;

// ======= ПОСАДКА: списать 1 семя и посадить в выбранную клетку =======
public IEnumerator PlantInSelectedCell(int productId)
{
    if (SelectedCell == null) yield break;

    // уменьшаем семена (без монет)
    bool ok = false;
    yield return ConsumeSeedOnServer(productId, () => ok = true);
    if (!ok) yield break;

    // запускаем рост
    var prod = allProducts.Find(p => p.id == productId);
    if (prod != null) SelectedCell.Plant(prod);
}

// уменьшение seed_count на сервере: -1 для productId
public IEnumerator ConsumeSeedOnServer(int productId, System.Action onOk)
{
    if (currentUser == null) yield break;

    var seeds = ParseSeeds(currentUser.seed_count);
    if (!seeds.ContainsKey(productId) || seeds[productId] <= 0)
    {
        Debug.Log("Нет семян");
        yield break;
    }
    seeds[productId] = Mathf.Max(0, seeds[productId] - 1);
    currentUser.seed_count = ToJson(seeds);

    string url = $"{backendUsersUrl}/users/{currentUser.id}";
    string json = JsonUtility.ToJson(new UserUpdateWrapper { seed_count = currentUser.seed_count });
    byte[] body = Encoding.UTF8.GetBytes(json);

    using (var req = new UnityWebRequest(url, "PUT"))
    {
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke();
        else Debug.LogError($"[Plant] PUT seed_count error: {req.responseCode} {req.error}");
    }
}

// ======= ДОБАВИТЬ В СКЛАД (storage_count += delta по id) =======
public IEnumerator AddToStorage(int productId, int delta)
{
    if (currentUser == null) yield break;

    var storage = ParseSeeds(currentUser.storage_count); // тот же формат key/value
    if (!storage.ContainsKey(productId)) storage[productId] = 0;
    storage[productId] = Mathf.Max(0, storage[productId] + delta);
    currentUser.storage_count = ToJson(storage);

    string url = $"{backendUsersUrl}/users/{currentUser.id}";
    string json = JsonUtility.ToJson(new UserUpdateWrapper { storage_count = currentUser.storage_count });
    byte[] body = Encoding.UTF8.GetBytes(json);

    using (var req = new UnityWebRequest(url, "PUT"))
    {
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[Storage] PUT error: {req.responseCode} {req.error}");
    }
}


    
    
    // ---------------- Users ----------------
    private IEnumerator EnsureUserExists()
    {
        string url = $"{backendUsersUrl}/users/{userID}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                currentUser = JsonUtility.FromJson<UserDto>(req.downloadHandler.text);
                ApplyUserData();
            }
            else if (req.responseCode == 404)
            {
                yield return StartCoroutine(CreateUser());
            }
            else
            {
                Debug.LogError($"[UsersAPI] Ошибка GET {req.responseCode} {req.error}");
            }
        }
    }

    private IEnumerator CreateUser()
    {
        string url = $"{backendUsersUrl}/users";
        var payload = new UserDto
        {
            id = userID,
            name = username,
            ton = 0,
            lvl_upgrade = 0,
            lvl = 1,
            coin = 100,
            bezoz = 10,
            ref_count = 0,
            time_farm = "",
            seed_count = "",
            storage_count = ""
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
            {
                currentUser = JsonUtility.FromJson<UserDto>(req.downloadHandler.text);
                Debug.Log(currentUser.id);
                ApplyUserData();
            }
            else
            {
                Debug.LogError($"[UsersAPI] Ошибка POST {req.responseCode} {req.error}");
            }
        }
    }

    private void ApplyUserData()
    {

        if (currentUser == null) return;
        money = (float)currentUser.coin;
        bezoz = (float)currentUser.bezoz;
        lvl = currentUser.lvl;
        lvl_up = currentUser.lvl_upgrade;
        
        if (usernameText) usernameText.text = currentUser.name;
        if (moneyText) moneyText.text = money.ToString();
        if (bezozText) bezozText.text = bezoz.ToString();
        if (lvlText) lvlText.text = lvl.ToString() + " lvl";
        if (lvl_up_Text) lvl_up_Text.text = lvl_up.ToString();
        if (id_text) id_text.text = userID;
        if (refCountText) refCountText.text = currentUser.ref_count.ToString();
    }

    // ---------------- Products ----------------
    public IEnumerator FetchAllProducts()
    {
        string url = $"{backendProductsUrl}/products";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[ProductsAPI] Raw JSON: " + req.downloadHandler.text);

                string json = "{\"items\":" + req.downloadHandler.text + "}";
                ProductListWrapper wrapper = JsonUtility.FromJson<ProductListWrapper>(json);

                if (wrapper != null && wrapper.items != null)
                {
                    allProducts = new List<ProductDto>(wrapper.items);
                    Debug.Log($"[ProductsAPI] Загружено {allProducts.Count} продуктов");

                    foreach (var prod in allProducts)
                    {
                        Debug.Log($"Product {prod.id}: {prod.name} | Price={prod.price}");
                    }
                }
                else
                {
                    Debug.LogError("[ProductsAPI] Не удалось распарсить продукты");
                }
            }
            else
            {
                Debug.LogError($"[ProductsAPI] Ошибка GET {req.responseCode} {req.error}");
            }
        }
    }


    public IEnumerator GetProduct(int id, Action<ProductDto> callback)
    {
        string url = $"{backendProductsUrl}/products/{id}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var prod = JsonUtility.FromJson<ProductDto>(req.downloadHandler.text);
                callback?.Invoke(prod);
            }
            else
            {
                Debug.LogError($"[ProductsAPI] Ошибка GET/{id} {req.responseCode} {req.error}");
            }
        }
    }

    public IEnumerator CreateProduct(ProductDto product, Action<ProductDto> callback = null)
    {
        string url = $"{backendProductsUrl}/products";
        string json = JsonUtility.ToJson(product);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
            {
                var prod = JsonUtility.FromJson<ProductDto>(req.downloadHandler.text);
                callback?.Invoke(prod);
            }
            else
            {
                Debug.LogError($"[ProductsAPI] Ошибка POST {req.responseCode} {req.error}");
            }
        }
    }

    // ---------------- Helpers ----------------
    [Serializable] private class ProductListWrapper { public ProductDto[] items; }

    public static string GetUsernameFromInitData(string initData)
    {
        try
        {
            TelegramInitData data = JsonUtility.FromJson<TelegramInitData>(initData);
            return !string.IsNullOrEmpty(data.user.username) ? data.user.username : "Unknown";
        }
        catch (Exception ex)
        {
            Debug.Log($"Error extracting Username: {ex.Message}");
            return "Unknown";
        }
    }

    public static long GetUserIdFromInitData(string initData)
    {
        try
        {
            string decodedData = Uri.UnescapeDataString(initData);
            int userStartIndex = decodedData.IndexOf("user={") + 5;
            if (userStartIndex == -1) return -1;
            int userEndIndex = decodedData.IndexOf('}', userStartIndex);
            if (userEndIndex == -1) return -1;

            string userJson = decodedData.Substring(userStartIndex, userEndIndex - userStartIndex + 1);
            string idKey = "\"id\":";
            int idStartIndex = userJson.IndexOf(idKey) + idKey.Length;
            if (idStartIndex == -1) return -1;

            int idEndIndex = userJson.IndexOfAny(new char[] { ',', '}' }, idStartIndex);
            if (idEndIndex == -1) return -1;

            string idString = userJson.Substring(idStartIndex, idEndIndex - idStartIndex).Trim();
            return long.Parse(idString);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    [Serializable] public class TelegramInitData { public TelegramUser user; }
    [Serializable] public class TelegramUser
    {
        public long id;
        public bool is_bot;
        public string first_name;
        public string last_name;
        public string username;
        public string language_code;
        public string photo_url;
    }
    
    
    
    public IEnumerator BuySeedCoroutine(ShopItemScript.ProductDto product)
{
    if (currentUser == null) yield break;

    if (currentUser.coin < (float)product.price)
    {
        Debug.Log("Недостаточно монет!");
        yield break;
    }

    // списываем монеты
    currentUser.coin -= (float)product.price;

    // обновляем семена
    Dictionary<int, int> seeds = ParseSeeds(currentUser.seed_count);
    if (!seeds.ContainsKey(product.id)) seeds[product.id] = 0;
    seeds[product.id]++;
    currentUser.seed_count = ToJson(seeds);

    // отправляем PUT на сервер
    string url = $"{backendUsersUrl}/users/{currentUser.id}";
    string json = JsonUtility.ToJson(new UserUpdateWrapper
    {
        coin = (float)currentUser.coin,
        seed_count = currentUser.seed_count
    });
    byte[] body = Encoding.UTF8.GetBytes(json);

    using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
    {
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Куплено семя {product.name}, всего {seeds[product.id]}");
            ApplyUserData();
        }
        else
        {
            Debug.LogError($"Ошибка покупки: {req.responseCode} {req.error}");
        }
    }
}

[System.Serializable]
private class UserUpdateWrapper
{
    public float coin;
    public string seed_count;
    public string storage_count;
}

// простейший парсер для seed_count JSON
public Dictionary<int, int> ParseSeeds(string json)
{
    if (string.IsNullOrEmpty(json)) return new Dictionary<int, int>();
    return JsonUtility.FromJson<SeedWrapper>(json).ToDict();
}

[System.Serializable]
private class SeedWrapper
{
    public SeedEntry[] items;
    public Dictionary<int, int> ToDict()
    {
        var dict = new Dictionary<int, int>();
        foreach (var entry in items) dict[entry.key] = entry.value;
        return dict;
    }
}

[System.Serializable]
private class SeedEntry
{
    public int key;
    public int value;
}

public string ToJson(Dictionary<int, int> dict)
{
    var wrapper = new SeedWrapper();
    var list = new List<SeedEntry>();
    foreach (var kv in dict)
        list.Add(new SeedEntry { key = kv.Key, value = kv.Value });
    wrapper.items = list.ToArray();
    return JsonUtility.ToJson(wrapper);
}



}
