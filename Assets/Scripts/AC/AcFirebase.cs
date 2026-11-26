// AcFirebase.cs — Firebase Realtime Database로 실시간 동기화 (휴대폰 웹·PC 공통)
// 데이터: rooms/<방코드>/players/<id> (내 위치 등, 0.2초마다 씀), rooms/<방코드>/orn/<id> (트리 장식)
// 웹: EventSource 스트림으로 바뀐 것만 받음 / 에디터·PC: 0.35초마다 통째로 읽음
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AcFirebase : MonoBehaviour
{
    // Firebase 프로젝트 ID(xmas-plaza)는 만든 뒤 바꿀 수 없어서 주소는 그대로 둠
    public const string DbUrl = "https://xmas-plaza-default-rtdb.asia-southeast1.firebasedatabase.app";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void AcWeb_Stream(string url, string go);
    [DllImport("__Internal")] static extern void AcWeb_StopStream();
    [DllImport("__Internal")] static extern void AcWeb_LeaveOnClose(string url);
    const bool UseStream = true;
#else
    const bool UseStream = false;
#endif

    string room, myId;
    AcNet me;
    bool writing, started;
    float nextWrite, nextPoll;
    long seq;
    Dictionary<string, object> tree = new Dictionary<string, object>();
    public string Status = "연결 중...";
    public bool Connected;

    // 서버 시계 맞추기: 내 기록에 서버 시각을 넣고 다시 읽어서 비교
    double serverT; float serverAt = -1;
    public double ServerNow => serverAt < 0 ? 0 : serverT + (Time.realtimeSinceStartup - serverAt) * 1000.0;

    string Base => DbUrl + "/rooms/" + UnityWebRequest.EscapeURL(room);
    string MyUrl => Base + "/players/" + UnityWebRequest.EscapeURL(myId) + ".json";

    public bool Offline;   // 혼자 둘러보기: 서버 없이 이 기기 안에서만 (눈사람·편지도 똑같은 코드로)

    public void Begin(string roomCode, string id, AcNet init, bool offline = false)
    {
        room = string.IsNullOrWhiteSpace(roomCode) ? "xmas" : roomCode.Trim();
        myId = id; me = init; me.id = id; started = true; Offline = offline;
        if (offline) { Status = ""; return; }
#if UNITY_WEBGL && !UNITY_EDITOR
        AcWeb_Stream(Base + ".json", gameObject.name);
        AcWeb_LeaveOnClose(MyUrl);
#endif
    }

    public void SetMe(AcNet s) { if (me != null) { s.id = myId; me = s; } }

    void Update()
    {
        if (!started || Offline) return;
        if (!writing && Time.unscaledTime >= nextWrite)
        {
            nextWrite = Time.unscaledTime + 0.2f;
            me.seq = ++seq;
            string json = JsonUtility.ToJson(me);
            int ti = json.LastIndexOf("\"t\":", StringComparison.Ordinal);
            if (ti >= 0) { int end = json.IndexOfAny(new[] { ',', '}' }, ti); json = json.Substring(0, ti) + "\"t\":{\".sv\":\"timestamp\"}" + json.Substring(end); }
            StartCoroutine(Send("PUT", MyUrl, json, true));
        }
        if (!UseStream && Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 0.35f;
            StartCoroutine(Poll());
        }
    }

    IEnumerator Send(string method, string url, string body, bool isMe = false)
    {
        if (isMe) writing = true;
        using (var r = new UnityWebRequest(url, method))
        {
            if (body != null) r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            r.downloadHandler = new DownloadHandlerBuffer();
            r.timeout = 8;
            yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success) { Status = "인터넷 연결을 확인해 주세요"; Connected = false; }
            else if (isMe)
            {
                Connected = true; Status = "";
                // 응답에 서버가 넣은 시각이 들어 있음
                var d = AcJson.Parse(r.downloadHandler.text) as Dictionary<string, object>;
                double t = AcJson.D(d, "t");
                if (t > 0) { serverT = t; serverAt = Time.realtimeSinceStartup; }
            }
        }
        if (isMe) writing = false;
    }

    IEnumerator Poll()
    {
        using (var r = UnityWebRequest.Get(Base + ".json"))
        {
            r.timeout = 8;
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
                tree = AcJson.Parse(r.downloadHandler.text) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }
    }

    // 웹 스트림 이벤트 ("put\n{...}" / "patch\n{...}")
    public void OnFbEvent(string msg)
    {
        int nl = msg.IndexOf('\n');
        string type = nl > 0 ? msg.Substring(0, nl) : msg, data = nl > 0 ? msg.Substring(nl + 1) : "";
        if (type == "error") { if (data == "conn") Status = "다시 연결하는 중..."; return; }
        var ev = AcJson.Parse(data) as Dictionary<string, object>;
        if (ev == null) return;
        string path = AcJson.S(ev, "path");
        ev.TryGetValue("data", out var val);
        if (type == "put") SetAt(path, val);
        else if (type == "patch" && val is Dictionary<string, object> pd)
            foreach (var kv in pd) SetAt(path.TrimEnd('/') + "/" + kv.Key, kv.Value);
        Status = "";
    }

    void SetAt(string path, object val)
    {
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) { tree = val as Dictionary<string, object> ?? new Dictionary<string, object>(); return; }
        var cur = tree;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!(cur.TryGetValue(parts[i], out var nx) && nx is Dictionary<string, object> nd)) { nd = new Dictionary<string, object>(); cur[parts[i]] = nd; }
            cur = nd;
        }
        if (val == null) cur.Remove(parts[parts.Length - 1]); else cur[parts[parts.Length - 1]] = val;
    }

    Dictionary<string, object> Node(string k) => tree.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;

    // ===== 눈사람·편지·쪽지 =====
    // 지금 데이터베이스 규칙은 rooms/<방>/players 와 rooms/<방>/orn 아래만 쓸 수 있어서,
    // 겉으로는 "snow/<id>", "letters/<id>", "msg/<받는id>/<id>"로 쓰고 실제로는 orn 아래 "x~종류~..." 이름으로 저장함
    static string Phys(string path)
    {
        var p = path.Split('/');
        if (p[0] == "snow" && p.Length == 2) return "orn/x~snow~" + p[1];
        if (p[0] == "letters" && p.Length == 2) return "orn/x~letter~" + p[1];
        if (p[0] == "msg" && p.Length == 3) return "orn/x~msg~" + p[1] + "~" + p[2];
        if (p[0] == "members" && p.Length == 2) return "orn/x~member~" + p[1];
        if (p[0] == "scores" && p.Length == 2) return "orn/x~score~" + p[1];
        if (p[0] == "quiz" && p.Length == 2) return "orn/x~quiz~" + p[1];
        if (p[0] == "sbf" && p.Length == 2) return "orn/x~sbf~" + p[1];
        if (p[0] == "qzs" && p.Length == 2) return "orn/x~qzs~" + p[1];
        if (p[0] == "candy" && p.Length == 2) return "orn/x~candy~" + p[1];
        if (p[0] == "plants" && p.Length == 2) return "orn/x~plant~" + p[1];   // 온실 화분 (11-26)
        if (p[0] == "pw" && p.Length == 2) return "orn/x~pw~" + p[1];           // 화분에 물 준 기록
        if (p[0] == "gifts" && p.Length == 2) return "orn/x~gift~" + p[1];      // 선물 (11-26)
        if (p[0] == "ck" && p.Length == 2) return "orn/x~ck~" + p[1];          // 주방 게임 판·상태
        if (p[0] == "cks" && p.Length == 2) return "orn/x~cks~" + p[1];        // 주방 게임 기록
        return path;
    }

    public Dictionary<string, object> Get(string path)
    {
        var p = path.Split('/');
        string prefix = p[0] == "snow" ? "x~snow~" : p[0] == "letters" ? "x~letter~" : p[0] == "msg" && p.Length == 2 ? "x~msg~" + p[1] + "~"
                      : p[0] == "members" ? "x~member~" : p[0] == "scores" ? "x~score~" : p[0] == "quiz" ? "x~quiz~" : p[0] == "sbf" ? "x~sbf~" : p[0] == "qzs" ? "x~qzs~" : p[0] == "candy" ? "x~candy~"
                      : p[0] == "plants" ? "x~plant~" : p[0] == "pw" ? "x~pw~" : p[0] == "gifts" ? "x~gift~"
                      : p[0] == "ck" ? "x~ck~" : p[0] == "cks" ? "x~cks~" : null;
        if (prefix != null)
        {
            var orn = Node("orn");
            if (orn == null) return null;
            var res = new Dictionary<string, object>();
            foreach (var kv in orn) if (kv.Key.StartsWith(prefix, StringComparison.Ordinal)) res[kv.Key.Substring(prefix.Length)] = kv.Value;
            return res;
        }
        object cur = tree;
        foreach (var part in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!(cur is Dictionary<string, object> d) || !d.TryGetValue(part, out cur)) return null;
        }
        return cur as Dictionary<string, object>;
    }

    public void Put(string path, string json)
    {
        path = Phys(path);
        SetAt(path, AcJson.Parse(json));   // 내 화면에 바로 반영
        if (!Offline) StartCoroutine(Send("PUT", Base + "/" + EscPath(path) + ".json", json));
    }

    public void Delete(string path)
    {
        path = Phys(path);
        SetAt(path, null);
        if (!Offline) StartCoroutine(Send("DELETE", Base + "/" + EscPath(path) + ".json", null));
    }

    static string EscPath(string p) => string.Join("/", Array.ConvertAll(p.Split('/'), UnityWebRequest.EscapeURL));

    // ===== 다른 참가자 =====
    readonly Dictionary<string, (long seq, float at)> seen = new Dictionary<string, (long, float)>();
    public List<AcNet> Peers()
    {
        var list = new List<AcNet>();
        var ps = Node("players");
        if (ps == null) return list;
        double now = ServerNow;
        foreach (var kv in ps)
        {
            if (kv.Key == myId) { continue; }
            if (!(kv.Value is Dictionary<string, object> d)) continue;
            var n = new AcNet
            {
                id = kv.Key, name = AcJson.S(d, "name"), look = AcJson.S(d, "look"),
                x = AcJson.F(d, "x"), z = AcJson.F(d, "z"), ry = AcJson.F(d, "ry"), speed = AcJson.F(d, "speed"),
                emote = AcJson.S(d, "emote"), emoteT = AcJson.D(d, "emoteT"), chat = AcJson.S(d, "chat"), chatT = AcJson.D(d, "chatT"),
                decorating = AcJson.B(d, "decorating"), skate = AcJson.B(d, "skate"), act = AcJson.S(d, "act"), seq = (long)AcJson.D(d, "seq"), t = AcJson.D(d, "t"),
            };
            // 오래 안 바뀐 기록(꺼진 사람)은 빼기
            if (now > 0 && n.t > 0 && now - n.t > 30000) continue;   // 휴대폰이 잠깐 화면을 끄거나 다른 앱에 가도 바로 나간 걸로 보지 않게 (11-25: 10초 → 30초)
            if (!seen.TryGetValue(n.id, out var s) || s.seq != n.seq) seen[n.id] = (n.seq, Time.unscaledTime);
            else if (Time.unscaledTime - s.at > 25f) continue;
            list.Add(n);
        }
        return list;
    }

    // ===== 트리 장식 =====
    public Dictionary<string, AcRoom.Orn> Ornaments()
    {
        var res = new Dictionary<string, AcRoom.Orn>();
        var os = Node("orn");
        if (os == null) return res;
        foreach (var kv in os)
        {
            if (kv.Key.StartsWith("x~", StringComparison.Ordinal)) continue;   // 눈사람·편지·쪽지
            if (!(kv.Value is Dictionary<string, object> d)) continue;
            res[kv.Key] = new AcRoom.Orn
            {
                id = kv.Key, type = AcJson.S(d, "type"), color = AcJson.S(d, "color"), owner = AcJson.S(d, "owner"),
                x = AcJson.F(d, "x"), y = AcJson.F(d, "y"), z = AcJson.F(d, "z"), ry = AcJson.F(d, "ry"), t = AcJson.D(d, "t"), v = (int)AcJson.D(d, "v"),
            };
        }
        return res;
    }

    public void PutOrn(AcRoom.Orn o)
    {
        SetAt("orn/" + o.id, AcJson.Parse(JsonUtility.ToJson(o)));   // 내 화면에 바로 반영
        if (!Offline) StartCoroutine(Send("PUT", Base + "/orn/" + UnityWebRequest.EscapeURL(o.id) + ".json", JsonUtility.ToJson(o)));
    }

    public void DeleteOrn(string id)
    {
        SetAt("orn/" + id, null);
        if (!Offline) StartCoroutine(Send("DELETE", Base + "/orn/" + UnityWebRequest.EscapeURL(id) + ".json", null));
    }

    void OnApplicationQuit() => Leave();
    void OnDestroy() => Leave();
    bool left;
    void Leave()
    {
        if (!started || left || Offline) return;
        left = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        AcWeb_StopStream();
#else
        try { var r = UnityWebRequest.Delete(MyUrl); r.SendWebRequest(); } catch { }
#endif
    }
}
