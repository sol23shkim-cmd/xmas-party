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
    double ServerNow => serverAt < 0 ? 0 : serverT + (Time.realtimeSinceStartup - serverAt) * 1000.0;

    string Base => DbUrl + "/rooms/" + UnityWebRequest.EscapeURL(room);
    string MyUrl => Base + "/players/" + UnityWebRequest.EscapeURL(myId) + ".json";

    public void Begin(string roomCode, string id, AcNet init)
    {
        room = string.IsNullOrWhiteSpace(roomCode) ? "xmas" : roomCode.Trim();
        myId = id; me = init; me.id = id; started = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        AcWeb_Stream(Base + ".json", gameObject.name);
        AcWeb_LeaveOnClose(MyUrl);
#endif
    }

    public void SetMe(AcNet s) { if (me != null) { s.id = myId; me = s; } }

    void Update()
    {
        if (!started) return;
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
                decorating = AcJson.B(d, "decorating"), seq = (long)AcJson.D(d, "seq"), t = AcJson.D(d, "t"),
            };
            // 오래 안 바뀐 기록(꺼진 사람)은 빼기
            if (now > 0 && n.t > 0 && now - n.t > 10000) continue;
            if (!seen.TryGetValue(n.id, out var s) || s.seq != n.seq) seen[n.id] = (n.seq, Time.unscaledTime);
            else if (Time.unscaledTime - s.at > 8f) continue;
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
            if (!(kv.Value is Dictionary<string, object> d)) continue;
            res[kv.Key] = new AcRoom.Orn
            {
                id = kv.Key, type = AcJson.S(d, "type"), color = AcJson.S(d, "color"), owner = AcJson.S(d, "owner"),
                x = AcJson.F(d, "x"), y = AcJson.F(d, "y"), z = AcJson.F(d, "z"), ry = AcJson.F(d, "ry"), t = AcJson.D(d, "t"),
            };
        }
        return res;
    }

    public void PutOrn(AcRoom.Orn o)
    {
        SetAt("orn/" + o.id, AcJson.Parse(JsonUtility.ToJson(o)));   // 내 화면에 바로 반영
        StartCoroutine(Send("PUT", Base + "/orn/" + UnityWebRequest.EscapeURL(o.id) + ".json", JsonUtility.ToJson(o)));
    }

    public void DeleteOrn(string id)
    {
        SetAt("orn/" + id, null);
        StartCoroutine(Send("DELETE", Base + "/orn/" + UnityWebRequest.EscapeURL(id) + ".json", null));
    }

    void OnApplicationQuit() => Leave();
    void OnDestroy() => Leave();
    bool left;
    void Leave()
    {
        if (!started || left) return;
        left = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        AcWeb_StopStream();
#else
        try { var r = UnityWebRequest.Delete(MyUrl); r.SendWebRequest(); } catch { }
#endif
    }
}
