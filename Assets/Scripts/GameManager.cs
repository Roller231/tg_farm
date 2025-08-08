using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UTeleApp;

public class GameManager : MonoBehaviour
{
    public string userID = "1";
    
    public string username = "123312";

    public string refLink = "";
        
    public float money = 0f;
    
    
    [Header("User Interface")]
    [SerializeField] private Text usernameText;
    [SerializeField] private Text refCountText;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text moneyText2;


    private void Awake()
    {
        TelegramWebApp.Ready();
        TelegramWebApp.Expand();

        userID = GetUserIdFromInitData(TelegramWebApp.InitData).ToString();
        username = GetUsernameFromInitData(JsonUtility.ToJson(TelegramWebApp.InitDataUnsafe));

        usernameText.text = username;
    }


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
    
    
    [Serializable]
    public class TelegramInitData
    {
        public TelegramUser user;
    }

    [Serializable]
    public class TelegramUser
    {
        public long id;
        public bool is_bot;
        public string first_name;
        public string last_name;
        public string username;
        public string language_code;
        public string photo_url;
    }
}
