using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace PresentationRewrite {
    public sealed class VoicePacket {
        [JsonRequired] public double utterance_started_at;
        [JsonRequired] public int segment_index;
        [JsonRequired] public float segment_seconds;
        [JsonRequired] public bool speech_ended;
        [JsonRequired] public float A;
        [JsonRequired] public float D;
        [JsonRequired] public float speech_rate;
        [JsonRequired] public float loudness;

        public static VoicePacket Parse(string json) {
            try {
                var packet = JsonConvert.DeserializeObject<VoicePacket>(json);
                if (packet != null && packet.utterance_started_at > 0 && !double.IsInfinity(packet.utterance_started_at) &&
                    packet.segment_index > 0 && packet.segment_seconds > 0 && !float.IsInfinity(packet.segment_seconds) &&
                    packet.A >= -1 && packet.A <= 1 && packet.D >= -1 && packet.D <= 1 &&
                    packet.speech_rate >= 0 && !float.IsInfinity(packet.speech_rate) &&
                    packet.loudness >= 0 && !float.IsInfinity(packet.loudness)) return packet;
            }
            catch (JsonException) { }
            return null;
        }
    }

    public sealed class VoiceReceiver : IDisposable {
        private readonly UdpClient _client;
        private IPEndPoint _sender = new IPEndPoint(IPAddress.Any, 0);

        public VoiceReceiver(int port = 5005) {
            _client = new UdpClient(AddressFamily.InterNetwork);
            try {
                _client.Client.ExclusiveAddressUse = true;
                _client.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
                _client.Client.Blocking = false;
            }
            catch {
                _client.Dispose();
                throw;
            }
        }

        // False: no datagram available. True with null: an invalid datagram was discarded.
        public bool TryReceive(out VoicePacket packet) {
            packet = null;
            try {
                if (!_client.Client.Poll(0, SelectMode.SelectRead)) return false;
                packet = VoicePacket.Parse(Encoding.UTF8.GetString(_client.Receive(ref _sender)));
                return true;
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.WouldBlock) {
                return false;
            }
        }

        public void Dispose() => _client.Dispose();
    }
}
