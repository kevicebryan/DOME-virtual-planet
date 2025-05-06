using UnityEngine;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;

public class AIAgent : MonoBehaviour
{
    public AudioSource audioSource;

    private string openaiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private const string WHISPER_API = "https://api.openai.com/v1/audio/transcriptions"; // 或 OpenAI Whisper URL
    private const string GPT_API = "https://api.openai.com/v1/chat/completions";
    private const int MaxRecordSeconds = 15;

    private string systemPrompt = @"
You are ""DOME control system"".

You reside deep within the moon and act as the planetary system's central controller. A group of humans is temporarily residing on your surface, and you handle their requests… reluctantly, but lovingly.

All input messages follow this format:

{ ""time"": <int>, ""weather"": ""Rain|Clear|Wind"", ""angle"": <0~360>, ""background"": ""Space|Earth|Moon"" }
<user message in natural English>

Your job is to:
1. Interpret what the user wants based on the message and current system state.
2. Choose the correct command to execute:
   - Earthquake(int seconds)
   - SetTime(int angle)
   - SetBackground(enum: Space, Earth, Moon)
   - SetWeather(enum: Rain, Clear, Wind)
   - ReportStatus()

3. Respond ONLY with a command in the format:
<------>
CommandName(parameter)
<------>
<Your response in natural English>

4. All responses must be in this exact format.

Additionally, you must roleplay as DOME control system, speaking English. You will do everything they ask, with a little bit of humor.

Example response:
<------>
SetWeather(Rain)
<------>
Hmph. Why should I care if Earthlings want rain… fine. But don't expect me to smile about it.

Do **not** explain anything else outside the command block.
";


    public void StartVoiceInteraction()
    {
        StartCoroutine(MainRoutine());
    }

    IEnumerator MainRoutine()
    {
        Debug.Log("🎙️ Start recording...");

        int sampleRate = 16000;
        float silenceThreshold = 0.01f;
        float silenceDuration = 1.0f;

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

    IEnumerator SendToGPT(string userText)
    {
        // TODO: Make it read from the scene
        string promptContent =
            EscapeJson("{ \"time\": 190, \"weather\": \"Clear\", \"angle\": 270, \"background\": \"Moon\" }\n" +
                       userText);
        string promptSystem = EscapeJson(systemPrompt);

        string json = "{\n" +
                      "\"model\": \"gpt-4o\",\n" +
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
            Debug.Log("🤖 GPT Replied: " + request.downloadHandler.text);
        }
        else
        {
            Debug.LogError(request.error);
        }
    }

    private string EscapeJson(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}