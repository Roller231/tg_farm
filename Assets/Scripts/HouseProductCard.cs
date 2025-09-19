using System;
using UnityEngine;
using UnityEngine.UI;

public class HouseProductCard : MonoBehaviour
{
    [Header("UI")]
    public Image productImage;
    public Text tonRewardText;
    public Text name;
    public Text timerText;

    [Header("Runtime")]
    public int productId;
    public int houseId;

    private int leftSec;
    private GameManager gm;
    private float acc; // накопитель времени
    private float syncAcc; // накопитель для синхронизации

    public void Init(GameManager gameManager, int houseId, GameManager.ProductDto product, int leftSeconds)
    {
        gm = gameManager;
        this.houseId = houseId;
        this.productId = product.id;
        leftSec = leftSeconds;

        if (productImage && !string.IsNullOrEmpty(product.image_ready_link))
        {
            StartCoroutine(LoadImage(product.image_ready_link));
        }

        if (tonRewardText)
            tonRewardText.text = $"+{product.speed_price} TON";

        if (name) 
            name.text = product.name;

        UpdateTimerText();
    }

    private void Update()
    {
        if (leftSec > 0)
        {
            acc += Time.deltaTime;
            if (acc >= 1f)
            {
                int sec = Mathf.FloorToInt(acc);
                acc -= sec;
                leftSec -= sec;
                if (leftSec < 0) leftSec = 0;
                UpdateTimerText();
            }
        }

        // --- каждые 2 сек сверяемся с GameManager ---
        syncAcc += Time.deltaTime;
        if (syncAcc >= 2f)
        {
            syncAcc = 0f;
            SyncWithGameManager();
        }
    }

    private void SyncWithGameManager()
    {
        var houses = gm.GetType()
                       .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                       .Invoke(gm, null) as GameManager.HousesWrapper;

        if (houses == null) return;
        var house = houses.items.Find(x => x.id == houseId);
        if (house == null || house.timers == null) return;

        var timer = house.timers.Find(t => t.pid == productId);
        if (timer == null) return;

        leftSec = timer.left; // подтянули актуальное значение
        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        TimeSpan ts = TimeSpan.FromSeconds(leftSec);
        timerText.text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private System.Collections.IEnumerator LoadImage(string url)
    {
        using (var www = new UnityEngine.Networking.UnityWebRequest(url))
        {
            www.downloadHandler = new UnityEngine.Networking.DownloadHandlerTexture();
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D tex = ((UnityEngine.Networking.DownloadHandlerTexture)www.downloadHandler).texture;
                productImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
