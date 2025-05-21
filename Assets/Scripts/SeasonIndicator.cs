using UnityEngine;
using UnityEngine.UI;

public class SeasonIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeatherController weatherController;

    [Header("Season Indicators")]
    [SerializeField] private Image springImage;
    [SerializeField] private Image summerImage;
    [SerializeField] private Image autumnImage;
    [SerializeField] private Image winterImage;

    [Header("Season Sprites")]
    [SerializeField] private Sprite springActive;
    [SerializeField] private Sprite springInactive;
    [SerializeField] private Sprite summerActive;
    [SerializeField] private Sprite summerInactive;
    [SerializeField] private Sprite autumnActive;
    [SerializeField] private Sprite autumnInactive;
    [SerializeField] private Sprite winterActive;
    [SerializeField] private Sprite winterInactive;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.1f;
    private CanvasGroup canvasGroup;
    private float targetAlpha = 0f;
    private float currentAlpha = 0f;
    private bool isFading = false;

    private void Start()
    {
        // Ensure we have a reference to WeatherController
        if (weatherController == null)
        {
            weatherController = FindObjectOfType<WeatherController>();
            if (weatherController == null)
            {
                Debug.LogError("WeatherController not found!");
                enabled = false;
                return;
            }
        }

        // Get or add CanvasGroup component
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Initialize with current season and hidden state
        UpdateSeasonIndicators(weatherController.GetCurrentSeason());
        canvasGroup.alpha = 0f;
        gameObject.SetActive(true); // Keep the object active but invisible
    }

    private void Update()
    {
        // Get the isRotatingSeason value from WeatherController
        bool shouldBeVisible = weatherController.isRotatingSeason;
        
        // Handle fade animation
        if (shouldBeVisible)
        {
            targetAlpha = 1f;
            if (!isFading && currentAlpha < 1f)
            {
                isFading = true;
            }
        }
        else
        {
            targetAlpha = 0f;
            if (!isFading && currentAlpha > 0f)
            {
                isFading = true;
            }
        }

        // Update fade
        if (isFading)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);
            canvasGroup.alpha = currentAlpha;

            // Check if fade is complete
            if (currentAlpha == targetAlpha)
            {
                isFading = false;
            }
        }

        // Update season indicators
        UpdateSeasonIndicators(weatherController.GetCurrentSeason());
    }

    private void UpdateSeasonIndicators(WeatherController.Season currentSeason)
    {
        // Update each season's sprite based on whether it's the current season
        if (springImage != null)
        {
            springImage.sprite = currentSeason == WeatherController.Season.Spring ? springActive : springInactive;
        }

        if (summerImage != null)
        {
            summerImage.sprite = currentSeason == WeatherController.Season.Summer ? summerActive : summerInactive;
        }

        if (autumnImage != null)
        {
            autumnImage.sprite = currentSeason == WeatherController.Season.Autumn ? autumnActive : autumnInactive;
        }

        if (winterImage != null)
        {
            winterImage.sprite = currentSeason == WeatherController.Season.Winter ? winterActive : winterInactive;
        }
    }
}
