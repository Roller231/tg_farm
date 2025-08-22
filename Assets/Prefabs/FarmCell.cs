using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FarmCell : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Text timerText;          // текст таймера
    public Image readyImage;        // сюда ставим image_ready_link, когда готово
    public GameObject busyOverlay;  // необязательно: рамка «занято»

    [Header("State (runtime)")]
    public bool isBusy;
    public int productId;
    public long endUnix;            // когда закончится (unix)

    private GameManager gm;
    private Coroutine timerCo;

    public void Init(GameManager manager)
    {
        gm = manager;
        ClearUI();
    }

    public void Plant(GameManager.ProductDto prod)
    {
        if (isBusy) return;
        isBusy = true;
        productId = prod.id;
        endUnix = UnixNow() + prod.time;

        if (busyOverlay) busyOverlay.SetActive(true);
        if (timerText) timerText.gameObject.SetActive(true);
        if (readyImage) readyImage.sprite = null;

        if (timerCo != null) StopCoroutine(timerCo);
        timerCo = StartCoroutine(TimerLoop());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isBusy)
        {
            // пользователь выбрал эту клетку для посадки
            if (gm != null) gm.SelectedCell = this;
            return;
        }

        // если готово — собираем
        if (UnixNow() >= endUnix)
            StartCoroutine(Harvest());
    }

    private IEnumerator TimerLoop()
    {
        while (UnixNow() < endUnix)
        {
            if (timerText) timerText.text = (endUnix - UnixNow()).ToString() + "s";
            yield return new WaitForSeconds(1f);
        }

        // готово → подгружаем картинку готового состояния
        var prod = gm != null ? gm.allProducts.Find(p => p.id == productId) : null;
        if (prod != null && !string.IsNullOrEmpty(prod.image_ready_link))
            yield return StartCoroutine(LoadReadyImage(prod.image_ready_link));

        if (timerText) timerText.text = "ГОТОВО";
    }

    private IEnumerator Harvest()
    {
        // +1 в storage_count по productId (и очистка клетки локально)
        yield return gm.AddToStorage(productId, 1);

        isBusy = false;
        productId = 0;
        endUnix = 0;
        if (timerCo != null) StopCoroutine(timerCo);
        ClearUI();
    }

    private void ClearUI()
    {
        if (busyOverlay) busyOverlay.SetActive(false);
        if (timerText) { timerText.text = ""; timerText.gameObject.SetActive(false); }
        if (readyImage) readyImage.sprite = null;
    }

    private IEnumerator LoadReadyImage(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                var sp = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f,0.5f));
                if (readyImage) readyImage.sprite = sp;
            }
            else Debug.LogError($"[FarmCell] image load error: {req.error}");
        }
    }

    private static long UnixNow() =>
        (long)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds;
}
