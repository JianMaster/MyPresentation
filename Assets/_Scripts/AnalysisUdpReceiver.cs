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
        "baseline_ready",
        "arousal_score",
        "raw_arousal_score",
        "arousal_level",
        "dominance_score",
        "raw_dominance_score",
        "dominance_level",
        "delivery_style",
        "relative_volume_score",
    };

    [SerializeField] private int listenPort = 5005;
    [SerializeField] private DeliveryPacket latestPacket;
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

    public event Action<DeliveryPacket> DeliveryReceived;

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

        if (!TryParseDeliveryPacket(json, out DeliveryPacket packet, out string parseError)) {
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
        DeliveryReceived?.Invoke(packet);
    }

    private void OnDisable() {
        StopReceiver();
    }

    private void OnValidate() {
        listenPort = Mathf.Clamp(listenPort, 1, 65535);
    }

    public static bool TryParseDeliveryPacket(string json, out DeliveryPacket packet, out string error) {
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
                error = $"UDP payload is not the delivery-only VoiceAnalyzer root schema (missing '{field}').";
                return false;
            }

            packet = JsonUtility.FromJson<DeliveryPacket>(json);
        }
        catch (Exception exception) {
            error = exception.Message;
            return false;
        }

        if (packet == null || packet.timestamp <= 0d || packet.sequence_id <= 0 ||
            packet.feature_window_seconds <= 0f || string.IsNullOrWhiteSpace(packet.delivery_style)) {
            packet = null;
            error = "UDP payload is not the delivery-only VoiceAnalyzer root schema.";
            return false;
        }
        return true;
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
                Name = "Delivery UDP Receiver"
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
public sealed class DeliveryPacket {
    public double timestamp;
    public long sequence_id;
    public bool speech_detected;
    public float feature_window_seconds;
    public bool baseline_ready;
    public double arousal_score;
    public double raw_arousal_score;
    public string arousal_level;
    public double dominance_score;
    public double raw_dominance_score;
    public string dominance_level;
    public string delivery_style;
    public double relative_volume_score;
}
