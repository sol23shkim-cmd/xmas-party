// AcGift.cs - 선물 포장실: 선물 만들기 → 들고 다니다 친구 옆에서 전달 → 받은 사람이 풀어보기 (11-26)
// 만들기: 내용물(36가지, 움직이는 것들 + 내 캐릭터 미니어처) / 상자 크기 / 포장지 색·무늬·무늬 색 / 리본 색·무늬·모양 / 카드 한마디
// 풀어보기: 따로 떨어진 '선물 무대'에서 크게. 리본을 3번 끌어 풀고, 포장지를 문질러 찢고, '상자 열기!' → 내용물이 짠!
// 저장: 들고 있는 선물 = PlayerPrefs gift_held (마지막으로 포장한 1개, 왼손)
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
        new GiftItem("p_jack", "펀치 상자", 2, 0.55f, "boing"),
        new GiftItem("p_snakecan", "뱀 나오는 땅콩 캔", 2, 0.5f, "boing"),
        new GiftItem("p_chicken", "꽥꽥 고무 닭", 2, 0.42f, "squawk"),
        new GiftItem("p_whoopee", "뿡뿡 방귀 쿠션", 2, 0.16f, "puff"),
        new GiftItem("p_teeth", "딱딱 태엽 이빨", 2, 0.18f, "chatter"),
        new GiftItem("p_spider", "대롱대롱 거미", 2, 0.5f, "dangle"),
        new GiftItem("p_popflower", "물총 꽃", 2, 0.5f, "squirt"),
        new GiftItem("p_glasses", "콧수염 변장 안경", 2, 0.16f, "wiggle"),
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
    GiftData HeldGift => Held.g.Count > 0 ? Held.g[Held.g.Count - 1] : null;
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
        Color cream = new Color(0.86f, 0.66f, 0.45f), inside = new Color(0.72f, 0.54f, 0.36f);   // 속 상자: 크라프트 종이 색 (흰 상자는 밋밋해 보였음)
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
        foreach (var r in holder.GetComponentsInChildren<MeshRenderer>()) { r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = false; }   // 작은 물체 위 그림자 얼룩(계단 무늬) 없애기
        return holder;
    }

    // 코드로 만든 내용물 (움직이는 부분은 이름 붙인 자식)
    GameObject ProcItem(string id)
    {
        float keepDetail = AcPB.DefaultDetail; AcPB.DefaultDetail = 1.8f;   // 가까이서 크게 보므로 잘게 나눠 매끈하게
        try { return ProcItemInner(id); } finally { AcPB.DefaultDetail = keepDetail; }
    }
    GameObject ProcItemInner(string id)
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
        void GEye(AcPB pb, Vector3 c, float r, float look = 0.35f)
        {   // 왕눈이 (흰 공 + 아래로 쏠린 까만 눈동자)
            pb.Sphere(c, r, white, 6, 8);
            pb.Sphere(c + new Vector3(0, -r * look, r * 0.62f), r * 0.5f, black, 5, 7);
            pb.Sphere(c + new Vector3(r * 0.2f, r * 0.15f, r * 0.9f), r * 0.14f, white, 3, 4);
        }
        Color red2 = new Color(0.92f, 0.2f, 0.25f), yellow2 = new Color(1f, 0.85f, 0.3f);
        switch (id)
        {
            case "p_penguin":
                {
                    // 몸·머리를 각각 한 덩어리로 만들고 배·얼굴의 흰 부분은 정점 색으로 칠함 (겹친 껍질끼리 깜빡이던 문제)
                    Color Belly(Vector3 u, float fz, float fy0, float fy1) { float k = Mathf.SmoothStep(0, 1, (u.z - fz) / 0.18f) * Mathf.SmoothStep(0, 1, (u.y - fy0) / 0.2f) * Mathf.SmoothStep(0, 1, (fy1 - u.y) / 0.2f); return Color.Lerp(black, white, k); }
                    b.Ellipsoid(new Vector3(0, 0.165f, 0), new Vector3(0.13f, 0.165f, 0.12f), black, 16, 22, null, u => Belly(u, 0.15f, -0.95f, 0.75f), 0.8f);
                    b.Ellipsoid(new Vector3(0, 0.3f, 0.005f), new Vector3(0.1f, 0.09f, 0.092f), black, 14, 20, null, u => { float k = Mathf.SmoothStep(0, 1, (u.z - 0.25f) / 0.25f) * Mathf.SmoothStep(0, 1, (0.55f - u.y) / 0.25f); return Color.Lerp(black, white, k); }, 0.85f);
                    Face2(b, new Vector3(0, 0.3f, 0.094f), 0.085f, 0.36f);
                    b.Cylinder(new Vector3(0, 0.287f, 0.09f), new Vector3(0, 0.28f, 0.13f), 0.017f, 0.002f, orange, 8);
                    b.Ellipsoid(new Vector3(-0.05f, 0.012f, 0.05f), new Vector3(0.036f, 0.013f, 0.052f), orange, 5, 8);
                    b.Ellipsoid(new Vector3(0.05f, 0.012f, 0.05f), new Vector3(0.036f, 0.013f, 0.052f), orange, 5, 8);
                    b.Ellipsoid(new Vector3(-0.128f, 0.17f, 0), new Vector3(0.026f, 0.09f, 0.05f), black, 6, 8, Quaternion.Euler(0, 0, -15));
                    b.Ellipsoid(new Vector3(0.128f, 0.17f, 0), new Vector3(0.026f, 0.09f, 0.05f), black, 6, 8, Quaternion.Euler(0, 0, 15));
                    // 목도리: 몸에서 살짝 띄운 도넛 + 늘어진 끝
                    var red = new Color(0.9f, 0.2f, 0.25f);
                    b.Torus(new Vector3(0, 0.232f, 0.004f), Vector3.up, 0.103f, 0.02f, red, 18, 7);
                    b.Ellipsoid(new Vector3(0.06f, 0.19f, 0.105f), new Vector3(0.022f, 0.04f, 0.012f), red, 5, 7, Quaternion.Euler(0, 0, -12));
                    Part("body", b, Vector3.zero);
                    break;
                }
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
            case "p_snakecan":
                {
                    float baseY = BoingBase(id);
                    var bx = new AcPB();
                    if (id == "p_jack")
                    {   // 줄무늬 상자 + 열린 뚜껑
                        for (int k = 0; k < 4; k++) bx.Box(new Vector3(0, 0.0175f + k * 0.035f, 0), new Vector3(0.16f, 0.035f, 0.16f), k % 2 == 0 ? new Color(0.35f, 0.6f, 0.95f) : new Color(0.98f, 0.96f, 0.92f));
                        bx.Star(new Vector3(0, 0.07f, 0.082f), Vector3.forward, Vector3.up, 0.04f, 0.017f, 0.006f, yellow2);
                        bx.Cylinder(new Vector3(0.082f, 0.07f, 0), new Vector3(0.11f, 0.07f, 0), 0.006f, 0.006f, yellow2, 8);   // 태엽 손잡이
                        bx.Box(new Vector3(0.115f, 0.085f, 0), new Vector3(0.008f, 0.04f, 0.012f), yellow2);
                        var lidB = new AcPB(); lidB.Box(new Vector3(0, 0.006f, 0.08f), new Vector3(0.165f, 0.012f, 0.165f), red2);   // 뒤쪽 경첩
                        var lidGo = ProcObj("jlid", lidB.M("jlid"), root.transform); lidGo.transform.localPosition = new Vector3(0, 0.14f, -0.08f); lidGo.transform.localRotation = Quaternion.Euler(-120, 0, 0);
                    }
                    else if (id == "p_snakecan")
                    {   // 땅콩 캔 (상표 띠) + 옆으로 날아간 뚜껑
                        bx.Cylinder(Vector3.zero, new Vector3(0, 0.15f, 0), 0.07f, 0.07f, new Color(0.85f, 0.86f, 0.9f), 20);
                        bx.Cylinder(new Vector3(0, 0.03f, 0), new Vector3(0, 0.12f, 0), 0.0715f, 0.0715f, new Color(0.95f, 0.75f, 0.2f), 20, false);
                        for (int k = 0; k < 5; k++) { float aa = -0.5f + k * 0.25f; bx.Ellipsoid(new Vector3(Mathf.Sin(aa) * 0.072f, 0.075f + (k % 2) * 0.012f, Mathf.Cos(aa) * 0.072f), new Vector3(0.012f, 0.008f, 0.004f), new Color(0.6f, 0.38f, 0.2f), 4, 6, Quaternion.Euler(0, aa * 57f, 25)); }
                        bx.Disk(new Vector3(0, 0.149f, 0), Vector3.up, 0.065f, new Color(0.2f, 0.18f, 0.2f), 18, false);
                        var lidB = new AcPB(); lidB.Cylinder(Vector3.zero, new Vector3(0, 0.012f, 0), 0.073f, 0.073f, new Color(0.9f, 0.9f, 0.95f), 20);
                        var lidGo = ProcObj("jlid", lidB.M("jlid"), root.transform); lidGo.transform.localPosition = new Vector3(0.12f, 0.02f, 0.05f); lidGo.transform.localRotation = Quaternion.Euler(10, 0, 70);
                    }
                    else
                    {   // 물총 꽃: 화분
                        bx.Cylinder(Vector3.zero, new Vector3(0, 0.12f, 0), 0.07f, 0.09f, new Color(0.9f, 0.5f, 0.3f), 16);
                        bx.Torus(new Vector3(0, 0.12f, 0), Vector3.up, 0.09f, 0.012f, new Color(0.95f, 0.58f, 0.36f), 18, 6);
                        bx.Disk(new Vector3(0, 0.118f, 0), Vector3.up, 0.085f, new Color(0.35f, 0.25f, 0.18f), 16, false);
                    }
                    Part("base", bx, Vector3.zero);
                    // 늘어나는 부분 (높이 0.2, 아래가 원점): 상자 = 용수철, 캔 = 줄무늬 뱀 몸통, 꽃 = 꽃대
                    var sp = new AcPB(); var pts = new List<Vector3>(); var rs = new List<float>(); var cs2 = new List<Color>();
                    if (id == "p_jack")
                        for (int i = 0; i <= 80; i++) { float aa = i / 80f * Mathf.PI * 2 * 6; pts.Add(new Vector3(Mathf.Cos(aa) * 0.035f, i / 80f * 0.2f, Mathf.Sin(aa) * 0.035f)); rs.Add(0.006f); cs2.Add(new Color(0.85f, 0.85f, 0.9f)); }
                    else if (id == "p_snakecan")
                        for (int i = 0; i <= 60; i++) { float u = i / 60f, aa = u * Mathf.PI * 2 * 2.5f; pts.Add(new Vector3(Mathf.Cos(aa) * 0.022f * (1 - u * 0.5f), u * 0.2f, Mathf.Sin(aa) * 0.022f * (1 - u * 0.5f))); rs.Add(0.03f - u * 0.006f); cs2.Add((i / 4) % 2 == 0 ? new Color(0.35f, 0.75f, 0.35f) : yellow2); }
                    else
                        for (int i = 0; i <= 20; i++) { float u = i / 20f; pts.Add(new Vector3(Mathf.Sin(u * 3f) * 0.01f, u * 0.2f, 0)); rs.Add(0.008f); cs2.Add(new Color(0.35f, 0.65f, 0.35f)); }
                    sp.Tube(pts, rs, Color.white, id == "p_snakecan" ? 10 : 5, cs2);
                    if (id == "p_popflower") sp.Leaf(new Vector3(0, 0.08f, 0), new Vector3(1, 0.5f, 0.2f), Vector3.forward, 0.07f, 0.035f, new Color(0.35f, 0.65f, 0.35f), new Color(0.5f, 0.75f, 0.4f), 0.15f);
                    var spGo = ProcObj("spring", sp.M("spring"), root.transform); spGo.transform.localPosition = new Vector3(0, baseY, 0);
                    var hd = new AcPB();
                    if (id == "p_jack")
                    {   // 빨간 권투 장갑 (앞으로 주먹)
                        hd.Ellipsoid(new Vector3(0, 0.06f, 0.02f), new Vector3(0.068f, 0.06f, 0.075f), red2, 12, 16, null, null, 0.8f);
                        hd.Ellipsoid(new Vector3(0.055f, 0.045f, 0.035f), new Vector3(0.025f, 0.032f, 0.03f), red2 * 0.95f, 8, 10, Quaternion.Euler(0, 0, -25));   // 엄지
                        hd.Cylinder(new Vector3(0, -0.005f, 0), new Vector3(0, 0.02f, 0), 0.045f, 0.05f, Color.white, 16);   // 손목
                        hd.Torus(new Vector3(0, 0.02f, 0), Vector3.up, 0.048f, 0.008f, Color.white, 16, 5);
                        hd.Ellipsoid(new Vector3(-0.025f, 0.09f, 0.07f), new Vector3(0.012f, 0.008f, 0.004f), Color.white * 0.98f, 4, 6);   // 반짝
                        GEye(hd, new Vector3(-0.022f, 0.075f, 0.085f), 0.016f); GEye(hd, new Vector3(0.022f, 0.075f, 0.085f), 0.016f);
                        hd.Tube(new[] { new Vector3(-0.02f, 0.045f, 0.093f), new Vector3(0, 0.038f, 0.096f), new Vector3(0.02f, 0.045f, 0.093f) }, new[] { 0.004f, 0.004f, 0.004f }, black, 5);
                    }
                    else if (id == "p_snakecan")
                    {   // 뱀 머리: 왕눈이 + 갈라진 혀
                        var g = new Color(0.35f, 0.75f, 0.35f);
                        hd.Ellipsoid(new Vector3(0, 0.035f, 0.015f), new Vector3(0.04f, 0.032f, 0.05f), g, 10, 14);
                        GEye(hd, new Vector3(-0.02f, 0.065f, 0.03f), 0.016f, 0.1f); GEye(hd, new Vector3(0.02f, 0.065f, 0.03f), 0.016f, 0.1f);
                        hd.Tube(new[] { new Vector3(0, 0.025f, 0.06f), new Vector3(0, 0.02f, 0.085f) }, new[] { 0.004f, 0.004f }, red2, 5);
                        hd.Tube(new[] { new Vector3(0, 0.02f, 0.085f), new Vector3(-0.01f, 0.018f, 0.1f) }, new[] { 0.003f, 0.002f }, red2, 4);
                        hd.Tube(new[] { new Vector3(0, 0.02f, 0.085f), new Vector3(0.01f, 0.018f, 0.1f) }, new[] { 0.003f, 0.002f }, red2, 4);
                        hd.Ellipsoid(new Vector3(-0.025f, 0.02f, 0.045f), new Vector3(0.008f, 0.005f, 0.003f), new Color(1f, 0.6f, 0.65f), 3, 5);
                        hd.Ellipsoid(new Vector3(0.025f, 0.02f, 0.045f), new Vector3(0.008f, 0.005f, 0.003f), new Color(1f, 0.6f, 0.65f), 3, 5);
                    }
                    else
                    {   // 물총 꽃: 가운데에 작은 물총 구멍
                        Color petal = new Color(1f, 0.55f, 0.7f);
                        for (int pp = 0; pp < 8; pp++) { float pa = pp * Mathf.PI / 4; var pd = new Vector3(Mathf.Cos(pa), Mathf.Sin(pa), 0.15f); hd.Leaf(new Vector3(0, 0.07f, 0), pd, Vector3.forward, 0.08f, 0.055f, petal, Color.Lerp(petal, Color.white, 0.4f), 0.05f, 3, 6); }
                        hd.Ellipsoid(new Vector3(0, 0.07f, 0.01f), new Vector3(0.05f, 0.05f, 0.025f), yellow2, 10, 14);
                        Face2(hd, new Vector3(0, 0.07f, 0.034f), 0.045f, 0.4f);
                        hd.Cylinder(new Vector3(0, 0.055f, 0.032f), new Vector3(0, 0.055f, 0.042f), 0.007f, 0.005f, new Color(0.3f, 0.6f, 0.95f), 8);
                    }
                    var hdGo = ProcObj("head", hd.M("head"), root.transform); hdGo.transform.localPosition = new Vector3(0, baseY + 0.2f, 0);
                    break;
                }
            case "p_chicken":
                {   // 꽥꽥 고무 닭: 노란 몸 + 긴 목(머리째 늘어남) + 쩍 벌린 부리
                    var yl = new Color(1f, 0.84f, 0.25f);
                    b.Ellipsoid(new Vector3(0, 0.1f, -0.02f), new Vector3(0.085f, 0.075f, 0.12f), yl, 12, 16, null, null, 0.8f);
                    b.Leaf(new Vector3(0, 0.12f, -0.12f), new Vector3(0, 0.6f, -1), Vector3.right, 0.06f, 0.06f, yl, yl * 1.05f, 0.2f, 0, 5);   // 꼬리
                    b.Ellipsoid(new Vector3(-0.08f, 0.1f, -0.02f), new Vector3(0.015f, 0.04f, 0.07f), yl * 0.95f, 6, 8);
                    b.Ellipsoid(new Vector3(0.08f, 0.1f, -0.02f), new Vector3(0.015f, 0.04f, 0.07f), yl * 0.95f, 6, 8);
                    foreach (float sx in new[] { -0.035f, 0.035f })
                    {   // 축 늘어진 다리
                        b.Tube(new[] { new Vector3(sx, 0.04f, 0), new Vector3(sx, 0.015f, 0.01f), new Vector3(sx, 0.006f, 0.03f) }, new[] { 0.007f, 0.006f, 0.006f }, orange, 6);
                        for (int k = -1; k <= 1; k++) b.Ellipsoid(new Vector3(sx + k * 0.01f, 0.004f, 0.045f), new Vector3(0.006f, 0.004f, 0.016f), orange, 3, 5, Quaternion.Euler(0, k * 25f, 0));
                    }
                    Part("body", b, Vector3.zero);
                    var nk = new AcPB();
                    nk.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0, 0.08f, 0.015f), new Vector3(0, 0.16f, 0.03f) }, new[] { 0.028f, 0.022f, 0.022f }, yl, 10);
                    nk.Sphere(new Vector3(0, 0.18f, 0.035f), 0.045f, yl, 10, 14);
                    for (int k = 0; k < 3; k++) nk.Sphere(new Vector3(0, 0.225f + k * 0.006f, 0.01f + k * 0.018f), 0.016f, red2, 5, 7);   // 볏
                    nk.Cylinder(new Vector3(0, 0.185f, 0.07f), new Vector3(0, 0.19f, 0.12f), 0.02f, 0.003f, orange, 8);     // 윗부리
                    nk.Cylinder(new Vector3(0, 0.168f, 0.07f), new Vector3(0, 0.145f, 0.11f), 0.016f, 0.003f, orange, 8);   // 아랫부리 (벌림)
                    nk.Ellipsoid(new Vector3(0, 0.168f, 0.075f), new Vector3(0.012f, 0.01f, 0.008f), red2 * 0.8f, 4, 6);   // 입 속
                    GEye(nk, new Vector3(-0.024f, 0.2f, 0.065f), 0.017f, 0.5f); GEye(nk, new Vector3(0.024f, 0.2f, 0.065f), 0.017f, 0.5f);
                    var nkGo = ProcObj("head", nk.M("head"), root.transform); nkGo.transform.localPosition = new Vector3(0, 0.15f, 0.07f);
                    break;
                }
            case "p_whoopee":
                {   // 뿡뿡 방귀 쿠션: 납작한 분홍 고무 + 주둥이
                    var pk = new Color(1f, 0.55f, 0.65f);
                    var cu = new AcPB();
                    cu.Ellipsoid(new Vector3(0, 0.05f, 0), new Vector3(0.14f, 0.05f, 0.12f), pk, 12, 18, null, u => Color.Lerp(pk, Color.white, Mathf.Max(0, u.y - 0.6f) * 0.8f), 0.75f);
                    cu.Torus(new Vector3(0, 0.05f, 0), Vector3.up, 0.13f, 0.006f, pk * 0.9f, 28, 5);
                    cu.Ellipsoid(new Vector3(-0.04f, 0.095f, 0.03f), new Vector3(0.03f, 0.006f, 0.015f), Color.white, 4, 6);   // 반짝
                    GEye(cu, new Vector3(-0.035f, 0.09f, 0.07f), 0.016f); GEye(cu, new Vector3(0.035f, 0.09f, 0.07f), 0.016f);
                    cu.Tube(new[] { new Vector3(-0.02f, 0.07f, 0.105f), new Vector3(0, 0.064f, 0.11f), new Vector3(0.02f, 0.07f, 0.105f) }, new[] { 0.004f, 0.004f, 0.004f }, black, 5);
                    var cuGo = ProcObj("cush", cu.M("cush"), root.transform);
                    var nz = new AcPB();
                    nz.Tube(new[] { new Vector3(0, 0.03f, -0.11f), new Vector3(0, 0.025f, -0.15f), new Vector3(0, 0.03f, -0.18f) }, new[] { 0.025f, 0.02f, 0.024f }, pk * 0.95f, 12);
                    nz.Torus(new Vector3(0, 0.03f, -0.18f), Vector3.forward, 0.022f, 0.005f, pk * 0.9f, 12, 5);
                    Part("nozzle", nz, Vector3.zero);
                    break;
                }
            case "p_teeth":
                {   // 딱딱 태엽 이빨: 아래턱(발 달림) + 위턱(움직임) + 태엽
                    var gum = new Color(1f, 0.45f, 0.55f);
                    b.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.03f, 0.065f), gum, 10, 16);
                    for (int k = 0; k < 7; k++) { float aa = (k - 3) * 0.32f; b.Box(new Vector3(Mathf.Sin(aa) * 0.06f, 0.062f, Mathf.Cos(aa) * 0.048f), new Vector3(0.018f, 0.022f, 0.012f), Color.white, Quaternion.Euler(0, aa * 57f, 0)); }
                    foreach (float sx in new[] { -0.04f, 0.04f }) b.Ellipsoid(new Vector3(sx, 0.006f, 0.02f), new Vector3(0.022f, 0.008f, 0.032f), orange, 5, 8);   // 발
                    Part("lower", b, Vector3.zero);
                    var up2 = new AcPB();
                    up2.Ellipsoid(new Vector3(0, 0.03f, 0.04f), new Vector3(0.082f, 0.03f, 0.066f), gum, 10, 16);
                    for (int k = 0; k < 7; k++) { float aa = (k - 3) * 0.32f; up2.Box(new Vector3(Mathf.Sin(aa) * 0.06f, -0.002f, 0.04f + Mathf.Cos(aa) * 0.048f), new Vector3(0.018f, 0.022f, 0.012f), Color.white, Quaternion.Euler(0, aa * 57f, 0)); }
                    GEye(up2, new Vector3(-0.03f, 0.065f, 0.06f), 0.017f); GEye(up2, new Vector3(0.03f, 0.065f, 0.06f), 0.017f);
                    var jaw = ProcObj("jaw", up2.M("jaw"), root.transform); jaw.transform.localPosition = new Vector3(0, 0.065f, -0.04f);   // 뒤쪽 경첩
                    var ky = new AcPB();
                    ky.Cylinder(Vector3.zero, new Vector3(0, 0, -0.03f), 0.005f, 0.005f, yellow2, 8);
                    ky.Torus(new Vector3(0.015f, 0, -0.035f), Vector3.forward, 0.012f, 0.004f, yellow2, 12, 5);
                    ky.Torus(new Vector3(-0.015f, 0, -0.035f), Vector3.forward, 0.012f, 0.004f, yellow2, 12, 5);
                    var key = ProcObj("key", ky.M("key"), root.transform, 0.15f); key.transform.localPosition = new Vector3(0, 0.035f, -0.065f);
                    break;
                }
            case "p_spider":
                {   // 대롱대롱 거미: 지팡이 사탕 걸이 + 실 + 털복숭이 거미
                    var stand = new AcPB();
                    stand.Cylinder(Vector3.zero, new Vector3(0, 0.012f, 0), 0.07f, 0.07f, new Color(0.55f, 0.36f, 0.25f), 18);
                    var cp = new List<Vector3>(); var cr = new List<float>(); var cc = new List<Color>();
                    for (int i = 0; i <= 30; i++) { float u = i / 30f; Vector3 pnt = u < 0.7f ? new Vector3(-0.05f, 0.012f + u / 0.7f * 0.42f, 0) : new Vector3(-0.05f + 0.05f * (1 - Mathf.Cos((u - 0.7f) / 0.3f * Mathf.PI)), 0.432f + 0.05f * Mathf.Sin((u - 0.7f) / 0.3f * Mathf.PI), 0); cp.Add(pnt); cr.Add(0.01f); cc.Add((i / 2) % 2 == 0 ? red2 : Color.white); }
                    stand.Tube(cp, cr, Color.white, 8, cc);
                    Part("stand", stand, Vector3.zero);
                    var th = new AcPB(); th.Cylinder(Vector3.zero, new Vector3(0, -0.2f, 0), 0.0015f, 0.0015f, new Color(0.95f, 0.95f, 1f), 4, false);
                    var thGo = ProcObj("thread", th.M("thread"), root.transform); thGo.transform.localPosition = new Vector3(0.05f, 0.43f, 0);
                    var spd = new AcPB(); var fur = new Color(0.18f, 0.16f, 0.22f);
                    spd.Ellipsoid(new Vector3(0, -0.05f, 0), new Vector3(0.05f, 0.045f, 0.05f), fur, 12, 16);
                    spd.Ellipsoid(new Vector3(0, -0.01f, 0.01f), new Vector3(0.006f, 0.012f, 0.006f), fur, 4, 5);
                    GEye(spd, new Vector3(-0.02f, -0.04f, 0.042f), 0.017f); GEye(spd, new Vector3(0.02f, -0.04f, 0.042f), 0.017f);
                    spd.Tube(new[] { new Vector3(-0.015f, -0.068f, 0.045f), new Vector3(0, -0.075f, 0.047f), new Vector3(0.015f, -0.068f, 0.045f) }, new[] { 0.003f, 0.003f, 0.003f }, new Color(1f, 0.5f, 0.6f), 5);
                    spd.Ellipsoid(new Vector3(0, -0.02f, 0.03f), new Vector3(0.025f, 0.006f, 0.006f), red2, 4, 6);   // 작은 리본
                    for (int k = 0; k < 8; k++)
                    {
                        float sx = k < 4 ? -1 : 1, a2 = (k % 4 - 1.5f) * 0.45f;
                        var p0 = new Vector3(sx * 0.04f, -0.05f, Mathf.Sin(a2) * 0.03f);
                        var p1 = p0 + new Vector3(sx * 0.04f, 0.03f, Mathf.Sin(a2) * 0.03f);
                        var p2 = p1 + new Vector3(sx * 0.03f, -0.06f, Mathf.Sin(a2) * 0.02f);
                        spd.Tube(new[] { p0, p1, p2 }, new[] { 0.006f, 0.005f, 0.004f }, fur, 5);
                    }
                    var spGo2 = ProcObj("spider", spd.M("spider"), root.transform); spGo2.transform.localPosition = new Vector3(0.05f, 0.23f, 0);
                    break;
                }
            case "p_glasses":
                {   // 콧수염 변장 안경: 까만 테 + 큰 코 + 덥수룩 콧수염 + 눈썹
                    Color fr = new Color(0.12f, 0.12f, 0.15f), skin = new Color(1f, 0.75f, 0.65f);
                    foreach (float sx in new[] { -0.045f, 0.045f }) { b.Torus(new Vector3(sx, 0.1f, 0), Vector3.forward, 0.035f, 0.007f, fr, 20, 6); b.Disk(new Vector3(sx, 0.1f, -0.002f), Vector3.forward, 0.032f, new Color(0.85f, 0.95f, 1f), 18); }
                    b.Tube(new[] { new Vector3(-0.012f, 0.105f, 0), new Vector3(0, 0.11f, 0), new Vector3(0.012f, 0.105f, 0) }, new[] { 0.005f, 0.005f, 0.005f }, fr, 5);
                    foreach (float sx in new[] { -1f, 1f }) b.Tube(new[] { new Vector3(sx * 0.08f, 0.1f, 0), new Vector3(sx * 0.085f, 0.1f, -0.08f), new Vector3(sx * 0.08f, 0.085f, -0.1f) }, new[] { 0.004f, 0.004f, 0.004f }, fr, 5);
                    b.Ellipsoid(new Vector3(0, 0.075f, 0.025f), new Vector3(0.022f, 0.03f, 0.028f), skin, 10, 12);
                    b.Sphere(new Vector3(0, 0.055f, 0.04f), 0.022f, skin, 10, 12);
                    foreach (float sx in new[] { -0.045f, 0.045f }) b.Ellipsoid(new Vector3(sx, 0.148f, 0.004f), new Vector3(0.03f, 0.008f, 0.008f), fr, 6, 8, Quaternion.Euler(0, 0, sx > 0 ? -10 : 10));
                    Part("frame", b, Vector3.zero);
                    var ms = new AcPB();
                    foreach (float sx in new[] { -1f, 1f }) ms.Tube(new[] { new Vector3(0, 0, 0), new Vector3(sx * 0.025f, -0.006f, 0.005f), new Vector3(sx * 0.05f, 0.002f, 0), new Vector3(sx * 0.062f, 0.015f, -0.005f) }, new[] { 0.012f, 0.012f, 0.008f, 0.003f }, new Color(0.3f, 0.2f, 0.15f), 8);
                    var msGo = ProcObj("stache", ms.M("stache"), root.transform); msGo.transform.localPosition = new Vector3(0, 0.03f, 0.045f);
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

    static float BoingBase(string id) => id == "p_jack" ? 0.12f : id == "p_snakecan" ? 0.13f : 0.11f;   // 늘어나는 부분이 시작하는 높이

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
                    // 쏙 들어갔다가(0~0.5초) 튀어나와 띠요옹
                    float p = Mathf.Repeat(t, 3f);
                    float k = p < 0.5f ? Mathf.Lerp(1f, 0.15f, Mathf.SmoothStep(0, 1, p / 0.5f)) : 1 + 0.5f * Mathf.Sin((p - 0.5f) * 12f) * Mathf.Exp(-(p - 0.5f) * 2.4f);
                    float bb = BoingBase(def.id);
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                    {
                        if (c.name == "spring") c.localScale = new Vector3(1, Mathf.Max(0.05f, k), 1);
                        if (c.name == "head")
                        {
                            float wob = p > 0.5f ? Mathf.Sin((p - 0.5f) * 9f) * Mathf.Exp(-(p - 0.5f) * 2f) : 0;
                            c.localPosition = new Vector3(0, bb + 0.2f * k, def.id == "p_jack" ? 0.04f * Mathf.Max(0, k - 1) * 3f : 0);
                            c.localRotation = def.id == "p_jack" ? Quaternion.Euler(-25f * wob, 0, 8f * wob) : Quaternion.Euler(0, 15f * wob, 14f * wob);
                        }
                    }
                    if (p >= 0.5f && p - Time.deltaTime < 0.5f && giftMode != GiftMode.None) Sfx(def.id == "p_jack" ? "punch" : def.id == "p_snakecan" ? "pop" : "boing", 0.45f);
                    break;
                }
            case "squirt":
                {   // 물총 꽃: 고개를 까딱하다가 찍! 물 뿜기
                    float p = Mathf.Repeat(t, 2.4f);
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                        if (c.name == "head")
                        {
                            float nod = p < 1.2f ? Mathf.Sin(p * 6f) * 8f : -18f * Mathf.Exp(-(p - 1.2f) * 4f);
                            c.localRotation = Quaternion.Euler(nod, 0, 6f * Mathf.Sin(t * 2f));
                            if (p >= 1.2f && p - Time.deltaTime < 1.2f && giftMode != GiftMode.None)
                            {
                                Sfx("squirt", 0.45f);
                                var fwd = c.forward; var at = c.TransformPoint(new Vector3(0, 0.055f, 0.045f));
                                for (int i = 0; i < 14; i++) FxOne("drop", at, (fwd * 1.6f + Vector3.up * 0.7f + UnityEngine.Random.insideUnitSphere * 0.25f) * UnityEngine.Random.Range(0.7f, 1.1f), 0.018f, new Color(0.55f, 0.8f, 1f), 0.7f, 5f);
                            }
                        }
                    break;
                }
            case "squawk":
                {   // 고무 닭: 목을 쭉 늘이며 꽥!
                    float p = Mathf.Repeat(t, 2.2f);
                    float st = p < 0.15f ? p / 0.15f : p < 0.6f ? 1 + 0.06f * Mathf.Sin(p * 60f) : Mathf.Max(0, 1 - (p - 0.6f) / 0.3f);
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                    {
                        if (c.name == "head") { c.localScale = new Vector3(1 - 0.12f * st, 1 + 0.55f * st, 1 - 0.12f * st); c.localRotation = Quaternion.Euler(-20f * st, 0, 0); }
                        if (c.name == "body") c.localScale = new Vector3(1 + 0.12f * st, 1 - 0.18f * st, 1 + 0.06f * st);
                    }
                    if (p < Time.deltaTime * 1.2f && giftMode != GiftMode.None) Sfx("squeak", 0.45f, UnityEngine.Random.Range(0.95f, 1.1f));
                    y = 0.02f * st;
                    break;
                }
            case "puff":
                {   // 방귀 쿠션: 부풀었다가 뿡! 하고 납작 + 폴짝
                    float p = Mathf.Repeat(t, 2.8f);
                    float inf = p < 1.6f ? Mathf.SmoothStep(0.55f, 1.15f, p / 1.6f) : p < 2.0f ? Mathf.Lerp(1.15f, 0.35f, (p - 1.6f) / 0.4f) : Mathf.Lerp(0.35f, 0.55f, (p - 2.0f) / 0.8f);
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                        if (c.name == "cush") c.localScale = new Vector3(1 + (1 - inf) * 0.15f, inf, 1 + (1 - inf) * 0.15f);
                    if (p >= 1.6f && p < 2.0f) { y = 0.05f * Mathf.Sin((p - 1.6f) / 0.4f * Mathf.PI); rz = 8f * Mathf.Sin(p * 50f); }
                    if (p >= 1.6f && p - Time.deltaTime < 1.6f && giftMode != GiftMode.None)
                    {
                        Sfx("pfft", 0.5f);
                        var back = tr.TransformPoint(new Vector3(0, 0.03f, -0.2f));
                        FxBurst("ring", back, 6, 0.6f, 0.03f, new[] { new Color(0.85f, 0.95f, 0.8f), Color.white }, 0.9f, -0.3f, null, -tr.forward);
                    }
                    break;
                }
            case "chatter":
                {   // 태엽 이빨: 딱딱거리며 동그랗게 총총
                    float a = t * 1.1f;
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                    {
                        if (c.name == "jaw") c.localRotation = Quaternion.Euler(-Mathf.Abs(Mathf.Sin(t * 14f)) * 32f, 0, 0);
                        if (c.name == "key") c.localRotation = Quaternion.Euler(0, 0, -t * 360f);
                    }
                    if (Mathf.Repeat(t, 1.6f) < Time.deltaTime * 1.2f && giftMode != GiftMode.None) Sfx("chatter", 0.4f);
                    var off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.12f;
                    tr.localPosition = basePos + off + Vector3.up * Mathf.Abs(Mathf.Sin(t * 14f)) * 0.012f;
                    tr.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 6f * Mathf.Sin(t * 14f));
                    tr.localScale = Vector3.one;
                    return;
                }
            case "dangle":
                {   // 거미: 실을 타고 내려왔다 올라갔다 + 다리 꼼지락
                    float d = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                    {
                        if (c.name == "thread") c.localScale = new Vector3(1, 0.3f + 1.0f * d, 1);
                        if (c.name == "spider") { c.localPosition = new Vector3(0.05f, 0.43f - 0.2f * (0.3f + 1.0f * d) + 0.0f, 0); c.localRotation = Quaternion.Euler(0, 30f * Mathf.Sin(t * 0.9f), 10f * Mathf.Sin(t * 5f)); c.localScale = new Vector3(1 + 0.05f * Mathf.Sin(t * 10f), 1 - 0.05f * Mathf.Sin(t * 10f), 1); }
                    }
                    break;
                }
            case "wiggle":
                {   // 변장 안경: 콧수염 씰룩, 안경 들썩
                    foreach (Transform c in holder.GetComponentsInChildren<Transform>())
                        if (c.name == "stache") c.localRotation = Quaternion.Euler(0, 0, 12f * Mathf.Sin(t * 9f) * (0.5f + 0.5f * Mathf.Sin(t * 1.3f)));
                    y = 0.02f * Mathf.Abs(Mathf.Sin(t * 3f)); yaw += 25f * Mathf.Sin(t * 0.8f);
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
        float objW = wrapBox != null ? Mathf.Sqrt(wrapBox.size.x * wrapBox.size.x + wrapBox.size.z * wrapBox.size.z) + 0.3f : 0.7f;   // 돌려 봐도 옆이 안 잘리게 대각선 폭
        if (giftMode == GiftMode.Wrap && wrapTab == 0) { float ih = GiftItemById(wrapDraft.item).h; objH = Mathf.Max(0.32f, ih * 1.5f); objW = Mathf.Max(0.5f, ih * 1.6f); }
        if (giftMode == GiftMode.Unwrap && unPhase >= 3) objH = (wrapBox != null ? wrapBox.size.y : 0.4f) + (openItemDef != null ? openItemDef.h * RevealK : 0.4f) + 0.25f;
        bool portrait = Screen.height > Screen.width;
        float vfov = cam.fieldOfView * Mathf.Deg2Rad;
        float need = Mathf.Max(objH / (portrait ? 0.42f : 0.6f), objW / Mathf.Max(0.3f, cam.aspect) / 0.85f);
        float dist = need / (2f * Mathf.Tan(vfov / 2f)) * studioZoom + 0.3f;
        var focus = StudioPos + Vector3.up * (TableY + objH * (portrait ? 0.18f : 0.38f));
        float pitch = 22f;
        var rot = Quaternion.Euler(pitch, studioYaw + 180f, 0);
        var want = focus - rot * Vector3.forward * dist;
        if (!camStudioInit || snap) { camStudioPos = want; camStudioInit = true; }
        camStudioPos = Vector3.Lerp(camStudioPos, want, 1 - Mathf.Exp(-dt * 6f));
        cam.transform.position = camStudioPos;
        cam.transform.rotation = Quaternion.LookRotation(focus - camStudioPos);
        // 깊이 정밀도: 휴대폰은 깊이 버퍼가 거칠어서 가까운 면/먼 면 비율이 크면 맞닿은 면이 지글지글 깨짐 → 무대 크기에 맞게 좁힘
        cam.nearClipPlane = Mathf.Clamp(dist * 0.35f, 0.2f, 1f);
        cam.farClipPlane = 30f;
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
        bool replaced = Held.g.Count > 0;
        Held.g.Clear(); Held.g.Add(g); SaveHeld();   // 마지막으로 포장한 하나만 들고 다님
        var keep = JsonUtility.FromJson<GiftData>(JsonUtility.ToJson(wrapDraft)); keep.msg = ""; PlayerPrefs.SetString("gift_last", JsonUtility.ToJson(keep));
        LeaveStudio();
        Emote("cheer");
        Sfx("ta_da", 0.5f);
        FxBurst("star", me.transform.position + Vector3.up * 1.2f, 16, 2f, 0.06f, FxGold, 1.1f, 1.5f);
        Toast(replaced ? "새 선물로 바꿔 들었어요! 친구 가까이 가면 '전달하기'가 생겨요" : "포장 완료! 친구 가까이 가면 '전달하기'가 생겨요");
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
                    GUI.Label(new Rect(lx, cy, SW - 60, 22), "<size=12>끌어서 돌려 보기.  받는 사람이 상자를 열면 짠! 하고 나와요</size>", sSmall);
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
        if (key == carryKey && (key == "" || carryObj)) return;   // 캐릭터를 다시 만들면 손에 든 것도 사라짐 → 다시 쥠
        carryKey = key;
        if (carryObj) { if (me) me.HoldLeft(null, 0); Destroy(carryObj); }
        if (key == "" || me == null) return;
        carryObj = AcCatalog.Spawn("gift_box", me.transform, new Dictionary<string, Color> { { "box", Hex(g.paper) }, { "ribbon", Hex(g.ribbon) } });
        me.HoldLeft(carryObj, 0.34f);   // 왼손에 쥠
    }

    void GiveGift(Remote r)
    {
        var g = HeldGift; if (g == null || r == null || r.net == null || net == null) return;
        string id = "g" + Guid.NewGuid().ToString("N").Substring(0, 10);
        string json = "{\"id\":" + AcJson.Esc(id) + ",\"from\":" + AcJson.Esc(myKey) + ",\"fromName\":" + AcJson.Esc(uiName.Trim()) +
                      ",\"to\":" + AcJson.Esc(NameKey(r.net.name)) + ",\"toName\":" + AcJson.Esc(r.net.name) + ",\"ct\":" + Math.Floor(NowMs).ToString("0") +
                      ",\"opened\":false,\"d\":" + AcJson.Esc(JsonUtility.ToJson(g)) + "}";
        net.Put("gifts/" + id, json);
        Held.g.Clear(); SaveHeld();
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
            Toast(giftPrimed ? freshFrom + " 님이 선물을 줬어요! 오른쪽 위 '선물 열기'를 눌러요" : $"받은 선물이 {giftInbox.Count}개 있어요! 오른쪽 위 '선물 열기'를 눌러요");
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
                    if (gb.bandX) gb.bandX.transform.localScale = new Vector3(1, st, 1);
                    if (gb.bandZ) gb.bandZ.transform.localScale = new Vector3(1, st, 1);
                    gb.bow.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(unT * 3) * 6f, Mathf.Sin(unT * 4.3f) * 4f);
                    if (ptrUp && !ptrOnUI && ptrMoved && (ptrPos - ptrStart).magnitude > Screen.height * 0.06f)
                    {
                        pullCount++;
                        Sfx("untie", 0.6f, 0.9f + pullCount * 0.1f);
                        FxBurst("conf", gb.bow.transform.position, 6, 1.2f, 0.035f, new[] { Hex(openData.ribbon) }, 0.9f, 3f);
                        float sf = gb.bow.transform.localScale.x;
                        gb.bow.transform.localScale = Vector3.one * sf * 0.85f;
                        // 당길 때마다 눈에 띄게 한 갈래씩 풀림: 1번째 가로 끈, 2번째 세로 끈, 3번째 리본 매듭
                        if (pullCount == 1 && gb.bandX) { FlyOff(gb.bandX, Vector3.up * 1.4f + cam.transform.right * 0.9f, 1.0f); gb.bandX = null; camShakeStudio = 0.12f; }
                        if (pullCount == 2 && gb.bandZ) { FlyOff(gb.bandZ, Vector3.up * 1.4f - cam.transform.right * 0.9f, 1.0f); gb.bandZ = null; camShakeStudio = 0.12f; }
                        if (pullCount >= 3)
                        {
                            var up = Vector3.up * 1.6f;
                            FlyOff(gb.bow, up + off.normalized * 1.2f + cam.transform.right * 0.5f, 1.2f); gb.bow = null;
                            if (gb.bandX) { FlyOff(gb.bandX, up + cam.transform.right * 0.8f, 1.0f); gb.bandX = null; }
                            if (gb.bandZ) { FlyOff(gb.bandZ, up - cam.transform.right * 0.8f, 1.0f); gb.bandZ = null; }
                            unPhase = 1; Sfx("candy_special", 0.4f, 1.2f);
                        }
                    }
                    if (!ptrPressed) { if (gb.bandX) gb.bandX.transform.localScale = Vector3.Lerp(gb.bandX.transform.localScale, Vector3.one, dt * 10); if (gb.bandZ) gb.bandZ.transform.localScale = Vector3.Lerp(gb.bandZ.transform.localScale, Vector3.one, dt * 10); }
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
                        float s = Mathf.Lerp(0.2f, RevealK, Mathf.SmoothStep(0, 1, r)) * (1 + 0.25f * Mathf.Sin(r * Mathf.PI));
                        openItem.transform.localScale = Vector3.one * s;
                        openItem.transform.localPosition = new Vector3(0, TableY + Mathf.Lerp(0.02f, gb.size.y + 0.03f, Mathf.SmoothStep(0, 1, r)) + Mathf.Sin(r * Mathf.PI) * 0.35f, 0);   // 높이 튀어 올랐다가 상자 위에 내려앉음
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
                    if (openItem) { AnimItem(openItem, openItemDef, unT, new Vector3(0, TableY + gb.size.y + 0.03f, 0), 0); openItem.transform.localScale *= RevealK; }
                    if (Mathf.Repeat(unT, 2.2f) < dt) FxBurst("star", gb.root.position + Vector3.up * (gb.size.y + openItemDef.h * 0.8f), 3, 0.6f, 0.04f, FxGold, 1f, -0.3f);
                    StudioDragInput();
                    break;
                }
        }
        if (camShakeStudio > 0) { camShakeStudio -= dt; cam.transform.position += UnityEngine.Random.insideUnitSphere * 0.006f; }
        if (Key("esc")) { if (unPhase == 4) LeaveStudio(); else LeaveStudio(); }
    }
    float camShakeStudio;
    const float RevealK = 1.5f;   // 짠! 나온 내용물은 상자보다 돋보이게 1.5배

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
                Hint(hy, $"리본을 위로 쭉 끌어당겨요!  ({pullCount}/3)", big);
                if (!ptrPressed && wrapBox != null && wrapBox.bow)
                {   // 손가락 안내: 리본에서 위로 올라가는 동그라미
                    var sp = cam.WorldToScreenPoint(wrapBox.bow.transform.position);
                    if (sp.z > 0)
                    {
                        float t = Mathf.Repeat(Time.time, 1.4f) / 1.1f;
                        if (t <= 1f)
                        {
                            float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK - t * 110f;
                            var k0 = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Sin(t * Mathf.PI));
                            AcUiTheme.Circle(new Rect(x - 20, y - 20, 40, 40), new Color(1f, 1f, 1f, 0.85f));
                            AcUiTheme.CircleRing(new Rect(x - 20, y - 20, 40, 40), AcUiTheme.Accent);
                            GUI.color = k0;
                        }
                    }
                }
                break;
            case 1:
                {
                    if (wrapBox != null && wrapBox.paper != null) { int all = wrapBox.paper.Count, torn = wrapBox.paper.Count(pp => pp.torn); Hint(hy, $"포장지를 쓱쓱 문질러 찢어요!  ({Mathf.RoundToInt(100f * torn / Mathf.Max(1, all))}%)", big); }
                    else Hint(hy, "포장지를 쓱쓱 문질러 찢어요!", big);
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
        // 받은 선물: 오른쪽 위(참가자 버튼 아래) 작은 배지 — 캐릭터 옆을 가리지 않게
        if (giftInbox.Count > 0)
        {
            float w = IsMobile ? 150 : 140, h = IsMobile ? 42 : 36;
            var r = new Rect(SW - 12 - w, (IsMobile ? 104 : 96), w, h);
            AcUiTheme.DrawGlow(r, 0.5f + 0.5f * Mathf.Sin(Time.time * 3f));
            if (GUI.Button(R(r), $"<b>선물 열기 ({giftInbox.Count})</b>", sBtnBig)) OpenUnwrap(0);
        }
        // 들고 있는 선물은 손에 보이므로 글자는 생략. 친구 가까이에서만 '전달하기'
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
