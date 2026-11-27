// AcSocial.cs - 참가자 목록(따라가기·쪽지), 받은 쪽지, 상황별 버튼(트리 꾸미기 / 눈사람 만들기 / 편지함)
// 쪽지: rooms/<방>/msg/<받는사람id>/<쪽지id> = {from, fromName, text, ct}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AcRoom
{
    bool showPeople;
    string selPeer;                 // 목록에서 고른 사람
    string followId;                // 따라가는 사람
    Vector2 peopleScroll;
    double joinedAt;

    class Note { public string id, from, fromName, text; public double ct; public bool read; }
    readonly List<Note> inbox = new List<Note>();
    readonly HashSet<string> seenNotes = new HashSet<string>();
    bool notesPrimed; float nextNotePoll;

    // ===== 이름 = 그 사람의 주소 (다음에 같은 이름으로 들어오면 편지·쪽지·점수가 그대로) =====
    public static string NameKey(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in (name ?? "").Trim())
            if (".$#[]/~\\".IndexOf(ch) < 0 && !char.IsControl(ch)) sb.Append(ch);
        var k = sb.ToString();
        return k.Length > 24 ? k.Substring(0, 24) : k == "" ? "noname" : k;
    }
    string myKey => NameKey(uiName);
    float nextMemberWrite;

    void UpdateMember()
    {
        if (net == null || Time.unscaledTime < nextMemberWrite) return;
        nextMemberWrite = Time.unscaledTime + 60f;
        net.Put("members/" + myKey, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"look\":" + AcJson.Esc(me.look.Encode()) + ",\"last\":" + AcSync.UnixMs.ToString("0") + "}");
    }

    // ===== 쪽지 =====
    void SendNote(string toId, string toName, string text)
    {
        if (net == null) return;
        text = text.Trim(); if (text == "") return;
        if (text.Length > 200) text = text.Substring(0, 200);
        string id = "n" + Guid.NewGuid().ToString("N").Substring(0, 10);
        string json = "{\"from\":" + AcJson.Esc(myKey) + ",\"fromName\":" + AcJson.Esc(uiName.Trim()) + ",\"text\":" + AcJson.Esc(text) + ",\"ct\":" + AcSync.UnixMs.ToString("0") + "}";
        net.Put("msg/" + toId + "/" + id, json);
        Toast(toName + " 님에게 쪽지를 보냈어요");
    }

    void PollNotes()
    {
        if (net == null || Time.unscaledTime < nextNotePoll) return;
        nextNotePoll = Time.unscaledTime + 0.5f;
        var box = net.Get("msg/" + myKey);
        if (box != null)
            foreach (var kv in box)
            {
                if (seenNotes.Contains(kv.Key) || !(kv.Value is Dictionary<string, object> d)) continue;
                seenNotes.Add(kv.Key);
                var n = new Note { id = kv.Key, from = AcJson.S(d, "from"), fromName = AcJson.S(d, "fromName"), text = AcJson.S(d, "text"), ct = AcJson.D(d, "ct") };
                if (readNotes == null) readNotes = new HashSet<string>(PlayerPrefs.GetString("ac_notes_read", "").Split(','));
                n.read = readNotes.Contains(n.id);
                inbox.Add(n);
                if (notesPrimed) { Toast(n.fromName + " 님의 쪽지: " + Short(n.text, 24)); Log("[쪽지] " + n.fromName + ": " + n.text); }
            }
        notesPrimed = true;   // 들어오기 전에 온 쪽지는 조용히 "새 쪽지"로만
        inbox.Sort((a, b) => b.ct.CompareTo(a.ct));
    }

    HashSet<string> readNotes;
    void MarkNoteRead(Note n)
    {
        n.read = true;
        if (readNotes == null) readNotes = new HashSet<string>();
        if (readNotes.Add(n.id)) { PlayerPrefs.SetString("ac_notes_read", string.Join(",", readNotes.Take(400))); PlayerPrefs.Save(); }
    }

    static string Short(string s, int n) => s == null ? "" : s.Length > n ? s.Substring(0, n) + "..." : s;
    int UnreadNotes => inbox.Count(n => !n.read);

    // ===== 따라가기 =====
    void UpdateFollow()
    {
        if (followId == null) return;
        if (!remotes.TryGetValue(followId, out var r) || r.c == null) { followId = null; Toast("따라가던 친구가 나갔어요"); return; }
        if (MoveAxis().sqrMagnitude > 0.01f || (ptrDown && !ptrOnUI) || decorating || building || lettersOpen) { followId = null; return; }
        var tp = r.c.transform.position;
        if (Vector3.Distance(tp, me.transform.position) < 1.7f) { walkTarget = null; return; }
        walkTarget = FollowStep(me.transform.position, tp);
    }

    // 길찾기가 실패했을 때: 그냥 그쪽으로 (트리는 돌아서)
    Vector3 NextWaypoint(Vector3 a, Vector3 b) => AvoidTree(a, b);

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    // 가는 길이 트리를 지나가면 트리 옆으로 돌아감
    Vector3 AvoidTree(Vector3 a, Vector3 b)
    {
        var t = new Vector3(TreePos.x, 0, TreePos.z);
        var ab = b - a; ab.y = 0; float len = ab.magnitude;
        if (len < 0.01f) return b;
        var d = ab / len;
        float along = Mathf.Clamp(Vector3.Dot(t - a, d), 0, len);
        var closest = a + d * along; closest.y = 0;
        var off = closest - t; off.y = 0;
        if (off.magnitude > TreeR + 0.5f || along <= 0.01f || along >= len - 0.01f) return b;
        if (off.magnitude < 0.05f) off = new Vector3(-d.z, 0, d.x);
        var w = t + off.normalized * (TreeR + 1.1f);
        return Flat(a, w) < 0.8f ? b : w;
    }


    // ===== 상황별 버튼 =====
    float TreeDist => Vector2.Distance(new Vector2(me.transform.position.x, me.transform.position.z), new Vector2(TreePos.x, TreePos.z));
    bool NearTree => phase == Phase.Play && !Outside && TreeDist < TreeR + 2.4f && AreaAt(me.transform.position) == "트리 거실";   // 트리 바로 앞, 거실 안에서만 (옆방 피아노 앞에서 안 뜨게)
    Vector3 firePos;
    bool NearFireplace => phase == Phase.Play && !Outside && Flat(me.transform.position, firePos) < 3.2f;
    bool InBakery => phase == Phase.Play && !Outside && AreaAt(me.transform.position) == "쿠키 공방";
    Vector3 mailPos = new Vector3(9999, 0, 9999), mailPos2 = new Vector3(9999, 0, 9999);
    // 편지함: 입구 우편함·앞마당 우편함·거실 벽난로 옆
    bool InStudy => phase == Phase.Play && (Flat(me.transform.position, mailPos) < 2.8f || Flat(me.transform.position, mailPos2) < 2.4f || NearFireplace);

    // 큰 버튼 옆 작은 버튼: 오른쪽에 자리가 없으면 큰 버튼 위에 둠 (화면 밖으로 나가거나 겹치지 않게)
    Rect Side(Rect r, float w)
    {
        if (r.xMax + 14 + w <= SW - 8) return new Rect(r.xMax + 14, r.y + 6, w, r.height - 12);
        return new Rect(r.center.x - w / 2, r.y - (r.height - 12) - 14, w, r.height - 12);
    }

    void ContextButtons()
    {
        if (phase != Phase.Play || showCustom) return;
        string label = null; Action act = null;
        bool piano = NearPiano(out bool musicRoom);
        bool inLibrary = !Outside && AreaAt(me.transform.position) == "서재";
        if (InStudy) { label = UnreadLetters > 0 ? $"편지함 ({UnreadLetters})" : "편지함"; act = OpenLetters; }
        else if (NearTree) { label = "트리 꾸미기"; act = StartDecorating; }
        else if (Outside && !snowMode) { label = "눈사람 만들기"; act = StartBuilding; }
        else if (Outside) { label = "눈싸움 그만"; act = ToggleSnowFight; }
        else if (InBakery) { label = "쿠키 달리기 게임"; act = OpenCookieGame; }
        else if (InPlayroom) { label = "캔디 팡 게임"; act = OpenCandy; }
        else if (InKitchenRoom) { label = "크리스마스 주방 게임"; act = OpenKitchen; }
        else if (piano) { label = "피아노 치기"; act = OpenPiano; }
        else if (inLibrary) { label = "크리스마스 퀴즈"; act = OpenQuiz; }
        var gextra = new List<(string, Action, float)>();
        if (label == null && GreenContext(out var gl, out var ga, gextra)) { label = gl; act = ga; }
        if (label == null && InGiftRoom) { label = "선물 포장하기"; act = OpenWrap; }
        if (label == null && pillowMode) { label = "베개 휘두르기"; act = () => SwingPillow(null); }   // 침실: 화면 아무 데나 누르는 대신 버튼으로
        if (label == null) return;
        float w = IsMobile ? 170 : 150, h = IsMobile ? 52 : 42;
        float y = SH - (IsMobile ? 46 : 36) - 12 - 16 - h - 10;
        var r = new Rect(SW / 2 - w / 2, y, w, h);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        AcUiTheme.DrawGlow(r, pulse);   // 네모난 깜빡이 테두리 대신 은은한 빛
        R(r);
        if (GUI.Button(r, "<b>" + label + "</b>", sBtnBig)) act();
        // 작은 버튼들: 오른쪽에 자리가 있으면 옆으로, 없으면 큰 버튼 위 한 줄로
        var extra = new List<(string label, Action act, float w)>();
        if (Outside)
        {
            if (!IsSantaMe) extra.Add((me.skating ? "스케이트 벗기" : "스케이트 타기", () => { me.SetSkates(!me.skating); skateVel = Vector3.zero; if (me.skating) snowMode = false; if (me.skating) Toast("씽씽! 누르거나 방향키를 계속 누르면 빨라져요"); }, IsMobile ? 132 : 120));
            if (!snowMode && label != "눈싸움 그만") extra.Add(("눈싸움", ToggleSnowFight, IsMobile ? 112 : 100));
            if (snowMode && label != "눈싸움 그만") extra.Add(("눈싸움 그만", ToggleSnowFight, IsMobile ? 112 : 100));
        }
        if (NearTree && !InStudy) extra.Add(("트리 전경", () => { treeOverview = true; decoYaw = 180f; decoZoom = 1f; }, IsMobile ? 112 : 100));
        if (inLibrary && label != "크리스마스 퀴즈") extra.Add(("크리스마스 퀴즈", OpenQuiz, IsMobile ? 150 : 136));
        if (piano && label != "피아노 치기") extra.Add(("피아노 치기", OpenPiano, IsMobile ? 112 : 100));
        foreach (var ge in gextra) extra.Add(ge);
        if (extra.Count == 0) return;
        float bh = h - 12, total = extra.Sum(e => e.w) + 10 * (extra.Count - 1);
        bool right = r.xMax + 14 + total <= SW - 8;
        float x = right ? r.xMax + 14 : SW / 2 - total / 2, yy = right ? r.y + 6 : r.y - bh - 14;
        foreach (var e in extra)
        {
            var b = new Rect(x, yy, e.w, bh); R(b);
            if (GUI.Button(b, e.label, sBtn)) e.act();
            x += e.w + 10;
        }
    }

    GUIStyle sBtnBig;

    // ===== 참가자 목록 (오른쪽 위) =====
    void PeopleUI()
    {
        if (phase != Phase.Play) return;
        int count = 1 + remotes.Count;
        int unread = UnreadNotes;
        float bw = 130, bh = IsMobile ? 40 : 32;
        var br = new Rect(SW - bw - 12, 12, bw, bh);
        string bl = $"참가자 {count}명" + (unread > 0 ? $"  <color=#d03030>쪽지 {unread}</color>" : "");
        if (unread > 0) br.width = bw + 60;
        br.x = SW - br.width - 12;
        R(br);
        MapButton(br);
        MusicButton(new Rect(br.x - 78, br.y, 70, br.height));
        if (GUI.Button(br, bl, sBtn)) showPeople = !showPeople;
        if (followId != null && remotes.TryGetValue(followId, out var fr) && fr.net != null)
        {
            var fb = new Rect(SW - 232, br.yMax + 6, 220, bh);
            R(fb);
            if (GUI.Button(fb, fr.net.name + " 님 따라가는 중 (그만)", sBtn)) { followId = null; walkTarget = null; }
        }
        if (!showPeople) return;

        float w = Mathf.Min(300, SW - 24), top = br.yMax + (followId != null ? bh + 12 : 6);
        float h = Mathf.Min(SH - top - (IsMobile ? 90 : 70), 520);
        var panel = new Rect(SW - w - 12, top, w, h);
        AcUiTheme.Fill(panel, new Color(1f, 0.995f, 0.985f, 1f), 0);   // 뒤 글자가 비치지 않게 불투명 바탕
        GUI.Box(R(panel), "", sBox);
        if (GUI.Button(R(new Rect(panel.xMax - 70, panel.y + 8, 60, 28)), "닫기", sBtn)) { showPeople = false; return; }
        GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 8, panel.width - 20, panel.height - 16));
        peopleScroll = BeginDragScroll("people", peopleScroll);
        GUILayout.Label("참가자", sLabel);
        PersonRow(myId, uiName.Trim() + " (나)", true);
        foreach (var kv in remotes.OrderBy(k => k.Value.net?.name ?? ""))
            if (kv.Value.net != null) PersonRow(kv.Key, kv.Value.net.name, false);
        if (remotes.Count == 0) GUILayout.Label("<size=13>아직 다른 사람이 없어요. 같은 방 코드로 들어오면 여기에 보여요.</size>", sSmallWrap);
        GUILayout.Space(10);
        GUILayout.Label("받은 쪽지" + (inbox.Count > 0 ? $" ({inbox.Count})" : ""), sLabel);
        if (inbox.Count == 0) GUILayout.Label("<size=13>받은 쪽지가 없어요</size>", sSmall);
        foreach (var n in inbox.Take(30))
        {
            GUILayout.BeginVertical(sCard);
            GUILayout.Label((n.read ? "" : "<color=#d03030>새 쪽지</color>  ") + "<b>" + n.fromName + "</b>  <size=11>" + Ago(n.ct) + "</size>", sSmall);
            GUILayout.Label(n.text, sSmallWrap);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("답장", sBtn, GUILayout.Width(70), GUILayout.Height(26)) && !Dragging)
            {
                var nn = n; MarkNoteRead(nn);
                Ask(nn.fromName + " 님에게 답장", "", false, t => SendNote(nn.from, nn.fromName, t));
            }
            if (!n.read && GUILayout.Button("읽음", sBtn, GUILayout.Width(60), GUILayout.Height(26)) && !Dragging) MarkNoteRead(n);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
        EndDragScroll("people");
        GUILayout.EndArea();
    }

    void PersonRow(string id, string name, bool isMe)
    {
        bool sel = selPeer == id;
        GUILayout.BeginVertical(sel ? sCardSel : sCard);
        bool following = followId == id;
        if (GUILayout.Button((following ? "> " : "") + name, sel ? sLabelSel : sSmall, GUILayout.Height(28)) && !Dragging) selPeer = sel ? null : id;
        if (sel && !isMe)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(following ? "그만 따라가기" : "따라가기", sBtn, GUILayout.Height(30)) && !Dragging)
            {
                followId = following ? null : id; walkTarget = null;
                if (!following) { Toast(name + " 님을 따라가요"); showPeople = !IsMobile && showPeople; }
            }
            if (GUILayout.Button("쪽지 보내기", sBtn, GUILayout.Height(30)) && !Dragging)
            {
                string nm = name;
                Ask(nm + " 님에게 쪽지", "", false, t => SendNote(NameKey(nm), nm, t));
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    static string Ago(double ct)
    {
        double s = (AcSync.UnixMs - ct) / 1000.0;
        if (ct <= 0 || s < 0) return "";
        if (s < 60) return "방금";
        if (s < 3600) return (int)(s / 60) + "분 전";
        if (s < 86400) return (int)(s / 3600) + "시간 전";
        return (int)(s / 86400) + "일 전";
    }

    GUIStyle sCard, sCardSel, sSmallWrap;
    void SocialStyles()
    {
        if (sCard != null) return;
        sCard = AcUiTheme.Card(new Color(1f, 1f, 1f));
        sCardSel = AcUiTheme.Card(new Color(1f, 0.92f, 0.86f));
        sSmallWrap = new GUIStyle(sSmall) { wordWrap = true };
        sBtnBig = AcUiTheme.BigButton(GUI.skin.button, 18);
    }
}
