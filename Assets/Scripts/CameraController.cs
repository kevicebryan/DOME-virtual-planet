using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 20f;

    void Start()
    {
        
    }

    void Update()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        Vector3 newRotation = transform.eulerAngles;
        newRotation.y += scrollInput * scrollSpeed;
        transform.eulerAngles = newRotation;
    }
}
