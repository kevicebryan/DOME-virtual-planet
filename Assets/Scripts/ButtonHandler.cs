using System;
using UnityEngine;

public class ButtonHandler:MonoBehaviour
{
    public static bool hold;
    public Action onTap = () => Debug.Log("Tap!");
    public Action onHoldStart = () =>
    {
        Debug.Log("Hold Start");
        hold = true;
    };
    public Action onHoldEnd = () =>
    {
        Debug.Log("Hold End");
        hold = false;
    };
    public Action onTapHoldComboStart = () => Debug.Log("Tap+Hold Combo Start");
    public Action onTapHoldComboEnd = () => Debug.Log("Tap+Hold Combo End");

    private bool prevButtonState = false;
    private float buttonDownTime = 0f;
    private bool isHolding = false;
    private bool isTapHoldCombo = false;

    private float tapThreshold = 0.2f;
    private float comboThreshold = 0.3f;

    private float lastTapReleaseTime = -999f;

    public bool currentState = false;

    private void Update()
    {
        UpdateState(currentState);
    }

    public void UpdateState(bool currentState)
    {
        float now = Time.time;

        if (currentState && !prevButtonState)
        {
            buttonDownTime = now;

            if (now - lastTapReleaseTime <= comboThreshold)
            {
                isTapHoldCombo = true;
                onTapHoldComboStart?.Invoke();
            }
            else
            {
                isHolding = true;
            }
        }

        if (!currentState && prevButtonState)
        {
            float heldDuration = now - buttonDownTime;

            if (isTapHoldCombo)
            {
                isTapHoldCombo = false;
                onTapHoldComboEnd?.Invoke();
            }
            else if (heldDuration <= tapThreshold)
            {
                onTap?.Invoke();
                lastTapReleaseTime = now;
            }
            else if (isHolding)
            {
                onHoldEnd?.Invoke();
            }

            isHolding = false;
        }

        if (currentState && isHolding && !isTapHoldCombo && (now - buttonDownTime >= tapThreshold))
        {
            onHoldStart?.Invoke();
        }

        prevButtonState = currentState;
    }
}