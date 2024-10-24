using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class New_BoxDeformer : MonoBehaviour
{
    [SerializeField] private FingerController fingerController;
    [SerializeField] private Transform indexFingerTip;
    [SerializeField] private float maxDeformation = 0.96f;
    [SerializeField] private float widthExpansionFactor = 0.96f;
    [SerializeField] private float smoothSpeed = 40f; // Adjust this to change overall responsiveness
    public Vector3 initialScale;
    private Vector3 initialPosition;
    private Vector3 thumbSidePosition;
    private Vector3 targetScale;
    private Vector3 currentScale;
    private float currentPressure = 0f;
    private float velocityPressure = 0f;
    private float targetPressure = 0f;

    void Start()
    {
        initialScale = transform.localScale;
        initialPosition = transform.position;
        currentScale = initialScale;
        targetScale = initialScale;

        // Calculate the position of the thumb side (assuming local right is towards index finger)
        thumbSidePosition = transform.position - transform.right * (transform.localScale.x * 0.5f);
    }

    void Update()
    {
        if (fingerController == null || indexFingerTip == null) return;

        // Get the current pressure from the FingerController
        targetPressure = fingerController.CurrentPressure;

        // Smoothly interpolate the current pressure
        currentPressure = Mathf.SmoothDamp(currentPressure, targetPressure, ref velocityPressure, 1 / smoothSpeed);

        // Calculate the compression direction (from object to index finger)
        Vector3 compressionDirection = (indexFingerTip.position - transform.position).normalized;

        // Calculate target scale factors
        float xScaleFactor = 1 - (currentPressure * maxDeformation);
        float yzScaleFactor = 1 + (currentPressure * maxDeformation * widthExpansionFactor);

        // Set the target scale based on the calculated factors
        targetScale = new Vector3(
            initialScale.x * xScaleFactor,
            initialScale.y * yzScaleFactor,
            initialScale.z * yzScaleFactor
        );

        // Smoothly interpolate the current scale towards the target scale
        currentScale = Vector3.Lerp(currentScale, targetScale, Time.deltaTime * smoothSpeed);

        // Apply the new scale to the object
        transform.localScale = currentScale;

        // Calculate the new position to keep the thumb side stationary
        Vector3 newThumbSidePosition = thumbSidePosition;
        Vector3 newPosition = newThumbSidePosition + transform.right * (currentScale.x * 0.5f);

        // Smoothly move the object to the new position
        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * smoothSpeed);
    }

    // Method to retrieve the amount of compression applied to the object
    public float GetCompressionAmount()
    {
        return 1 - transform.localScale.x / initialScale.x;
    }

    // Method to adjust the max deformation value, allowing dynamic changes based on experiment conditions
    public void SetMaxDeformation(float newMaxDeformation)
    {
        maxDeformation = Mathf.Clamp(newMaxDeformation, 0.1f, 0.9f);
    }

    // Method to get the current max deformation value
    public float GetMaxDeformation()
    {
        return maxDeformation;
    }
}
