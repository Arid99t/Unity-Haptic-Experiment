using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.Text;
using System;
using System.Net;
using System.IO;
using System.Collections.Generic;

public class ExperimentManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI[] instructionTexts;
    [SerializeField] private TextMeshProUGUI stepText;
    [SerializeField] private TextMeshProUGUI materialConstantText;

    [Header("Game Objects")]
    [SerializeField] private GameObject refCube; // Reference Cube
    [SerializeField] private GameObject[] cubeObjects; // Array of Cube Objects
    [SerializeField] private GameObject hand;
    [SerializeField] private GameObject cube;

    [Header("Components")]
    [SerializeField] private BoxDeformer boxDeformer;
    [SerializeField] private PressureSensor pressureSensor;
    [SerializeField] private Transform cubePoint;

    [Header("Experiment Settings")]
    [SerializeField] private float maxDistance = 0.1f;
    [SerializeField] private float refLineStartX = 3.1f;

    private enum ExperimentState
    {
        Welcome,
        DevicePress,
        LineAlignment,
        NonVisualTransition, // New state after step 10
        ExperimentComplete
    }

    private ExperimentState currentState;
    private int currentStep = 0;
    private GameObject currentCube;
    private float materialConstant = 1f;
    private float targetDistance;

    private UdpClient udpClient;
    private const int UdpSendPort = 8893;
    private string csvFilePath;

    // Target compressions for each cube (ensure this matches the number of cubeObjects)
    private readonly float[] targetCompressions = { 0.68794f, 0.66001f, 0.62448f, 0.57949f, 0.54304f, 0.49568f, 0.45790f, 0.42509f, 0.39034f, 0.34864f };

    private float stepStartTime; // Tracks the start time of each step

    // Latin Square sequence
    private List<int> cubeSequence = new List<int>();
    private int totalSteps = 140; // Changed from 120 to 140
    private int stepsPerBlock = 10;
    private int totalBlocks;

    // Flag to control visibility of hand and cube after step 10
    private bool hideHandAndCubeAfterStep10 = false;

    private float nonVisualTimerStartTime; // Start time for non-visual timer

    private void Start()
    {
        // Validate that the number of cubeObjects matches targetCompressions
        if (cubeObjects.Length != targetCompressions.Length)
        {
            Debug.LogError("The number of cubeObjects does not match the number of targetCompressions.");
            enabled = false;
            return;
        }

        totalBlocks = totalSteps / stepsPerBlock;
        if (totalSteps % stepsPerBlock != 0)
        {
            Debug.LogError("Total steps must be a multiple of stepsPerBlock (10).");
            enabled = false;
            return;
        }

        InitializeCubeSequence();
        InitializeUdpClient();
        InitializeCsvFile();
        HideAllCubes(); // Initially hide all cube objects
        SetState(ExperimentState.Welcome); // Start with the Welcome state
        UpdateStepAndMaterialConstantText();
        HideExperimentObjects(); // Hide cubeObjects, hand, and cube in Welcome state
        SetVisibility(refCube, false); // Hide Ref_Cube in Welcome state
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ProcessStateTransition();
        }
    }

    /// <summary>
    /// Initializes the Latin Square cube sequence ensuring each cube appears once per block of stepsPerBlock.
    /// </summary>
    private void InitializeCubeSequence()
    {
        cubeSequence.Clear();

        for (int block = 0; block < totalBlocks; block++)
        {
            List<int> blockCubes = new List<int>();
            for (int i = 0; i < cubeObjects.Length; i++)
            {
                blockCubes.Add(i);
            }

            Shuffle(blockCubes);
            cubeSequence.AddRange(blockCubes);
        }
    }

    /// <summary>
    /// Shuffles a list in-place using the Fisher-Yates algorithm.
    /// </summary>
    /// <param name="list">The list to shuffle.</param>
    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    /// <summary>
    /// Initializes the CSV file on the desktop with the appropriate headers.
    /// </summary>
    private void InitializeCsvFile()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fileName = $"ExperimentData_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        csvFilePath = Path.Combine(desktopPath, fileName);

        string header = "Step,Pressure,Time,StepTime,Distance,Accuracy,CubePointX,CubePointY,CubePointZ,CubeX,CubeY,CubeZ,MaterialConstant,TargetDistance,ScaleX,TargetCompression,CompressionError,Timer_Non_Visual";
        File.WriteAllText(csvFilePath, header + Environment.NewLine);
    }

    /// <summary>
    /// Sets the current state of the experiment and updates the UI accordingly.
    /// </summary>
    /// <param name="newState">The new state to transition to.</param>
    private void SetState(ExperimentState newState)
    {
        currentState = newState;
        UpdateInstructionText();
        UpdateStepAndMaterialConstantText();
        ManageExperimentObjectsVisibility();
    }

    /// <summary>
    /// Updates the instruction texts based on the current state.
    /// </summary>
    private void UpdateInstructionText()
    {
        // Deactivate all instruction texts
        for (int i = 0; i < instructionTexts.Length; i++)
        {
            instructionTexts[i].gameObject.SetActive(false);
        }

        // Activate the appropriate text based on current state
        switch (currentState)
        {
            case ExperimentState.Welcome:
                if (instructionTexts.Length > 0)
                    instructionTexts[0].gameObject.SetActive(true);
                break;
            case ExperimentState.DevicePress:
                if (instructionTexts.Length > 1)
                    instructionTexts[1].gameObject.SetActive(true);
                break;
            case ExperimentState.LineAlignment:
                if (instructionTexts.Length > 2)
                    instructionTexts[2].gameObject.SetActive(true);
                break;
            case ExperimentState.NonVisualTransition:
                if (instructionTexts.Length > 6) // Activate text array element 6 (index 6)
                    instructionTexts[6].gameObject.SetActive(true);
                else
                    Debug.LogWarning("Instruction text for NonVisualTransition state is missing.");
                break;
            case ExperimentState.ExperimentComplete:
                if (instructionTexts.Length > 4)
                    instructionTexts[4].gameObject.SetActive(true);
                break;
            default:
                Debug.LogWarning("Unhandled experiment state in UpdateInstructionText.");
                break;
        }
    }

    /// <summary>
    /// Updates the step and material constant texts in the UI.
    /// </summary>
    private void UpdateStepAndMaterialConstantText()
    {
        if (stepText != null)
            stepText.text = $"Step: {currentStep}/{totalSteps}";
        if (materialConstantText != null)
            materialConstantText.text = $"Material Constant: {materialConstant}";
    }

    /// <summary>
    /// Returns the total number of steps in the experiment.
    /// </summary>
    /// <returns>Total steps (140).</returns>
    private int GetTotalSteps()
    {
        return totalSteps;
    }

    /// <summary>
    /// Handles the transition between different experiment states based on user input.
    /// </summary>
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
                if (currentStep == 10)
                {
                    hideHandAndCubeAfterStep10 = true;
                    SetObjectsVisibility(false);
                    Debug.Log("Hand and Cube have been made invisible after step 10.");
                    SetState(ExperimentState.NonVisualTransition);
                }
                else
                {
                    if (currentStep < GetTotalSteps())
                    {
                        StartNewStep();
                    }
                    else
                    {
                        ExperimentComplete();
                    }
                }
                break;
            case ExperimentState.NonVisualTransition:
                // Start step 11 and start non-visual timer
                nonVisualTimerStartTime = Time.time;
                StartNewStep(); // This will increment currentStep to 11
                break;
            case ExperimentState.ExperimentComplete:
                // Optionally handle post-experiment logic here
                Debug.Log("Experiment Completed!");
                break;
        }
    }

    /// <summary>
    /// Initiates a new step in the experiment.
    /// </summary>
    private void StartNewStep()
    {
        currentStep++;
        if (currentStep > GetTotalSteps())
        {
            ExperimentComplete();
            return;
        }

        // Change material constant every 10 steps
        if ((currentStep - 1) % stepsPerBlock == 0)
        {
            materialConstant = ((currentStep - 1) / stepsPerBlock) % 2 == 0 ? 1f : 2f;

            if (materialConstant == 1f)
            {
                boxDeformer.SetMaxDeformation(0.9f);
            }
            else if (materialConstant == 2f)
            {
                boxDeformer.SetMaxDeformation(0.9f);
            }

            // Update the material constant text
            UpdateStepAndMaterialConstantText();
        }

        stepStartTime = Time.time; // Record the start time of the step

        SelectCubeForCurrentStep(); // Select and activate the appropriate cube
        SetState(ExperimentState.LineAlignment);
    }

    /// <summary>
    /// Selects the cube for the current step based on the Latin Square sequence.
    /// </summary>
    private void SelectCubeForCurrentStep()
    {
        HideAllCubes();

        if (currentStep > cubeSequence.Count)
        {
            Debug.LogError("Current step exceeds the precomputed cube sequence.");
            return;
        }

        int cubeIndex = cubeSequence[currentStep - 1];
        if (cubeIndex < 0 || cubeIndex >= cubeObjects.Length)
        {
            Debug.LogError($"Invalid cube index {cubeIndex} at step {currentStep}.");
            return;
        }

        currentCube = cubeObjects[cubeIndex];
        currentCube.SetActive(true);

        targetDistance = CalculateTargetDistance();
    }

    /// <summary>
    /// Hides all cubes in the cubeObjects array.
    /// </summary>
    private void HideAllCubes()
    {
        foreach (var cube in cubeObjects)
        {
            cube.SetActive(false);
        }
    }

    /// <summary>
    /// Collects data for the current step and records it to the CSV file and via UDP.
    /// </summary>
    private void CollectData()
    {
        float pressure = pressureSensor.GetCurrentPressure();
        float time = Time.time;
        float stepTime = time - stepStartTime; // Time taken for the step
        float distance = CalculateXAxisDistance(cubePoint.position);
        float accuracy = CalculateAccuracy(distance);
        Vector3 cubePointPosition = cubePoint.position;
        Vector3 cubePosition = currentCube != null ? currentCube.transform.position : Vector3.zero;
        float scaleX = cube.transform.localScale.x;
        float targetCompression = GetTargetCompression();
        float compressionError = Mathf.Abs(targetCompression - scaleX);
        float timerNonVisual = currentStep >= 11 ? Time.time - nonVisualTimerStartTime : 0f;

        string dataLine = $"{currentStep},{pressure},{time},{stepTime:F4},{distance:F4},{accuracy:F2}," +
                          $"{cubePointPosition.x},{cubePointPosition.y},{cubePointPosition.z}," +
                          $"{cubePosition.x},{cubePosition.y},{cubePosition.z}," +
                          $"{materialConstant},{targetDistance:F4},{scaleX},{targetCompression:F5},{compressionError:F5},{timerNonVisual:F4}";

        File.AppendAllText(csvFilePath, dataLine + Environment.NewLine);
        SendDataViaUDP(dataLine);
    }

    /// <summary>
    /// Calculates the absolute distance along the X-axis between the cube point and the current cube.
    /// </summary>
    /// <param name="point">The position of the cube point.</param>
    /// <returns>Absolute distance along the X-axis.</returns>
    private float CalculateXAxisDistance(Vector3 point)
    {
        if (currentCube == null)
            return -1f;

        Vector3 cubePosition = currentCube.transform.position;

        float distance = Mathf.Abs(point.x - cubePosition.x);

        return distance;
    }

    /// <summary>
    /// Calculates the target distance based on the reference line start X position and current cube position.
    /// </summary>
    /// <returns>Target distance.</returns>
    private float CalculateTargetDistance()
    {
        if (currentCube == null)
            return -1f;

        Vector3 cubePosition = currentCube.transform.position;

        float targetDistance = Mathf.Abs(refLineStartX - cubePosition.x);

        return targetDistance;
    }

    /// <summary>
    /// Calculates the accuracy based on the distance.
    /// </summary>
    private float CalculateAccuracy(float distance)
    {
        return Mathf.Max(0f, 100f * (1f - Mathf.Clamp01(distance / maxDistance)));
    }

    /// <summary>
    /// Retrieves the target compression value for the current cube.
    /// </summary>
    /// <returns>Target compression.</returns>
    private float GetTargetCompression()
    {
        int cubeIndex = Array.IndexOf(cubeObjects, currentCube);
        if (cubeIndex >= 0 && cubeIndex < targetCompressions.Length)
            return targetCompressions[cubeIndex];
        else
            return -1f;
    }

    /// <summary>
    /// Completes the experiment by transitioning to the ExperimentComplete state.
    /// </summary>
    private void ExperimentComplete()
    {
        HideAllCubes();
        SetState(ExperimentState.ExperimentComplete);
        Debug.Log("Experiment Completed!");
    }

    /// <summary>
    /// Sets the visibility of the hand and cube GameObjects.
    /// </summary>
    private void SetObjectsVisibility(bool isVisible)
    {
        SetVisibility(hand, isVisible);
        SetVisibility(cube, isVisible);
    }

    /// <summary>
    /// Sets the visibility of a specific GameObject and its children.
    /// </summary>
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

    /// <summary>
    /// Sends data via UDP to the specified port.
    /// </summary>
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

    /// <summary>
    /// Initializes the UDP client for data transmission.
    /// </summary>
    private void InitializeUdpClient()
    {
        udpClient = new UdpClient();
    }

    /// <summary>
    /// Ensures that the UDP client is properly closed when the object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.Close();
        }
    }

    /// <summary>
    /// Manages the visibility of experiment-related objects based on the current state.
    /// Hides cubeObjects, hand, cube, and Ref_Cube during the Welcome, DevicePress, NonVisualTransition, and ExperimentComplete states.
    /// Shows them during LineAlignment state, but hides hand and cube after step 10.
    /// Ref_Cube is visible during LineAlignment state.
    /// </summary>
    private void ManageExperimentObjectsVisibility()
    {
        switch (currentState)
        {
            case ExperimentState.Welcome:
            case ExperimentState.DevicePress:
            case ExperimentState.NonVisualTransition:
            case ExperimentState.ExperimentComplete:
                // Hide all experiment objects including Ref_Cube
                HideExperimentObjects();
                SetVisibility(refCube, false);
                break;
            case ExperimentState.LineAlignment:
                // Show Ref_Cube
                SetVisibility(refCube, true);
                if (hideHandAndCubeAfterStep10)
                {
                    // From step 11 onwards
                    SetObjectsVisibility(false); // Ensure that hand and cube remain hidden
                }
                else
                {
                    // For steps 1 to 10
                    SetObjectsVisibility(true); // Show hand and cube
                }
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Hides all cubeObjects, hand, and cube.
    /// Does not manage Ref_Cube visibility here.
    /// </summary>
    private void HideExperimentObjects()
    {
        HideAllCubes(); // Hides all cubeObjects
        SetObjectsVisibility(false); // Hides hand and cube
        // Note: Ref_Cube visibility is managed separately
    }

    /// <summary>
    /// Shows hand and cube.
    /// Does not manage Ref_Cube visibility here.
    /// </summary>
    private void ShowExperimentObjects()
    {
        SetObjectsVisibility(true); // Shows hand and cube
        // Note: Ref_Cube visibility is managed separately
    }
}
