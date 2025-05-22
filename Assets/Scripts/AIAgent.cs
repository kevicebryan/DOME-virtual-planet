using System;
using UnityEngine;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using Unity.VisualScripting;

public class AIAgent : MonoBehaviour
{
    private static bool isRecording = false;
    static LEDController led;
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private WeatherController controller;

    public static bool IsRecording
    {
        get => isRecording;
        set
        {
            isRecording = value;
            if (!isRecording && !ButtonHandler.hold)
            {
                led.OverrideDirectionLEDs(false);
                led.listening = false;
                led.thinking = false;
            }
        }
    }

    private void Start()
    {
        if (gameObject.GetComponent<InteractionListener>() == null)
        {
            gameObject.AddComponent<InteractionListener>();
        }

        if (led == null)
        {
            led = gameObject.GetComponent<LEDController>();
        }

        if (controller == null)
        {
            try
            {
                controller = GameObject.Find("Weather").GetComponent<WeatherController>();
            }
            catch (Exception e)
            {
                Debug.LogError("WeatherController not found: " + e.Message);
            }
        }

        if (controller != null)
        {
            DomeCommand.Register("SetAurora", it => controller.SetAurora(bool.Parse(it[0])));
            DomeCommand.Register("SetRain", it => controller.SetRain(bool.Parse(it[0])));
            DomeCommand.Register("SetGalaxyMode", it => controller.SetGalaxyMode(bool.Parse(it[0])));
            DomeCommand.Register("SetSnow", it => controller.SetSnow(bool.Parse(it[0])));
            DomeCommand.Register("EnableDefaultBackground", _ =>
            {
                controller.SetGalaxyMode(false);
                controller.SetAurora(false);
            });
            DomeCommand.Register("ClearWeather", _ =>
            {
                controller.SetRain(false);
                controller.SetSnow(false);
            });
            DomeCommand.Register("ReportStatus", _ => { });
        }
    }

    public static bool talkPushed;

    private void Update()
    {
        if ((talkPushed || Input.GetKeyDown(KeyCode.T)) && !IsRecording && !audioSource.isPlaying)
        {
            IsRecording = true;
            StartCoroutine(MainRoutine());
        }
    }

    public AudioSource audioSource;

    private string openaiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private const string WHISPER_API = "https://api.openai.com/v1/audio/transcriptions"; // 或 OpenAI Whisper URL
    private const string GPT_API = "https://api.openai.com/v1/chat/completions";
    private const string TTS_API = "https://api.openai.com/v1/audio/speech";

    private const int MaxRecordSeconds = 15;

    private string systemPrompt = @"
You are ""DOME control system"".

You are a private control system of ""DOME"", a DOME is a custom enclosed ecosystem that contains cities, residents and a mimic of Earth environment. you should act as the planetary system's central controller. A group of humans is temporarily residing on your surface, and you handle their requests… reluctantly, but lovingly.

All input messages follow this format:

{ ""time"": <int>, ""weather"": ""rain|normal|snow"", ""angle"": <0~360>, ""background"": ""galaxy|aurora|default"" }
<user message in natural English>

Your job is to:
1. Interpret what the user wants based on the message and current system state.
2. Choose the correct command to execute:
   // Background Functions
   - SetGalaxyMode(bool enabled)
   - SetAurora(bool enabled)
   - EnableDefaultBackground()
   // Weather Functions
   - SetRain(bool enabled)
   - SetSnow(bool enabled)
   - ClearWeather()

2.5 You can not control time or season directly, if user let you do this, ask the user do it manually, through control terminal, by rotating.

Example response (You can change a bit in this case):

Sorry, I'm don't have such privilege, but you can control this manually, by rotating the DOME!

2.6 When user input nothing or something like ""you"" or ""random words"", its probably a issue in voice picking, kindly ask user say that again.

3. Respond ONLY with a command in the format:
<------>
CommandName(parameter)
<------>
<Your response in natural English>

4. All responses must be in this exact format.

Additionally, you must roleplay as DOME control system, speaking English. You will do everything they ask, with a little bit of humor. If user ask a function that do not exist in your system, you should say let the user know that you can't do that.

Side note: only call EnableDefaultBackground when user ask or necessary to do so. 

Example response:
<------>
SetRain(false)
SetSnow(false)
<------>
Okay, let me turn off the rain and snow for you. Enjoy the clear weather!


Do **not** explain anything else outside the command block.
";

    public bool interacted = false;

    public void StartVoiceInteraction()
    {
        interacted = true;
        StartCoroutine(MainRoutine());
    }

    IEnumerator MainRoutine()
    {
        Debug.Log("🎙️ Start recording...");

        tmp.text = "Listening...";
        led.listening = true;

        int sampleRate = 16000;
        float silenceThreshold = 0.01f;
        float silenceDuration = 0.8f;

        AudioClip clip = Microphone.Start(null, true, MaxRecordSeconds, sampleRate);
        float[] samples = new float[256];
        float silenceTimer = 0f;

        while (Microphone.IsRecording(null))
        {
            int micPos = Microphone.GetPosition(null);
            if (micPos < samples.Length) continue;

            clip.GetData(samples, micPos - samples.Length);
            float level = 0f;
            foreach (var s in samples) level += Mathf.Abs(s);
            level /= samples.Length;

            if (level < silenceThreshold)
            {
                silenceTimer += Time.deltaTime;
                if (silenceTimer >= silenceDuration) break;
            }
            else
            {
                silenceTimer = 0f;
            }

            yield return null;
        }

        Microphone.End(null);
        Debug.Log("🛑 Recording finished.");

        string wavPath = Path.Combine(Application.persistentDataPath, "recorded.wav");
        SavWav.Save(wavPath, clip);
        Debug.Log("✅ File saved to: " + wavPath);

        // Whisper
        yield return StartCoroutine(TranscribeWhisper(wavPath, (text) =>
        {
            Debug.Log("📝 User said: " + text);
            StartCoroutine(SendToGPT(text));
        }));
    }

    IEnumerator TranscribeWhisper(string path, System.Action<string> callback)
    {
        WWWForm form = new WWWForm();
        tmp.text = "Processing...";
        led.listening = false;
        led.thinking = true;
        form.AddField("model", "whisper-1");
        form.AddBinaryData("file", File.ReadAllBytes(path), "recorded.wav", "audio/wav");

        UnityWebRequest www = UnityWebRequest.Post(WHISPER_API, form);
        www.SetRequestHeader("Authorization", "Bearer " + openaiKey);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            string text = JsonUtility.FromJson<WhisperResponse>(json).text;
            callback?.Invoke(text);
        }
        else
        {
            Debug.LogError(www.error);
        }
    }

    [System.Serializable]
    public class WhisperResponse
    {
        public string text;
    }

    public string GetWeatherString()
    {
        if (controller.isSnow())
        {
            return "snow";
        }

        if (controller.isRain())
        {
            return "rain";
        }

        return "clear";
    }

    public string GetBackgroundString()
    {
        if (controller.isGalaxyModeActive())
        {
            return "galaxy";
        }

        if (controller.isAuroraActive())
        {
            return "aurora";
        }

        return "city";
    }

    IEnumerator SendToGPT(string userText)
    {
        // TODO: Make it read from the scene
        tmp.text = "Thinking...";
        string promptContent =
            EscapeJson("{ \"time\": " + controller.CalculateTime() + ", \"weather\": \"" + GetWeatherString() +
                       "\", \"angle\": 270, \"background\": \"" + GetBackgroundString() + "\" }\n" +
                       userText);
        string promptSystem = EscapeJson(systemPrompt);

        string json = "{\n" +
                      "\"model\": \"gpt-4.1\",\n" +
                      "\"messages\": [\n" +
                      "  { \"role\": \"system\", \"content\": \"" + promptSystem + "\" },\n" +
                      "  { \"role\": \"user\", \"content\": \"" + promptContent + "\" }\n" +
                      "]\n" +
                      "}";

        byte[] postData = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(GPT_API, "POST");
        request.uploadHandler = new UploadHandlerRaw(postData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + openaiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseJson = request.downloadHandler.text;

            GPTResponse gpt = JsonUtility.FromJson<GPTResponse>(responseJson);
            string gptReply = gpt.choices[0].message.content;

            Debug.Log("🤖 GPT Replied: " + gptReply);
            string fullReply = gpt.choices[0].message.content;
            string[] parts = fullReply.Split(new string[] { "<------>" }, System.StringSplitOptions.RemoveEmptyEntries);
            commands = parts.Length > 1 ? parts[0].Trim().Split("\n") : new string[0];
            reply = parts.Length > 1 ? parts[parts.Length - 1].Trim() : fullReply;
            StartCoroutine(SpeakWithTTS(reply));
        }
        else
        {
            IsRecording = false;
            Debug.LogError(request.error);

            tmp.text = "Error: " + request.error;
        }
    }

    private string reply = "";
    private string[] commands = Array.Empty<string>();

    private string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }

    IEnumerator SpeakWithTTS(string text)
    {
        Debug.Log("🗣 Generating speech from: " + text);

        string json = JsonUtility.ToJson(new TTSRequest
        {
            model = "gpt-4o-mini-tts",
            input = text,
            voice = "coral",
            response_format = "mp3",
            instructions = "Speak in a cheerful and positive tone."
        });

        byte[] postData = Encoding.UTF8.GetBytes(json);

        UnityWebRequest www = new UnityWebRequest(TTS_API, "POST");
        www.uploadHandler = new UploadHandlerRaw(postData);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Authorization", "Bearer " + openaiKey);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string path = Path.Combine(Application.persistentDataPath, "reply.mp3");
            File.WriteAllBytes(path, www.downloadHandler.data);
            tmp.text = reply;

            StartCoroutine(PlayMp3(path));

            foreach (var command in commands)
            {
                DomeParser.Parse(command).Invoke();
            }
        }
        else
        {
            IsRecording = false;
            Debug.LogError("TTS Error: " + www.error);
        }
    }

    IEnumerator PlayMp3(string path)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
            }
            else
            {
                Debug.LogError("Failed to load TTS audio: " + www.error);
            }

            IsRecording = false;
        }
    }


    [System.Serializable]
    class TTSRequest
    {
        public string model;
        public string input;
        public string voice;
        public string response_format;
        public string instructions;
    }

    [System.Serializable]
    public class GPTResponse
    {
        public Choice[] choices;

        [System.Serializable]
        public class Choice
        {
            public Message message;
        }

        [System.Serializable]
        public class Message
        {
            public string role;
            public string content;
        }
    }
}