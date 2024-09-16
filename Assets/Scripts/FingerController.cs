using UnityEngine;

public class FingerController : MonoBehaviour
{
    [SerializeField] private Transform indexFingerBase;
    [SerializeField] private Transform indexFingerTip;
    [SerializeField] private Transform indexRest;
    [SerializeField] private Transform indexRest_2;

    [SerializeField] private Transform thumbBase;
    [SerializeField] private Transform thumbTip;
    [SerializeField] private Transform thumbRest;
    [SerializeField] private Transform thumbRest_2;
    [SerializeField] public float minDistance = 0.1f;
    [SerializeField] public float maxDistance = 0.3f;

    [SerializeField] private float rotationSpeed = 100f;

    public float CurrentPressure { get; private set; } = 0f;

    private Quaternion initialIndexRotation;
    private Quaternion initialThumbRotation;
    private float currentDistance;

    private void Start()
    {
        initialIndexRotation = indexFingerBase.localRotation;
        initialThumbRotation = thumbBase.localRotation;
    }

    private void Update()
    {
        if (CurrentPressure > 0)
        {
            RotateFingerTowardsTarget(indexFingerBase, indexFingerTip, indexRest, indexRest_2);
            RotateFingerTowardsTarget(thumbBase, thumbTip, thumbRest, thumbRest_2);
        }
        else
        {
            ReturnToInitialPosition(indexFingerBase, initialIndexRotation);
            ReturnToInitialPosition(thumbBase, initialThumbRotation);
        }
    }

    private void RotateFingerTowardsTarget(Transform fingerBase, Transform fingerTip, Transform restPosition, Transform rest2Position)
    {
        Vector3 targetPosition = Vector3.Lerp(restPosition.position, rest2Position.position, CurrentPressure);

        Vector3 targetDirection = targetPosition - fingerBase.position;
        Quaternion targetRotation = Quaternion.FromToRotation(fingerTip.position - fingerBase.position, targetDirection);

        fingerBase.rotation = Quaternion.RotateTowards(
            fingerBase.rotation,
            targetRotation * fingerBase.rotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void ReturnToInitialPosition(Transform fingerBase, Quaternion initialRotation)
    {
        fingerBase.localRotation = Quaternion.RotateTowards(
            fingerBase.localRotation,
            initialRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    public void UpdatePressure(float normalizedPressure)
    {
        CurrentPressure = Mathf.Clamp01(normalizedPressure);
        Debug.Log($"FingerController updated pressure: {CurrentPressure}");
    }

    public void UpdateFingerDistance(float distance)
    {
        currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }
}