using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopScript : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;        // Ссылка на твой GameManager
    public GameObject shopItemPrefab;      // Префаб карточки товара
    public Transform itemsParent;
    public bool doOnce;// Контейнер (например Content в ScrollView)

    private List<GameObject> spawnedItems = new List<GameObject>();

    private void Start()
    {
        PopulateShop();
    }

    // Вызовем откуда-то (например, кнопка "Обновить магазин")
    public void PopulateShop()
    {
        
        ClearShop();

        if (gameManager == null)
        {
            Debug.LogError("[ShopScript] GameManager не назначен!");
            return;
        }

        // Берём список продуктов из GameManager
        List<GameManager.ProductDto> products = gameManager.allProducts;


        foreach (var product in products)
        {
            GameObject itemGO = Instantiate(shopItemPrefab, itemsParent);
            Debug.Log(product.id);
            ShopItemScript itemScript = itemGO.GetComponent<ShopItemScript>();
            if (itemScript != null)
            {
                // Конвертируем данные в формат ShopItemScript
                var dto = new ShopItemScript.ProductDto
                {
                    id = product.id,
                    name = product.name,
                    price = (float)product.price,
                    sell_price = (float)product.sell_price,
                    speed_price = (float)product.speed_price,
                    lvl_for_buy = product.lvl_for_buy,
                    time = product.time,
                    exp = (float) product.exp,
                    image_seed_link = product.image_seed_link,
                    image_ready_link = product.image_ready_link

                };

                // ✅ передаём и продукт, и gameManager
                itemScript.SetProduct(dto, gameManager);
            }

            spawnedItems.Add(itemGO);
        }
    }

    // Очистка (если обновляем список)
    public void ClearShop()
    {
        foreach (var go in spawnedItems)
        {
            Destroy(go);
        }
        spawnedItems.Clear();
    }
}
