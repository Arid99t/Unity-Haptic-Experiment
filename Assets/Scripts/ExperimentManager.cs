using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.Text;
using System;
using System.Net;
using System.IO;

public class ExperimentManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] instructionTexts;
    [SerializeField] private TextMeshProUGUI stepText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private LineRenderer[] lineRenderers;
    [SerializeField] private GameObject hand;
    [SerializeField] private GameObject cube;
    [SerializeField] private BoxDeformer boxDeformer;
    [SerializeField] private PressureSensor pressureSensor;
    [SerializeField] private Transform cubePoint;
    [SerializeField] private Transform refPt;
    [SerializeField] private float maxDistance = 0.1f;
    [SerializeField] private float refLineStartX = 3.1f;

    private enum ExperimentState
    {
        Welcome,
        DevicePress,
        LineAlignment,
        DataCollected,
        IntermediateScreen
    }

    private ExperimentState currentState;
    private int currentRound = 0;
    private int currentStep = 0;
    private GameObject currentLine;
    private int experimentIteration = 1;
    private float materialConstant = 1f;
    private float targetDistance;

    private UdpClient udpClient;
    private const int UdpSendPort = 8893;
    private string csvFilePath;

    private void Start()
    {
        InitializeUdpClient();
        InitializeCsvFile();
        HideAllLines();
        SetState(ExperimentState.Welcome);
        UpdateStepAndRoundText();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ProcessStateTransition();
        }
    }

    private void InitializeCsvFile()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fileName = $"ExperimentData_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        csvFilePath = Path.Combine(desktopPath, fileName);

        string header = "Iteration,Round,Step,Phase,Pressure,Time,Distance,Accuracy,CubePointX,CubePointY,CubePointZ,LineStartX,LineStartY,LineStartZ,LineEndX,LineEndY,LineEndZ,MaterialConstant,TargetDistance,ScaleX,TargetCompression,CompressionError";
        File.WriteAllText(csvFilePath, header + Environment.NewLine);
    }

    private void SetState(ExperimentState newState)
    {
        currentState = newState;
        UpdateInstructionText();
        UpdateStepAndRoundText();
    }

    private void UpdateInstructionText()
    {
        for (int i = 0; i < instructionTexts.Length; i++)
        {
            instructionTexts[i].gameObject.SetActive(i == (int)currentState);
        }
    }

    private void UpdateStepAndRoundText()
    {
        if (stepText != null)
            stepText.text = $"Step: {currentStep}/{GetTotalSteps()}";
        if (roundText != null)
            roundText.text = $"Round: {currentRound + 1}/2 Material: {experimentIteration}";
    }

    private int GetTotalSteps()
    {
        return currentRound == 0 ? 10 : 30;
    }

    private void ProcessStateTransition()
    {
        switch (currentState)
        {
            case ExperimentState.Welcome:
                SetState(ExperimentState.DevicePress);
                break;
            case ExperimentState.DevicePress:
                StartNewStep();
                break;
            case ExperimentState.LineAlignment:
                CollectData();
                break;
            case ExperimentState.DataCollected:
                if (currentStep < GetTotalSteps())
                {
                    StartNewStep();
                }
                else
                {
                    SetState(ExperimentState.IntermediateScreen);
                }
                break;
            case ExperimentState.IntermediateScreen:
                CompleteRound();
                break;
        }
    }

    private void StartNewStep()
    {
        currentStep++;
        if (currentStep > GetTotalSteps())
        {
            CompleteRound();
            return;
        }

        if (currentStep > 0)
        {
            SelectRandomLine();
        }
        else
        {
            HideAllLines();
        }
        SetState(ExperimentState.LineAlignment);
    }

    private void SelectRandomLine()
    {
        HideAllLines();
        int randomIndex = UnityEngine.Random.Range(0, lineRenderers.Length);
        currentLine = lineRenderers[randomIndex].gameObject;
        currentLine.SetActive(true);

        targetDistance = CalculateTargetDistance();
    }

    private void HideAllLines()
    {
        foreach (var lineRenderer in lineRenderers)
        {
            lineRenderer.gameObject.SetActive(false);
        }
    }

    private void CollectData()
    {
        float pressure = pressureSensor.GetCurrentPressure();
        float time = Time.time;
        float distance = CalculateXAxisDistance(cubePoint.position);
        float accuracy = CalculateAccuracy(distance);
        Vector3 cubePointPosition = cubePoint.position;
        Vector3 lineStartPoint = GetVisibleLineStartPoint();
        Vector3 lineEndPoint = GetVisibleLineEndPoint();
        float scaleX = cube.transform.localScale.x;
        float targetCompression = GetTargetCompression();
        float compressionError = Mathf.Abs(targetCompression - scaleX);

        string phase = currentRound == 0 ? "Visual" : "NonVisual";
        string dataLine = $"{experimentIteration},{currentRound + 1},{currentStep},{phase},{pressure},{time},{distance:F4},{accuracy:F2}," +
                          $"{cubePointPosition.x},{cubePointPosition.y},{cubePointPosition.z}," +
                          $"{lineStartPoint.x},{lineStartPoint.y},{lineStartPoint.z}," +
                          $"{lineEndPoint.x},{lineEndPoint.y},{lineEndPoint.z}," +
                          $"{materialConstant},{targetDistance:F4},{scaleX},{targetCompression:F5},{compressionError:F5}";

        File.AppendAllText(csvFilePath, dataLine + Environment.NewLine);
        SendDataViaUDP(dataLine);

        SetState(ExperimentState.DataCollected);
    }

    private float CalculateXAxisDistance(Vector3 point)
    {
        if (currentLine == null)
            return -1f;

        LineRenderer lineRenderer = currentLine.GetComponent<LineRenderer>();
        if (lineRenderer == null || lineRenderer.positionCount < 2)
            return -1f;

        Vector3 lineStart = lineRenderer.GetPosition(0);

        float distance = Mathf.Abs(point.x - lineStart.x);

        return distance;
    }

    private float CalculateTargetDistance()
    {
        if (currentLine == null)
            return -1f;

        LineRenderer lineRenderer = currentLine.GetComponent<LineRenderer>();
        if (lineRenderer == null || lineRenderer.positionCount < 2)
            return -1f;

        Vector3 lineStart = lineRenderer.GetPosition(0);

        float targetDistance = Mathf.Abs(refLineStartX - lineStart.x);

        return targetDistance;
    }

    private float CalculateAccuracy(float distance)
    {
        return Mathf.Max(0f, 100f * (1f - Mathf.Clamp01(distance / maxDistance)));
    }

    private Vector3 GetVisibleLineStartPoint()
    {
        LineRenderer lineRenderer = currentLine?.GetComponent<LineRenderer>();
        return lineRenderer != null ? lineRenderer.GetPosition(0) : Vector3.zero;
    }

    private Vector3 GetVisibleLineEndPoint()
    {
        LineRenderer lineRenderer = currentLine?.GetComponent<LineRenderer>();
        return lineRenderer != null ? lineRenderer.GetPosition(lineRenderer.positionCount - 1) : Vector3.zero;
    }

    private float GetTargetCompression()
    {
        int lineIndex = Array.IndexOf(lineRenderers, currentLine?.GetComponent<LineRenderer>());
        switch (lineIndex)
        {
            case 0: return 0.68794f;
            case 1: return 0.66001f;
            case 2: return 0.62448f;
            case 3: return 0.57949f;
            case 4: return 0.54304f;
            case 5: return 0.49568f;
            case 6: return 0.45790f;
            case 7: return 0.42509f;
            case 8: return 0.39034f;
            case 9: return 0.34864f;
            default: return -1f; // Or any other default value
        }
    }

    private void CompleteRound()
    {
        HideAllLines();
        if (currentRound == 0)
        {
            currentRound++;
            currentStep = 0;
            SetObjectsVisibility(false);
            Debug.Log("Hand and cube visuals made invisible after completing Round 1");
            SetState(ExperimentState.DevicePress);
        }
        else
        {
            RestartExperiment();
        }
    }

    private void RestartExperiment()
    {
        experimentIteration++;
        currentRound = 0;
        currentStep = 0;

        // Set max deformation based on the current iteration
        if (experimentIteration == 1)
        {
            boxDeformer.SetMaxDeformation(0.6f);
        }
        else if (experimentIteration == 2)
        {
            boxDeformer.SetMaxDeformation(0.75f);
        }

        materialConstant = 2f;
        SetObjectsVisibility(true);
        HideAllLines();
        SetState(ExperimentState.DevicePress);
        UpdateStepAndRoundText();
        Debug.Log($"Starting new experiment iteration {experimentIteration} with material constant {materialConstant}");
    }

    private void SetObjectsVisibility(bool isVisible)
    {
        SetVisibility(hand, isVisible);
        SetVisibility(cube, isVisible);
    }

    private void SetVisibility(GameObject obj, bool isVisible)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = isVisible;
            }
            Debug.Log($"Set visibility of {obj.name} and its {renderers.Length} renderer(s) to {isVisible}");
        }
        else
        {
            Debug.LogWarning($"No Renderer components found on {obj.name} or its children");
        }
    }

    private void SendDataViaUDP(string data)
    {
        if (udpClient != null)
        {
            try
            {
                byte[] dataBytes = Encoding.ASCII.GetBytes(data);
                udpClient.Send(dataBytes, dataBytes.Length, "127.0.0.1", UdpSendPort);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending data via UDP: {ex.Message}");
            }
        }
    }

    private void InitializeUdpClient()
    {
        udpClient = new UdpClient();
    }

    private void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.Close();
        }
    }
}
