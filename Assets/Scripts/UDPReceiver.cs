using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class UDPReceiver : MonoBehaviour
{
    private UdpClient udpClient;
    private const int Port = 8889;  // Ensure this matches the port your C# app is sending to
    private string receivedPressure = "No data received";

    // Delegate and event for pressure data received
    public delegate void PressureDataReceivedHandler(float pressure);
    public event PressureDataReceivedHandler OnPressureDataReceived;

    void Start()
    {
        InitializeUdpClient();
    }

    private void InitializeUdpClient()
    {
        udpClient = new UdpClient(Port);
        udpClient.BeginReceive(ReceiveCallback, null);
        Debug.Log($"Listening for pressure data on port {Port}");
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, Port);
        byte[] data = udpClient.EndReceive(ar, ref ip);
        string message = Encoding.ASCII.GetString(data);

        receivedPressure = message;
        

        // Parse the pressure value and invoke the event
        if (message.StartsWith("PRESSURE:"))
        {
            if (float.TryParse(message.Substring(9), out float pressure))
            {
                OnPressureDataReceived?.Invoke(pressure);
            }
        }

        // Continue listening for UDP data packages
        udpClient.BeginReceive(ReceiveCallback, null);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 20), $"Pressure: {receivedPressure}");
    }

    void OnDisable()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }
}
