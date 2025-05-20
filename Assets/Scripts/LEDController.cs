using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine.InputSystem.XR;

public class LEDController : MonoBehaviour
{
    public WeatherController controller;
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

    private void Start()
    {
    }

    private void Update()
    {
        UpdateRiverFlow();
        /*for (int i = 19; i <= 29; i++)
        {
            SetLED(i, controller.isDaytime ? "off" : "FFFFFF");
        }

        SetLED(34, controller.isDaytime ? "off" : "FFFFFF");
        SetLED(35, controller.isDaytime ? "off" : "FFFFFF");
        SetLED(36, controller.isDaytime ? "off" : "FFFFFF");*/

        if (isOverridingDirection)
        {
            UpdateOverrideDirectionLEDs();
        }

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


    public void LightSingleLEDByDirection(float directionDeg, string colorHex = "fcb400")
    {
        int ledIndex = Mathf.RoundToInt(directionDeg / 20f);
        ledIndex = Mathf.Clamp(ledIndex, 0, 18);

        for (int i = 0; i < 18; i++)
        {
            fullLedState[17 - i] = (i == ledIndex) ? colorHex : "off";
        }
    }

    [SerializeField] private int riverStart = 30;
    [SerializeField] private int riverLength = 4;
    [SerializeField] private float waveSpeed = 4.6f;
    [SerializeField] private float waveRange = 1f;

    private float riverTime = 0f;

    private void UpdateRiverFlow()
    {
        riverTime += Time.deltaTime * waveSpeed;

        for (int i = 0; i < riverLength; i++)
        {
            float phase = (float)i / riverLength;
            float brightness = 1f - waveRange * (0.5f + 0.5f * Mathf.Sin(riverTime + phase * Mathf.PI * 2));
            fullLedState[riverStart + i] = DimColor("FFFFFF", brightness);
        }
    }

    public void UpdateCityLightsByDirection(float directionDeg)
    {
        float hour = Mathf.Clamp01(directionDeg / 360f) * 24f;
        float baseBrightness = GetBaseCityBrightness(hour);

        for (int i = 19; i <= 29; i++)
            fullLedState[i] = ComputeBreathingWhite(i, baseBrightness);

        for (int i = 34; i <= 36; i++)
            fullLedState[i] = ComputeBreathingWhite(i, baseBrightness);
    }

    private float GetBaseCityBrightness(float hour)
    {
        if (hour < 6f) return Mathf.Lerp(0.2f, 0.4f, hour / 6f);
        if (hour < 8f) return Mathf.Lerp(0.4f, 1.0f, (hour - 6f) / 2f);
        if (hour < 17f) return 1.0f;
        if (hour < 19f) return Mathf.Lerp(1.0f, 0.5f, (hour - 17f) / 2f);
        return Mathf.Lerp(0.5f, 0.3f, (hour - 19f) / 5f);
    }

    private string ComputeBreathingWhite(int index, float baseBrightness)
    {
        float offset = index * 0.618f;
        float pulse = Mathf.Sin(Time.time * 2.0f + offset) * 0.1f + 0.9f;
        float finalBrightness = Mathf.Clamp01(baseBrightness * pulse);
        return DimColor("FFFFFF", finalBrightness);
    }

    private bool isOverridingDirection = false;
    private float overrideFlashTimer = 0f;
    private float overrideFlashDuration = 0.2f;

    public void OverrideDirectionLEDs(bool active)
    {
        if (active && !isOverridingDirection)
        {
            isOverridingDirection = true;
            overrideFlashTimer = overrideFlashDuration;
        }
        else if (!active && isOverridingDirection)
        {
            isOverridingDirection = false;
        }
    }

    private void UpdateOverrideDirectionLEDs()
    {
        overrideFlashTimer -= updateInterval;

        string color = "00FFFF";
        string flashColor = (Mathf.FloorToInt(Time.time * 20f) % 2 == 0) ? color : "off";

        for (int i = 0; i < 18; i++)
        {
            fullLedState[i] = (overrideFlashTimer > 0f) ? flashColor : color;
        }
    }
}