using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ProductHouseCard : MonoBehaviour
{
 [Header("UI refs")]
    public Text titleText;
    public Text priceText;
    public Text cycleText;
    public Text incomeText;
    public Text currencyBadgeText;  // “COIN” или “TON”
    public Button actionButton;

    [Header("Image")]
    public Image productImage;      // назначь Image в префабе
    public Sprite fallbackSprite;   // необязателен, если загрузка не удалась
    public bool preserveAspect = true;

    [Header("Lock overlay (optional)")]
    public CanvasGroup lockOverlay;
    public Text lockedHintText;

    // простой кэш, чтобы не тянуть одну и ту же картинку по нескольку раз
    private static readonly Dictionary<string, Sprite> _spriteCache = new();

    public void SetLocked(bool locked, string hint = "Купите дом")
    {
        if (actionButton) actionButton.interactable = !locked;
        if (lockOverlay)
        {
            lockOverlay.alpha = locked ? 1f : 0f;
            lockOverlay.blocksRaycasts = locked;
            lockOverlay.interactable = false;
        }
        if (lockedHintText) lockedHintText.text = locked ? hint : "Купить";
    }

    public void SetTexts(string title, float price, int cycleSec, float incomePerCycle, bool payCoin)
    {
        if (titleText) titleText.text = title;
        if (cycleText) cycleText.text = $"Цикл: {FormatTime(cycleSec)}";
        if (incomeText) incomeText.text = $"Доход: {incomePerCycle.ToString("0.##", CultureInfo.InvariantCulture)}" + " TON";
        if (currencyBadgeText) currencyBadgeText.text = payCoin ? "COIN" : "TON";

        string ss = payCoin ? "SunCoin" : "TON";
        if (priceText) priceText.text = $"Цена: {price.ToString("0.##", CultureInfo.InvariantCulture)} {ss}";

    }

    public void SetButtonListener(UnityEngine.Events.UnityAction onClick)
    {
        if (!actionButton) return;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(onClick);
    }

    public void SetImageFromUrls(MonoBehaviour runner, string primaryUrl, string fallbackUrl = null)
    {
        if (!productImage) return;

        productImage.preserveAspect = preserveAspect;

        // выбираем первый доступный url
        string urlToLoad = !string.IsNullOrWhiteSpace(primaryUrl) ? primaryUrl : fallbackUrl;
        if (string.IsNullOrWhiteSpace(urlToLoad))
        {
            ApplySprite(null); // сброс / fallback
            return;
        }

        // кэш
        if (_spriteCache.TryGetValue(urlToLoad, out var sprite))
        {
            ApplySprite(sprite);
            return;
        }

        // грузим
        runner.StartCoroutine(LoadSpriteCoroutine(urlToLoad, (spr) =>
        {
            if (spr != null) _spriteCache[urlToLoad] = spr;
            ApplySprite(spr);
        }));
    }

    private void ApplySprite(Sprite spr)
    {
        if (productImage)
        {
            productImage.sprite = spr != null ? spr : fallbackSprite;
            productImage.enabled = (productImage.sprite != null);
        }
    }

    private IEnumerator LoadSpriteCoroutine(string url, System.Action<Sprite> done)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url, true))
        {
#if UNITY_WEBGL
            // На WebGL важно, чтобы сервер отдавал CORS заголовки:
            // Access-Control-Allow-Origin: *
            req.downloadHandler = new DownloadHandlerTexture(true);
#endif
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ProductCard] image load fail: {req.responseCode} {req.error} {url}");
                done?.Invoke(null);
            }
            else
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null) { done?.Invoke(null); yield break; }

                var spr = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                done?.Invoke(spr);
            }
        }
    }

    private string FormatTime(int sec)
    {
        if (sec < 60) return $"{sec}s";
        int m = sec / 60; int s = sec % 60;
        if (m < 60) return s > 0 ? $"{m}m {s}s" : $"{m}m";
        int h = m / 60; m = m % 60;
        return m > 0 ? $"{h}h {m}m" : $"{h}h";
    }
}
