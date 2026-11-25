// AcUiTheme.cs - 화면 UI 공통 모양 (11-25 다듬기)
//  · 둥근 모서리 판·버튼 그림을 코드로 만들어 9조각(border)으로 늘려 씀 → 어떤 크기에도 모서리가 찌그러지지 않음
//  · 판: 살짝 비치는 흰 유리판 + 가는 테두리 / 버튼: 둥근 흰 칩(누르면 살짝 진해짐) / 큰 버튼: 크리스마스 빨강
//  · 글자: 짙은 남보라(갈색 대신). 색은 sRGB로 적고 .linear로 넘김(프로젝트가 linear 색 공간)
using UnityEngine;

public static class AcUiTheme
{
    public static readonly Color Ink = new Color(0.20f, 0.18f, 0.30f);        // 기본 글자
    public static readonly Color InkSoft = new Color(0.40f, 0.37f, 0.52f);    // 보조 글자
    public static readonly Color Accent = new Color(0.86f, 0.24f, 0.30f);     // 크리스마스 빨강
    public static readonly Color AccentDark = new Color(0.68f, 0.13f, 0.22f);
    public static readonly Color Gold = new Color(0.98f, 0.78f, 0.35f);

    // 둥근 사각형 그림 (위→아래 색 변화, 테두리, 아래쪽 그림자 띠, 위쪽 반짝임)
    public static Texture2D Round(int size, float radius, Color top, Color bottom, Color border, float borderW, float shadow = 0, float shine = 0)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        float h = size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;                       // 텍스처 y는 아래가 0
                float body = shadow;                                       // 아래쪽 그림자 두께만큼 몸통을 위로 올림
                float d = RoundDist(fx, fy - body, size, h - body, radius);
                float a = Mathf.Clamp01(0.5f - d);                         // 안쪽 1, 바깥 0 (1px 부드러운 가장자리)
                float v = 1f - (fy - body) / (h - body);                   // 위 0 → 아래 1
                var c = Color.Lerp(top, bottom, Mathf.Clamp01(v));
                if (shine > 0 && v < 0.45f) c = Color.Lerp(c, Color.white, shine * (1 - v / 0.45f) * 0.6f);
                if (borderW > 0) { float bw = Mathf.Clamp01(d + borderW + 0.5f); c = Color.Lerp(c, new Color(border.r, border.g, border.b, Mathf.Max(c.a, border.a)), bw * border.a); }
                c.a *= a;
                if (shadow > 0 && a < 1)
                {
                    float ds = RoundDist(fx, fy, size, h - body, radius);   // 아래로 내려간 그림자 모양
                    float sa = Mathf.Clamp01(0.5f - ds) * 0.22f;
                    float outA = c.a + sa * (1 - c.a);
                    if (outA > 0) { c = new Color((c.r * c.a + 0.15f * sa * (1 - c.a)) / outA, (c.g * c.a + 0.1f * sa * (1 - c.a)) / outA, (c.b * c.a + 0.25f * sa * (1 - c.a)) / outA, outA); }
                }
                px[y * size + x] = c;
            }
        t.SetPixels(px); t.Apply();
        return t;
    }
    static float RoundDist(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w / 2) - (w / 2 - r), qy = Mathf.Abs(y - h / 2) - (h / 2 - r);
        return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }

    static Texture2D panel, btn, btnHover, btnDown, big, bigHover, bigDown, chip;
    public static void Build()
    {
        if (panel) return;
        // 판: 거의 흰 유리 (살짝 비침), 가는 흰 테두리
        panel = Round(48, 16, new Color(1f, 1f, 1f, 0.90f), new Color(0.96f, 0.96f, 0.99f, 0.88f), new Color(1f, 1f, 1f, 0.95f), 1.2f);
        // 버튼: 흰 칩 + 아래 그림자
        btn = Round(40, 12, new Color(1f, 1f, 1f, 0.97f), new Color(0.93f, 0.92f, 0.97f, 0.97f), new Color(0.80f, 0.78f, 0.90f, 0.9f), 1f, 2f, 0.5f);
        btnHover = Round(40, 12, new Color(1f, 1f, 1f, 1f), new Color(0.97f, 0.96f, 1f, 1f), new Color(0.70f, 0.66f, 0.86f, 1f), 1.2f, 2f, 0.6f);
        btnDown = Round(40, 12, new Color(0.88f, 0.86f, 0.95f, 1f), new Color(0.92f, 0.90f, 0.97f, 1f), new Color(0.66f, 0.62f, 0.82f, 1f), 1.2f, 0f, 0f);
        // 큰 버튼: 빨강
        big = Round(48, 16, new Color(0.95f, 0.38f, 0.40f), AccentDark, new Color(1f, 0.85f, 0.6f, 0.9f), 1.5f, 3f, 0.55f);
        bigHover = Round(48, 16, new Color(0.98f, 0.45f, 0.45f), new Color(0.75f, 0.18f, 0.26f), new Color(1f, 0.9f, 0.65f, 1f), 1.8f, 3f, 0.65f);
        bigDown = Round(48, 16, AccentDark, new Color(0.82f, 0.25f, 0.30f), new Color(1f, 0.85f, 0.6f, 0.9f), 1.5f, 0f, 0f);
        chip = Round(32, 10, new Color(1f, 1f, 1f, 0.9f), new Color(0.96f, 0.96f, 1f, 0.9f), new Color(1f, 1f, 1f, 1f), 1f);
    }
    public static Texture2D Glow;   // 큰 버튼 둘레의 은은한 빛
    public static Texture2D GlowTex()
    {
        if (Glow) return Glow;
        Glow = Round(64, 26, new Color(1f, 0.85f, 0.45f, 0.9f), new Color(1f, 0.6f, 0.4f, 0.9f), Color.clear, 0);
        return Glow;
    }

    public static GUIStyle Panel(GUIStyle baseBox)
    {
        Build();
        return new GUIStyle(baseBox) { normal = { background = panel }, border = new RectOffset(17, 17, 17, 17), padding = new RectOffset(14, 14, 10, 10) };
    }
    public static GUIStyle Button(GUIStyle baseBtn, int size)
    {
        Build();
        var ink = Ink.linear;
        return new GUIStyle(baseBtn)
        {
            fontSize = size, richText = true, alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(13, 13, 13, 15), padding = new RectOffset(8, 8, 4, 6),
            normal = { background = btn, textColor = ink }, hover = { background = btnHover, textColor = ink },
            active = { background = btnDown, textColor = ink }, focused = { background = btn, textColor = ink },
            onNormal = { background = btnDown, textColor = ink }, onHover = { background = btnDown, textColor = ink }, onActive = { background = btnDown, textColor = ink },
        };
    }
    public static GUIStyle BigButton(GUIStyle baseBtn, int size)
    {
        Build();
        var w = Color.white.linear;
        return new GUIStyle(baseBtn)
        {
            fontSize = size, richText = true, alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(17, 17, 17, 20), padding = new RectOffset(10, 10, 4, 7),
            normal = { background = big, textColor = w }, hover = { background = bigHover, textColor = w },
            active = { background = bigDown, textColor = w }, focused = { background = big, textColor = w },
        };
    }
    public static GUIStyle Card(Color tint)
    {
        Build();
        var t = Round(32, 10, new Color(tint.r, tint.g, tint.b, 0.95f), new Color(tint.r * 0.97f, tint.g * 0.97f, tint.b, 0.95f), new Color(1f, 1f, 1f, 0.9f), 1f);
        return new GUIStyle(GUIStyle.none) { normal = { background = t }, border = new RectOffset(11, 11, 11, 11), padding = new RectOffset(8, 8, 4, 6), margin = new RectOffset(0, 0, 2, 4) };
    }
    // 둥근 테두리 / 둥근 채움 (선택 표시 등, 네모 대신)
    static Texture2D ringTex, fillTex; static GUIStyle ringSt, fillSt;
    public static void Ring(Rect r, Color c, float pad = 3)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!ringTex) { ringTex = Round(40, 13, Color.clear, Color.clear, Color.white, 3f); ringSt = new GUIStyle { normal = { background = ringTex }, border = new RectOffset(14, 14, 14, 14) }; }
        var k = GUI.color; GUI.color = c.linear;
        ringSt.Draw(new Rect(r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2), false, false, false, false);
        GUI.color = k;
    }
    public static void Fill(Rect r, Color c, float pad = 0)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!fillTex) { fillTex = Round(40, 12, Color.white, Color.white, Color.clear, 0); fillSt = new GUIStyle { normal = { background = fillTex }, border = new RectOffset(13, 13, 13, 13) }; }
        var k = GUI.color; GUI.color = c.linear;
        fillSt.Draw(new Rect(r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2), false, false, false, false);
        GUI.color = k;
    }

    // 큰 버튼 둘레 빛 (네모난 깜빡이 테두리 대신)
    public static void DrawGlow(Rect r, float pulse)
    {
        if (Event.current.type != EventType.Repaint) return;
        var g = GlowTex();
        var st = new GUIStyle { normal = { background = g }, border = new RectOffset(28, 28, 28, 28) };
        var k = GUI.color; GUI.color = new Color(1, 1, 1, 0.35f + 0.35f * pulse);
        float e = 5 + 2 * pulse;
        st.Draw(new Rect(r.x - e, r.y - e, r.width + e * 2, r.height + e * 2), false, false, false, false);
        GUI.color = k;
    }
}
