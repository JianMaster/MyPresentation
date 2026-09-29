using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class AnalysisUdpReceiver : MonoBehaviour {
    private static readonly string[] RequiredRootFields = {
        "A", "D",
        "utterance_started_at", "segment_index", "segment_seconds", "speech_ended",
        "speech_rate",
        "speech_rate_level",
        "loudness",
        "loudness_level",
    };

    [SerializeField] private int listenPort = 5005;
    [SerializeField] private VoiceAnalysisPacket latestPacket;
    [SerializeField] private string latestJson;
    [SerializeField] private int packetsReceived;
    [SerializeField] private string lastError;

    private readonly object pendingLock = new object();
    private UdpClient udpClient;
    private Thread receiveThread;
    private readonly Queue<string> pendingJson = new Queue<string>();
    private string pendingError;
    private bool hasPendingError;
    private volatile bool isRunning;
    private int receivedCount;

    public event Action<VoiceAnalysisPacket> AnalysisReceived;

    private void OnEnable() {
        StartReceiver();
    }

    private void Update() {
        string[] messages;
        string error = null;

        lock (pendingLock) {
            messages = pendingJson.ToArray();
            pendingJson.Clear();

            if (hasPendingError) {
                error = pendingError;
                pendingError = null;
                hasPendingError = false;
            }
        }

        if (!string.IsNullOrEmpty(error)) {
            lastError = error;
        }
        foreach (string json in messages) {
            if (!TryParseAnalysisPacket(json, out VoiceAnalysisPacket packet, out string parseError)) {
                lastError = parseError;
                continue;
            }

            latestPacket = packet;
            latestJson = json;
            packetsReceived = Volatile.Read(ref receivedCount);
            lastError = string.Empty;
            AnalysisReceived?.Invoke(packet);
        }
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
                error = $"VoiceAD packet is missing '{field}'.";
                return false;
            }

            if (root["speech_ended"].Type != JTokenType.Boolean) {
                error = "'speech_ended' must be a boolean.";
                return false;
            }

            if (!TryReadNormalizedScore(root["A"], out _) ||
                !TryReadNormalizedScore(root["D"], out _)) {
                error = "VoiceAD A and D must be finite numbers in [-1, 1].";
                return false;
            }
            if (!TryReadFiniteNumber(root["speech_rate"], out double rate) || rate < 0d || rate > float.MaxValue ||
                !TryReadFiniteNumber(root["loudness"], out double loudness) || loudness < 0d || loudness > float.MaxValue ||
                !TryReadFiniteNumber(root["utterance_started_at"], out double startedAt) || startedAt <= 0d ||
                !TryReadFiniteNumber(root["segment_seconds"], out double seconds) || seconds <= 0d || seconds > float.MaxValue ||
                root["segment_index"].Type != JTokenType.Integer ||
                !IsSupportedLevel(root["speech_rate_level"]) || !IsSupportedLevel(root["loudness_level"])) {
                error = "Invalid VoiceAD timing, segment index, or acoustic values.";
                return false;
            }

            packet = root.ToObject<VoiceAnalysisPacket>();
        }
        catch (Exception exception) {
            error = exception.Message;
            return false;
        }

        if (packet == null || packet.segment_index <= 0 || packet.segment_seconds <= 0f) {
            packet = null;
            error = "Invalid VoiceAD segment index.";
            return false;
        }
        return true;
    }

    private static bool TryReadNormalizedScore(JToken token, out double value) {
        return TryReadFiniteNumber(token, out value) && value >= -1d && value <= 1d;
    }

    private static bool TryReadFiniteNumber(JToken token, out double value) {
        value = 0d;
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) {
            return false;
        }

        value = token.Value<double>();
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsSupportedLevel(JToken token) {
        return token.Type == JTokenType.Integer && token.Value<long>() >= -1 && token.Value<long>() <= 1;
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
                    // Keep a final packet even when another segment arrives in the same frame.
                    if (pendingJson.Count >= 128) pendingJson.Dequeue();
                    pendingJson.Enqueue(json);
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
        lock (pendingLock) {
            pendingJson.Clear();
            hasPendingError = false;
        }
    }
}

[Serializable]
public sealed class VoiceAnalysisPacket {
    public double utterance_started_at;
    public int segment_index;
    public float segment_seconds;
    public bool speech_ended;
    public double A;
    public double D;
    public double speech_rate;
    public int speech_rate_level;
    public double loudness;
    public int loudness_level;
}
