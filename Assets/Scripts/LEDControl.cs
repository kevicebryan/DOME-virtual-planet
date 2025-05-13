using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class LEDController : MonoBehaviour
{
    public string baseUrl = "http://192.168.8.185/leds";

    public void SendLED(string[] rgbValues)
    {
        if (rgbValues.Length != 21)
        {
            Debug.LogError("必须提供21个RGB参数！");
            return;
        }

        StartCoroutine(SendRequest(rgbValues));
    }

    private string lastSentQuery = "";

    private IEnumerator SendRequest(string[] rgbValues)
    {
        StringBuilder query = new StringBuilder();

        for (int i = 0; i < 21; i++)
        {
            query.Append($"rgb{i}={rgbValues[i]}");
            if (i < 20)
                query.Append("&");
        }

        string fullUrl = $"{baseUrl}?{query}";

        if (fullUrl != lastSentQuery)
        {
            lastSentQuery = fullUrl;
            Debug.Log(fullUrl);

            using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("发送失败: " + www.error);
                }
                else
                {
                    Debug.Log("发送成功: " + www.downloadHandler.text);
                }
            }
        }
    }

    public void LightSingleLEDByDirection(float direction01, string colorHex = "00FFFF")
    {
        int ledIndex = Mathf.RoundToInt(direction01 / 18);
        ledIndex = Mathf.Clamp(ledIndex, 0, 19);

        string[] leds = new string[21];
        for (int i = 0; i < 20; i++)
        {
            leds[i] = (i == ledIndex) ? colorHex : "off";
        }

        SendLED(leds);
    }
}