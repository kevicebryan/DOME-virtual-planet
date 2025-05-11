using System;
using UnityEngine;

public class InteractionListener : MonoBehaviour
{
    [SerializeField] private WeatherController controller;
    [SerializeField] private AIAgent agent;
    [SerializeField] private GyroReader gyro;

    [SerializeField] private float inactiveThreshold = 30f; // 触发等待时间
    private float inactivityTimer = 0f;

    private void Start()
    {
        controller = FindObjectOfType<WeatherController>();
        agent = FindObjectOfType<AIAgent>();
        gyro = FindObjectOfType<GyroReader>();

        if (controller == null)
            Debug.LogWarning("WeatherController not found in scene!");
        if (agent == null)
            Debug.LogWarning("AIAgent not found in scene!");
        if (gyro == null)
            Debug.LogWarning("GyroReader not found in scene!");
    }

    private void Update()
    {
        bool interacted = agent.interacted || controller.interacted || gyro.interacted;

        if (interacted)
        {
            inactivityTimer = 0f;
        }
        else
        {
            inactivityTimer += Time.deltaTime;
            if (inactivityTimer >= inactiveThreshold)
            {
                TriggerIntroduction();
                inactivityTimer = 0f;
            }
        }

        agent.interacted = controller.interacted = gyro.interacted = false;
    }

    public void TriggerIntroduction()
    {
    }
}