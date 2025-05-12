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

    [Header("Season Colors")]
    [SerializeField] private Color springColor = new Color(0.5f, 1f, 0.5f); // Lime
    [SerializeField] private Color summerColor = new Color(1f, 1f, 0.5f); // Yellow
    [SerializeField] private Color autumnColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color winterColor = new Color(0.8f, 0.9f, 1f); // Light ice blue

    [Header("Rain Settings")]
    [SerializeField] private ParticleSystem rainParticleSystem;
    [SerializeField] private AudioSource rainAudioSource;
    [SerializeField, Range(1f, 20f)] private float maxRainIntensity = 20f;
    [SerializeField, Range(1f, 20f)] private float minRainIntensity = 1f;
    [SerializeField, Range(0.1f, 1f)] private float maxRainVolume = 1f;
    [SerializeField, Range(0.1f, 1f)] private float minRainVolume = 0.1f;

    [Header("Rotation Controls")]
    [SerializeField] private bool isRotatingRainIntensity = false;
    [SerializeField] private bool isRotatingSeason = false;

    private Season currentSeason = Season.Spring;

    private enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

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
        HandleRainIntensityRotation();
        HandleSeasonRotation();

        string tempLabel = GetTempString(hour);

        // Adjust temperature based on season
        tempLabel = AdjustSeasonTemp(tempLabel);

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
            // Only allow snow toggle if not in winter (winter handles snow automatically)
            if (currentSeason != Season.Winter)
            {
                SetSnow(!isSnowMode);
            }
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

    private string AdjustSeasonTemp(string tempString)
    {
        Match match = Regex.Match(tempString, @"\d+");
        if (match.Success)
        {
            int baseTemp = int.Parse(match.Value);
            int adjustedTemp = baseTemp;

            switch (currentSeason)
            {
                case Season.Spring:
                    adjustedTemp = baseTemp - 2;
                    break;
                case Season.Summer:
                    adjustedTemp = baseTemp + 5;
                    break;
                case Season.Autumn:
                    adjustedTemp = baseTemp - 3;
                    break;
                case Season.Winter:
                    adjustedTemp = baseTemp - 10;
                    break;
            }

            // Additional temperature adjustments based on time of day
            float cameraY = NormalizeAngle(mainCamera.transform.eulerAngles.y);
            float normalized = (cameraY + 180f) % 360f;
            int hour = Mathf.FloorToInt(normalized / 15f);

            // Cooler at night, warmer during day
            if (hour >= 22 || hour < 5) // Night hours
            {
                adjustedTemp -= 3;
            }
            else if (hour >= 12 && hour < 15) // Peak day hours
            {
                adjustedTemp += 2;
            }

            return $"TEMP: {adjustedTemp}'C";
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

    private void HandleRainIntensityRotation()
    {
        if (Input.GetKeyDown(KeyCode.Comma))
        {
            isRotatingRainIntensity = !isRotatingRainIntensity;
            if (isRotatingRainIntensity)
            {
                SetRain(true);
            }
        }

        if (isRotatingRainIntensity && rainParticleSystem != null)
        {
            float cameraY = NormalizeAngle(mainCamera.transform.eulerAngles.y);
            float normalized = (cameraY + 180f) % 360f;
            float intensity = Mathf.Lerp(minRainIntensity, maxRainIntensity, normalized / 360f);
            
            var main = rainParticleSystem.main;
            main.simulationSpeed = intensity;

            // Adjust rain audio volume
            if (rainAudioSource != null)
            {
                rainAudioSource.volume = Mathf.Lerp(minRainVolume, maxRainVolume, normalized / 360f);
            }
        }
    }

    private void HandleSeasonRotation()
    {
        if (Input.GetKeyDown(KeyCode.Period))
        {
            isRotatingSeason = !isRotatingSeason;
        }

        if (isRotatingSeason)
        {
            float cameraY = NormalizeAngle(mainCamera.transform.eulerAngles.y);
            float normalized = (cameraY + 180f) % 360f;
            
            // Update season based on camera rotation
            float seasonValue = normalized / 360f;
            if (seasonValue < 0.25f)
            {
                UpdateSeason(Season.Spring);
            }
            else if (seasonValue < 0.5f)
            {
                UpdateSeason(Season.Summer);
            }
            else if (seasonValue < 0.75f)
            {
                UpdateSeason(Season.Autumn);
            }
            else
            {
                UpdateSeason(Season.Winter);
            }
        }
    }

    private void UpdateSeason(Season newSeason)
    {
        if (currentSeason == newSeason) return;
        
        // If we're leaving winter, turn off snow
        if (currentSeason == Season.Winter && newSeason != Season.Winter)
        {
            SetSnow(false);
        }
        
        currentSeason = newSeason;
        Color targetColor = GetSeasonColor(newSeason);
        
        if (directionalLight != null)
        {
            directionalLight.color = targetColor;
        }

        // Handle snow for winter
        if (newSeason == Season.Winter)
        {
            SetSnow(true);
        }
    }

    private Color GetSeasonColor(Season season)
    {
        switch (season)
        {
            case Season.Spring:
                return springColor;
            case Season.Summer:
                return summerColor;
            case Season.Autumn:
                return autumnColor;
            case Season.Winter:
                return winterColor;
            default:
                return Color.white;
        }
    }
}