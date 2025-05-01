using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;

public class WeatherController : MonoBehaviour
{
    [Header("Skyboxes & Camera")]
    [SerializeField] private GameObject rainObject;
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material rainSkybox;
    [SerializeField] private Material sunriseSkybox;
    [SerializeField] private Material sunsetSkybox;
    [SerializeField] private Material galaxySkybox;
    [SerializeField] private Camera mainCamera;
    [SerializeField, Range(10f, 90f)] private float transitionAngle = 30f;
    [SerializeField] private float transitionSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource dayAmbience;
    [SerializeField] private AudioSource nightAmbience;
    [SerializeField] private AudioSource rainAmbience;
    [SerializeField] private AudioSource earthquakeAmbience;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI weatherText;

    [Header("Terrain")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainLayer normalLayer;
    [SerializeField] private TerrainLayer snowLayer;

    [Header("Particles")]
    [SerializeField] private GameObject snowParticleObject;

    private bool isSnowMode = false;
    private bool isGalaxyMode = false;

    private Material currentSkybox;
    private Material targetSkybox;
    private float transitionProgress = 0f;
    private bool isDaytime = true;

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        currentSkybox = RenderSettings.skybox;
        UpdateAmbience();
        ApplyTerrainLayer(normalLayer);

        if (snowParticleObject != null)
            snowParticleObject.SetActive(false);
    }

    private void Update()
    {
        float cameraY = NormalizeAngle(mainCamera.transform.eulerAngles.y);
        float normalized = (cameraY + 180f) % 360f;
        int hour = Mathf.FloorToInt(normalized / 15f);
        string timeLabel = GetTimeString(hour);

        HandleRainToggle();
        HandleSnowToggle();
        HandleGalaxyToggle();

        string tempLabel = GetTempString(hour);

        if (isSnowMode)
        {
            tempLabel = AdjustRainTemp(tempLabel, -10);
        }
        else if (rainObject.activeSelf)
        {
            tempLabel = AdjustRainTemp(tempLabel, -5);
        }

        if (rainObject.activeSelf && !isGalaxyMode)
        {
            StartTransition(rainSkybox);
            if (!isGalaxyMode) UpdateSkyboxTransition();
            UpdateWeatherText("D.O.M.E.", timeLabel, tempLabel);
            return;
        }

        bool wasDaytime = isDaytime;
        UpdateTimeWeatherAndSkybox(hour, timeLabel, tempLabel);

        if (wasDaytime != isDaytime)
        {
            UpdateAmbience();
        }

        if (!isGalaxyMode)
        {
            UpdateSkyboxTransition();
        }
    }

    private float NormalizeAngle(float angle)
    {
        return (angle > 180f) ? angle - 360f : angle;
    }

    private void HandleRainToggle()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            rainObject.SetActive(!rainObject.activeSelf);
            UpdateAmbience();
        }
    }

    private void HandleSnowToggle()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            isSnowMode = !isSnowMode;
            ApplyTerrainLayer(isSnowMode ? snowLayer : normalLayer);

            if (snowParticleObject != null)
                snowParticleObject.SetActive(isSnowMode);
        }
    }

    private void HandleGalaxyToggle()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            isGalaxyMode = !isGalaxyMode;

            if (isGalaxyMode)
            {
                RenderSettings.skybox = galaxySkybox;
            }
            else
            {
                // Resume normal skybox behavior
                StartTransition(currentSkybox);
            }
        }
    }

    private void UpdateTimeWeatherAndSkybox(int hour, string timeLabel, string tempLabel)
    {
        if (!isGalaxyMode)
        {
            if (hour >= 5 && hour < 7)
            {
                StartTransition(sunriseSkybox);
                isDaytime = true;
            }
            else if (hour >= 7 && hour < 17)
            {
                StartTransition(daySkybox);
                isDaytime = true;
            }
            else if (hour >= 17 && hour < 19)
            {
                StartTransition(sunsetSkybox);
                isDaytime = false;
            }
            else
            {
                StartTransition(nightSkybox);
                isDaytime = false;
            }
        }

        UpdateWeatherText("D.O.M.E.", timeLabel, tempLabel);
    }

    private string GetTimeString(int hour)
    {
        int displayHour = (hour % 12 == 0) ? 12 : hour % 12;
        string ampm = (hour < 12) ? "AM" : "PM";
        return $"TIME: {displayHour:00}:00 {ampm}";
    }

    private string GetTempString(int hour)
    {
        int[] tempByHour = {
            10, 10, 10, 12, 14, 16, 18, 20, 23, 25, 27, 28,
            30, 30, 28, 26, 24, 22, 20, 18, 16, 14, 12, 10
        };
        int temp = tempByHour[hour % 24];
        return $"TEMP: {temp}'C";
    }

    private string AdjustRainTemp(string tempString, int delta)
    {
        Match match = Regex.Match(tempString, @"\d+");
        if (match.Success)
        {
            int temp = int.Parse(match.Value) + delta;
            return $"TEMP: {temp}'C";
        }
        return tempString;
    }

    private void UpdateWeatherText(string header, string time, string temp)
    {
        weatherText.text = $"{header}\n{time}\n{temp}";
    }

    private void StartTransition(Material newSkybox)
    {
        if (targetSkybox != newSkybox)
        {
            currentSkybox = RenderSettings.skybox;
            targetSkybox = newSkybox;
            transitionProgress = 0f;
        }
    }

    private void UpdateSkyboxTransition()
    {
        if (targetSkybox == null) return;

        transitionProgress += Time.deltaTime * transitionSpeed;
        if (transitionProgress >= 1f)
        {
            RenderSettings.skybox = targetSkybox;
            currentSkybox = targetSkybox;
            targetSkybox = null;
            return;
        }

        Material transitionMat = new Material(currentSkybox);
        transitionMat.Lerp(currentSkybox, targetSkybox, transitionProgress);
        RenderSettings.skybox = transitionMat;
    }

    private void UpdateAmbience()
    {
        dayAmbience.Stop();
        nightAmbience.Stop();
        rainAmbience.Stop();
        earthquakeAmbience.Stop();

        if (rainObject.activeSelf)
        {
            rainAmbience.Play();
        }
        else if (isDaytime)
        {
            dayAmbience.Play();
        }
        else
        {
            nightAmbience.Play();
        }
    }

    private void ApplyTerrainLayer(TerrainLayer layer)
    {
        TerrainData data = terrain.terrainData;

        data.terrainLayers = new TerrainLayer[] { layer };

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        float[,,] map = new float[width, height, 1];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map[x, y, 0] = 1f;
            }
        }

        data.SetAlphamaps(0, 0, map);
    }
}
