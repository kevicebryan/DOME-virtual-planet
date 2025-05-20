using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class LEDController : MonoBehaviour
{
    public string baseUrl = "http://192.168.8.185/leds";
    private const int TOTAL_LED_COUNT = 39;

    private string[] fullLedState = new string[TOTAL_LED_COUNT];
    private string lastSentQuery = "";

    private float updateTimer = 0f;
    [SerializeField] private float updateInterval = 0.05f; // 50ms

    private void Awake()
    {
        for (int i = 0; i < TOTAL_LED_COUNT; i++)
            fullLedState[i] = "off";
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            SendCurrentState();
        }
    }

    public void SetLED(int index, string colorHex)
    {
        if (index >= 0 && index < TOTAL_LED_COUNT)
        {
            fullLedState[index] = colorHex;
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < TOTAL_LED_COUNT; i++)
            fullLedState[i] = "off";
    }

    private void SendCurrentState()
    {
        StringBuilder query = new StringBuilder();
        for (int i = 0; i < fullLedState.Length; i++)
        {
            query.Append($"rgb{i}={fullLedState[i]}");
            if (i < fullLedState.Length - 1)
                query.Append("&");
        }

        string fullUrl = $"{baseUrl}?{query}";

        if (fullUrl != lastSentQuery)
        {
            lastSentQuery = fullUrl;
            StartCoroutine(SendRequest(fullUrl));
        }
    }

    private IEnumerator SendRequest(string fullUrl)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(fullUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("LED send failed: " + www.error);
            }
            else
            {
                Debug.Log("LED sent: " + www.downloadHandler.text);
            }
        }
    }

    public static string DimColor(string hexColor, float brightness = 1f)
    {
        brightness = Mathf.Clamp01(brightness);
        if (hexColor.Length != 6) return "000000";

        byte r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

        r = (byte)(r * brightness);
        g = (byte)(g * brightness);
        b = (byte)(b * brightness);

        return $"{r:X2}{g:X2}{b:X2}";
    }

    public static string FadeWhite(float t)
    {
        t = Mathf.Clamp01(t);
        return DimColor("FFFFFF", t);
    }
    
    
    public void LightSingleLEDByDirection(float directionDeg, string colorHex = "00FFFF")
    {
        int ledIndex = Mathf.RoundToInt(directionDeg / 20f);
        ledIndex = Mathf.Clamp(ledIndex, 0, 18);

        for (int i = 0; i < 19; i++)
        {
            fullLedState[i] = (i == ledIndex) ? colorHex : "off";
        }
    }
}
