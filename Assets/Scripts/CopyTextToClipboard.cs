using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;

public class CopyTextToClipboard : MonoBehaviour
{

    [SerializeField] private Text textToCopy; // Для обычного UI Text
    // [SerializeField] private TMPro.TMP_Text textToCopyTMP; // Для TextMeshPro
    
    // Для WebGL
    [DllImport("__Internal")]
    private static extern void CopyToClipboard(string text);
    
    public void CopyText()
    {
        if (textToCopy == null || string.IsNullOrEmpty(textToCopy.text))
        {
            Debug.LogWarning("Текст для копирования отсутствует!");
            return;
        }
        
        string text = textToCopy.text;
        
#if UNITY_WEBGL && !UNITY_EDITOR
            CopyToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
        Debug.Log("Текст скопирован: " + text);
        ShowNotification();
#endif
    }
    
    private void ShowNotification()
    {
        // Реализация уведомления (например, временный текст на экране)
    }

}