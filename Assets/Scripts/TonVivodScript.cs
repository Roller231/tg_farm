using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class TonVivodScript : MonoBehaviour
{
    [Tooltip("Базовый URL вашего withdrawBack (без завершающего /)")]
    private string Endpoint => $"{ApiConfig.BaseUrl}/withdraw";

    [Header("UI References")]
    public InputField tonAddressInput;
    public InputField amountInput;
    public InputField memoInput;           // необяз.
    public Button submitButton;
    public Text statusText;                // сюда пишем статус (успех/ошибка)
    public Text tonbalance;
    
    [Header("Optional")]
    public bool clearOnSuccess = true;
    public GameManager gm;

    [Serializable]
    private class WithdrawCreate
    {
        public string ton_address;
        public float amount;
        public string memo;

        public WithdrawCreate(string addr, float amt, string memo = null)
        {
            this.ton_address = addr;
            this.amount = amt;
            this.memo = memo;
        }
    }

    [Serializable]
    private class WithdrawOut
    {
        public int id;
        public string ton_address;
        public float amount;
        public string memo;
        public string created_at;
    }

    private void Awake()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmitClicked);
        }
    }

    private void OnEnable()
    {
        tonbalance.text = gm.currentUser.ton.ToString() + " TON";
    }

    private void OnSubmitClicked()
    {
        // Простая валидация полей
        string addr = tonAddressInput != null ? tonAddressInput.text.Trim() : "";
        string amtStr = amountInput != null ? amountInput.text.Trim().Replace(",", ".") : "0";
        string memo = memoInput != null ? memoInput.text.Trim() : null;

        if (string.IsNullOrEmpty(addr) || addr.Length < 5)
        {
            statusText.gameObject.SetActive(true);
            SetStatus("Введите корректный TON адрес.", true);
            return;
        }

        if (!float.TryParse(amtStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float amount) || amount <= 0f )
        {
            statusText.gameObject.SetActive(true);

            SetStatus("Введите сумму > 0.", true);
            return;
        }

        if (amount > gm.currentUser.ton)
        {
            statusText.gameObject.SetActive(true);

            SetStatus("Недостаточно средств", true);
            return;
        }
        
        if (amount < 0.3f)
        {
            statusText.gameObject.SetActive(true);

            SetStatus("Минимальная сумма вывода 0.3 TON", true);
            return;
        }

        StartCoroutine(SendWithdraw(addr, amount, memo));
    }

    private IEnumerator SendWithdraw(string addr, float amount, string memo)
    {
        ToggleInteractable(false);
            //SetStatus("Отправка заявки...", false);
            SetStatus("Ваша заявка успешно отправлена!", false);
        var payload = new WithdrawCreate(addr, amount, string.IsNullOrEmpty(memo) ? gm.currentUser.id : memo);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest(Endpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            
            gm.currentUser.ton -= amount;
            tonbalance.text = gm.currentUser.ton.ToString() + " TON";

            yield return gm.PatchUserField("ton", gm.currentUser.ton.ToString());
            
            
            if (req.result == UnityWebRequest.Result.Success || (req.responseCode >= 200 && req.responseCode < 300))
            {
                // Парс ответа
                WithdrawOut resp = null;
                try { resp = JsonUtility.FromJson<WithdrawOut>(req.downloadHandler.text); } catch { }

                if (resp != null && resp.id > 0)
                {
                    SetStatus($"Заявка #{resp.id} создана: {resp.amount} TON → {resp.ton_address}", false);
                    if (clearOnSuccess)
                    {
                        if (tonAddressInput) tonAddressInput.text = "";
                        if (amountInput) amountInput.text = "";
                        if (memoInput) memoInput.text = "";
                    }
                }
                else
                {
                    SetStatus("Заявка создана, но ответ не распознан.", false);
                }
            }
            else
            {
                string err = string.IsNullOrEmpty(req.error) ? $"HTTP {req.responseCode}" : req.error;
                
            }
        }

        ToggleInteractable(true);
    }

    private void ToggleInteractable(bool state)
    {
        if (submitButton) submitButton.interactable = state;
        if (tonAddressInput) tonAddressInput.interactable = state;
        if (amountInput) amountInput.interactable = state;
        if (memoInput) memoInput.interactable = state;
    }

    private void SetStatus(string msg, bool isError)
    {
        if (!statusText) return;
        statusText.text = msg;
        statusText.color = isError ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 0.2f);
    }

    private void OnDisable()
    {
        statusText.gameObject.SetActive(false);
    }
}
