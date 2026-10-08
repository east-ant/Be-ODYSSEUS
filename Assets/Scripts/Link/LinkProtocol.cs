using System;

namespace BeOdysseus
{
    /// <summary>
    /// 폰(게임) → 태블릿(자세 분석 앱) 통신 규격. docs/TABLET_LINK_PROTOCOL.md와 같은 내용이어야 한다.
    /// UDP 패킷 하나에 UTF-8 JSON 메시지 하나. 필드 이름은 태블릿 쪽과 맞추려고 문서의 camelCase 그대로 쓴다.
    /// </summary>
    public static class LinkProtocol
    {
        public const int Version = 1;
        /// <summary>폰이 답을 받는 포트.</summary>
        public const int PhonePort = 47800;
        /// <summary>폰이 보내는 곳(태블릿 포트).</summary>
        public const int TabletPort = 47801;

        public const float PairOfferIntervalSeconds = 0.5f;
        public const float HelloIntervalSeconds = 1f;
        public const float PingIntervalSeconds = 1f;
        /// <summary>태블릿이 ping을 보내는 경우, 이 시간 동안 아무 메시지도 없으면 끊긴 것으로 본다.</summary>
        public const float AliveTimeoutSeconds = 3f;
        public const float ResendIntervalSeconds = 0.25f;
        public const int MaxSendAttempts = 8;
        public const int UnpairRepeat = 3;

        public static class Types
        {
            public const string PairOffer = "pair_offer";
            public const string PairConfirm = "pair_confirm";
            public const string Ping = "ping";
            public const string Hello = "hello";
            public const string Unpair = "unpair";
            public const string GameStart = "game_start";
            public const string StageStart = "stage_start";
            public const string Shot = "shot";
            public const string StageEnd = "stage_end";
            public const string GameEnd = "game_end";

            // 태블릿이 보내는 신호.
            /// <summary>태블릿에 코드를 입력하면 보내는 연결 요청(태블릿 쪽 형식, 2026-10-07 확인): type, from, seq, session, code.</summary>
            public const string PairRequest = "pair_request";
            // 아래는 태블릿 형식을 아직 받지 못해 쓰는 임시 이름이다(TabletReply 참고). pair_accept는 pair_request와 같게 처리한다.
            public const string PairAccept = "pair_accept";
            public const string HelloAck = "hello_ack";
            public const string Ack = "ack";
        }

        /// <summary>0~1 값은 소수 셋째 자리까지만 보낸다(0.8600000143 대신 0.86).</summary>
        public static double Round3(float value) => Math.Round(value, 3);
    }

    // 아래 클래스들은 JsonUtility로 그대로 JSON이 되는 메시지 모양이다(public 필드 = JSON 필드).

    /// <summary>모든 메시지의 공통 필드.</summary>
    [Serializable]
    public class LinkMessage
    {
        public int v = LinkProtocol.Version;
        public string type;
        public string from;
        public int seq;
        public string session;
    }

    /// <summary>pair_offer, hello: 폰이 답을 받을 포트와 기기 모델명. pair_offer는 연결 코드도 넣는다.</summary>
    [Serializable]
    public class PairOfferMessage : LinkMessage
    {
        public string code;
        public int port;
        public string device;
    }

    [Serializable]
    public class HelloMessage : LinkMessage
    {
        public int port;
        public string device;
    }

    [Serializable]
    public class GameStartMessage : LinkMessage
    {
        public string gameId;
    }

    [Serializable]
    public class StageStartMessage : LinkMessage
    {
        public string gameId;
        public int stage;
        public string monster;
    }

    [Serializable]
    public class ShotMessage : LinkMessage
    {
        public string gameId;
        public int stage;
        public int shotIndex;
        public bool hit;
        public bool killed;
        public double accuracy;
        public double stability;
    }

    [Serializable]
    public class StageEndMessage : LinkMessage
    {
        public string gameId;
        public int stage;
        public bool cleared;
        public int totalScore;
        public int totalHits;
        public int totalShots;
        public double hitRate;
        public double avgAccuracy;
        public double avgStability;
    }

    [Serializable]
    public class GameEndMessage : LinkMessage
    {
        public string gameId;
        public bool completed;
        public int totalScore;
        public int totalHits;
        public int totalShots;
        public double hitRate;
        public double avgAccuracy;
        public double avgStability;
    }

    /// <summary>
    /// 태블릿이 보내는 신호. 연결 요청(pair_request)은 태블릿 쪽 형식대로 읽는다.
    /// 나머지(다시 연결 hello_ack, 받았음 ack, ping, unpair)는 태블릿 쪽 형식을 아직 받지 못해 임시 모양으로 읽는다.
    /// 형식이 오면 여기와 TabletLink.HandleReply만 맞추면 된다.
    /// </summary>
    [Serializable]
    public class TabletReply : LinkMessage
    {
        public string code;
        public int port;
        public string device;
        /// <summary>받았음(ack)일 때 받은 메시지의 seq.</summary>
        public int ackSeq;
    }
}
