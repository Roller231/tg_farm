using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FriendList : MonoBehaviour
{
    [SerializeField] public Text usernameText;
    [SerializeField] public Text rewardText;

    public void ChangeText(string username, string reward)
    {
        usernameText.text = username;
        rewardText.text = reward;
    }
    
}
