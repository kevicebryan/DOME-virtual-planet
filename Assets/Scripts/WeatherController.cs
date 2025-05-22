using System;
using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.UI;

public class WeatherController : MonoBehaviour
{
    [Header("Skyboxes & Camera")] [SerializeField]
    public GyroReader gyroReader;

    public GameObject rainObject;
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material rainDaySkybox; // Renamed from rainSkybox
    [SerializeField] private Material rainNightSkybox; // New rain night skybox
    [SerializeField] private Material sunriseSkybox;
    [SerializeField] private Material sunsetSkybox;
    [SerializeField] private Material galaxySkybox;
    [SerializeField] private Material auroraSkybox;
    [SerializeField] private Camera mainCamera;
    [SerializeField, Range(10f, 90f)] private float transitionAngle = 30f;
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private Light directionalLight;

    [Header("Trees")] [SerializeField] private GameObject springTrees;
    [SerializeField] private GameObject autumnTrees;

    [Header("Audio")] [SerializeField] private AudioSource dayAmbience;
    [SerializeField] private AudioSource nightAmbience;
    [SerializeField] private AudioSource rainAmbience;
    [SerializeField] private AudioSource earthquakeAmbience;
    [SerializeField] private AudioSource auroraAmbience;

    [Header("UI")] [SerializeField] private TextMeshProUGUI weatherText;
    [SerializeField] private Image compassImage; // Reference to UI Image
    [SerializeField] private TextMeshProUGUI rainFeedbackText; // Separate text for rain feedback
    [SerializeField] private GameObject seasonIndicator; // Reference to SeasonIndicator GameObject
    [SerializeField] private float textFadeSpeed = 2f; // Speed of text fade in/out

    [Header("Terrain")] [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainLayer normalLayer;
    [SerializeField] private TerrainLayer snowLayer;
    [SerializeField] private TerrainLayer autumnLayer;
    [SerializeField] private TerrainLayer springLayer;

    [Header("Particles")] [SerializeField] private GameObject snowParticleObject;

    [Header("Season Colors")] [SerializeField]
    private Color springColor = new Color(0.5f, 1f, 0.5f); // Lime

    [SerializeField] private Color summerColor = new Color(1f, 1f, 0.5f); // Yellow
    [SerializeField] private Color autumnColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color winterColor = new Color(0.8f, 0.9f, 1f); // Light ice blue

    [Header("Rain Settings")] [SerializeField]
    private ParticleSystem rainParticleSystem;

    [SerializeField] private AudioSource rainAudioSource;
    [SerializeField, Range(1f, 20f)] private float maxRainIntensity = 20f;
    [SerializeField, Range(1f, 20f)] private float minRainIntensity = 1f;
    [SerializeField, Range(0.1f, 1f)] private float maxRainVolume = 1f;
    [SerializeField, Range(0.1f, 1f)] private float minRainVolume = 0.1f;

    [Header("Rotation Controls")] [SerializeField]
    private bool isRotatingRainIntensity = false;

    [SerializeField] public bool isRotatingSeason = false;
    [SerializeField] private float rotationModeCameraY = 1.5f; // New field for camera Y position in rotation mode
    [SerializeField] private float cameraTransitionDuration = 0.3f; // Duration of camera movement in seconds

    private Vector3 originalCameraPosition;
    private bool wasInRotationMode = false;
    private float cameraTransitionProgress = 1f;
    private Vector3 targetCameraPosition;

    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public Season currentSeason = Season.Spring;
    private Color currentSeasonColor;
    private Color targetSeasonColor;
    private float seasonTransitionProgress = 1f;
    private const float SEASON_TRANSITION_SPEED = 2f;

    public bool isSnowMode = false;
    public bool isGalaxyMode = false;
    public bool isAuroraMode = false;

    private Material currentSkybox;
    private Material targetSkybox;
    private float transitionProgress = 0f;
    public bool isDaytime = true;
    private float previousLightIntensity = 8f;

    private float rainTextAlpha = 0f;
    private float seasonTextAlpha = 0f;
    private bool isRainTextFading = false;
    private bool isSeasonTextFading = false;

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        originalCameraPosition = mainCamera.transform.position;
        targetCameraPosition = originalCameraPosition;

        if (directionalLight == null)
        {
            directionalLight = FindObjectOfType<Light>();
        }

        currentSkybox = RenderSettings.skybox;
        currentSeasonColor = GetSeasonColor(currentSeason);
        targetSeasonColor = currentSeasonColor;
        UpdateAmbience();
        ApplyTerrainLayer(normalLayer);
        UpdateTrees(currentSeason);

        if (snowParticleObject != null)
            snowParticleObject.SetActive(false);

        // Initialize feedback texts with zero alpha
        if (rainFeedbackText != null)
        {
            Color textColor = rainFeedbackText.color;
            textColor.a = 0f;
            rainFeedbackText.color = textColor;
            rainFeedbackText.gameObject.SetActive(false);
        }
    }

    private void UpdateTrees(Season season)
    {
        if (season == Season.Autumn || season == Season.Winter)
        {
            if (springTrees != null) springTrees.SetActive(false);
            if (autumnTrees != null) autumnTrees.SetActive(true);
        }
        else
        {
            if (springTrees != null) springTrees.SetActive(true);
            if (autumnTrees != null) autumnTrees.SetActive(false);
        }
    }

    public string CalculateTime()
    {
        float cameraY = NormalizeAngle(gyroReader.GetRotY(0));
        float normalized = 360 - ((cameraY + 180f) % 360f);
        int hour = Mathf.FloorToInt(normalized / 15f);
        string timeLabel = GetTimeString(hour);
        return timeLabel;
    }

    private void Update()
    {
        bool isInRotationMode = gyroReader.currentMode == 2 || gyroReader.currentMode == 1;
        isRotatingRainIntensity = gyroReader.currentMode == 2;

        // Handle camera position changes when rotation mode is toggled
        if (isInRotationMode && !wasInRotationMode)
        {
            // Entering rotation mode - start transition to lower position
            targetCameraPosition = new Vector3(mainCamera.transform.position.x, rotationModeCameraY,
                mainCamera.transform.position.z);
            cameraTransitionProgress = 0f;
        }
        else if (!isInRotationMode && wasInRotationMode)
        {
            // Exiting rotation mode - start transition back to original position
            targetCameraPosition = originalCameraPosition;
            cameraTransitionProgress = 0f;
        }

        wasInRotationMode = isInRotationMode;

        // Update camera position lerp
        if (cameraTransitionProgress < 1f)
        {
            cameraTransitionProgress += Time.deltaTime / cameraTransitionDuration;
            if (cameraTransitionProgress > 1f) cameraTransitionProgress = 1f;

            Vector3 currentPosition = mainCamera.transform.position;
            Vector3 startPosition = wasInRotationMode
                ? originalCameraPosition
                : new Vector3(currentPosition.x, rotationModeCameraY, currentPosition.z);
            mainCamera.transform.position = Vector3.Lerp(startPosition, targetCameraPosition, cameraTransitionProgress);
        }

        float cameraY = NormalizeAngle(gyroReader.GetRotY(0));
        float normalized = 360 - NormalizeAngle(cameraY + 180f);
        int hour = Mathf.FloorToInt(normalized / 15f);
        string timeLabel = GetTimeString(hour);

        // Update compass rotation only when in clock mode (mode 0)
        if (compassImage != null && gyroReader.currentMode == 0)
        {
            compassImage.transform.rotation = Quaternion.Euler(0, 0, mainCamera.transform.eulerAngles.y);
        }

        HandleRainToggle();
        HandleSnowToggle();
        HandleGalaxyToggle();
        HandleAuroraToggle();
        HandleRainIntensityRotation();
        HandleSeasonRotation();

        // Update season color transition
        if (seasonTransitionProgress < 1f)
        {
            seasonTransitionProgress += Time.deltaTime * SEASON_TRANSITION_SPEED;
            if (seasonTransitionProgress >= 1f)
            {
                seasonTransitionProgress = 1f;
                currentSeasonColor = targetSeasonColor;
            }

            if (directionalLight != null)
            {
                directionalLight.color = Color.Lerp(currentSeasonColor, targetSeasonColor, seasonTransitionProgress);
            }
        }

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
            // Update isDaytime based on hour
            isDaytime = (hour >= 5 && hour < 19);

            // Use the appropriate rain skybox based on time of day
            StartTransition(isDaytime ? rainDaySkybox : rainNightSkybox);
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

        // Update text fade states
        UpdateTextFade();
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0) angle += 360f;
        return angle;
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
        ApplyTerrainLayer(isSnowMode ? snowLayer : GetSeasonTerrainLayer());

        if (snowParticleObject != null)
            snowParticleObject.SetActive(isSnowMode);

        UpdateAmbience();
    }

    private TerrainLayer GetSeasonTerrainLayer()
    {
        switch (currentSeason)
        {
            case Season.Spring:
                return springLayer;
            case Season.Summer:
                return normalLayer;
            case Season.Autumn:
                return autumnLayer;
            case Season.Winter:
                return snowLayer;
            default:
                return normalLayer;
        }
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
            if (rainObject.activeSelf)
            {
                // Handle rain skyboxes based on time of day
                if (hour >= 5 && hour < 19) // Daytime hours (5 AM to 7 PM)
                {
                    StartTransition(rainDaySkybox);
                    isDaytime = true;
                    directionalLight.intensity = Mathf.Lerp(0.25f, 8f, (hour - 5f) / 2f);
                }
                else // Nighttime hours (7 PM to 5 AM)
                {
                    StartTransition(rainNightSkybox);
                    isDaytime = false;
                    directionalLight.intensity = 0.25f;
                }
            }
            else
            {
                if (hour >= 4 && hour < 7) // Sunrise (4 AM to 7 AM)
                {
                    StartTransition(sunriseSkybox);
                    isDaytime = true;
                    directionalLight.intensity = Mathf.Lerp(0.25f, 8f, (hour - 4f) / 3f);
                }
                else if (hour >= 7 && hour < 16) // Day (7 AM to 4 PM)
                {
                    StartTransition(daySkybox);
                    isDaytime = true;
                    directionalLight.intensity = 8f;
                }
                else if (hour >= 16 && hour < 19) // Sunset (4 PM to 7 PM)
                {
                    StartTransition(sunsetSkybox);
                    isDaytime = false;
                    directionalLight.intensity = Mathf.Lerp(8f, 0.25f, (hour - 16f) / 3f);
                }
                else // Night (7 PM to 4 AM)
                {
                    StartTransition(nightSkybox);
                    isDaytime = false;
                    directionalLight.intensity = 0.25f;
                }
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

            // Get current hour for time-based adjustments
            float cameraY = NormalizeAngle(gyroReader.GetRotY(1));
            float normalized = (cameraY + 180f) % 360f;
            int hour = Mathf.FloorToInt(normalized / 15f);

            // Base temperature adjustments for time of day
            // Peak temperature around 2-3 PM, coldest around 4-5 AM
            if (hour >= 2 && hour < 5) // Early morning (2-5 AM)
            {
                adjustedTemp -= 5; // Coldest part of the day
            }
            else if (hour >= 5 && hour < 8) // Morning (5-8 AM)
            {
                adjustedTemp -= 3; // Still cool but warming up
            }
            else if (hour >= 8 && hour < 11) // Late morning (8-11 AM)
            {
                adjustedTemp -= 1; // Warming up
            }
            else if (hour >= 11 && hour < 14) // Early afternoon (11 AM - 2 PM)
            {
                adjustedTemp += 2; // Warmest part of the day
            }
            else if (hour >= 14 && hour < 17) // Late afternoon (2-5 PM)
            {
                adjustedTemp += 1; // Still warm but cooling down
            }
            else if (hour >= 17 && hour < 20) // Evening (5-8 PM)
            {
                adjustedTemp -= 1; // Cooling down
            }
            else if (hour >= 20 && hour < 23) // Night (8-11 PM)
            {
                adjustedTemp -= 3; // Getting colder
            }
            else // Late night (11 PM - 2 AM)
            {
                adjustedTemp -= 4; // Cold
            }

            // Season-based adjustments
            switch (currentSeason)
            {
                case Season.Spring:
                    adjustedTemp -= 2; // Mild temperatures
                    break;
                case Season.Summer:
                    adjustedTemp += 5; // Hotter temperatures
                    break;
                case Season.Autumn:
                    adjustedTemp -= 3; // Cooler temperatures
                    break;
                case Season.Winter:
                    adjustedTemp -= 10; // Cold temperatures
                    break;
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

        // Increase transition speed for more responsive changes
        transitionProgress += Time.deltaTime * (transitionSpeed * 2f);
        if (transitionProgress >= 1f)
        {
            RenderSettings.skybox = targetSkybox;
            currentSkybox = targetSkybox;
            targetSkybox = null;
            return;
        }

        // Create a new material for the transition
        Material transitionMat = new Material(currentSkybox);
        // Use a modified smoothstep for faster but still smooth transitions
        float smoothProgress = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, transitionProgress));
        transitionMat.Lerp(currentSkybox, targetSkybox, smoothProgress);
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
            Debug.Log("Comma key pressed - Starting rain intensity rotation");
            isRotatingRainIntensity = true;
            if (rainFeedbackText != null)
            {
                Debug.Log("Rain feedback text found - Activating and starting fade");
                rainFeedbackText.gameObject.SetActive(true);
                isRainTextFading = true;
                rainTextAlpha = 0f; // Reset alpha to ensure fade in starts from 0
            }
            else
            {
                Debug.LogWarning("Rain feedback text is null! Make sure it's assigned in the inspector.");
            }

            SetRain(true);
        }
        else if (Input.GetKeyUp(KeyCode.Comma))
        {
            Debug.Log("Comma key released - Stopping rain intensity rotation");
            isRotatingRainIntensity = false;
            if (rainFeedbackText != null)
            {
                isRainTextFading = true;
            }
        }

        if (isRotatingRainIntensity && rainParticleSystem != null)
        {
            float cameraY = NormalizeAngle(gyroReader.GetRotY(2));
            float normalized = (cameraY + 180f) % 360f;
            float intensity = Mathf.Lerp(minRainIntensity, maxRainIntensity, normalized / 360f);

            var main = rainParticleSystem.main;
            main.simulationSpeed = intensity;

            // Update feedback text
            if (rainFeedbackText != null)
            {
                string intensityType = currentSeason == Season.Winter ? "SNOW" : "RAIN";
                rainFeedbackText.text = $"CHANGING {intensityType} INTENSITY TO: {intensity:F1}";
                Debug.Log($"Updated rain text: {rainFeedbackText.text}, Alpha: {rainTextAlpha}");
            }

            // Adjust rain audio volume
            if (rainAudioSource != null)
            {
                rainAudioSource.volume = Mathf.Lerp(minRainVolume, maxRainVolume, normalized / 360f);
            }
        }
    }

    public bool holding = false;

    private void HandleSeasonRotation()
    {
        if ((Input.GetKeyDown(KeyCode.P) || ButtonHandler.currentState) && !isRotatingSeason)
        {
            isRotatingSeason = true;
            relativeSeasonY = NormalizeAngle(gyroReader.GetRotY(-2));
        }
        else if (!Input.GetKey(KeyCode.P) && !ButtonHandler.currentState && isRotatingSeason)
        {
            isRotatingSeason = false;
            previousSeasonY =
                NormalizeAngle(Mathf.DeltaAngle(relativeSeasonY, gyroReader.GetRotY(-2)) + previousSeasonY);
        }

        if (isRotatingSeason)
        {
            float cameraY = NormalizeAngle(Mathf.DeltaAngle(relativeSeasonY, gyroReader.GetRotY(-2)) + previousSeasonY);

            Debug.Log("relativeSeasonY:" + relativeSeasonY + "previousSeasonY: " + previousSeasonY + " CY: " + cameraY);
            SetSeason(cameraY);
        }
    }

    private float relativeSeasonY = 0f;
    private float previousSeasonY = 0f;

    public void SetSeason(float cameraY)
    {
        float normalized = (cameraY + 180f) % 360f;

        // Update season based on camera rotation
        float seasonValue = 1 - normalized / 360f;
        Season newSeason;
        if (seasonValue < 0.25f)
        {
            newSeason = Season.Spring;
        }
        else if (seasonValue < 0.5f)
        {
            newSeason = Season.Summer;
        }
        else if (seasonValue < 0.75f)
        {
            newSeason = Season.Autumn;
        }
        else
        {
            newSeason = Season.Winter;
        }

        UpdateSeason(newSeason);
        UpdateAmbience();
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
        currentSeasonColor = directionalLight.color;
        targetSeasonColor = GetSeasonColor(newSeason);
        seasonTransitionProgress = 0f;

        // Apply the appropriate terrain layer for the new season
        if (newSeason == Season.Winter)
        {
            SetSnow(true);
        }
        else
        {
            ApplyTerrainLayer(GetSeasonTerrainLayer());
        }

        // Update trees based on season
        UpdateTrees(newSeason);
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

    private void UpdateTextFade()
    {
        // Update rain text fade
        if (isRainTextFading)
        {
            if (rainFeedbackText != null)
            {
                float targetAlpha = isRotatingRainIntensity ? 1f : 0f;
                rainTextAlpha = Mathf.MoveTowards(rainTextAlpha, targetAlpha, Time.deltaTime * textFadeSpeed);
                Color textColor = rainFeedbackText.color;
                textColor.a = rainTextAlpha;
                rainFeedbackText.color = textColor;

                if (rainTextAlpha == 0f)
                {
                    rainFeedbackText.gameObject.SetActive(false);
                    isRainTextFading = false;
                }
            }
        }
    }

    public Season GetCurrentSeason()
    {
        return currentSeason;
    }
}