// AcCandy.cs - 놀이방 미니게임 '크리스마스 캔디 팡' (같은 모양 3개 맞추기)
//  · 8x8 판, 여섯 가지 사탕(페퍼민트·트리·별·눈꽃 보석·선물 상자·진저브레드) + 눈덩이 블록(1~2번 맞아야 깨짐)
//  · 4개 한 줄 → 줄무늬(가로/세로 한 줄), ㄱ·T자 → 포장 사탕(주변 폭발), 5개 한 줄 → 반짝 방울(같은 색 모두)
//  · 특수끼리 바꾸면 조합 효과, 목표(모양별 개수)를 정해진 횟수 안에 모으기. 다 모으면 남은 횟수가 줄무늬로 변해 터지는 보너스
//  · 그림: Resources/Candy (Tools/candy/make_sprites.py), 소리: Sfx/candy_* (Tools/audio/candy.py)
//  · 점수: orn/x~candy~<이름>~<시각> = {name, best, look, t, clear} → 명예의 전당
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AcRoom
{
    // ===== 상태 =====
    bool candyOpen; enum CdS { Menu, Play, Result, Board } CdS cds; float cdsT;
    enum CdP { Idle, Swap, Back, Pop, Fall, Shuffle, Banner } CdP cdp; float cdpT;
    const int CW = 8, CH = 8, CdKinds = 6, CdMovesStart = 25, CdSnowCount = 8;
    const int SpNone = 0, SpH = 1, SpV = 2, SpWrap = 3, SpBomb = 4, SpSnow = 5;
    class CT { public int c, sp, hp, id; public float x, y, vy, pop = -1, squash, born = 1, ox, oy; public bool dying; }
    readonly CT[,] cdB = new CT[CW, CH];
    int cdMoves, cdScore, cdCombo, cdIdSeq; float cdShown;
    readonly List<int> cdGoalKind = new List<int>(); readonly List<int> cdGoalLeft = new List<int>(); readonly List<int> cdGoalStart = new List<int>(); float[] cdGoalPop = new float[5];
    bool cdBonus, cdWin, cdSaved, cdNewBest; int cdStars, cdPrevBest;
    System.Random cdRnd = new System.Random();
    Vector2Int cdA = new Vector2Int(-1, -1), cdB2 = new Vector2Int(-1, -1), cdDown = new Vector2Int(-1, -1), cdSel = new Vector2Int(-1, -1);
    Vector2 cdDownPos; float cdIdle; (Vector2Int a, Vector2Int b)? cdHint;
    string cdBannerText; float cdShake; int cdPopFrame; int cdCamMask = -1;
    Rect cdBoardR; float cdCell; readonly Rect[] cdGoalRects = new Rect[5];
    static readonly Color[] CdCol = { new Color(0.95f, 0.25f, 0.3f), new Color(0.3f, 0.82f, 0.38f), new Color(1f, 0.84f, 0.2f), new Color(0.35f, 0.65f, 1f), new Color(0.72f, 0.42f, 0.95f), new Color(0.92f, 0.58f, 0.3f), new Color(0.85f, 0.93f, 1f) };
    static readonly string[] CdKindKo = { "페퍼민트", "트리", "별", "눈꽃", "선물", "진저브레드", "눈덩이" };

    // 효과
    struct CdPart { public Vector2 p, v; public float life, max, size, rot, vr; public Color c; public int kind; }
    readonly List<CdPart> cdParts = new List<CdPart>();
    readonly List<(bool row, int idx, Color c, float t)> cdBeams = new List<(bool, int, Color, float)>();
    readonly List<(Vector2 p, float maxR, Color c, float t)> cdRings = new List<(Vector2, float, Color, float)>();
    readonly List<(Vector2 a, Vector2 b, float t)> cdBolts = new List<(Vector2, Vector2, float)>();
    readonly List<(Vector2 p, string s, Color c, float t, int size)> cdPops = new List<(Vector2, string, Color, float, int)>();
    readonly List<(int kind, Vector2 a, Vector2 b, float t, int goal)> cdFlies = new List<(int, Vector2, Vector2, float, int)>();
    (string s, float t) cdComboText = ("", 9);
    float cdSfxPopT, cdSfxCollectT;

    // ===== 그림 =====
    readonly Dictionary<string, Texture2D> cdTex = new Dictionary<string, Texture2D>();
    Texture2D CTx(string n) { if (!cdTex.TryGetValue(n, out var t)) { t = Resources.Load<Texture2D>("Candy/" + n); cdTex[n] = t; } return t; }
    GUIStyle cdPanelStyle;
    Texture2D TileTex(CT t)
    {
        if (t.sp == SpSnow) return CTx(t.hp >= 2 ? "snow_2" : "snow_1");
        if (t.sp == SpBomb) return CTx("bomb");
        string suf = t.sp == SpH ? "_h" : t.sp == SpV ? "_v" : t.sp == SpWrap ? "_w" : "";
        return CTx("piece_" + t.c + suf);
    }
    Texture2D KindTex(int k) => k == 6 ? CTx("snow_2") : CTx("piece_" + k);
    void DT(Rect r, Texture t, Color c) { if (t == null || Event.current.type != EventType.Repaint) return; var k = GUI.color; GUI.color = c.linear; GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); GUI.color = k; }
    void Panel(Rect r, Color c)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (cdPanelStyle == null) cdPanelStyle = new GUIStyle { normal = { background = CTx("panel") }, border = new RectOffset(30, 30, 30, 30) };
        var k = GUI.color; GUI.color = c.linear; cdPanelStyle.Draw(r, false, false, false, false); GUI.color = k;
    }

    // ===== 열기·닫기 =====
    bool InPlayroom => phase == Phase.Play && !Outside && AreaAt(me.transform.position) == "놀이방";
    void OpenCandy()
    {
        candyOpen = true; cds = CdS.Menu; cdsT = 0; walkTarget = null; decorating = false;
        if (cam) { cdCamMask = cam.cullingMask; cam.cullingMask = 0; }   // 게임 중엔 뒤 3D 장면을 그리지 않음
        net?.Get("candy");
    }
    void CloseCandy()
    {
        candyOpen = false; cdParts.Clear(); cdBeams.Clear(); cdRings.Clear(); cdBolts.Clear(); cdPops.Clear(); cdFlies.Clear();
        if (cam && cdCamMask != -1) cam.cullingMask = cdCamMask;
    }

    // ===== 새 판 =====
    void CdNewGame()
    {
        cdRnd = new System.Random(Environment.TickCount);
        cdMoves = CdMovesStart; cdScore = 0; cdShown = 0; cdCombo = 0; cdBonus = false; cdWin = false; cdSaved = false; cdNewBest = false;
        cdPrevBest = CdMyBest();
        cdParts.Clear(); cdBeams.Clear(); cdRings.Clear(); cdBolts.Clear(); cdPops.Clear(); cdFlies.Clear();
        // 목표: 사탕 세 가지 + 눈덩이
        var kinds = Enumerable.Range(0, CdKinds).OrderBy(_ => cdRnd.Next()).Take(3).ToList();
        cdGoalKind.Clear(); cdGoalLeft.Clear(); cdGoalStart.Clear();
        foreach (var k in kinds) { int n = 18 + cdRnd.Next(4) * 3; cdGoalKind.Add(k); cdGoalLeft.Add(n); cdGoalStart.Add(n); }
        cdGoalKind.Add(6); cdGoalLeft.Add(CdSnowCount); cdGoalStart.Add(CdSnowCount);
        cdGoalPop = new float[cdGoalKind.Count];
        for (int tries = 0; tries < 50; tries++)
        {
            for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
                {
                    int c; int guard = 0;
                    do { c = cdRnd.Next(CdKinds); guard++; }
                    while (guard < 30 && ((x >= 2 && Mc(x - 1, y) == c && Mc(x - 2, y) == c) || (y >= 2 && Mc(x, y - 1) == c && Mc(x, y - 2) == c)));
                    cdB[x, y] = NewT(c, x, y);
                }
            // 눈덩이: 아래쪽 절반에 흩어 놓기 (1~2번 맞아야 깨짐)
            int placed = 0;
            while (placed < CdSnowCount)
            {
                int x = cdRnd.Next(CW), y = 3 + cdRnd.Next(CH - 3);
                if (cdB[x, y].sp == SpSnow) continue;
                cdB[x, y].sp = SpSnow; cdB[x, y].c = -2; cdB[x, y].hp = 1 + cdRnd.Next(2); placed++;
            }
            if (CdFindMove() != null) break;
        }
        // 시작 연출: 위에서 차례로 떨어짐
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) { var t = cdB[x, y]; t.y = y - CH - 1.5f - (CW - x) * 0.12f; t.vy = 0; }
        cdp = CdP.Fall; cdpT = 0; cdIdle = 0; cdHint = null; cdSel = cdDown = new Vector2Int(-1, -1);
        cds = CdS.Play; cdsT = 0;
    }
    CT NewT(int c, int x, int y) => new CT { c = c, sp = SpNone, id = ++cdIdSeq, x = x, y = y };
    int Mc(int x, int y) { if (x < 0 || y < 0 || x >= CW || y >= CH) return -9; var t = cdB[x, y]; if (t == null || t.sp == SpSnow || t.sp == SpBomb) return -9; return t.c; }
    bool In(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < CW && p.y < CH;

    // ===== 맞춤 찾기 =====
    class CdGroup { public HashSet<Vector2Int> cells = new HashSet<Vector2Int>(); public int color, maxRun; public bool h, v; public List<List<Vector2Int>> runs = new List<List<Vector2Int>>(); }
    List<CdGroup> CdFindGroups()
    {
        var runs = new List<(List<Vector2Int> cells, bool h)>();
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW;)
            {
                int c = Mc(x, y); int e = x + 1;
                while (e < CW && c >= 0 && Mc(e, y) == c) e++;
                if (c >= 0 && e - x >= 3) runs.Add((Enumerable.Range(x, e - x).Select(i => new Vector2Int(i, y)).ToList(), true));
                x = e;
            }
        for (int x = 0; x < CW; x++)
            for (int y = 0; y < CH;)
            {
                int c = Mc(x, y); int e = y + 1;
                while (e < CH && c >= 0 && Mc(x, e) == c) e++;
                if (c >= 0 && e - y >= 3) runs.Add((Enumerable.Range(y, e - y).Select(i => new Vector2Int(x, i)).ToList(), false));
                y = e;
            }
        var groups = new List<CdGroup>();
        foreach (var r in runs)
        {
            var hit = groups.Where(g => g.color == Mc(r.cells[0].x, r.cells[0].y) && r.cells.Any(c => g.cells.Contains(c))).ToList();
            CdGroup g0;
            if (hit.Count == 0) { g0 = new CdGroup { color = Mc(r.cells[0].x, r.cells[0].y) }; groups.Add(g0); }
            else { g0 = hit[0]; for (int i = 1; i < hit.Count; i++) { foreach (var c in hit[i].cells) g0.cells.Add(c); g0.runs.AddRange(hit[i].runs); g0.h |= hit[i].h; g0.v |= hit[i].v; g0.maxRun = Math.Max(g0.maxRun, hit[i].maxRun); groups.Remove(hit[i]); } }
            foreach (var c in r.cells) g0.cells.Add(c);
            g0.runs.Add(r.cells); g0.maxRun = Math.Max(g0.maxRun, r.cells.Count); if (r.h) g0.h = true; else g0.v = true;
        }
        return groups;
    }
    bool CdMatchAt(int x, int y)
    {
        int c = Mc(x, y); if (c < 0) return false;
        int n = 1; for (int i = x - 1; i >= 0 && Mc(i, y) == c; i--) n++; for (int i = x + 1; i < CW && Mc(i, y) == c; i++) n++;
        if (n >= 3) return true;
        n = 1; for (int i = y - 1; i >= 0 && Mc(x, i) == c; i--) n++; for (int i = y + 1; i < CH && Mc(x, i) == c; i++) n++;
        return n >= 3;
    }
    bool Swappable(CT t) => t != null && t.sp != SpSnow;
    (Vector2Int, Vector2Int)? CdFindMove()
    {
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
                foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) })
                {
                    var a = new Vector2Int(x, y); var b = a + d; if (!In(b)) continue;
                    var ta = cdB[a.x, a.y]; var tb = cdB[b.x, b.y];
                    if (!Swappable(ta) || !Swappable(tb)) continue;
                    if (ta.sp == SpBomb || tb.sp == SpBomb || (ta.sp != SpNone && tb.sp != SpNone)) return (a, b);
                    cdB[a.x, a.y] = tb; cdB[b.x, b.y] = ta;
                    bool ok = CdMatchAt(a.x, a.y) || CdMatchAt(b.x, b.y);
                    cdB[a.x, a.y] = ta; cdB[b.x, b.y] = tb;
                    if (ok) return (a, b);
                }
        return null;
    }

    // ===== 터뜨리기 =====
    Vector2 CellCenter(float x, float y) => new Vector2(cdBoardR.x + (x + 0.5f) * cdCell, cdBoardR.y + (y + 0.5f) * cdCell);
    int MostColor()
    {
        var cnt = new int[CdKinds];
        foreach (var t in cdB) if (t != null && t.sp != SpSnow && t.sp != SpBomb && t.c >= 0) cnt[t.c]++;
        int best = 0; for (int i = 1; i < CdKinds; i++) if (cnt[i] > cnt[best]) best = i; return best;
    }
    void Detonate(Vector2Int p, CT t, HashSet<Vector2Int> set, Queue<Vector2Int> q, int bombColor = -1)
    {
        var col = t.c >= 0 && t.c < CdKinds ? CdCol[t.c] : Color.white;
        void Add(int x, int y) { var v = new Vector2Int(x, y); if (In(v) && set.Add(v)) q.Enqueue(v); }
        switch (t.sp)
        {
            case SpH: for (int x = 0; x < CW; x++) Add(x, p.y); cdBeams.Add((true, p.y, col, 0)); CdSfx("candy_line", 0.6f); cdShake = Mathf.Max(cdShake, 4); break;
            case SpV: for (int y = 0; y < CH; y++) Add(p.x, y); cdBeams.Add((false, p.x, col, 0)); CdSfx("candy_line", 0.6f); cdShake = Mathf.Max(cdShake, 4); break;
            case SpWrap:
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) Add(p.x + dx, p.y + dy);
                cdRings.Add((CellCenter(p.x, p.y), cdCell * 2.2f, col, 0)); CdSfx("candy_boom", 0.75f); cdShake = Mathf.Max(cdShake, 8); break;
            case SpBomb:
                {
                    int c = bombColor >= 0 ? bombColor : MostColor();
                    for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) if (Mc(x, y) == c) { Add(x, y); cdBolts.Add((CellCenter(p.x, p.y), CellCenter(x, y), 0)); }
                    cdRings.Add((CellCenter(p.x, p.y), cdCell * 1.6f, Color.white, 0)); CdSfx("candy_magic", 0.7f); cdShake = Mathf.Max(cdShake, 6);
                }
                break;
        }
        t.sp = SpNone;   // 한 번만 터짐
    }

    // 맞춤(+ 조합으로 생긴 칸)을 터뜨림. 아무것도 없으면 false
    bool CdResolve(HashSet<Vector2Int> extra, Vector2Int? focus = null, Vector2Int? focus2 = null)
    {
        var groups = CdFindGroups();
        var set = new HashSet<Vector2Int>(); var q = new Queue<Vector2Int>();
        var make = new List<(Vector2Int p, int sp, int c)>();
        var snowHit = new Dictionary<Vector2Int, int>();
        foreach (var g in groups)
        {
            int sp = g.maxRun >= 5 ? SpBomb : (g.h && g.v) ? SpWrap : g.maxRun == 4 ? (g.runs.Any(r => r.Count == 4 && r[0].y == r[1].y) ? SpV : SpH) : SpNone;
            if (sp != SpNone)
            {
                Vector2Int at;
                if (focus.HasValue && g.cells.Contains(focus.Value)) at = focus.Value;
                else if (focus2.HasValue && g.cells.Contains(focus2.Value)) at = focus2.Value;
                else if (sp == SpWrap) { var hc = g.runs.Where(r => r[0].y == r[1].y).SelectMany(r => r); var vc = g.runs.Where(r => r[0].x == r[1].x).SelectMany(r => r); at = hc.Intersect(vc).DefaultIfEmpty(g.cells.First()).First(); }
                else { var r = g.runs.OrderByDescending(rr => rr.Count).First(); at = r[r.Count / 2]; }
                make.Add((at, sp, sp == SpBomb ? -1 : g.color));
            }
            foreach (var c in g.cells) { if (set.Add(c)) q.Enqueue(c); }
            // 옆 눈덩이 한 번씩 맞음
            foreach (var c in g.cells)
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d; if (!In(n)) continue; var nt = cdB[n.x, n.y];
                    if (nt != null && nt.sp == SpSnow) snowHit[n] = 1;
                }
            // 점수 + 글자
            int gain = g.cells.Count * 60 * Math.Max(1, cdCombo + 1);
            cdScore += gain;
            var ctr = Vector2.zero; foreach (var c in g.cells) ctr += CellCenter(c.x, c.y); ctr /= g.cells.Count;
            cdPops.Add((ctr, "+" + gain, CdCol[g.color], 0, 22));
        }
        if (extra != null) foreach (var c in extra) if (In(c) && set.Add(c)) q.Enqueue(c);
        // 특수 사탕 연쇄
        while (q.Count > 0)
        {
            var p = q.Dequeue(); var t = cdB[p.x, p.y]; if (t == null) continue;
            if (t.sp == SpSnow) { snowHit[p] = 1; continue; }
            if (t.sp == SpH || t.sp == SpV || t.sp == SpWrap || t.sp == SpBomb) { cdScore += 200; Detonate(p, t, set, q); }
        }
        // 눈덩이
        foreach (var kv in snowHit)
        {
            var t = cdB[kv.Key.x, kv.Key.y]; if (t == null || t.sp != SpSnow) continue;
            t.hp -= 1; t.squash = 0.3f;
            CdBurst(CellCenter(kv.Key.x, kv.Key.y), CdCol[6], 8, true);
            CdSfx("candy_snow", 0.7f);
            if (t.hp <= 0) set.Add(kv.Key); else set.Remove(kv.Key);
        }
        foreach (var p in set.ToList()) { var t = cdB[p.x, p.y]; if (t != null && t.sp == SpSnow && t.hp > 0) set.Remove(p); }
        if (set.Count == 0 && snowHit.Count == 0) return false;
        // 터지는 칸 표시 + 목표 모으기
        int n0 = 0;
        foreach (var p in set)
        {
            var t = cdB[p.x, p.y]; if (t == null || t.dying) continue;
            t.dying = true; t.pop = 0; n0++;
            int kind = t.sp == SpSnow ? 6 : t.c;
            if (extra != null && !groups.Any(g => g.cells.Contains(p))) cdScore += 60;
            var cc = CellCenter(p.x, p.y);
            if (kind >= 0) CdBurst(cc, CdCol[kind], kind == 6 ? 10 : 7, kind == 6);
            int gi = cdGoalKind.IndexOf(kind);
            if (gi >= 0 && cdGoalLeft[gi] > 0) { cdGoalLeft[gi]--; cdFlies.Add((kind, cc, cdGoalRects[gi].center, 0, gi)); }
        }
        // 새 특수 사탕 (터진 자리에 생김)
        foreach (var m in make)
        {
            var nt = new CT { c = m.c, sp = m.sp, id = ++cdIdSeq, x = m.p.x, y = m.p.y, born = 0 };
            cdMakeLater.Add((m.p, nt));
            cdPops.Add((CellCenter(m.p.x, m.p.y), m.sp == SpBomb ? "반짝 방울!" : m.sp == SpWrap ? "포장 사탕!" : "줄무늬!", Color.white, 0, 20));
        }
        if (make.Count > 0) CdSfx("candy_special", 0.7f);
        if (n0 > 0) CdSfx("candy_pop", 0.7f, 1f + Mathf.Min(cdCombo, 8) * 0.07f);
        cdCombo++;
        if (cdCombo == 2) cdComboText = ("좋아요!", 0); else if (cdCombo == 3) cdComboText = ("멋져요!", 0); else if (cdCombo == 4) cdComboText = ("대단해요!", 0); else if (cdCombo >= 5) cdComboText = ("환상적이에요!", 0);
        cdp = CdP.Pop; cdpT = 0;
        return true;
    }
    readonly List<(Vector2Int p, CT t)> cdMakeLater = new List<(Vector2Int, CT)>();

    void CdAfterPop()
    {
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) if (cdB[x, y] != null && cdB[x, y].dying) cdB[x, y] = null;
        foreach (var m in cdMakeLater) cdB[m.p.x, m.p.y] = m.t;
        cdMakeLater.Clear();
        // 아래로 떨어지기 + 위에서 새 사탕
        for (int x = 0; x < CW; x++)
        {
            int w = CH - 1;
            for (int y = CH - 1; y >= 0; y--) { var t = cdB[x, y]; if (t == null) continue; cdB[x, y] = null; cdB[x, w] = t; t.x = x; w--; }
            int k = 0;
            for (int y = w; y >= 0; y--) { var t = NewT(cdRnd.Next(CdKinds), x, y); t.y = -1 - k - 0.25f; t.vy = 2f; cdB[x, y] = t; k++; }
        }
        cdp = CdP.Fall; cdpT = 0;
    }

    void CdSettle()
    {
        cdCombo = 0;
        bool goalsDone = cdGoalLeft.All(n => n <= 0);
        if (goalsDone && !cdBonus) { cdBonus = true; cdBannerText = "목표 달성!"; cdp = CdP.Banner; cdpT = 0; CdSfx("candy_win", 0.8f); return; }
        if (cdBonus)
        {
            if (cdMoves > 0)
            {
                // 남은 횟수 하나 = 사탕 하나가 줄무늬로
                var cand = new List<Vector2Int>();
                for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) { var t = cdB[x, y]; if (t != null && t.sp == SpNone && t.c >= 0) cand.Add(new Vector2Int(x, y)); }
                int n = Mathf.Min(cdMoves, Mathf.Min(4, cand.Count));
                for (int i = 0; i < n; i++)
                {
                    var p = cand[cdRnd.Next(cand.Count)]; cand.Remove(p);
                    var t = cdB[p.x, p.y]; t.sp = cdRnd.Next(2) == 0 ? SpH : SpV; t.born = 0;
                    CdBurst(CellCenter(p.x, p.y), Color.white, 6, false);
                    cdMoves--; cdScore += 500; cdPops.Add((CellCenter(p.x, p.y), "+500", new Color(1f, 0.9f, 0.5f), 0, 20));
                }
                CdSfx("candy_special", 0.6f);
                cdp = CdP.Banner; cdBannerText = null; cdpT = 0.75f;   // 잠깐 쉬었다가 다음
                return;
            }
            var specials = new HashSet<Vector2Int>();
            for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) { var t = cdB[x, y]; if (t != null && (t.sp == SpH || t.sp == SpV || t.sp == SpWrap || t.sp == SpBomb)) specials.Add(new Vector2Int(x, y)); }
            if (specials.Count > 0 && CdResolve(specials)) return;
            CdFinish(true); return;
        }
        if (cdMoves <= 0) { CdFinish(false); return; }
        if (CdFindMove() == null) { CdShuffle(); return; }
        cdp = CdP.Idle; cdIdle = 0; cdHint = null;
    }

    void CdShuffle()
    {
        var cells = new List<Vector2Int>();
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) { var t = cdB[x, y]; if (t != null && t.sp != SpSnow) cells.Add(new Vector2Int(x, y)); }
        var tiles = cells.Select(p => cdB[p.x, p.y]).ToList();
        for (int tries = 0; tries < 80; tries++)
        {
            var order = tiles.OrderBy(_ => cdRnd.Next()).ToList();
            for (int i = 0; i < cells.Count; i++) cdB[cells[i].x, cells[i].y] = order[i];
            if (CdFindGroups().Count == 0 && CdFindMove() != null) break;
        }
        foreach (var p in cells) { var t = cdB[p.x, p.y]; t.ox = t.x; t.oy = t.y; }
        cdBannerText = "섞을게요!"; cdp = CdP.Shuffle; cdpT = 0; CdSfx("candy_swap", 0.8f);
    }

    void CdFinish(bool win)
    {
        cdWin = win; cdp = CdP.Banner; cdpT = 0; cdBannerText = win ? "메리 크리스마스!" : "아쉬워요!";
        if (!win) CdSfx("candy_lose", 0.7f);
        int clearBonus = win ? 3000 : 0; cdScore += clearBonus;
        cdStars = !win ? 0 : cdScore >= 30000 ? 3 : cdScore >= 18000 ? 2 : 1;
        cdNewBest = cdScore > cdPrevBest;
        if (!cdSaved && net != null && cdScore > 0)
        {
            cdSaved = true;
            net.Put("candy/" + myKey + "~" + AcSync.UnixMs.ToString("0"), "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"best\":" + cdScore + ",\"clear\":" + (win ? 1 : 0) + ",\"look\":" + AcJson.Esc(me.look.Encode()) + ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
        }
        cdFinishPending = true;
    }
    bool cdFinishPending;

    // ===== 효과 도우미 =====
    void CdSfx(string n, float v, float pitch = 1f)
    {
        if (n == "candy_pop") { if (Time.unscaledTime - cdSfxPopT < 0.05f) return; cdSfxPopT = Time.unscaledTime; }
        Sfx(n, v, pitch);
    }
    void CdBurst(Vector2 at, Color c, int n, bool snowy)
    {
        for (int i = 0; i < n; i++)
        {
            float a = (float)cdRnd.NextDouble() * Mathf.PI * 2, sp = cdCell * (2f + (float)cdRnd.NextDouble() * 4f);
            cdParts.Add(new CdPart { p = at, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp + Vector2.up * -cdCell * 2f, life = 0, max = 0.45f + (float)cdRnd.NextDouble() * 0.35f, size = cdCell * (0.18f + (float)cdRnd.NextDouble() * 0.22f), rot = a * 57f, vr = (float)cdRnd.NextDouble() * 400 - 200, c = c, kind = snowy ? 0 : (i % 2) });
        }
        cdParts.Add(new CdPart { p = at, v = Vector2.zero, life = 0, max = 0.3f, size = cdCell * 1.4f, c = new Color(c.r, c.g, c.b, 0.8f), kind = 2 });
        if (cdParts.Count > 420) cdParts.RemoveRange(0, cdParts.Count - 420);
    }

    // ===== 매 프레임 =====
    void CandyUpdate(float dt)
    {
        if (!candyOpen) return;
        dt = Mathf.Min(dt, 0.05f);
        cdsT += dt; cdpT += dt;
        if (Key("esc")) { if (cds == CdS.Play) cds = CdS.Menu; else CloseCandy(); return; }
        if (cds != CdS.Play) return;
        cdShown = Mathf.MoveTowards(cdShown, cdScore, Mathf.Max(60, (cdScore - cdShown) * 6) * dt);
        for (int i = 0; i < cdGoalPop.Length; i++) cdGoalPop[i] = Mathf.Max(0, cdGoalPop[i] - dt * 3);
        cdShake = Mathf.MoveTowards(cdShake, 0, dt * 40);
        // 타일 움직임
        bool moving = false;
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
            {
                var t = cdB[x, y]; if (t == null) continue;
                if (t.born < 1) t.born = Mathf.Min(1, t.born + dt * 4);
                if (t.squash > 0) t.squash = Mathf.Max(0, t.squash - dt * 1.6f);
                if (t.dying) { t.pop += dt / 0.22f; continue; }
                if (cdp == CdP.Fall)
                {
                    if (t.y < y) { t.vy += 70f * dt; t.y += t.vy * dt; moving = true; if (t.y >= y) { t.y = y; t.squash = Mathf.Clamp(t.vy * 0.018f, 0.06f, 0.28f); t.vy = 0; } }
                    t.x = Mathf.MoveTowards(t.x, x, dt * 10);
                }
            }
        switch (cdp)
        {
            case CdP.Swap:
            case CdP.Back:
                {
                    float u = Mathf.Clamp01(cdpT / 0.16f), e = u * u * (3 - 2 * u);
                    var ta = cdB[cdA.x, cdA.y]; var tb = cdB[cdB2.x, cdB2.y];
                    Vector2 pa = cdA, pb = cdB2;
                    if (cdp == CdP.Swap) { if (ta != null) { ta.x = Mathf.Lerp(pa.x, pb.x, e); ta.y = Mathf.Lerp(pa.y, pb.y, e); } if (tb != null) { tb.x = Mathf.Lerp(pb.x, pa.x, e); tb.y = Mathf.Lerp(pb.y, pa.y, e); } }
                    else { if (ta != null) { ta.x = Mathf.Lerp(pb.x, pa.x, e); ta.y = Mathf.Lerp(pb.y, pa.y, e); } if (tb != null) { tb.x = Mathf.Lerp(pa.x, pb.x, e); tb.y = Mathf.Lerp(pa.y, pb.y, e); } }
                    if (u >= 1)
                    {
                        if (cdp == CdP.Back) { cdp = CdP.Idle; cdIdle = 0; break; }
                        cdB[cdA.x, cdA.y] = tb; cdB[cdB2.x, cdB2.y] = ta;
                        CdAfterSwap();
                    }
                }
                break;
            case CdP.Pop:
                if (cdpT >= 0.22f) CdAfterPop();
                break;
            case CdP.Fall:
                if (!moving && cdpT > 0.08f) { if (!CdResolve(null)) CdSettle(); }
                break;
            case CdP.Shuffle:
                {
                    float u = Mathf.Clamp01(cdpT / 0.5f), e = u * u * (3 - 2 * u);
                    for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) { var t = cdB[x, y]; if (t == null || t.sp == SpSnow) continue; t.x = Mathf.Lerp(t.ox, x, e); t.y = Mathf.Lerp(t.oy, y, e) - Mathf.Sin(u * Mathf.PI) * 0.4f; }
                    if (u >= 1) { cdBannerText = null; CdSettle(); }
                }
                break;
            case CdP.Banner:
                if (cdpT >= 1.3f)
                {
                    cdBannerText = null;
                    if (cdFinishPending) { cdFinishPending = false; cds = CdS.Result; cdsT = 0; if (cdWin) CdSfx("candy_win", 0.7f); }
                    else CdSettle();
                }
                break;
            case CdP.Idle:
                cdIdle += dt;
                if (cdIdle > 4.5f && cdHint == null) cdHint = CdFindMove();
                break;
        }
        // 효과들
        for (int i = cdParts.Count - 1; i >= 0; i--)
        {
            var p = cdParts[i]; p.life += dt; if (p.life >= p.max) { cdParts.RemoveAt(i); continue; }
            p.p += p.v * dt; p.v.y += cdCell * 14f * dt * (p.kind == 0 ? 0.5f : 1f); p.v *= 1 - dt * 1.5f; p.rot += p.vr * dt; cdParts[i] = p;
        }
        for (int i = cdBeams.Count - 1; i >= 0; i--) { var b = cdBeams[i]; b.t += dt; if (b.t > 0.45f) cdBeams.RemoveAt(i); else cdBeams[i] = b; }
        for (int i = cdRings.Count - 1; i >= 0; i--) { var b = cdRings[i]; b.t += dt; if (b.t > 0.5f) cdRings.RemoveAt(i); else cdRings[i] = b; }
        for (int i = cdBolts.Count - 1; i >= 0; i--) { var b = cdBolts[i]; b.t += dt; if (b.t > 0.4f) cdBolts.RemoveAt(i); else cdBolts[i] = b; }
        for (int i = cdPops.Count - 1; i >= 0; i--) { var b = cdPops[i]; b.t += dt; if (b.t > 1.0f) cdPops.RemoveAt(i); else cdPops[i] = b; }
        for (int i = cdFlies.Count - 1; i >= 0; i--)
        {
            var f = cdFlies[i]; f.t += dt / 0.65f;
            if (f.t >= 1) { if (f.goal < cdGoalPop.Length) cdGoalPop[f.goal] = 1; if (Time.unscaledTime - cdSfxCollectT > 0.06f) { cdSfxCollectT = Time.unscaledTime; Sfx("candy_collect", 0.35f, 1f + (float)cdRnd.NextDouble() * 0.2f); } cdFlies.RemoveAt(i); }
            else cdFlies[i] = f;
        }
        cdComboText.t += dt;
    }

    void CdAfterSwap()
    {
        var ta = cdB[cdA.x, cdA.y]; var tb = cdB[cdB2.x, cdB2.y];   // 바꾼 뒤: ta는 A자리에 온 것(원래 B), tb는 B자리에 온 것
        cdHint = null; cdCombo = 0;
        // 특수 조합
        if (ta.sp == SpBomb || tb.sp == SpBomb || (ta.sp != SpNone && tb.sp != SpNone))
        {
            cdMoves--;
            var extra = new HashSet<Vector2Int>();
            var A = cdA; var B = cdB2;
            if (ta.sp == SpBomb && tb.sp == SpBomb)
            {
                for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) extra.Add(new Vector2Int(x, y));
                ta.sp = tb.sp = SpNone; ta.c = tb.c = -1;
                cdRings.Add((CellCenter(B.x, B.y), cdCell * 9f, Color.white, 0)); CdSfx("candy_magic", 0.9f); CdSfx("candy_boom", 0.9f); cdShake = 14;
                cdComboText = ("크리스마스 대폭발!", 0);
            }
            else if (ta.sp == SpBomb || tb.sp == SpBomb)
            {
                var bomb = ta.sp == SpBomb ? ta : tb; var other = ta.sp == SpBomb ? tb : ta; var bp = ta.sp == SpBomb ? A : B;
                int c = other.c; int osp = other.sp;
                bomb.sp = SpNone; bomb.c = -1; extra.Add(bp);
                for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
                    {
                        var t = cdB[x, y]; if (t == null || t.c != c || t.sp == SpSnow) continue;
                        if (osp == SpH || osp == SpV) t.sp = cdRnd.Next(2) == 0 ? SpH : SpV; else if (osp == SpWrap) t.sp = SpWrap;
                        extra.Add(new Vector2Int(x, y)); cdBolts.Add((CellCenter(bp.x, bp.y), CellCenter(x, y), 0));
                    }
                cdRings.Add((CellCenter(bp.x, bp.y), cdCell * 2f, Color.white, 0)); CdSfx("candy_magic", 0.8f); cdShake = 8;
                // 방울 자체는 색이 없어 맞춤에 안 들어가므로 직접 지움
                bomb.c = c;
            }
            else
            {
                bool sa = ta.sp == SpH || ta.sp == SpV, sb = tb.sp == SpH || tb.sp == SpV;
                int r = (sa && sb) ? 0 : (ta.sp == SpWrap && tb.sp == SpWrap) ? 2 : 1;   // 0: 십자, 1: 굵은 십자(3줄), 2: 큰 폭발
                ta.sp = SpNone; tb.sp = SpNone; extra.Add(A); extra.Add(B);
                if (r == 2)
                {
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) extra.Add(new Vector2Int(B.x + dx, B.y + dy));
                    cdRings.Add((CellCenter(B.x, B.y), cdCell * 3.6f, CdCol[Mathf.Clamp(tb.c, 0, 5)], 0)); CdSfx("candy_boom", 0.9f); cdShake = 12;
                }
                else
                {
                    int w = r == 0 ? 0 : 1;
                    for (int k = -w; k <= w; k++)
                    {
                        for (int x = 0; x < CW; x++) extra.Add(new Vector2Int(x, B.y + k));
                        for (int y = 0; y < CH; y++) extra.Add(new Vector2Int(B.x + k, y));
                        if (B.y + k >= 0 && B.y + k < CH) cdBeams.Add((true, B.y + k, CdCol[Mathf.Clamp(tb.c, 0, 5)], 0));
                        if (B.x + k >= 0 && B.x + k < CW) cdBeams.Add((false, B.x + k, CdCol[Mathf.Clamp(ta.c, 0, 5)], 0));
                    }
                    CdSfx("candy_line", 0.8f); if (r == 1) CdSfx("candy_boom", 0.7f); cdShake = 10;
                }
            }
            CdResolve(extra, cdB2, cdA);
            return;
        }
        if (CdMatchAt(cdA.x, cdA.y) || CdMatchAt(cdB2.x, cdB2.y))
        {
            cdMoves--;
            CdResolve(null, cdB2, cdA);
            return;
        }
        // 안 맞으면 되돌림
        cdB[cdA.x, cdA.y] = tb; cdB[cdB2.x, cdB2.y] = ta;
        cdp = CdP.Back; cdpT = 0; CdSfx("candy_bad", 0.6f);
    }

    Vector2Int CdCellAt(Vector2 u)
    {
        if (!cdBoardR.Contains(u)) return new Vector2Int(-1, -1);
        return new Vector2Int(Mathf.Clamp((int)((u.x - cdBoardR.x) / cdCell), 0, CW - 1), Mathf.Clamp((int)((u.y - cdBoardR.y) / cdCell), 0, CH - 1));
    }
    void CdTrySwap(Vector2Int a, Vector2Int b)
    {
        if (!In(a) || !In(b) || Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) != 1) return;
        if (!Swappable(cdB[a.x, a.y]) || !Swappable(cdB[b.x, b.y])) { CdSfx("candy_bad", 0.4f); return; }
        cdA = a; cdB2 = b; cdp = CdP.Swap; cdpT = 0; cdSel = new Vector2Int(-1, -1); CdSfx("candy_swap", 0.55f);
    }
    // 손가락/마우스: IMGUI 이벤트로 받음(웹·휴대폰 모두 확실하게 들어옴). 좌표는 UI 단위
    void CdInputGUI()
    {
        var e = Event.current; if (e == null) return;
        var et = e.type;
        bool dn = et == EventType.MouseDown || et == EventType.TouchDown, mv = et == EventType.MouseDrag || et == EventType.TouchMove, up = et == EventType.MouseUp || et == EventType.TouchUp;
        if (!dn && !mv && !up) return;
        if (cds != CdS.Play) return;
        var u = e.mousePosition;
        bool idle = cdp == CdP.Idle;
        if (dn)
        {
            var c = CdCellAt(u);
            if (In(c) && Swappable(cdB[c.x, c.y])) { cdDown = c; cdDownPos = u; e.Use(); } else cdDown = new Vector2Int(-1, -1);
            return;
        }
        if (!In(cdDown)) return;
        if (mv)
        {
            var d = u - cdDownPos;
            if (d.magnitude > cdCell * 0.3f)
            {
                var dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2Int(d.x > 0 ? 1 : -1, 0) : new Vector2Int(0, d.y > 0 ? 1 : -1);
                var from = cdDown; cdDown = new Vector2Int(-1, -1);
                if (idle) CdTrySwap(from, from + dir);
            }
            e.Use();
            return;
        }
        // MouseUp = 톡 누르기: 고르기 / 옆 칸이면 바꾸기
        var cc = cdDown; cdDown = new Vector2Int(-1, -1); e.Use();
        if (!idle) return;
        if (In(cdSel) && Mathf.Abs(cdSel.x - cc.x) + Mathf.Abs(cdSel.y - cc.y) == 1) CdTrySwap(cdSel, cc);
        else if (cdSel == cc) cdSel = new Vector2Int(-1, -1);
        else { cdSel = cc; Sfx("click", 0.3f); }
    }

    // ===== 그리기 =====
    void CandyGUI()
    {
        CgTextures();
        if (Event.current.type == EventType.Layout) cgUi.Clear();
        var bg = CTx("bg");
        if (bg != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(0, 0, SW, SH), bg, ScaleMode.ScaleAndCrop, false);
        // 배경 눈
        if (Event.current.type == EventType.Repaint)
        {
            var dot = CTx("dot");
            for (int i = 0; i < 40; i++)
            {
                float sx = Mathf.Repeat(i * 97.3f + Mathf.Sin(Time.time * 0.6f + i) * 20f, SW), sy = Mathf.Repeat(i * 53.7f + Time.time * (18 + i % 7 * 6), SH);
                float s = 3 + i % 4 * 2; DT(new Rect(sx - s, sy - s, s * 2, s * 2), dot, new Color(1, 1, 1, 0.55f));
            }
        }
        switch (cds)
        {
            case CdS.Menu: CandyMenuGUI(); break;
            case CdS.Play: CandyPlayGUI(); CdInputGUI(); break;
            case CdS.Result: CandyPlayGUI(); CandyResultGUI(); break;
            case CdS.Board: CandyBoardGUI(); break;
        }
    }

    void CandyMenuGUI()
    {
        bool portrait = SH > SW;
        var tt = CTx("title");
        float tw = Mathf.Min(SW - 30, 520), th = tw * 300f / 1100f;
        float ty = portrait ? SH * 0.1f : 30;
        float bob = Mathf.Sin(Time.time * 2f) * 4f;
        if (tt != null) DT(new Rect(SW / 2 - tw / 2, ty + bob, tw, th), tt, Color.white);
        // 빙글빙글 떠다니는 사탕
        float cy = ty + th + (portrait ? 60 : 40);
        for (int i = 0; i < 6; i++)
        {
            float a = Time.time * 0.7f + i * Mathf.PI / 3;
            float s = portrait ? 54 : 46;
            var p = new Vector2(SW / 2 + Mathf.Cos(a) * Mathf.Min(SW * 0.36f, 170), cy + Mathf.Sin(a) * 34 + 30);
            DT(new Rect(p.x - s / 2 + 3, p.y - s / 2 + 6, s, s), CTx("shadow"), new Color(1, 1, 1, 0.5f));
            DT(new Rect(p.x - s / 2, p.y - s / 2, s, s), CTx("piece_" + i), Color.white);
        }
        float py = cy + (portrait ? 110 : 90);
        float pw = Mathf.Min(SW - 30, 440);
        var info = new Rect(SW / 2 - pw / 2, py, pw, portrait ? 116 : 104);
        Panel(info, new Color(1f, 0.97f, 0.93f, 0.95f));
        var st = new GUIStyle(CgStyle(portrait ? 14 : 15, TextAnchor.MiddleCenter)) { wordWrap = true, normal = { textColor = new Color(0.36f, 0.2f, 0.25f).linear } };
        GUI.Label(new Rect(info.x + 14, info.y + 8, info.width - 28, info.height - 16),
            "같은 사탕 3개를 맞춰 목표를 모아요!\n4개 한 줄: 줄무늬 사탕,  ㄱ/T 모양: 포장 사탕\n5개 한 줄: 반짝 방울\n특수 사탕끼리 바꾸면 더 크게 펑!", st);
        int best = CdMyBest();
        CgText(new Rect(0, info.yMax + 8, SW, 26), best > 0 ? $"{uiName.Trim()} 님의 최고 점수  {best:#,0}" : "첫 기록을 세워 보세요!", 17, new Color(1f, 0.92f, 0.6f));
        float bw = Mathf.Min(260, SW - 60), by = info.yMax + 46;
        if (CgButton(new Rect(SW / 2 - bw / 2, by, bw, 58), "게임 시작!", new Color(0.92f, 0.3f, 0.38f), 24)) CdNewGame();
        float sw2 = (bw - 10) / 2;
        if (CgButton(new Rect(SW / 2 - bw / 2, by + 68, sw2, 46), "명예의 전당", new Color(0.95f, 0.65f, 0.2f), 17)) { cds = CdS.Board; cdsT = 0; }
        if (CgButton(new Rect(SW / 2 - bw / 2 + sw2 + 10, by + 68, sw2, 46), "나가기", new Color(0.45f, 0.55f, 0.7f), 17)) CloseCandy();
    }

    void CdLayout(out bool portrait, out Rect hud)
    {
        portrait = SH > SW * 1.05f;
        if (portrait)
        {
            float headH = 132;
            cdCell = Mathf.Floor(Mathf.Min((SW - 44) / CW, (SH - headH - 110) / CH));
            float bw = cdCell * CW;
            float top = 14 + headH + 18 + Mathf.Max(0, (SH - 14 - headH - 18 - bw - 40) * 0.4f);
            cdBoardR = new Rect(Mathf.Round(SW / 2 - bw / 2), Mathf.Round(top), bw, bw);
            hud = new Rect(10, 12, SW - 20, headH);
        }
        else
        {
            float side = Mathf.Min(260, SW * 0.32f);
            cdCell = Mathf.Floor(Mathf.Min((SW - side - 50) / CW, (SH - 40) / CH));
            float bw = cdCell * CW;
            cdBoardR = new Rect(Mathf.Round(side + 30 + (SW - side - 30 - bw) / 2), Mathf.Round(SH / 2 - bw / 2), bw, bw);
            hud = new Rect(14, 14, side, SH - 28);
        }
    }

    void CandyPlayGUI()
    {
        CdLayout(out bool portrait, out Rect hud);
        bool rep = Event.current.type == EventType.Repaint;
        Vector2 shake = cdShake > 0 ? new Vector2(Mathf.Sin(Time.time * 70f), Mathf.Cos(Time.time * 63f)) * cdShake * 0.5f : Vector2.zero;
        var br = new Rect(cdBoardR.x + shake.x, cdBoardR.y + shake.y, cdBoardR.width, cdBoardR.height);
        // 판 바탕
        Panel(new Rect(br.x - 14, br.y - 10, br.width + 28, br.height + 28), new Color(0.12f, 0.06f, 0.2f, 0.35f));
        Panel(new Rect(br.x - 12, br.y - 12, br.width + 24, br.height + 24), new Color(0.98f, 0.94f, 1f, 0.9f));
        var cell = CTx("cell");
        for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
            {
                var c = ((x + y) & 1) == 0 ? new Color(0.78f, 0.72f, 0.95f, 0.75f) : new Color(0.9f, 0.86f, 1f, 0.75f);
                DT(new Rect(br.x + x * cdCell + 1, br.y + y * cdCell + 1, cdCell - 2, cdCell - 2), cell, c);
            }
        // 사탕 (판 밖은 잘라서 안 보이게)
        GUI.BeginGroup(br);
        var shadow = CTx("shadow");
        if (rep)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
                    {
                        var t = cdB[x, y]; if (t == null) continue;
                        if (t.y < -1.05f) continue;
                        float s = cdCell * 0.92f;
                        float scale = t.born < 1 ? Mathf.Sin(t.born * Mathf.PI * 0.5f) * (1 + Mathf.Sin(t.born * Mathf.PI) * 0.25f) : 1f;
                        float alpha = 1;
                        if (t.dying) { float p = Mathf.Clamp01(t.pop); scale = 1 + p * 0.45f; alpha = 1 - p * p; }
                        bool hinted = cdHint.HasValue && (cdHint.Value.a == new Vector2Int(x, y) || cdHint.Value.b == new Vector2Int(x, y)) && cdp == CdP.Idle;
                        if (hinted) scale *= 1 + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.12f;
                        bool sel = cdSel == new Vector2Int(x, y) && cdp == CdP.Idle;
                        float sq = t.squash * Mathf.Sin((0.3f - Mathf.Min(0.3f, t.squash)) * 30f + 1.57f);
                        float w = s * scale * (1 + sq * 0.5f), h = s * scale * (1 - sq * 0.6f);
                        float cx = (t.x + 0.5f) * cdCell, cyy = (t.y + 0.5f) * cdCell + (s - h) * 0.45f;
                        if (sel) cyy -= Mathf.Abs(Mathf.Sin(Time.time * 7f)) * cdCell * 0.08f;
                        if (pass == 0)
                        {
                            if (!t.dying) DT(new Rect(cx - w * 0.44f, (t.y + 0.5f) * cdCell + s * 0.36f - h * 0.08f, w * 0.88f, h * 0.22f), shadow, new Color(1, 1, 1, 0.55f));
                            if (sel || hinted) DT(new Rect(cx - s * 0.62f, (t.y + 0.5f) * cdCell - s * 0.62f, s * 1.24f, s * 1.24f), CTx("dot"), sel ? new Color(1f, 1f, 0.7f, 0.85f) : new Color(1f, 1f, 1f, 0.5f));
                            continue;
                        }
                        var tex = TileTex(t);
                        if (t.sp == SpBomb)
                        {
                            float pul = 1 + Mathf.Sin(Time.time * 4f + t.id) * 0.05f;   // 두근두근
                            DT(new Rect(cx - w * pul / 2, cyy - h * pul / 2, w * pul, h * pul), tex, new Color(1, 1, 1, alpha));
                            // 반짝임
                            float tw = (Time.time * 2 + t.id) % 3f;
                            if (tw < 0.6f) { float ss = s * 0.5f * Mathf.Sin(tw / 0.6f * Mathf.PI); DT(new Rect(cx + s * 0.12f - ss / 2, cyy - s * 0.2f - ss / 2, ss, ss), CTx("spark"), Color.white); }
                        }
                        else DT(new Rect(cx - w / 2, cyy - h / 2, w, h), tex, new Color(1, 1, 1, alpha));
                        if (t.dying && t.pop < 0.5f) DT(new Rect(cx - w / 2, cyy - h / 2, w, h), CTx("dot"), new Color(1, 1, 1, (0.5f - t.pop) * 1.4f));
                    }
        }
        GUI.EndGroup();
        // 줄무늬 광선
        foreach (var b in cdBeams)
        {
            float k = b.t / 0.45f, a = 1 - k, thick = cdCell * (1.3f - k * 0.6f);
            Rect r = b.row ? new Rect(br.x - cdCell * 0.5f, br.y + (b.idx + 0.5f) * cdCell - thick / 2, br.width + cdCell, thick) : new Rect(br.x + (b.idx + 0.5f) * cdCell - thick / 2, br.y - cdCell * 0.5f, thick, br.height + cdCell);
            if (b.row) { DT(r, CTx("beam"), new Color(b.c.r, b.c.g, b.c.b, a)); DT(new Rect(r.x, r.center.y - thick * 0.15f, r.width, thick * 0.3f), CTx("beam"), new Color(1, 1, 1, a)); }
            else
            {
                if (rep)
                {
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(90, r.center * UiK);
                    var rr = new Rect(r.center.x - r.height / 2, r.center.y - r.width / 2, r.height, r.width);
                    DT(rr, CTx("beam"), new Color(b.c.r, b.c.g, b.c.b, a)); DT(new Rect(rr.x, rr.center.y - thick * 0.15f, rr.width, thick * 0.3f), CTx("beam"), new Color(1, 1, 1, a));
                    GUI.matrix = m;
                }
            }
        }
        // 폭발 고리
        foreach (var g in cdRings)
        {
            float k = g.t / 0.5f, r = g.maxR * (0.25f + k * 0.85f);
            var p = g.p + shake;
            DT(new Rect(p.x - r, p.y - r, r * 2, r * 2), CTx("ring"), new Color(g.c.r, g.c.g, g.c.b, 1 - k));
            if (k < 0.4f) DT(new Rect(p.x - r * 0.6f, p.y - r * 0.6f, r * 1.2f, r * 1.2f), CTx("dot"), new Color(1, 1, 1, (0.4f - k) * 2f));
        }
        // 방울 번개
        if (rep)
            foreach (var bo in cdBolts)
            {
                var d = bo.b - bo.a; float len = d.magnitude; if (len < 1) continue;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a = 1 - bo.t / 0.4f;
                var c = (bo.a + bo.b) / 2;
                var m = GUI.matrix; GUIUtility.RotateAroundPivot(ang, c * UiK);
                DT(new Rect(c.x - len / 2, c.y - cdCell * 0.25f, len, cdCell * 0.5f), CTx("beam"), new Color(0.8f, 0.9f, 1f, a));
                DT(new Rect(c.x - len / 2, c.y - cdCell * 0.07f, len, cdCell * 0.14f), CTx("beam"), new Color(1, 1, 1, a));
                GUI.matrix = m;
            }
        // 가루·반짝이
        if (rep)
            foreach (var p in cdParts)
            {
                float k = p.life / p.max, a = 1 - k;
                if (p.kind == 2) { float r = p.size * (0.5f + k * 0.8f); DT(new Rect(p.p.x - r / 2, p.p.y - r / 2, r, r), CTx("dot"), new Color(p.c.r, p.c.g, p.c.b, p.c.a * a)); continue; }
                float s = p.size * (p.kind == 1 ? (1 - k * 0.5f) : (1 - k * 0.3f));
                var tex = p.kind == 1 ? CTx("spark") : CTx("dot");
                var col = p.kind == 1 ? Color.Lerp(Color.white, p.c, 0.4f) : p.c;
                DT(new Rect(p.p.x - s / 2, p.p.y - s / 2, s, s), tex, new Color(col.r, col.g, col.b, a));
            }
        // 점수 글자
        foreach (var pp in cdPops)
        {
            float k = pp.t, a = Mathf.Clamp01(1.4f - k * 1.4f), rise = k * 40;
            CgText(new Rect(pp.p.x - 120, pp.p.y - 14 - rise, 240, 28), pp.s, pp.size, new Color(pp.c.r, pp.c.g, pp.c.b, a) * 0.6f + new Color(1, 1, 1, a) * 0.4f, TextAnchor.MiddleCenter, 2, new Color(0.25f, 0.1f, 0.2f, a * 0.9f));
        }
        // 위쪽 정보
        CandyHUD(portrait, hud);
        // 모으는 사탕이 목표 칸으로 날아감
        foreach (var f in cdFlies)
        {
            float k = f.t, e = 1 - (1 - k) * (1 - k);
            var ctrl = (f.a + f.b) / 2 + new Vector2(0, -80);
            var p = Vector2.Lerp(Vector2.Lerp(f.a, ctrl, e), Vector2.Lerp(ctrl, f.b, e), e);
            float s = cdCell * (0.8f - k * 0.35f);
            DT(new Rect(p.x - s / 2, p.y - s / 2, s, s), KindTex(f.kind), Color.white);
        }
        // 콤보 글자
        if (cdComboText.t < 1.1f && !string.IsNullOrEmpty(cdComboText.s))
        {
            float k = cdComboText.t, sc = k < 0.15f ? Mathf.Lerp(0.3f, 1.25f, k / 0.15f) : k < 0.3f ? Mathf.Lerp(1.25f, 1f, (k - 0.15f) / 0.15f) : 1f;
            float a = Mathf.Clamp01((1.1f - k) * 3);
            var c = new Vector2(br.center.x, br.y + br.height * 0.42f);
            var m = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), c * UiK);
            CgText(new Rect(c.x - 200, c.y - 30, 400, 60), cdComboText.s, 34, new Color(1f, 0.95f, 0.6f, a), TextAnchor.MiddleCenter, 3, new Color(0.6f, 0.1f, 0.25f, a));
            GUI.matrix = m;
        }
        // 큰 알림 (목표 달성 / 끝)
        if (cdp == CdP.Banner && !string.IsNullOrEmpty(cdBannerText) || cdp == CdP.Shuffle && !string.IsNullOrEmpty(cdBannerText))
        {
            float k = cdpT, sc = k < 0.2f ? Mathf.Lerp(0.4f, 1.15f, k / 0.2f) : k < 0.35f ? Mathf.Lerp(1.15f, 1f, (k - 0.2f) / 0.15f) : 1f;
            var c = br.center;
            Panel(new Rect(br.x - 4, c.y - 46, br.width + 8, 92), new Color(0.35f, 0.12f, 0.35f, 0.75f));
            var m = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), c * UiK);
            CgText(new Rect(c.x - 220, c.y - 34, 440, 68), cdBannerText, 38, new Color(1f, 0.93f, 0.55f), TextAnchor.MiddleCenter, 3, new Color(0.5f, 0.08f, 0.2f));
            GUI.matrix = m;
        }
    }

    void CandyHUD(bool portrait, Rect hud)
    {
        // 남은 횟수 / 점수(별 막대) / 목표
        if (portrait)
        {
            Panel(hud, new Color(1f, 0.97f, 0.94f, 0.94f));
            // 횟수 동그라미
            var mr = new Rect(hud.x + 10, hud.y + 12, 78, 78);
            DT(mr, cgDisc, cdMoves <= 5 ? new Color(0.95f, 0.35f, 0.4f) : new Color(0.55f, 0.35f, 0.85f));
            CgText(new Rect(mr.x, mr.y + 6, mr.width, 44), cdMoves.ToString(), 32, Color.white, TextAnchor.MiddleCenter, 2, new Color(0.2f, 0.05f, 0.25f, 0.8f));
            CgText(new Rect(mr.x, mr.y + 46, mr.width, 22), "남은 횟수", 12, Color.white, TextAnchor.MiddleCenter, 1.5f, new Color(0.2f, 0.05f, 0.25f, 0.8f));
            // 그만 버튼
            if (cds == CdS.Play && CgButton(new Rect(hud.xMax - 74, hud.y + 8, 66, 30), "그만", new Color(0.55f, 0.6f, 0.72f), 14)) { cds = CdS.Menu; cdsT = 0; }
            // 점수
            CgText(new Rect(mr.xMax + 8, hud.y + 8, 200, 34), ((int)cdShown).ToString("#,0"), 26, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft, 2, new Color(0.45f, 0.15f, 0.2f));
            StarBar(new Rect(mr.xMax + 10, hud.y + 46, hud.xMax - mr.xMax - 26, 14));
            // 목표
            float gx = mr.xMax + 8, gy = hud.y + 70, gw = (hud.xMax - 10 - gx) / cdGoalKind.Count;
            for (int i = 0; i < cdGoalKind.Count; i++) GoalCell(i, new Rect(gx + i * gw, gy, gw, 54));
        }
        else
        {
            Panel(hud, new Color(1f, 0.97f, 0.94f, 0.94f));
            var mr = new Rect(hud.center.x - 48, hud.y + 16, 96, 96);
            DT(mr, cgDisc, cdMoves <= 5 ? new Color(0.95f, 0.35f, 0.4f) : new Color(0.55f, 0.35f, 0.85f));
            CgText(new Rect(mr.x, mr.y + 10, mr.width, 50), cdMoves.ToString(), 38, Color.white, TextAnchor.MiddleCenter, 2, new Color(0.2f, 0.05f, 0.25f, 0.8f));
            CgText(new Rect(mr.x, mr.y + 60, mr.width, 22), "남은 횟수", 13, Color.white, TextAnchor.MiddleCenter, 1.5f, new Color(0.2f, 0.05f, 0.25f, 0.8f));
            CgText(new Rect(hud.x, mr.yMax + 10, hud.width, 36), ((int)cdShown).ToString("#,0"), 28, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, 2, new Color(0.45f, 0.15f, 0.2f));
            StarBar(new Rect(hud.x + 18, mr.yMax + 52, hud.width - 36, 14));
            var st = new GUIStyle(CgStyle(15, TextAnchor.MiddleCenter)) { normal = { textColor = new Color(0.4f, 0.22f, 0.3f).linear } };
            GUI.Label(new Rect(hud.x, mr.yMax + 76, hud.width, 22), "목표", st);
            float gy = mr.yMax + 100;
            int cols = 2; float gw = (hud.width - 24) / cols;
            for (int i = 0; i < cdGoalKind.Count; i++) GoalCell(i, new Rect(hud.x + 12 + (i % cols) * gw, gy + (i / cols) * 64, gw, 58));
            if (cds == CdS.Play && CgButton(new Rect(hud.center.x - 50, hud.yMax - 48, 100, 36), "그만", new Color(0.55f, 0.6f, 0.72f), 15)) { cds = CdS.Menu; cdsT = 0; }
        }
    }
    void StarBar(Rect r)
    {
        Pill(r, new Color(0.35f, 0.25f, 0.45f, 0.35f));
        float k = Mathf.Clamp01(cdShown / 36000f);
        if (k > 0.01f) Pill(new Rect(r.x + 2, r.y + 2, Mathf.Max(r.height - 4, (r.width - 4) * k), r.height - 4), new Color(1f, 0.72f, 0.25f));
        var star = CTx("piece_2");
        float[] th = { 0, 18000f / 36000f, 30000f / 36000f };
        for (int i = 0; i < 3; i++)
        {
            float px = i == 0 ? r.x + r.width * 0.25f : r.x + r.width * th[i];
            bool on = i == 0 ? cdGoalLeft.All(n => n <= 0) : cdShown >= th[i] * 36000f;
            DT(new Rect(px - 11, r.center.y - 11, 22, 22), star, on ? Color.white : new Color(0.45f, 0.42f, 0.5f, 0.9f));
        }
    }
    void GoalCell(int i, Rect r)
    {
        cdGoalRects[i] = new Rect(r.x + r.width / 2 - 18, r.y, 36, 36);
        float pop = i < cdGoalPop.Length ? cdGoalPop[i] : 0;
        float s = 38 * (1 + pop * 0.25f);
        DT(new Rect(r.center.x - s / 2, r.y + 18 - s / 2, s, s), KindTex(cdGoalKind[i]), Color.white);
        int left = cdGoalLeft[i];
        if (left <= 0)
        {
            Pill(new Rect(r.center.x - 22, r.y + 36, 44, 20), new Color(0.3f, 0.72f, 0.38f));
            CgText(new Rect(r.center.x - 22, r.y + 36, 44, 20), "완료", 13, Color.white, TextAnchor.MiddleCenter, 1, new Color(0.1f, 0.3f, 0.12f));
        }
        else CgText(new Rect(r.x, r.y + 34, r.width, 22), left.ToString(), 18, Color.white, TextAnchor.MiddleCenter, 2, new Color(0.35f, 0.12f, 0.3f));
    }

    void CandyResultGUI()
    {
        float k = Mathf.Clamp01(cdsT / 0.35f), sc = Mathf.Lerp(0.7f, 1f, 1 - (1 - k) * (1 - k));
        var full = new Rect(0, 0, SW, SH);
        DT(full, Texture2D.whiteTexture, new Color(0.1f, 0.05f, 0.15f, 0.45f * k));
        float pw = Mathf.Min(SW - 30, 400), ph = 330;
        var r = new Rect(SW / 2 - pw / 2, SH / 2 - ph / 2, pw, ph);
        var m = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), r.center * UiK);
        Panel(new Rect(r.x, r.y + 6, r.width, r.height), new Color(0.15f, 0.05f, 0.2f, 0.4f));
        Panel(r, new Color(1f, 0.97f, 0.93f, 1f));
        var rib = new Rect(r.x + 30, r.y - 22, r.width - 60, 52);
        Pill(rib, cdWin ? new Color(0.92f, 0.3f, 0.38f) : new Color(0.5f, 0.55f, 0.7f));
        CgText(rib, cdWin ? "성공!" : "아쉬워요!", 28, new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter, 2.5f, new Color(0.4f, 0.06f, 0.15f));
        // 별 3개
        var star = CTx("piece_2");
        for (int i = 0; i < 3; i++)
        {
            float d = Mathf.Clamp01((cdsT - 0.3f - i * 0.25f) * 4);
            bool on = i < cdStars;
            float s = (i == 1 ? 76 : 60) * (on ? (d < 1 ? Mathf.Lerp(1.6f, 1f, d) : 1f) : 1f);
            float cx = r.center.x + (i - 1) * 84, cy = r.y + 72 + (i == 1 ? -8 : 4);
            DT(new Rect(cx - s / 2, cy - s / 2, s, s), star, on && d > 0 ? Color.white : new Color(0.75f, 0.72f, 0.8f, 0.8f));
        }
        float shownFinal = Mathf.Min(cdScore, cdScore * Mathf.Clamp01((cdsT - 0.2f) / 1.0f));
        CgText(new Rect(r.x, r.y + 120, r.width, 46), ((int)shownFinal).ToString("#,0") + "점", 34, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, 2.5f, new Color(0.45f, 0.15f, 0.2f));
        var st = new GUIStyle(CgStyle(15, TextAnchor.MiddleCenter)) { normal = { textColor = new Color(0.4f, 0.22f, 0.3f).linear } };
        GUI.Label(new Rect(r.x, r.y + 166, r.width, 22), cdNewBest ? "내 최고 기록 경신!" : (cdPrevBest > 0 ? $"내 최고 점수 {Mathf.Max(cdPrevBest, cdScore):#,0}" : ""), st);
        if (CgButton(new Rect(r.center.x - 120, r.y + 200, 240, 54), "다시 하기", new Color(0.92f, 0.3f, 0.38f), 21)) CdNewGame();
        float bw = 115;
        if (CgButton(new Rect(r.center.x - bw - 5, r.y + 264, bw, 44), "명예의 전당", new Color(0.95f, 0.65f, 0.2f), 15)) { cds = CdS.Board; cdsT = 0; }
        if (CgButton(new Rect(r.center.x + 5, r.y + 264, bw, 44), "나가기", new Color(0.45f, 0.55f, 0.7f), 15)) CloseCandy();
        GUI.matrix = m;
        // 성공 색종이
        if (cdWin && Event.current.type == EventType.Repaint)
        {
            for (int i = 0; i < 40; i++)
            {
                float t = cdsT * (0.6f + (i % 5) * 0.1f) + i * 0.37f;
                float x = Mathf.Repeat(i * 71.3f + Mathf.Sin(t * 2 + i) * 30, SW), y = Mathf.Repeat(t * 160 + i * 41, SH + 40) - 20;
                DT(new Rect(x - 6, y - 6, 12, 12), CTx("spark"), CdCol[i % 6]);
            }
        }
    }

    // ===== 점수판 =====
    int CdMyBest()
    {
        var s = net != null ? net.Get("candy") : null; int best = 0;
        if (s != null) foreach (var kv in s) if (kv.Value is Dictionary<string, object> d && NameKey(AcJson.S(d, "name")) == myKey) best = Mathf.Max(best, (int)AcJson.D(d, "best"));
        return best;
    }
    List<(string name, int best, string look)> CdTop()
    {
        var res = new List<(string, int, string)>();
        var s = net != null ? net.Get("candy") : null;
        if (s != null) foreach (var kv in s) if (kv.Value is Dictionary<string, object> d) res.Add((AcJson.S(d, "name"), (int)AcJson.D(d, "best"), AcJson.S(d, "look")));
        return res.OrderByDescending(x => x.Item2).ToList();
    }
    void CandyBoardGUI()
    {
        var list = CdTop();
        bool portrait = SH > SW;
        var tr = new Rect(SW / 2 - 160, 18, 320, 54);
        Pill(new Rect(tr.x, tr.y + 5, tr.width, tr.height), new Color(0.2f, 0.06f, 0.15f, 0.5f));
        Pill(tr, new Color(0.92f, 0.3f, 0.38f));
        CgText(tr, "명예의 전당", 28, new Color(1f, 0.93f, 0.6f), TextAnchor.MiddleCenter, 2.5f, new Color(0.45f, 0.06f, 0.12f));
        // 1·2·3등 (가운데가 1등)
        float cw = Mathf.Min(130, (SW - 40) / 3f), top = tr.yMax + 36;
        int[] order = { 1, 0, 2 };
        var medals = new[] { cgMedalG, cgMedalS, cgMedalB };
        for (int j = 0; j < 3; j++)
        {
            int rank = order[j];
            float x = SW / 2 + (j - 1) * (cw + 8) - cw / 2;
            float lift = rank == 0 ? 0 : rank == 1 ? 26 : 40;
            float pop = Mathf.Clamp01((cdsT - 0.15f * (2 - rank)) * 3);
            var card = new Rect(x, top + lift + (1 - pop) * 30, cw, (portrait ? 196 : 176) - lift);
            if (pop <= 0) continue;
            Panel(new Rect(card.x, card.y + 5, card.width, card.height), new Color(0.15f, 0.05f, 0.2f, 0.35f * pop));
            Panel(card, new Color(1f, 0.97f, 0.93f, pop));
            if (rank < list.Count)
            {
                var face = CgPortrait(list[rank].look);
                float fs = Mathf.Min(card.width - 24, 92);
                if (face != null) DT(new Rect(card.center.x - fs / 2, card.y + 12, fs, fs), face, new Color(1, 1, 1, pop));
                if (rank == 0 && cgCrown != null) DT(new Rect(card.center.x - 22, card.y - 26, 44, 34), cgCrown, new Color(1, 1, 1, pop));
                DT(new Rect(card.x + 4, card.y + 4, 36, 36), medals[rank], new Color(1, 1, 1, pop));
                var ns = new GUIStyle(CgStyle(15, TextAnchor.MiddleCenter)) { normal = { textColor = new Color(0.4f, 0.22f, 0.25f).linear } };
                GUI.Label(new Rect(card.x, card.y + fs + 16, card.width, 22), Short(list[rank].name, 7), ns);
                CgText(new Rect(card.x, card.y + fs + 38, card.width, 24), list[rank].best.ToString("#,0"), 18, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, 2, new Color(0.45f, 0.15f, 0.2f));
            }
            else { var ns = new GUIStyle(CgStyle(14, TextAnchor.MiddleCenter)) { normal = { textColor = new Color(0.6f, 0.5f, 0.5f).linear } }; GUI.Label(new Rect(card.x, card.y, card.width, card.height), "비어 있음", ns); }
        }
        // 4~10등
        float ly = top + (portrait ? 214 : 190), lw = Mathf.Min(SW - 30, 440), rowH = 34;
        var lr = new Rect(SW / 2 - lw / 2, ly, lw, rowH * 7 + 16);
        Panel(lr, new Color(1f, 0.97f, 0.93f, 0.95f));
        for (int i = 3; i < 10; i++)
        {
            var r = new Rect(lr.x + 10, lr.y + 8 + (i - 3) * rowH, lr.width - 20, rowH);
            var ns = new GUIStyle(CgStyle(15, TextAnchor.MiddleLeft)) { normal = { textColor = new Color(0.42f, 0.25f, 0.28f).linear } };
            GUI.Label(new Rect(r.x + 4, r.y, 34, r.height), (i + 1) + "", ns);
            if (i < list.Count)
            {
                var face = CgPortrait(list[i].look);
                if (face != null) DT(new Rect(r.x + 36, r.y + 2, r.height - 4, r.height - 4), face, Color.white);
                GUI.Label(new Rect(r.x + 36 + r.height + 4, r.y, r.width - 170, r.height), Short(list[i].name, 9), ns);
                var ss = new GUIStyle(ns) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(r.xMax - 120, r.y, 116, r.height), list[i].best.ToString("#,0") + "점", ss);
            }
            else GUI.Label(new Rect(r.x + 40, r.y, 200, r.height), "-", ns);
        }
        int mine = list.FindIndex(b => NameKey(b.name) == myKey);
        if (mine >= 0) CgText(new Rect(0, lr.yMax + 6, SW, 24), $"내 순위 {mine + 1}등,  {list[mine].best:#,0}점", 16, new Color(1f, 0.9f, 0.6f));
        float bw = 140, by = Mathf.Min(SH - 62, lr.yMax + 40);
        if (CgButton(new Rect(SW / 2 - bw - 6, by, bw, 50), "게임 시작!", new Color(0.92f, 0.3f, 0.38f), 18)) CdNewGame();
        if (CgButton(new Rect(SW / 2 + 6, by, bw, 50), "돌아가기", new Color(0.45f, 0.55f, 0.7f), 18)) { cds = CdS.Menu; cdsT = 0; }
    }
}
