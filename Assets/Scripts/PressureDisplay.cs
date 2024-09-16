using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class PressureDisplay : MonoBehaviour
{
    private UdpClient udpClient;
    private const int Port = 8889;  // Make sure this matches the port your C# app is sending to
    private string receivedPressure = "No data received";

    void Start()
    {
        udpClient = new UdpClient(Port);
        udpClient.BeginReceive(ReceiveCallback, null);
        Debug.Log($"Listening for pressure data on port {Port}");
    }

    void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, Port);
        byte[] data = udpClient.EndReceive(ar, ref ip);
        string message = Encoding.ASCII.GetString(data);

        receivedPressure = message;
        Debug.Log($"Received: {message}");

        udpClient.BeginReceive(ReceiveCallback, null);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 20), $"Pressure: {receivedPressure}");
    }

    void OnDisable()
    {
        if (udpClient != null)
            udpClient.Close();
    }
}