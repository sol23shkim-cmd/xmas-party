// AcLetters.cs - 서재의 편지함: 편지 쓰기(비밀/공개), 나에게 온 편지·공개 편지를 테이블 위 봉투로 보고 읽기. 글씨는 손글씨체.
// 편지: rooms/<방>/letters/<id> = {from, fromName, to("all" 또는 id), toName, title, body, ct}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AcRoom
{
    class Letter { public string id, from, fromName, to, toName, title, body; public double ct; }
    readonly List<Letter> letters = new List<Letter>();
    readonly HashSet<string> knownLetters = new HashSet<string>();
    HashSet<string> readLetters;
    bool lettersPrimed, lettersOpen;
    float nextLetterPoll;
    int letterTab;              // 0 나에게 온 편지, 1 공개 편지, 2 보낸 편지, 3 쓰기
    Letter openLetter;
    Vector2 letterScroll, readScroll;
    // 쓰기
    int writeTo; string writeTitle = "", writeBody = "";

    static Font handFont;
    GUIStyle sHand, sHandTitle, sHandSmall, sHandArea, sHandField;
    Texture2D envTex, envTexNew, woodTex, paperTex;

    void LetterStyles()
    {
        if (sHand != null) return;
        if (handFont == null) handFont = Resources.Load<Font>("Fonts/NanumPenScript-Regular");
        var ink = new Color(0.27f, 0.18f, 0.13f);
        sHand = new GUIStyle(GUI.skin.label) { font = handFont, fontSize = 28, wordWrap = true, richText = false, normal = { textColor = ink } };
        sHandTitle = new GUIStyle(sHand) { fontSize = 38, wordWrap = true };
        sHandSmall = new GUIStyle(sHand) { fontSize = 24, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.UpperCenter };
        var paperBg = AcPalette.Tex(new Color(1f, 0.99f, 0.95f));
        sHandArea = new GUIStyle(GUI.skin.textArea) { font = handFont, fontSize = 28, wordWrap = true, normal = { textColor = ink, background = paperBg }, focused = { textColor = ink, background = paperBg }, hover = { textColor = ink, background = paperBg }, active = { textColor = ink, background = paperBg } };
        sHandField = new GUIStyle(GUI.skin.textField) { font = handFont, fontSize = 28, normal = { textColor = ink, background = paperBg }, focused = { textColor = ink, background = paperBg }, hover = { textColor = ink, background = paperBg }, active = { textColor = ink, background = paperBg } };
        envTex = EnvelopeTex(false); envTexNew = EnvelopeTex(true);
        woodTex = WoodTex(); paperTex = PaperTex();
    }

    // 봉투 그림 (날개 + 하트 봉인)
    static Texture2D EnvelopeTex(bool fresh)
    {
        int w = 120, h = 80;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var paper = fresh ? new Color(1f, 0.95f, 0.86f) : new Color(0.98f, 0.95f, 0.9f);
        var edge = new Color(0.78f, 0.62f, 0.48f); var flap = fresh ? new Color(0.98f, 0.86f, 0.74f) : new Color(0.93f, 0.88f, 0.8f);
        var seal = new Color(0.85f, 0.2f, 0.25f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = paper;
                float fy = (h - 1 - y);                      // 위에서부터
                float vx = Mathf.Abs(x - w / 2f) / (w / 2f);
                bool inFlap = fy < (1 - vx) * h * 0.58f;
                if (inFlap) c = flap;
                if (Mathf.Abs(fy - (1 - vx) * h * 0.58f) < 1.2f) c = edge;
                // 아래쪽 V 접힘선
                if (Mathf.Abs(y - vx * h * 0.42f) < 0.8f && !inFlap) c = Color.Lerp(c, edge, 0.5f);
                if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) c = edge;
                // 하트 봉인
                float hx = (x - w / 2f) / 9f, hy = (y - h * 0.42f) / 9f;
                float hv = Mathf.Pow(hx * hx + hy * hy - 1, 3) - hx * hx * hy * hy * hy;
                if (hv < 0) c = seal;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    static Texture2D WoodTex()
    {
        int w = 256, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float g = Mathf.Sin(y * 0.35f + Mathf.Sin(x * Mathf.PI * 2f / w * 3f) * 2.5f) * 0.5f + 0.5f;   // 가로로 이어 붙여도 이음새가 안 보이게
                float plank = (y % 32) < 1 ? 0.6f : 1f;
                var c = Color.Lerp(new Color(0.55f, 0.34f, 0.2f), new Color(0.66f, 0.43f, 0.27f), g) * plank;
                c.a = 1; t.SetPixel(x, y, c);
            }
        t.Apply(); return t;
    }

    static Texture2D PaperTex()
    {
        int w = 8, h = 40;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, y == 0 ? new Color(0.82f, 0.86f, 0.93f) : new Color(1f, 0.985f, 0.94f));
        t.Apply(); return t;
    }

    // ===== 데이터 =====
    void PollLetters()
    {
        if (net == null || Time.unscaledTime < nextLetterPoll) return;
        nextLetterPoll = Time.unscaledTime + 0.7f;
        if (readLetters == null) readLetters = new HashSet<string>(PlayerPrefs.GetString("ac_letters_read", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        var node = net.Get("letters");
        var alive = new HashSet<string>();
        if (node != null)
            foreach (var kv in node)
            {
                if (!(kv.Value is Dictionary<string, object> d)) continue;
                alive.Add(kv.Key);
                if (knownLetters.Contains(kv.Key)) continue;
                knownLetters.Add(kv.Key);
                var l = new Letter
                {
                    id = kv.Key, from = AcJson.S(d, "from"), fromName = AcJson.S(d, "fromName"), to = AcJson.S(d, "to"), toName = AcJson.S(d, "toName"),
                    title = AcJson.S(d, "title"), body = AcJson.S(d, "body"), ct = AcJson.D(d, "ct"),
                };
                if (l.to != "all" && l.to != myKey && l.from != myKey) continue;   // 남의 비밀 편지는 안 보임
                letters.Add(l);
                if (lettersPrimed && l.from != myKey)
                {
                    if (l.to == myKey) { Toast(l.fromName + " 님이 비밀 편지를 보냈어요! 입구 우편함이나 거실 벽난로 편지함에서 읽어 보세요"); Log("[편지] " + l.fromName + " 님의 비밀 편지가 도착했어요"); }
                    else { Toast(l.fromName + " 님이 모두가 보라고 편지를 썼어요 (입구 우편함/벽난로 편지함)"); Log("[편지] " + l.fromName + " 님의 공개 편지: " + l.title); }
                }
            }
        letters.RemoveAll(l => !alive.Contains(l.id));
        letters.Sort((a, b) => b.ct.CompareTo(a.ct));
        lettersPrimed = true;
    }

    int UnreadLetters => readLetters == null ? 0 : letters.Count(l => l.from != myKey && !readLetters.Contains(l.id));

    void MarkRead(Letter l)
    {
        if (readLetters == null || readLetters.Contains(l.id)) return;
        readLetters.Add(l.id);
        PlayerPrefs.SetString("ac_letters_read", string.Join(",", readLetters.Take(400))); PlayerPrefs.Save();
    }

    void SendLetter()
    {
        if (net == null) return;
        var ppl = Recipients();
        var (toId, toNameRaw) = ppl[Mathf.Clamp(writeTo, 0, ppl.Count - 1)];
        string toName = toNameRaw.Replace("  (접속 중)", "");
        string title = writeTitle.Trim(), body = writeBody.Trim();
        if (title == "") { Toast("제목을 적어 주세요"); return; }
        if (body == "") { Toast("내용을 적어 주세요"); return; }
        string id = "l" + Guid.NewGuid().ToString("N").Substring(0, 10);
        string json = "{\"from\":" + AcJson.Esc(myKey) + ",\"fromName\":" + AcJson.Esc(uiName.Trim()) + ",\"to\":" + AcJson.Esc(toId) + ",\"toName\":" + AcJson.Esc(toName) +
                      ",\"title\":" + AcJson.Esc(Short(title, 40)) + ",\"body\":" + AcJson.Esc(body.Length > 1500 ? body.Substring(0, 1500) : body) + ",\"ct\":" + AcSync.UnixMs.ToString("0") + "}";
        net.Put("letters/" + id, json);
        writeTitle = ""; writeBody = "";
        letterTab = toId == "all" ? 1 : 2;
        nextLetterPoll = 0;
        Toast(toId == "all" ? "공개 편지를 테이블에 올려 두었어요" : toName + " 님에게 비밀 편지를 보냈어요");
    }

    // 받는 사람: 한 번이라도 들어왔던 사람 모두 (이름으로 구분해서, 다음에 들어와도 편지가 남아 있음)
    List<(string id, string name)> Recipients()
    {
        var l = new List<(string, string)> { ("all", "모두에게 (공개)") };
        var online = new HashSet<string>();
        foreach (var r in remotes.Values) if (r.net != null) online.Add(NameKey(r.net.name));
        var seen = new HashSet<string> { myKey, "all" };
        var mem = net != null ? net.Get("members") : null;
        var all = new List<(string key, string name, bool on)>();
        foreach (var r in remotes.Values) if (r.net != null && seen.Add(NameKey(r.net.name))) all.Add((NameKey(r.net.name), r.net.name, true));
        if (mem != null)
            foreach (var kv in mem)
                if (kv.Value is Dictionary<string, object> d && seen.Add(kv.Key)) all.Add((kv.Key, AcJson.S(d, "name"), online.Contains(kv.Key)));
        foreach (var m in all.OrderBy(m => m.on ? 0 : 1).ThenBy(m => m.name)) l.Add((m.key, m.name + (m.on ? "  (접속 중)" : "")));
        return l;
    }

    void OpenLetters() { lettersOpen = true; openLetter = null; letterTab = UnreadMine() > 0 ? 0 : UnreadPublic() > 0 ? 1 : 0; walkTarget = null; followId = null; nextLetterPoll = 0; }
    int UnreadMine() => readLetters == null ? 0 : letters.Count(l => l.to == myKey && l.from != myKey && !readLetters.Contains(l.id));
    int UnreadPublic() => readLetters == null ? 0 : letters.Count(l => l.to == "all" && l.from != myKey && !readLetters.Contains(l.id));

    // ===== 화면 =====
    void LettersWindow()
    {
        LetterStyles();
        R(new Rect(0, 0, SW, SH));
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.1f, 0.07f, 0.05f, 0.45f)));
        float pad = SW < 640 ? 8 : 30;
        var table = new Rect(pad, pad, SW - pad * 2, SH - pad * 2);
        GUI.DrawTextureWithTexCoords(table, woodTex, new Rect(0, 0, table.width / 256f, table.height / 128f));
        GUI.DrawTexture(new Rect(table.x, table.y, table.width, 4), AcPalette.Tex(new Color(0.35f, 0.2f, 0.12f)));
        // 위쪽 탭
        string[] tabs = { $"나에게 온 편지{Badge(UnreadMine())}", $"공개 편지{Badge(UnreadPublic())}", "보낸 편지", "편지 쓰기" };
        bool narrow = SW < 640;
        float tx = table.x + 12, ty = table.y + 12, tw = narrow ? (table.width - 24 - 70) / 2 - 4 : 150, th = 32;
        for (int i = 0; i < tabs.Length; i++)
        {
            var r = narrow ? new Rect(tx + (i % 2) * (tw + 6), ty + (i / 2) * (th + 6), tw, th) : new Rect(tx + i * (tw + 6), ty, tw, th);
            if (i == letterTab && openLetter == null) AcUiTheme.Ring(r, AcUiTheme.Gold, 3);
            if (GUI.Button(r, tabs[i], sBtn)) { letterTab = i; openLetter = null; pickTo = false; }
        }
        if (GUI.Button(new Rect(table.xMax - 82, ty, 70, th), "나오기", sBtn) || (Key("esc") && !pickTo)) { lettersOpen = false; pickTo = false; openLetter = null; return; }
        float top = ty + (narrow ? 2 * (th + 6) : th + 10) + 4;
        var area = new Rect(table.x + 12, top, table.width - 24, table.yMax - top - 12);
        if (openLetter != null) ReadLetterView(area);
        else if (letterTab == 3) WriteLetterView(area);
        else EnvelopeGrid(area);
    }

    static string Badge(int n) => n > 0 ? $" <color=#d03030>({n})</color>" : "";

    void EnvelopeGrid(Rect area)
    {
        var list = letterTab == 0 ? letters.Where(l => l.to == myKey && l.from != myKey).ToList()
                 : letterTab == 1 ? letters.Where(l => l.to == "all").ToList()
                 : letters.Where(l => l.from == myKey).ToList();
        if (list.Count == 0)
        {
            string msg = letterTab == 0 ? "아직 나에게 온 편지가 없어요" : letterTab == 1 ? "아직 공개 편지가 없어요. 첫 편지를 써 보세요!" : "아직 보낸 편지가 없어요";
            // 글씨가 커서 두 줄이 되면 잘리던 문제 → 넉넉한 높이 + 좁은 화면은 작은 글씨
            var ms = new GUIStyle(sHandTitle) { fontSize = SW < 640 ? 30 : 38, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(1f, 0.95f, 0.85f) } };
            float mh = Mathf.Max(48, ms.CalcHeight(new GUIContent(msg), area.width - 20) + 8);
            GUI.Label(new Rect(area.x + 10, area.y + 24, area.width - 20, mh), msg, ms);
            if (GUI.Button(new Rect(area.center.x - 70, area.y + 24 + mh + 18, 140, 36), "편지 쓰기", sBtn)) letterTab = 3;
            return;
        }
        float cw = SW < 640 ? 160 : 200, ch = SW < 640 ? 185 : 215, gap = 16;
        int cols = Mathf.Max(1, (int)((area.width + gap) / (cw + gap)));
        float startX = area.x + (area.width - (cols * (cw + gap) - gap)) / 2;
        int rows = (list.Count + cols - 1) / cols;
        float contentH = rows * (ch + gap);
        DragScrollArea("letters", area, ref letterScroll);
        letterScroll.y = Mathf.Clamp(letterScroll.y, 0, Mathf.Max(0, contentH - area.height));
        GUI.BeginGroup(area);
        for (int i = 0; i < list.Count; i++)
        {
            var l = list[i];
            int r = i / cols, c = i % cols;
            float x = startX - area.x + c * (cw + gap), y = r * (ch + gap) - letterScroll.y;
            if (y + ch < 0 || y > area.height) continue;
            bool unread = l.from != myKey && readLetters != null && !readLetters.Contains(l.id);
            float tilt = ((l.id.GetHashCode() & 7) - 3.5f) * 1.2f;
            var er = new Rect(x + 8, y + 6, cw - 16, (cw - 16) * 0.66f);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(tilt, er.center);
            GUI.DrawTexture(new Rect(er.x + 3, er.y + 4, er.width, er.height), AcPalette.Tex(new Color(0, 0, 0, 0.25f)));
            GUI.DrawTexture(er, unread ? envTexNew : envTex);
            GUI.matrix = m;
            if (unread) GUI.Label(new Rect(er.xMax - 40, er.y - 6, 50, 22), "<color=#ffffff><b> 새 편지 </b></color>", new GUIStyle(sSmall) { normal = { background = AcPalette.Tex(new Color(0.85f, 0.2f, 0.25f)) }, fontSize = 11 });
            var paperR = new Rect(x, er.yMax + 6, cw, ch - er.height - 14);
            GUI.DrawTexture(paperR, AcPalette.Tex(new Color(1f, 0.97f, 0.9f, 0.92f)));
            string who = letterTab == 2 ? "To. " + (l.to == "all" ? "모두" : l.toName) : "From. " + l.fromName;
            GUI.Label(new Rect(paperR.x + 4, paperR.y + 2, paperR.width - 8, 30), who, sHandSmall);
            GUI.Label(new Rect(paperR.x + 4, paperR.y + 30, paperR.width - 8, 32), Short(l.title, 12), new GUIStyle(sHandSmall) { fontSize = 27, normal = { textColor = new Color(0.6f, 0.18f, 0.2f) } });
            if (GUI.Button(new Rect(x, y, cw, ch), "", GUIStyle.none) && !Dragging) { openLetter = l; readScroll = Vector2.zero; MarkRead(l); }
        }
        GUI.EndGroup();
    }

    void ReadLetterView(Rect area)
    {
        var l = openLetter;
        float w = Mathf.Min(620, area.width), x = area.center.x - w / 2;
        var paper = new Rect(x, area.y, w, area.height);
        GUI.DrawTexture(new Rect(paper.x + 5, paper.y + 6, paper.width, paper.height), AcPalette.Tex(new Color(0, 0, 0, 0.25f)));
        GUI.DrawTextureWithTexCoords(paper, paperTex, new Rect(0, 0, paper.width / 8f, paper.height / 40f));
        GUI.DrawTexture(new Rect(paper.x, paper.y, 6, paper.height), AcPalette.Tex(new Color(0.9f, 0.5f, 0.5f)));
        float ix = paper.x + 26, iw = paper.width - 46;
        GUI.Label(new Rect(ix, paper.y + 10, iw, 48), l.title, sHandTitle);
        string to = l.to == "all" ? "모두에게" : l.toName;
        GUI.Label(new Rect(ix, paper.y + 60, iw, 34), "To. " + to + (l.to == "all" ? "  (공개 편지)" : "  (비밀 편지)"), new GUIStyle(sHand) { fontSize = 25 });
        var bodyR = new Rect(ix, paper.y + 100, iw, paper.height - 100 - 56);
        float bh = sHand.CalcHeight(new GUIContent(l.body), iw - 10);
        DragScrollArea("read", bodyR, ref readScroll);
        readScroll.y = Mathf.Clamp(readScroll.y, 0, Mathf.Max(0, bh - bodyR.height + 20));
        GUI.BeginGroup(bodyR);
        GUI.Label(new Rect(0, -readScroll.y, iw - 10, bh + 10), l.body, sHand);
        GUI.EndGroup();
        GUI.Label(new Rect(ix, paper.yMax - 50, iw - 190, 30), "From. " + l.fromName + "   " + Ago(l.ct), new GUIStyle(sHand) { fontSize = 22 });
        if (GUI.Button(new Rect(paper.xMax - 96, paper.yMax - 46, 80, 32), "닫기", sBtn)) openLetter = null;
        if (l.from != myKey && GUI.Button(new Rect(paper.xMax - 182, paper.yMax - 46, 80, 32), "답장", sBtn))
        {
            var ppl = Recipients();
            int idx = ppl.FindIndex(p => p.id == l.from);
            writeTo = idx >= 0 ? idx : 0;
            writeTitle = "Re: " + l.title; openLetter = null; letterTab = 3;
            if (idx < 0) Toast(l.fromName + " 님을 목록에서 못 찾아서 공개 편지로 써요");
        }
    }

    void WriteLetterView(Rect area)
    {
        float w = Mathf.Min(620, area.width), x = area.center.x - w / 2;
        var paper = new Rect(x, area.y, w, area.height);
        GUI.DrawTexture(new Rect(paper.x + 5, paper.y + 6, paper.width, paper.height), AcPalette.Tex(new Color(0, 0, 0, 0.25f)));
        GUI.DrawTextureWithTexCoords(paper, paperTex, new Rect(0, 0, paper.width / 8f, paper.height / 40f));
        float ix = paper.x + 20, iw = paper.width - 40, y = paper.y + 12;
        var ppl = Recipients();
        writeTo = Mathf.Clamp(writeTo, 0, ppl.Count - 1);
        bool en = GUI.enabled; if (pickTo) GUI.enabled = false;   // 목록이 떠 있으면 뒤 버튼은 눌리지 않게
        GUI.Label(new Rect(ix, y + 4, 80, 30), "받는 사람", sSmall);
        if (GUI.Button(new Rect(ix + 84, y, 34, 32), "<", sBtn)) writeTo = Wrap(writeTo - 1, ppl.Count);
        // 받는 사람 칸을 누르면 목록에서 바로 고르기 (한 번이라도 들어왔던 사람 모두, 지금 접속 중인 사람이 위)
        var toR = new Rect(ix + 122, y, iw - 122 - 40, 32);
        GUI.DrawTexture(toR, AcPalette.Tex(new Color(1f, 0.93f, 0.82f)));
        GUI.Label(new Rect(ix + 130, y + 1, iw - 122 - 50 - 40, 32), ppl[writeTo].name, new GUIStyle(sHand) { fontSize = 24, wordWrap = false, clipping = TextClipping.Clip });
        GUI.Label(new Rect(toR.xMax - 46, y + 6, 44, 22), $"<size=11>{ppl.Count - 1}명</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight });
        if (GUI.Button(toR, "", GUIStyle.none)) { pickTo = true; pickScroll = Vector2.zero; }
        if (GUI.Button(new Rect(ix + iw - 34, y, 34, 32), ">", sBtn)) writeTo = Wrap(writeTo + 1, ppl.Count);
        y += 38;
        GUI.Label(new Rect(ix, y, iw, 22), writeTo == 0 ? "<size=12>공개 편지: 테이블에 올려 두면 모두 읽을 수 있고, 모두에게 알림이 가요</size>"
                                                       : "<size=12>비밀 편지: 받는 사람만 읽을 수 있고, 지금 없어도 다음에 들어오면 받아요</size>", sSmall);
        y += 26;
        GUI.Label(new Rect(ix, y + 6, 50, 30), "제목", sSmall);
        var titleR = new Rect(ix + 54, y, iw - 54, 38);
        if (Web) { if (GUI.Button(titleR, writeTitle == "" ? "눌러서 제목 쓰기" : writeTitle, sHandField)) Ask("편지 제목", writeTitle, false, t => writeTitle = Short(t, 40)); }
        else writeTitle = GUI.TextField(titleR, writeTitle, 40, sHandField);
        y += 46;
        var bodyR = new Rect(ix, y, iw, paper.yMax - y - 56);
        if (Web)
        {
            if (GUI.Button(bodyR, "", sHandArea)) Ask("편지 내용", writeBody, true, t => writeBody = t);
            GUI.Label(new Rect(bodyR.x + 8, bodyR.y + 6, bodyR.width - 16, bodyR.height - 12), writeBody == "" ? "눌러서 내용 쓰기" : writeBody, sHand);
        }
        else writeBody = GUI.TextArea(bodyR, writeBody, 1500, sHandArea);
        GUI.Label(new Rect(ix, paper.yMax - 46, iw - 120, 30), "From. " + uiName.Trim(), new GUIStyle(sHand) { fontSize = 22 });
        if (GUI.Button(new Rect(paper.xMax - 116, paper.yMax - 46, 100, 34), "<b>보내기</b>", sBtn)) SendLetter();
        GUI.enabled = en;
        if (pickTo) RecipientPicker(ppl, area);
    }

    bool pickTo; Vector2 pickScroll;
    void RecipientPicker(List<(string id, string name)> ppl, Rect area)
    {
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.08f, 0.06f, 0.12f, 0.45f)));
        float w = Mathf.Min(360, SW - 30), rowH = 44;
        float listH = Mathf.Min(ppl.Count * (rowH + 6), SH - 220);
        var p = new Rect(SW / 2 - w / 2, Mathf.Max(30, SH / 2 - (listH + 110) / 2), w, listH + 110);
        GUI.Box(R(p), "", sBox);
        GUI.Label(new Rect(p.x + 16, p.y + 12, p.width - 32, 26), "<b>받는 사람 고르기</b>", sLabel);
        GUI.Label(new Rect(p.x + 16, p.y + 38, p.width - 32, 20), "<size=12>한 번이라도 들어왔던 사람에게 남길 수 있어요. 다음에 들어오면 받아요</size>", sSmall);
        var view = new Rect(p.x + 12, p.y + 64, p.width - 24, listH);
        float contentH = ppl.Count * (rowH + 6);
        DragScrollArea("pickTo", view, ref pickScroll);
        pickScroll.y = Mathf.Clamp(pickScroll.y, 0, Mathf.Max(0, contentH - view.height));
        GUI.BeginGroup(view);
        for (int i = 0; i < ppl.Count; i++)
        {
            var r = new Rect(0, i * (rowH + 6) - pickScroll.y, view.width, rowH);
            if (r.yMax < 0 || r.y > view.height) continue;
            if (i == writeTo) AcUiTheme.Ring(r, AcUiTheme.Accent, 2);
            if (GUI.Button(r, (i == 0 ? "<b>" : "") + ppl[i].name + (i == 0 ? "</b>" : ""), sBtn)) { writeTo = i; pickTo = false; }
        }
        GUI.EndGroup();
        if (GUI.Button(new Rect(p.center.x - 60, p.yMax - 40, 120, 32), "닫기", sBtn) || Key("esc")) pickTo = false;
    }
}
