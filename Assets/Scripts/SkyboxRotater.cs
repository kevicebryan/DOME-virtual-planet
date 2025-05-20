using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    public float rotationSpeed = 1f;
    private float currentRotation = 0f;
    private float targetRotation = 0f;
    [SerializeField] private float smoothness = 0.1f; // Controls how smooth the rotation is (lower = smoother)

    void Update()
    {
        // Calculate target rotation
        targetRotation = Time.time * rotationSpeed;
        
        // Smoothly interpolate current rotation towards target
        currentRotation = Mathf.Lerp(currentRotation, targetRotation, smoothness);
        
        // Apply the rotation
        RenderSettings.skybox.SetFloat("_Rotation", currentRotation);
    }
}
