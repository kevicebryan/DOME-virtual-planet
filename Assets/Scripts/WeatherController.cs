using UnityEngine;

public class WeatherController : MonoBehaviour
{
    [SerializeField] private GameObject rainObject;
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material rainSkybox;
    [SerializeField] private Material sunriseSkybox;
    [SerializeField] private Material sunsetSkybox;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float transitionAngle = 30f; // Angle range for sunrise/sunset transitions
    [SerializeField] private float transitionSpeed = 2f; // Speed of skybox transitions

    [SerializeField] private AudioSource dayAmbience;
    [SerializeField] private AudioSource nightAmbience;
    [SerializeField] private AudioSource rainAmbience;
    [SerializeField] private AudioSource earthquakeAmbience;

    private Material currentSkybox;
    private Material targetSkybox;
    private float transitionProgress = 0f;
    private bool isDaytime = true;

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
            transitionProgress = 1f;
            RenderSettings.skybox = targetSkybox;
            currentSkybox = targetSkybox;
            targetSkybox = null;
            return;
        }

        // Create a temporary material for the transition
        Material transitionMaterial = new Material(currentSkybox);
        transitionMaterial.Lerp(currentSkybox, targetSkybox, transitionProgress);
        RenderSettings.skybox = transitionMaterial;
    }

    private void UpdateAmbience()
    {
        // Stop all ambience first
        dayAmbience.Stop();
        nightAmbience.Stop();
        rainAmbience.Stop();
        earthquakeAmbience.Stop();

        // Play appropriate ambience based on conditions
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

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        currentSkybox = RenderSettings.skybox;
        UpdateAmbience();
    }

    void Update()
    {
        // Toggle rain with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            rainObject.SetActive(!rainObject.activeSelf);
            UpdateAmbience();
        }

        // Check if rain is active
        if (rainObject.activeSelf)
        {
            StartTransition(rainSkybox);
            UpdateSkyboxTransition();
            return;
        }

        // Set skybox based on camera Y rotation
        float cameraYRotation = mainCamera.transform.eulerAngles.y;
        if (cameraYRotation > 180f)
        {
            cameraYRotation -= 360f;
        }

        bool wasDaytime = isDaytime;
        
        // Handle transitions between day and night following the order:
        // Dawn -> Day -> Sunset -> Night -> Dawn
        if (cameraYRotation >= 180f - transitionAngle && cameraYRotation < 180f)
        {
            StartTransition(sunriseSkybox); // Dawn
            isDaytime = true;
        }
        else if (cameraYRotation >= 0f && cameraYRotation < 180f - transitionAngle)
        {
            StartTransition(daySkybox); // Day
            isDaytime = true;
        }
        else if (cameraYRotation >= -transitionAngle && cameraYRotation < 0f)
        {
            StartTransition(sunsetSkybox); // Sunset
            isDaytime = false;
        }
        else if (cameraYRotation >= -180f && cameraYRotation < -transitionAngle)
        {
            StartTransition(nightSkybox); // Night
            isDaytime = false;
        }
        else
        {
            StartTransition(sunriseSkybox); // Back to Dawn
            isDaytime = true;
        }

        // Update ambience if day/night state changed
        if (wasDaytime != isDaytime)
        {
            UpdateAmbience();
        }

        UpdateSkyboxTransition();
    }
}
