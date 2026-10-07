using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BeOdysseus
{
    public enum TabletLinkState
    {
        /// <summary>연결한 태블릿이 없음.</summary>
        NotPaired,
        /// <summary>코드를 띄우고 태블릿이 입력하기를 기다리는 중.</summary>
        Pairing,
        Connected,
        /// <summary>연결했던 태블릿이 있지만 지금은 연락이 안 돼 hello로 다시 찾는 중.</summary>
        Reconnecting,
    }

    /// <summary>
    /// 폰 → 태블릿 UDP 연결. 규격은 docs/TABLET_LINK_PROTOCOL.md.
    /// - 연결: 4자리 코드를 만들고, 태블릿이 그 코드를 입력할 때까지 0.5초마다 pair_offer를 브로드캐스트한다.
    /// - 유지: 1초마다 ping. 태블릿이 ping을 보내는 경우 3초 동안 아무 메시지가 없으면 끊긴 것으로 보고,
    ///   저장해 둔 짝을 hello로 다시 찾는다.
    /// - 게임 기록은 태블릿이 받았다는 답을 줄 때까지 0.25초마다 최대 8번 다시 보낸다(seq는 그대로).
    /// - seq는 앱을 다시 켜도 이어서 올라간다.
    /// 소켓 수신은 별도 스레드에서 하고, 처리는 전부 메인 스레드(Update)에서 한다.
    /// </summary>
    public class TabletLink : MonoBehaviour
    {
        private const string SessionKey = "tabletLink.session";
        private const string AddressKey = "tabletLink.address";
        private const string PortKey = "tabletLink.port";
        private const string DeviceKey = "tabletLink.device";
        // seq를 매번 저장하지 않고 100개씩 미리 잡아 저장한다. 앱이 갑자기 꺼져도 다음엔 그보다 큰 번호부터 쓴다.
        private const string SeqReservedKey = "tabletLink.seqReserved";
        private const int SeqReserveBlock = 100;
        // 태블릿이 계속 없을 때 보낼 메시지가 끝없이 쌓이지 않게 하는 상한.
        private const int MaxPending = 100;

        private class PendingSend
        {
            public string Json;
            public float NextSendTime;
            public int Attempts;
        }

        private readonly ConcurrentQueue<(string Json, IPEndPoint From)> _inbox = new();
        private readonly SortedDictionary<int, PendingSend> _pending = new();

        private UdpClient _socket;
        private Thread _receiveThread;
        private volatile bool _running;
        private int _seq;
        private int _seqReserved;
        private string _session;
        private string _savedSession;
        private IPEndPoint _tablet;
        private IPEndPoint _savedTablet;
        private string _code;
        private bool _tabletSendsPing;
        private float _lastHeardTime;
        private float _nextOfferTime;
        private float _nextHelloTime;
        private float _nextPingTime;

        public TabletLinkState State { get; private set; }
        public bool IsConnected => State == TabletLinkState.Connected;
        public string PairingCode => _code;
        public string TabletDevice { get; private set; }
        /// <summary>받았다는 답을 아직 못 받은 게임 기록 수.</summary>
        public int PendingCount => _pending.Count;

        private void OnEnable()
        {
            _seqReserved = PlayerPrefs.GetInt(SeqReservedKey, 0);
            _seq = _seqReserved;
            LoadSaved();
            OpenSocket();
        }

        private void OnDisable() => CloseSocket();

        /// <summary>새 코드로 연결을 시작한다. 이미 연결돼 있던 짝은 새로 연결될 때까지 그대로 기억한다.</summary>
        public void StartPairing()
        {
            _code = UnityEngine.Random.Range(0, 10000).ToString("D4");
            _session = UnityEngine.Random.Range(0, 100000000).ToString("D8");
            _nextOfferTime = 0f;
            SetState(TabletLinkState.Pairing);
            Debug.Log($"[TabletLink] 연결 코드 {_code} (session {_session})");
        }

        /// <summary>코드 화면을 닫았을 때. 기억해 둔 짝이 있으면 그쪽으로 다시 붙는다.</summary>
        public void CancelPairing()
        {
            if (State != TabletLinkState.Pairing) return;
            _code = null;
            _session = _savedSession;
            _tablet = _savedTablet;
            SetState(string.IsNullOrEmpty(_session) ? TabletLinkState.NotPaired : TabletLinkState.Reconnecting);
        }

        /// <summary>연결을 끊고 기억한 짝을 지운다. 태블릿에도 unpair를 3번 보낸다.</summary>
        public void Unpair()
        {
            if (_tablet != null && !string.IsNullOrEmpty(_session))
            {
                for (int i = 0; i < LinkProtocol.UnpairRepeat; i++)
                    SendRaw(Prepare(new LinkMessage { type = LinkProtocol.Types.Unpair }), _tablet);
            }

            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.DeleteKey(AddressKey);
            PlayerPrefs.DeleteKey(PortKey);
            PlayerPrefs.DeleteKey(DeviceKey);
            PlayerPrefs.Save();
            _session = _savedSession = null;
            _tablet = _savedTablet = null;
            TabletDevice = null;
            _pending.Clear();
            SetState(TabletLinkState.NotPaired);
        }

        /// <summary>
        /// 빠지면 안 되는 메시지(게임 기록)를 보낸다. 태블릿이 받았다는 답을 줄 때까지 다시 보낸다.
        /// 연결한 태블릿이 없으면 보내지 않는다. 잠시 끊긴 동안에는 쌓아 두었다가 다시 연결되면 보낸다.
        /// </summary>
        public void SendReliable(LinkMessage message)
        {
            if (State is TabletLinkState.NotPaired or TabletLinkState.Pairing) return;
            if (_pending.Count >= MaxPending) return;

            string json = Prepare(message);
            _pending[message.seq] = new PendingSend { Json = json, NextSendTime = 0f, Attempts = 0 };
        }

        private void Update()
        {
            while (_inbox.TryDequeue(out var item)) HandleReply(item.Json, item.From);

            float now = Time.unscaledTime;
            switch (State)
            {
                case TabletLinkState.Pairing:
                    if (now < _nextOfferTime) break;
                    _nextOfferTime = now + LinkProtocol.PairOfferIntervalSeconds;
                    Broadcast(Prepare(new PairOfferMessage
                    {
                        type = LinkProtocol.Types.PairOffer, code = _code, port = LinkProtocol.PhonePort, device = SystemInfo.deviceModel,
                    }));
                    break;

                case TabletLinkState.Reconnecting:
                    if (now < _nextHelloTime) break;
                    _nextHelloTime = now + LinkProtocol.HelloIntervalSeconds;
                    string hello = Prepare(new HelloMessage
                    {
                        type = LinkProtocol.Types.Hello, port = LinkProtocol.PhonePort, device = SystemInfo.deviceModel,
                    });
                    Broadcast(hello);
                    if (_tablet != null) SendRaw(hello, _tablet);
                    break;

                case TabletLinkState.Connected:
                    if (_tabletSendsPing && now - _lastHeardTime > LinkProtocol.AliveTimeoutSeconds)
                    {
                        Debug.Log("[TabletLink] 태블릿 응답 없음 → 다시 찾는 중");
                        SetState(TabletLinkState.Reconnecting);
                        break;
                    }
                    if (now >= _nextPingTime)
                    {
                        _nextPingTime = now + LinkProtocol.PingIntervalSeconds;
                        SendRaw(Prepare(new LinkMessage { type = LinkProtocol.Types.Ping }), _tablet);
                    }
                    ResendPending(now);
                    break;
            }
        }

        /// <summary>
        /// 태블릿이 보낸 답을 처리한다. 태블릿 → 폰 형식은 태블릿 쪽에서 정하기로 해서, 지금은 임시 형식
        /// (pair_accept / hello_ack / ack / ping / unpair, from = "tablet")으로 읽는다. 형식이 오면 여기를 맞춘다.
        /// </summary>
        private void HandleReply(string json, IPEndPoint from)
        {
            TabletReply reply;
            try
            {
                reply = JsonUtility.FromJson<TabletReply>(json);
            }
            catch (Exception)
            {
                return;
            }
            if (reply == null || reply.from != "tablet") return;

            switch (reply.type)
            {
                case LinkProtocol.Types.PairAccept:
                    OnPairAccept(reply, from);
                    return;
                case LinkProtocol.Types.HelloAck:
                    OnHelloAck(reply, from);
                    return;
            }

            if (string.IsNullOrEmpty(_session) || reply.session != _session) return;
            _lastHeardTime = Time.unscaledTime;
            // 끊겼다고 본 사이에 태블릿이 다시 말을 걸어오면, 그 주소로 다시 연결된 것으로 본다.
            if (State == TabletLinkState.Reconnecting)
            {
                _tablet = new IPEndPoint(from.Address, _tablet?.Port ?? LinkProtocol.TabletPort);
                SetConnected(TabletDevice);
            }

            switch (reply.type)
            {
                case LinkProtocol.Types.Ack:
                    _pending.Remove(reply.ackSeq);
                    break;
                case LinkProtocol.Types.Ping:
                    _tabletSendsPing = true;
                    break;
                case LinkProtocol.Types.Unpair:
                    Debug.Log("[TabletLink] 태블릿이 연결을 해제함");
                    Unpair();
                    break;
            }
        }

        private void OnPairAccept(TabletReply reply, IPEndPoint from)
        {
            if (State != TabletLinkState.Pairing || reply.code != _code || reply.session != _session) return;
            _tablet = new IPEndPoint(from.Address, reply.port > 0 ? reply.port : LinkProtocol.TabletPort);
            SetConnected(reply.device);
            SendRaw(Prepare(new LinkMessage { type = LinkProtocol.Types.PairConfirm }), _tablet);
            Save();
            Debug.Log($"[TabletLink] 연결 완료: {reply.device} {_tablet}");
        }

        private void OnHelloAck(TabletReply reply, IPEndPoint from)
        {
            if (State != TabletLinkState.Reconnecting || reply.session != _session) return;
            _tablet = new IPEndPoint(from.Address, reply.port > 0 ? reply.port : LinkProtocol.TabletPort);
            SetConnected(reply.device);
            Save();
            Debug.Log($"[TabletLink] 다시 연결됨: {reply.device} {_tablet}");
        }

        private void SetConnected(string device)
        {
            TabletDevice = device;
            _code = null;
            _lastHeardTime = Time.unscaledTime;
            _nextPingTime = 0f;
            SetState(TabletLinkState.Connected);
        }

        private void SetState(TabletLinkState state)
        {
            if (State == state) return;
            State = state;
            _nextHelloTime = 0f;
        }

        private void ResendPending(float now)
        {
            List<int> exhausted = null;
            foreach (var pair in _pending)
            {
                PendingSend p = pair.Value;
                if (now < p.NextSendTime) continue;
                if (p.Attempts >= LinkProtocol.MaxSendAttempts)
                {
                    (exhausted ??= new List<int>()).Add(pair.Key);
                    continue;
                }
                SendRaw(p.Json, _tablet);
                p.Attempts++;
                p.NextSendTime = now + LinkProtocol.ResendIntervalSeconds;
            }

            if (exhausted == null) return;
            // 받았다는 답이 없어도 연결은 그대로 둔다(태블릿이 답을 아직 안 보내는 단계에서도 기록은 계속 가게).
            foreach (int seq in exhausted)
            {
                Debug.LogWarning($"[TabletLink] seq={seq} 기록을 {LinkProtocol.MaxSendAttempts}번 보냈지만 받았다는 답이 없음");
                _pending.Remove(seq);
            }
        }

        // ── 저장된 짝 ─────────────────────────────

        private void LoadSaved()
        {
            _savedSession = PlayerPrefs.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(_savedSession))
            {
                _savedSession = null;
                State = TabletLinkState.NotPaired;
                return;
            }

            if (IPAddress.TryParse(PlayerPrefs.GetString(AddressKey, ""), out IPAddress address))
                _savedTablet = new IPEndPoint(address, PlayerPrefs.GetInt(PortKey, LinkProtocol.TabletPort));
            TabletDevice = PlayerPrefs.GetString(DeviceKey, "");
            _session = _savedSession;
            _tablet = _savedTablet;
            State = TabletLinkState.Reconnecting;
        }

        private void Save()
        {
            _savedSession = _session;
            _savedTablet = _tablet;
            PlayerPrefs.SetString(SessionKey, _session);
            PlayerPrefs.SetString(AddressKey, _tablet.Address.ToString());
            PlayerPrefs.SetInt(PortKey, _tablet.Port);
            PlayerPrefs.SetString(DeviceKey, TabletDevice ?? "");
            PlayerPrefs.Save();
        }

        // ── 소켓 ─────────────────────────────

        private string Prepare(LinkMessage message)
        {
            message.v = LinkProtocol.Version;
            message.from = "phone";
            message.seq = NextSeq();
            message.session = _session ?? "";
            return JsonUtility.ToJson(message);
        }

        private int NextSeq()
        {
            if (++_seq > _seqReserved)
            {
                _seqReserved = _seq + SeqReserveBlock;
                PlayerPrefs.SetInt(SeqReservedKey, _seqReserved);
                PlayerPrefs.Save();
            }
            return _seq;
        }

        private void OpenSocket()
        {
            try
            {
                _socket = new UdpClient(new IPEndPoint(IPAddress.Any, LinkProtocol.PhonePort)) { EnableBroadcast = true };
                _running = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "TabletLink" };
                _receiveThread.Start();
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[TabletLink] 포트 {LinkProtocol.PhonePort}을 열지 못함: {e.Message}");
            }
        }

        private void CloseSocket()
        {
            _running = false;
            _socket?.Close();
            _socket = null;
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _socket.Receive(ref remote);
                    _inbox.Enqueue((Encoding.UTF8.GetString(data), remote));
                }
                catch (SocketException)
                {
                    // Windows에서는 상대 포트가 닫혀 있으면 다음 Receive에서 예외가 난다. 계속 받는다.
                    if (!_running) return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void SendRaw(string json, IPEndPoint to)
        {
            if (_socket == null || to == null) return;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                _socket.Send(bytes, bytes.Length, to);
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[TabletLink] 보내기 실패({to}): {e.Message}");
            }
        }

        /// <summary>
        /// 같은 네트워크 전체에 보낸다. 핫스팟에 따라 받히는 주소가 달라서 255.255.255.255와
        /// 이 기기 네트워크의 브로드캐스트 주소(끝자리 255) 두 곳으로 보낸다.
        /// </summary>
        private void Broadcast(string json)
        {
            SendRaw(json, new IPEndPoint(IPAddress.Broadcast, LinkProtocol.TabletPort));
            IPAddress subnetBroadcast = GuessSubnetBroadcast();
            if (subnetBroadcast != null) SendRaw(json, new IPEndPoint(subnetBroadcast, LinkProtocol.TabletPort));
            // 에디터에서는 같은 PC에서 돌리는 가짜 태블릿으로도 보낸다.
            if (Application.isEditor) SendRaw(json, new IPEndPoint(IPAddress.Loopback, LinkProtocol.TabletPort));
        }

        /// <summary>이 기기의 IPv4 주소 끝자리를 255로 바꾼 주소(예: 192.168.43.17 → 192.168.43.255).</summary>
        private static IPAddress GuessSubnetBroadcast()
        {
            try
            {
                using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                probe.Connect("10.255.255.255", 1); // 실제로 보내지는 않고, 어느 네트워크로 나가는지만 정한다
                byte[] ip = ((IPEndPoint)probe.LocalEndPoint).Address.GetAddressBytes();
                ip[3] = 255;
                return new IPAddress(ip);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
