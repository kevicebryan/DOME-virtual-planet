using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using System.Net.Http;
using System.Threading.Tasks;

public class GyroReader : MonoBehaviour
{
    public WeatherController controller;
    private TcpClient client;
    private NetworkStream stream;
    private StreamReader reader;
    private Thread listenThread;
    private Action<double> onGyroZUpdate;
    private HttpClient httpClient;

    public string server = "192.168.8.115";
    public int port = 80;
    public string eventsPath = "/events";

    private bool running = false;

    private void Awake()
    {
        httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri($"http://{server}:{port}");
    }

    public async void ResetZ()
    {
        try
        {
            var response = await httpClient.GetAsync("/resetZ");
            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Gyro Z reset triggered");
                // Reset the current rotation offset for the current mode
                RotOffset[currentMode] = 0f;
                rotY = 0f;
                lastY = 0f;
            }
            else
            {
                Debug.LogError($"Reset failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Reset failed: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        httpClient?.Dispose();
    }

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


    private float lastY = 0f;
    private float checkTimer = 0f;

    public bool interacted = false;


    public float checkInterval = 1f;
    public float threshold = 5f;

    public float GetRotY(int mode = 0)
    {
        return ((mode == currentMode) ? rotY : 0) + RotOffset[mode];
    }

    public void SetMode(int mode){
        RotOffset[currentMode] = GetRotY();
        currentMode = mode;
        //ResetZ();
    }
    public int currentMode = 0;
    float[] RotOffset = new [] {0,0,0f};

    [SerializeField] private float scrollSpeed = 20f;
    public void Update()
    {
        if (Input.GetKey(KeyCode.P)){
            if(currentMode == 0){
                currentMode = 1;
                SetMode(currentMode);
            }
        }else if(currentMode != 0){
            currentMode = 0;
            SetMode(currentMode);
        }

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        RotOffset[currentMode] += scrollInput * scrollSpeed;
        

        var rotation = this.transform.rotation;
        var rotX = rotation.eulerAngles.x;
        var rotZ = rotation.eulerAngles.z;
        rotation = Quaternion.Euler(rotX, GetRotY(currentMode), rotZ);
        this.transform.rotation = rotation;
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            float currentY = GetRotY();
            float delta = Mathf.DeltaAngle(lastY, currentY);

            interacted = Mathf.Abs(delta) > threshold;
            lastY = currentY;
            checkTimer = 0f;
        }
    }
}