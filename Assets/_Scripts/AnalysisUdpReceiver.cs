using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class AnalysisUdpReceiver : MonoBehaviour {
    private static readonly string[] RequiredRootFields = {
        "timestamp",
        "sequence_id",
        "speech_detected",
        "feature_window_seconds",
        "arousal",
        "valence",
        "speech_rate_value",
        "speech_rate_level",
        "volume_value",
        "volume_level",
    };

    [SerializeField] private int listenPort = 5005;
    [SerializeField] private VoiceAnalysisPacket latestPacket;
    [SerializeField] private string latestJson;
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
    private double lastAcceptedTimestamp;
    private long lastAcceptedSequenceId;

    public event Action<VoiceAnalysisPacket> AnalysisReceived;

    private void OnEnable() {
        StartReceiver();
    }

    private void Update() {
        string json = null;
        string error = null;

        lock (pendingLock) {
            if (hasPendingJson) {
                json = pendingJson;
                pendingJson = null;
                hasPendingJson = false;
            }

            if (hasPendingError) {
                error = pendingError;
                pendingError = null;
                hasPendingError = false;
            }
        }

        if (!string.IsNullOrEmpty(error)) {
            lastError = error;
        }
        if (string.IsNullOrEmpty(json)) return;

        if (!TryParseAnalysisPacket(json, out VoiceAnalysisPacket packet, out string parseError)) {
            lastError = parseError;
            return;
        }
        if (packet.timestamp <= lastAcceptedTimestamp && packet.sequence_id <= lastAcceptedSequenceId) return;

        latestPacket = packet;
        lastAcceptedTimestamp = packet.timestamp;
        lastAcceptedSequenceId = packet.sequence_id;
        latestJson = json;
        packetsReceived = Volatile.Read(ref receivedCount);
        lastError = string.Empty;
        AnalysisReceived?.Invoke(packet);
    }

    private void OnDisable() {
        StopReceiver();
    }

    private void OnValidate() {
        listenPort = Mathf.Clamp(listenPort, 1, 65535);
    }

    public static bool TryParseAnalysisPacket(string json, out VoiceAnalysisPacket packet, out string error) {
        packet = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json)) {
            error = "Received an empty UDP payload.";
            return false;
        }

        try {
            JObject root = JObject.Parse(json);
            foreach (string field in RequiredRootFields) {
                if (root.Property(field) != null) continue;
                error = $"UDP payload is not the VoiceAnalyzer Arousal/Valence root schema (missing '{field}').";
                return false;
            }

            if (root["speech_detected"].Type != JTokenType.Boolean) {
                error = "'speech_detected' must be a boolean.";
                return false;
            }

            bool speechDetected = root.Value<bool>("speech_detected");
            if (speechDetected &&
                (!TryReadNormalizedScore(root["arousal"], out _) ||
                 !TryReadNormalizedScore(root["valence"], out _))) {
                error = "Speech frames require numeric Arousal and Valence values in [-1, 1].";
                return false;
            }
            if (!speechDetected) {
                if (!NormalizeOptionalEmotionScore(root, "arousal") ||
                    !NormalizeOptionalEmotionScore(root, "valence")) {
                    error = "Arousal and Valence must be null or numeric values in [-1, 1].";
                    return false;
                }
            }
            if (!TryReadFiniteNumber(root["speech_rate_value"], out _) ||
                !TryReadFiniteNumber(root["volume_value"], out _)) {
                error = "Speech-rate and volume reference values must be finite numbers.";
                return false;
            }

            packet = JsonUtility.FromJson<VoiceAnalysisPacket>(root.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch (Exception exception) {
            error = exception.Message;
            return false;
        }

        if (packet == null || packet.timestamp <= 0d || packet.sequence_id <= 0 ||
            packet.feature_window_seconds <= 0f ||
            !IsSupportedLevel(packet.speech_rate_level) || !IsSupportedLevel(packet.volume_level)) {
            packet = null;
            error = "UDP payload is not the VoiceAnalyzer Arousal/Valence root schema.";
            return false;
        }
        return true;
    }

    private static bool TryReadNormalizedScore(JToken token, out double value) {
        return TryReadFiniteNumber(token, out value) && value >= -1d && value <= 1d;
    }

    private static bool NormalizeOptionalEmotionScore(JObject root, string field) {
        JToken token = root[field];
        if (token.Type == JTokenType.Null) {
            root[field] = 0d;
            return true;
        }
        return TryReadNormalizedScore(token, out _);
    }

    private static bool TryReadFiniteNumber(JToken token, out double value) {
        value = 0d;
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) {
            return false;
        }

        value = token.Value<double>();
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsSupportedLevel(string level) {
        return level == "low" || level == "medium" || level == "high";
    }

    private void StartReceiver() {
        if (isRunning) return;

        try {
            udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Loopback, listenPort));

            isRunning = true;
            receiveThread = new Thread(ReceiveLoop) {
                IsBackground = true,
                Name = "Voice Analysis UDP Receiver"
            };
            receiveThread.Start();
            lastError = string.Empty;
        }
        catch (Exception exception) {
            lastError = exception.Message;
            StopReceiver();
        }
    }

    private void ReceiveLoop() {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning) {
            try {
                byte[] bytes = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(bytes);

                lock (pendingLock) {
                    pendingJson = json;
                    hasPendingJson = true;
                }

                Interlocked.Increment(ref receivedCount);
            }
            catch (ObjectDisposedException) {
                break;
            }
            catch (SocketException) {
                if (isRunning) break;
            }
            catch (Exception exception) {
                lock (pendingLock) {
                    pendingError = exception.Message;
                    hasPendingError = true;
                }
            }
        }
    }

    private void StopReceiver() {
        isRunning = false;

        if (udpClient != null) {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null) {
            receiveThread.Join(200);
            receiveThread = null;
        }
    }
}

[Serializable]
public sealed class VoiceAnalysisPacket {
    public double timestamp;
    public long sequence_id;
    public bool speech_detected;
    public float feature_window_seconds;
    public double arousal;
    public double valence;
    public double speech_rate_value;
    public string speech_rate_level;
    public double volume_value;
    public string volume_level;
}
