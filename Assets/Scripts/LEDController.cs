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

        if (AIAgent.IsRecording || isOverridingDirection || directionOverrideLerp > 0f)
        {
            UpdateOverrideDirectionLEDs(AIAgent.IsRecording);
        }

        if (controller.isAuroraMode || controller.isGalaxyMode)
        {
            UpdateCityLightsByDirection(0, true);
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
                // Debug.Log("LED sent: " + www.downloadHandler.text);
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
        int ledIndex = Mathf.RoundToInt(directionDeg / 18.94f);
        ledIndex = Mathf.Clamp(ledIndex, 0, 18);

        for (int i = 0; i < 19; i++)
        {
            fullLedState[18 - i] = (i == ledIndex) ? colorHex : "off";
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

    public void UpdateCityLightsByDirection(float directionDeg, bool forceLight = false)
    {
        float hour = Mathf.Clamp01(directionDeg / 360f) * 24f;
        float baseBrightness = forceLight ? 1f : GetBaseCityBrightness(hour);

        bool rain = controller.isRain();
        bool snow = controller.isSnow();

        for (int i = 19; i <= 29; i++)
        {
            fullLedState[i] = ComputeWeatherCityLight(i, baseBrightness, rain, snow);
        }

        for (int i = 34; i <= 36; i++)
        {
            fullLedState[i] = ComputeWeatherCityLight(i, baseBrightness, rain, snow);
        }
    }

    private string ComputeWeatherCityLight(int index, float baseBrightness, bool isRain, bool isSnow)
    {
        if (isRain)
        {
            float flash = Mathf.PingPong(Time.time * 3f + index * 0.3f, 1f); // 范围 [0~1]
            float brightness = flash > 0.5f ? baseBrightness : 0f;
            return DimColor("99CCFF", brightness);
        }

        if (isSnow)
        {
            float flash = Mathf.PingPong(Time.time * 1.5f + index * 0.2f, 1f);
            float brightness = flash > 0.7f ? baseBrightness : 0.3f * baseBrightness;
            return DimColor("EEEEEE", brightness);
        }

        return ComputeBreathingWhite(index, baseBrightness);
    }


    private float GetBaseCityBrightness(float hour)
    {
        if (hour < 6f) return 0f;
        if (hour < 8f) return Mathf.Lerp(0f, 1.0f, (hour - 6f) / 2f);
        if (hour < 17f) return 1.0f;
        if (hour < 19f) return Mathf.Lerp(1.0f, 0f, (hour - 17f) / 2f);
        return 0f;
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

    private float directionOverrideLerp = 0f;
    public float directionOverrideLerpSpeed = 0.5f;

    private string GetSeasonColor(WeatherController.Season season)
    {
        switch (season)
        {
            case WeatherController.Season.Spring: return "00FF00";
            case WeatherController.Season.Summer: return "FF9900";
            case WeatherController.Season.Autumn: return "FF2200";
            case WeatherController.Season.Winter: return "66CCFF";
            default: return "FFFFFF";
        }
    }

    public bool listening = false;
    public bool thinking = false;

    private void UpdateOverrideDirectionLEDs(bool pushToTalkMode = false)
    {
        float target = isOverridingDirection || pushToTalkMode || AIAgent.IsRecording ? 1f : 0f;
        directionOverrideLerp =
            Mathf.MoveTowards(directionOverrideLerp, target, directionOverrideLerpSpeed * updateInterval);

        string baseColor;

        if (AIAgent.IsRecording)
        {
            if (thinking)
                baseColor = "0000FF";
            else if (listening)
                baseColor = "FF0000";
            else
                baseColor = GetSeasonColor(controller.currentSeason); // fallback
        }
        else if (pushToTalkMode)
        {
            baseColor = "FF0000";
        }
        else
        {
            baseColor = GetSeasonColor(controller.currentSeason);
        }

        for (int i = 0; i <= 18; i++)
        {
            float offset = i * 0.5f;
            float breathSpeed = 1.5f;
            float breathAmplitude = 0.6f;
            float breathBase = 0.4f;

            float pulse = Mathf.Sin(Time.time * breathSpeed + offset) * breathAmplitude + breathBase;
            float finalBrightness = directionOverrideLerp * pulse;
            fullLedState[i] = DimColor(baseColor, finalBrightness);
        }

        if (!AIAgent.IsRecording && pushToTalkMode)
        {
            OverrideDirectionLEDs(false);
        }
    }
}