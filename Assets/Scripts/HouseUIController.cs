using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class HouseUIController : MonoBehaviour
{
    [Header("Links")]
    public GameManager gm;                       // GameManager из сцены
    public int houseId = 1;                      // 1 / 2 / 3
    public string houseType = "home1";           // "home1" | "home2" | "home3"
    public Transform contentParent;              // контейнер для карточек
    public ProductHouseCard productCardPrefab;   // префаб карточки
    public Text headerTitle;                     // заголовок “Дом 1” (опционально)

    [Header("Options")]
    public bool clearOnBuild = true;

    private List<GameManager.ProductDto> _products = new();

    [Serializable] private class HouseTimer { public int pid; public int left; }
    [Serializable] private class House { public int id; public float price; public int lvl_for_buy; public int build_time; public bool active; public string type; public List<HouseTimer> timers; }
    [Serializable] private class HousesWrap { public List<House> items; }

    // ---------- Helpers ----------
    private bool IsHouseActive()
    {
        try
        {
            if (gm == null || gm.currentUser == null || string.IsNullOrEmpty(gm.currentUser.houses))
                return false;
            var w = JsonUtility.FromJson<HousesWrap>(gm.currentUser.houses);
            if (w == null || w.items == null) return false;
            var h = w.items.Find(x => x.id == houseId);
            return h != null && h.active;
        }
        catch { return false; }
    }

    private bool IsProductAlreadyInHouse(int productId)
    {
        try
        {
            if (gm == null || gm.currentUser == null || string.IsNullOrEmpty(gm.currentUser.houses))
                return false;
            var w = JsonUtility.FromJson<HousesWrap>(gm.currentUser.houses);
            if (w == null || w.items == null) return false;
            var h = w.items.Find(x => x.id == houseId);
            if (h == null || h.timers == null) return false;
            return h.timers.Exists(t => t.pid == productId);
        }
        catch { return false; }
    }

    private List<GameManager.ProductDto> GetProductsByType()
    {
        if (gm == null) return new List<GameManager.ProductDto>();
        return houseType == "home1" ? gm.home1Products :
               houseType == "home2" ? gm.home2Products :
               houseType == "home3" ? gm.home3Products :
               new List<GameManager.ProductDto>();
    }

    private void DisableBuyButton(ProductHouseCard card, string reason = "Недоступно")
    {
        if (card != null)
        {
            card.SetLocked(true, reason);
            var btn = card.GetComponentInChildren<Button>(true);
            if (btn != null) btn.interactable = false;
        }
    }

    private void EnableBuyButton(ProductHouseCard card)
    {
        if (card != null)
        {
            card.SetLocked(false, "");
            var btn = card.GetComponentInChildren<Button>(true);
            if (btn != null) btn.interactable = true;
        }
    }

    private bool HasFunds(bool payCoin, float price)
    {
        if (gm == null || gm.currentUser == null) return false;
        return payCoin ? gm.currentUser.coin >= price : gm.currentUser.ton >= price;
    }

    // ---------- Build UI ----------
    public void TryBuild()
    {
        if (gm == null) return;

        if (headerTitle) headerTitle.text = $"Дом {houseId}";

        _products = GetProductsByType();
        int count = Mathf.Min(4, _products.Count);

        if (clearOnBuild && contentParent)
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
                Destroy(contentParent.GetChild(i).gameObject);
        }

        bool houseActive = IsHouseActive();

        for (int i = 0; i < count; i++)
        {
            var p = _products[i];
            var card = Instantiate(productCardPrefab, contentParent);
            bool payCoin = i < 2; // первые две — монеты, вторые две — TON

            // Тексты и картинка
            card.SetTexts(
                title: p.name,
                price: p.price,
                cycleSec: p.time,
                incomePerCycle: p.sell_price,
                payCoin: payCoin
            );
            card.SetImageFromUrls(this, p.image_ready_link, p.image_seed_link);

            // Условия доступности карточки
            string disableReason = null;

            if (!houseActive)
                disableReason = "Купи дом";
            else if (IsProductAlreadyInHouse(p.id))
                disableReason = "Куплено";
            else if (!HasFunds(payCoin, p.price))
                disableReason = payCoin ? "Нет монет" : "Нет TON";

            if (disableReason != null)
            {
                DisableBuyButton(card, disableReason);
                continue;
            }
            else
            {
                EnableBuyButton(card);
            }

            // Клик: списать валюту + добавить продукт в дом
            int pid = p.id;
            var cardRef = card;
            card.SetButtonListener(() =>
            {
                gm.currentUser.houses = gm.HousesToJson();
                
                // Повторная проверка на всякий случай
                if (!IsHouseActive())
                {
                    DisableBuyButton(cardRef, "Купи дом");
                    return;
                }
                if (IsProductAlreadyInHouse(pid))
                {
                    DisableBuyButton(cardRef, "Куплено");
                    return;
                }
                if (!HasFunds(payCoin, p.price))
                {
                    DisableBuyButton(cardRef, payCoin ? "Нет монет" : "Нет TON");
                    return;
                }

                StartCoroutine(HandleBuyAndAddProduct(pid, payCoin, p.price, cardRef));
            });
        }
    }

    // ---------- Purchase + Add ----------
    private IEnumerator HandleBuyAndAddProduct(int productId, bool payCoin, float price, ProductHouseCard cardToDisable)
    {
        if (gm == null || gm.currentUser == null) yield break;

        // Оплата
        if (payCoin)
        {
            if (gm.currentUser.coin < price)
            {
                DisableBuyButton(cardToDisable, "Нет монет");
                yield break;
            }
            gm.currentUser.coin -= price;
            yield return gm.StartCoroutine(gm.PatchUserField("coin", gm.currentUser.coin.ToString(CultureInfo.InvariantCulture)));
            gm.ApplyUserData();
        }
        else
        {
            if (gm.currentUser.ton < price)
            {
                DisableBuyButton(cardToDisable, "Нет TON");
                yield break;
            }
            gm.currentUser.ton -= price;
            yield return gm.StartCoroutine(gm.PatchUserField("ton", gm.currentUser.ton.ToString(CultureInfo.InvariantCulture)));
        }

        // Добавляем продукт в дом (создаёт таймер в GameManager)
        gm.AddProductToHouseButton(houseId, productId);

        // Мгновенно помечаем карточку как купленную
        DisableBuyButton(cardToDisable, "Куплено");

        // Можно перерисовать весь список, если нужно:
        // TryBuild();
        yield return null;
    }
}
