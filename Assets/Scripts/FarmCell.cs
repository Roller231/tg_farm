using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FarmCell : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Text timerText;           // текст таймера
    public Image readyImage;         // картинка готового продукта
    public GameObject busyOverlay;   // рамка «занято»
    public GameObject imageBuyBtn;   // кнопка покупки клетки/рамка
    public Image progressImage;      // <--- ПРОГРЕСС-БАР (Image, type = Filled)
    public GameObject progressImageParent;      // <--- ПРОГРЕСС-БАР (Image, type = Filled)

    [Header("State (runtime)")]
    public bool isBusy;
    public bool isLocked;
    public int productId;
    public long endUnix;             // когда закончится (unix)
    private long startUnix;          // когда началось (unix)
    private int totalDuration;       // полная длительность выращивания (сек)

    public int priceGrid;
    public int needLvl;
    private bool isHarvesting;

    private GameManager gm;
    private Coroutine timerCo;

    private bool isBuyingGrid;

    public void Init(GameManager manager)
    {
        gm = manager;
        ClearToIdle();
    }

    // восстановление из grid_state
    public void RestoreFromState(int pid, long endUnixAbs, GameManager.ProductDto prodIfKnown)
    {
        if (pid <= 0)
        {
            ClearToIdle();
            return;
        }

        isBusy = true;
        productId = pid;
        endUnix = endUnixAbs;

        // если знаем продукт — восстановим длительность/старт
        if (prodIfKnown != null)
        {
            totalDuration = Mathf.Max(0, prodIfKnown.time);
            startUnix = endUnix - totalDuration;
        }
        else
        {
            totalDuration = 0;
            startUnix = 0;
        }

        if (busyOverlay) busyOverlay.SetActive(true);

        long now = UnixNow();

        if (endUnix > now)
        {
            // ещё растёт
            if (timerText) timerText.gameObject.SetActive(true);
            if (readyImage) readyImage.gameObject.SetActive(false);

            // прогресс-бар включаем, если знаем длительность
            if (progressImage)
            {
                if (totalDuration > 0)
                {
                    float remaining = Mathf.Max(0, (float)(endUnix - now));
                    float fill = 1f - (remaining / Mathf.Max(1, totalDuration));
                    progressImage.fillAmount = Mathf.Clamp01(fill);
                    progressImageParent.SetActive(true);
                }
                else
                {
                    progressImage.fillAmount = 0f;
                    progressImageParent.SetActive(false);
                }
            }

            if (timerCo != null) StopCoroutine(timerCo);
            timerCo = StartCoroutine(TimerLoop());
        }
        else
        {
            // уже готово
            if (timerText)
            {
                timerText.text = "";
                timerText.gameObject.SetActive(true);
            }

            if (prodIfKnown != null && !string.IsNullOrEmpty(prodIfKnown.image_ready_link))
                StartCoroutine(LoadReadyImage(prodIfKnown.image_ready_link));

            if (readyImage) readyImage.gameObject.SetActive(true);

            // прогресс-бар прячем
            if (progressImage)
            {
                progressImage.fillAmount = 1f;
                progressImageParent.SetActive(false);
            }
        }
    }

    public void Plant(GameManager.ProductDto prod)
    {
        if (isLocked || isBusy) return;

        isBusy = true;
        productId = prod.id;
        totalDuration = Mathf.Max(0, prod.time);
        endUnix = UnixNow() + totalDuration;
        startUnix = endUnix - totalDuration;

        if (busyOverlay) busyOverlay.SetActive(true);
        if (timerText) timerText.gameObject.SetActive(true);
        if (readyImage) readyImage.gameObject.SetActive(false);

        // прогресс-бар стартует с 0
        if (progressImage)
        {
            progressImage.fillAmount = 0f;
            
            progressImageParent.SetActive(totalDuration > 0);
        }

        if (timerCo != null) StopCoroutine(timerCo);
        timerCo = StartCoroutine(TimerLoop());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isBusy && !isLocked)
        {
            if (gm != null)
            {
                gm.SelectedCell = this;
                if (gm.plantMenuUI != null)
                    gm.plantMenuUI.SetActive(true);
            }
            return;
        }

        if (UnixNow() >= endUnix)
            StartCoroutine(Harvest());
    }

    private IEnumerator TimerLoop()
    {
        while (true)
        {
            long now = UnixNow();
            if (now >= endUnix) break;

            // таймер
            if (timerText) timerText.text = (endUnix - now) + "s";

            // прогресс
            if (progressImage)
            {
                if (totalDuration > 0)
                {
                    float remaining = Mathf.Max(0, (float)(endUnix - now));
                    float fill = 1f - (remaining / Mathf.Max(1, totalDuration));
                    progressImage.fillAmount = Mathf.Clamp01(fill);
                    if (!progressImage.gameObject.activeSelf) progressImageParent.SetActive(true);
                }
                else
                {
                    progressImage.fillAmount = 0f;
                    progressImageParent.SetActive(false);
                }
            }

            yield return new WaitForSeconds(1f);
        }

        // растение готово
        if (timerText) timerText.text = "";

        var prod = gm != null ? gm.allProducts.Find(p => p.id == productId) : null;
        if (prod != null && !string.IsNullOrEmpty(prod.image_ready_link))
            yield return StartCoroutine(LoadReadyImage(prod.image_ready_link));

        if (readyImage) readyImage.gameObject.SetActive(true);

        // прогресс-бар скрываем
        if (progressImage)
        {
            progressImage.fillAmount = 1f;
            progressImageParent.SetActive(false);
        }
    }

    private IEnumerator Harvest()
    {
        if (isHarvesting) yield break;
        isHarvesting = true;

        yield return gm.AddToStorage(productId, 1);

        ClearToIdle();

        yield return new WaitForSeconds(2.5f); // анти-спам
        isHarvesting = false;
    }


    public void ClearToIdle()
    {
        isBusy = false;
        productId = 0;
        endUnix = 0;
        startUnix = 0;
        totalDuration = 0;

        if (timerCo != null) StopCoroutine(timerCo);
        timerCo = null;

        if (busyOverlay) busyOverlay.SetActive(false);
        if (timerText) { timerText.text = ""; timerText.gameObject.SetActive(false); }
        if (readyImage) { readyImage.sprite = null; readyImage.gameObject.SetActive(false); }

        if (progressImage)
        {
            progressImage.fillAmount = 0f;
            progressImageParent.gameObject.SetActive(false);
        }
    }

    private IEnumerator LoadReadyImage(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                if (readyImage)
                {
                    readyImage.sprite = sp;
                    readyImage.gameObject.SetActive(true);
                }
            }
            else Debug.LogError($"[FarmCell] image load error: {req.error}");
        }
    }

    public void OpenGrid()
    {
        GetComponent<Button>().interactable = true;
        imageBuyBtn.SetActive(false);
        isLocked = false;
    }

    public void BuyGridFunc()
    {
        StartCoroutine(BuyGrid());
    }

    public IEnumerator BuyGrid()
    {
        if (isBuyingGrid) yield break;
        isBuyingGrid = true;

        if (gm.money >= priceGrid && gm.lvl >= needLvl)
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.interactable = false;

            gm.money -= priceGrid;
            gm.currentUser.coin = gm.money;

            imageBuyBtn.SetActive(false);
            isLocked = false;
            gm.currentUser.grid_count++;
            Debug.Log(gm.currentUser.grid_count);

            gm.ApplyUserData();

            yield return gm.PatchUserField("grid_count", gm.currentUser.grid_count.ToString(CultureInfo.InvariantCulture));
            yield return gm.PatchUserField("coin", gm.money.ToString(CultureInfo.InvariantCulture));

            if (btn != null) btn.interactable = true;
        }

        isBuyingGrid = false;
    }

    private static long UnixNow() =>
        (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
}
