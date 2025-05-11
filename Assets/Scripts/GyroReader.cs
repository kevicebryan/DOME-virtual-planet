using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

public class GyroReader : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private StreamReader reader;
    private Thread listenThread;
    private Action<double> onGyroZUpdate;

    public string server = "192.168.8.115";
    public int port = 80;
    public string eventsPath = "/events";

    private bool running = false;

    public void StartListening(Action<double> callback)
    {
        onGyroZUpdate = callback;
        running = true;
        listenThread = new Thread(ListenLoop);
        listenThread.Start();
    }

    public void StopListening()
    {
        running = false;
        listenThread?.Abort();
        stream?.Close();
        client?.Close();
    }

    private void ListenLoop()
    {
        try
        {
            client = new TcpClient(server, port);
            stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);

            // Send HTTP GET request manually
            string request = $"GET {eventsPath} HTTP/1.1\r\n" +
                             $"Host: {server}:{port}\r\n" +
                             $"Accept: text/event-stream\r\n" +
                             $"Connection: keep-alive\r\n\r\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);
            stream.Flush();

            // Skip HTTP headers
            string line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                Debug.Log("[HEADER] " + line);
            }

            // Read event stream
            while (running)
            {
                string eventLine = reader.ReadLine();
                if (eventLine == null)
                {
                    Debug.LogWarning("Server closed connection.");
                    break;
                }

                if (eventLine.StartsWith("data:"))
                {
                    Debug.Log(eventLine);
                    string jsonData = eventLine.Substring(5).Trim();
                    try
                    {
                        GyroReadingData data = JsonUtility.FromJson<GyroReadingData>(jsonData);
                        onGyroZUpdate?.Invoke(Math.Round(data.gyroZ, 2));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Failed to parse JSON: " + e.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("SSE Listening error: " + ex.Message);
        }
    }

    [Serializable]
    private class GyroReadingData
    {
        public double gyroZ;
    }

    public void Start()
    {
        this.StartListening(it => { rotY = (float)it * 360; });
    }

    private float rotY = 0f;
    public float RotYOffset { set; get; } = 0f;


    private float lastY = 0f;
    private float checkTimer = 0f;

    public bool interacted = false;


    public float checkInterval = 1f;
    public float threshold = 5f;

    public void Update()
    {
        var rotation = this.transform.rotation;
        var rotX = rotation.eulerAngles.x;
        var rotZ = rotation.eulerAngles.z;
        rotation = Quaternion.Euler(rotX, rotY + RotYOffset, rotZ);
        this.transform.rotation = rotation;
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            float currentY = rotY + RotYOffset;
            float delta = Mathf.DeltaAngle(lastY, currentY);

            interacted = Mathf.Abs(delta) > threshold;
            lastY = currentY;
            checkTimer = 0f;
        }
    }
}