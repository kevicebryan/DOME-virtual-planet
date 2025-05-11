using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.UI;

public class WeatherController : MonoBehaviour
{
    [Header("Skyboxes & Camera")] [SerializeField]
    private GameObject rainObject;

    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material rainSkybox;
    [SerializeField] private Material sunriseSkybox;
    [SerializeField] private Material sunsetSkybox;
    [SerializeField] private Material galaxySkybox;
    [SerializeField] private Material auroraSkybox;
    [SerializeField] private Camera mainCamera;
    [SerializeField, Range(10f, 90f)] private float transitionAngle = 30f;
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private Light directionalLight;

    [Header("Audio")] [SerializeField] private AudioSource dayAmbience;
    [SerializeField] private AudioSource nightAmbience;
    [SerializeField] private AudioSource rainAmbience;
    [SerializeField] private AudioSource earthquakeAmbience;
    [SerializeField] private AudioSource auroraAmbience;

    [Header("UI")] [SerializeField] private TextMeshProUGUI weatherText;
    [SerializeField] private Image compassImage; // Reference to UI Image

    [Header("Terrain")] [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainLayer normalLayer;
    [SerializeField] private TerrainLayer snowLayer;

    [Header("Particles")] [SerializeField] private GameObject snowParticleObject;

    private bool isSnowMode = false;
    private bool isGalaxyMode = false;
    private bool isAuroraMode = false;

    private Material currentSkybox;
    private Material targetSkybox;
    private float transitionProgress = 0f;
    private bool isDaytime = true;
    private float previousLightIntensity = 8f;

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (directionalLight == null)
        {
            directionalLight = FindObjectOfType<Light>();
        }

        currentSkybox = RenderSettings.skybox;
        UpdateAmbience();
        ApplyTerrainLayer(normalLayer);

        if (snowParticleObject != null)
            snowParticleObject.SetActive(false);
    }

    public string CalculateTime()
    {
        float cameraY = NormalizeAngle(mainCamera.transform.eulerAngles.y);
        float normalized = (cameraY + 180f) % 360f;
        int hour = Mathf.FloorToInt(normalized / 15f);
        string timeLabel = GetTimeString(hour);
        return timeLabel;
    }

    private void Update()
    {
        float cameraY = NormalizeAngle(mainCamera.transform.eulerAngles.y);
        float normalized = (cameraY + 180f) % 360f;
        int hour = Mathf.FloorToInt(normalized / 15f);
        string timeLabel = GetTimeString(hour);

        // Update compass rotation
        if (compassImage != null)
        {
            compassImage.transform.rotation = Quaternion.Euler(0, 0, -mainCamera.transform.eulerAngles.y);
        }

        HandleRainToggle();
        HandleSnowToggle();
        HandleGalaxyToggle();
        HandleAuroraToggle();

        string tempLabel = GetTempString(hour);

        if (isSnowMode)
        {
            tempLabel = AdjustRainTemp(tempLabel, -10);
        }
        else if (rainObject.activeSelf)
        {
            tempLabel = AdjustRainTemp(tempLabel, -5);
        }

        if (rainObject.activeSelf && !isGalaxyMode && !isAuroraMode)
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

        if (!isGalaxyMode && !isAuroraMode)
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
            SetRain(!rainObject.activeSelf);
        }
    }

    public void SetRain(bool rain)
    {
        rainObject.SetActive(rain);
        UpdateAmbience();
    }

    public bool isRain()
    {
        return rainObject.activeSelf;
    }

    private void HandleSnowToggle()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            SetSnow(!isSnowMode);
        }
    }

    public void SetSnow(bool snow)
    {
        isSnowMode = snow;
        ApplyTerrainLayer(isSnowMode ? snowLayer : normalLayer);

        if (snowParticleObject != null)
            snowParticleObject.SetActive(isSnowMode);

        UpdateAmbience();
    }

    public bool isSnow()
    {
        return isSnowMode;
    }

    private void HandleGalaxyToggle()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            SetGalaxyMode(!isGalaxyMode);
        }
    }

    public bool isGalaxyModeActive()
    {
        return isGalaxyMode;
    }

    public bool isAuroraActive()
    {
        return isAuroraMode;
    }

    public void SetGalaxyMode(bool galaxy)
    {
        isGalaxyMode = galaxy;
        isAuroraMode = false;

        if (isGalaxyMode)
        {
            RenderSettings.skybox = galaxySkybox;
            rainObject.SetActive(false);
            if (snowParticleObject != null)
                snowParticleObject.SetActive(false);
            isSnowMode = false;
        }
        else
        {
            // Resume normal skybox behavior
            StartTransition(currentSkybox);
        }

        UpdateAmbience();
    }

    private void HandleAuroraToggle()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            SetAurora(!isAuroraMode);
        }
    }

    public void EnableDefaultBackground()
    {
        SetGalaxyMode(false);
        SetAurora(false);
    }

    public void SetAurora(bool aurora)
    {
        isAuroraMode = aurora;
        isGalaxyMode = false;

        if (isAuroraMode)
        {
            previousLightIntensity = directionalLight.intensity;
            RenderSettings.skybox = auroraSkybox;
            rainObject.SetActive(false);
            if (snowParticleObject != null)
                snowParticleObject.SetActive(false);
            isSnowMode = false;
            directionalLight.intensity = 0.1f;
        }
        else
        {
            // Resume normal skybox behavior
            StartTransition(currentSkybox);
            directionalLight.intensity = previousLightIntensity;
        }

        UpdateAmbience();
    }

    private void UpdateTimeWeatherAndSkybox(int hour, string timeLabel, string tempLabel)
    {
        if (!isGalaxyMode && !isAuroraMode)
        {
            if (hour >= 5 && hour < 7)
            {
                StartTransition(sunriseSkybox);
                isDaytime = true;
                directionalLight.intensity = Mathf.Lerp(0.25f, 8f, (hour - 5f) / 2f);
            }
            else if (hour >= 7 && hour < 17)
            {
                StartTransition(daySkybox);
                isDaytime = true;
                directionalLight.intensity = 8f;
            }
            else if (hour >= 17 && hour < 19)
            {
                StartTransition(sunsetSkybox);
                isDaytime = false;
                directionalLight.intensity = Mathf.Lerp(8f, 0.25f, (hour - 17f) / 2f);
            }
            else
            {
                StartTransition(nightSkybox);
                isDaytime = false;
                directionalLight.intensity = 0.25f;
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
        int[] tempByHour =
        {
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

    public bool interacted = false;

    private void UpdateAmbience()
    {
        interacted = true;
        dayAmbience.Stop();
        nightAmbience.Stop();
        rainAmbience.Stop();
        earthquakeAmbience.Stop();
        auroraAmbience.Stop();

        if (isAuroraMode)
        {
            auroraAmbience.Play();
        }
        else if (rainObject.activeSelf)
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