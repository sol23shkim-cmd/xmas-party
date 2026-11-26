// AcKitchen.cs - 부엌의 '산타의 크리스마스 주방' (오버쿡처럼 함께 요리하기, 11-26)
// 부엌에서 '크리스마스 주방 게임' → 따로 떨어진 주방 무대(6000,0,6000)로 들어가 대기실(연습 가능) → 누군가 '시작!'
// 시작한 사람이 방장: 주문·판정을 맡고 상태를 ck/st 에 0.2초마다 올림. 손님은 행동(잡기/놓기·문지르기)을 act "ka"로 보냄.
// 내 캐릭터를 무대로 옮겨서 움직이므로, 다른 사람 위치는 원래 동기화 그대로 보임.
// 저장: orn/x~ck~cur (판 정보), orn/x~ck~st (진행 상태), orn/x~cks~<스테이지>~<시각> (기록: names, score, stars, n)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class AcRoom
{
    enum KMode { Lobby, Count, Play, Result }
    bool kitchenOpen; KMode km;
    int kStage;                                   // 고른 스테이지 (0~7)
    CkState ks = new CkState();                   // 지금 보이는 상태 (방장: 진짜 / 손님: 받은 상태 + 내 행동)
    CkState kBase;                                // 손님: 마지막으로 받은 상태
    bool kHost; string kHostId = "", kSid = ""; double kT0; int kSeq, kLastSnap = -1; float kSnapAt, kSendAt;
    readonly List<(int seq, char a, int tile)> kPending = new List<(int, char, int)>();
    readonly List<(string pid, int seq, char a, int tile)> kInbox = new List<(string, int, char, int)>();
    readonly Dictionary<string, (string name, float at)> kLobby = new Dictionary<string, (string, float)>();
    System.Random kRng = new System.Random();
    Vector3 kSavedPos; float kSavedYaw; float kBeat;
    static readonly Vector3 KO = new Vector3(6000, 0, 6000);
    Kc.Stage KS => Kc.Stages[Mathf.Clamp(ks.stage, 0, Kc.Stages.Length - 1)];
    bool InKitchenRoom => phase == Phase.Play && me != null && !Outside && AreaAt(me.transform.position) == "부엌";
    int KUnlocked => Mathf.Clamp(PlayerPrefs.GetInt("kitchen_lv", 0), 0, Kc.Stages.Length - 1);
    int KBestStars(int s) => PlayerPrefs.GetInt("kitchen_star" + s, 0);

    static Vector3 KPos(int x, int z) => KO + new Vector3((x - (Kc.W - 1) * 0.5f) * Kc.Tile, 0, ((Kc.H - 1) * 0.5f - z) * Kc.Tile);
    static Vector3 KPos(int i) => KPos(i % Kc.W, i / Kc.W);
    static Vector2Int KCell(Vector3 p) => new Vector2Int(Mathf.RoundToInt((p.x - KO.x) / Kc.Tile + (Kc.W - 1) * 0.5f), Mathf.RoundToInt((Kc.H - 1) * 0.5f - (p.z - KO.z) / Kc.Tile));
    char KAt(int x, int z) => x < 0 || z < 0 || x >= Kc.W || z >= Kc.H ? '#' : KS.map[z][x];

    // ===== 들어가기 / 나오기 =====
    void OpenKitchen()
    {
        kitchenOpen = true; km = KMode.Lobby; walkTarget = null; followId = null;
        kSavedPos = me.transform.position; kSavedYaw = yaw;
        kStage = Mathf.Clamp(PlayerPrefs.GetInt("kitchen_sel", KUnlocked), 0, KUnlocked);
        NewPractice(kStage);
        me.transform.position = KSpawn(0);
        RenderSettings.fog = false;
        UpdateCamera(0.016f, true);
        net?.Get("cks");
        Toast("주방 대기실: 미리 연습해 봐요! 모두 모이면 '시작!'");
    }

    void CloseKitchen()
    {
        kitchenOpen = false;
        me.transform.position = kSavedPos; yaw = kSavedYaw;
        if (kRoot) kRoot.gameObject.SetActive(false);
        ClearHeldVisuals();
        RenderSettings.fog = true;
        UpdateCamera(0.016f, true);
    }

    void NewPractice(int stage)
    {
        ks = new CkState { stage = stage, sid = "", nPlayers = 1 };
        kHost = true; kSid = ""; kPending.Clear(); kInbox.Clear(); kBase = null; kLastSnap = -1;
        BuildArena(stage);
    }

    Vector3 KSpawn(int k)
    {
        var floors = new List<int>();
        for (int i = 0; i < Kc.W * Kc.H; i++) if (Kc.At(KS, i) == '.') floors.Add(i);
        if (floors.Count == 0) return KO;
        // 가운데 쪽 바닥부터
        floors.Sort((a, b) => (KPos(a) - KO).sqrMagnitude.CompareTo((KPos(b) - KO).sqrMagnitude));
        return KPos(floors[Mathf.Clamp(k * 3, 0, floors.Count - 1)]);
    }

    // ===== 시작 (방장) =====
    void KitchenStart()
    {
        var ids = new List<string> { myId };
        foreach (var kv in kLobby) if (Time.unscaledTime - kv.Value.at < 3.5f && remotes.ContainsKey(kv.Key)) ids.Add(kv.Key);
        kSid = Guid.NewGuid().ToString("N").Substring(0, 6);
        int seed = UnityEngine.Random.Range(1, 999999);
        kT0 = NetNow + 3500;
        ks = new CkState { sid = kSid, stage = kStage, seed = seed, nPlayers = Mathf.Clamp(ids.Count, 1, 4) };
        kHost = true; kHostId = myId; kRng = new System.Random(seed); kPending.Clear(); kInbox.Clear(); kLastSnap = -1;
        BuildArena(kStage);
        PlayerPrefs.SetInt("kitchen_sel", kStage);
        if (net != null && ids.Count > 1)
        {
            net.Put("ck/cur", "{\"sid\":" + AcJson.Esc(kSid) + ",\"stage\":" + kStage + ",\"seed\":" + seed + ",\"t0\":" + kT0.ToString("0") + ",\"host\":" + AcJson.Esc(myId) + ",\"ps\":" + AcJson.Esc(string.Join(",", ids)) + "}");
            AddAct("ks", kSid + "," + kStage + "," + seed + "," + kT0.ToString("0") + "," + string.Join("/", ids));
        }
        me.transform.position = KSpawn(0);
        km = KMode.Count;
    }

    // 다른 사람이 보낸 주방 사건
    void KitchenAct(string rid, Remote r, string kind, string data)
    {
        switch (kind)
        {
            case "kj": kLobby[rid] = (r.net.name, Time.unscaledTime); break;
            case "ks":
                {
                    var v = data.Split(',');
                    if (v.Length < 5 || !kitchenOpen || km != KMode.Lobby) break;
                    if (!v[4].Split('/').Contains(myId)) break;
                    if (!int.TryParse(v[1], out int stage) || !int.TryParse(v[2], out int seed) || !double.TryParse(v[3], out double t0)) break;
                    kSid = v[0]; kHost = false; kHostId = rid; kT0 = t0; kStage = stage;
                    ks = new CkState { sid = kSid, stage = stage, seed = seed, nPlayers = Mathf.Clamp(v[4].Split('/').Length, 1, 4) };
                    kBase = null; kPending.Clear(); kLastSnap = -1; kSnapAt = Time.unscaledTime;
                    BuildArena(stage);
                    int myIdx = Array.IndexOf(v[4].Split('/'), myId);
                    me.transform.position = KSpawn(Mathf.Max(0, myIdx));
                    km = KMode.Count;
                    Toast(r.net.name + " 님이 주방 게임을 시작해요!");
                    break;
                }
            case "ka":
                {
                    if (!kHost || km != KMode.Play) break;
                    var v = data.Split(':');
                    if (v.Length < 4 || v[0] != kSid || !int.TryParse(v[1], out int seq) || v[2].Length != 1 || !int.TryParse(v[3], out int tile)) break;
                    kInbox.Add((rid, seq, v[2][0], tile));
                    break;
                }
        }
    }

    // ===== 매 프레임 =====
    void KitchenUpdate(float dt)
    {
        UpdateKitchenFx(dt);
        if (!kitchenOpen) return;
        ProcessKitchenIcons();   // 아이콘은 화면 그리기 중이 아니라 여기서 만듦
        if (Key("esc") && km != KMode.Play) { CloseKitchen(); return; }
        if (km == KMode.Lobby && Time.unscaledTime > kBeat) { kBeat = Time.unscaledTime + 1.5f; AddAct("kj", kStage.ToString()); }
        if (km == KMode.Count)
        {
            if (NetNow >= kT0) { km = KMode.Play; Sfx("go", 0.6f); }
            else { int left = Mathf.CeilToInt((float)((kT0 - NetNow) / 1000.0)); if (left != kCountShown) { kCountShown = left; if (left <= 3) Sfx("count", 0.5f); } }
        }
        var evs = new List<string>();
        if (km == KMode.Lobby) Kc.Step(ks, KS, dt, false, kRng, evs);
        else if (km == KMode.Play)
        {
            if (kHost)
            {
                foreach (var a in kInbox)
                {
                    var ack = ks.acks.FirstOrDefault(x => x.id == a.pid);
                    if (ack == null) { ack = new CkAck { id = a.pid }; ks.acks.Add(ack); }
                    if (a.seq <= ack.seq) continue;
                    ack.seq = a.seq;
                    string e = a.a == 'c' ? Kc.Chop(ks, KS, a.tile) : Kc.Interact(ks, KS, a.pid, a.tile);
                    if (e.StartsWith("serve", StringComparison.Ordinal)) evs.Add(e);
                }
                kInbox.Clear();
                Kc.Step(ks, KS, dt, true, kRng, evs);
                if (ks.nPlayers > 1 && net != null && Time.unscaledTime > kSendAt) { kSendAt = Time.unscaledTime + 0.2f; ks.seq++; PutKState(); }
                if (ks.over) KitchenOver();
            }
            else
            {
                ReadSnapshot(evs);
                Kc.Step(ks, KS, dt, false, null, evs);
                if (Time.unscaledTime - kSnapAt > 7f) { Toast("방장 연결이 끊겼어요"); km = KMode.Result; kLostHost = true; }
                if (ks.over && km == KMode.Play) KitchenOver();
            }
        }
        foreach (var e in evs) KitchenEvent(e);
        if (km != KMode.Result) KitchenMove(dt);
        else me.speed = 0;
        SyncArena(dt);
    }
    int kCountShown; bool kLostHost;

    void ReadSnapshot(List<string> evs)
    {
        var node = net != null ? net.Get("ck") : null;
        if (node == null || !node.TryGetValue("st", out var v) || !(v is Dictionary<string, object> d)) return;
        CkState snap = null;
        try { snap = JsonUtility.FromJson<CkState>(AcJson.S(d, "j")); } catch { }   // 상태는 글자 하나(j)로 묶어서 올림
        if (snap == null || snap.sid != kSid || snap.seq <= kLastSnap) return;
        kLastSnap = snap.seq; kSnapAt = Time.unscaledTime;
        // 손님 화면 소리: 앞 상태와 비교
        if (kBase != null)
        {
            if (snap.served > kBase.served && snap.score != kBase.score) evs.Add("serve:-1:" + (snap.score - kBase.score));
            if (snap.failed > kBase.failed) evs.Add("fail:-1");
            if (snap.nextOrder > kBase.nextOrder) evs.Add("order");
        }
        kBase = snap;
        int acked = snap.acks.FirstOrDefault(a => a.id == myId)?.seq ?? 0;
        kPending.RemoveAll(p => p.seq <= acked);
        ks = JsonUtility.FromJson<CkState>(JsonUtility.ToJson(snap));
        foreach (var p in kPending) { if (p.a == 'c') Kc.Chop(ks, KS, p.tile); else Kc.Interact(ks, KS, myId, p.tile); }
    }

    // 내 행동: 바로 내 화면에 + 방장에게 보냄
    void KDo(char a, int tile)
    {
        if (km == KMode.Count || km == KMode.Result) return;
        string e = a == 'c' ? Kc.Chop(ks, KS, tile) : Kc.Interact(ks, KS, myId, tile);
        if (e == "") return;
        KitchenEvent(e, tile);
        if (km == KMode.Play && !kHost)
        {
            kSeq++;
            kPending.Add((kSeq, a, tile));
            AddAct("ka", kSid + ":" + kSeq + ":" + a + ":" + tile);
        }
    }

    void KitchenEvent(string e, int tile = -1)
    {
        var tp = tile >= 0 ? KPos(tile) + Vector3.up * 1.0f : me.transform.position + Vector3.up * 1.2f;
        var p = e.Split(':');
        switch (p[0])
        {
            case "pick": Sfx("jelly", 0.3f, 1.1f); break;
            case "place": Sfx("click", 0.35f, 0.9f); break;
            case "add": Sfx("coin", 0.3f, 1.2f); FxBurst("star", tp, 4, 0.8f, 0.05f, FxGold, 0.6f, 1f); break;
            case "cook": Sfx("throw", 0.35f, 0.8f); break;
            case "trash": Sfx("splat", 0.3f, 1.2f); break;
            case "bad": Sfx("candy_bad", 0.3f, 1.1f); break;
            case "chop": Sfx("smash", 0.35f, UnityEngine.Random.Range(1.1f, 1.4f)); FxBurst("dot", tp, 3, 0.9f, 0.035f, ChopCols(tile), 0.45f, 6f); break;
            case "spread": Sfx("slide", 0.35f, UnityEngine.Random.Range(1.0f, 1.2f)); FxBurst("dot", tp, 3, 0.6f, 0.035f, new[] { Color.white }, 0.45f, 4f); break;
            case "chopdone": Sfx("candy_pop", 0.45f, 1.1f); FxBurst("star", tp, 6, 1.2f, 0.05f, FxGold, 0.7f, 1f); break;
            case "done": Sfx("correct", 0.25f, 1.3f); break;
            case "burn": Sfx("wrong", 0.4f); break;
            case "order": Sfx("tick", 0.35f, 1.4f); break;
            case "fail": Sfx("candy_bad", 0.45f, 0.8f); kFailFlash = 1f; break;
            case "serve":
                {
                    Sfx("candy_collect", 0.55f); Sfx("star", 0.35f, 1.3f);
                    int pts = p.Length > 2 && int.TryParse(p[2], out int v) ? v : 0;
                    kPops.Add(("+" + pts, Time.unscaledTime));
                    var vp = ServePos(); FxBurst("star", vp + Vector3.up * 1.2f, 14, 1.8f, 0.06f, FxGold, 1f, 1f); FxBurst("conf", vp + Vector3.up * 1.2f, 16, 2f, 0.04f, FxParty, 1.2f, 2.5f);
                    break;
                }
        }
    }
    Color[] ChopCols(int tile)
    {
        string it = tile >= 0 ? Kc.ItemAt(ks, tile) : "";
        if (it.StartsWith("straw", StringComparison.Ordinal)) return new[] { new Color(0.9f, 0.15f, 0.2f) };
        if (it.StartsWith("potato", StringComparison.Ordinal)) return new[] { new Color(0.95f, 0.85f, 0.5f) };
        return new[] { new Color(0.85f, 0.65f, 0.4f) };
    }
    Vector3 ServePos() { for (int i = 0; i < Kc.W * Kc.H; i++) if (Kc.At(KS, i) == 'V') return KPos(i); return KO; }
    readonly List<(string text, float at)> kPops = new List<(string, float)>();
    float kFailFlash;

    void KitchenOver()
    {
        km = KMode.Result;
        int stars = Kc.Stars(KS, ks.nPlayers, ks.score);
        kResultStars = stars; kResultAt = Time.unscaledTime;
        if (stars > 0)
        {
            if (ks.stage >= KUnlocked && ks.stage + 1 < Kc.Stages.Length) PlayerPrefs.SetInt("kitchen_lv", ks.stage + 1);
            if (stars > KBestStars(ks.stage)) PlayerPrefs.SetInt("kitchen_star" + ks.stage, stars);
            PlayerPrefs.Save();
            Sfx("record", 0.5f);
        }
        else Sfx("gameover", 0.5f);
        if (kHost && net != null)
        {
            if (ks.nPlayers > 1) { ks.seq++; PutKState(); }
            var names = new List<string> { uiName.Trim() };
            foreach (var a in ks.hands) if (a.id != myId && remotes.TryGetValue(a.id, out var r) && r.net != null) names.Add(r.net.name);
            foreach (var a in ks.acks) if (a.id != myId && remotes.TryGetValue(a.id, out var r) && r.net != null && !names.Contains(r.net.name)) names.Add(r.net.name);
            string key = ks.stage + "~" + NetNow.ToString("0");
            net.Put("cks/" + key, "{\"names\":" + AcJson.Esc(string.Join(", ", names)) + ",\"score\":" + ks.score + ",\"stars\":" + stars + ",\"n\":" + ks.nPlayers + ",\"stage\":" + ks.stage + ",\"t\":" + NetNow.ToString("0") + "}");
        }
    }
    int kResultStars; float kResultAt;
    void PutKState() => net.Put("ck/st", "{\"j\":" + AcJson.Esc(JsonUtility.ToJson(ks)) + "}");

    // ===== 움직이기 =====
    Vector3 kVel; Vector2 kStick; int kTarget = -1; float kChopAccum;
    void KitchenMove(float dt)
    {
        // 손가락이 없을 때(마우스로 휴대폰 화면을 흉내 낼 때): 왼쪽을 누른 채 끌면 이동
        if (IsMobile && Time.unscaledTime - kTouchAt > 1.5f)
        {
            var u0 = new Vector2(ptrStart.x / UiK, (Screen.height - ptrStart.y) / UiK);
            if (ptrPressed && !ptrOnUI && u0.x < SW * 0.62f && u0.y > SH * 0.25f)
            {
                kStickOn = true; kStickBase = ptrStart;
                var d = (ptrPos - ptrStart) / (44f * UiK);
                kStick = Vector2.ClampMagnitude(d, 1f); if (kStick.magnitude < 0.15f) kStick = Vector2.zero;
            }
            else if (!ptrPressed) { kStickOn = false; kStick = Vector2.zero; }
        }
        Vector2 mv = MoveAxis();
        if (kStick.sqrMagnitude > 0.0001f) mv = kStick;
        var want = new Vector3(mv.x, 0, mv.y);
        if (want.sqrMagnitude > 1) want.Normalize();
        var cell = KCell(me.transform.position);
        bool ice = KAt(cell.x, cell.y) == 'i';
        const float speed = 3.4f;
        kVel = ice ? Vector3.Lerp(kVel, want * speed * 1.15f, 1 - Mathf.Exp(-dt * 1.6f)) : Vector3.Lerp(kVel, want * speed, 1 - Mathf.Exp(-dt * 14f));
        var p = me.transform.position + kVel * dt;
        p = KCollide(p, 0.34f);
        // 부딪히면 그 방향 속도는 없앰
        var moved = (p - me.transform.position) / Mathf.Max(dt, 1e-4f);
        if (ice) kVel = new Vector3(Mathf.Abs(moved.x) < Mathf.Abs(kVel.x) * 0.5f ? 0 : kVel.x, 0, Mathf.Abs(moved.z) < Mathf.Abs(kVel.z) * 0.5f ? 0 : kVel.z);
        me.transform.position = p;
        me.speed = new Vector3(moved.x, 0, moved.z).magnitude;
        if (want.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg;
        me.transform.rotation = Quaternion.Slerp(me.transform.rotation, Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-dt * 16f));
        // 바라보는 칸
        kTarget = FindTarget();
        // PC: 스페이스 잡기/놓기, E 문지르기
        if (Key("space") || Key("j")) { if (kTarget >= 0) KDo('g', kTarget); }
        if ((Key("e") || Key("k")) && kTarget >= 0 && Kc.At(KS, kTarget) == 'X') KDo('c', kTarget);
    }

    int FindTarget()
    {
        var pos = me.transform.position; var cell = KCell(pos);
        var f = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        int best = -1; float bs = -1;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = cell.x + dx, z = cell.y + dz;
                if (x < 0 || z < 0 || x >= Kc.W || z >= Kc.H || Kc.Walkable(KAt(x, z))) continue;
                var d = KPos(x, z) - pos; d.y = 0;
                float dist = d.magnitude; if (dist > 1.55f) continue;
                float s = Vector3.Dot(f, d / dist) - dist * 0.25f;
                if (s > bs && Vector3.Dot(f, d / dist) > 0.35f) { bs = s; best = Kc.Idx(x, z); }
            }
        return best;
    }

    Vector3 KCollide(Vector3 p, float r)
    {
        var c = KCell(p);
        for (int it = 0; it < 2; it++)
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = c.x + dx, z = c.y + dz;
                    if (!(x < 0 || z < 0 || x >= Kc.W || z >= Kc.H) && Kc.Walkable(KAt(x, z))) continue;
                    var tc = KPos(x, z); float h = Kc.Tile * 0.5f;
                    float qx = Mathf.Clamp(p.x, tc.x - h, tc.x + h), qz = Mathf.Clamp(p.z, tc.z - h, tc.z + h);
                    float ddx = p.x - qx, ddz = p.z - qz, d2 = ddx * ddx + ddz * ddz;
                    if (d2 >= r * r) continue;
                    if (d2 > 1e-8f) { float d = Mathf.Sqrt(d2); p.x = qx + ddx / d * r; p.z = qz + ddz / d * r; }
                    else { var away = p - tc; away.y = 0; if (away.sqrMagnitude < 1e-6f) away = Vector3.back; away.Normalize(); p = tc + away * (h + r); }
                }
        p.y = 0;
        return p;
    }

    // ===== 카메라 =====
    bool KitchenCamera(float dt, bool snap)
    {
        if (!kitchenOpen) return false;
        bool portrait = Screen.height > Screen.width;
        float pitch = portrait ? 60f : 55f;
        // 주방이 들어갈 화면 띠 (아래는 조작 버튼, 위는 주문표): 화면 아래에서부터의 비율
        float lo = portrait ? (km == KMode.Lobby ? 0.41f : 0.17f) : (km == KMode.Lobby ? 0.3f : 0.12f);
        float hi = portrait ? (km == KMode.Lobby ? 0.97f : 0.8f) : (km == KMode.Lobby ? 0.97f : 0.84f);
        if (Mathf.Abs(kCamAspect - cam.aspect) > 0.001f || kCamMode != km || kCamDist <= 0) { kCamAspect = cam.aspect; kCamMode = km; FitKitchenCamera(pitch, lo, hi); }
        var rot = Quaternion.Euler(pitch, 0, 0);
        var want = KO + new Vector3(0, 0.5f, kCamFz) - rot * Vector3.forward * kCamDist;
        cam.transform.position = snap ? want : Vector3.Lerp(cam.transform.position, want, 1 - Mathf.Exp(-dt * 8f));
        cam.transform.rotation = rot;
        cam.nearClipPlane = 0.3f;
        return true;
    }
    float kCamAspect, kCamDist, kCamFz; KMode kCamMode = (KMode)(-1);

    // 주방 네 귀퉁이가 화면 띠 안에 꼭 들어오는 가장 가까운 거리와 위치를 찾음
    void FitKitchenCamera(float pitch, float lo, float hi)
    {
        var rot = Quaternion.Euler(pitch, 0, 0);
        float hx = Kc.W * Kc.Tile * 0.5f + 0.2f, hz = Kc.H * Kc.Tile * 0.5f + 0.2f;
        var corners = new List<Vector3>();
        foreach (float x in new[] { -hx, hx }) foreach (float z in new[] { -hz, hz }) foreach (float y in new[] { 0f, 1.1f }) corners.Add(KO + new Vector3(x, y, z));
        var oldPos = cam.transform.position; var oldRot = cam.transform.rotation;
        cam.transform.rotation = rot;
        void Measure(float dist, float fz, out float minX, out float maxX, out float minY, out float maxY)
        {
            cam.transform.position = KO + new Vector3(0, 0.5f, fz) - rot * Vector3.forward * dist;
            minX = minY = 9; maxX = maxY = -9;
            foreach (var c in corners) { var v = cam.WorldToViewportPoint(c); minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }
        }
        float fz = 0, d0 = 4f, d1 = 80f;
        for (int it = 0; it < 3; it++)
        {
            d0 = 4f; d1 = 80f;
            for (int k = 0; k < 18; k++)
            {
                float m = (d0 + d1) / 2;
                Measure(m, fz, out float a, out float b, out float c, out float d);
                bool fit = a >= 0.02f && b <= 0.98f && (d - c) <= (hi - lo);
                if (fit) d1 = m; else d0 = m;
            }
            // 위아래 가운데 맞추기: 앞뒤로 조금 옮겨 보고 기울기로 계산
            Measure(d1, fz, out _, out _, out float y0a, out float y0b);
            Measure(d1, fz + 1f, out _, out _, out float y1a, out float y1b);
            float c0 = (y0a + y0b) / 2, c1 = (y1a + y1b) / 2, want = (lo + hi) / 2;
            if (Mathf.Abs(c1 - c0) > 1e-4f) fz += (want - c0) / (c1 - c0);
        }
        kCamDist = d1 * 1.02f; kCamFz = fz;
        cam.transform.position = oldPos; cam.transform.rotation = oldRot;
    }

    // ===== 무대 만들기 =====
    Transform kRoot, kStatic, kDyn; int kBuiltStage = -1;
    readonly Dictionary<int, (GameObject go, string key)> kTileObjs = new Dictionary<int, (GameObject, string)>();
    readonly Dictionary<string, (GameObject go, string key)> kHandObjs = new Dictionary<string, (GameObject, string)>();
    readonly Dictionary<int, Renderer> kOvenGlass = new Dictionary<int, Renderer>();
    GameObject kRing;
    const float CounterH = 0.92f;

    void BuildArena(int stage)
    {
        if (!kRoot) { kRoot = new GameObject("Kitchen").transform; kRoot.position = Vector3.zero; }
        kRoot.gameObject.SetActive(true);
        if (kBuiltStage == stage && kStatic) { ClearDyn(); return; }
        if (kStatic) Destroy(kStatic.gameObject);
        ClearDyn();
        kBuiltStage = stage;
        kStatic = new GameObject("static").transform; kStatic.SetParent(kRoot, false);
        kOvenGlass.Clear();
        var s = Kc.Stages[stage];
        // 바깥 바닥·벽
        var env = new AcPB();
        env.Box(KO + new Vector3(0, -0.06f, 0), new Vector3(40, 0.1f, 40), new Color(0.93f, 0.9f, 0.86f));
        float bw = Kc.W * Kc.Tile, bd = Kc.H * Kc.Tile;
        env.Box(KO + new Vector3(0, 1.6f, bd / 2 + 0.9f), new Vector3(bw + 4f, 3.2f, 0.3f), new Color(0.95f, 0.88f, 0.8f));       // 뒷벽
        env.Box(KO + new Vector3(0, 0.45f, bd / 2 + 0.74f), new Vector3(bw + 4f, 0.9f, 0.04f), new Color(0.55f, 0.75f, 0.6f));      // 벽 아래 판
        env.Box(KO + new Vector3(-bw / 2 - 1.2f, 1.0f, 0.5f), new Vector3(0.3f, 2.0f, bd + 2.6f), new Color(0.95f, 0.88f, 0.8f));   // 옆벽
        env.Box(KO + new Vector3(bw / 2 + 1.2f, 1.0f, 0.5f), new Vector3(0.3f, 2.0f, bd + 2.6f), new Color(0.95f, 0.88f, 0.8f));
        for (int k = -1; k <= 1; k += 2)
        {   // 창문 (밖은 눈 내리는 밤)
            env.Box(KO + new Vector3(k * 2.4f, 2.0f, bd / 2 + 0.74f), new Vector3(1.6f, 1.2f, 0.05f), new Color(0.25f, 0.32f, 0.55f));
            env.Box(KO + new Vector3(k * 2.4f, 2.0f, bd / 2 + 0.72f), new Vector3(1.7f, 0.08f, 0.06f), Color.white);
            env.Box(KO + new Vector3(k * 2.4f, 2.0f, bd / 2 + 0.72f), new Vector3(0.08f, 1.3f, 0.06f), Color.white);
        }
        ProcObj("env", env.M("kenv"), kStatic, 0f, false);
        // 가랜드 (벽 위)
        var gar = new AcPB(); var pts = new List<Vector3>(); var rs = new List<float>();
        for (int k = 0; k <= 24; k++) { float u = k / 24f; pts.Add(KO + new Vector3(-bw / 2 - 1f + u * (bw + 2f), 3.0f - Mathf.Sin(u * Mathf.PI * 4) * 0.18f - 0.1f, bd / 2 + 0.7f)); rs.Add(0.09f); }
        gar.Tube(pts, rs, new Color(0.15f, 0.5f, 0.28f), 6);
        for (int k = 1; k < 24; k += 2) gar.Sphere(pts[k] + Vector3.down * 0.1f, 0.06f, k % 4 == 1 ? new Color(0.9f, 0.2f, 0.25f) : new Color(1f, 0.82f, 0.3f), 4, 6);
        ProcObj("garland", gar.M("kgar"), kStatic, 0.15f, false);
        // 바닥 칸
        var fl = new AcPB();
        for (int z = 0; z < Kc.H; z++)
            for (int x = 0; x < Kc.W; x++)
            {
                char c = s.map[z][x]; var p = KPos(x, z);
                Color col = c == 'i' ? new Color(0.78f, 0.92f, 1f) : ((x + z) % 2 == 0 ? new Color(0.98f, 0.95f, 0.9f) : new Color(0.93f, 0.82f, 0.78f));
                fl.Box(p + new Vector3(0, -0.01f, 0), new Vector3(Kc.Tile, 0.02f, Kc.Tile), col);
                if (c == 'i') { fl.Box(p + new Vector3(-0.15f, 0.002f, 0.1f), new Vector3(0.5f, 0.01f, 0.05f), Color.white, Quaternion.Euler(0, 30, 0)); fl.Box(p + new Vector3(0.2f, 0.002f, -0.2f), new Vector3(0.3f, 0.01f, 0.04f), Color.white, Quaternion.Euler(0, 30, 0)); }
            }
        ProcObj("floor", fl.M("kfloor"), kStatic, 0f, false);
        // 기능 칸
        for (int z = 0; z < Kc.H; z++)
            for (int x = 0; x < Kc.W; x++)
            {
                char c = s.map[z][x]; if (Kc.Walkable(c)) continue;
                var go = BuildStation(c, x, z);
                if (go) go.transform.SetParent(kStatic, true);
            }
        // 바라보는 칸 표시 (노란 테두리)
        if (!kRing)
        {
            var rb = new AcPB();
            float h = Kc.Tile * 0.5f - 0.04f;
            foreach (var (a, b2) in new[] { (new Vector3(-h, 0, -h), new Vector3(h, 0, -h)), (new Vector3(h, 0, -h), new Vector3(h, 0, h)), (new Vector3(h, 0, h), new Vector3(-h, 0, h)), (new Vector3(-h, 0, h), new Vector3(-h, 0, -h)) })
                rb.Cylinder(a, b2, 0.035f, 0.035f, new Color(1f, 0.85f, 0.25f), 6);
            kRing = ProcObj("kRing", rb.M("kring"), kRoot, 0.9f, false);
        }
        kDyn = new GameObject("dyn").transform; kDyn.SetParent(kRoot, false);
    }

    void ClearDyn()
    {
        foreach (var kv in kTileObjs) if (kv.Value.go) Destroy(kv.Value.go);
        kTileObjs.Clear();
        ClearHeldVisuals();
        if (kDyn) Destroy(kDyn.gameObject);
        kDyn = new GameObject("dyn").transform; if (kRoot) kDyn.SetParent(kRoot, false);
    }
    void ClearHeldVisuals() { foreach (var kv in kHandObjs) if (kv.Value.go) Destroy(kv.Value.go); kHandObjs.Clear(); }

    static readonly Color CtrTop = new Color(0.98f, 0.96f, 0.93f), CtrSide = new Color(0.82f, 0.25f, 0.27f), CtrSide2 = new Color(0.2f, 0.52f, 0.33f);
    GameObject BuildStation(char c, int x, int z)
    {
        var p = KPos(x, z); var b = new AcPB();
        float T = Kc.Tile;
        Color side = (x + z) % 2 == 0 ? CtrSide : CtrSide2;
        void Counter(Color top, Color sd) { b.Box(p + new Vector3(0, CounterH / 2 - 0.03f, 0), new Vector3(T - 0.04f, CounterH - 0.06f, T - 0.04f), sd); b.Box(p + new Vector3(0, CounterH - 0.03f, 0), new Vector3(T, 0.06f, T), top); }
        GameObject extra = null;
        switch (c)
        {
            case '#': Counter(CtrTop, side); break;
            case '>':
                b.Box(p + new Vector3(0, CounterH / 2 - 0.05f, 0), new Vector3(T, CounterH - 0.1f, T - 0.1f), new Color(0.45f, 0.47f, 0.52f));
                b.Box(p + new Vector3(0, CounterH - 0.06f, 0), new Vector3(T, 0.04f, T - 0.2f), new Color(0.2f, 0.2f, 0.24f));
                for (int k = 0; k < 2; k++) { var q = p + new Vector3(-0.25f + k * 0.5f, CounterH - 0.035f, 0); b.Box(q + new Vector3(-0.05f, 0, 0.1f), new Vector3(0.22f, 0.01f, 0.05f), new Color(1f, 0.8f, 0.2f), Quaternion.Euler(0, -35, 0)); b.Box(q + new Vector3(-0.05f, 0, -0.1f), new Vector3(0.22f, 0.01f, 0.05f), new Color(1f, 0.8f, 0.2f), Quaternion.Euler(0, 35, 0)); }
                break;
            case 'X':
                Counter(CtrTop, side);
                b.Box(p + new Vector3(0, CounterH + 0.02f, 0), new Vector3(T * 0.8f, 0.04f, T * 0.6f), new Color(0.85f, 0.65f, 0.42f));
                b.Box(p + new Vector3(T * 0.33f, CounterH + 0.05f, -T * 0.22f), new Vector3(0.28f, 0.02f, 0.06f), new Color(0.85f, 0.87f, 0.9f));
                b.Box(p + new Vector3(T * 0.33f + 0.19f, CounterH + 0.05f, -T * 0.22f), new Vector3(0.12f, 0.03f, 0.05f), new Color(0.3f, 0.2f, 0.15f));
                break;
            case 'H':
                Counter(new Color(0.3f, 0.3f, 0.34f), new Color(0.88f, 0.88f, 0.9f));
                b.Torus(p + new Vector3(0, CounterH + 0.01f, 0), Vector3.up, 0.24f, 0.02f, new Color(0.15f, 0.15f, 0.17f), 20, 5);
                b.Cylinder(p + new Vector3(0, CounterH + 0.02f, 0), p + new Vector3(0, CounterH + 0.24f, 0), 0.26f, 0.27f, new Color(0.7f, 0.72f, 0.76f), 18, false);
                b.Torus(p + new Vector3(0, CounterH + 0.24f, 0), Vector3.up, 0.27f, 0.02f, new Color(0.8f, 0.82f, 0.86f), 20, 5);
                b.Disk(p + new Vector3(0, CounterH + 0.03f, 0), Vector3.up, 0.26f, new Color(0.5f, 0.52f, 0.55f), 18, false);
                b.Cylinder(p + new Vector3(0.27f, CounterH + 0.2f, 0), p + new Vector3(0.55f, CounterH + 0.22f, 0), 0.025f, 0.025f, new Color(0.25f, 0.2f, 0.18f), 6);
                break;
            case 'O':
                {
                    b.Box(p + new Vector3(0, CounterH / 2, 0), new Vector3(T - 0.04f, CounterH, T - 0.04f), new Color(0.85f, 0.3f, 0.3f));
                    b.Box(p + new Vector3(0, CounterH + 0.01f, 0), new Vector3(T, 0.05f, T), new Color(0.3f, 0.3f, 0.34f));
                    b.Cylinder(p + new Vector3(0.2f, CounterH, 0.25f), p + new Vector3(0.2f, CounterH + 0.55f, 0.25f), 0.08f, 0.08f, new Color(0.5f, 0.5f, 0.55f), 8);   // 굴뚝
                    // 앞 유리 (구울 때 빛남) — 카메라 쪽(-z)
                    var gb = new AcPB(); gb.Box(p + new Vector3(0, CounterH * 0.5f, -T / 2 + 0.01f), new Vector3(T * 0.6f, CounterH * 0.4f, 0.03f), new Color(0.25f, 0.2f, 0.2f));
                    var glass = ProcObj("ovenGlass", gb.M("og"), kStatic, 0f, false);
                    kOvenGlass[Kc.Idx(x, z)] = glass.GetComponent<Renderer>();
                    b.Box(p + new Vector3(0, CounterH * 0.78f, -T / 2 + 0.01f), new Vector3(T * 0.5f, 0.04f, 0.05f), new Color(0.9f, 0.85f, 0.7f));
                    break;
                }
            case 'R':
                b.Cylinder(p, p + new Vector3(0, 0.75f, 0), 0.34f, 0.4f, new Color(0.45f, 0.5f, 0.55f), 16, true);
                b.Torus(p + new Vector3(0, 0.75f, 0), Vector3.up, 0.4f, 0.03f, new Color(0.35f, 0.4f, 0.45f), 18, 5);
                b.Disk(p + new Vector3(0, 0.74f, 0), Vector3.up, 0.38f, new Color(0.15f, 0.15f, 0.18f), 16, false);
                break;
            case 'V':
                {
                    Counter(new Color(0.98f, 0.95f, 0.85f), new Color(0.95f, 0.75f, 0.25f));
                    // 줄무늬 차양 + 종
                    for (int k = 0; k < 5; k++) b.Box(p + new Vector3(-T * 0.4f + k * T * 0.2f, 1.65f, 0.15f), new Vector3(T * 0.2f, 0.04f, 0.6f), k % 2 == 0 ? new Color(0.9f, 0.2f, 0.25f) : Color.white, Quaternion.Euler(-20, 0, 0));
                    b.Cylinder(p + new Vector3(-T * 0.45f, CounterH, 0.4f), p + new Vector3(-T * 0.45f, 1.7f, 0.4f), 0.03f, 0.03f, new Color(0.6f, 0.4f, 0.25f), 6);
                    b.Cylinder(p + new Vector3(T * 0.45f, CounterH, 0.4f), p + new Vector3(T * 0.45f, 1.7f, 0.4f), 0.03f, 0.03f, new Color(0.6f, 0.4f, 0.25f), 6);
                    b.Ellipsoid(p + new Vector3(0.3f, CounterH + 0.08f, 0.3f), new Vector3(0.09f, 0.08f, 0.09f), new Color(1f, 0.8f, 0.25f), 6, 10);
                    b.Star(p + new Vector3(0, 1.95f, 0.25f), Vector3.back, Vector3.up, 0.18f, 0.08f, 0.04f, new Color(1f, 0.82f, 0.25f));
                    break;
                }
            default:
                if (Kc.IsDispenser(c))
                {   // 상자 + 위에 그 재료
                    Counter(CtrTop, side);
                    b.Box(p + new Vector3(0, CounterH + 0.12f, 0), new Vector3(T * 0.78f, 0.22f, T * 0.78f), new Color(0.75f, 0.55f, 0.35f));
                    b.Box(p + new Vector3(0, CounterH + 0.2f, -T * 0.39f), new Vector3(T * 0.78f, 0.04f, 0.02f), new Color(0.6f, 0.42f, 0.26f));
                    extra = MakeKItem(Kc.Dispense(c));
                    extra.transform.position = p + new Vector3(0, CounterH + 0.23f, 0);
                    extra.transform.localScale *= 0.95f;
                }
                break;
        }
        var go = ProcObj("st_" + c, b.M("st"), null);
        if (extra) extra.transform.SetParent(go.transform, true);
        return go;
    }

    // ===== 음식·그릇 모양 =====
    readonly Dictionary<string, Mesh> kItemMesh = new Dictionary<string, Mesh>();
    GameObject MakeKItem(string it)
    {
        if (string.IsNullOrEmpty(it)) return null;
        if (!kItemMesh.TryGetValue(it, out var m)) { m = BuildKItemMesh(it); kItemMesh[it] = m; }
        var go = ProcObj("item", m, null, 0.08f);
        return go;
    }

    static void IngredientInto(AcPB b, string it, Vector3 o, float k)
    {
        // o: 바닥 가운데, k: 크기
        Color red = new Color(0.9f, 0.15f, 0.2f);
        switch (it)
        {
            case "milk.r":
                b.Cylinder(o, o + Vector3.up * 0.3f * k, 0.09f * k, 0.09f * k, Color.white, 12);
                b.Cylinder(o + Vector3.up * 0.3f * k, o + Vector3.up * 0.4f * k, 0.09f * k, 0.04f * k, Color.white, 12);
                b.Cylinder(o + Vector3.up * 0.4f * k, o + Vector3.up * 0.45f * k, 0.045f * k, 0.045f * k, new Color(0.25f, 0.5f, 0.95f), 10);
                b.Cylinder(o + Vector3.up * 0.1f * k, o + Vector3.up * 0.2f * k, 0.093f * k, 0.093f * k, new Color(0.3f, 0.55f, 0.95f), 12, false);
                break;
            case "milk.h":
                b.Cylinder(o, o + Vector3.up * 0.14f * k, 0.15f * k, 0.16f * k, new Color(0.75f, 0.77f, 0.8f), 14, false);
                b.Disk(o + Vector3.up * 0.005f, Vector3.up, 0.15f * k, new Color(0.7f, 0.72f, 0.75f), 14, false);
                b.Disk(o + Vector3.up * 0.11f * k, Vector3.up, 0.15f * k, new Color(1f, 0.97f, 0.9f), 14, false);
                b.Cylinder(o + new Vector3(0.15f * k, 0.1f * k, 0), o + new Vector3(0.35f * k, 0.12f * k, 0), 0.02f * k, 0.02f * k, new Color(0.25f, 0.2f, 0.18f), 6);
                break;
            case "marsh.r":
                for (int i = 0; i < 4; i++) { var q = o + new Vector3((i % 2 - 0.5f) * 0.12f * k, (i / 2) * 0.09f * k, (i % 2 == 0 ? 0.03f : -0.03f) * k); b.Cylinder(q, q + Vector3.up * 0.08f * k, 0.055f * k, 0.055f * k, i == 3 ? new Color(1f, 0.75f, 0.85f) : Color.white, 10); }
                break;
            case "dough.r":
                b.Ellipsoid(o + Vector3.up * 0.05f * k, new Vector3(0.18f, 0.07f, 0.16f) * k, new Color(0.92f, 0.78f, 0.55f), 6, 10);
                break;
            case "dough.c": case "cookie.b": case "cookie.i":
                {
                    Color cc = it == "dough.c" ? new Color(0.93f, 0.8f, 0.58f) : new Color(0.72f, 0.45f, 0.22f);
                    var c0 = o + Vector3.up * 0.03f * k;
                    b.Ellipsoid(c0 + new Vector3(0, 0, 0.13f * k), new Vector3(0.08f, 0.03f, 0.08f) * k, cc, 5, 10);          // 머리
                    b.Ellipsoid(c0 + new Vector3(0, 0, -0.03f * k), new Vector3(0.09f, 0.03f, 0.11f) * k, cc, 5, 10);         // 몸
                    b.Ellipsoid(c0 + new Vector3(-0.11f * k, 0, 0.02f * k), new Vector3(0.07f, 0.025f, 0.035f) * k, cc, 4, 8, Quaternion.Euler(0, 20, 0));
                    b.Ellipsoid(c0 + new Vector3(0.11f * k, 0, 0.02f * k), new Vector3(0.07f, 0.025f, 0.035f) * k, cc, 4, 8, Quaternion.Euler(0, -20, 0));
                    b.Ellipsoid(c0 + new Vector3(-0.06f * k, 0, -0.14f * k), new Vector3(0.035f, 0.025f, 0.07f) * k, cc, 4, 8, Quaternion.Euler(0, -15, 0));
                    b.Ellipsoid(c0 + new Vector3(0.06f * k, 0, -0.14f * k), new Vector3(0.035f, 0.025f, 0.07f) * k, cc, 4, 8, Quaternion.Euler(0, 15, 0));
                    if (it == "cookie.i")
                    {
                        b.Sphere(c0 + new Vector3(-0.03f * k, 0.03f * k, 0.15f * k), 0.012f * k, Color.white, 3, 5);
                        b.Sphere(c0 + new Vector3(0.03f * k, 0.03f * k, 0.15f * k), 0.012f * k, Color.white, 3, 5);
                        for (int i = 0; i < 3; i++) b.Sphere(c0 + new Vector3(0, 0.03f * k, (0.03f - i * 0.05f) * k), 0.016f * k, i == 1 ? new Color(0.3f, 0.7f, 0.35f) : red, 3, 5);
                        foreach (float sx in new[] { -1f, 1f }) b.Box(c0 + new Vector3(sx * 0.12f * k, 0.03f * k, 0.02f * k), new Vector3(0.05f, 0.008f, 0.012f) * k, Color.white);
                    }
                    break;
                }
            case "batter.r":
                b.Cylinder(o, o + Vector3.up * 0.13f * k, 0.1f * k, 0.17f * k, new Color(0.95f, 0.95f, 0.98f), 14, false);
                b.Disk(o + Vector3.up * 0.005f, Vector3.up, 0.1f * k, new Color(0.9f, 0.9f, 0.93f), 14, false);
                b.Disk(o + Vector3.up * 0.11f * k, Vector3.up, 0.155f * k, new Color(1f, 0.88f, 0.45f), 14, false);
                b.Cylinder(o + new Vector3(0.05f * k, 0.1f * k, 0), o + new Vector3(0.12f * k, 0.3f * k, 0.03f * k), 0.012f * k, 0.012f * k, new Color(0.85f, 0.75f, 0.6f), 5);
                break;
            case "sponge.b": case "sponge.c":
                b.Cylinder(o, o + Vector3.up * 0.12f * k, 0.15f * k, 0.15f * k, new Color(0.95f, 0.78f, 0.4f), 16);
                if (it == "sponge.c")
                {
                    b.Cylinder(o + Vector3.up * 0.12f * k, o + Vector3.up * 0.17f * k, 0.155f * k, 0.15f * k, new Color(1f, 0.98f, 0.95f), 16);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; b.Sphere(o + new Vector3(Mathf.Cos(a) * 0.12f * k, 0.18f * k, Mathf.Sin(a) * 0.12f * k), 0.025f * k, Color.white, 4, 6); }
                }
                break;
            case "straw.r":
                for (int i = 0; i < 2; i++)
                {
                    var q = o + new Vector3((i - 0.5f) * 0.12f * k, 0.07f * k, i * 0.03f * k);
                    b.Ellipsoid(q, new Vector3(0.065f, 0.08f, 0.065f) * k, red, 6, 8);
                    b.Cylinder(q - Vector3.up * 0.06f * k, q - Vector3.up * 0.1f * k, 0.03f * k, 0.002f, red, 6);
                    for (int l = 0; l < 4; l++) { float a = l * Mathf.PI / 2; b.Leaf(q + Vector3.up * 0.07f * k, new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)), Vector3.up, 0.05f * k, 0.03f * k, new Color(0.3f, 0.65f, 0.3f), new Color(0.4f, 0.75f, 0.35f), 0.1f); }
                }
                break;
            case "straw.s":
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2 + 0.4f; var q = o + new Vector3(Mathf.Cos(a) * 0.07f * k, 0.03f * k, Mathf.Sin(a) * 0.07f * k);
                    b.Ellipsoid(q, new Vector3(0.05f, 0.03f, 0.06f) * k, red, 5, 8, Quaternion.Euler(0, a * 57f, 0));
                    b.Ellipsoid(q + Vector3.up * 0.02f * k, new Vector3(0.03f, 0.012f, 0.04f) * k, new Color(1f, 0.85f, 0.85f), 4, 6, Quaternion.Euler(0, a * 57f, 0));
                }
                break;
            case "turkey.r": case "turkey.b":
                {
                    Color tc = it == "turkey.r" ? new Color(0.98f, 0.8f, 0.72f) : new Color(0.78f, 0.45f, 0.18f);
                    b.Ellipsoid(o + Vector3.up * 0.1f * k, new Vector3(0.18f, 0.11f, 0.14f) * k, tc, 8, 12);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var q = o + new Vector3(sx * 0.12f * k, 0.12f * k, -0.1f * k);
                        b.Ellipsoid(q, new Vector3(0.05f, 0.05f, 0.08f) * k, tc, 5, 8, Quaternion.Euler(-30, sx * 20, 0));
                        b.Cylinder(q + new Vector3(0, 0.03f * k, -0.07f * k), q + new Vector3(0, 0.05f * k, -0.13f * k), 0.012f * k, 0.012f * k, Color.white, 5);
                        b.Sphere(q + new Vector3(0, 0.05f * k, -0.14f * k), 0.02f * k, Color.white, 3, 5);
                    }
                    break;
                }
            case "potato.r":
                b.Ellipsoid(o + new Vector3(-0.06f * k, 0.06f * k, 0), new Vector3(0.09f, 0.06f, 0.07f) * k, new Color(0.7f, 0.52f, 0.32f), 6, 8);
                b.Ellipsoid(o + new Vector3(0.07f * k, 0.05f * k, 0.04f * k), new Vector3(0.07f, 0.05f, 0.06f) * k, new Color(0.65f, 0.48f, 0.3f), 6, 8);
                break;
            case "potato.s":
                for (int i = 0; i < 5; i++) { float a = i * 1.25f; var q = o + new Vector3(Mathf.Cos(a) * 0.07f * k, 0.03f * k, Mathf.Sin(a) * 0.07f * k); b.Box(q, new Vector3(0.11f, 0.04f, 0.04f) * k, new Color(1f, 0.85f, 0.4f), Quaternion.Euler(0, a * 57f, 0)); }
                break;
            case "coal":
                for (int i = 0; i < 3; i++) b.Ellipsoid(o + new Vector3((i - 1) * 0.07f * k, 0.05f * k, (i % 2) * 0.04f * k), new Vector3(0.07f, 0.05f, 0.06f) * k, new Color(0.12f, 0.1f, 0.1f), 5, 7);
                break;
        }
    }

    static Mesh BuildKItemMesh(string it)
    {
        var b = new AcPB();
        const float K = 1.75f;   // 화면에서 잘 보이게 실제보다 크게
        if (Kc.IsContainer(it))
        {
            string ct = Kc.ContType(it); var parts = Kc.Parts(it);
            if (ct == "mug")
            {
                b.Cylinder(Vector3.zero, Vector3.up * 0.22f * K, 0.1f * K, 0.11f * K, new Color(0.88f, 0.2f, 0.25f), 16, false);
                b.Disk(Vector3.up * 0.003f, Vector3.up, 0.1f * K, new Color(0.8f, 0.18f, 0.22f), 14, false);
                b.Torus(Vector3.up * 0.22f * K, Vector3.up, 0.11f * K, 0.012f * K, Color.white, 16, 5);
                b.Torus(new Vector3(0.12f * K, 0.11f * K, 0), Vector3.forward, 0.05f * K, 0.015f * K, new Color(0.88f, 0.2f, 0.25f), 12, 5);
                bool milk = parts.Contains("milk.h"), marsh = parts.Contains("marsh.r");
                if (milk) b.Disk(Vector3.up * 0.19f * K, Vector3.up, 0.1f * K, marsh ? new Color(0.5f, 0.3f, 0.18f) : new Color(0.98f, 0.95f, 0.9f), 16, false);
                if (marsh) for (int i = 0; i < 4; i++) { float a = i * 1.57f + 0.3f; var q = new Vector3(Mathf.Cos(a) * 0.05f * K, (milk ? 0.2f : 0.03f) * K, Mathf.Sin(a) * 0.05f * K); b.Box(q, new Vector3(0.04f, 0.035f, 0.04f) * K, i == 2 ? new Color(1f, 0.78f, 0.86f) : Color.white, Quaternion.Euler(10, a * 57, 15)); }
            }
            else
            {
                b.Cylinder(Vector3.zero, Vector3.up * 0.025f * K, 0.15f * K, 0.2f * K, Color.white, 20);
                b.Torus(Vector3.up * 0.025f * K, Vector3.up, 0.2f * K, 0.012f * K, new Color(0.3f, 0.6f, 0.4f), 20, 5);
                // 재료를 접시 위에 나눠 올림
                for (int i = 0; i < parts.Count; i++)
                {
                    var off = parts.Count == 1 ? Vector3.zero : new Vector3((i == 0 ? -0.06f : 0.07f) * K, 0, (i == 0 ? 0.02f : -0.04f) * K);
                    IngredientInto(b, parts[i], Vector3.up * 0.026f * K + off, K * (parts.Count == 1 ? 1f : 0.85f));
                }
            }
        }
        else IngredientInto(b, it, Vector3.zero, K);
        return b.M("k_" + it);
    }

    // ===== 상태를 무대에 그리기 =====
    void SyncArena(float dt)
    {
        if (!kDyn) return;
        var s = KS;
        var seen = new HashSet<int>();
        foreach (var t in ks.tiles)
        {
            seen.Add(t.i);
            char c = Kc.At(s, t.i);
            string key = t.it;
            if (!kTileObjs.TryGetValue(t.i, out var cur) || cur.key != key)
            {
                if (cur.go) Destroy(cur.go);
                var go = c == 'O' ? null : MakeKItem(t.it);   // 오븐 안은 안 보임 (다 되면 위에 그림 표시)
                if (go)
                {
                    go.transform.SetParent(kDyn, false);
                    float y = c == 'H' ? CounterH + 0.06f : c == 'X' ? CounterH + 0.04f : CounterH;
                    go.transform.position = KPos(t.i) + new Vector3(0, y, 0);
                    if (c == 'H' && t.it == "milk.h") go.transform.localScale = Vector3.one * 1.15f;
                }
                kTileObjs[t.i] = (go, key);
            }
        }
        foreach (var k in kTileObjs.Keys.Where(kk => !seen.Contains(kk)).ToList()) { if (kTileObjs[k].go) Destroy(kTileObjs[k].go); kTileObjs.Remove(k); }
        // 오븐 유리: 굽는 중이면 주황빛
        foreach (var kv in kOvenGlass)
        {
            var t = Kc.TileAt(ks, kv.Key, false);
            float e = t != null && t.it != "" ? (t.it == "coal" ? 0.2f : 0.6f + 0.25f * Mathf.Sin(Time.time * 6f)) : 0f;
            giftMpb.Clear(); giftMpb.SetFloat("_Emit", e); kv.Value.SetPropertyBlock(giftMpb);
            if (t != null && t.it != "") kv.Value.GetComponent<MeshFilter>().sharedMesh = OvenGlassMesh(t.it == "coal");
            else kv.Value.GetComponent<MeshFilter>().sharedMesh = OvenGlassMesh(false, true);
        }
        // 손에 든 것
        var handSeen = new HashSet<string>();
        foreach (var h in ks.hands)
        {
            if (string.IsNullOrEmpty(h.it)) continue;
            Transform holder = h.id == myId ? me.transform : (remotes.TryGetValue(h.id, out var r) && r.c ? r.c.transform : null);
            if (holder == null) continue;
            handSeen.Add(h.id);
            if (!kHandObjs.TryGetValue(h.id, out var cur) || cur.key != h.it || !cur.go)
            {
                if (cur.go) Destroy(cur.go);
                var go = MakeKItem(h.it);
                go.transform.SetParent(holder, false);
                go.transform.localPosition = new Vector3(0, 0.62f, 0.42f);
                kHandObjs[h.id] = (go, h.it);
            }
        }
        foreach (var k in kHandObjs.Keys.Where(kk => !handSeen.Contains(kk)).ToList()) { if (kHandObjs[k].go) Destroy(kHandObjs[k].go); kHandObjs.Remove(k); }
        // 바라보는 칸
        if (kRing)
        {
            kRing.SetActive(kTarget >= 0 && km != KMode.Result);
            if (kTarget >= 0) kRing.transform.position = KPos(kTarget) + new Vector3(0, Kc.At(s, kTarget) == 'R' ? 0.78f : CounterH + 0.005f, 0);
        }
        // 탄 것: 연기
        if (Time.time > kSmokeAt)
        {
            kSmokeAt = Time.time + 0.25f;
            foreach (var t in ks.tiles)
            {
                char c = Kc.At(s, t.i);
                bool warn = Kc.CanBurn(c, t.it) && t.p > Kc.BurnAfter(c) * 0.45f;
                if (t.it == "coal" || warn) FxBurst("dot", KPos(t.i) + new Vector3(0, CounterH + 0.3f, 0), 1, 0.25f, t.it == "coal" ? 0.09f : 0.06f, new[] { t.it == "coal" ? new Color(0.25f, 0.25f, 0.27f) : new Color(0.8f, 0.8f, 0.82f) }, 1.4f, -0.8f);
            }
        }
    }
    float kSmokeAt;
    Mesh kGlassOn, kGlassBurn, kGlassOff;
    Mesh OvenGlassMesh(bool burnt, bool off = false)
    {
        Mesh Make(Color c) { var b = new AcPB(); float T = Kc.Tile; b.Box(new Vector3(0, CounterH * 0.5f, -T / 2 + 0.01f), new Vector3(T * 0.6f, CounterH * 0.4f, 0.03f), c); return b.M("og"); }
        if (off) return kGlassOff ? kGlassOff : (kGlassOff = Make(new Color(0.25f, 0.2f, 0.2f)));
        if (burnt) return kGlassBurn ? kGlassBurn : (kGlassBurn = Make(new Color(0.3f, 0.25f, 0.25f)));
        return kGlassOn ? kGlassOn : (kGlassOn = Make(new Color(1f, 0.6f, 0.2f)));
    }

    void UpdateKitchenFx(float dt)
    {
        kFailFlash = Mathf.Max(0, kFailFlash - dt * 1.5f);
    }

    // ===== 아이콘 (주문표) =====
    readonly Dictionary<string, Texture2D> kIcons = new Dictionary<string, Texture2D>();
    readonly HashSet<string> kIconWant = new HashSet<string>();
    Texture KIcon(string it) { if (kIcons.TryGetValue(it, out var t) && t) return t; kIconWant.Add(it); return null; }
    void ProcessKitchenIcons()
    {
        if (kIconWant.Count == 0) return;
        EnsureIconCam();
        int n = 0;
        foreach (var it in kIconWant.ToList())
        {
            if (n++ >= 3) break;
            var go = MakeKItem(it); go.transform.rotation = Quaternion.Euler(35, 20, 0);
            var tex = ObjIcon(go);
            if (tex) { kIcons[it] = tex; kIconWant.Remove(it); }
        }
    }
    static string RecipeItem(int r) => r == 0 ? "mug+marsh.r+milk.h" : r == 1 ? "plate+cookie.i" : r == 2 ? "plate+sponge.c+straw.s" : "plate+potato.s+turkey.b";
    static readonly string[][] RecipeSteps = {
        new[] { "milk.r", "marsh.r", "mug" }, new[] { "dough.r", "cookie.i", "plate" }, new[] { "batter.r", "straw.r", "plate" }, new[] { "turkey.r", "potato.r", "plate" } };

    // ===== 화면 =====
    // 조작 영역 (화면 UI 좌표)
    Rect kBtnRect, kPadRect; Vector2 kStickBase; bool kStickOn; int kStickTouch = -1, kPadTouch = -1; Vector2 kPadLast; bool kBtnTouchDown;

    void KitchenGUI()
    {
        var s = KS;
        // 이름표
        foreach (var r in remotes.Values) if (r.net != null && (r.c.transform.position - KO).sqrMagnitude < 400f) KTag(r.c.transform.position, r.net.name, false);
        KTag(me.transform.position, uiName, true);
        // 진행 표시 (굽기·문지르기)
        foreach (var t in ks.tiles)
        {
            char c = Kc.At(s, t.i);
            float k = -1; Color col = new Color(0.4f, 0.8f, 0.4f); bool warn = false;
            if ((c == 'H' || c == 'O') && Kc.Cooks(c, t.it, out float ct, out _)) k = t.p / ct;
            else if ((c == 'H' || c == 'O') && Kc.CanBurn(c, t.it)) { float b = t.p / Kc.BurnAfter(c); if (b > 0.45f) { warn = true; k = b; col = new Color(0.95f, 0.3f, 0.2f); } }
            else if (c == 'X' && Kc.Chopped(t.it) != null && t.p > 0) { k = t.p / Kc.Strokes; col = new Color(1f, 0.75f, 0.25f); }
            if (c == 'O' && t.it != "" && k < 0) { DrawWorldIcon(KPos(t.i) + Vector3.up * 1.5f, t.it); }   // 다 구워진 것 / 탄 것
            if (k >= 0)
            {
                var sp = cam.WorldToScreenPoint(KPos(t.i) + Vector3.up * 1.35f); if (sp.z < 0) continue;
                float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
                var bar = new Rect(x - 24, y, 48, 9);
                AcUiTheme.Fill(bar, new Color(0, 0, 0, 0.45f), 0);
                AcUiTheme.Fill(new Rect(bar.x + 1, bar.y + 1, Mathf.Max(4, 46 * Mathf.Clamp01(k)), 7), col, 0);
                if (warn && Mathf.Repeat(Time.time * 4f, 1f) < 0.6f) GUI.Label(new Rect(x - 12, y - 30, 24, 28), "<size=24><b><color=#e53935>!</color></b></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            }
        }
        // 내는 곳 / 쓰레기통 이름 (대기실·처음 20초)
        if (km == KMode.Lobby || ks.t < 20f)
            for (int i = 0; i < Kc.W * Kc.H; i++) { char c = Kc.At(s, i); if (c == 'V' || c == 'R') KSmallTag(KPos(i) + Vector3.up * (c == 'V' ? 2.2f : 1.2f), c == 'V' ? "내는 곳" : "쓰레기통"); }
        if (toast != null && Time.time < toastUntil)
        {
            float tw = Mathf.Min(460, SW - 20);
            var r = new Rect(SW / 2 - tw / 2, SH * 0.52f, tw, 40);
            GUI.Box(r, "", sBox); GUI.Label(new Rect(r.x + 14, r.y + 9, r.width - 28, 24), "<size=13>" + toast + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        }
        if (km == KMode.Lobby) { KitchenLobbyGUI(); KControls(); return; }
        OrdersGUI();
        if (km == KMode.Count)
        {
            int left = Mathf.Max(1, Mathf.CeilToInt((float)((kT0 - NetNow) / 1000.0)));
            GUI.Label(new Rect(0, SH / 2 - 60, SW, 100), "<size=72><b>" + left + "</b></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(0, SH / 2 + 30, SW, 40), "<size=20>" + (ks.stage + 1) + ". " + s.name + "</size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
        }
        if (km == KMode.Play) KControls();
        if (km == KMode.Result) KitchenResultGUI();
        if (kFailFlash > 0) GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.9f, 0.1f, 0.1f, 0.18f * kFailFlash)));
    }

    void KTag(Vector3 p, string name, bool isMe)
    {
        var sp = cam.WorldToScreenPoint(p + Vector3.up * 1.75f); if (sp.z < 0) return;
        float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
        var st = isMe ? sTagMe : sTag; if (st == null) return;
        var size = st.CalcSize(new GUIContent(name));
        GUI.Label(new Rect(x - size.x / 2 - 6, y - 10, size.x + 12, 20), "<size=12>" + name + "</size>", st);
    }
    void KSmallTag(Vector3 p, string t)
    {
        var sp = cam.WorldToScreenPoint(p); if (sp.z < 0) return;
        float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
        var r = new Rect(x - 34, y - 10, 68, 20);
        AcUiTheme.Fill(r, new Color(1f, 1f, 1f, 0.85f), 2);
        GUI.Label(r, "<size=11>" + t + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
    }
    void DrawWorldIcon(Vector3 p, string it)
    {
        var sp = cam.WorldToScreenPoint(p); if (sp.z < 0) return;
        float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
        var r = new Rect(x - 18, y - 18, 36, 36);
        AcUiTheme.Fill(r, it == "coal" ? new Color(0.3f, 0.3f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.92f), 3);
        var tex = KIcon(it); if (tex) GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, 32, 32), tex, ScaleMode.ScaleToFit);
    }

    void OrdersGUI()
    {
        var s = KS;
        float cw = Mathf.Min(78, (SW - 20 - 4 * 5) / 5f), chh = cw + 30;
        float x = 10;
        foreach (var o in ks.orders.OrderBy(q => q.id))
        {
            var r = new Rect(x, 8, cw, chh);
            float left = Mathf.Clamp01(1f - (ks.t - o.t0) / o.life);
            bool hurry = left < 0.25f;
            AcUiTheme.Fill(r, hurry && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f ? new Color(1f, 0.85f, 0.85f) : new Color(1f, 0.99f, 0.95f), 3);
            AcUiTheme.Ring(r, hurry ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.85f, 0.75f, 0.6f), 2);
            var tex = KIcon(RecipeItem(o.r)); if (tex) GUI.DrawTexture(new Rect(r.x + 4, r.y + 3, cw - 8, cw - 14), tex, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x, r.y + cw - 13, cw, 16), "<size=10>" + Kc.RecipeKo[o.r] + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow });
            // 필요한 재료 작은 그림
            var steps = RecipeSteps[o.r]; float iw = (cw - 8) / steps.Length;
            for (int k = 0; k < steps.Length; k++) { var t2 = KIcon(steps[k]); if (t2) GUI.DrawTexture(new Rect(r.x + 4 + k * iw, r.y + cw + 2, iw - 1, 16), t2, ScaleMode.ScaleToFit); }
            var bar = new Rect(r.x + 4, r.yMax - 8, cw - 8, 5);
            AcUiTheme.Fill(bar, new Color(0.85f, 0.85f, 0.85f), 0);
            AcUiTheme.Fill(new Rect(bar.x, bar.y, bar.width * left, bar.height), Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.35f, 0.75f, 0.35f), left), 0);
            x += cw + 5;
        }
        // 시간·점수
        float remain = Mathf.Max(0, s.time - ks.t);
        var tr = new Rect(10, 8 + chh + 6, 92, 28);
        AcUiTheme.Fill(tr, new Color(1f, 1f, 1f, 0.88f), 3);
        GUI.Label(tr, $"<size=15><b>{(int)remain / 60}:{(int)remain % 60:00}</b></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = remain < 20 ? new Color(0.85f, 0.2f, 0.2f) : sLabel.normal.textColor } });
        var sr = new Rect(SW - 10 - 110, 8 + chh + 6, 110, 28);
        AcUiTheme.Fill(sr, new Color(1f, 1f, 1f, 0.88f), 3);
        int stars = Kc.Stars(s, ks.nPlayers, ks.score);
        GUI.Label(sr, $"<size=15><b>{ks.score}점</b></size> <size=12>별 {stars}</size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
        for (int i = kPops.Count - 1; i >= 0; i--)
        {
            float a = Time.unscaledTime - kPops[i].at; if (a > 1.2f) { kPops.RemoveAt(i); continue; }
            GUI.Label(new Rect(sr.x, sr.yMax + 4 - a * 30, sr.width, 30), "<size=20><b><color=#2e7d32>" + kPops[i].text + "</color></b></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
        }
    }

    // 조작: 왼쪽 아래 끌기 = 이동, 오른쪽 아래 버튼 = 잡기/놓기, 도마 앞이면 버튼 위 문지르기 판
    void KControls()
    {
        bool narrow = SW < 640;
        float bs = narrow ? 92 : 84;
        kBtnRect = new Rect(SW - bs - 18, SH - bs - 22, bs, bs);
        bool chopHere = kTarget >= 0 && Kc.At(KS, kTarget) == 'X' && Kc.Chopped(Kc.ItemAt(ks, kTarget)) != null;
        kPadRect = chopHere ? new Rect(SW - 18 - (narrow ? 170 : 200), kBtnRect.y - (narrow ? 118 : 110), narrow ? 170 : 200, narrow ? 104 : 96) : new Rect(-999, -999, 0, 0);
        string label = KActionLabel();
        // 버튼
        AcUiTheme.DrawGlow(kBtnRect, label != "" ? 0.6f + 0.4f * Mathf.Sin(Time.time * 5f) : 0.15f);
        R(kBtnRect);
        if (GUI.Button(kBtnRect, "<size=" + (narrow ? 17 : 15) + "><b>" + (label == "" ? "잡기\n놓기" : label) + "</b></size>", sBtnBig) && Time.unscaledTime - kTouchAt > 1.5f) { if (kTarget >= 0) KDo('g', kTarget); }
        if (chopHere)
        {
            bool spread = Kc.IsSpread(Kc.ItemAt(ks, kTarget));
            var t = Kc.TileAt(ks, kTarget, false);
            AcUiTheme.Fill(kPadRect, new Color(1f, 0.97f, 0.88f, 0.92f), 3);
            AcUiTheme.Ring(kPadRect, AcUiTheme.Gold, 3);
            GUI.Label(new Rect(kPadRect.x, kPadRect.y + 8, kPadRect.width, 30), "<size=17><b>" + (spread ? "쓱쓱 문질러 바르기" : "쓱쓱 문질러 썰기") + "</b></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            int done = t != null ? Mathf.RoundToInt(t.p) : 0;
            for (int k = 0; k < Kc.Strokes; k++)
            {
                var d = new Rect(kPadRect.center.x - Kc.Strokes * 15 + k * 30 + 4, kPadRect.y + 44, 22, 22);
                AcUiTheme.Fill(d, k < done ? new Color(1f, 0.7f, 0.2f) : new Color(0.85f, 0.82f, 0.78f), 10);
            }
            GUI.Label(new Rect(kPadRect.x, kPadRect.yMax - 28, kPadRect.width, 24), "<size=12>" + (IsMobile ? "여기를 좌우로 문질러요" : "여기를 끌거나 E 키") + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            R(kPadRect);
            // 마우스로 끌기 (PC)
            var e = Event.current;
            if (Time.unscaledTime - kTouchAt > 1.5f)
            {
                if (e.type == EventType.MouseDown && kPadRect.Contains(e.mousePosition)) { kPadMouse = true; kPadLast = e.mousePosition; kChopAccum = 0; e.Use(); }
                if (e.type == EventType.MouseDrag && kPadMouse) { kChopAccum += (e.mousePosition - kPadLast).magnitude; kPadLast = e.mousePosition; if (kChopAccum > 70f) { kChopAccum = 0; KDo('c', kTarget); } e.Use(); }
                if (e.type == EventType.MouseUp) kPadMouse = false;
            }
        }
        // 손가락 조작 표시
        if (IsMobile && kStickOn)
        {
            var c = kStickBase / UiK; c.y = SH - c.y;
            AcUiTheme.Fill(new Rect(c.x - 56, c.y - 56, 112, 112), new Color(1f, 1f, 1f, 0.35f), 56);
            var k = c + new Vector2(kStick.x, -kStick.y) * 44f;
            AcUiTheme.Fill(new Rect(k.x - 26, k.y - 26, 52, 52), new Color(1f, 1f, 1f, 0.85f), 26);
        }
        else if (IsMobile && km != KMode.Lobby)
            GUI.Label(new Rect(16, SH - 60, SW * 0.5f, 40), "<size=12>왼쪽을 누른 채 끌어서 이동</size>", new GUIStyle(sSmall) { normal = { textColor = new Color(0.35f, 0.3f, 0.3f) } });
        else if (km != KMode.Lobby)
            GUI.Label(new Rect(16, SH - 34, SW * 0.6f, 24), "<size=12>WASD/방향키 이동 · 스페이스 잡기/놓기 · E 문지르기</size>", sSmall);
        KTouch();
    }
    bool kPadMouse;

    string KActionLabel()
    {
        if (kTarget < 0) return "";
        char c = Kc.At(KS, kTarget); var hand = ks.hands.FirstOrDefault(hh => hh.id == myId); string h = hand?.it ?? ""; string on = Kc.ItemAt(ks, kTarget);
        if (c == 'R') return h != "" ? "버리기" : "";
        if (c == 'V') return h != "" ? "내기" : "";
        if (Kc.IsDispenser(c)) return h == "" ? "꺼내기" : "담기";
        if (c == 'H' || c == 'O') return h == "" ? (on != "" ? "꺼내기" : "") : (on == "" ? (c == 'O' ? "굽기" : "데우기") : "담기");
        if (h == "" && on != "") return "집기";
        if (h != "" && on == "") return "놓기";
        if (h != "" && on != "") return "담기";
        return "";
    }

    // 여러 손가락 (이동 + 버튼 + 문지르기 동시에)
    void KTouch()
    {
        if (Event.current.type != EventType.Repaint) return;
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts == null) return;
        bool stickAlive = false, padAlive = false;
        foreach (var t in ts.touches)
        {
            int id = t.touchId.ReadValue();
            var ph = t.phase.ReadValue();
            var pos = t.position.ReadValue();                       // 화면 픽셀, 아래가 0
            var ui = new Vector2(pos.x / UiK, (Screen.height - pos.y) / UiK);
            bool began = ph == UnityEngine.InputSystem.TouchPhase.Began;
            bool active = ph == UnityEngine.InputSystem.TouchPhase.Began || ph == UnityEngine.InputSystem.TouchPhase.Moved || ph == UnityEngine.InputSystem.TouchPhase.Stationary;
            if (!active) continue;
            if (began)
            {
                kTouchAt = Time.unscaledTime;
                if (kBtnRect.Contains(ui)) { if (kBtnSeen != id) { kBtnSeen = id; if (kTarget >= 0) KDo('g', kTarget); } continue; }
                if (kPadRect.Contains(ui)) { kPadTouch = id; kPadLast = ui; kChopAccum = 0; continue; }
                if (ui.x < SW * 0.62f && ui.y > SH * 0.25f && kStickTouch < 0) { kStickTouch = id; kStickBase = pos; }
            }
            if (id == kStickTouch)
            {
                stickAlive = true;
                var d = (pos - kStickBase) / (44f * UiK);
                kStick = Vector2.ClampMagnitude(d, 1f); if (kStick.magnitude < 0.15f) kStick = Vector2.zero;
                if (d.magnitude > 1.4f) kStickBase = pos - d.normalized * 1.4f * 44f * UiK;   // 손가락을 따라 받침도 옮김
            }
            if (id == kPadTouch)
            {
                padAlive = true;
                kChopAccum += (ui - kPadLast).magnitude; kPadLast = ui;
                if (kChopAccum > 55f) { kChopAccum = 0; if (kTarget >= 0) KDo('c', kTarget); }
            }
        }
        if (!stickAlive && kStickTouch >= 0) { kStickTouch = -1; kStick = Vector2.zero; kStickOn = false; }
        if (stickAlive) kStickOn = true;
        if (!padAlive) kPadTouch = -1;
        bool anyBtn = false; foreach (var t in ts.touches) if (t.touchId.ReadValue() == kBtnSeen && t.press.isPressed) anyBtn = true;
        if (!anyBtn) kBtnSeen = -1;
#endif
    }
    int kBtnSeen = -1; float kTouchAt = -10f;

    void KitchenLobbyGUI()
    {
        bool narrow = SW < 640;
        float bsz = narrow ? 92 : 84;
        float w = Mathf.Min(SW - bsz - 46, 500), h = 300;
        var r = new Rect(narrow ? 10 : SW / 2 - w / 2, SH - h - 10, w, h);   // 아래쪽 (오른쪽은 잡기 버튼 자리)
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 16, r.y + 10, w - 32, 28), "<b>산타의 크리스마스 주방</b>", sLabel);
        var s = Kc.Stages[kStage];
        bool locked = kStage > KUnlocked;
        float cy = r.y + 44;
        if (GUI.Button(new Rect(r.x + 16, cy, 34, 34), "<", sBtn)) { kStage = Mathf.Max(0, kStage - 1); NewPractice(kStage); me.transform.position = KSpawn(0); }
        if (GUI.Button(new Rect(r.xMax - 50, cy, 34, 34), ">", sBtn)) { kStage = Mathf.Min(Kc.Stages.Length - 1, kStage + 1); NewPractice(kStage); me.transform.position = KSpawn(0); }
        GUI.Label(new Rect(r.x + 56, cy + 2, w - 112, 30), "<size=15><b>" + (kStage + 1) + ". " + s.name + "</b></size>" + (locked ? " <size=11>(잠김)</size>" : ""), new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow });
        cy += 38;
        int best = KBestStars(kStage);
        GUI.Label(new Rect(r.x + 10, cy, w - 20, 36), "<size=12>" + s.desc + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
        cy += 36;
        // 메뉴 그림 + 내 최고 별
        float iw = 44, total = s.menus.Length * (iw + 6);
        for (int k = 0; k < s.menus.Length; k++)
        {
            var ir = new Rect(r.center.x - total / 2 + k * (iw + 6), cy, iw, iw);
            AcUiTheme.Fill(ir, new Color(1f, 1f, 1f, 0.85f), 3);
            var tex = KIcon(RecipeItem(s.menus[k])); if (tex) GUI.DrawTexture(ir, tex, ScaleMode.ScaleToFit);
        }
        cy += iw + 4;
        GUI.Label(new Rect(r.x + 16, cy, w - 32, 22), "<size=13>" + string.Join(" · ", s.menus.Select(m => Kc.RecipeKo[m])) + (best > 0 ? $"   내 최고: 별 {best}개" : "") + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        cy += 24;
        var with = new List<string> { uiName.Trim() };
        foreach (var kv in kLobby) if (Time.unscaledTime - kv.Value.at < 3.5f && remotes.ContainsKey(kv.Key)) with.Add(kv.Value.name);
        GUI.Label(new Rect(r.x + 16, cy, w - 32, 22), "<size=13>함께: " + string.Join(", ", with.Take(4)) + (with.Count > 4 ? " ..." : "") + $"  ({Mathf.Min(4, with.Count)}명)</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        cy += 24;
        string rec = KBestRecord(kStage);
        if (rec != "") GUI.Label(new Rect(r.x + 16, cy, w - 32, 22), "<size=12>" + rec + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        float bw = Mathf.Min(110, (w - 44) / 2), by = r.yMax - 50;
        if (GUI.Button(R(new Rect(r.center.x - bw - 6, by, bw, 40)), "나가기", sBtn)) { CloseKitchen(); return; }
        GUI.enabled = !locked;
        if (GUI.Button(R(new Rect(r.center.x + 6, by, bw, 40)), locked ? "<size=12>앞 판을 깨요</size>" : "<b>시작!</b>", sBtnBig)) KitchenStart();
        GUI.enabled = true;
    }

    string KBestRecord(int stage)
    {
        var node = net != null ? net.Get("cks") : null;
        if (node == null) return "";
        int bestScore = -1; string who = ""; int n = 0;
        foreach (var kv in node)
        {
            if (!(kv.Value is Dictionary<string, object> d) || (int)AcJson.D(d, "stage") != stage) continue;
            int sc = (int)AcJson.D(d, "score");
            if (sc > bestScore) { bestScore = sc; who = AcJson.S(d, "names"); n = (int)AcJson.D(d, "n"); }
        }
        return bestScore < 0 ? "" : $"최고 기록 {bestScore}점 · {who} ({n}명)";
    }

    void KitchenResultGUI()
    {
        var s = KS;
        float w = Mathf.Min(SW - 24, 420), h = 300;
        var r = new Rect(SW / 2 - w / 2, SH / 2 - h / 2, w, h);
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0, 0, 0, 0.25f)));
        GUI.Box(R(r), "", sBox);
        var center = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(r.x, r.y + 12, w, 30), kLostHost ? "<b>게임이 멈췄어요</b>" : "<size=22><b>" + (ks.stage + 1) + ". " + s.name + "</b></size>", center);
        float a = Time.unscaledTime - kResultAt;
        for (int k = 0; k < 3; k++)
        {
            bool on = k < kResultStars && a > 0.3f + k * 0.35f;
            var sr = new Rect(r.center.x - 90 + k * 64, r.y + 50, 52, 52);
            float pop = on ? Mathf.Clamp01((a - 0.3f - k * 0.35f) / 0.25f) : 1f;
            var sr2 = new Rect(sr.center.x - 26 * (0.6f + 0.4f * pop), sr.center.y - 26 * (0.6f + 0.4f * pop), 52 * (0.6f + 0.4f * pop), 52 * (0.6f + 0.4f * pop));
            AcUiTheme.Fill(sr2, on ? new Color(1f, 0.8f, 0.2f) : new Color(0.85f, 0.82f, 0.78f), 26);
            GUI.Label(sr2, "<size=26><b>" + (k + 1) + "</b></size>", center);
            if (on && a - dtStarSnd(k) < Time.unscaledDeltaTime && a >= dtStarSnd(k)) Sfx("star", 0.5f, 1f + k * 0.15f);
        }
        GUI.Label(new Rect(r.x, r.y + 112, w, 40), $"<size=30><b>{ks.score}점</b></size>", center);
        int n = Mathf.Clamp(ks.nPlayers, 1, 4) - 1;
        GUI.Label(new Rect(r.x, r.y + 152, w, 24), $"<size=13>낸 요리 {ks.served}개 · 놓친 주문 {ks.failed}개 · {ks.nPlayers}명</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        GUI.Label(new Rect(r.x, r.y + 176, w, 24), $"<size=12>별 기준: {s.thr[n, 0]} / {s.thr[n, 1]} / {s.thr[n, 2]}점</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        if (kResultStars > 0 && ks.stage + 1 < Kc.Stages.Length) GUI.Label(new Rect(r.x, r.y + 200, w, 24), "<size=13><color=#2e7d32>다음 스테이지가 열렸어요!</color></size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        else if (kResultStars == 0 && !kLostHost) GUI.Label(new Rect(r.x, r.y + 200, w, 24), "<size=13>별 1개를 받으면 다음 스테이지가 열려요</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        float bw = 120, by = r.yMax - 52;
        if (GUI.Button(R(new Rect(r.center.x - bw - 6, by, bw, 40)), "나가기", sBtn)) { CloseKitchen(); return; }
        if (GUI.Button(R(new Rect(r.center.x + 6, by, bw, 40)), "<b>대기실로</b>", sBtnBig))
        {
            km = KMode.Lobby; kLostHost = false;
            if (kResultStars > 0 && ks.stage + 1 <= KUnlocked) kStage = ks.stage + 1; else kStage = Mathf.Min(ks.stage, KUnlocked);
            NewPractice(kStage); me.transform.position = KSpawn(0);
        }
    }
    static float dtStarSnd(int k) => 0.3f + k * 0.35f;
}
