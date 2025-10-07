using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class BuyValuteScript : MonoBehaviour
{
    public GameManager gm;
    
    
    
    
    public void BuyCoins500() => StartCoroutine(BuyDonateCurrency("coin", 0.15f, 500));
    public void BuyCoins1200() => StartCoroutine(BuyDonateCurrency("coin", 0.30f, 1200));
    public void BuyCoins2500() => StartCoroutine(BuyDonateCurrency("coin", 0.55f, 2500));
    
    
    public void BuyBezoz50() => StartCoroutine(BuyDonateCurrency("bezoz", 1.1f, 50));
    public void BuyBezoz150() => StartCoroutine(BuyDonateCurrency("bezoz", 2.8f, 150));
    public void BuyBezoz500() => StartCoroutine(BuyDonateCurrency("bezoz", 6.9f, 500));
    public void BuyBezoz1000() => StartCoroutine(BuyDonateCurrency("bezoz", 14f, 1000));
    public void BuyBezoz2500() => StartCoroutine(BuyDonateCurrency("bezoz", 27f, 2500));
    public void BuyBezoz5000() => StartCoroutine(BuyDonateCurrency("bezoz", 45f, 5000));

    
    
    
    
    
    
    // 💰 Покупка донатной валюты за TON
    public IEnumerator BuyDonateCurrency(string currencyType, float tonPrice, float rewardAmount)
    {
        if (gm.currentUser == null)
        {
            Debug.LogError("[DONATE] Пользователь не загружен");
            yield break;
        }

        if (gm.currentUser.ton < tonPrice)
        {
            Debug.Log("[DONATE] Недостаточно TON для покупки");
            yield break;
        }

        // списываем TON
        gm.currentUser.ton -= tonPrice;
        yield return gm.PatchUserField("ton", gm.currentUser.ton.ToString(CultureInfo.InvariantCulture));

        // начисляем выбранную валюту
        switch (currencyType.ToLower())
        {
            case "coin":
                gm.currentUser.coin += rewardAmount;
                yield return gm.PatchUserField("coin", gm.currentUser.coin.ToString(CultureInfo.InvariantCulture));
                break;

            case "bezoz":
                gm.currentUser.bezoz += rewardAmount;
                yield return gm.PatchUserField("bezoz", gm.currentUser.bezoz.ToString(CultureInfo.InvariantCulture));
                break;

            case "lvl_up":
                gm.currentUser.lvl_upgrade += rewardAmount;
                yield return gm.PatchUserField("lvl_upgrade", gm.currentUser.lvl_upgrade.ToString(CultureInfo.InvariantCulture));
                break;

            default:
                Debug.LogWarning($"[DONATE] Неизвестная валюта: {currencyType}");
                break;
        }

        Debug.Log($"[DONATE] Покупка успешна: {rewardAmount} {currencyType} за {tonPrice} TON");
        gm.ApplyUserData();
    }

}
