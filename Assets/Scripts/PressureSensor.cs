using UnityEngine;

public class PressureSensor : MonoBehaviour
{
    [SerializeField] private UDPReceiver udpReceiver;
    [SerializeField] private FingerController fingerController;
    [SerializeField] private float minPressure = 0f;
    [SerializeField] private float maxPressure = 1000f;

    private void Start()
    {
        if (udpReceiver != null)
        {
            udpReceiver.OnPressureDataReceived += HandlePressureData;
        }
        else
        {
            Debug.LogError("UdpReceiver not assigned to PressureSensor!");
        }
    }

    private void HandlePressureData(float pressure)
    {
        Debug.Log($"Pressure sensor received: {pressure}");

        // Normalize pressure to 0-1 range with specified precision
        float normalizedPressure = Mathf.InverseLerp(minPressure, maxPressure, pressure);
        normalizedPressure = Mathf.Round(normalizedPressure * 100000f) / 100000f; // Round to 5 decimal places

        Debug.Log($"Normalized pressure: {normalizedPressure}");

        // Update finger controller
        if (fingerController != null)
        {
            fingerController.UpdatePressure(normalizedPressure);
        }
        else
        {
            Debug.LogWarning("FingerController not assigned to PressureSensor!");
        }
    }

    private void OnDisable()
    {
        if (udpReceiver != null)
        {
            udpReceiver.OnPressureDataReceived -= HandlePressureData;
        }
    }
    public float GetCurrentPressure()
    {
        return fingerController.CurrentPressure;
    }
}
