// AcGift.cs - 선물 포장실: 선물 만들기 → 들고 다니다 친구 옆에서 전달 → 받은 사람이 풀어보기 (11-26)
// 만들기: 내용물(36가지, 움직이는 것들 + 내 캐릭터 미니어처) / 상자 크기 / 포장지 색·무늬·무늬 색 / 리본 색·무늬·모양 / 카드 한마디
// 풀어보기: 따로 떨어진 '선물 무대'에서 크게. 리본을 3번 끌어 풀고, 포장지를 문질러 찢고, '상자 열기!' → 내용물이 짠!
// 저장: 들고 있는 선물 = PlayerPrefs gift_held (최대 3개)
//       보낸 선물 = orn/x~gift~<id> = {id, from, fromName, to, toName, ct, opened, d(GiftData json)}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable] public class GiftData
{
    public string item = "teddy_bear", paper = "E53935", pc2 = "", ribbon = "FDD835", msg = "", look = "", from = "";
    public int box = 1, pat = 1, rpat = 0, bow = 0;
}
[Serializable] public class GiftHeldList { public List<GiftData> g = new List<GiftData>(); }

public partial class AcRoom
{
    class GiftItem
    {
        public string id, ko, anim; public int cat; public float h;
        public GiftItem(string id, string ko, int cat, float h, string anim) { this.id = id; this.ko = ko; this.cat = cat; this.h = h; this.anim = anim; }
    }
    static readonly string[] GiftCatKo = { "귀여운 것", "예쁜 것", "깜짝 놀래기", "맛있는 것" };
    static readonly GiftItem[] GiftItems = {
        new GiftItem("mini_me", "내 캐릭터 미니어처", 0, 0.5f, "dance"),
        new GiftItem("pet_cat", "아기 고양이", 0, 0.42f, "dance"),
        new GiftItem("pet_rabbit", "아기 토끼", 0, 0.45f, "dance"),
        new GiftItem("pet_panda", "아기 판다", 0, 0.42f, "dance"),
        new GiftItem("pet_fox", "아기 여우", 0, 0.42f, "dance"),
        new GiftItem("pet_dog", "아기 강아지", 0, 0.42f, "dance"),
        new GiftItem("p_penguin", "아기 펭귄", 0, 0.36f, "waddle"),
        new GiftItem("p_chick", "아기 병아리", 0, 0.24f, "hop"),
        new GiftItem("teddy_bear", "곰 인형", 0, 0.4f, "hop"),
        new GiftItem("reindeer_plush", "루돌프 인형", 0, 0.45f, "sway"),
        new GiftItem("gingerbread_man", "진저브레드맨", 0, 0.45f, "hop"),
        new GiftItem("snowman_small", "꼬마 눈사람", 0, 0.42f, "sway"),
        new GiftItem("p_bouquet", "꽃다발", 1, 0.5f, "bloom"),
        new GiftItem("p_crown", "반짝 왕관", 1, 0.22f, "spin"),
        new GiftItem("p_ring", "보석 반지", 1, 0.2f, "spin"),
        new GiftItem("snow_globe", "스노우볼", 1, 0.32f, "spin"),
        new GiftItem("star_lantern", "별 램프", 1, 0.36f, "glow"),
        new GiftItem("wreath", "크리스마스 리스", 1, 0.42f, "spin"),
        new GiftItem("ice_sculpture", "얼음 별 조각", 1, 0.5f, "spin"),
        new GiftItem("p_rainbow", "무지개", 1, 0.36f, "glow"),
        new GiftItem("p_balloons", "하트 풍선", 1, 0.7f, "float"),
        new GiftItem("p_minitree", "미니 트리", 1, 0.48f, "glow"),
        new GiftItem("poinsettia", "포인세티아", 1, 0.36f, "sway"),
        new GiftItem("p_starwand", "별 요술봉", 1, 0.5f, "spin"),
        new GiftItem("p_jack", "깜짝 용수철 상자", 2, 0.55f, "boing"),
        new GiftItem("p_popflower", "깜짝 꽃", 2, 0.5f, "boing"),
        new GiftItem("gift_box", "선물 속 선물", 2, 0.3f, "hop"),
        new GiftItem("bell_pair", "딸랑 종", 2, 0.36f, "swing"),
        new GiftItem("toy_train", "칙칙폭폭 기차", 2, 0.26f, "drive"),
        new GiftItem("nutcracker", "호두까기 병정", 2, 0.5f, "hop"),
        new GiftItem("p_cupcake", "컵케이크", 3, 0.28f, "bob"),
        new GiftItem("p_donut", "딸기 도넛", 3, 0.14f, "spin"),
        new GiftItem("cocoa_mug", "코코아", 3, 0.22f, "bob"),
        new GiftItem("cookie_plate", "쿠키와 우유", 3, 0.2f, "bob"),
        new GiftItem("candy_cane_big", "지팡이 사탕", 3, 0.45f, "spin"),
        new GiftItem("gingerbread_woman", "진저브레드 아가씨", 3, 0.45f, "hop"),
    };
    static GiftItem GiftItemById(string id) => GiftItems.FirstOrDefault(g => g.id == id) ?? GiftItems[8];

    static readonly string[] BoxKo = { "작은 상자", "보통 상자", "큰 상자", "길쭉한 상자", "납작한 상자" };
    static readonly Vector3[] BoxSize = { new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0.42f, 0.38f, 0.42f), new Vector3(0.56f, 0.48f, 0.56f), new Vector3(0.28f, 0.6f, 0.28f), new Vector3(0.58f, 0.2f, 0.44f) };
    static readonly string[] PatKo = { "민무늬", "물방울", "줄무늬", "체크", "별", "눈송이", "하트", "지그재그", "반짝이" };
    static readonly string[] RPatKo = { "민무늬", "줄무늬", "물방울", "금테", "체크" };
    static readonly string[] BowKo = { "나비 리본", "꽃 리본", "큰 리본", "끈만 묶기" };
    static readonly string[] MotifKo = { "자동", "하양", "금색", "빨강", "초록", "파랑", "분홍", "남색" };
    static readonly Color[] MotifCols = { Color.clear, new Color(1f, 1f, 1f), new Color(1f, 0.8f, 0.25f), new Color(0.86f, 0.15f, 0.2f), new Color(0.2f, 0.55f, 0.3f), new Color(0.25f, 0.5f, 0.9f), new Color(1f, 0.6f, 0.75f), new Color(0.16f, 0.2f, 0.45f) };
    static readonly Vector3 StudioPos = new Vector3(4000, 0, 4000);
    const float TableY = 0.75f;

    enum GiftMode { None, Wrap, Unwrap }
    GiftMode giftMode;
    bool GiftBusy => giftMode != GiftMode.None;
    bool InGiftRoom => phase == Phase.Play && me != null && !Outside && AreaAt(me.transform.position) == "선물 포장실";

    // ===== 들고 있는 선물 =====
    GiftHeldList held;
    GiftHeldList Held { get { if (held == null) { try { held = JsonUtility.FromJson<GiftHeldList>(PlayerPrefs.GetString("gift_held", "")); } catch { } if (held == null) held = new GiftHeldList(); } return held; } }
    GiftData HeldGift => Held.g.Count > 0 ? Held.g[0] : null;
    void SaveHeld() { PlayerPrefs.SetString("gift_held", JsonUtility.ToJson(Held)); PlayerPrefs.Save(); }

    // ===== 무늬 그림 =====
    readonly Dictionary<string, Texture2D> giftTex = new Dictionary<string, Texture2D>();
    static float Hash2(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
    static Color AutoMotif(Color b) { float lum = 0.3f * b.r + 0.59f * b.g + 0.11f * b.b; return lum > 0.62f ? new Color(0.86f, 0.18f, 0.22f) : Color.white; }

    static float PatCover(int pat, float u, float v, float px)
    {
        float S(float d) => Mathf.Clamp01(0.5f - d / px);   // d<0 안쪽
        switch (pat)
        {
            case 1: // 물방울
                {
                    float best = 9;
                    foreach (var c in new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Vector2(0.75f, -0.25f), new Vector2(-0.25f, 0.75f), new Vector2(1.25f, 0.25f), new Vector2(0.25f, 1.25f) })
                        best = Mathf.Min(best, new Vector2(u - c.x, v - c.y).magnitude);
                    return S(best - 0.11f);
                }
            case 2: { float f = Mathf.Repeat((u + v) * 3f, 1f); return S(Mathf.Abs(f - 0.25f) * (1f / 3f) / 1.41f - 0.083f / 1.41f); }
            case 3: { float a = S(Mathf.Abs(Mathf.Repeat(u * 4, 1) - 0.5f) / 4 - 0.07f), b = S(Mathf.Abs(Mathf.Repeat(v * 4, 1) - 0.5f) / 4 - 0.07f); return Mathf.Clamp01(a * 0.5f + b * 0.5f + a * b * 0.3f); }
            case 4: // 별
                {
                    float best = 0;
                    foreach (var c in new[] { new Vector2(0.27f, 0.3f), new Vector2(0.77f, 0.8f) })
                        for (int ox = -1; ox <= 1; ox++) for (int oy = -1; oy <= 1; oy++)
                            {
                                var d = new Vector2(u - c.x - ox, v - c.y - oy); float r = d.magnitude; if (r > 0.2f) continue;
                                float th = Mathf.Atan2(d.y, d.x) - Mathf.PI / 2;
                                float lim = Mathf.Lerp(0.065f, 0.15f, Mathf.Pow(Mathf.Abs(Mathf.Cos(th * 2.5f)), 4f));
                                best = Mathf.Max(best, S(r - lim));
                            }
                    return best;
                }
            case 5: // 눈송이
                {
                    float best = 0;
                    foreach (var c in new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f) })
                        for (int ox = -1; ox <= 1; ox++) for (int oy = -1; oy <= 1; oy++)
                            {
                                var d = new Vector2(u - c.x - ox, v - c.y - oy); if (d.magnitude > 0.18f) continue;
                                float dm = 9;
                                for (int k = 0; k < 6; k++)
                                {
                                    float a = k * Mathf.PI / 3; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                                    float t = Mathf.Clamp(Vector2.Dot(d, dir), 0, 0.14f); dm = Mathf.Min(dm, (d - dir * t).magnitude);
                                    var bp = dir * 0.085f; var b1 = new Vector2(Mathf.Cos(a + 0.7f), Mathf.Sin(a + 0.7f)); var b2 = new Vector2(Mathf.Cos(a - 0.7f), Mathf.Sin(a - 0.7f));
                                    float t1 = Mathf.Clamp(Vector2.Dot(d - bp, b1), 0, 0.045f); dm = Mathf.Min(dm, (d - bp - b1 * t1).magnitude);
                                    float t2 = Mathf.Clamp(Vector2.Dot(d - bp, b2), 0, 0.045f); dm = Mathf.Min(dm, (d - bp - b2 * t2).magnitude);
                                }
                                best = Mathf.Max(best, S(dm - 0.012f));
                            }
                    return best;
                }
            case 6: // 하트
                {
                    float best = 0;
                    foreach (var c in new[] { new Vector2(0.27f, 0.27f), new Vector2(0.77f, 0.77f) })
                        for (int ox = -1; ox <= 1; ox++) for (int oy = -1; oy <= 1; oy++)
                            {
                                float x = (u - c.x - ox) / 0.11f, y = (v - c.y - oy) / 0.11f + 0.15f;
                                if (Mathf.Abs(x) > 2 || Mathf.Abs(y) > 2) continue;
                                float q = x * x + y * y - 1; float f = q * q * q - x * x * y * y * y;
                                best = Mathf.Max(best, f <= 0 ? 1f : Mathf.Clamp01(1 - f * 6f));
                            }
                    return best;
                }
            case 7: { float z = Mathf.Abs(Mathf.Repeat(u * 4, 1) - 0.5f) * 0.25f; float f = Mathf.Repeat(v * 4 + z * 4, 1); return S(Mathf.Abs(f - 0.3f) / 4 - 0.045f); }
            case 8: { int ix = Mathf.FloorToInt(u * 40), iy = Mathf.FloorToInt(v * 40); float h = Hash2(ix, iy); return h > 0.86f ? 1f : h > 0.8f ? 0.5f : 0f; }
            default: return 0;
        }
    }

    Texture2D PaperTex(Color baseC, Color motif, int pat)
    {
        string key = "p" + ColorUtility.ToHtmlStringRGB(baseC) + ColorUtility.ToHtmlStringRGB(motif) + pat;
        if (giftTex.TryGetValue(key, out var tx) && tx) return tx;
        const int N = 128;
        tx = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2, name = key };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float c = PatCover(pat, u, v, 1.6f / N);
                var col = Color.Lerp(baseC, motif, c);
                if (pat == 0 || pat == 3) col *= 0.97f + 0.03f * Hash2(x, y);   // 종이 결
                px[y * N + x] = col;
            }
        tx.SetPixels32(px); tx.Apply(true);
        giftTex[key] = tx;
        return tx;
    }

    Texture2D RibbonTex(Color c, int pat)
    {
        string key = "r" + ColorUtility.ToHtmlStringRGB(c) + pat;
        if (giftTex.TryGetValue(key, out var tx) && tx) return tx;
        const int W = 32, H = 64;
        tx = new Texture2D(W, H, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = key };
        var px = new Color32[W * H];
        var light = Color.Lerp(c, Color.white, 0.75f); var gold = new Color(1f, 0.82f, 0.3f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                var col = c * (0.9f + 0.1f * Mathf.Sin(u * Mathf.PI));   // 가운데가 살짝 밝게 (윤기)
                switch (pat)
                {
                    case 1: if (Mathf.Abs(u - 0.2f) < 0.06f || Mathf.Abs(u - 0.8f) < 0.06f) col = light; break;
                    case 2: { var d = new Vector2(u - 0.5f, (Mathf.Repeat(v * 2, 1) - 0.5f) * 0.5f); if (d.magnitude < 0.16f) col = light; break; }
                    case 3: if (u < 0.12f || u > 0.88f) col = gold; break;
                    case 4: { bool a = Mathf.Repeat(u * 3, 1) < 0.5f, b = Mathf.Repeat(v * 6, 1) < 0.5f; if (a ^ b) col = Color.Lerp(c, light, 0.6f); break; }
                }
                col.a = 1; px[y * W + x] = col;
            }
        tx.SetPixels32(px); tx.Apply(true);
        giftTex[key] = tx;
        return tx;
    }

    Shader texShader;
    Material TexMat(Texture2D t)
    {
        if (texShader == null) { texShader = Resources.Load<Shader>("ACToonTex"); if (texShader == null) texShader = Shader.Find("XmasParty/ACToonTex"); }
        var m = new Material(texShader != null ? texShader : AcmLoader.Mat.shader) { name = "giftTex" };
        if (texShader != null) m.SetTexture("_MainTex", t);
        return m;
    }

    // ===== 선물 상자 만들기 =====
    class PaperPiece { public GameObject go; public Vector3 n, c; public bool torn, marked; }
    class GiftBox { public Transform root; public GameObject baseObj, lid, bandX, bandZ, bow; public List<PaperPiece> paper = new List<PaperPiece>(); public Vector3 size; public readonly List<Material> mats = new List<Material>(); }

    static void Face(AcPB b, Vector3 c, Vector3 n, Vector3 u, Vector3 w, float su, float sw, Color col, bool uvMeters = false, float uvK = 5f)
    {
        var hu = u * su * 0.5f; var hw = w * sw * 0.5f;
        Vector2 U(float a, float bb) => uvMeters ? new Vector2((Vector3.Dot(c + u * (a - 0.5f) * su, u)) * uvK, (Vector3.Dot(c + w * (bb - 0.5f) * sw, w)) * uvK) : new Vector2(1, 0);
        int a0 = b.V(c - hu - hw, n, col, U(0, 0)), a1 = b.V(c + hu - hw, n, col, U(1, 0)), a2 = b.V(c + hu + hw, n, col, U(1, 1)), a3 = b.V(c - hu + hw, n, col, U(0, 1));
        b.Quad(a0, a1, a2, a3);
    }

    GiftBox BuildGiftBox(GiftData d, Transform parent)
    {
        var gb = new GiftBox { size = BoxSize[Mathf.Clamp(d.box, 0, BoxSize.Length - 1)] };
        float W = gb.size.x, H = gb.size.y, D = gb.size.z;
        gb.root = new GameObject("GiftBox").transform; gb.root.SetParent(parent, false);
        var paperC = Hex(d.paper); var motif = d.pc2 == "" ? AutoMotif(paperC) : Hex(d.pc2);
        var ribC = Hex(d.ribbon);
        // 속 상자 (뚜껑 없는 상자) + 뚜껑
        float lidH = Mathf.Min(0.1f, H * 0.28f);
        var inner = new AcPB();
        Color cream = new Color(0.98f, 0.95f, 0.9f), inside = new Color(0.86f, 0.8f, 0.72f);
        float hb = H - lidH * 0.55f;
        Face(inner, new Vector3(0, 0.002f, 0), Vector3.down, Vector3.right, Vector3.forward, W, D, cream * 0.9f);
        Face(inner, new Vector3(0, 0.006f, 0), Vector3.up, Vector3.right, Vector3.forward, W - 0.01f, D - 0.01f, inside);
        foreach (var (n, u, sz) in new[] { (Vector3.forward, Vector3.right, W), (Vector3.back, Vector3.right, W), (Vector3.right, Vector3.forward, D), (Vector3.left, Vector3.forward, D) })
        {
            float off = (n.x != 0 ? W : D) * 0.5f;
            Face(inner, n * off + Vector3.up * hb * 0.5f, n, u, Vector3.up, sz, hb, cream);
            Face(inner, n * (off - 0.005f) + Vector3.up * hb * 0.5f, -n, u, Vector3.up, sz - 0.01f, hb, inside);
        }
        gb.baseObj = ProcObj("innerBox", inner.M("innerBox"), gb.root);
        var lb = new AcPB();
        lb.Box(new Vector3(0, lidH * 0.5f, 0), new Vector3(W + 0.016f, lidH, D + 0.016f), Color.Lerp(paperC, Color.white, 0.55f));
        gb.lid = ProcObj("lid", lb.M("lid"), gb.root);
        gb.lid.transform.localPosition = new Vector3(0, H - lidH, 0);
        // 포장지 조각 (면마다 2×2)
        var pmat = TexMat(PaperTex(paperC, motif, d.pat)); gb.mats.Add(pmat);
        float o = 0.012f;
        var faces = new List<(Vector3 n, Vector3 u, Vector3 w, float su, float sw, Vector3 c)>
        {
            (Vector3.up, Vector3.right, Vector3.forward, W + 0.02f, D + 0.02f, new Vector3(0, H + o * 0.5f, 0)),
            (Vector3.forward, Vector3.right, Vector3.up, W + 0.02f, H + 0.01f, new Vector3(0, H * 0.5f, D * 0.5f + o)),
            (Vector3.back, Vector3.right, Vector3.up, W + 0.02f, H + 0.01f, new Vector3(0, H * 0.5f, -D * 0.5f - o)),
            (Vector3.right, Vector3.forward, Vector3.up, D + 0.02f, H + 0.01f, new Vector3(W * 0.5f + o, H * 0.5f, 0)),
            (Vector3.left, Vector3.forward, Vector3.up, D + 0.02f, H + 0.01f, new Vector3(-W * 0.5f - o, H * 0.5f, 0)),
        };
        int pi = 0;
        foreach (var f in faces)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    var pc = f.c + f.u * (f.su * (i - 0.5f) * 0.5f) + f.w * (f.sw * (j - 0.5f) * 0.5f) + f.n * (0.0007f * (pi % 4));
                    var b = new AcPB();
                    // 가운데를 중심으로 만들고 물체 위치를 조각 가운데로 (찢을 때 그 자리에서 날아가게)
                    float su = f.su * 0.5f + 0.006f, sw = f.sw * 0.5f + 0.006f;
                    var hu = f.u * su * 0.5f; var hw = f.w * sw * 0.5f;
                    Vector2 U(Vector3 p) => new Vector2(Vector3.Dot(pc + p, f.u) * 5f, Vector3.Dot(pc + p, f.w) * 5f);
                    // 가장자리를 살짝 들쭉날쭉하게 (종이 느낌)
                    int a0 = b.V(-hu - hw, f.n, Color.white, U(-hu - hw)), a1 = b.V(hu - hw, f.n, Color.white, U(hu - hw)), a2 = b.V(hu + hw, f.n, Color.white, U(hu + hw)), a3 = b.V(-hu + hw, f.n, Color.white, U(-hu + hw));
                    b.Quad(a0, a1, a2, a3);
                    var go = new GameObject("paper" + pi);
                    go.transform.SetParent(gb.root, false); go.transform.localPosition = pc;
                    go.AddComponent<MeshFilter>().sharedMesh = b.M("paper");
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = pmat; r.shadowCastingMode = ShadowCastingMode.Off;
                    gb.paper.Add(new PaperPiece { go = go, n = f.n, c = pc });
                    pi++;
                }
        // 리본 띠 두 줄 (바닥 빼고 세 면)
        var rmat = TexMat(RibbonTex(ribC, d.rpat)); gb.mats.Add(rmat);
        float rw = Mathf.Clamp(Mathf.Min(W, D) * 0.14f, 0.035f, 0.07f), ro = 0.02f;
        GameObject Band(bool alongX)
        {
            var b = new AcPB();
            var axis = alongX ? Vector3.right : Vector3.forward;   // 띠가 지나가는 방향
            var across = alongX ? Vector3.forward : Vector3.right;
            float half = (alongX ? W : D) * 0.5f + ro;
            void Strip(Vector3 c, Vector3 n, Vector3 len, float L, float v0)
            {
                var hl = len * L * 0.5f; var hw = across * rw * 0.5f;
                int a0 = b.V(c - hl - hw, n, Color.white, new Vector2(0, v0)), a1 = b.V(c + hl - hw, n, Color.white, new Vector2(0, v0 + L * 8)),
                    a2 = b.V(c + hl + hw, n, Color.white, new Vector2(1, v0 + L * 8)), a3 = b.V(c - hl + hw, n, Color.white, new Vector2(1, v0));
                b.Quad(a0, a1, a2, a3);
            }
            float ty = H + ro + (alongX ? 0.001f : 0.002f);
            Strip(new Vector3(0, ty, 0), Vector3.up, axis, half * 2, 0);
            Strip(axis * half + Vector3.up * (H * 0.5f + ro * 0.5f), axis, Vector3.up, H + ro, 0);
            Strip(-axis * half + Vector3.up * (H * 0.5f + ro * 0.5f), -axis, Vector3.up, H + ro, 0);
            var go = new GameObject(alongX ? "bandX" : "bandZ"); go.transform.SetParent(gb.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.M("band");
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = rmat; r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
        gb.bandX = Band(true); gb.bandZ = Band(false);
        // 리본 매듭
        float sf = Mathf.Clamp(Mathf.Min(W, D) / 0.42f, 0.7f, 1.3f);
        var bowB = new AcPB();
        var dark = ribC * 0.85f;
        void Loop(float yaw, float R, float tilt)
        {
            var q = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, tilt);
            bowB.xf = Matrix4x4.TRS(q * new Vector3(R * 0.9f, R * 0.55f, 0), q, new Vector3(1f, 1f, 0.32f));
            bowB.Torus(Vector3.zero, Vector3.forward, R, R * 0.28f, ribC, 18, 6);
            bowB.xf = Matrix4x4.identity;
        }
        int style = Mathf.Clamp(d.bow, 0, BowKo.Length - 1);
        if (style == 0) { Loop(0, 0.055f, 25); Loop(180, 0.055f, 25); }
        else if (style == 1) { for (int k = 0; k < 8; k++) Loop(k * 45, 0.045f, 40 + (k % 2) * 15); for (int k = 0; k < 5; k++) Loop(k * 72 + 20, 0.03f, 70); }
        else if (style == 2) { Loop(0, 0.085f, 20); Loop(180, 0.085f, 20); }
        if (style != 3 || true) bowB.Ellipsoid(new Vector3(0, 0.015f, 0), new Vector3(0.025f, 0.02f, 0.022f), dark, 6, 10);
        // 꼬리: 상자 윗면 위에 눕혀서 (큰 리본만 앞으로 늘어짐)
        float tailL = style == 2 ? 0.16f : style == 3 ? 0.05f : 0.07f;
        bowB.Leaf(new Vector3(0, 0.006f, 0), new Vector3(0.5f, 0f, -0.85f), new Vector3(0.85f, 0f, 0.5f), tailL, 0.035f, ribC, dark, 0.02f, 2, 6, style == 2 ? 0.5f : 0.02f);
        bowB.Leaf(new Vector3(0, 0.006f, 0), new Vector3(-0.5f, 0f, -0.85f), new Vector3(0.85f, 0f, -0.5f), tailL, 0.035f, ribC, dark, 0.02f, 2, 6, style == 2 ? 0.5f : 0.02f);
        gb.bow = ProcObj("bow", bowB.M("bow"), gb.root);
        gb.bow.transform.localPosition = new Vector3(0, H + ro + 0.004f, 0);
        gb.bow.transform.localScale = Vector3.one * sf;
        return gb;
    }

    void DestroyGiftBox(GiftBox gb)
    {
        if (gb == null) return;
        foreach (var m in gb.mats) if (m) Destroy(m);
        if (gb.root) Destroy(gb.root.gameObject);
    }

    // ===== 내용물 만들기 =====
    static AcLook PetLook(string sp)
    {
        var l = new AcLook { species = sp };
        switch (sp)
        {
            case "cat": l.fur = new Color(0.98f, 0.7f, 0.4f); l.hat = "none"; l.sweater = new Color(1f, 0.6f, 0.75f); l.scarf = Color.white; break;
            case "rabbit": l.fur = new Color(0.98f, 0.97f, 0.95f); l.hat = "earmuffs"; l.sweater = new Color(0.5f, 0.85f, 0.75f); l.hatColor = new Color(1f, 0.6f, 0.75f); break;
            case "panda": l.fur = new Color(0.98f, 0.98f, 0.97f); l.hat = "beanie"; l.sweater = new Color(0.86f, 0.2f, 0.22f); l.hatColor = new Color(0.3f, 0.55f, 0.9f); break;
            case "fox": l.fur = new Color(0.95f, 0.55f, 0.25f); l.hat = "santa"; l.sweater = new Color(0.25f, 0.6f, 0.35f); break;
            default: l.fur = new Color(0.92f, 0.8f, 0.6f); l.hat = "antlers"; l.sweater = new Color(0.35f, 0.6f, 0.95f); break;
        }
        return l;
    }

    GameObject MakeGiftItem(string id, string look)
    {
        var def = GiftItemById(id);
        GameObject g;
        if (id == "mini_me" || id.StartsWith("pet_", StringComparison.Ordinal))
        {
            AcLook l = id == "mini_me" ? (string.IsNullOrEmpty(look) ? me.look : AcLook.Decode(look)) : PetLook(id.Substring(4));
            var c = AcCharacter.Create(l, null);
            g = c.gameObject;
        }
        else if (id.StartsWith("p_", StringComparison.Ordinal)) g = ProcItem(id);
        else g = AcmLoader.Exists(id) ? AcCatalog.Spawn(id, null, id == "gift_box" ? new Dictionary<string, Color> { { "box", new Color(0.3f, 0.6f, 0.95f) }, { "ribbon", new Color(1f, 0.85f, 0.3f) } } : null) : ProcItem("p_cupcake");
        var holder = FitHolder(g, def.h, "item_" + id);
        foreach (var r in holder.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        return holder;
    }

    // 코드로 만든 내용물 (움직이는 부분은 이름 붙인 자식)
    GameObject ProcItem(string id)
    {
        var root = new GameObject(id);
        AcPB b = new AcPB();
        void Part(string name, AcPB pb, Vector3 pos, float emit = 0) { var go = ProcObj(name, pb.M(name), root.transform, emit); go.transform.localPosition = pos; }
        Color white = new Color(0.98f, 0.98f, 0.97f), black = new Color(0.12f, 0.12f, 0.16f), orange = new Color(1f, 0.6f, 0.15f), pinkC = new Color(1f, 0.6f, 0.7f);
        void Face2(AcPB pb, Vector3 c, float s, float eyeGap = 0.35f)
        {   // 귀여운 얼굴 (점 눈 + 볼터치 + 웃는 입) — c: 얼굴 앞 가운데, +z 방향
            pb.Sphere(c + new Vector3(-eyeGap * s, 0.1f * s, 0), 0.1f * s, black, 5, 7);
            pb.Sphere(c + new Vector3(eyeGap * s, 0.1f * s, 0), 0.1f * s, black, 5, 7);
            pb.Sphere(c + new Vector3(-eyeGap * s + 0.03f * s, 0.14f * s, 0.06f * s), 0.03f * s, white, 3, 4);
            pb.Sphere(c + new Vector3(eyeGap * s + 0.03f * s, 0.14f * s, 0.06f * s), 0.03f * s, white, 3, 4);
            pb.Ellipsoid(c + new Vector3(-0.55f * s, -0.12f * s, -0.04f * s), new Vector3(0.12f, 0.07f, 0.03f) * s, pinkC, 4, 6);
            pb.Ellipsoid(c + new Vector3(0.55f * s, -0.12f * s, -0.04f * s), new Vector3(0.12f, 0.07f, 0.03f) * s, pinkC, 4, 6);
            var pts = new List<Vector3>(); var rs = new List<float>();
            for (int i = 0; i <= 8; i++) { float a = Mathf.PI * (0.15f + 0.7f * i / 8f); pts.Add(c + new Vector3(Mathf.Cos(a) * 0.18f * s, -0.12f * s - Mathf.Sin(a) * 0.12f * s, 0.01f * s)); rs.Add(0.025f * s); }
            pb.Tube(pts, rs, black, 5);
        }
        switch (id)
        {
            case "p_penguin":
                b.Ellipsoid(new Vector3(0, 0.17f, 0), new Vector3(0.13f, 0.17f, 0.12f), black, 10, 14);
                b.Ellipsoid(new Vector3(0, 0.15f, 0.05f), new Vector3(0.1f, 0.13f, 0.08f), white, 8, 12);
                b.Ellipsoid(new Vector3(0, 0.3f, 0.01f), new Vector3(0.1f, 0.09f, 0.09f), black, 8, 12);
                b.Ellipsoid(new Vector3(0, 0.29f, 0.05f), new Vector3(0.075f, 0.06f, 0.05f), white, 6, 10);
                Face2(b, new Vector3(0, 0.3f, 0.1f), 0.09f, 0.33f);
                b.Cylinder(new Vector3(0, 0.285f, 0.095f), new Vector3(0, 0.28f, 0.13f), 0.016f, 0.002f, orange, 6);
                b.Ellipsoid(new Vector3(-0.05f, 0.01f, 0.05f), new Vector3(0.035f, 0.012f, 0.05f), orange, 4, 8);
                b.Ellipsoid(new Vector3(0.05f, 0.01f, 0.05f), new Vector3(0.035f, 0.012f, 0.05f), orange, 4, 8);
                b.Ellipsoid(new Vector3(-0.13f, 0.17f, 0), new Vector3(0.025f, 0.09f, 0.05f), black, 5, 8, Quaternion.Euler(0, 0, -15));
                b.Ellipsoid(new Vector3(0.13f, 0.17f, 0), new Vector3(0.025f, 0.09f, 0.05f), black, 5, 8, Quaternion.Euler(0, 0, 15));
                b.Torus(new Vector3(0, 0.235f, 0.005f), Vector3.up, 0.095f, 0.022f, new Color(0.9f, 0.2f, 0.25f), 16, 6);
                Part("body", b, Vector3.zero);
                break;
            case "p_chick":
                {
                    var yel = new Color(1f, 0.85f, 0.3f);
                    b.Sphere(new Vector3(0, 0.1f, 0), 0.1f, yel, 10, 14);
                    b.Sphere(new Vector3(0, 0.19f, 0.01f), 0.07f, yel, 8, 12);
                    Face2(b, new Vector3(0, 0.19f, 0.07f), 0.06f, 0.4f);
                    b.Cylinder(new Vector3(0, 0.18f, 0.065f), new Vector3(0, 0.175f, 0.095f), 0.014f, 0.002f, orange, 6);
                    b.Ellipsoid(new Vector3(-0.095f, 0.1f, 0), new Vector3(0.02f, 0.05f, 0.04f), yel * 0.95f, 4, 6, Quaternion.Euler(0, 0, -25));
                    b.Ellipsoid(new Vector3(0.095f, 0.1f, 0), new Vector3(0.02f, 0.05f, 0.04f), yel * 0.95f, 4, 6, Quaternion.Euler(0, 0, 25));
                    b.Leaf(new Vector3(0, 0.255f, 0), new Vector3(0.2f, 1, 0), Vector3.forward, 0.04f, 0.02f, yel, yel, 0.3f);
                    b.Ellipsoid(new Vector3(-0.03f, 0.005f, 0.03f), new Vector3(0.02f, 0.008f, 0.03f), orange, 3, 6);
                    b.Ellipsoid(new Vector3(0.03f, 0.005f, 0.03f), new Vector3(0.02f, 0.008f, 0.03f), orange, 3, 6);
                    Part("body", b, Vector3.zero);
                    break;
                }
            case "p_bouquet":
                {
                    var wrap = new AcPB();
                    wrap.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0, 0.18f, 0), new Vector3(0, 0.25f, 0) }, new[] { 0.015f, 0.08f, 0.11f }, new Color(1f, 0.85f, 0.9f), 12, null, true, false);
                    wrap.Torus(new Vector3(0, 0.1f, 0), Vector3.up, 0.05f, 0.012f, new Color(0.9f, 0.2f, 0.3f), 14, 5);
                    Part("wrap", wrap, Vector3.zero);
                    Color[] fc = { new Color(0.95f, 0.25f, 0.35f), new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.6f, 0.8f), new Color(0.6f, 0.5f, 1f), Color.white, new Color(1f, 0.5f, 0.3f), new Color(0.95f, 0.3f, 0.6f) };
                    for (int i = 0; i < 7; i++)
                    {
                        var fb = new AcPB();
                        float a = i * 2.4f, rr = i == 0 ? 0 : 0.055f + (i % 2) * 0.02f;
                        var top = new Vector3(Mathf.Cos(a) * rr, 0.3f + (i == 0 ? 0.04f : (i % 3) * 0.015f), Mathf.Sin(a) * rr);
                        fb.Cylinder(Vector3.zero - top + new Vector3(0, 0.12f, 0), Vector3.zero, 0.005f, 0.004f, new Color(0.35f, 0.6f, 0.3f), 5);
                        var fd = (top - new Vector3(0, 0.12f, 0)).normalized; Basis(fd, out var s1, out var s2);
                        for (int p = 0; p < 6; p++) { float pa = p * Mathf.PI / 3; var pd = (fd * 0.35f + (s1 * Mathf.Cos(pa) + s2 * Mathf.Sin(pa))).normalized; fb.Leaf(Vector3.zero, pd, Vector3.Cross(fd, pd), 0.05f, 0.04f, fc[i], Color.Lerp(fc[i], Color.white, 0.3f), 0.25f, 3, 4); }
                        fb.Sphere(fd * 0.008f, 0.014f, new Color(1f, 0.9f, 0.4f), 4, 6);
                        var go = ProcObj("f" + i, fb.M("f" + i), root.transform); go.transform.localPosition = top;
                    }
                    var lv = new AcPB();
                    for (int k = 0; k < 6; k++) { float a = k * 1.05f; var d = new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a)); lv.Leaf(new Vector3(0, 0.2f, 0), d, Vector3.Cross(d, Vector3.up), 0.12f, 0.04f, new Color(0.25f, 0.55f, 0.3f), new Color(0.4f, 0.7f, 0.4f), 0.1f, 1, 5, 0.3f); }
                    Part("leaves", lv, Vector3.zero);
                    break;
                }
            case "p_crown":
                {
                    var gold = new Color(1f, 0.8f, 0.25f);
                    b.Cylinder(Vector3.zero, new Vector3(0, 0.07f, 0), 0.11f, 0.12f, gold, 24, false);
                    b.Torus(new Vector3(0, 0.005f, 0), Vector3.up, 0.112f, 0.01f, gold * 1.05f, 24, 5);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4; var p = new Vector3(Mathf.Cos(a) * 0.118f, 0.07f, Mathf.Sin(a) * 0.118f);
                        b.Cylinder(p, p + new Vector3(Mathf.Cos(a) * 0.01f, 0.08f, Mathf.Sin(a) * 0.01f), 0.02f, 0.003f, gold, 6);
                        b.Sphere(p + new Vector3(Mathf.Cos(a) * 0.012f, 0.085f, Mathf.Sin(a) * 0.012f), 0.012f, gold * 1.1f, 4, 6);
                        Color gem = k % 3 == 0 ? new Color(0.9f, 0.15f, 0.3f) : k % 3 == 1 ? new Color(0.3f, 0.6f, 1f) : new Color(0.3f, 0.85f, 0.5f);
                        b.Ellipsoid(new Vector3(Mathf.Cos(a + 0.39f) * 0.124f, 0.035f, Mathf.Sin(a + 0.39f) * 0.124f), new Vector3(0.016f, 0.016f, 0.016f), gem, 4, 6);
                    }
                    Part("crown", b, Vector3.zero, 0.15f);
                    break;
                }
            case "p_ring":
                {
                    var gold = new Color(1f, 0.82f, 0.3f);
                    b.Torus(new Vector3(0, 0.08f, 0), Vector3.forward, 0.07f, 0.012f, gold, 28, 7);
                    b.Cylinder(new Vector3(0, 0.145f, 0), new Vector3(0, 0.165f, 0), 0.02f, 0.03f, gold, 8);
                    b.Ellipsoid(new Vector3(0, 0.18f, 0), new Vector3(0.035f, 0.03f, 0.035f), new Color(0.75f, 0.92f, 1f), 4, 8);
                    b.Ellipsoid(new Vector3(0, 0.026f, 0), new Vector3(0.05f, 0.025f, 0.04f), new Color(0.85f, 0.2f, 0.3f), 6, 10);   // 받침 쿠션
                    Part("ring", b, Vector3.zero, 0.2f);
                    break;
                }
            case "p_rainbow":
                {
                    Color[] rc = { new Color(0.95f, 0.25f, 0.3f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.88f, 0.3f), new Color(0.4f, 0.85f, 0.4f), new Color(0.35f, 0.65f, 1f), new Color(0.65f, 0.45f, 0.95f) };
                    for (int k = 0; k < rc.Length; k++)
                    {
                        float R = 0.3f - k * 0.032f; var pts = new List<Vector3>(); var rs = new List<float>();
                        for (int i = 0; i <= 20; i++) { float a = Mathf.PI * i / 20f; pts.Add(new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0)); rs.Add(0.017f); }
                        b.Tube(pts, rs, rc[k], 6);
                    }
                    foreach (float sx in new[] { -0.22f, 0.22f }) { b.Sphere(new Vector3(sx, 0.02f, 0), 0.07f, white, 6, 8); b.Sphere(new Vector3(sx - 0.06f, 0.0f, 0.02f), 0.05f, white, 6, 8); b.Sphere(new Vector3(sx + 0.06f, 0.0f, 0.02f), 0.05f, white, 6, 8); }
                    Part("rainbow", b, Vector3.zero);
                    break;
                }
            case "p_balloons":
                {
                    Color[] bc = { new Color(0.95f, 0.2f, 0.3f), new Color(1f, 0.55f, 0.7f), new Color(0.95f, 0.35f, 0.5f) };
                    Vector3[] tops = { new Vector3(0, 0.62f, 0), new Vector3(-0.13f, 0.52f, 0.03f), new Vector3(0.13f, 0.5f, -0.02f) };
                    for (int i = 0; i < 3; i++)
                    {
                        var hb = new AcPB(); hb.Heart(Vector3.zero, 0.17f, bc[i], 0.5f, 26);
                        var go = ProcObj("b" + i, hb.M("b" + i), root.transform, 0.1f); go.transform.localPosition = tops[i];
                        var sb = new AcPB(); sb.Tube(new[] { new Vector3(0, 0.02f, 0), (tops[i] - new Vector3(0, 0.09f, 0)) * 0.5f + new Vector3(0.02f, 0, 0), tops[i] - new Vector3(0, 0.09f, 0) }, new[] { 0.002f, 0.002f, 0.002f }, white, 4);
                        Part("s" + i, sb, Vector3.zero);
                    }
                    var w2 = new AcPB(); w2.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.03f, 0.04f), new Color(1f, 0.85f, 0.3f), 6, 10);   // 묶은 리본(추)
                    Part("weight", w2, Vector3.zero);
                    break;
                }
            case "p_minitree":
                {
                    b.Cylinder(Vector3.zero, new Vector3(0, 0.06f, 0), 0.07f, 0.08f, new Color(0.85f, 0.2f, 0.22f), 14);
                    b.Cylinder(new Vector3(0, 0.06f, 0), new Vector3(0, 0.1f, 0), 0.02f, 0.02f, new Color(0.45f, 0.3f, 0.2f), 6);
                    for (int t = 0; t < 3; t++) { float y0 = 0.1f + t * 0.1f, r = 0.15f - t * 0.04f; b.Cylinder(new Vector3(0, y0, 0), new Vector3(0, y0 + 0.16f, 0), r, 0.002f, new Color(0.15f, 0.5f, 0.28f), 14, true); }
                    Color[] oc = { new Color(0.95f, 0.2f, 0.25f), new Color(1f, 0.85f, 0.3f), new Color(0.35f, 0.65f, 1f), new Color(1f, 0.6f, 0.8f) };
                    for (int k = 0; k < 12; k++) { float y = 0.13f + (k % 4) * 0.075f, r = 0.13f - (k % 4) * 0.03f, a = k * 2.3f; b.Sphere(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), 0.014f, oc[k % 4], 4, 6); }
                    b.Star(new Vector3(0, 0.44f, 0), Vector3.forward, Vector3.up, 0.045f, 0.02f, 0.012f, new Color(1f, 0.85f, 0.25f));
                    Part("tree", b, Vector3.zero);
                    break;
                }
            case "p_starwand":
                {
                    b.Cylinder(Vector3.zero, new Vector3(0, 0.36f, 0), 0.008f, 0.008f, new Color(1f, 0.6f, 0.8f), 6);
                    for (int k = 0; k < 6; k++) b.Torus(new Vector3(0, 0.06f + k * 0.05f, 0), Vector3.up, 0.009f, 0.003f, Color.white, 8, 4);
                    b.Star(new Vector3(0, 0.42f, 0), Vector3.forward, Vector3.up, 0.08f, 0.035f, 0.02f, new Color(1f, 0.85f, 0.25f));
                    var rib = new Color(0.55f, 0.75f, 1f);
                    b.Leaf(new Vector3(0, 0.35f, 0), new Vector3(0.4f, -1, 0.2f), Vector3.forward, 0.1f, 0.02f, rib, rib, 0.1f, 2, 5, 0.2f);
                    b.Leaf(new Vector3(0, 0.35f, 0), new Vector3(-0.4f, -1, 0.2f), Vector3.forward, 0.12f, 0.02f, new Color(1f, 0.6f, 0.8f), new Color(1f, 0.6f, 0.8f), 0.1f, 2, 5, 0.2f);
                    Part("wand", b, Vector3.zero, 0.2f);
                    break;
                }
            case "p_jack":
            case "p_popflower":
                {
                    bool jack = id == "p_jack";
                    var bx = new AcPB();
                    if (jack)
                    {
                        bx.Box(new Vector3(0, 0.07f, 0), new Vector3(0.16f, 0.14f, 0.16f), new Color(0.35f, 0.6f, 0.95f));
                        bx.Box(new Vector3(0, 0.07f, 0), new Vector3(0.165f, 0.03f, 0.165f), new Color(1f, 0.85f, 0.3f));
                        bx.Star(new Vector3(0, 0.07f, 0.083f), Vector3.forward, Vector3.up, 0.035f, 0.015f, 0.004f, new Color(1f, 0.85f, 0.3f));
                        var lidB = new AcPB(); lidB.Box(new Vector3(0.08f, 0.005f, 0), new Vector3(0.16f, 0.01f, 0.16f), new Color(0.95f, 0.3f, 0.4f));
                        var lidGo = ProcObj("jlid", lidB.M("jlid"), root.transform); lidGo.transform.localPosition = new Vector3(-0.08f, 0.14f, 0); lidGo.transform.localRotation = Quaternion.Euler(0, 0, 110);
                    }
                    else
                    {
                        bx.Cylinder(Vector3.zero, new Vector3(0, 0.12f, 0), 0.07f, 0.09f, new Color(0.9f, 0.5f, 0.3f), 14);
                        bx.Disk(new Vector3(0, 0.118f, 0), Vector3.up, 0.085f, new Color(0.35f, 0.25f, 0.18f), 14, false);
                    }
                    Part("base", bx, Vector3.zero);
                    // 용수철 (높이 0.2, 아래가 원점)
                    var sp = new AcPB(); var pts = new List<Vector3>(); var rs = new List<float>();
                    for (int i = 0; i <= 80; i++) { float a = i / 80f * Mathf.PI * 2 * 6; pts.Add(new Vector3(Mathf.Cos(a) * 0.035f, i / 80f * 0.2f, Mathf.Sin(a) * 0.035f)); rs.Add(0.006f); }
                    sp.Tube(pts, rs, jack ? new Color(0.85f, 0.85f, 0.9f) : new Color(0.35f, 0.65f, 0.35f), 5);
                    var spGo = ProcObj("spring", sp.M("spring"), root.transform); spGo.transform.localPosition = new Vector3(0, jack ? 0.12f : 0.11f, 0);
                    var hd = new AcPB();
                    if (jack)
                    {
                        hd.Sphere(new Vector3(0, 0.07f, 0), 0.075f, new Color(1f, 0.88f, 0.78f), 10, 14);
                        Face2(hd, new Vector3(0, 0.07f, 0.072f), 0.06f, 0.42f);
                        hd.Sphere(new Vector3(0, 0.065f, 0.08f), 0.016f, new Color(0.95f, 0.2f, 0.25f), 5, 6);
                        hd.Cylinder(new Vector3(0, 0.13f, 0), new Vector3(0.02f, 0.23f, 0), 0.06f, 0.003f, new Color(0.95f, 0.3f, 0.4f), 12);
                        hd.Sphere(new Vector3(0.02f, 0.235f, 0), 0.018f, new Color(1f, 0.85f, 0.3f), 5, 6);
                        hd.Torus(new Vector3(0, 0.005f, 0), Vector3.up, 0.04f, 0.018f, Color.white, 14, 6);   // 목 주름
                    }
                    else
                    {
                        Color petal = new Color(1f, 0.55f, 0.7f);
                        for (int p = 0; p < 8; p++) { float pa = p * Mathf.PI / 4; var pd = new Vector3(Mathf.Cos(pa), Mathf.Sin(pa), 0.15f); hd.Leaf(new Vector3(0, 0.07f, 0), pd, Vector3.forward, 0.08f, 0.055f, petal, Color.Lerp(petal, Color.white, 0.4f), 0.05f, 3, 4); }
                        hd.Ellipsoid(new Vector3(0, 0.07f, 0.01f), new Vector3(0.05f, 0.05f, 0.025f), new Color(1f, 0.85f, 0.3f), 8, 12);
                        Face2(hd, new Vector3(0, 0.07f, 0.034f), 0.045f, 0.4f);
                        hd.Leaf(new Vector3(0, -0.04f, 0), new Vector3(1, 0.4f, 0), Vector3.forward, 0.07f, 0.035f, new Color(0.35f, 0.65f, 0.35f), new Color(0.5f, 0.75f, 0.4f), 0.15f);
                    }
                    var hdGo = ProcObj("head", hd.M("head"), root.transform); hdGo.transform.localPosition = new Vector3(0, (jack ? 0.12f : 0.11f) + 0.2f, 0);
                    break;
                }
            case "p_cupcake":
                {
                    for (int k = 0; k < 16; k++)
                    {
                        float a0 = k * Mathf.PI / 8, a1 = (k + 1) * Mathf.PI / 8;
                        var col = k % 2 == 0 ? new Color(0.98f, 0.6f, 0.7f) : new Color(1f, 0.85f, 0.88f);
                        var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                        var nn = ((p0 + p1) * 0.5f).normalized;
                        int i0 = b.V(p0 * 0.07f, nn, col), i1 = b.V(p1 * 0.07f, nn, col), i2 = b.V(p1 * 0.095f + Vector3.up * 0.11f, nn, col), i3 = b.V(p0 * 0.095f + Vector3.up * 0.11f, nn, col);
                        b.Quad(i0, i1, i2, i3);
                    }
                    b.Disk(new Vector3(0, 0.001f, 0), Vector3.down, 0.07f, new Color(0.9f, 0.5f, 0.6f), 16, false);
                    var fr = new Color(0.98f, 0.95f, 0.92f);
                    for (int k = 0; k < 4; k++) b.Torus(new Vector3(0, 0.12f + k * 0.035f, 0), Vector3.up, 0.075f - k * 0.017f, 0.028f - k * 0.004f, fr, 18, 7);
                    b.Sphere(new Vector3(0, 0.24f, 0), 0.025f, new Color(0.88f, 0.1f, 0.15f), 6, 8);
                    b.Tube(new[] { new Vector3(0, 0.26f, 0), new Vector3(0.01f, 0.285f, 0), new Vector3(0.025f, 0.295f, 0) }, new[] { 0.003f, 0.003f, 0.003f }, new Color(0.35f, 0.5f, 0.25f), 4);
                    Color[] spc = { new Color(1f, 0.3f, 0.4f), new Color(0.3f, 0.7f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.4f, 0.85f, 0.4f) };
                    var rnd = new System.Random(4);
                    for (int k = 0; k < 18; k++) { float a = (float)rnd.NextDouble() * 6.28f, lvl = (float)rnd.NextDouble(); float r = 0.09f - lvl * 0.06f; b.Box(new Vector3(Mathf.Cos(a) * r, 0.135f + lvl * 0.1f, Mathf.Sin(a) * r), new Vector3(0.012f, 0.004f, 0.004f), spc[k % 4], Quaternion.Euler(0, a * 57f, 30)); }
                    Part("cake", b, Vector3.zero);
                    break;
                }
            case "p_donut":
                {
                    b.xf = Matrix4x4.Scale(new Vector3(1f, 0.75f, 1f));
                    b.Torus(new Vector3(0, 0.05f, 0), Vector3.up, 0.07f, 0.042f, new Color(0.85f, 0.6f, 0.35f), 24, 10);
                    b.xf = Matrix4x4.Translate(new Vector3(0, 0.012f, 0)) * Matrix4x4.Scale(new Vector3(1f, 0.55f, 1f));
                    b.Torus(new Vector3(0, 0.09f, 0), Vector3.up, 0.07f, 0.04f, new Color(1f, 0.6f, 0.75f), 24, 10);
                    b.xf = Matrix4x4.identity;
                    Color[] spc = { Color.white, new Color(0.3f, 0.7f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.6f, 0.4f, 0.9f) };
                    for (int k = 0; k < 20; k++) { float a = k * 0.314f + (k % 3) * 0.1f, r = 0.07f + ((k % 3) - 1) * 0.018f; b.Box(new Vector3(Mathf.Cos(a) * r, 0.084f, Mathf.Sin(a) * r), new Vector3(0.012f, 0.004f, 0.004f), spc[k % 4], Quaternion.Euler(0, k * 47f, 0)); }
                    Part("donut", b, Vector3.zero);
                    break;
                }
            default:
                b.Sphere(new Vector3(0, 0.1f, 0), 0.1f, pinkC);
                Part("blob", b, Vector3.zero);
                break;
        }
        return root;
    }

    // 내용물 움직이기 (holder: 맞춘 크기 물체, 원점 바닥 가운데)
    void AnimItem(GameObject holder, GiftItem def, float t, Vector3 basePos, float baseYaw)
    {
        if (!holder) return;
        var tr = holder.transform;
        float y = 0, yaw = baseYaw, rz = 0; float sy = 1;
        switch (def.anim)
        {
            case "bob": y = 0.02f + 0.02f * Mathf.Sin(t * 2.5f); yaw += t * 30f; break;
            case "spin": y = 0.03f + 0.015f * Mathf.Sin(t * 2f); yaw += t * 70f; break;
            case "hop": { float p = Mathf.Repeat(t * 1.5f, 1f); y = 0.09f * 4 * p * (1 - p) * def.h / 0.4f; sy = p < 0.12f ? 1 - 0.15f * (1 - p / 0.12f) : 1; yaw += Mathf.Sin(t * 0.8f) * 25f; break; }
            case "sway": rz = 9f * Mathf.Sin(t * 2.2f); yaw += 20f * Mathf.Sin(t * 0.7f); break;
            case "waddle": rz = 11f * Mathf.Sin(t * 6f); y = 0.012f * Mathf.Abs(Mathf.Sin(t * 6f)); yaw += 20f * Mathf.Sin(t * 0.8f); break;
            case "swing": rz = 18f * Mathf.Sin(t * 4f); break;
            case "drive": { float a = t * 1.4f; var off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.22f; tr.localPosition = basePos + off; tr.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0); return; }
            case "glow": yaw += t * 25f; y = 0.015f * Mathf.Sin(t * 2f); SetEmitAll(holder, 0.15f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * 3f))); break;
            case "float": y = 0.03f * Mathf.Sin(t * 1.3f); rz = 4f * Mathf.Sin(t * 0.9f); foreach (Transform c in holder.GetComponentsInChildren<Transform>()) if (c.name.Length == 2 && c.name[0] == 'b' && char.IsDigit(c.name[1])) c.localRotation = Quaternion.Euler(0, 0, 8f * Mathf.Sin(t * 1.7f + c.name[1])); break;
            case "bloom":
                {
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                        if (c.name.Length == 2 && c.name[0] == 'f' && char.IsDigit(c.name[1]))
                        {
                            int i = c.name[1] - '0'; float k = Mathf.Clamp01((t - 0.15f * i) / 0.45f);
                            float s = k < 1 ? Mathf.SmoothStep(0, 1, k) * (1 + 0.25f * Mathf.Sin(k * Mathf.PI)) : 1 + 0.04f * Mathf.Sin(t * 3 + i);
                            c.localScale = Vector3.one * Mathf.Max(0.01f, s);
                        }
                    rz = 4f * Mathf.Sin(t * 1.5f);
                    break;
                }
            case "boing":
                {
                    float p = Mathf.Repeat(t, 2.6f);
                    float k = 1 + 0.45f * Mathf.Sin(p * 11f) * Mathf.Exp(-p * 2.2f);
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                    {
                        if (c.name == "spring") c.localScale = new Vector3(1, k, 1);
                        if (c.name == "head") { c.localPosition = new Vector3(0, (def.id == "p_jack" ? 0.12f : 0.11f) + 0.2f * k, 0); c.localRotation = Quaternion.Euler(0, 0, 10f * Mathf.Sin(p * 9f) * Mathf.Exp(-p * 2f)); }
                    }
                    if (p < Time.deltaTime * 1.2f && giftMode != GiftMode.None) Sfx("boing", 0.4f);
                    break;
                }
            case "dance":
                {
                    yaw += 25f * Mathf.Sin(t * 0.6f);
                    var ch = holder.GetComponentInChildren<AcCharacter>();
                    if (ch != null && Mathf.Repeat(t, 2.4f) < Time.deltaTime * 1.2f) { string[] em = { "cheer", "wave", "heart", "highfive" }; ch.PlayEmote(em[(int)(t / 2.4f) % em.Length]); }
                    break;
                }
        }
        tr.localPosition = basePos + Vector3.up * y;
        tr.localRotation = Quaternion.Euler(0, yaw, rz);
        tr.localScale = new Vector3(1 / Mathf.Sqrt(sy), sy, 1 / Mathf.Sqrt(sy));
    }

    readonly MaterialPropertyBlock giftMpb = new MaterialPropertyBlock();
    void SetEmitAll(GameObject g, float e)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.GetPropertyBlock(giftMpb); giftMpb.SetFloat("_Emit", e); r.SetPropertyBlock(giftMpb); }
    }

    // ===== 선물 무대 =====
    Transform studio;
    void EnsureStudio()
    {
        if (studio) { studio.gameObject.SetActive(true); return; }
        studio = new GameObject("GiftStudio").transform; studio.position = StudioPos;
        var dome = new AcPB { inward = true };
        dome.Ellipsoid(new Vector3(0, 0, 0), new Vector3(9, 7, 9), Color.white, 16, 24, null,
            u => Color.Lerp(new Color(1f, 0.9f, 0.86f), new Color(0.72f, 0.82f, 1f), Mathf.Clamp01(u.y * 1.3f + 0.2f)));
        var domeGo = ProcObj("dome", dome.M("dome"), studio, 0.92f, false);
        var floor = new AcPB();
        floor.Disk(new Vector3(0, 0.001f, 0), Vector3.up, 8.5f, new Color(0.98f, 0.93f, 0.9f), 32, false);
        ProcObj("floor", floor.M("floor"), studio, 0.2f, false);
        var tb = new AcPB();
        tb.Cylinder(new Vector3(0, 0, 0), new Vector3(0, TableY - 0.06f, 0), 0.12f, 0.1f, new Color(0.75f, 0.55f, 0.4f), 12);
        tb.Cylinder(new Vector3(0, TableY - 0.06f, 0), new Vector3(0, TableY, 0), 0.95f, 0.95f, new Color(0.99f, 0.97f, 0.95f), 40);
        tb.Torus(new Vector3(0, TableY - 0.03f, 0), Vector3.up, 0.95f, 0.035f, new Color(0.86f, 0.2f, 0.25f), 48, 6);
        for (int k = 0; k < 24; k++) { float a = k * Mathf.PI / 12; tb.Sphere(new Vector3(Mathf.Cos(a) * 0.98f, TableY - 0.075f, Mathf.Sin(a) * 0.98f), 0.022f, k % 2 == 0 ? new Color(1f, 0.85f, 0.3f) : new Color(0.3f, 0.7f, 0.45f), 4, 6); }
        ProcObj("table", tb.M("table"), studio);
        // 둘레 작은 트리·선물 (분위기)
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3 + 0.5f; var p = new Vector3(Mathf.Cos(a) * 3.2f, 0, Mathf.Sin(a) * 3.2f);
            var g = AcmLoader.Exists("pine_snowy") ? AcCatalog.Spawn(k % 2 == 0 ? "pine_snowy" : "gift_pile", studio, null) : null;
            if (g) { g.transform.localPosition = p; g.transform.localScale = Vector3.one * (k % 2 == 0 ? 0.6f : 1.1f); }
        }
    }

    float studioYaw = 30f, studioZoom = 1f, studioYawVel; Vector3 camStudioPos; bool camStudioInit;
    bool GiftCamera(float dt, bool snap)
    {
        if (giftMode == GiftMode.None) { camStudioInit = false; return false; }
        float objH = 0.5f;
        if (wrapBox != null) objH = Mathf.Max(objH, wrapBox.size.y + 0.15f);
        if (giftMode == GiftMode.Wrap && wrapTab == 0) objH = Mathf.Max(0.5f, GiftItemById(wrapDraft.item).h + 0.1f);
        if (giftMode == GiftMode.Unwrap && unPhase >= 3) objH = (wrapBox != null ? wrapBox.size.y : 0.4f) + (openItemDef != null ? openItemDef.h : 0.4f) + 0.2f;
        bool portrait = Screen.height > Screen.width;
        float vfov = cam.fieldOfView * Mathf.Deg2Rad;
        float need = Mathf.Max(objH / (portrait ? 0.42f : 0.6f), 0.8f / Mathf.Max(0.3f, cam.aspect));
        float dist = need / (2f * Mathf.Tan(vfov / 2f)) * studioZoom + 0.3f;
        var focus = StudioPos + Vector3.up * (TableY + objH * (portrait ? 0.18f : 0.38f));
        float pitch = 22f;
        var rot = Quaternion.Euler(pitch, studioYaw + 180f, 0);
        var want = focus - rot * Vector3.forward * dist;
        if (!camStudioInit || snap) { camStudioPos = want; camStudioInit = true; }
        camStudioPos = Vector3.Lerp(camStudioPos, want, 1 - Mathf.Exp(-dt * 6f));
        cam.transform.position = camStudioPos;
        cam.transform.rotation = Quaternion.LookRotation(focus - camStudioPos);
        cam.nearClipPlane = 0.05f;
        return true;
    }

    void LeaveStudio()
    {
        giftMode = GiftMode.None;
        if (wrapBox != null) { DestroyGiftBox(wrapBox); wrapBox = null; }
        if (wrapItem) { Destroy(wrapItem); wrapItem = null; }
        if (openItem) { Destroy(openItem); openItem = null; }
        wrapBoxKey = ""; wrapItemKey = "";
        foreach (var f in giftFly) if (f.tr) Destroy(f.tr.gameObject);
        giftFly.Clear();
        if (studio) studio.gameObject.SetActive(false);
        UpdateCamera(0.016f, true);
    }

    // ===== 포장하기 =====
    GiftData wrapDraft; int wrapTab; int giftCat; GiftBox wrapBox; string wrapBoxKey = ""; GameObject wrapItem; string wrapItemKey = ""; float wrapItemT;
    readonly IconStripState giftStrip = new IconStripState();

    void OpenWrap()
    {
        if (Held.g.Count >= 3) { Toast("선물은 3개까지 들 수 있어요. 먼저 친구에게 전달해 주세요"); return; }
        var last = PlayerPrefs.GetString("gift_last", "");
        wrapDraft = null;
        if (last != "") try { wrapDraft = JsonUtility.FromJson<GiftData>(last); } catch { }
        if (wrapDraft == null) wrapDraft = new GiftData();
        wrapDraft.msg = "";
        giftMode = GiftMode.Wrap; wrapTab = 0; giftCat = GiftItemById(wrapDraft.item).cat; studioYaw = 25f; studioZoom = 1f;
        wrapBoxKey = ""; wrapItemKey = ""; walkTarget = null; followId = null;
        EnsureStudio();
    }

    void WrapUpdate(float dt)
    {
        // 상자 / 내용물 보여 주기
        bool showItem = wrapTab == 0;
        string bk = JsonUtility.ToJson(new GiftData { box = wrapDraft.box, paper = wrapDraft.paper, pc2 = wrapDraft.pc2, pat = wrapDraft.pat, ribbon = wrapDraft.ribbon, rpat = wrapDraft.rpat, bow = wrapDraft.bow });
        if (bk != wrapBoxKey) { DestroyGiftBox(wrapBox); wrapBox = BuildGiftBox(wrapDraft, studio); wrapBox.root.localPosition = new Vector3(0, TableY, 0); wrapBoxKey = bk; }
        if (wrapDraft.item != wrapItemKey)
        {
            if (wrapItem) Destroy(wrapItem);
            wrapItem = MakeGiftItem(wrapDraft.item, me.look.Encode());
            wrapItem.transform.SetParent(studio, false); wrapItemKey = wrapDraft.item; wrapItemT = 0;
        }
        wrapBox.root.gameObject.SetActive(!showItem);
        wrapItem.SetActive(showItem);
        wrapItemT += dt;
        if (showItem) AnimItem(wrapItem, GiftItemById(wrapDraft.item), wrapItemT, new Vector3(0, TableY, 0), 0);
        else wrapBox.root.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 0.5f) * 12f, 0);
        StudioDragInput();
        if (Key("esc")) LeaveStudio();
    }

    void StudioDragInput()
    {
        if (PinchZoomStudio()) return;
        if (ptrPressed && ptrMoved && !ptrOnUI) studioYaw -= (ptrPos - lastPtr).x * 0.3f;
        lastPtr = ptrPos;
        float sc = OverUI(ptrPos) ? 0 : Scroll(); if (Mathf.Abs(sc) > 0.01f) studioZoom = Mathf.Clamp(studioZoom * (sc > 0 ? 0.9f : 1.1f), 0.6f, 1.8f);
    }
    bool PinchZoomStudio() { float z = decoZoom; decoZoom = studioZoom; bool p = PinchZoom(); studioZoom = Mathf.Clamp(decoZoom, 0.6f, 1.8f); decoZoom = z; return p; }

    void FinishWrap()
    {
        var g = JsonUtility.FromJson<GiftData>(JsonUtility.ToJson(wrapDraft));
        g.look = me.look.Encode(); g.from = uiName.Trim();
        Held.g.Add(g); SaveHeld();
        var keep = JsonUtility.FromJson<GiftData>(JsonUtility.ToJson(wrapDraft)); keep.msg = ""; PlayerPrefs.SetString("gift_last", JsonUtility.ToJson(keep));
        LeaveStudio();
        Emote("cheer");
        Sfx("ta_da", 0.5f);
        FxBurst("star", me.transform.position + Vector3.up * 1.2f, 16, 2f, 0.06f, FxGold, 1.1f, 1.5f);
        Toast("포장 완료! 친구 가까이 가면 '전달하기'가 생겨요");
    }

    void WrapGUI()
    {
        bool narrow = SW < 640;
        // 위: 제목 + 취소/완료
        var top = new Rect(10, 10, SW - 20, 46);
        GUI.Box(R(top), "", sBox);
        GUI.Label(new Rect(24, 20, 200, 26), "선물 포장하기", sLabel);
        float bw = narrow ? 76 : 90;
        if (GUI.Button(R(new Rect(SW - 20 - bw * 2 - 8, 16, bw, 34)), "취소", sBtn)) { LeaveStudio(); return; }
        if (GUI.Button(R(new Rect(SW - 20 - bw, 16, bw, 34)), "<b>포장 완료!</b>", sBtn)) { FinishWrap(); return; }
        float h = narrow ? 290 : 240, y = SH - h - 8;
        var box = new Rect(10, y, SW - 20, h);
        GUI.Box(R(box), "", sBox);
        string[] tabs = { "내용물", "상자", "포장지", "리본", "카드" };
        float tw = (SW - 44 - 4 * 5) / 5f;
        for (int i = 0; i < tabs.Length; i++)
        {
            var tr = new Rect(22 + i * (tw + 5), y + 8, tw, 30);
            if (i == wrapTab) AcUiTheme.Ring(tr, AcUiTheme.Accent, 3);
            if (GUI.Button(tr, "<size=13>" + tabs[i] + "</size>", sBtn)) wrapTab = i;
        }
        float cy = y + 48, lx = 26, labelW = narrow ? 86 : 120, valW = narrow ? 150 : 180;
        var d = wrapDraft;
        switch (wrapTab)
        {
            case 0:
                {
                    float cw = (SW - 52 - 3 * 5) / 4f;
                    for (int c = 0; c < GiftCatKo.Length; c++)
                    {
                        var r = new Rect(lx + c * (cw + 5), cy, cw, 28);
                        if (c == giftCat) AcUiTheme.Ring(r, AcUiTheme.Gold, 3);
                        if (GUI.Button(r, "<size=12>" + GiftCatKo[c] + "</size>", sBtn)) giftCat = c;
                    }
                    cy += 36;
                    var list = GiftItems.Where(g => g.cat == giftCat).ToList();
                    float size = narrow ? 58 : 64;
                    int sel = list.FindIndex(g => g.id == d.item);
                    int ci = IconStrip(giftStrip, new Rect(lx - 3, cy, SW - 2 * lx, size + 6), list.Count, size, i => GiftIcon(list[i].id), sel, true);
                    if (ci >= 0) { d.item = list[ci].id; Sfx("click", 0.35f, 1.2f); }
                    cy += size + 14;
                    var it = GiftItemById(d.item);
                    GUI.Label(new Rect(lx, cy, SW - 60, 26), "<b>" + it.ko + "</b>" + (it.id == "mini_me" ? "  <size=12>(지금 내 모습 그대로)</size>" : ""), sLabel);
                    cy += 28;
                    GUI.Label(new Rect(lx, cy, SW - 60, 22), "<size=12>끌어서 돌려 보기 · 받는 사람이 상자를 열면 짠! 하고 나와요</size>", sSmall);
                    break;
                }
            case 1:
                {
                    float cw = (SW - 52 - 2 * 6) / 3f;
                    for (int i = 0; i < BoxKo.Length; i++)
                    {
                        var r = new Rect(lx + (i % 3) * (cw + 6), cy + (i / 3) * 40, cw, 34);
                        if (i == d.box) AcUiTheme.Ring(r, AcUiTheme.Accent, 3);
                        if (GUI.Button(r, BoxKo[i], sBtn)) d.box = i;
                    }
                    break;
                }
            case 2:
                {
                    int pc = AcPalette.Nearest(AcPalette.Vivid, Hex(d.paper));
                    ArrowRow(ref cy, lx, labelW, valW, "포장지 색", AcPalette.VividNames[pc], k => d.paper = HexOf(AcPalette.Vivid[Wrap(pc + k, AcPalette.Vivid.Length)]), Hex(d.paper));
                    ArrowRow(ref cy, lx, labelW, valW, "무늬", PatKo[Wrap(d.pat, PatKo.Length)], k => d.pat = Wrap(d.pat + k, PatKo.Length));
                    int mi = d.pc2 == "" ? 0 : Mathf.Max(1, Array.FindIndex(MotifCols, c => HexOf(c) == d.pc2));
                    var mc = mi == 0 ? AutoMotif(Hex(d.paper)) : MotifCols[mi];
                    ArrowRow(ref cy, lx, labelW, valW, "무늬 색", MotifKo[mi], k => { int n = Wrap(mi + k, MotifCols.Length); d.pc2 = n == 0 ? "" : HexOf(MotifCols[n]); }, mc);
                    break;
                }
            case 3:
                {
                    int rc = AcPalette.Nearest(AcPalette.Vivid, Hex(d.ribbon));
                    ArrowRow(ref cy, lx, labelW, valW, "리본 색", AcPalette.VividNames[rc], k => d.ribbon = HexOf(AcPalette.Vivid[Wrap(rc + k, AcPalette.Vivid.Length)]), Hex(d.ribbon));
                    ArrowRow(ref cy, lx, labelW, valW, "리본 무늬", RPatKo[Wrap(d.rpat, RPatKo.Length)], k => d.rpat = Wrap(d.rpat + k, RPatKo.Length));
                    ArrowRow(ref cy, lx, labelW, valW, "리본 모양", BowKo[Wrap(d.bow, BowKo.Length)], k => d.bow = Wrap(d.bow + k, BowKo.Length));
                    break;
                }
            default:
                {
                    GUI.Label(new Rect(lx, cy, SW - 60, 22), "<size=13>선물에 붙일 카드 (받는 사람은 전달할 때 정해져요)</size>", sSmall);
                    cy += 28;
                    var r = new Rect(lx, cy, SW - 2 * lx, 90);
                    AcUiTheme.Fill(r, new Color(1f, 0.98f, 0.92f), 3);
                    GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16), string.IsNullOrEmpty(d.msg) ? "<color=#a08a78>눌러서 한마디 쓰기</color>" : d.msg, new GUIStyle(sSmall) { wordWrap = true });
                    if (GUI.Button(R(r), "", GUIStyle.none)) Ask("선물 카드에 쓸 말", d.msg, true, s => d.msg = s.Length > 120 ? s.Substring(0, 120) : s);
                    break;
                }
        }
    }

    // 내용물 아이콘 (Update에서 만듦)
    readonly Dictionary<string, Texture2D> giftIcons = new Dictionary<string, Texture2D>();
    readonly HashSet<string> giftIconWant = new HashSet<string>();
    Texture GiftIcon(string id) { if (giftIcons.TryGetValue(id, out var t) && t) return t; giftIconWant.Add(id); return null; }
    void ProcessGiftIcons()
    {
        if (giftIconWant.Count == 0) return;
        EnsureIconCam();
        int n = 0;
        foreach (var id in giftIconWant.ToList())
        {
            if (n++ >= 3) break;
            var obj = MakeGiftItem(id, me.look.Encode());
            obj.transform.rotation = Quaternion.Euler(6, 20, 0);
            var tex = ObjIcon(obj);
            if (tex != null) { giftIcons[id] = tex; giftIconWant.Remove(id); }
        }
    }

    // ===== 들고 다니기 / 전달하기 =====
    GameObject carryObj; string carryKey = "";
    void UpdateCarry()
    {
        var g = HeldGift;
        string key = g == null || GiftBusy || playingGame || candyOpen ? "" : g.paper + g.ribbon + Held.g.Count;
        if (key == carryKey) return;
        carryKey = key;
        if (carryObj) Destroy(carryObj);
        if (key == "" || me == null) return;
        carryObj = AcCatalog.Spawn("gift_box", me.transform, new Dictionary<string, Color> { { "box", Hex(g.paper) }, { "ribbon", Hex(g.ribbon) } });
        carryObj.transform.localPosition = new Vector3(0, 0.62f, 0.34f);
        carryObj.transform.localScale = Vector3.one * 0.42f;
    }

    void GiveGift(Remote r)
    {
        var g = HeldGift; if (g == null || r == null || r.net == null || net == null) return;
        string id = "g" + Guid.NewGuid().ToString("N").Substring(0, 10);
        string json = "{\"id\":" + AcJson.Esc(id) + ",\"from\":" + AcJson.Esc(myKey) + ",\"fromName\":" + AcJson.Esc(uiName.Trim()) +
                      ",\"to\":" + AcJson.Esc(NameKey(r.net.name)) + ",\"toName\":" + AcJson.Esc(r.net.name) + ",\"ct\":" + Math.Floor(NowMs).ToString("0") +
                      ",\"opened\":false,\"d\":" + AcJson.Esc(JsonUtility.ToJson(g)) + "}";
        net.Put("gifts/" + id, json);
        Held.g.RemoveAt(0); SaveHeld();
        Emote("gift", r.net.id); FaceTo(r.c.transform.position);
        Sfx("candy_collect", 0.5f, 1.1f);
        FxBurst("heart", r.c.transform.position + Vector3.up * 1.4f, 10, 1.5f, 0.07f, new[] { new Color(1f, 0.4f, 0.55f), new Color(1f, 0.65f, 0.75f) }, 1.2f, -0.5f);
        Toast(r.net.name + " 님에게 선물을 전달했어요!");
    }

    // ===== 받은 선물 =====
    readonly List<(string id, string fromName, GiftData d, double ct)> giftInbox = new List<(string, string, GiftData, double)>();
    readonly HashSet<string> giftSeen = new HashSet<string>(), giftOpenedLocal = new HashSet<string>();
    bool giftPrimed; float nextGiftPoll;
    void PollGifts()
    {
        if (net == null || Time.unscaledTime < nextGiftPoll) return;
        nextGiftPoll = Time.unscaledTime + 1f;
        var node = net.Get("gifts");
        giftInbox.Clear();
        int fresh = 0; string freshFrom = "";
        if (node != null)
            foreach (var kv in node)
            {
                if (!(kv.Value is Dictionary<string, object> d)) continue;
                if (AcJson.S(d, "to") != myKey || AcJson.B(d, "opened") || giftOpenedLocal.Contains(kv.Key)) continue;
                GiftData gd = null; try { gd = JsonUtility.FromJson<GiftData>(AcJson.S(d, "d")); } catch { }
                if (gd == null) continue;
                giftInbox.Add((kv.Key, AcJson.S(d, "fromName"), gd, AcJson.D(d, "ct")));
                if (giftSeen.Add(kv.Key)) { fresh++; freshFrom = AcJson.S(d, "fromName"); }
            }
        giftInbox.Sort((a, b) => a.ct.CompareTo(b.ct));
        if (fresh > 0)
        {
            Toast(giftPrimed ? freshFrom + " 님이 선물을 줬어요! '선물 풀어보기'를 눌러요" : $"받은 선물이 {giftInbox.Count}개 있어요! '선물 풀어보기'를 눌러요");
            Sfx("heart", 0.5f);
        }
        giftPrimed = true;
    }

    // ===== 풀어보기 =====
    string openId, openFrom; GiftData openData; GiftItem openItemDef; GameObject openItem;
    int unPhase, pullCount; float unT;
    class Fly { public Transform tr; public Vector3 vel, ax; public float spin, life, max; }
    readonly List<Fly> giftFly = new List<Fly>();

    void OpenUnwrap(int idx)
    {
        if (idx < 0 || idx >= giftInbox.Count) return;
        var e = giftInbox[idx];
        openId = e.id; openFrom = e.fromName; openData = e.d; openItemDef = GiftItemById(e.d.item);
        giftMode = GiftMode.Unwrap; unPhase = 0; pullCount = 0; unT = 0; studioYaw = 32f; studioZoom = 1f; bowRestSet = false;
        EnsureStudio();
        DestroyGiftBox(wrapBox);
        wrapBox = BuildGiftBox(openData, studio); wrapBox.root.localPosition = new Vector3(0, TableY, 0);
        if (openItem) Destroy(openItem); openItem = null;
        walkTarget = null; followId = null;
        Sfx("whoosh", 0.4f, 0.9f);
    }

    void FlyOff(GameObject go, Vector3 vel, float life = 1.1f)
    {
        if (!go) return;
        go.transform.SetParent(studio, true);
        giftFly.Add(new Fly { tr = go.transform, vel = vel, ax = UnityEngine.Random.onUnitSphere, spin = UnityEngine.Random.Range(200f, 600f), life = 0, max = life });
    }

    Vector3 bowRest; bool bowRestSet;
    void UnwrapUpdate(float dt)
    {
        unT += dt;
        // 날아가는 조각들
        for (int i = giftFly.Count - 1; i >= 0; i--)
        {
            var f = giftFly[i]; f.life += dt;
            if (!f.tr || f.life > f.max) { if (f.tr) Destroy(f.tr.gameObject); giftFly.RemoveAt(i); continue; }
            f.vel += Vector3.down * 5f * dt; f.tr.position += f.vel * dt; f.tr.Rotate(f.ax, f.spin * dt, Space.World);
            float k = f.life / f.max; f.tr.localScale *= k > 0.6f ? Mathf.Max(0.0f, 1 - (k - 0.6f) * dt * 6f) : 1f;
        }
        var gb = wrapBox; if (gb == null) return;
        if (!bowRestSet && gb.bow) { bowRest = gb.bow.transform.localPosition; bowRestSet = true; }
        float thr = Screen.height * 0.07f;
        switch (unPhase)
        {
            case 0: // 리본 풀기: 끌어당기기 3번
                {
                    if (!gb.bow) break;
                    var drag = ptrPressed && !ptrOnUI ? ptrPos - ptrStart : Vector2.zero;
                    var off = (cam.transform.right * drag.x + cam.transform.up * drag.y) * (0.6f / Screen.height);
                    off = Vector3.ClampMagnitude(off, 0.18f);
                    var tgt = bowRest + gb.root.InverseTransformVector(off);
                    gb.bow.transform.localPosition = Vector3.Lerp(gb.bow.transform.localPosition, tgt, 1 - Mathf.Exp(-dt * 18f));
                    float st = 1 + off.magnitude * 0.6f;
                    gb.bandX.transform.localScale = new Vector3(1, st, 1); gb.bandZ.transform.localScale = new Vector3(1, st, 1);
                    gb.bow.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(unT * 3) * 6f, Mathf.Sin(unT * 4.3f) * 4f);
                    if (ptrUp && !ptrOnUI && ptrMoved && (ptrPos - ptrStart).magnitude > Screen.height * 0.06f)
                    {
                        pullCount++;
                        Sfx("untie", 0.6f, 0.9f + pullCount * 0.1f);
                        FxBurst("conf", gb.bow.transform.position, 6, 1.2f, 0.035f, new[] { Hex(openData.ribbon) }, 0.9f, 3f);
                        float sf = gb.bow.transform.localScale.x;
                        gb.bow.transform.localScale = Vector3.one * sf * 0.82f;
                        if (pullCount >= 3)
                        {
                            var up = Vector3.up * 1.6f;
                            FlyOff(gb.bow, up + off.normalized * 1.2f + cam.transform.right * 0.5f, 1.2f); gb.bow = null;
                            FlyOff(gb.bandX, up + cam.transform.right * 0.8f, 1.0f); gb.bandX = null;
                            FlyOff(gb.bandZ, up - cam.transform.right * 0.8f, 1.0f); gb.bandZ = null;
                            unPhase = 1; Sfx("candy_special", 0.4f, 1.2f);
                        }
                    }
                    if (!ptrPressed && gb.bandX) { gb.bandX.transform.localScale = Vector3.Lerp(gb.bandX.transform.localScale, Vector3.one, dt * 10); gb.bandZ.transform.localScale = gb.bandX.transform.localScale; }
                    break;
                }
            case 1: // 포장지 찢기: 문지른 곳 조각을 표시했다가 손을 떼면 찢김 (한 번에 최대 6조각)
                {
                    var toCam = (cam.transform.position - gb.root.position).normalized;
                    if (ptrPressed && !ptrOnUI)
                    {
                        int marked = gb.paper.Count(p => p.marked && !p.torn);
                        foreach (var p in gb.paper)
                        {
                            if (p.torn || p.marked || marked >= 6) continue;
                            var wn = gb.root.TransformDirection(p.n);
                            if (Vector3.Dot(wn, toCam) < 0.05f) continue;
                            var sp = cam.WorldToScreenPoint(gb.root.TransformPoint(p.c));
                            if (sp.z > 0 && (new Vector2(sp.x, sp.y) - ptrPos).magnitude < thr * 1.4f)
                            {
                                p.marked = true; marked++;
                                p.go.transform.localPosition = p.c + p.n * 0.015f;
                                p.go.transform.localRotation = Quaternion.AngleAxis(8f, Vector3.Cross(p.n, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(p.n, Vector3.up) : Vector3.right);
                                Sfx("click", 0.25f, 1.6f);
                            }
                        }
                    }
                    if (ptrUp)
                    {
                        bool any = false;
                        foreach (var p in gb.paper)
                            if (p.marked && !p.torn)
                            {
                                p.torn = true; any = true;
                                var wn = gb.root.TransformDirection(p.n);
                                FlyOff(p.go, wn * UnityEngine.Random.Range(1.2f, 2f) + Vector3.up * UnityEngine.Random.Range(1f, 2f) + UnityEngine.Random.insideUnitSphere * 0.4f, 1.1f);
                                FxBurst("conf", gb.root.TransformPoint(p.c), 4, 1.0f, 0.03f, new[] { Hex(openData.paper), openData.pc2 == "" ? AutoMotif(Hex(openData.paper)) : Hex(openData.pc2) }, 0.9f, 3f);
                            }
                        if (any) { Sfx("tear", 0.7f, UnityEngine.Random.Range(0.9f, 1.15f)); camShakeStudio = 0.12f; }
                        // 보이는 쪽이 다 찢기면 나머지(뒤쪽)도 같이
                        bool visibleLeft = gb.paper.Any(p => !p.torn && Vector3.Dot(gb.root.TransformDirection(p.n), toCam) >= 0.05f);
                        if (!visibleLeft)
                        {
                            foreach (var p in gb.paper) if (!p.torn) { p.torn = true; FlyOff(p.go, gb.root.TransformDirection(p.n) * 1.5f + Vector3.up * 1.5f, 1f); }
                            unPhase = 2; unT = 0; Sfx("candy_special", 0.45f, 1.3f);
                            FxBurst("star", gb.root.position + Vector3.up * gb.size.y, 10, 1.4f, 0.05f, FxGold, 1f, 1f);
                        }
                    }
                    break;
                }
            case 2: // 열기 버튼 기다림: 상자가 콩콩
                {
                    float hop = Mathf.Max(0, Mathf.Sin(unT * 5f)) * 0.03f * (Mathf.Repeat(unT, 2f) < 0.65f ? 1 : 0);
                    gb.root.localPosition = new Vector3(0, TableY + hop, 0);
                    break;
                }
            case 3: // 열리는 중
                {
                    float k = Mathf.Clamp01(unT / 0.9f);
                    if (gb.lid) { gb.lid.transform.localPosition = new Vector3(0.25f * k, gb.size.y - Mathf.Min(0.1f, gb.size.y * 0.28f) + 0.5f * Mathf.Sin(k * Mathf.PI * 0.9f), -0.1f * k); gb.lid.transform.localRotation = Quaternion.Euler(-40 * k, 0, -100 * k); if (k >= 1) { FlyOff(gb.lid, new Vector3(1.2f, 0.8f, -0.4f), 0.8f); gb.lid = null; } }
                    if (openItem)
                    {
                        float r = Mathf.Clamp01((unT - 0.1f) / 0.7f);
                        float s = Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0, 1, r)) * (1 + 0.2f * Mathf.Sin(r * Mathf.PI));
                        openItem.transform.localScale = Vector3.one * s;
                        openItem.transform.localPosition = new Vector3(0, TableY + Mathf.Lerp(0.02f, gb.size.y + 0.03f, Mathf.SmoothStep(0, 1, r)) + Mathf.Sin(r * Mathf.PI) * 0.15f, 0);
                        openItem.transform.localRotation = Quaternion.Euler(0, r * 360f, 0);
                    }
                    if (unT > 0.3f && unT - dt <= 0.3f)
                    {
                        var p = gb.root.position + Vector3.up * gb.size.y;
                        FxBurst("conf", p, 40, 2.6f, 0.04f, FxParty, 1.6f, 2.5f);
                        FxBurst("star", p, 18, 2.2f, 0.06f, FxGold, 1.3f, 1f);
                        Sfx("ta_da", 0.6f);
                    }
                    if (unT > 1.1f) { unPhase = 4; unT = 0; MarkOpened(); }
                    break;
                }
            case 4: // 짠!
                {
                    if (openItem) { openItem.transform.localScale = Vector3.one; AnimItem(openItem, openItemDef, unT, new Vector3(0, TableY + gb.size.y + 0.03f, 0), 0); }
                    if (Mathf.Repeat(unT, 2.2f) < dt) FxBurst("star", gb.root.position + Vector3.up * (gb.size.y + openItemDef.h * 0.8f), 3, 0.6f, 0.04f, FxGold, 1f, -0.3f);
                    StudioDragInput();
                    break;
                }
        }
        if (camShakeStudio > 0) { camShakeStudio -= dt; cam.transform.position += UnityEngine.Random.insideUnitSphere * 0.006f; }
        if (Key("esc")) { if (unPhase == 4) LeaveStudio(); else LeaveStudio(); }
    }
    float camShakeStudio;

    void OpenBox()
    {
        if (unPhase != 2 || wrapBox == null) return;
        unPhase = 3; unT = 0;
        openItem = MakeGiftItem(openData.item, openData.look);
        openItem.transform.SetParent(studio, false);
        openItem.transform.localPosition = new Vector3(0, TableY + 0.02f, 0);
        openItem.transform.localScale = Vector3.one * 0.2f;
        Sfx("box_open", 0.7f);
    }

    void MarkOpened()
    {
        if (net == null || openId == null) return;
        giftOpenedLocal.Add(openId);
        var node = net.Get("gifts");
        if (node != null && node.TryGetValue(openId, out var v) && v is Dictionary<string, object> d)
        {
            string json = "{\"id\":" + AcJson.Esc(openId) + ",\"from\":" + AcJson.Esc(AcJson.S(d, "from")) + ",\"fromName\":" + AcJson.Esc(AcJson.S(d, "fromName")) +
                          ",\"to\":" + AcJson.Esc(AcJson.S(d, "to")) + ",\"toName\":" + AcJson.Esc(AcJson.S(d, "toName")) + ",\"ct\":" + AcJson.D(d, "ct").ToString("0") +
                          ",\"opened\":true,\"ot\":" + Math.Floor(NowMs).ToString("0") + ",\"d\":" + AcJson.Esc(AcJson.S(d, "d")) + "}";
            net.Put("gifts/" + openId, json);
        }
        nextGiftPoll = 0;
    }

    void UnwrapGUI()
    {
        var top = new Rect(10, 10, SW - 20, 50);
        GUI.Box(R(top), "", sBox);
        string title = openFrom + " 님이 보낸 선물";
        GUI.Label(new Rect(24, 22, SW - 200, 26), title, sLabel);
        float bw = SW < 640 ? 92 : 110;
        if (unPhase < 3 && GUI.Button(R(new Rect(SW - 20 - bw, 16, bw, 38)), "나중에 열기", sBtn)) { LeaveStudio(); return; }
        var big = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 22, wordWrap = true };
        float hy = top.yMax + 14;
        switch (unPhase)
        {
            case 0:
                Hint(hy, $"리본을 쭉 끌어당겨 풀어요!  ({pullCount}/3)", big);
                break;
            case 1:
                {
                    Hint(hy, "포장지를 쓱쓱 문질러 찢어요!", big);
                    break;
                }
            case 2:
                {
                    Hint(hy, "두근두근...", big);
                    float w = 200, h = 64;
                    var r = new Rect(SW / 2 - w / 2, SH - h - 40, w, h);
                    AcUiTheme.DrawGlow(r, 0.5f + 0.5f * Mathf.Sin(Time.time * 4f));
                    if (GUI.Button(R(r), "<size=22><b>상자 열기!</b></size>", sBtnBig)) OpenBox();
                    break;
                }
            case 4:
                {
                    float w = Mathf.Min(SW - 24, 460);
                    float h = string.IsNullOrEmpty(openData.msg) ? 130 : 200;
                    var r = new Rect(SW / 2 - w / 2, SH - h - 12, w, h);
                    GUI.Box(R(r), "", sBox);
                    GUI.Label(new Rect(r.x, r.y + 10, w, 34), "<size=24><b>" + openItemDef.ko + "!</b></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
                    float cy = r.y + 48;
                    if (!string.IsNullOrEmpty(openData.msg))
                    {
                        var card = new Rect(r.x + 16, cy, w - 32, 70);
                        AcUiTheme.Fill(card, new Color(1f, 0.98f, 0.9f), 3);
                        GUI.Label(new Rect(card.x + 10, card.y + 6, card.width - 20, card.height - 12), "<size=15>" + openData.msg + "</size>", new GUIStyle(sSmall) { wordWrap = true });
                        GUI.Label(new Rect(card.x, card.yMax - 2, card.width - 8, 20), "<size=12>From. " + openFrom + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
                        cy += 92;
                    }
                    float b2 = 130;
                    if (GUI.Button(R(new Rect(r.x + w / 2 - b2 - 6, r.yMax - 46, b2, 36)), "<b>고마워요!</b>", sBtn)) { LeaveStudio(); Emote("heart"); Toast(openFrom + " 님, 고마워요!"); return; }
                    if (GUI.Button(R(new Rect(r.x + w / 2 + 6, r.yMax - 46, b2, 36)), "닫기", sBtn)) { LeaveStudio(); return; }
                    break;
                }
        }
    }

    void Hint(float y, string text, GUIStyle st)
    {
        float w = Mathf.Min(SW - 30, 420);
        var r = new Rect(SW / 2 - w / 2, y, w, 44);
        AcUiTheme.Fill(r, new Color(1f, 1f, 1f, 0.8f), 3);
        GUI.Label(r, "<size=17>" + text + "</size>", st);
    }

    // ===== 평소 화면: 받은 선물·들고 있는 선물 =====
    void GiftHUD()
    {
        if (phase != Phase.Play) return;
        float y = SH * 0.36f;
        if (giftInbox.Count > 0)
        {
            var r = new Rect(12, y, IsMobile ? 170 : 160, IsMobile ? 50 : 42);
            AcUiTheme.DrawGlow(r, 0.5f + 0.5f * Mathf.Sin(Time.time * 3f));
            if (GUI.Button(R(r), $"<b>선물 풀어보기 ({giftInbox.Count})</b>", sBtnBig)) OpenUnwrap(0);
            y = r.yMax + 10;
        }
        var g = HeldGift;
        if (g != null)
        {
            var near = NearestRemote(2.4f);
            if (near != null && near.net != null)
            {
                float w = IsMobile ? 230 : 210, h = IsMobile ? 48 : 40;
                var r = new Rect(SW / 2 - w / 2, SH - (IsMobile ? 46 : 36) - 12 - 16 - 52 - 10 - h - 66, w, h);
                AcUiTheme.DrawGlow(r, 0.5f + 0.5f * Mathf.Sin(Time.time * 4f));
                if (GUI.Button(R(r), "<b>" + near.net.name + " 님에게 전달하기</b>", sBtnBig)) GiveGift(near);
            }
            else
            {
                var r = new Rect(12, y, IsMobile ? 170 : 160, 30);
                GUI.Box(R(r), "", sBox);
                GUI.Label(new Rect(r.x + 10, r.y + 5, r.width - 16, 22), $"<size=12>선물 {Held.g.Count}개 들고 있어요</size>", sSmall);
            }
        }
    }

    // ===== 매 프레임 =====
    void GiftUpdate(float dt)
    {
        if (phase == Phase.Play) PollGifts();
        ProcessGiftIcons();
        UpdateCarry();
        if (giftMode == GiftMode.Wrap) { me.speed = 0; WrapUpdate(dt); }
        else if (giftMode == GiftMode.Unwrap) { me.speed = 0; UnwrapUpdate(dt); }
    }

    void GiftGUI()
    {
        if (giftMode == GiftMode.Wrap) WrapGUI(); else UnwrapGUI();
        AskWindow();
    }
}
