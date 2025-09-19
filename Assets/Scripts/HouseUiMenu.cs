using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HouseUiMenu : MonoBehaviour
{
    [Header("Links")]
    public GameManager gm;                       // GameManager из сцены
    public int houseId = 1;                      // 1 / 2 / 3
    public Transform contentParent;              // контейнер для карточек
    public HouseProductCard productCardPrefab;   // префаб карточки
    public Text headerTitle;                     // заголовок “Дом 1” (опционально)

    [Header("Options")]
    public bool clearOnBuild = true;

    private List<GameManager.ProductDto> _products = new();

    public void BuildUI()
    {
        if (gm == null || gm.currentUser == null) return;

        var houses = gm.GetType()
            .GetMethod("GetHouses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(gm, null) as GameManager.HousesWrapper;

        if (houses == null || houses.items == null) return;
        var house = houses.items.Find(x => x.id == houseId);
        if (house == null || !house.active) return;

        if (clearOnBuild)
        {
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
        }

        foreach (var t in house.timers)
        {
            if (!gm.productById.TryGetValue(t.pid, out var product)) continue;

            var card = Instantiate(productCardPrefab, contentParent);
            card.Init(gm, houseId, product, t.left, t.lvl);
        }

        if (headerTitle) headerTitle.text = $"Дом {houseId}";
    }
}
