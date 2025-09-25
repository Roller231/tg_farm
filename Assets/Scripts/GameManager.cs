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
    [SerializeField] private Image lvlProgressBar;


    [Header("Planting UI")]
    public GameObject plantMenuUI;
    public FarmCell SelectedCell;
    public List<FarmCell> cells = new();

    private Coroutine heartbeatCo;

    public GridController GridController;

    public UserDto currentUser;

    // --- продукты
    public List<ProductDto> allProducts = new();        // только type == ""
    public List<ProductDto> home1Products = new();      // type == "home1"
    public List<ProductDto> home2Products = new();      // type == "home2"
    public List<ProductDto> home3Products = new();      // type == "home3"
    public List<ProductDto> mineProducts = new();    // type == "mine"
    public List<ProductDto> voyageProducts = new();  // type == "voyage"
    public Dictionary<int, ProductDto> productById = new();
    
    
    

    // ===== DTO =====
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
        public string houses; // JSON домов
        public int isPremium;
    }

    [Serializable]
    public class ProductDto
    {
        public int id;
        public string name;
        public string type;          // "", "home1/2/3"
        public float price;
        public float sell_price;
        public float speed_price;
        public int lvl_for_buy;
        public int time;
        public float exp;
        public string image_seed_link;
        public string image_ready_link;
    }

    // ====== Модель домов (локальная) ======
    [Serializable] 
    public class HouseTimer 
    { 
        public int pid; 
        public int left;  
        public int lvl = 1;  
        public string currency; // "coin", "bezoz", "ton"
    }

    [Serializable] public class House
    {
        public int id;
        public float price;
        public int lvl_for_buy;
        public int build_time;
        public bool active;
        public string type; // "home1" | "home2" | "home3"
        public List<HouseTimer> timers = new();
    }
    [Serializable] public class HousesWrapper { public List<House> items = new(); }

    // ====== Unity ======
    private void Start()
    {
        TelegramWebApp.Ready();
        TelegramWebApp.Expand();

        userID = GetUserIdFromInitData(TelegramWebApp.InitData).ToString();
        username = GetUsernameFromInitData(JsonUtility.ToJson(TelegramWebApp.InitDataUnsafe));
        firstName = GetFirstNameFromInitData(JsonUtility.ToJson(TelegramWebApp.InitDataUnsafe));

        foreach (var c in cells)
            if (c != null) c.Init(this);

        heartbeatCo = StartCoroutine(HeartbeatCoroutine());

        if (usernameText) usernameText.text = firstName;

        StartCoroutine(EnsureUserExists());
        

    }
    
    public void UpgradeProductInHouseButton(int houseId, int productId)
    {
        StartCoroutine(UpgradeProductInHouse(houseId, productId));
    }

    public IEnumerator UpgradeProductInHouse(int houseId, int productId)
    {
        var houses = GetHouses();
        var h = houses.items.Find(x => x.id == houseId);
        if (h == null || !h.active) yield break;

        var timer = h.timers.Find(t => t.pid == productId);
        if (timer == null) yield break;

        if (!productById.TryGetValue(productId, out var p)) yield break;

        // цена улучшения = price * (lvl+1)
        float upgradeCost = p.price * (timer.lvl + 1);

        if (currentUser.coin < upgradeCost)
        {
            Debug.Log("[UPGRADE] Недостаточно монет");
            yield break;
        }

        // списываем монеты
        currentUser.coin -= upgradeCost;
        yield return PatchUserField("coin", currentUser.coin.ToString(CultureInfo.InvariantCulture));

        // повышаем уровень
        timer.lvl++;
        SaveHouses();

        Debug.Log($"[UPGRADE] Продукт {p.name} в доме {houseId} улучшен до {timer.lvl} уровня (цена {upgradeCost})");
        ApplyUserData();
    }


    public void DebugPrintHousesActive()
    {
        var hw = GetHouses();
        if (hw.items == null || hw.items.Count == 0)
        {
            Debug.Log("[HOUSES] пусто");
            return;
        }
        foreach (var h in hw.items)
            Debug.Log($"[HOUSES] Дом {h.id}: active={h.active}");
    }

    // ====== Telegram helpers ======
    public static string GetFirstNameFromInitData(string initData)
    {
        try
        {
            TelegramInitData data = JsonUtility.FromJson<TelegramInitData>(initData);
            return data != null && data.user != null && !string.IsNullOrEmpty(data.user.first_name)
                ? data.user.first_name
                : "";
        }
        catch { return "nick"; }
    }
    
    
    private string TypeForHouseId(int id)
    {
        if (id == 1) return "home1";
        if (id == 2) return "home2";
        if (id == 3) return "home3";
        // fallback, если решишь сделать больше домов
        return $"home{id}";
    }

    public static string GetUsernameFromInitData(string initData)
    {
        try
        {
            TelegramInitData data = JsonUtility.FromJson<TelegramInitData>(initData);
            return !string.IsNullOrEmpty(data.user.username) ? data.user.username : "Unknown";
        }
        catch { return "Unknown"; }
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
        catch { return -1; }
    }

    [Serializable] public class TelegramInitData { public TelegramUser user; }
    [Serializable] public class TelegramUser
    {
        public long id; public bool is_bot; public string first_name; public string last_name;
        public string username; public string language_code; public string photo_url;
    }

    public RewardsManager rewManager;

    // ====== Level / EXP ======
    public IEnumerator AddLvl(float lvlToAdd)
    {
        currentUser.lvl_upgrade += lvlToAdd;

        if (currentUser.lvl_upgrade > 1f)
        {
            while (currentUser.lvl_upgrade > 1f)
            {
                currentUser.lvl_upgrade -= 1f;
                currentUser.lvl++;
                rewManager.GiveReward(currentUser.lvl);
                
            }
            StartCoroutine(PatchUserField("lvl_upgrade", currentUser.lvl_upgrade.ToString(CultureInfo.InvariantCulture)));
            yield return PatchUserField("lvl", currentUser.lvl.ToString());
            ApplyUserData();
        }
        else if (Mathf.Approximately(currentUser.lvl_upgrade, 1f))
        {
            currentUser.lvl_upgrade = 0f;
            currentUser.lvl++;
            rewManager.GiveReward(currentUser.lvl);

            StartCoroutine(PatchUserField("lvl_upgrade", currentUser.lvl_upgrade.ToString(CultureInfo.InvariantCulture)));
            yield return PatchUserField("lvl", currentUser.lvl.ToString());
            ApplyUserData();
        }

        ApplyUserData();
        yield return PatchUserField("lvl_upgrade", currentUser.lvl_upgrade.ToString(CultureInfo.InvariantCulture));
    }

    // ====== PATCH helper ======
    [Serializable]
    private class PatchBody { public string field; public string value; public PatchBody(string f, string v) { field = f; value = v; } }

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

    // ====== Heartbeat (time_farm, grid_state, дома-таймеры) ======
    private float lastTick;
    private IEnumerator HeartbeatCoroutine()
    {
        lastTick = Time.realtimeSinceStartup;
        while (true)
        {
            yield return new WaitForSeconds(2f);
            if (currentUser == null) continue;

            // 1) time_farm + grid_state
            long now = UnixNow();
            yield return PatchUserField("time_farm", now.ToString());
            string stateJson = BuildGridStateJson();
            yield return PatchUserField("grid_state", stateJson);

            // 2) домики — уменьшаем таймеры и выплачиваем
            float nowT = Time.realtimeSinceStartup;
            int delta = Mathf.Max(1, Mathf.RoundToInt(nowT - lastTick));
            lastTick = nowT;

            bool changed = TickHouses(delta);
            if (changed)
            {
                string housesJson = HousesToJson();
                yield return PatchUserField("houses", housesJson);
            }
        }
    }

    // ====== GRID SAVE/RESTORE ======
    [Serializable] private class CellStateEntry { public int key; public int pid; public int left; }
    [Serializable] private class CellStateWrapper { public CellStateEntry[] items; }

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
                long left = cell.endUnix > now ? (cell.endUnix - now) : 1;
                e.pid = cell.productId;
                e.left = (int)left;
            }
            entries.Add(e);
        }

        var w = new CellStateWrapper { items = entries.ToArray() };
        return JsonUtility.ToJson(w);
    }

    private void RestoreGridFromServer()
    {
        if (currentUser == null) { StartCoroutine(RetryRestore(1f)); return; }
        if (allProducts == null || allProducts.Count == 0) { StartCoroutine(RetryRestore(1f)); return; }
        if (string.IsNullOrEmpty(currentUser.grid_state)) return;

        string rawState = currentUser.grid_state;
        if (rawState.StartsWith("\"") && rawState.EndsWith("\""))
            rawState = rawState.Substring(1, rawState.Length - 2);
        rawState = rawState.Replace("\\\"", "\"");

        CellStateWrapper w = null;
        try { w = JsonUtility.FromJson<CellStateWrapper>(rawState); }
        catch { StartCoroutine(RetryRestore(1f)); return; }

        if (w == null || w.items == null) { StartCoroutine(RetryRestore(1f)); return; }

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
            }
            else cell.ClearToIdle();
        }
        
        foreach (var voyage in FindObjectsOfType<VoyageUIController>())
        {
            voyage.InitAfterUserLoaded();
        }
    }

    private IEnumerator RetryRestore(float delay) { yield return new WaitForSeconds(delay); RestoreGridFromServer(); }

    private static long UnixNow() => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

    // ====== Посадка ======
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
            if (plantMenuUI) plantMenuUI.SetActive(false);
        }
    }

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

    public IEnumerator AddToStorage(int productId, int delta)
    {
        if (currentUser == null) yield break;

        StartCoroutine(AddLvl(LookupExp(productId)));

        var storage = ParseSeeds(currentUser.storage_count);
        if (!storage.ContainsKey(productId)) storage[productId] = 0;
        storage[productId] = Mathf.Max(0, storage[productId] + delta);
        currentUser.storage_count = ToJson(storage);

        yield return PatchUserField("storage_count", currentUser.storage_count);
    }

    private float LookupExp(int productId)
    {
        ProductDto p;
        if (productById.TryGetValue(productId, out p)) return p.exp;
        p = allProducts.Find(x => x.id == productId);
        return p != null ? p.exp : 0f;
    }

    // ====== Users ======
    private IEnumerator EnsureUserExists()
    {
        string url = $"{backendUsersUrl}/users/{userID}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string raw = req.downloadHandler.text;
                currentUser = JsonUtility.FromJson<UserDto>(raw);

                // нормализуем поля
                if (string.IsNullOrEmpty(currentUser.storage_count) || currentUser.storage_count == "0" || currentUser.storage_count == "null")
                    currentUser.storage_count = "{\"items\":[]}";
                if (string.IsNullOrEmpty(currentUser.seed_count) || currentUser.seed_count == "0" || currentUser.seed_count == "null")
                    currentUser.seed_count = "{\"items\":[]}";
                if (string.IsNullOrEmpty(currentUser.grid_state))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(raw, "\"grid_state\"\\s*:\\s*\"(.*?)\"");
                    if (match.Success)
                    {
                        string fixedState = match.Groups[1].Value;
                        fixedState = fixedState.Replace("\\\"", "\"");
                        currentUser.grid_state = fixedState;
                    }
                }
                if (string.IsNullOrEmpty(currentUser.houses))
                    currentUser.houses = "{\"items\":[]}";

                ApplyUserData();
                
                
                
                

                // продукты: сначала базовые (type == ""), затем для домов
                yield return StartCoroutine(FetchProductsByType("", list => allProducts = list));
                yield return StartCoroutine(FetchProductsByType("home1", list => home1Products = list));
                yield return StartCoroutine(FetchProductsByType("home2", list => home2Products = list));
                yield return StartCoroutine(FetchProductsByType("home3", list => home3Products = list));
                yield return StartCoroutine(FetchProductsByType("mine", list => mineProducts = list));
                yield return StartCoroutine(FetchProductsByType("voyage", list => voyageProducts = list));


                // заполним map по id
                productById.Clear();
                void AddMap(IEnumerable<ProductDto> lst) { foreach (var p in lst) productById[p.id] = p; }
                AddMap(allProducts); AddMap(home1Products); AddMap(home2Products); AddMap(home3Products);
                AddMap(mineProducts);
                AddMap(voyageProducts);


                // === оффлайн-начисление по домам ДО старта гриды ===
                yield return StartCoroutine(ApplyOfflineProgressHouses());

                RestoreGridFromServer();

                if (waitPanel) waitPanel.SetActive(false);
            }
            else if (req.responseCode == 404)
            {
                yield return StartCoroutine(CreateUser());
            }
            else
            {
                Debug.LogError($"[UsersAPI] GET error {req.responseCode} {req.error}");
            }
        }
    }

    private IEnumerator CreateUser()
    {
string url = $"{backendUsersUrl}/users";
var payload = new UserDto
{
    id = userID, name = username, firstName = firstName,
    ton = 0, lvl_upgrade = 0, lvl = 1,
    coin = 100, bezoz = 10, ref_count = 0,
    time_farm = "", seed_count = "{\"items\":[]}", storage_count = "{\"items\":[]}",
    grid_count = 3, grid_state = "", refId = "",
    houses = "{\"items\":[" +
             // Домики для продуктов
             "{\"id\":1,\"price\":100,\"lvl_for_buy\":1,\"build_time\":3600,\"active\":false,\"type\":\"home1\",\"timers\":[]}," +
             "{\"id\":2,\"price\":500,\"lvl_for_buy\":2,\"build_time\":7200,\"active\":false,\"type\":\"home2\",\"timers\":[]}," +
             "{\"id\":3,\"price\":1000,\"lvl_for_buy\":3,\"build_time\":14400,\"active\":false,\"type\":\"home3\",\"timers\":[]}," +
             // Новый дом 4: шахта
             "{\"id\":4,\"price\":2000,\"lvl_for_buy\":4,\"build_time\":28800,\"active\":true,\"type\":\"mine\",\"timers\":[]}," +
             // Новый дом 5: поход
             "{\"id\":5,\"price\":2500,\"lvl_for_buy\":5,\"build_time\":36000,\"active\":true,\"type\":\"voyage\",\"timers\":[]}" +
             "]}"
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
                    currentUser = JsonUtility.FromJson<UserDto>(req.downloadHandler.text);
                    ApplyUserData();
                    if (waitPanel) waitPanel.SetActive(false);
                    success = true;

                    // подтянуть продукты
                    yield return StartCoroutine(FetchProductsByType("", list => allProducts = list));
                    yield return StartCoroutine(FetchProductsByType("home1", list => home1Products = list));
                    yield return StartCoroutine(FetchProductsByType("home2", list => home2Products = list));
                    yield return StartCoroutine(FetchProductsByType("home3", list => home3Products = list));
                    yield return StartCoroutine(FetchProductsByType("mine", list => mineProducts = list));
                    yield return StartCoroutine(FetchProductsByType("voyage", list => voyageProducts = list));
                    Debug.Log(voyageProducts[0].price);

                    productById.Clear();
                    void AddMap(IEnumerable<ProductDto> lst) { foreach (var p in lst) productById[p.id] = p; }
                    AddMap(allProducts); AddMap(home1Products); AddMap(home2Products); AddMap(home3Products);
                    
                    foreach (var voyage in FindObjectsOfType<VoyageUIController>())
                    {
                        voyage.InitAfterUserLoaded();
                    }
                }
                else
                {
                    Debug.LogError($"[UsersAPI] POST error {req.responseCode} {req.error}, retry...");
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
        if (lvlProgressBar) lvlProgressBar.fillAmount = lvl_up;


        if (GridController) GridController.StartGrid();


        
        //SetCount(1);
    }

    // ====== Products by type ======
    [Serializable] private class ProductListWrapper { public ProductDto[] items; }

    private IEnumerator FetchProductsByType(string type, Action<List<ProductDto>> setter)
    {
        string path = string.IsNullOrEmpty(type) ? "/products/by-type/_empty" : $"/products/by-type/{type}";
        string url = backendProductsUrl + path;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // ответ — JSON-массив; завернём
                string json = "{\"items\":" + req.downloadHandler.text + "}";
                ProductListWrapper wrapper = JsonUtility.FromJson<ProductListWrapper>(json);
                var list = (wrapper != null && wrapper.items != null) ? new List<ProductDto>(wrapper.items) : new List<ProductDto>();
                setter?.Invoke(list);
            }
            else
            {
                Debug.LogError($"[ProductsAPI] GET {type} error {req.responseCode} {req.error}");
                setter?.Invoke(new List<ProductDto>());
            }
        }
    }

    // ====== Покупка семян (без изменений) ======
    public IEnumerator BuySeedCoroutine(ShopItemScript.ProductDto product)
    {
        if (currentUser == null) yield break;

        if (currentUser.coin < product.price)
        {
            Debug.Log("Недостаточно монет!");
            yield break;
        }

        currentUser.coin -= product.price;

        Dictionary<int, int> seeds = ParseSeeds(currentUser.seed_count);
        if (!seeds.ContainsKey(product.id)) seeds[product.id] = 0;
        seeds[product.id]++;
        currentUser.seed_count = ToJson(seeds);

        yield return PatchUserField("coin", currentUser.coin.ToString(CultureInfo.InvariantCulture));
        yield return PatchUserField("seed_count", currentUser.seed_count);

        ApplyUserData();
    }

    // ====== Houses (3 дома) ======
    private HousesWrapper _housesCache;

    private HousesWrapper GetHouses()
    {
        // если пользователь ещё не загрузился — возвращаем пустой контейнер
        if (currentUser == null || string.IsNullOrEmpty(currentUser.houses))
            return new HousesWrapper { items = new List<House>() };

        // если есть актуальный кэш — возвращаем его
        if (_housesCache != null)
            return _housesCache;

        try
        {
            // парсим JSON домов
            _housesCache = JsonUtility.FromJson<HousesWrapper>(currentUser.houses);

            // если JsonUtility вернул null — создаём пустой список
            if (_housesCache == null || _housesCache.items == null)
                _housesCache = new HousesWrapper { items = new List<House>() };

            // нормализация (ставим тип и гарантируем timers != null)
            foreach (var h in _housesCache.items)
            {
                if (string.IsNullOrEmpty(h.type))
                    h.type = TypeForHouseId(h.id);
                if (h.timers == null)
                    h.timers = new List<HouseTimer>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[HOUSES] Ошибка парсинга JSON: {e.Message}\n{currentUser.houses}");
            _housesCache = new HousesWrapper { items = new List<House>() };
        }

        return _housesCache;
    }

    
    public void CheckHousesAndDo(int houseId, Action<House> onActive)
    {
        if (string.IsNullOrEmpty(currentUser.houses))
        {
            Debug.LogError("[HOUSE] currentUser.houses пусто");
            return;
        }

        HousesWrapper wrapper = JsonUtility.FromJson<HousesWrapper>(currentUser.houses);
        if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
        {
            Debug.LogError("[HOUSE] В houses нет домов");
            return;
        }

        var house = wrapper.items.Find(x => x.id == houseId);
        if (house == null)
        {
            Debug.LogError($"[HOUSE] Дом с id={houseId} не найден");
            return;
        }

        if (house.active)
        {
            Debug.Log($"[HOUSE] Дом {houseId} активен ✅");
            onActive?.Invoke(house); // выполнить действие
        }
        else
        {
            Debug.Log($"[HOUSE] Дом {houseId} не активен ❌");
        }
    }

    
    public void SetCount(int houseNumber, Text count, GameObject menuBuy, Button buyBtn)
    {
        if (string.IsNullOrEmpty(currentUser.houses))
        {
            Debug.LogError("[HOUSE] У currentUser.houses пусто");
            return;
        }

        // Прямо парсим houses
        HousesWrapper wrapper = JsonUtility.FromJson<HousesWrapper>(currentUser.houses);
        if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
        {
            Debug.LogError("[HOUSE] В houses нет домов");
            return;
        }

        var he = wrapper.items.Find(x => x.id == houseNumber);
        if (he == null)
        {
            Debug.LogError($"[HOUSE] Дом с id={houseNumber} не найден");
            return;
        }

        // устанавливаем цену
        count.text = he.price.ToString(CultureInfo.InvariantCulture);
        menuBuy.SetActive(true);

        if (money >= he.price )
        {
            buyBtn.interactable = true;
            count.color = Color.white;
        }
        else
        {
            buyBtn.interactable = false;
            count.color = Color.red;
        }

        if (lvl < he.lvl_for_buy)
        {
            buyBtn.interactable = false;
            count.color = Color.red;
            count.text = "Нужен lvl " + he.lvl_for_buy;
        }
        
        else
        {
            buyBtn.interactable = true;
            count.color = Color.white;
        }
        

        Debug.Log($"[HOUSE] Всего домов: {wrapper.items.Count}, выбран id={houseNumber}, цена={he.price}");
    }


    public string HousesToJson()
    {
        if (_housesCache == null)
            _housesCache = new HousesWrapper { items = new List<House>() };

        return JsonUtility.ToJson(_housesCache);
    }

    public void RefreshHousesFromJson(string housesJson)
    {
        currentUser.houses = housesJson;
        _housesCache = null; // сбрасываем кэш, чтобы заново распарсить
    }


    // публичный вызов из UI
    public void BuyHouseButton(int houseId) => StartCoroutine(BuyHouse(houseId));

    // покупка дома: списываем coin, ставим active=true и инициируем таймеры
    public IEnumerator BuyHouse(int houseId)
    {
        var houses = GetHouses();
        var h = houses.items.Find(x => x.id == houseId);
        
        if (h == null) { Debug.LogError($"[HOUSE] не найден id={houseId}"); yield break; }

        if (currentUser.lvl < h.lvl_for_buy) { Debug.Log($"[HOUSE] Нужен уровень {h.lvl_for_buy}"); yield break; }
        if (currentUser.coin < h.price) { Debug.Log($"[HOUSE] Недостаточно монет ({h.price})"); yield break; }
        if (h.active) { Debug.Log("[HOUSE] Уже куплен"); yield break; }

        currentUser.coin -= h.price;
        h.active = true;

        SaveHouses(); // 👈 сразу обновляем JSON в currentUser и сервере
        ApplyUserData();
    }


    private void SaveHouses()
    {
        if (_housesCache == null)
            _housesCache = new HousesWrapper { items = new List<House>() };

        string json = JsonUtility.ToJson(_housesCache);
        currentUser.houses = json;   // обновляем runtime-модель
        StartCoroutine(PatchUserField("houses", json)); // сразу шлём на сервер
        StartCoroutine( PatchUserField("coin", currentUser.coin.ToString(CultureInfo.InvariantCulture)));

    }



    // тикаем таймеры; при достижении 0 — вызываем выплату и перезапускаем
    public bool TickHouses(int deltaSec)
    {
        if (currentUser == null) return false;

        var houses = GetHouses();
        if (houses.items == null || houses.items.Count == 0) return false;

        bool changed = false;

        foreach (var h in houses.items)
        {
            if (!h.active || h.timers == null) continue;

            for (int i = 0; i < h.timers.Count; i++)
            {
                var t = h.timers[i];
                t.left -= deltaSec;

                if (t.left <= 0)
                {
                    if (h.type == "mine")
                    {
                        StartCoroutine(MinePayoutOnce(h, t.pid));
                        h.timers.Clear();
                    }
                    else if (h.type == "voyage")
                    {
                        StartCoroutine(VoyagePayoutOnce(h, t));
                        h.timers.Clear();
                    }

                }

                if (t.left <= 2)
                {
                    if(h.type != "mine" && h.type != "voyage")
                    {
                        Debug.Log(h.price);
                        
                        
                        
                        t.left = 4;
                    }
                }
                else if (t.left <= 0)
                
                    
                
                {
                    if(h.type != "mine" && h.type != "voyage")
                    {
                        StartCoroutine(HousePayout(h.id, t.pid));
                        t.left = GetCycleTimeForProduct(t.pid);
                    }

                }
                
                changed = true;
            }
        }

        if (changed)
        {
            _housesCache = houses;
            SaveHouses();
        }

        return changed;
    }


    public void GiveReward(int pid)
    {        
        var houses = GetHouses();

        foreach (var h in houses.items)
        {
            if (!h.active || h.timers == null) continue;

            for (int i = 0; i < h.timers.Count; i++)
            {
                var t = h.timers[i];

                if (t.pid == pid)
                {
                    if(h.type != "mine" && h.type != "voyage")
                    {
                        StartCoroutine(HousePayout(h.id, t.pid));
                        t.left = GetCycleTimeForProduct(t.pid);
                    }
                }





                
                
            }
        }
    }
    
    private IEnumerator VoyagePayoutOnce(House h, HouseTimer t)
    {
        if (!productById.TryGetValue(t.pid, out var p)) yield break;
        if (string.IsNullOrEmpty(t.currency)) yield break;

        System.Random rnd = new System.Random();

        if (t.currency == "coin")
        {
            int rewardCoin = rnd.Next(1, Mathf.CeilToInt(p.sell_price * 2));
            currentUser.coin += rewardCoin;
            yield return PatchUserField("coin", currentUser.coin.ToString());
            Debug.Log($"[VOYAGE PAYOUT] +{rewardCoin} монет");
        }
        else if (t.currency == "bezoz")
        {
            int rewardBezoz = rnd.Next(1, Mathf.CeilToInt(p.sell_price / 50f));
            currentUser.bezoz += rewardBezoz;
            yield return PatchUserField("bezoz", currentUser.bezoz.ToString());
            Debug.Log($"[VOYAGE PAYOUT] +{rewardBezoz} BEZOZ");
        }
        else if (t.currency == "ton")
        {
            float rewardTon = Mathf.Max(0.01f, p.sell_price / 500f);
            currentUser.ton += rewardTon;
            yield return PatchUserField("ton", currentUser.ton.ToString("F2"));
            Debug.Log($"[VOYAGE PAYOUT] +{rewardTon:F2} TON");
        }

        ApplyUserData();
    }




    private IEnumerator MinePayoutOnce(House h, int productId)
    {
        if (!productById.TryGetValue(productId, out var p)) yield break;

        System.Random rnd = new System.Random();
        int roll = rnd.Next(0, 100);

        if (roll < 80)
        {
            int rewardCoin = rnd.Next(1, Mathf.CeilToInt(p.sell_price));
            currentUser.coin += rewardCoin;
            yield return PatchUserField("coin", currentUser.coin.ToString(CultureInfo.InvariantCulture));
            Debug.Log($"[MINE PAYOUT] +{rewardCoin} монет");
        }
        else
        {
            int rewardBezoz = rnd.Next(1, Mathf.Max(1, Mathf.CeilToInt(p.sell_price / 100f)));
            currentUser.bezoz += rewardBezoz;
            yield return PatchUserField("bezoz", currentUser.bezoz.ToString(CultureInfo.InvariantCulture));
            Debug.Log($"[MINE PAYOUT] +{rewardBezoz} BEZOZ");
        }

        ApplyUserData();
    }








    private int GetCycleTimeForProduct(int pid)
    {
        ProductDto p;
        if (productById.TryGetValue(pid, out p)) return Mathf.Max(1, p.time);
        return 60; // дефолт
    }

    private IEnumerator HousePayout(int houseId, int productId)
    {
        var houses = GetHouses();
        var h = houses.items.Find(x => x.id == houseId);
        if (h == null) yield break;

        var timer = h.timers.Find(t => t.pid == productId);
        if (timer == null) yield break;

        if (!productById.TryGetValue(productId, out var p)) yield break;

        if (timer.lvl < 4)
        {
            // выдаём монеты
            float rewardCoin = p.sell_price * 1.5f * timer.lvl;
            currentUser.coin += rewardCoin;
            yield return PatchUserField("coin", currentUser.coin.ToString(CultureInfo.InvariantCulture));
            Debug.Log($"[PAYOUT] Дом {houseId}, продукт {p.name}, lvl {timer.lvl}: +{rewardCoin} монет");
        }
        else
        {
            // выдаём TON
            float rewardTon = p.sell_price / 100;
            currentUser.ton += rewardTon;
            yield return PatchUserField("ton", currentUser.ton.ToString(CultureInfo.InvariantCulture));
            Debug.Log($"[PAYOUT] Дом {houseId}, продукт {p.name}, lvl {timer.lvl}: +{rewardTon} TON");
        }

        ApplyUserData();
    }




    // Публичный вызов из UI: добавить продукт в дом
    public void AddProductToHouseButton(int houseId, int productId)
    {
        StartCoroutine(AddProductToHouse(houseId, productId));
    }

    public void AddProductToHouseButton(int productId)
    {
        StartCoroutine(AddProductToHouse(1, productId));
    }

    // Добавление продукта в дом (таймер)
    public IEnumerator AddProductToHouse(int houseId, int productId)
    {
        var houses = GetHouses();
        var h = houses.items.Find(x => x.id == houseId);
        if (h == null) { Debug.LogError($"[HOUSE] Дом {houseId} не найден"); yield break; }
        if (!h.active) { Debug.LogError($"[HOUSE] Дом {houseId} не куплен"); yield break; }

        if (!productById.TryGetValue(productId, out var p))
        {
            Debug.LogError($"[HOUSE] Продукт {productId} не найден");
            yield break;
        }

        var houseType = string.IsNullOrEmpty(h.type) ? TypeForHouseId(h.id) : h.type;
        if (!string.Equals(p.type, houseType, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"[HOUSE] Продукт {p.name} не подходит для дома {houseType}");
            yield break;
        }

        if (h.timers == null) h.timers = new List<HouseTimer>();
    
        // Check if product already exists in timers
        var existingTimer = h.timers.Find(t => t.pid == productId);
        if (existingTimer != null)
        {
            Debug.Log($"[HOUSE] Продукт {p.name} уже добавлен в дом {houseId}");
            yield break;
        }

        h.timers.Add(new HouseTimer { pid = productId, left = p.time });
    
        // Update cache and save immediately
        _housesCache = houses;
        SaveHouses();

        Debug.Log($"[HOUSE] В дом {houseId} добавлен продукт {p.name}, таймер: {p.time} сек");
    }






    [Serializable] private class TonResp { public float ton; }

    // ====== Helpers: seed/storage JSON ======
    [Serializable] private class SeedWrapper { public SeedEntry[] items; public Dictionary<int, int> ToDict(){ var d=new Dictionary<int,int>(); if(items==null)return d; foreach(var e in items)d[e.key]=e.value; return d; } }
    [Serializable] private class SeedEntry { public int key; public int value; }

    public Dictionary<int, int> ParseSeeds(string json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<int, int>();
        string s = json.Trim();
        if (s == "0" || s == "null" || s == "\"0\"" || s == "\"null\"") return new Dictionary<int, int>();
        try { var w = JsonUtility.FromJson<SeedWrapper>(s); return w != null ? w.ToDict() : new Dictionary<int, int>(); }
        catch { return new Dictionary<int, int>(); }
    }

    public string ToJson(Dictionary<int, int> dict)
    {
        var w = new SeedWrapper();
        var list = new List<SeedEntry>();
        foreach (var kv in dict) list.Add(new SeedEntry { key = kv.Key, value = kv.Value });
        w.items = list.ToArray();
        return JsonUtility.ToJson(w);
    }

    // ====== ОФФЛАЙН-начисление по домам ======
    private IEnumerator ApplyOfflineProgressHouses()
    {
        // нужен корректный last time_farm
        long lastFarm = 0;
        long now = UnixNow();
        long.TryParse(currentUser.time_farm, out lastFarm);
        long delta = now - lastFarm;

        if (delta <= 0) yield break;

        var houses = GetHouses();
        if (houses.items == null || houses.items.Count == 0) yield break;
        
        bool timersChanged = false;

        foreach (var h in houses.items)
        {
            if (!h.active || h.timers == null || h.timers.Count == 0) continue;

            for (int i = 0; i < h.timers.Count; i++)
            {
                var t = h.timers[i];
                int period = GetCycleTimeForProduct(t.pid);
                if (period <= 0) period = 60;

                int left0 = t.left > 0 ? t.left : period;

                if (h.type == "mine")
                {
                    if (delta >= left0)
                    {
                        // таймер успел закончиться оффлайн → награда и очистка
                        yield return StartCoroutine(MinePayoutOnce(h, t.pid));
                        h.timers.Clear();
                        timersChanged = true;
                    }
                    else
                    {
                        // таймер ещё не истёк → уменьшаем на прошедшее время
                        t.left = left0 - (int)delta;
                        timersChanged = true;
                    }
                }
                else if (h.type == "voyage")
                {
                    if (delta >= left0)
                    {
                        yield return StartCoroutine(VoyagePayoutOnce(h, t));
                        h.timers.Clear();
                        timersChanged = true;
                    }
                    else
                    {
                        // таймер ещё не истёк → уменьшаем на прошедшее время
                        t.left = left0 - (int)delta;
                        timersChanged = true;
                    }
                }

                else // обычные дома
                {
                    int newLeft = left0 - (int)delta;

                    // 🔹 Если таймер <= 0, ставим 4
                    t.left = newLeft <= 0 ? 4 : newLeft;

                    timersChanged = true;
                }
            }
        }


        if (timersChanged)
        {
            // сохраним новые таймеры домов
            string housesJson = HousesToJson();
            yield return PatchUserField("houses", housesJson);
        }
    }

    // left0 — сколько оставалось в прошлом сеансе до выплаты
    // period — полный цикл товара
    // delta  — сколько времени прошло
    // Возвращает: cycles (сколько выплат) и newLeft (новый остаток до следующей выплаты)
    private void ComputeOfflineCycles(int left0, int period, int delta, out long cycles, out int newLeft)
    {
        if (delta < left0)
        {
            cycles = 0;
            newLeft = left0 - delta;
            return;
        }

        int afterFirst = delta - left0;          // время после первой выплаты
        long extra = period > 0 ? afterFirst / period : 0;
        cycles = 1 + Math.Max(0, extra);

        int rem = period > 0 ? afterFirst % period : 0;
        newLeft = period - rem;
        if (newLeft <= 0) newLeft = period;
    }
}
