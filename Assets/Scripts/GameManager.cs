using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UTeleApp;

public class GameManager : MonoBehaviour
{
    [Header("Backends")]
    public string backendUsersUrl = "http://127.0.0.1:8000";
    public string backendProductsUrl = "http://127.0.0.1:8008";

    [Header("User (runtime)")]
    public string userID = "1";
    public string username = "Unknown";
    public string firstName = ""; 
    public float money = 0f;
    public float bezoz = 0f;
    public int lvl = 0;
    public float lvl_up = 0f;
    public string id = "";

    [Header("User Interface")]
    [SerializeField] private Text usernameText;
    [SerializeField] private Text refCountText;
    [SerializeField] public Text moneyText;
    [SerializeField] private Text bezozText;
    [SerializeField] private Text lvlText;
    [SerializeField] private Text lvl_up_Text;
    [SerializeField] private Text id_text;
    [SerializeField] private GameObject waitPanel;

    [Header("Planting UI")]
    public GameObject plantMenuUI;          // панель посадки
    public FarmCell SelectedCell;           // выбранная клетка
    public List<FarmCell> cells = new();    // назначь в инспекторе

    private Coroutine heartbeatCo;

    public GridController GridController;
    
    
    
    public UserDto currentUser;
    public List<ProductDto> allProducts = new();

    [Serializable]
    public class UserDto
    {
        public string id;
        public string name;
        public string firstName;  
        public float ton;
        public float lvl_upgrade;
        public int lvl;
        public float coin;
        public float bezoz;
        public int ref_count;
        public string time_farm;
        public string seed_count;
        public string storage_count;
        public int grid_count = 3;
        public string grid_state = "";
        public string refId;
    }


    [Serializable]
    public class ProductDto
    {
        public int id;
        public string name;
        public float price;
        public float sell_price;
        public float speed_price;
        public int lvl_for_buy;
        public int time;
        public float exp;   // ← новое поле
        public string image_seed_link;
        public string image_ready_link;


    }




    private void Start()
    {
        TelegramWebApp.Ready();
        TelegramWebApp.Expand();

        userID = GetUserIdFromInitData(TelegramWebApp.InitData).ToString();
        username = GetUsernameFromInitData(JsonUtility.ToJson(TelegramWebApp.InitDataUnsafe));
        firstName = GetFirstNameFromInitData(JsonUtility.ToJson(TelegramWebApp.InitDataUnsafe)); // <--- NEW

        foreach (var c in cells)
            if (c != null) c.Init(this);

        heartbeatCo = StartCoroutine(HeartbeatCoroutine());

        if (usernameText) usernameText.text = firstName;

        StartCoroutine(EnsureUserExists());
        foreach (var VARIABLE in allProducts)
        {
            Debug.Log(VARIABLE);
        }
    }

    
    
    
    public static string GetFirstNameFromInitData(string initData)
    {
        try
        {
            TelegramInitData data = JsonUtility.FromJson<TelegramInitData>(initData);
            // Telegram присылает first_name; если пусто — вернём пустую строку
            return data != null && data.user != null && !string.IsNullOrEmpty(data.user.first_name)
                ? data.user.first_name
                : "";
        }
        catch (Exception ex)
        {
            Debug.Log($"Error extracting FirstName: {ex.Message}");
            return "nick";
        }
    }


    public IEnumerator AddLvl(float lvlToAdd)
    {
        currentUser.lvl_upgrade += lvlToAdd;
        



        if (currentUser.lvl_upgrade > 1)
        {
            currentUser.lvl_upgrade--;
            currentUser.lvl++;
            Debug.Log(currentUser.lvl);
            
            StartCoroutine( PatchUserField("lvl_upgrade", currentUser.lvl_upgrade.ToString()));
            yield return PatchUserField("lvl", currentUser.lvl.ToString());
            ApplyUserData();
        }
        else if (currentUser.lvl_upgrade == 1f)
        {
            currentUser.lvl_upgrade = 0;
            currentUser.lvl++;
            
            StartCoroutine( PatchUserField("lvl_upgrade", currentUser.lvl_upgrade.ToString()));
            yield return PatchUserField("lvl", currentUser.lvl.ToString());
            ApplyUserData();
        }
        ApplyUserData();

        yield return PatchUserField("lvl_upgrade", currentUser.lvl_upgrade.ToString());
        
        
    }


    [Serializable] private class CellStateEntry { public int key; public int pid; public int left; }
    [Serializable] private class CellStateWrapper { public CellStateEntry[] items; }

    
    // Сбор таймеров по клеткам в JSON (как seed_count/storage_count)
// Собираем состояние клеток: какой продукт и сколько осталось
    public string BuildGridStateJson()
    {
        var entries = new List<CellStateEntry>();
        long now = UnixNow();

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var e = new CellStateEntry { key = i + 1, pid = 0, left = 0 };

            if (cell != null && cell.isBusy)
            {
                // если идёт таймер — считаем остаток; если созрело, сохраняем left=0, pid остаётся
                long left = cell.endUnix > now ? (cell.endUnix - now) : 1;
                e.pid = cell.productId;
                e.left = (int)left;
            }
            entries.Add(e);
        }

        var w = new CellStateWrapper { items = entries.ToArray() };
        return JsonUtility.ToJson(w);
    }



    
    // ---------- PATCH helper ----------
    [Serializable]
    private class PatchBody
    {
        public string field;
        public string value;
        public PatchBody(string f, string v) { field = f; value = v; }
    }

    public IEnumerator PatchUserField(string field, string valueAsString)
    {
        string url = $"{backendUsersUrl}/users/{currentUser.id}";
        var body = new PatchBody(field, valueAsString);
        string json = JsonUtility.ToJson(body);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(url, "PATCH"))
        {
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError($"[PATCH] {field} error: {req.responseCode} {req.error}");
        }
    }

    private IEnumerator HeartbeatCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);
            if (currentUser == null) continue;

            long now = UnixNow();
            yield return PatchUserField("time_farm", now.ToString());

            string stateJson = BuildGridStateJson();
            yield return PatchUserField("grid_state", stateJson);
        }
    }

    
   private void RestoreGridFromServer()
{
    // Проверяем наличие юзера
    if (currentUser == null)
    {
        Debug.LogWarning("[GRID] currentUser == null, жду...");
        StartCoroutine(RetryRestore(1f));
        return;
    }

    // Проверяем наличие продуктов
    if (allProducts == null || allProducts.Count == 0)
    {
        Debug.LogWarning("[GRID] allProducts пустые, жду...");
        StartCoroutine(RetryRestore(1f));
        return;
    }

    // Проверяем JSON
    if (string.IsNullOrEmpty(currentUser.grid_state))
    {
        Debug.LogWarning("[GRID] grid_state пустое у пользователя");
        return;
    }

    string rawState = currentUser.grid_state;

    // Убираем экранирование
    if (rawState.StartsWith("\"") && rawState.EndsWith("\""))
        rawState = rawState.Substring(1, rawState.Length - 2);
    rawState = rawState.Replace("\\\"", "\"");

    Debug.Log("[GRID RAW STATE] " + rawState);

    CellStateWrapper w = null;
    try
    {
        w = JsonUtility.FromJson<CellStateWrapper>(rawState);
    }
    catch (Exception ex)
    {
        Debug.LogError("[GRID PARSE ERROR] " + ex.Message);
        StartCoroutine(RetryRestore(1f));
        return;
    }

    if (w == null || w.items == null)
    {
        Debug.LogWarning("[GRID] JSON распарсился в null, жду...");
        StartCoroutine(RetryRestore(1f));
        return;
    }

    long now = UnixNow();
    long lastFarm = 0;
    long.TryParse(currentUser.time_farm, out lastFarm);
    long delta = now - lastFarm;

    foreach (var e in w.items)
    {
        int idx = e.key - 1;
        if (idx < 0 || idx >= cells.Count) continue;
        var cell = cells[idx];
        if (cell == null) continue;

        if (e.pid > 0 && e.left > 0)
        {
            long newLeft = e.left - delta;
            if (newLeft < 0) newLeft = 1;

            var prod = allProducts.Find(p => p.id == e.pid);

            if (newLeft > 1)
                cell.RestoreFromState(e.pid, now + newLeft, prod);
            else
                cell.RestoreFromState(e.pid, now, prod);

            Debug.Log($"[GRID] Восстановил ячейку {idx + 1}: pid={e.pid}, left={newLeft}");
        }
        else
        {
            cell.ClearToIdle();
        }
    }
}

private IEnumerator RetryRestore(float delay)
{
    yield return new WaitForSeconds(delay);
    RestoreGridFromServer();
}






    private static long UnixNow() =>
        (long)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds;

    // ---------- Посадка ----------
    public IEnumerator PlantInSelectedCell(int productId)
    {
        if (SelectedCell == null) yield break;

        bool ok = false;
        yield return ConsumeSeedOnServer(productId, () => ok = true);
        if (!ok) yield break;

        var prod = allProducts.Find(p => p.id == productId);
        if (prod != null)
        {
            SelectedCell.Plant(prod);
            if (plantMenuUI) plantMenuUI.SetActive(false); // закрыть меню посадки
        }
    }

    // Списать 1 семя (локально + PATCH seed_count)
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

        yield return PatchUserField("seed_count", currentUser.seed_count);
        onOk?.Invoke();
    }

    // Сбор рыбы → storage_count += delta (локально + PATCH storage_count)
    public IEnumerator AddToStorage(int productId, int delta)
    {
        if (currentUser == null) yield break;

        Debug.Log(productId);
        
        StartCoroutine( AddLvl(allProducts[productId-1].exp));

        
        var storage = ParseSeeds(currentUser.storage_count);
        if (!storage.ContainsKey(productId)) storage[productId] = 0;
        storage[productId] = Mathf.Max(0, storage[productId] + delta);
        currentUser.storage_count = ToJson(storage);

        yield return PatchUserField("storage_count", currentUser.storage_count);
    }

    // ---------- Users ----------
  private IEnumerator EnsureUserExists()
{
    string url = $"{backendUsersUrl}/users/{userID}";
    using (UnityWebRequest req = UnityWebRequest.Get(url))
    {
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            string raw = req.downloadHandler.text;
            Debug.Log("[USER RAW JSON] " + raw);

            currentUser = JsonUtility.FromJson<UserDto>(raw);
            StartCoroutine(FetchAllProducts());
            // ⚡ фикс: иногда storage_count или seed_count приходят как "0"/"null"
            if (string.IsNullOrEmpty(currentUser.storage_count) 
                || currentUser.storage_count == "0" 
                || currentUser.storage_count == "null")
            {
                currentUser.storage_count = "{\"items\":[]}";
            }

            if (string.IsNullOrEmpty(currentUser.seed_count) 
                || currentUser.seed_count == "0" 
                || currentUser.seed_count == "null")
            {
                currentUser.seed_count = "{\"items\":[]}";
            }

            // ⚡ фикс: grid_state
            if (string.IsNullOrEmpty(currentUser.grid_state))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    raw, "\"grid_state\"\\s*:\\s*\"(.*?)\""
                );
                if (match.Success)
                {
                    string fixedState = match.Groups[1].Value;
                    fixedState = fixedState.Replace("\\\"", "\"");
                    currentUser.grid_state = fixedState;
                    Debug.Log("[GRID FIXED] " + currentUser.grid_state);
                }
                else
                {
                    Debug.LogWarning("[GRID] grid_state не найден в RAW JSON");
                }
            }

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
            firstName = firstName,   // <--- NEW
            ton = 0,
            lvl_upgrade = 0,
            lvl = 1,
            coin = 100,
            bezoz = 10,
            ref_count = 0,
            time_farm = "",
            seed_count = "",
            storage_count = "",
            grid_count = 3,
            grid_state = "",
            refId = ""
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        bool success = false;
        while (!success)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
                {
                    Debug.Log("[UsersAPI] User created OK");
                    currentUser = JsonUtility.FromJson<UserDto>(req.downloadHandler.text);
                    ApplyUserData();
                    waitPanel.SetActive(false);
                    success = true;
                }
                else
                {
                    Debug.LogError($"[UsersAPI] Ошибка POST {req.responseCode} {req.error}, retrying...");
                    yield return new WaitForSeconds(2f);
                }
            }
        }
    }

    public void ApplyUserData()
    {
        if (currentUser == null) return;
        money = currentUser.coin;
        bezoz = currentUser.bezoz;
        lvl = currentUser.lvl;
        lvl_up = currentUser.lvl_upgrade;

        if (usernameText) usernameText.text = currentUser.firstName;
        if (moneyText) moneyText.text = money.ToString(CultureInfo.InvariantCulture);
        if (bezozText) bezozText.text = bezoz.ToString(CultureInfo.InvariantCulture);
        if (lvlText) lvlText.text = lvl + " lvl";
        if (lvl_up_Text) lvl_up_Text.text = lvl_up.ToString(CultureInfo.InvariantCulture);
        if (id_text) id_text.text = userID;
        if (refCountText) refCountText.text = currentUser.ref_count.ToString();
        
        
        GridController.StartGrid();
    }

    // ---------- Products ----------
    public IEnumerator FetchAllProducts()
    {
        string url = $"{backendProductsUrl}/products.php";
        bool success = false;

        while (!success)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[ProductsAPI] Products loaded OK");
                    string json = "{\"items\":" + req.downloadHandler.text + "}";
                    ProductListWrapper wrapper = JsonUtility.FromJson<ProductListWrapper>(json);
Debug.Log(req.downloadHandler.text);
                    if (wrapper != null && wrapper.items != null)
                    {
                        allProducts = new List<ProductDto>(wrapper.items);
                        Debug.Log(allProducts[0].exp);
                        RestoreGridFromServer();
                        waitPanel.SetActive(false);
                        success = true;
                        
                        
                        foreach (var VARIABLE in allProducts)
                        {
                            Debug.Log(VARIABLE.exp);
                        }
                        

                    }
                    else
                    {
                        Debug.LogError("[ProductsAPI] Parse error, retrying...");
                        waitPanel.SetActive(false);

                        yield return new WaitForSeconds(2f);
                    }
                }
                else
                {
                    Debug.LogError($"[ProductsAPI] Ошибка GET {req.responseCode} {req.error}, retrying...");
                    waitPanel.SetActive(false);

                    yield return new WaitForSeconds(2f);
                }
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

    // ---------- Покупка семян ----------
    public IEnumerator BuySeedCoroutine(ShopItemScript.ProductDto product)
    {
        if (currentUser == null) yield break;

        if (currentUser.coin < product.price)
        {
            Debug.Log("Недостаточно монет!");
            yield break;
        }

        // локально списываем монеты
        currentUser.coin -= product.price;

        // локально обновляем seeds
        Dictionary<int, int> seeds = ParseSeeds(currentUser.seed_count);
        if (!seeds.ContainsKey(product.id)) seeds[product.id] = 0;
        seeds[product.id]++;
        currentUser.seed_count = ToJson(seeds);

        // два PATCH: coin и seed_count (чтобы ничего не перетирать)
        yield return PatchUserField("coin", currentUser.coin.ToString(CultureInfo.InvariantCulture));
        yield return PatchUserField("seed_count", currentUser.seed_count);

        ApplyUserData();
    }

    // ---------- Helpers ----------
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

    public static string GetFPFromInitData(string initData)
    {
        try
        {
            TelegramInitData data = JsonUtility.FromJson<TelegramInitData>(initData);
            return !string.IsNullOrEmpty(data.user.first_name) ? data.user.first_name : "Unknown";
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

    [Serializable]
    private class UserUpdateWrapper
    {
        public float coin;
        public string seed_count;
        public string storage_count;
    }

    // общий парсер key/value JSON (подходит и для seed_count, и для storage_count)
    public Dictionary<int, int> ParseSeeds(string json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<int, int>();

        string s = json.Trim();
        if (s == "0" || s == "null" || s == "\"0\"" || s == "\"null\"")
            return new Dictionary<int, int>();

        try
        {
            var w = JsonUtility.FromJson<SeedWrapper>(s);
            return w != null ? w.ToDict() : new Dictionary<int, int>();
        }
        catch
        {
            Debug.LogWarning("[ParseSeeds] Некорректный JSON: " + json);
            return new Dictionary<int, int>();
        }
    }


    [Serializable]
    private class SeedWrapper
    {
        public SeedEntry[] items;
        public Dictionary<int, int> ToDict()
        {
            var dict = new Dictionary<int, int>();
            if (items == null) return dict;
            foreach (var e in items) dict[e.key] = e.value;
            return dict;
        }
    }

    [Serializable] private class SeedEntry { public int key; public int value; }

    public string ToJson(Dictionary<int, int> dict)
    {
        var w = new SeedWrapper();
        var list = new List<SeedEntry>();
        foreach (var kv in dict) list.Add(new SeedEntry { key = kv.Key, value = kv.Value });
        w.items = list.ToArray();
        return JsonUtility.ToJson(w);
    }
}
