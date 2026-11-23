// AcCookieUI.cs - 쿠키 달리기 화면: 시작 화면, 점수·체력·피버 표시, 점프/슬라이드 버튼, 결과, 명예의 전당(3D 시상대 + 4~10등)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class AcRoom
{
    // ===== 그림 재료 (한 번 만듦) =====
    Texture2D cgPill, cgDisc, cgHeart, cgMedalG, cgMedalS, cgMedalB, cgCrown, cgVignette, cgGrad, cgStripe;
    GUIStyle cgPillStyle;
    readonly Dictionary<int, GUIStyle> cgStyles = new Dictionary<int, GUIStyle>();
    readonly Dictionary<string, RenderTexture> cgPortraits = new Dictionary<string, RenderTexture>();
    readonly List<(Vector2 p, Vector2 v, float rot, float vr, Color c, float life)> cgConfetti = new List<(Vector2, Vector2, float, float, Color, float)>();

    void CgTextures()
    {
        if (cgPill != null) return;
        cgPill = new Texture2D(64, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, 16, 48), d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, 16));
                cgPill.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(16 - d)));
            }
        cgPill.Apply();
        cgPillStyle = new GUIStyle { normal = { background = cgPill }, border = new RectOffset(15, 15, 15, 15) };
        cgDisc = new Texture2D(96, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(48, 48));
                float shade = 1 - 0.18f * Mathf.Clamp01((48 - y) / 48f);
                cgDisc.SetPixel(x, y, new Color(shade, shade, shade, Mathf.Clamp01(46 - d)));
            }
        cgDisc.Apply();
        cgHeart = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float hx = (x - 31.5f) / 24f, hy = (y - 28f) / 24f;
                float f = Mathf.Pow(hx * hx + hy * hy - 1, 3) - hx * hx * hy * hy * hy;
                float a = Mathf.Clamp01(-f * 40f);
                float shade = 0.85f + 0.15f * (y / 63f);
                cgHeart.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
        cgHeart.Apply();
        cgMedalG = MedalTex(new Color(1f, 0.9f, 0.45f), new Color(0.85f, 0.55f, 0.08f), new Color(0.85f, 0.15f, 0.2f));
        cgMedalS = MedalTex(new Color(0.97f, 0.98f, 1f), new Color(0.55f, 0.6f, 0.7f), new Color(0.2f, 0.45f, 0.85f));
        cgMedalB = MedalTex(new Color(1f, 0.75f, 0.5f), new Color(0.6f, 0.32f, 0.12f), new Color(0.2f, 0.6f, 0.35f));
        cgCrown = CrownTex();
        cgVignette = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 45f;
                cgVignette.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((d - 0.45f) * 1.6f)));
            }
        cgVignette.Apply();
        cgGrad = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) cgGrad.SetPixel(0, y, Color.Lerp(new Color(0.26f, 0.14f, 0.1f), new Color(0.47f, 0.26f, 0.15f), y / 63f));
        cgGrad.Apply();
        cgStripe = new Texture2D(32, 8) { wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 8; y++) for (int x = 0; x < 32; x++) cgStripe.SetPixel(x, y, ((x + y) / 8) % 2 == 0 ? new Color(0.86f, 0.16f, 0.2f) : new Color(1f, 0.97f, 0.93f));
        cgStripe.Apply();
    }

    static Texture2D MedalTex(Color light, Color dark, Color ribbon)
    {
        const int W = 128, H = 168;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                Color c = clear;
                float py = H - 1 - y;   // 위에서부터
                // 리본 (위쪽 V자)
                float rx1 = Mathf.Abs((x - 40) - (py) * 0.18f), rx2 = Mathf.Abs((x - 88) + (py) * 0.18f);
                if (py < 70 && (rx1 < 13 || rx2 < 13))
                {
                    bool stripe = (rx1 < 13 ? rx1 : rx2) < 4;
                    c = stripe ? Color.Lerp(ribbon, Color.white, 0.75f) : ribbon * (0.85f + 0.15f * (py / 70f));
                    c.a = 1;
                }
                // 메달
                var p = new Vector2(x + 0.5f, py + 0.5f); var ctr = new Vector2(64, 104);
                float d = Vector2.Distance(p, ctr);
                if (d < 60)
                {
                    float g = Mathf.Clamp01(((p.x - ctr.x) - (p.y - ctr.y)) / 120f + 0.5f);
                    Color m = Color.Lerp(light, dark, g);
                    if (d > 47 && d < 56) m = Color.Lerp(dark, light, Mathf.Clamp01(1 - g)) * 0.95f;   // 테두리
                    if (d > 55) m = dark * 0.85f;
                    // 별
                    float a = Mathf.Atan2(p.y - ctr.y, p.x - ctr.x) * Mathf.Rad2Deg + 90;
                    float local = Mathf.Abs(Mathf.Repeat(a, 72f) - 36f) / 36f;
                    float sr = Mathf.Lerp(16, 34, local);
                    if (d < sr) m = Color.Lerp(light, Color.white, 0.35f) * (1 - 0.15f * g);
                    // 반짝임
                    float hl = Vector2.Distance(p, ctr + new Vector2(-20, -22));
                    if (hl < 10) m = Color.Lerp(m, Color.white, (10 - hl) / 10f * 0.6f);
                    m.a = Mathf.Clamp01(60 - d);
                    c = c.a > 0 && m.a < 1 ? Color.Lerp(c, m, m.a) : m;
                    c.a = Mathf.Max(c.a, m.a);
                }
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    static Texture2D CrownTex()
    {
        const int W = 128, H = 96;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color gold = new Color(1f, 0.82f, 0.25f), deep = new Color(0.85f, 0.55f, 0.1f);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = 1 - y / (float)H;   // v: 위 0
                bool band = v > 0.62f && v < 0.9f && u > 0.1f && u < 0.9f;
                bool spike = false;
                foreach (var cx in new[] { 0.16f, 0.5f, 0.84f })
                {
                    float top = cx == 0.5f ? 0.08f : 0.2f, half = 0.17f * (v - top) / (0.64f - top);
                    if (v > top && v < 0.66f && Mathf.Abs(u - cx) < half) spike = true;
                }
                Color c = new Color(0, 0, 0, 0);
                if (band || spike) { c = Color.Lerp(gold, deep, v); c.a = 1; }
                foreach (var cx in new[] { 0.16f, 0.5f, 0.84f })
                {
                    float top = cx == 0.5f ? 0.08f : 0.2f;
                    if (Vector2.Distance(new Vector2(u, v * 0.75f), new Vector2(cx, top * 0.75f)) < 0.05f) c = new Color(1f, 0.95f, 0.8f, 1);
                }
                if (band && Mathf.Abs(v - 0.76f) < 0.07f)
                    foreach (var (cx, jc) in new[] { (0.3f, new Color(0.9f, 0.15f, 0.25f)), (0.5f, new Color(0.2f, 0.55f, 0.95f)), (0.7f, new Color(0.25f, 0.75f, 0.35f)) })
                        if (Mathf.Abs(u - cx) < 0.05f) c = jc;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    GUIStyle CgStyle(int size, TextAnchor a)
    {
        int k = size * 16 + (int)a;
        if (cgStyles.TryGetValue(k, out var s)) return s;
        s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a, richText = false, wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = Color.white } };
        if (uiFont != null) s.font = uiFont;
        cgStyles[k] = s;
        return s;
    }

    // 테두리 있는 글자 (게임 화면 위에서도 잘 보이게)
    void CgText(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, float outline = 2f, Color? oc = null)
    {
        var st = CgStyle(size, a);
        var o = oc ?? new Color(0.24f, 0.12f, 0.08f, 0.95f);
        var keep = st.normal.textColor;
        st.normal.textColor = o.linear;
        for (int i = 0; i < 8; i++)
        {
            float an = i * Mathf.PI / 4;
            GUI.Label(new Rect(r.x + Mathf.Cos(an) * outline, r.y + Mathf.Sin(an) * outline, r.width, r.height), s, st);
        }
        GUI.Label(new Rect(r.x, r.y + outline * 0.9f, r.width, r.height), s, st);
        st.normal.textColor = c.linear;
        GUI.Label(r, s, st);
        st.normal.textColor = keep;
    }

    void Pill(Rect r, Color c) { if (Event.current.type != EventType.Repaint) return; var k = GUI.color; GUI.color = c.linear; cgPillStyle.Draw(r, false, false, false, false); GUI.color = k; }

    bool CgButton(Rect r, string label, Color col, int size = 22)
    {
        cgUi.Add(r);
        bool hover = r.Contains(Event.current.mousePosition);
        Pill(new Rect(r.x, r.y + 4, r.width, r.height), new Color(0.2f, 0.1f, 0.05f, 0.45f));
        Pill(r, hover ? Color.Lerp(col, Color.white, 0.15f) : col);
        Pill(new Rect(r.x + 6, r.y + 4, r.width - 12, r.height * 0.42f), new Color(1, 1, 1, 0.22f));
        CgText(r, label, size, Color.white, TextAnchor.MiddleCenter, 2, new Color(0, 0, 0, 0.35f));
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (clicked) Sfx("click", 0.6f);
        return clicked;
    }

    // ========================================================
    void CookieGameGUI()
    {
        if (!playingGame || cgRoot == null) return;
        CgTextures();
        if (Event.current.type == EventType.Layout) cgUi.Clear();
        var band = CgBand();
        bool portrait = CgIsPortrait;
        // 세로 화면: 띠 바깥을 쿠키색으로 채우고 사탕 줄무늬 테두리
        if (portrait)
        {
            GUI.DrawTexture(new Rect(0, 0, SW, band.y), cgGrad);
            GUI.DrawTexture(new Rect(0, band.yMax, SW, SH - band.yMax), cgGrad);
            GUI.DrawTextureWithTexCoords(new Rect(0, band.y - 6, SW, 8), cgStripe, new Rect(0, 0, SW / 32f, 1));
            GUI.DrawTextureWithTexCoords(new Rect(0, band.yMax - 2, SW, 8), cgStripe, new Rect(0, 0, SW / 32f, 1));
        }
        // 맞으면 빨간 테두리
        if (cgFlash > 0) { GUI.color = (new Color(1, 0.1f, 0.1f, cgFlash * 1.8f)).linear; GUI.DrawTexture(band, cgVignette); GUI.color = Color.white; }
        if (cgFeverT > 0) { GUI.color = (new Color(1, 0.85f, 0.2f, 0.35f + 0.2f * Mathf.Sin(Time.time * 10))).linear; GUI.DrawTexture(band, cgVignette); GUI.color = Color.white; }
        if (cgRushT > 0 && cg == CgState.Play)
        {   // 질주 바람 줄
            var rnd = new System.Random((int)(Time.time * 20));
            for (int i = 0; i < 14; i++)
            {
                float y = band.y + (float)rnd.NextDouble() * band.height, w = 60 + (float)rnd.NextDouble() * 160;
                GUI.color = (new Color(1, 1, 1, 0.35f)).linear; GUI.DrawTexture(new Rect((float)rnd.NextDouble() * band.width, y, w, 2), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        // 점수 튀어나오기
        foreach (var p in cgPops)
        {
            var sp = cgCam.WorldToScreenPoint(p.w); if (sp.z < 0) continue;
            var u = new Vector2(sp.x / UiK, (Screen.height - sp.y) / UiK);
            var col = p.col; col.a = 1 - Mathf.Clamp01((p.t - 0.5f) / 0.4f);
            CgText(new Rect(u.x - 100, u.y - 20, 200, 40), p.text, p.text.Length > 5 ? 28 : 22, col, TextAnchor.MiddleCenter, 2, new Color(0.2f, 0.1f, 0.05f, col.a));
        }
        switch (cg)
        {
            case CgState.Menu: MenuGUI(band, portrait); break;
            case CgState.Count: HudGUI(band, portrait); CountGUI(band); break;
            case CgState.Play: case CgState.Dead: HudGUI(band, portrait); break;
            case CgState.Result: HudGUI(band, portrait); ResultGUI(band, portrait); break;
            case CgState.Board: BoardGUI(band, portrait); break;
        }
        ConfettiGUI();
    }

    void MenuGUI(Rect band, bool portrait)
    {
        float t = cgStateT;
        // 제목
        float ty = portrait ? Mathf.Max(10, band.y * 0.5f - 46) : band.y + 22;
        float bounce = Mathf.Sin(t * 3f) * 4;
        CgText(new Rect(0, ty + bounce, SW, 70), "쿠키 달리기", 56, new Color(1f, 0.93f, 0.7f), TextAnchor.MiddleCenter, 4, new Color(0.55f, 0.2f, 0.1f));
        CgText(new Rect(0, ty + 66, SW, 30), "크리스마스 저녁, 눈길을 신나게 달려요!", 18, Color.white, TextAnchor.MiddleCenter, 2);
        // 버튼
        float bw = Mathf.Min(300, SW - 60);
        float by = portrait ? band.yMax + 24 : band.yMax - 210;
        if (CgButton(new Rect(SW / 2 - bw / 2, by, bw, 62), "달리기 시작!", new Color(0.92f, 0.32f, 0.3f), 30)) StartRun();
        if (CgButton(new Rect(SW / 2 - bw / 2, by + 74, bw * 0.48f, 48), "명예의 전당", new Color(0.95f, 0.66f, 0.2f), 20)) SetCg(CgState.Board);
        if (CgButton(new Rect(SW / 2 + bw * 0.02f, by + 74, bw * 0.48f, 48), "나가기", new Color(0.45f, 0.55f, 0.65f), 20)) { CloseCookieGame(); return; }
        int best = MyBest();
        CgText(new Rect(0, by + 132, SW, 28), best > 0 ? $"{uiName.Trim()} 님의 최고 기록  {best:#,0}점" : "첫 기록을 세워 보세요!", 18, new Color(1f, 0.9f, 0.6f));
        if (portrait || SH > 520)
            CgText(new Rect(0, by + 160, SW, 26), IsMobile ? "점프 버튼(두 번=2단 점프) · 슬라이드 버튼을 누르고 있기" : "스페이스: 점프(두 번=2단 점프) · 아래 화살표: 슬라이드", 15, new Color(0.95f, 0.9f, 0.85f), TextAnchor.MiddleCenter, 1.5f);
    }

    void CountGUI(Rect band)
    {
        int n = Mathf.CeilToInt(3 - cgStateT / 0.75f);
        float f = Mathf.Repeat(cgStateT / 0.75f, 1f);
        float s = 1.6f - 0.6f * Mathf.Clamp01(f * 3);
        var m = GUI.matrix;
        var c = new Vector2(band.center.x, band.center.y - 20);
        GUIUtility.ScaleAroundPivot(new Vector2(s, s), c * UiK);
        CgText(new Rect(c.x - 150, c.y - 50, 300, 100), n > 0 ? n.ToString() : "출발!", 84, n > 0 ? Color.white : new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, 4);
        GUI.matrix = m;
    }

    void HudGUI(Rect band, bool portrait)
    {
        float top = portrait ? Mathf.Max(8, band.y - 108) : band.y + 10;
        float w = Mathf.Min(SW - 24, 520);
        float x0 = portrait ? 12 : 16;
        // 체력
        float hp = Mathf.Clamp01(cgHp / 100f);
        Rect bar = new Rect(x0 + 34, top + 8, Mathf.Min(260, SW * 0.5f), 26);
        Pill(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0.2f, 0.1f, 0.06f, 0.75f));
        if (hp > 0.02f)
        {
            var hc = hp > 0.35f ? Color.Lerp(new Color(1f, 0.55f, 0.25f), new Color(0.95f, 0.25f, 0.35f), hp) : (Mathf.Repeat(Time.time * 4, 1) < 0.5f ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.6f, 0.2f));
            Pill(new Rect(bar.x + 2, bar.y + 2, Mathf.Max(26, (bar.width - 4) * hp), bar.height - 4), hc);
            Pill(new Rect(bar.x + 8, bar.y + 4, Mathf.Max(14, (bar.width - 16) * hp), 7), new Color(1, 1, 1, 0.35f));
        }
        GUI.color = (new Color(0.3f, 0.08f, 0.08f, 0.8f)).linear; GUI.DrawTexture(new Rect(x0 - 1, top + 3, 42, 42), cgHeart);
        float beat = 1 + 0.08f * Mathf.Max(0, Mathf.Sin(Time.time * (hp < 0.35f ? 14 : 6)));
        GUI.color = (new Color(0.95f, 0.22f, 0.32f)).linear; GUI.DrawTexture(new Rect(x0 + 20 - 19 * beat, top + 21 - 19 * beat, 38 * beat, 38 * beat), cgHeart); GUI.color = Color.white;
        // 피버 게이지
        Rect fb = new Rect(bar.x, bar.yMax + 8, bar.width * 0.85f, 14);
        Pill(new Rect(fb.x - 2, fb.y - 2, fb.width + 4, fb.height + 4), new Color(0.2f, 0.1f, 0.06f, 0.7f));
        float fv = cgFeverT > 0 ? cgFeverT / 7f : cgFever / 100f;
        if (fv > 0.02f)
        {
            var fc = Color.HSVToRGB(Mathf.Repeat(Time.time * (cgFeverT > 0 ? 1.2f : 0.25f), 1f), 0.6f, 1f);
            Pill(new Rect(fb.x + 1, fb.y + 1, Mathf.Max(12, (fb.width - 2) * Mathf.Clamp01(fv)), fb.height - 2), fc);
        }
        CgText(new Rect(fb.xMax + 6, fb.y - 6, 80, 26), cgFeverT > 0 ? "피버!" : "피버", 15, cgFeverT > 0 ? new Color(1f, 0.85f, 0.3f) : Color.white, TextAnchor.MiddleLeft, 1.5f);
        // 점수
        float sx = SW - 16;
        CgText(new Rect(sx - 260, top, 250, 44), ((int)cgShownScore).ToString("#,0"), 36, Color.white, TextAnchor.MiddleRight, 3);
        CgText(new Rect(sx - 260, top + 40, 250, 24), cgDistM.ToString("#,0") + " m", 16, new Color(1f, 0.92f, 0.75f), TextAnchor.MiddleRight, 1.5f);
        // 아이템 남은 시간
        float ix = x0; float iy = fb.yMax + 10;
        void Pow(string label, float tleft, Color c) { if (tleft <= 0) return; Pill(new Rect(ix, iy, 104, 26), new Color(c.r, c.g, c.b, 0.9f)); CgText(new Rect(ix, iy, 104, 26), label + " " + Mathf.CeilToInt(tleft), 15, Color.white, TextAnchor.MiddleCenter, 1.5f); ix += 112; }
        Pow("거대화", cgGiantT, new Color(0.95f, 0.65f, 0.15f)); Pow("자석", cgMagnetT, new Color(0.9f, 0.25f, 0.3f)); Pow("질주", cgRushT, new Color(0.3f, 0.65f, 0.95f));
        // 그만하기
        if (cg == CgState.Play)
        {
            var pr = portrait ? new Rect(SW - 84, top + 70, 72, 34) : new Rect(SW - 92, top + 70, 76, 34);
            if (CgButton(pr, "그만", new Color(0.4f, 0.45f, 0.55f, 0.9f), 16)) Die();
        }
        // 점프·슬라이드 버튼
        if (cg == CgState.Play || cg == CgState.Count)
        {
            float bs = portrait ? Mathf.Min(150, (SH - band.yMax) - 30, SW * 0.38f) : Mathf.Min(130, SH * 0.22f);
            float byy = portrait ? band.yMax + (SH - band.yMax) / 2 - bs / 2 : SH - bs - 18;
            cgJumpBtn = new Rect(portrait ? SW * 0.25f - bs / 2 : 22, byy, bs, bs);
            cgSlideBtn = new Rect(portrait ? SW * 0.75f - bs / 2 : SW - bs - 22, byy, bs, bs);
            bool jp = cgJumps > 0 && cgVy > 0;
            RoundBtn(cgJumpBtn, cgJumps == 2 ? "2단 점프" : "점프", new Color(0.95f, 0.45f, 0.3f), jp, portrait ? 1f : 0.82f);
            RoundBtn(cgSlideBtn, "슬라이드", new Color(0.3f, 0.6f, 0.95f), cgSlideShown, portrait ? 1f : 0.82f);
        }
        else { cgJumpBtn = cgSlideBtn = new Rect(-999, -999, 0, 0); }
    }

    void RoundBtn(Rect r, string label, Color c, bool pressed, float alpha)
    {
        var rr = pressed ? new Rect(r.x + r.width * 0.04f, r.y + r.height * 0.06f, r.width * 0.92f, r.height * 0.92f) : r;
        GUI.color = (new Color(0.15f, 0.08f, 0.04f, 0.45f * alpha)).linear; GUI.DrawTexture(new Rect(rr.x, rr.y + 6, rr.width, rr.height), cgDisc);
        GUI.color = (new Color(c.r, c.g, c.b, alpha)).linear; GUI.DrawTexture(rr, cgDisc);
        GUI.color = (new Color(1, 1, 1, 0.25f * alpha)).linear; GUI.DrawTexture(new Rect(rr.x + rr.width * 0.18f, rr.y + rr.height * 0.08f, rr.width * 0.64f, rr.height * 0.34f), cgDisc);
        GUI.color = Color.white;
        CgText(rr, label, label.Length > 3 ? 20 : 26, Color.white, TextAnchor.MiddleCenter, 2, new Color(0, 0, 0, 0.35f));
    }

    void ResultGUI(Rect band, bool portrait)
    {
        float t = cgStateT;
        float pw = Mathf.Min(SW - 30, 440), ph = 330;
        float py = portrait ? Mathf.Max(band.y + 10, SH / 2 - ph / 2 - 30) : SH / 2 - ph / 2;
        var pr = new Rect(SW / 2 - pw / 2, py + Mathf.Max(0, 1 - t * 4) * 60, pw, ph);
        Pill(new Rect(pr.x, pr.y + 8, pr.width, pr.height), new Color(0.15f, 0.07f, 0.04f, 0.5f));
        Pill(pr, new Color(1f, 0.96f, 0.88f, 0.97f));
        GUI.DrawTextureWithTexCoords(new Rect(pr.x + 24, pr.y + 54, pr.width - 48, 6), cgStripe, new Rect(0, 0, (pr.width - 48) / 32f, 1));
        CgText(new Rect(pr.x, pr.y + 10, pr.width, 40), "달리기 결과", 28, new Color(0.55f, 0.25f, 0.12f), TextAnchor.MiddleCenter, 0.01f, new Color(0, 0, 0, 0));
        CgText(new Rect(pr.x, pr.y + 66, pr.width, 70), ((int)cgShownScore).ToString("#,0") + "점", 56, new Color(1f, 0.82f, 0.25f), TextAnchor.MiddleCenter, 3, new Color(0.55f, 0.25f, 0.1f));
        var st = new GUIStyle(CgStyle(20, TextAnchor.MiddleLeft)) { normal = { textColor = new Color(0.45f, 0.28f, 0.18f).linear } };
        var sr = new GUIStyle(st) { alignment = TextAnchor.MiddleRight };
        float ry = pr.y + 146;
        void Row(string a, string b) { GUI.Label(new Rect(pr.x + 40, ry, 200, 28), a, st); GUI.Label(new Rect(pr.xMax - 240, ry, 200, 28), b, sr); ry += 30; }
        Row("달린 거리", cgDistM.ToString("#,0") + " m");
        Row("젤리 점수", cgJelly.ToString("#,0") + "점");
        Row("내 최고 기록", Mathf.Max(cgLastBest, cgLastScore).ToString("#,0") + "점");
        if (cgNewBest)
        {
            float s = 1 + 0.06f * Mathf.Sin(t * 8);
            var m = GUI.matrix;
            var c = new Vector2(pr.center.x, pr.y - 4);
            GUIUtility.ScaleAroundPivot(new Vector2(s, s), c * UiK);
            var rb = new Rect(c.x - 120, c.y - 22, 240, 44);
            Pill(rb, new Color(0.9f, 0.2f, 0.25f));
            CgText(rb, "새 최고 기록!", 24, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter, 2, new Color(0.45f, 0.05f, 0.08f));
            GUI.matrix = m;
            if (t < 3 && Random.value < 0.6f) SpawnConfetti(new Vector2(Random.Range(0, SW), -10));
        }
        if (t < 0.8f) return;
        float bw = (pw - 60) / 3f, by = pr.yMax - 62;
        if (CgButton(new Rect(pr.x + 20, by, bw, 46), "다시 달리기", new Color(0.92f, 0.32f, 0.3f), 17)) StartRun();
        if (CgButton(new Rect(pr.x + 30 + bw, by, bw, 46), "명예의 전당", new Color(0.95f, 0.66f, 0.2f), 17)) SetCg(CgState.Board);
        if (CgButton(new Rect(pr.x + 40 + bw * 2, by, bw, 46), "나가기", new Color(0.45f, 0.55f, 0.65f), 17)) { CloseCookieGame(); return; }
    }

    void SpawnConfetti(Vector2 at)
    {
        var c = Color.HSVToRGB(Random.value, 0.65f, 1f);
        cgConfetti.Add((at, new Vector2(Random.Range(-40, 40), Random.Range(60, 160)), Random.Range(0, 360), Random.Range(-300, 300), c, 4f));
    }
    void ConfettiGUI()
    {
        if (cgConfetti.Count == 0) return;
        float dt = Event.current.type == EventType.Repaint ? Time.deltaTime : 0;
        var m = GUI.matrix;
        for (int i = cgConfetti.Count - 1; i >= 0; i--)
        {
            var q = cgConfetti[i];
            q.p += q.v * dt; q.v.x += Mathf.Sin(Time.time * 3 + i) * 20 * dt; q.rot += q.vr * dt; q.life -= dt;
            cgConfetti[i] = q;
            if (q.life <= 0 || q.p.y > SH + 20) { cgConfetti.RemoveAt(i); continue; }
            GUIUtility.RotateAroundPivot(q.rot, q.p * UiK);
            GUI.color = q.c.linear; GUI.DrawTexture(new Rect(q.p.x - 5, q.p.y - 3, 10, 6), Texture2D.whiteTexture);
            GUI.matrix = m;
        }
        GUI.color = Color.white;
    }

    // ========================================================
    // 명예의 전당: 3D 시상대 (2등-1등-3등) + 4~10등 목록
    static readonly Vector3 PodiumPos = new Vector3(-300, 0, 2f);
    Transform podiumRoot;
    readonly List<AcCharacter> podiumChars = new List<AcCharacter>();
    List<(string name, int best, string look)> boardList = new List<(string, int, string)>();
    float podiumConfetti;

    void ShowPodium()
    {
        ClearPodium();
        boardList = TopScores();
        podiumRoot = new GameObject("Podium").transform; podiumRoot.SetParent(cgRoot, false); podiumRoot.localPosition = PodiumPos;
        // 무대 바닥 + 뒤 배경 (큰 트리, 선물, 사탕 지팡이)
        var stage = MeshObj("stage", BoxMesh(new Vector3(16, 0.3f, 8), new Color(0.82f, 0.16f, 0.2f), new Color(0.6f, 0.1f, 0.14f)), podiumRoot, 0f);
        stage.transform.localPosition = new Vector3(0, 0.15f, 1.5f);
        var tree = AcCatalog.Spawn("tree_giant", podiumRoot, null); tree.transform.localPosition = new Vector3(0, 0, 7.5f); tree.transform.localScale = Vector3.one * 1.05f;
        var topper = AcCatalog.Spawn("tree_star_topper", podiumRoot, null); topper.transform.localPosition = new Vector3(0, 6.85f * 1.05f, 7.5f); topper.transform.localScale = Vector3.one * 1.1f; SetEmit(topper, 0.8f);
        foreach (var (n, p, ry) in new[] { ("gift_pile", new Vector3(-5.5f, 0.3f, 3.5f), 20f), ("gift_pile", new Vector3(5.5f, 0.3f, 3.2f), -30f), ("candy_cane_big", new Vector3(-7f, 0.3f, 1.2f), 90f), ("candy_cane_big", new Vector3(7f, 0.3f, 1.2f), -90f), ("lamp_post", new Vector3(-6.5f, 0.3f, 4.5f), 0f), ("lamp_post", new Vector3(6.5f, 0.3f, 4.5f), 0f), ("reindeer_light", new Vector3(-4f, 0.3f, 5f), 150f), ("reindeer_light", new Vector3(4f, 0.3f, 5f), 210f) })
        {
            var g = AcCatalog.Spawn(n, podiumRoot, null); g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, ry, 0); g.transform.localScale = Vector3.one * 1.2f;
        }
        // 시상대 블록
        var blocks = new[] { (0, 0f, 1.5f, new Color(1f, 0.82f, 0.3f)), (1, -2.2f, 1.05f, new Color(0.85f, 0.88f, 0.95f)), (2, 2.2f, 0.7f, new Color(0.9f, 0.6f, 0.35f)) };
        foreach (var (rank, bx, bh, col) in blocks)
        {
            var blk = MeshObj("block" + rank, BoxMesh(new Vector3(2.1f, bh, 2.0f), Color.Lerp(col, Color.white, 0.25f), col), podiumRoot, 0.05f);
            blk.transform.localPosition = new Vector3(bx, 0.3f + bh / 2, 0);
            if (rank < boardList.Count)
            {
                var ch = AcCharacter.Create(CookieLook(boardList[rank].look), podiumRoot);
                ch.transform.localPosition = new Vector3(bx, 0.3f + bh, 0); ch.transform.localRotation = Quaternion.Euler(0, 180 + (rank == 1 ? 15 : rank == 2 ? -15 : 0), 0);
                ch.transform.localScale = Vector3.one * (rank == 0 ? 1.25f : 1.1f);
                ch.SetRunPose(rank == 0 ? 7 : 8);
                ch.runT = rank * 0.4f;
                podiumChars.Add(ch);
            }
            else
            {
                var gift = ModelH("gift_box", 0.6f, new Dictionary<string, Color> { { "box", col } }); gift.transform.SetParent(podiumRoot, false); gift.transform.localPosition = new Vector3(bx, 0.3f + bh, 0);
            }
        }
        // 무대 조명
        var lg = new GameObject("PodiumLight"); lg.transform.SetParent(podiumRoot, false); lg.transform.localPosition = new Vector3(0, 4.5f, -2.5f);
        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 12; l.intensity = 2.2f; l.color = new Color(1f, 0.88f, 0.7f);
        // 4~10등 얼굴 그림 미리 만들기
        for (int i = 3; i < Mathf.Min(10, boardList.Count); i++) CgPortrait(boardList[i].look);
        int mine = boardList.FindIndex(b => NameKey(b.name) == myKey);
        if (mine >= 10) CgPortrait(boardList[mine].look);
        Sfx("record", 0.6f);
        podiumConfetti = 0;
    }

    void ClearPodium()
    {
        foreach (var c in podiumChars) if (c) Destroy(c.gameObject);
        podiumChars.Clear();
        if (podiumRoot) Destroy(podiumRoot.gameObject);
        podiumRoot = null;
    }

    void UpdatePodium(float dt)
    {
        podiumConfetti -= dt;
        if (podiumConfetti <= 0)
        {
            podiumConfetti = 0.06f;
            var ep = new ParticleSystem.EmitParams { position = CgO + PodiumPos + new Vector3(Random.Range(-6f, 6f), 7f, Random.Range(-1f, 2f)), velocity = new Vector3(Random.Range(-0.5f, 0.5f), -2.2f, 0), startLifetime = 3.5f };
            cgFx[Random.Range(0, cgFx.Count)].Emit(ep, 1);
        }
        for (int i = 0; i < podiumChars.Count; i++)
            if (podiumChars[i] && i > 0 && podiumChars[i].runT > 3f) podiumChars[i].SetRunPose(podiumChars[i].runPose == 7 ? 8 : 7);
    }

    Texture CgPortrait(string look)
    {
        if (string.IsNullOrEmpty(look)) look = "bear";
        if (cgPortraits.TryGetValue(look, out var rt) && rt) return rt;
        EnsureIconCam();
        var ch = AcCharacter.Create(CookieLook(look), null);
        ch.transform.position = new Vector3(0, -500 - 0.74f, 0);
        foreach (var tr in ch.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = IconLayer;
        foreach (var r in ch.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        var oldPos = iconCam.transform.position; float oldSize = iconCam.orthographicSize; var oldBg = iconCam.backgroundColor;
        iconCam.transform.position = new Vector3(0, -500f, 2.5f);
        iconCam.orthographicSize = 0.44f; iconCam.farClipPlane = 6f;
        iconCam.backgroundColor = new Color(1f, 0.95f, 0.85f);
        rt = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32) { name = "portrait" }; rt.Create();
        bool done = false;
        try
        {
            var req = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(iconCam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(iconCam, req); done = true; }
        }
        catch { }
        if (!done) { iconCam.targetTexture = rt; iconCam.Render(); iconCam.targetTexture = null; }
        DestroyImmediate(ch.gameObject);
        iconCam.transform.position = oldPos; iconCam.orthographicSize = oldSize; iconCam.farClipPlane = 5f; iconCam.backgroundColor = oldBg;
        cgPortraits[look] = rt;
        return rt;
    }

    void BoardGUI(Rect band, bool portrait)
    {
        float t = cgStateT;
        // 제목 띠
        float ty = portrait ? Mathf.Max(6, band.y - 70) : band.y + 10;
        var tr = new Rect(SW / 2 - 170, ty, 340, 56);
        Pill(new Rect(tr.x, tr.y + 5, tr.width, tr.height), new Color(0.2f, 0.08f, 0.04f, 0.5f));
        Pill(tr, new Color(0.85f, 0.18f, 0.22f));
        CgText(tr, "명예의 전당", 30, new Color(1f, 0.93f, 0.6f), TextAnchor.MiddleCenter, 2.5f, new Color(0.45f, 0.06f, 0.08f));
        // 시상대 위 이름표
        var bx = new[] { 0f, -2.2f, 2.2f }; var bh = new[] { 1.5f, 1.05f, 0.7f };
        var medals = new[] { cgMedalG, cgMedalS, cgMedalB };
        for (int i = 0; i < 3; i++)
        {
            var wp = CgO + PodiumPos + new Vector3(bx[i], 0.3f + bh[i] * 0.5f, -1.05f);
            var sp = cgCam.WorldToScreenPoint(wp); if (sp.z < 0) continue;
            var u = new Vector2(sp.x / UiK, (Screen.height - sp.y) / UiK);
            float ms = i == 0 ? 54 : 44;
            float pop = Mathf.Clamp01((t - 0.2f * (2 - i)) * 3);
            if (pop <= 0) continue;
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), u * UiK);
            GUI.DrawTexture(new Rect(u.x - ms / 2, u.y - ms * 1.31f / 2 - 10, ms, ms * 1.31f), medals[i]);
            if (i == 0 && boardList.Count > 0)
            {   // 1등 머리 위 왕관
                var hp2 = cgCam.WorldToScreenPoint(CgO + PodiumPos + new Vector3(0, 0.3f + bh[0] + 1.25f * 1.35f + Mathf.Sin(Time.time * 3) * 0.05f, 0));
                var hu = new Vector2(hp2.x / UiK, (Screen.height - hp2.y) / UiK);
                GUI.DrawTexture(new Rect(hu.x - 34, hu.y - 44, 68, 51), cgCrown);
            }
            CgText(new Rect(u.x - ms / 2, u.y - 6, ms, ms * 0.6f), (i + 1).ToString(), i == 0 ? 28 : 22, Color.white, TextAnchor.MiddleCenter, 2, new Color(0.35f, 0.2f, 0.05f));
            string nm = i < boardList.Count ? boardList[i].name : "비어 있음";
            string sc = i < boardList.Count ? boardList[i].best.ToString("#,0") + "점" : "";
            var nr = new Rect(u.x - 70, u.y + ms * 0.5f + 8, 140, 30);
            Pill(nr, new Color(0.18f, 0.1f, 0.06f, 0.82f));
            CgText(nr, Short(nm, 7), 17, i < boardList.Count ? Color.white : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, 1.2f);
            if (sc != "") CgText(new Rect(nr.x, nr.yMax, nr.width, 26), sc, 16, new Color(1f, 0.88f, 0.45f), TextAnchor.MiddleCenter, 1.5f);
            GUI.matrix = m;
        }
        // 4~10등
        Rect list;
        if (portrait) list = new Rect(12, band.yMax + 12, SW - 24, SH - band.yMax - 82);
        else list = new Rect(SW - Mathf.Min(320, SW * 0.34f) - 14, band.y + 80, Mathf.Min(320, SW * 0.34f), Mathf.Min(7 * 44 + 8, SH - band.y - 160));
        Pill(new Rect(list.x - 4, list.y - 4, list.width + 8, list.height + 8), new Color(0.18f, 0.09f, 0.05f, 0.55f));
        int rows = 7; float rh = Mathf.Min(44, (list.height - 8) / rows);
        int mine = boardList.FindIndex(b => NameKey(b.name) == myKey);
        for (int k = 0; k < rows; k++)
        {
            int idx = 3 + k;
            var r = new Rect(list.x + 4, list.y + 4 + k * rh, list.width - 8, rh - 4);
            bool me2 = idx == mine;
            Pill(r, me2 ? new Color(1f, 0.88f, 0.55f) : new Color(1f, 0.95f, 0.85f, 0.95f));
            GUI.color = (new Color(0.55f, 0.32f, 0.18f)).linear; GUI.DrawTexture(new Rect(r.x + 4, r.y + 2, r.height - 4, r.height - 4), cgDisc); GUI.color = Color.white;
            CgText(new Rect(r.x + 4, r.y + 2, r.height - 4, r.height - 4), (idx + 1).ToString(), 16, Color.white, TextAnchor.MiddleCenter, 1);
            if (idx < boardList.Count)
            {
                var face = CgPortraitCached(boardList[idx].look);
                if (face != null) GUI.DrawTexture(new Rect(r.x + r.height + 4, r.y + 1, r.height - 2, r.height - 2), face, ScaleMode.ScaleToFit, false);
                var ns = new GUIStyle(CgStyle(17, TextAnchor.MiddleLeft)) { normal = { textColor = new Color(0.42f, 0.25f, 0.15f).linear } };
                GUI.Label(new Rect(r.x + r.height * 2 + 10, r.y, r.width - r.height * 2 - 110, r.height), Short(boardList[idx].name, 9), ns);
                var ss = new GUIStyle(ns) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(r.xMax - 110, r.y, 100, r.height), boardList[idx].best.ToString("#,0") + "점", ss);
            }
            else
            {
                var ns = new GUIStyle(CgStyle(15, TextAnchor.MiddleLeft)) { normal = { textColor = new Color(0.6f, 0.5f, 0.42f).linear } };
                GUI.Label(new Rect(r.x + r.height + 10, r.y, 200, r.height), "-", ns);
            }
        }
        if (mine >= 10)
            CgText(new Rect(list.x, list.yMax + 4, list.width, 24), $"내 순위 {mine + 1}등 · {boardList[mine].best:#,0}점", 16, new Color(1f, 0.9f, 0.6f));
        // 버튼
        float bw = 150, by = portrait ? SH - 62 : SH - 64;
        if (CgButton(new Rect(SW / 2 - bw - 6, by, bw, 50), "달리기 시작!", new Color(0.92f, 0.32f, 0.3f), 19)) StartRun();
        if (CgButton(new Rect(SW / 2 + 6, by, bw, 50), "나가기", new Color(0.45f, 0.55f, 0.65f), 19)) { CloseCookieGame(); return; }
    }

    Texture CgPortraitCached(string look) { if (string.IsNullOrEmpty(look)) look = "bear"; return cgPortraits.TryGetValue(look, out var r) ? r : null; }
}
