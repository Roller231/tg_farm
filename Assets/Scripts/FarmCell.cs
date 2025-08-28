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
    public Text timerText;          // текст таймера
    public Image readyImage;        // картинка готового продукта
    public GameObject busyOverlay;  // рамка «занято»
    public GameObject imageBuyBtn;  // рамка «занято»

    [Header("State (runtime)")]
    public bool isBusy;
    public bool isLocked;
    public int productId;
    public long endUnix;       // когда закончится (unix)

    
    public int priceGrid;
    public int needLvl;

    
    private GameManager gm;
    private Coroutine timerCo;

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

        if (busyOverlay) busyOverlay.SetActive(true);

        long now = UnixNow();

        if (endUnix > now)
        {
            // растение растёт
            if (timerText) timerText.gameObject.SetActive(true);
            if (readyImage) readyImage.gameObject.SetActive(false);

            if (timerCo != null) StopCoroutine(timerCo);
            timerCo = StartCoroutine(TimerLoop());
        }
        else
        {
            // растение уже готово
            if (timerText)
            {
                timerText.text = "";
                timerText.gameObject.SetActive(true);
            }

            if (prodIfKnown != null && !string.IsNullOrEmpty(prodIfKnown.image_ready_link))
                StartCoroutine(LoadReadyImage(prodIfKnown.image_ready_link));

            if (readyImage) readyImage.gameObject.SetActive(true);

            // ⚡ важный фикс: НЕ запускаем TimerLoop и НЕ сбрасываем
        }
    }

    public void Plant(GameManager.ProductDto prod)
    {
        if (isLocked || isBusy) return;

        isBusy = true;
        productId = prod.id;
        endUnix = UnixNow() + prod.time;

        if (busyOverlay) busyOverlay.SetActive(true);
        if (timerText) timerText.gameObject.SetActive(true);
        if (readyImage) readyImage.gameObject.SetActive(false);

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
        while (UnixNow() < endUnix)
        {
            if (timerText) timerText.text = (endUnix - UnixNow()) + "s";
            yield return new WaitForSeconds(1f);
        }

        // растение готово
        if (timerText) timerText.text = "";

        var prod = gm != null ? gm.allProducts.Find(p => p.id == productId) : null;
        if (prod != null && !string.IsNullOrEmpty(prod.image_ready_link))
            yield return StartCoroutine(LoadReadyImage(prod.image_ready_link));

        if (readyImage) readyImage.gameObject.SetActive(true);
    }

    private IEnumerator Harvest()
    {
        yield return gm.AddToStorage(productId, 1);

        ClearToIdle();
    }

    public void ClearToIdle()
    {
        isBusy = false;
        productId = 0;
        endUnix = 0;

        if (timerCo != null) StopCoroutine(timerCo);
        timerCo = null;

        if (busyOverlay) busyOverlay.SetActive(false);
        if (timerText) { timerText.text = ""; timerText.gameObject.SetActive(false); }
        if (readyImage) { readyImage.sprite = null; readyImage.gameObject.SetActive(false); }
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
        if (gm.money >= priceGrid && gm.lvl >= needLvl)
        {
            gm.money -= priceGrid;
            
            GetComponent<Button>().interactable = true;
            imageBuyBtn.SetActive(false);
            isLocked = false;
            gm.currentUser.grid_count++;
            Debug.Log(gm.currentUser.grid_count);

            gm.moneyText.text = gm.money.ToString();
            
            yield return gm.PatchUserField("grid_count", gm.currentUser.grid_count.ToString(CultureInfo.InvariantCulture));
            yield return gm.PatchUserField("coin", gm.money.ToString(CultureInfo.InvariantCulture));
            
            
        }
    }


    
    
    private static long UnixNow() =>
        (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
}
