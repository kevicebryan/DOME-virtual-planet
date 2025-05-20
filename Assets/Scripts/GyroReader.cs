using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine.Networking;

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

    private void OnDestroy()
    {
        httpClient?.Dispose();
        listenThread?.Abort();
        buttonListenThread?.Abort();
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
                    //Debug.Log(eventLine);
                    string jsonData = eventLine.Substring(5).Trim();
                    try
                    {
                        GyroReadingData data = JsonUtility.FromJson<GyroReadingData>(jsonData);
                        onGyroZUpdate?.Invoke(Math.Round(data.gyroZ, 2));
                        button.currentState = data.button2;
                        //button.UpdateState(data.button2);
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
        public bool button1;
        public bool button2;
        public bool button3;
    }

    public LEDController led;
    public ButtonHandler button;
    public bool[] buttons = new bool[5];

    [Serializable]
    private class ButtonData
    {
        public bool button1;
        public bool button2;
        public bool button3;
        public bool button4;
        public bool button5;
    }

    public void Start()
    {
        StartListening(it => { rotY = (float)it * 360; });
        StartButtonListening();
        if (gameObject.GetComponent<LEDController>() == null)
        {
            led = gameObject.AddComponent<LEDController>();
        }

        if (gameObject.GetComponent<ButtonHandler>() == null)
        {
            button = gameObject.AddComponent<ButtonHandler>();
        }
    }

    private float rotY = 0f;


    private float lastY = 0f;
    private float checkTimer = 0f;

    public bool interacted = false;


    public float checkInterval = 1f;
    public float threshold = 5f;
    private Thread buttonListenThread;

    public void StartButtonListening()
    {
        buttonListenThread = new Thread(() => ListenToData("/data"));
        buttonListenThread.Start();
    }

    private void ListenToData(string path)
    {
        try
        {
            using (TcpClient dataClient = new TcpClient("192.168.8.185", 80))
            using (NetworkStream dataStream = dataClient.GetStream())
            using (StreamReader dataReader = new StreamReader(dataStream, Encoding.UTF8))
            {
                string request = $"GET {path} HTTP/1.1\r\n" +
                                 $"Host: 192.168.8.185\r\n" +
                                 $"Accept: text/event-stream\r\n" +
                                 $"Connection: keep-alive\r\n\r\n";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                dataStream.Write(requestBytes, 0, requestBytes.Length);
                dataStream.Flush();

                string line;
                while (!string.IsNullOrEmpty(line = dataReader.ReadLine()))
                {
                    Debug.Log("[DATA HEADER] " + line);
                }

                while (true)
                {
                    string eventLine = dataReader.ReadLine();
                    if (eventLine == null) break;

                    if (eventLine.StartsWith("data:"))
                    {
                        string jsonData = eventLine.Substring(5).Trim();
                        try
                        {
                            ButtonData buttonData = JsonUtility.FromJson<ButtonData>(jsonData);
                            buttons[0] = buttonData.button1;
                            buttons[1] = buttonData.button2;
                            buttons[2] = buttonData.button3;
                            buttons[3] = buttonData.button4;
                            buttons[4] = buttonData.button5;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Terminal data exception: " + e.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Terminal data exception: " + ex.Message);
        }
    }

    public float GetRotY(int mode = -1, bool includeRotY = true)
    {
        if (mode < 0)
        {
            mode = currentMode;
        }

        return ((mode == currentMode && includeRotY ? rotY : 0) + RotOffset[mode]);
    }

    private float rotYP = 0;

    public void SetMode(int mode)
    {
        if (mode == currentMode)
            return;

        if (mode == 0)
        {
            RotOffset[0] -= rotY;
        }
        else if (currentMode == 0)
        {
            rotYP = rotY;
            RotOffset[mode] = RotOffset[0];
            RotOffset[0] += rotYP;
        }

        currentMode = mode;
    }


    public int currentMode = 0;
    float[] RotOffset = new[] { 0, 0, 0f };

    [SerializeField] private float scrollSpeed = 20f;

    public void Update()
    {
        if (Input.GetKey(KeyCode.P) || ButtonHandler.hold)
        {
            if (currentMode == 0)
            {
                SetMode(1);
            }
        }
        else if (currentMode != 0)
        {
            SetMode(0);
        }

        Debug.Log("Mode:" + currentMode + ", RotY:" + rotY + ", RotYP:" + rotYP + ", Now: " + GetRotY(currentMode) +
                  ", mode 0: " +
                  RotOffset[0] +
                  ", mode 1: " + RotOffset[1]);

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        RotOffset[currentMode] += scrollInput * scrollSpeed;


        var rotation = this.transform.rotation;
        var rotX = rotation.eulerAngles.x;
        var rotZ = rotation.eulerAngles.z;
        if (currentMode == 0)
        {
            rotation = Quaternion.Euler(rotX, GetRotY(currentMode), rotZ);
            this.transform.rotation = rotation;
        }

        led.LightSingleLEDByDirection(NormalizeAngle(GetRotY(0)));
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

    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0) angle += 360f;
        return angle;
    }
}