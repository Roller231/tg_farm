using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LeaderboardManager : MonoBehaviour
{
    [Header("Backend")]
    [Tooltip("Полный URL до /users, например https://example.com/users")]
    public string usersApiUrl = "http://127.0.0.1:8009/users";

    [Header("UI")]
    [Tooltip("Префаб элемента списка (FriendList с usernameText и rewardText)")]
    public GameObject entryPrefab;

    [Tooltip("Родительский объект, куда будут спавниться элементы")]
    public Transform parentContainer;

    [Header("Options")]
    [Tooltip("Сколько элементов показывать (0 или меньше — показать всех)")]
    public int topN = 50;

    [Tooltip("Формат отображения монет в rewardText, {0} — число монет")]
    public string coinsFormat = "{0} coins";

    [Tooltip("Формат отображения уровня в rewardText, {0} — уровень")]
    public string levelFormat = "Lvl {0}";

    // внутреннее состояние/кэш (не обязательно, но удобно)
    private List<UserDto> _cache = new List<UserDto>();
    private bool _loaded = false;
    private bool _isLoading = false;

    #region DTO
    [Serializable]
    public class UserDto
    {
        public string id;
        public string name;
        public string refId;

        // уровень может приходить числом
        public int lvl;

        // монеты на разных бэках могут прийти строкой ("9650.00") ИЛИ числом.
        // Поэтому оставляем два поля — одно строка, одно число.
        // JsonUtility проигнорирует то, что не совпало по типу.
        public string coin;   // если пришла строка
        public float coinF;   // если пришло числом
    }

    private void Start()
    {
        ShowLeaderboardByCoins();
    }

    [Serializable]
    private class UsersWrapper
    {
        public UserDto[] items;
    }
    #endregion

    #region Public API (привяжи к кнопкам)
    public void ShowLeaderboardByCoins()
    {
        StartCoroutine(EnsureLoadedThen(() =>
        {
            ClearList();
            var sorted = _cache.OrderByDescending(u => GetCoinValue(u)).ToList();
            SpawnEntries(sorted, user => string.Format(coinsFormat, FormatCoins(GetCoinValue(user))));
        }));
    }

    public void ShowLeaderboardByLevel()
    {
        StartCoroutine(EnsureLoadedThen(() =>
        {
            ClearList();
            var sorted = _cache.OrderByDescending(u => u.lvl).ToList();
            SpawnEntries(sorted, user => string.Format(levelFormat, user.lvl));
        }));
    }
    #endregion

    #region Loading & Rendering
    private IEnumerator EnsureLoadedThen(Action after)
    {
        if (_loaded)
        {
            after?.Invoke();
            yield break;
        }

        if (_isLoading)
        {
            // ждём пока текущая загрузка завершится
            while (_isLoading) yield return null;
            after?.Invoke();
            yield break;
        }

        _isLoading = true;
        yield return StartCoroutine(LoadAllUsers());
        _isLoading = false;
        _loaded = _cache.Count > 0;

        after?.Invoke();
    }

    private IEnumerator LoadAllUsers()
    {
        _cache.Clear();

        using (UnityWebRequest req = UnityWebRequest.Get(usersApiUrl))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Leaderboard] Ошибка GET {req.responseCode} {req.error}");
                yield break;
            }

            string wrapped = "{\"items\":" + req.downloadHandler.text + "}";
            UsersWrapper wrapper = null;
            try
            {
                wrapper = JsonUtility.FromJson<UsersWrapper>(wrapped);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Leaderboard] Parse error: " + ex.Message);
            }

            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogWarning("[Leaderboard] users пустые");
                yield break;
            }

            _cache.AddRange(wrapper.items);
            // Можно отфильтровать пустые имена, если нужно:
            // _cache = _cache.Where(u => !string.IsNullOrEmpty(u.name)).ToList();

            Debug.Log($"[Leaderboard] Загружено игроков: {_cache.Count}");
        }
    }

    private void SpawnEntries(List<UserDto> data, Func<UserDto, string> makeRightText)
    {
        int count = (topN <= 0) ? data.Count : Mathf.Min(topN, data.Count);

        for (int i = 0; i < count; i++)
        {
            var u = data[i];
            GameObject go = Instantiate(entryPrefab, parentContainer);

            var fl = go.GetComponent<FriendList>();
            if (fl != null)
            {
                if (fl.usernameText != null)
                {
                    fl.usernameText.text = string.IsNullOrEmpty(u.name) ? u.id : u.name;
                }
                if (fl.rewardText != null)
                {
                    fl.rewardText.text = makeRightText(u);
                }
            }
        }
    }

    private void ClearList()
    {
        if (!parentContainer) return;
        for (int i = parentContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(parentContainer.GetChild(i).gameObject);
        }
    }
    #endregion

    #region Helpers
    private decimal GetCoinValue(UserDto u)
    {
        // 1) если пришло числом — используем coinF
        if (!float.IsNaN(u.coinF) && !float.IsInfinity(u.coinF) && u.coinF != 0f)
            return (decimal)u.coinF;

        // 2) если пришло строкой — парсим
        if (!string.IsNullOrEmpty(u.coin))
        {
            if (decimal.TryParse(u.coin, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            // иногда бэк шлёт как "1234,56"
            if (decimal.TryParse(u.coin, NumberStyles.Any, new CultureInfo("ru-RU"), out d))
                return d;
        }

        // 3) иначе ноль
        return 0m;
    }

    private string FormatCoins(decimal value)
    {
        // человеко-читаемый формат: 12 345.67
        return value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }
    #endregion
}
