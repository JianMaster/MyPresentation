using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class AnalysisUdpReceiver : MonoBehaviour
{
    [SerializeField] private int listenPort = 5005;
    [SerializeField] private AnalysisData latestData;
    [SerializeField] private string latestJson;
    [SerializeField] private bool hasData;
    [SerializeField] private int packetsReceived;
    [SerializeField] private string lastError;

    private readonly object pendingLock = new object();
    private UdpClient udpClient;
    private Thread receiveThread;
    private string pendingJson;
    private string pendingError;
    private bool hasPendingJson;
    private bool hasPendingError;
    private volatile bool isRunning;
    private int receivedCount;

    public AnalysisData LatestData => latestData;
    public bool HasData => hasData;

    private void OnEnable()
    {
        StartReceiver();
    }

    private void Update()
    {
        string json = null;
        string error = null;

        lock (pendingLock)
        {
            if (hasPendingJson)
            {
                json = pendingJson;
                pendingJson = null;
                hasPendingJson = false;
            }

            if (hasPendingError)
            {
                error = pendingError;
                pendingError = null;
                hasPendingError = false;
            }
        }

        if (!string.IsNullOrEmpty(error))
        {
            lastError = error;
        }

        if (string.IsNullOrEmpty(json)) return;

        try
        {
            latestData = JsonUtility.FromJson<AnalysisData>(json);
            latestJson = json;
            hasData = true;
            packetsReceived = Volatile.Read(ref receivedCount);
            lastError = string.Empty;
            Debug.Log($"Received AnalysisData: {JsonUtility.ToJson(latestData)}");
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
        }
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnApplicationQuit()
    {
        StopReceiver();
    }

    private void OnValidate()
    {
        listenPort = Mathf.Clamp(listenPort, 1, 65535);
    }

    private void StartReceiver()
    {
        if (isRunning) return;

        try
        {
            udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Loopback, listenPort));

            isRunning = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Analysis UDP Receiver"
            };
            receiveThread.Start();
            lastError = string.Empty;
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            StopReceiver();
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] bytes = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(bytes);

                lock (pendingLock)
                {
                    pendingJson = json;
                    hasPendingJson = true;
                }

                Interlocked.Increment(ref receivedCount);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (isRunning) break;
            }
            catch (Exception exception)
            {
                lock (pendingLock)
                {
                    pendingError = exception.Message;
                    hasPendingError = true;
                }
            }
        }
    }

    private void StopReceiver()
    {
        isRunning = false;

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null)
        {
            receiveThread.Join(200);
            receiveThread = null;
        }
    }
}

[Serializable]
public class AnalysisData
{
    public double timestamp;
    public int sample_rate;
    public int block_seconds;
    public int feature_window_seconds;
    public int asr_window_seconds;
    public AnalysisEnabled enabled;
    public AnalysisAnalyzers analyzers;
}

[Serializable]
public class AnalysisEnabled
{
    public bool pronunciation;
    public bool prosody;
    public bool asr;
}

[Serializable]
public class AnalysisAnalyzers
{
    public PronunciationAnalysis pronunciation;
    public ProsodyAnalysis prosody;
    public AsrAnalysis asr;
}

[Serializable]
public class PronunciationAnalysis
{
    public double hnr_db;
    public double jitter;
    public double shimmer_db;
    public double spectral_flux;
}

[Serializable]
public class ProsodyAnalysis
{
    public double mean_pitch_st;
    public double pitch_range_st;
    public double pitch_variation;
    public double mean_loudness;
    public double loudness_range;
    public double loudness_variation;
    public double sound_level_db;
}

[Serializable]
public class AsrAnalysis
{
    public string status;
    public string transcript;
    public double speech_rate_cpm;
    public int pause_count;
    public double speaking_ratio;
    public double available_seconds;
    public int required_seconds;
    public string error;
}
