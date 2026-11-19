// AcSync.cs — 공유 폴더 슬롯 파일로 실시간 위치 동기화 (서버 없음)
// 폴더\room\playerNN.json : 각자 자기 슬롯만 150ms마다 쓰고, 남의 슬롯은 250ms마다 읽음.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

[Serializable]
public class AcNet
{
    public string id = "", name = "", look = "";
    public float x, z, ry, speed;
    public string emote = ""; public double emoteT;     // 인사·하트 등
    public string chat = ""; public double chatT;       // 말풍선
    public bool decorating, left, empty;
    public long seq; public double t;
}

public class AcSync
{
    public const int MaxSlots = 16;
    const int WriteMs = 150, ReadMs = 250;
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static long NowMs => Clock.ElapsedMilliseconds;
    public static double UnixMs => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

    public class Peer { public AcNet data; public long seq = -1; public long seqAt; }

    readonly string dir, myId;
    readonly object lk = new object();
    AcNet me = new AcNet();
    Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
    Thread thread;
    volatile bool running;
    public int Slot { get; private set; } = -1;
    public string Status { get; private set; } = "";
    public float WriteMs_ { get; private set; }

    public AcSync(string sharedDir, string id)
    {
        dir = Path.Combine(sharedDir, "room");
        myId = id;
        Directory.CreateDirectory(dir);
    }

    public void Start(AcNet initial)
    {
        lock (lk) me = Clone(initial);
        me.id = myId;
        running = true;
        Status = "빈 자리 찾는 중...";
        thread = new Thread(Main) { IsBackground = true, Name = "AcSync" };
        thread.Start();
    }

    public void Stop()
    {
        if (!running) return;
        running = false;
        if (thread != null && thread.IsAlive) thread.Join(1500);
    }

    public void SetMe(AcNet s) { lock (lk) { var id = me.id; me = Clone(s); me.id = id; } }

    public Dictionary<int, AcNet> Peers()
    {
        var d = new Dictionary<int, AcNet>();
        lock (lk) foreach (var kv in peers) if (kv.Value.data != null) d[kv.Key] = kv.Value.data;
        return d;
    }

    static AcNet Clone(AcNet s) => JsonUtility.FromJson<AcNet>(JsonUtility.ToJson(s));
    string SlotPath(int i) => Path.Combine(dir, $"player{i:00}.json");

    int Read(int i, out AcNet st)   // 0 정상, 1 없음, 2 오류
    {
        st = null;
        try
        {
            var p = SlotPath(i);
            if (!File.Exists(p)) return 1;
            string txt;
            using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs)) txt = sr.ReadToEnd();
            if (string.IsNullOrWhiteSpace(txt)) { st = new AcNet { empty = true }; return 0; }
            st = JsonUtility.FromJson<AcNet>(txt);
            return st == null ? 2 : 0;
        }
        catch (FileNotFoundException) { return 1; }
        catch { return 2; }
    }

    bool Write(int i, AcNet s)
    {
        var p = SlotPath(i); var tmp = Path.Combine(dir, $"player{i:00}_{myId}.tmp");
        var json = JsonUtility.ToJson(s);
        try
        {
            File.WriteAllText(tmp, json);
            try { if (File.Exists(p)) File.Replace(tmp, p, null); else File.Move(tmp, p); }
            catch { try { File.WriteAllText(p, json); } finally { try { File.Delete(tmp); } catch { } } }
            return true;
        }
        catch { return false; }
    }

    AcNet CopyMe() { lock (lk) return Clone(me); }

    int Claim()
    {
        var a = new int[MaxSlots + 1]; var aS = new AcNet[MaxSlots + 1];
        var b = new int[MaxSlots + 1]; var bS = new AcNet[MaxSlots + 1];
        for (int i = 1; i <= MaxSlots; i++) a[i] = Read(i, out aS[i]);
        // 지난번에 내가 쓰던 자리가 있으면 바로 다시 씀
        for (int i = 1; i <= MaxSlots; i++) if (a[i] == 0 && aS[i].id == myId) return i;
        Thread.Sleep(1200);
        for (int i = 1; i <= MaxSlots; i++) b[i] = Read(i, out bS[i]);
        var free = new List<int>();
        for (int i = 1; i <= MaxSlots; i++)
        {
            if (b[i] == 2) continue;
            if (b[i] == 1 || bS[i].empty || bS[i].left) { free.Add(i); continue; }
            if (a[i] == 0 && aS[i].seq == bS[i].seq) free.Add(i);   // 1.2초 동안 안 변함 = 나간 사람
        }
        var rnd = new System.Random();
        foreach (var i in free)
        {
            if (!running) return -1;
            var st = CopyMe(); st.seq = 0; st.t = UnixMs;
            Write(i, st);
            Thread.Sleep(700 + rnd.Next(600));
            if (Read(i, out var c) == 0 && c.id == myId) return i;
        }
        return -1;
    }

    void Main()
    {
        int slot;
        try { slot = Claim(); } catch (Exception e) { Status = "자리 배정 오류: " + e.Message; return; }
        if (slot < 0) { Status = "빈 자리가 없어요 (최대 " + MaxSlots + "명)"; return; }
        Slot = slot; Status = "";
        long seq = 0, nextW = 0, nextR = 0;
        var missing = new Dictionary<int, long>();
        var staleSeq = new Dictionary<int, long>();   // 멈춘 채 남아 있는 슬롯 (꺼진 사람) — 같은 seq면 무시
        var local = new Dictionary<int, Peer>();
        var sw = new Stopwatch();
        while (running)
        {
            long now = NowMs;
            if (now >= nextW)
            {
                nextW = now + WriteMs;
                var st = CopyMe(); st.seq = ++seq; st.t = UnixMs;
                sw.Restart(); Write(slot, st); WriteMs_ = (float)sw.Elapsed.TotalMilliseconds;
            }
            if (now >= nextR)
            {
                nextR = now + ReadMs;
                for (int i = 1; i <= MaxSlots; i++)
                {
                    if (i == slot) continue;
                    if (!local.ContainsKey(i) && missing.TryGetValue(i, out var mt) && now - mt < 3000) continue;
                    int r = Read(i, out var d);
                    if (r == 1) { local.Remove(i); missing[i] = now; continue; }
                    if (r == 2) continue;
                    if (d.empty || d.left || d.id == myId) { local.Remove(i); missing[i] = now; continue; }
                    if (staleSeq.TryGetValue(i, out var ss)) { if (ss == d.seq) { missing[i] = now; continue; } staleSeq.Remove(i); }
                    if (!local.TryGetValue(i, out var p)) { p = new Peer { seqAt = now }; local[i] = p; }
                    if (d.seq != p.seq) { p.seq = d.seq; p.seqAt = now; p.data = d; }
                    else if (now - p.seqAt > 6000) { local.Remove(i); missing[i] = now; staleSeq[i] = d.seq; }   // 6초 멈춤 = 꺼짐
                }
                lock (lk) { peers = new Dictionary<int, Peer>(local); }
            }
            Thread.Sleep(5);
        }
        Write(slot, new AcNet { id = myId, left = true });
    }
}
