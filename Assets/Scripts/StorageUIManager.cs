using System.Collections.Generic;
using UnityEngine;

public class StorageUIManager : MonoBehaviour
{
    public GameManager gameManager;
    public Transform contentRoot;   // сюда спавним префабы
    public GameObject storageItemPrefab;

    private List<GameObject> spawnedItems = new();

    public void RefreshStorageUI()
    {
        // Очистка старых
        foreach (var go in spawnedItems) Destroy(go);
        spawnedItems.Clear();

        if (gameManager.currentUser == null) return;

        // Парсим JSON storage_count
        Dictionary<int, int> storage = gameManager.ParseSeeds(gameManager.currentUser.storage_count);

        foreach (var kv in storage)
        {
            int productId = kv.Key;
            int count = kv.Value;

            if (count <= 0) continue;

            var prod = gameManager.allProducts.Find(p => p.id == productId);
            if (prod == null) continue;

            var go = Instantiate(storageItemPrefab, contentRoot);
            var script = go.GetComponent<StorageItemScript>();
            script.Init(gameManager, prod, count);

            spawnedItems.Add(go);
        }
    }
}