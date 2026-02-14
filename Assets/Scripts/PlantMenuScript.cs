using System.Collections.Generic;
using UnityEngine;

public class PlantMenuScript : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;          // ссылка на GameManager
    public GameObject plantItemPrefab;       // префаб карточки посадки (с PlantMenuItemScript)
    public Transform itemsParent;            // контейнер (например, Content в ScrollView)

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private void OnEnable()
    {
        PopulatePlantMenu();
    }
    public void SetAllButtonsInteractable(bool state)
    {
        foreach (var go in spawnedItems)
        {
            var item = go.GetComponent<PlantMenuItemScript>();
            if (item != null)
                item.SetButtonState(state);
        }
    }


    public void PopulatePlantMenu()
    {
        ClearPlantMenu();

        if (gameManager == null || gameManager.currentUser == null)
        {
            Debug.LogError("[PlantMenu] GameManager или текущий пользователь не назначены");
            return;
        }

        // читаем запасы семян
        Dictionary<int, int> seeds = gameManager.ParseSeeds(gameManager.currentUser.seed_count);

        // идём по всем продуктам, показываем только те, где есть запас > 0
        foreach (var product in gameManager.allProducts)
        {
            int have = seeds != null && seeds.ContainsKey(product.id) ? seeds[product.id] : 0;
            if (have <= 0) continue; // нет семян — не показываем

            GameObject itemGO = Instantiate(plantItemPrefab, itemsParent);
            var item = itemGO.GetComponent<PlantMenuItemScript>();
            if (item != null)
            {
                // маппим в dto посадки (минимальные поля)
                var dto = new PlantMenuItemScript.ProductDto
                {
                    id = product.id,
                    name = product.name,
                    time = product.time,
                    exp = product.exp,
                    lvl_for_buy = product.lvl_for_buy,
                    image_seed_link = product.image_seed_link,
                };

                item.SetProduct(dto, gameManager);
            }

            spawnedItems.Add(itemGO);
        }
    }

    public void ClearPlantMenu()
    {
        foreach (var go in spawnedItems)
            Destroy(go);

        spawnedItems.Clear();
    }
}
