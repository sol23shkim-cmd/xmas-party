// XmasSyncTest.cs — 크리스마스 광장 (2D 그림책 버전, Unity 6 + URP)
// 배경: Assets/StreamingAssets/village.png (Gemini로 생성한 그림)
// 캐릭터: 코드로 그리는 작은 사람 스프라이트 (코트·모자·목도리·피부 색 커스터마이징)
//
// 조작: WASD/방향키 또는 클릭으로 이동 · 문/트리 앞에서 E 키 · ESC로 닫기 · F1 동기화 상태
// 동기화: 공유 폴더의 playerNN.json 슬롯 파일 (각자 자기 파일만 쓰고 남의 파일은 읽기만)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

[Serializable]
public class NetState
{
    public string id = "";
    public string name = "";
    public string look = "0,0,0,0";   // 코트,모자,목도리,피부
    public float x, z;                // z = 화면 y
    public int face = 1;              // 1 오른쪽, -1 왼쪽
    public bool moving;
    public string zone = "";
    public string wish = "";
    public bool left;
    public bool empty;
    public long seq;
    public double t;
}

public class PeerInfo
{
    public NetState data;
    public long seq = -1;
    public long seqAt;
    public float gap, lat;
    public int err;
    public PeerInfo Clone() { return (PeerInfo)MemberwiseClone(); }
}

public class XmasSyncTest : MonoBehaviour
{
    // ===== 설정 =====
    const int MaxSlots = 12;
    const int WriteMs = 150;
    const int ReadMs = 250;
    const float Speed = 2.4f;
    const float WorldH = 10f;                  // 배경 높이 (월드 단위)
    const float StreetTop = -3.25f, StreetBottom = -4.75f, XLimit = 8.5f;

    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static long NowMs => Clock.ElapsedMilliseconds;
    static double UnixMs => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<XmasSyncTest>() != null) return;
        new GameObject("XmasSyncTest").AddComponent<XmasSyncTest>();
    }

    // ===== 커스터마이징 =====
    static readonly Color[] CoatColors =
    {
        new Color(0.78f, 0.22f, 0.22f), new Color(0.22f, 0.45f, 0.33f), new Color(0.24f, 0.36f, 0.58f), new Color(0.85f, 0.62f, 0.25f),
        new Color(0.45f, 0.3f, 0.5f), new Color(0.93f, 0.9f, 0.84f), new Color(0.35f, 0.28f, 0.24f), new Color(0.9f, 0.55f, 0.6f),
    };
    static readonly Color[] ScarfColors =
    {
        new Color(0.85f, 0.25f, 0.25f), new Color(0.95f, 0.95f, 0.92f), new Color(0.25f, 0.55f, 0.4f), new Color(0.95f, 0.75f, 0.3f),
        new Color(0.35f, 0.55f, 0.75f), new Color(0.9f, 0.6f, 0.7f),
    };
    static readonly Color[] SkinColors = { new Color(1f, 0.86f, 0.74f), new Color(0.95f, 0.78f, 0.62f), new Color(0.82f, 0.62f, 0.45f), new Color(0.6f, 0.42f, 0.3f) };
    static readonly string[] HatNames = { "산타 모자", "털모자", "귀마개", "루돌프", "중절모", "없음" };

    struct Look { public int coat, hat, scarf, skin; }
    static string LookStr(Look l) => $"{l.coat},{l.hat},{l.scarf},{l.skin}";
    static Look ParseLook(string s)
    {
        var l = new Look();
        try
        {
            var p = s.Split(',');
            l.coat = Mathf.Clamp(int.Parse(p[0]), 0, CoatColors.Length - 1);
            l.hat = Mathf.Clamp(int.Parse(p[1]), 0, HatNames.Length - 1);
            l.scarf = Mathf.Clamp(int.Parse(p[2]), 0, ScarfColors.Length - 1);
            l.skin = Mathf.Clamp(int.Parse(p[3]), 0, SkinColors.Length - 1);
        }
        catch { }
        return l;
    }

    // ===== 구역 (배경 그림 속 문 위치) =====
    class Zone
    {
        public string id, label, desc;
        public float x, halfW;
    }

    // 배경 이미지 가로 위치(0~1)를 월드 x로 바꿔서 정의
    static float ImgX(float u) => (u - 0.5f) * WorldH * (2752f / 1536f);

    static readonly Zone[] Zones =
    {
        new Zone { id = "spot",  label = "시계탑 집 · 틀린 그림 찾기", x = ImgX(126f / 1280f),  halfW = 0.75f, desc = "같은 그림 두 장에서 다른 곳을 먼저 찾는 사람이 이겨요." },
        new Zone { id = "mines", label = "분홍 집 · 눈 지뢰찾기",     x = ImgX(397f / 1280f),  halfW = 0.75f, desc = "같은 지뢰밭을 동시에 풀어요. 먼저 다 찾으면 승리, 지뢰를 밟으면 패배!" },
        new Zone { id = "tree",  label = "소원 트리",                 x = ImgX(642f / 1280f),  halfW = 1.0f,  desc = "올해의 소원을 트리에 걸어 보세요. 다른 사람의 소원도 볼 수 있어요." },
        new Zone { id = "ark",   label = "베이커리 · 선물 깨기",      x = ImgX(910f / 1280f),  halfW = 0.8f,  desc = "각자 선물 벽돌을 깨면서 상대에게 방해 선물을 보내요." },
        new Zone { id = "photo", label = "빨간 사진관 · 포토존",      x = ImgX(1090f / 1280f), halfW = 0.8f,  desc = "모여서 찰칵! 사진은 내 PC의 '사진\\XmasPlaza' 폴더에 저장돼요." },
    };

    // ===== UI 상태 =====
    string uiPath = "", uiName = "", uiWish = "";
    Look myLook;
    string status = "공유 폴더 경로와 이름을 입력하고 입장하세요.";
    bool joined;
    string openZone;
    Zone nearZone;
    string myWish = "";
    string toast; float toastUntil;
    bool hideUI, showStats;

    // ===== 동기화 (스레드 공유) =====
    readonly object lk = new object();
    Thread thread;
    volatile bool running;
    string dir = "";
    string myId;
    NetState sharedMe = new NetState();
    Dictionary<int, PeerInfo> sharedPeers = new Dictionary<int, PeerInfo>();
    int sharedSlot = -1;
    string sharedStatus;
    float writeMsAvg, readMsAvg;
    int writeErr, readErr;
    bool conflict;

    // ===== 장면 =====
    Camera cam;
    Character me;
    Vector2? moveTarget;
    float worldW;
    readonly Dictionary<int, Character> remotes = new Dictionary<int, Character>();
    Dictionary<int, PeerInfo> peersView = new Dictionary<int, PeerInfo>();
    readonly Dictionary<string, (string name, string text)> wishes = new Dictionary<string, (string, string)>();
    readonly Dictionary<string, SpriteRenderer> ornaments = new Dictionary<string, SpriteRenderer>();
    readonly List<(Transform tr, float speed, float sway)> snow = new List<(Transform, float, float)>();
    readonly List<(SpriteRenderer sr, float phase, float baseA)> glows = new List<(SpriteRenderer, float, float)>();
    float fps;
    GUIStyle sLabel, sSmall, sPanel, sTag, sTagMe, sSign, sTitle, sPrompt, sWishCard, sDark, sBtn;
    Rect panelRect, uiBlockRect;
    Vector2 wishScroll;

    // ========================================================
    // 스프라이트 그리기 (흰색 모양 → SpriteRenderer.color로 색칠)
    static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    delegate float Sdf(float x, float y);                 // 단위: 픽셀, 음수 = 안쪽
    delegate Color Paint(float x, float y, float inside); // 색 (inside 0~1)

    static Sprite MakeSprite(string key, int w, int h, Vector2 pivot, float ppu, Sdf sdf, Paint paint = null, int seed = 1)
    {
        if (spriteCache.TryGetValue(key, out var cached)) return cached;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        var rnd = new System.Random(seed);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = sdf(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(0.5f - d);
                if (a <= 0) { px[y * w + x] = new Color32(255, 255, 255, 0); continue; }
                // 수채화 느낌: 가장자리 살짝 어둡게 + 결
                float grain = 0.93f + 0.07f * (float)rnd.NextDouble();
                float edge = Mathf.Clamp01(-d / 3f);
                float shade = Mathf.Lerp(0.86f, 1f, edge) * grain;
                Color c = paint != null ? paint(x + 0.5f, y + 0.5f, edge) : Color.white;
                c.r *= shade; c.g *= shade; c.b *= shade;
                c.a *= a;
                px[y * w + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
        spriteCache[key] = s;
        return s;
    }

    static float SdCircle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
    static float SdBox(float x, float y, float cx, float cy, float hw, float hh, float r)
    {
        float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r;
        return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }
    static float SdEllipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }
    // 위로 갈수록 좁아지는 둥근 사다리꼴 (코트 몸통)
    static float SdTaper(float x, float y, float cx, float y0, float y1, float hwBottom, float hwTop, float r)
    {
        float t = Mathf.Clamp01((y - y0) / (y1 - y0));
        float hw = Mathf.Lerp(hwBottom, hwTop, t);
        return SdBox(x, y, cx, (y0 + y1) / 2, hw, (y1 - y0) / 2, r);
    }

    // 캐릭터 부품 (128px = 캐릭터 키 1유닛)
    const float PPU = 128f;
    static Sprite SprShadow() => MakeSprite("shadow", 64, 24, new Vector2(0.5f, 0.5f), PPU, (x, y) => SdEllipse(x, y, 32, 12, 28, 9),
        (x, y, e) => new Color(0.2f, 0.25f, 0.35f, 0.28f * e + 0.05f));
    static Sprite SprLeg() => MakeSprite("leg", 14, 34, new Vector2(0.5f, 1f), PPU, (x, y) => SdBox(x, y, 7, 17, 5, 16, 4),
        (x, y, e) => y < 9 ? new Color(0.22f, 0.16f, 0.13f) : new Color(0.3f, 0.3f, 0.36f));
    static Sprite SprCoat() => MakeSprite("coat", 60, 64, new Vector2(0.5f, 0f), PPU, (x, y) => SdTaper(x, y, 30, 2, 62, 27, 18, 9));
    static Sprite SprCoatTrim() => MakeSprite("coattrim", 60, 64, new Vector2(0.5f, 0f), PPU,
        (x, y) => Mathf.Min(SdBox(x, y, 30, 5, 26, 4, 4), SdBox(x, y, 30, 32, 3, 28, 2)),
        (x, y, e) => new Color(0.97f, 0.95f, 0.9f));
    static Sprite SprArm() => MakeSprite("arm", 16, 40, new Vector2(0.5f, 0.92f), PPU, (x, y) => SdBox(x, y, 8, 20, 6, 18, 6));
    static Sprite SprMitten() => MakeSprite("mitten", 16, 16, new Vector2(0.5f, 0.5f), PPU, (x, y) => SdCircle(x, y, 8, 8, 6));
    static Sprite SprHead() => MakeSprite("head", 44, 44, new Vector2(0.5f, 0.5f), PPU, (x, y) => SdCircle(x, y, 22, 22, 20));
    static Sprite SprFace() => MakeSprite("face", 44, 44, new Vector2(0.5f, 0.5f), PPU,
        (x, y) => Mathf.Min(Mathf.Min(SdCircle(x, y, 27, 23, 2.4f), SdCircle(x, y, 35, 23, 2.4f)),
                   Mathf.Min(SdEllipse(x, y, 25, 15, 4, 2.6f), SdEllipse(x, y, 37, 15, 4, 2.6f))),
        (x, y, e) => y > 19 ? new Color(0.2f, 0.14f, 0.12f) : new Color(1f, 0.55f, 0.55f, 0.55f));
    static Sprite SprScarf() => MakeSprite("scarf", 52, 30, new Vector2(0.5f, 0.5f), PPU,
        (x, y) => Mathf.Min(SdBox(x, y, 26, 21, 22, 6, 6), SdBox(x, y, 16, 10, 5, 10, 4)));

    static Sprite SprHat(int kind)
    {
        switch (kind)
        {
            case 0: // 산타 모자
                return MakeSprite("hat0", 56, 56, new Vector2(0.5f, 0.12f), PPU,
                    (x, y) => Mathf.Min(Mathf.Min(SdBox(x, y, 28, 9, 23, 6, 6),
                        SdTaper(x, y, 30, 10, 40, 19, 6, 5)), SdCircle(x, y, 44, 40, 7)),
                    (x, y, e) => (y < 15 || SdCircle(x, y, 44, 40, 7) < 0) ? new Color(0.98f, 0.97f, 0.94f) : new Color(0.8f, 0.16f, 0.18f));
            case 1: // 털모자 (흰색 → 목도리 색으로 칠함)
                return MakeSprite("hat1", 56, 56, new Vector2(0.5f, 0.12f), PPU,
                    (x, y) => Mathf.Min(Mathf.Min(SdEllipse(x, y, 28, 14, 22, 18), SdCircle(x, y, 28, 36, 8)), SdBox(x, y, 28, 8, 22, 6, 5)),
                    (x, y, e) => y < 13 ? new Color(0.85f, 0.85f, 0.85f) : Color.white);
            case 2: // 귀마개
                return MakeSprite("hat2", 56, 56, new Vector2(0.5f, 0.3f), PPU,
                    (x, y) => Mathf.Min(Mathf.Min(SdCircle(x, y, 8, 12, 8), SdCircle(x, y, 48, 12, 8)),
                        Mathf.Abs(SdCircle(x, y, 28, 12, 22)) - 2.5f + (y < 12 ? 100 : 0)),
                    (x, y, e) => y < 18 ? new Color(0.98f, 0.92f, 0.9f) : new Color(0.75f, 0.2f, 0.25f));
            case 3: // 루돌프 뿔
                return MakeSprite("hat3", 56, 56, new Vector2(0.5f, 0.12f), PPU,
                    (x, y) => Mathf.Min(Mathf.Min(SdBox(x, y, 14, 22, 3, 16, 2), SdBox(x, y, 42, 22, 3, 16, 2)),
                        Mathf.Min(SdBox(x, y, 8, 30, 6, 2.5f, 2), SdBox(x, y, 48, 30, 6, 2.5f, 2))),
                    (x, y, e) => new Color(0.55f, 0.36f, 0.2f));
            case 4: // 중절모
                return MakeSprite("hat4", 56, 56, new Vector2(0.5f, 0.12f), PPU,
                    (x, y) => Mathf.Min(SdBox(x, y, 28, 8, 26, 4, 3), SdBox(x, y, 28, 22, 16, 13, 3)),
                    (x, y, e) => (y > 11 && y < 15) ? new Color(0.75f, 0.2f, 0.2f) : new Color(0.18f, 0.16f, 0.17f));
        }
        return null;
    }

    static Sprite SprSoftDot() => MakeSprite("dot", 32, 32, new Vector2(0.5f, 0.5f), 100f, (x, y) => SdCircle(x, y, 16, 16, 15),
        (x, y, e) => new Color(1, 1, 1, Mathf.Pow(e, 0.6f)));
    static Sprite SprGlow() => MakeSprite("glow", 64, 64, new Vector2(0.5f, 0.5f), 100f, (x, y) => SdCircle(x, y, 32, 32, 31),
        (x, y, e) => new Color(1, 0.85f, 0.5f, Mathf.Pow(e, 1.8f)));

    // ===== 캐릭터 =====
    class Character
    {
        public GameObject root;
        public Transform body, legL, legR, armL, armR;
        public SpriteRenderer[] parts;
        public int[] baseOrder;
        public float walkT;
        public int face = 1;
        public bool moving;
        public string name, zone, lookStr;
        public Vector2 pos;
    }

    static SpriteRenderer Part(Transform parent, string name, Sprite s, Vector2 local, int order, Color c)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = local;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    Character MakeCharacter(Look l)
    {
        var ch = new Character { lookStr = LookStr(l) };
        ch.root = new GameObject("Character");
        var coat = CoatColors[l.coat];
        var skin = SkinColors[l.skin];
        var scarf = ScarfColors[l.scarf];
        var list = new List<SpriteRenderer>();
        var orders = new List<int>();
        void Add(SpriteRenderer sr) { list.Add(sr); orders.Add(sr.sortingOrder); }

        Add(Part(ch.root.transform, "Shadow", SprShadow(), new Vector2(0, 0.0f), 0, Color.white));
        ch.body = new GameObject("Body").transform;
        ch.body.SetParent(ch.root.transform, false);
        var legL = Part(ch.body, "LegL", SprLeg(), new Vector2(-0.06f, 0.27f), 1, Color.white); Add(legL); ch.legL = legL.transform;
        var legR = Part(ch.body, "LegR", SprLeg(), new Vector2(0.06f, 0.27f), 1, Color.white); Add(legR); ch.legR = legR.transform;
        var armB = Part(ch.body, "ArmBack", SprArm(), new Vector2(-0.15f, 0.66f), 2, coat * 0.85f + Color.black * 0.15f); Add(armB); ch.armL = armB.transform;
        Add(Part(ch.body, "Coat", SprCoat(), new Vector2(0, 0.2f), 3, coat));
        Add(Part(ch.body, "Trim", SprCoatTrim(), new Vector2(0, 0.2f), 4, Color.white));
        var armF = Part(ch.body, "ArmFront", SprArm(), new Vector2(0.15f, 0.66f), 6, coat); Add(armF); ch.armR = armF.transform;
        Add(Part(armF.transform, "Mitten", SprMitten(), new Vector2(0, -0.27f), 7, scarf));
        Add(Part(ch.body, "Head", SprHead(), new Vector2(0, 0.83f), 8, skin));
        Add(Part(ch.body, "Face", SprFace(), new Vector2(0, 0.83f), 9, Color.white));
        Add(Part(ch.body, "Scarf", SprScarf(), new Vector2(0, 0.67f), 10, scarf));
        var hat = SprHat(l.hat);
        if (hat != null)
        {
            var hatColor = l.hat == 1 ? scarf : Color.white;
            Vector2 hp = l.hat == 2 ? new Vector2(0, 0.83f) : new Vector2(0, 0.94f);
            Add(Part(ch.body, "Hat", hat, hp, 11, hatColor));
        }
        ch.parts = list.ToArray();
        ch.baseOrder = orders.ToArray();
        return ch;
    }

    void PlaceCharacter(Character ch, Vector2 p, float dt)
    {
        ch.pos = p;
        float s = CharScale(p);
        ch.root.transform.position = new Vector3(p.x, p.y, 0);
        ch.root.transform.localScale = new Vector3(s * ch.face, s, 1);
        int baseOrder = 1000 + Mathf.RoundToInt((StreetTop + 0.5f - p.y) * 300f) * 16;   // 앞에 있을수록 위에 (최대 약 16000)
        for (int i = 0; i < ch.parts.Length; i++) ch.parts[i].sortingOrder = baseOrder + ch.baseOrder[i];

        if (ch.moving) ch.walkT += dt;
        float sw = ch.moving ? Mathf.Sin(ch.walkT * 11f) : 0f;
        ch.legL.localRotation = Quaternion.Euler(0, 0, sw * 22f);
        ch.legR.localRotation = Quaternion.Euler(0, 0, -sw * 22f);
        ch.armL.localRotation = Quaternion.Euler(0, 0, -sw * 25f);
        ch.armR.localRotation = Quaternion.Euler(0, 0, sw * 25f);
        float bob = ch.moving ? Mathf.Abs(Mathf.Cos(ch.walkT * 11f)) * 0.035f : Mathf.Sin(Time.time * 2f + p.x) * 0.006f;
        ch.body.localPosition = new Vector3(0, bob, 0);
    }

    static float CharScale(Vector2 p) => Mathf.Lerp(0.82f, 0.64f, Mathf.InverseLerp(StreetBottom, StreetTop, p.y));

    // ========================================================
    void Awake()
    {
        myId = Guid.NewGuid().ToString("N").Substring(0, 8);
        uiPath = PlayerPrefs.GetString("xmas_path", "");
        uiName = PlayerPrefs.GetString("xmas_name", "");
        myLook = ParseLook(PlayerPrefs.GetString("xmas_look", "0,0,0,0"));
        Application.targetFrameRate = 60;
        BuildScene();
        me = MakeCharacter(myLook);
        PlaceCharacter(me, new Vector2(-1.5f, -4.1f), 0);
    }

    void BuildScene()
    {
        // 3D 템플릿의 조명/볼륨은 그림 색을 바꾸므로 끄기
        foreach (var l in FindObjectsByType<Light>()) l.enabled = false;
        foreach (var v in FindObjectsByType<Volume>()) v.enabled = false;

        cam = Camera.main;
        if (!cam) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); }
        cam.orthographic = true;
        cam.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = false;

        // 배경 그림
        worldW = WorldH * 2752f / 1536f;
        var path = Path.Combine(Application.streamingAssetsPath, "village.png");
        var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
        bg.sortingOrder = -30000;
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            worldW = WorldH * tex.width / tex.height;
            bg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height / WorldH);
        }
        catch (Exception e) { Debug.LogError("[Xmas] 배경 그림을 못 읽었어요: " + path + " / " + e.Message); }

        // 창문 불빛 (은은하게 깜빡)
        (float u, float v, float r)[] windows =
        {
            (0.049f, 0.555f, 0.5f), (0.146f, 0.555f, 0.5f), (0.227f, 0.555f, 0.55f), (0.38f, 0.555f, 0.55f),
            (0.71f, 0.53f, 0.7f), (0.937f, 0.68f, 0.9f), (0.86f, 0.68f, 0.55f), (0.5f, 0.6f, 0.9f),
        };
        foreach (var w in windows)
        {
            var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
            g.sprite = SprGlow();
            g.sortingOrder = -29000;
            g.transform.position = new Vector3((w.u - 0.5f) * worldW, (0.5f - w.v) * WorldH, 0);
            g.transform.localScale = Vector3.one * w.r * 2.2f;
            float baseA = 0.22f;
            g.color = new Color(1, 1, 1, baseA);
            glows.Add((g, Random.Range(0f, 10f), baseA));
        }

        // 눈: 뒤쪽(작고 느림) + 앞쪽(크고 빠름)
        for (int i = 0; i < 220; i++)
        {
            bool front = i % 4 == 0;
            var s = new GameObject("Snow").AddComponent<SpriteRenderer>();
            s.sprite = SprSoftDot();
            s.sortingOrder = front ? 30000 : -20000;
            float size = front ? Random.Range(0.09f, 0.16f) : Random.Range(0.035f, 0.08f);
            s.transform.localScale = Vector3.one * size;
            s.color = new Color(1, 1, 1, front ? 0.85f : 0.75f);
            s.transform.position = new Vector3(Random.Range(-worldW / 2, worldW / 2), Random.Range(-5f, 5.5f), 0);
            snow.Add((s.transform, front ? Random.Range(0.9f, 1.4f) : Random.Range(0.35f, 0.7f), Random.Range(0f, 6f)));
        }
        FitCamera();
    }

    void FitCamera()
    {
        // 그림 밖이 보이지 않게 화면을 꽉 채우기
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        cam.orthographicSize = Mathf.Min(WorldH / 2f, worldW / 2f / aspect);
        // 화면이 가로로 길면 하늘 쪽을 잘라서 아래쪽 길이 항상 보이게
        cam.transform.position = new Vector3(0, -WorldH / 2f + cam.orthographicSize, -10);
    }

    // ========================================================
    // 입력
    Vector2 MoveAxis()
    {
        float h = 0, v = 0;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1;
        }
#else
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1;
#endif
        return new Vector2(h, v);
    }

    bool KeyDown(string key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        if (key == "e") return kb.eKey.wasPressedThisFrame;
        if (key == "esc") return kb.escapeKey.wasPressedThisFrame;
        if (key == "f1") return kb.f1Key.wasPressedThisFrame;
        return false;
#else
        if (key == "e") return Input.GetKeyDown(KeyCode.E);
        if (key == "esc") return Input.GetKeyDown(KeyCode.Escape);
        if (key == "f1") return Input.GetKeyDown(KeyCode.F1);
        return false;
#endif
    }

    bool ClickDown(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        pos = m != null ? m.position.ReadValue() : Vector2.zero;
        return m != null && m.leftButton.wasPressedThisFrame;
#else
        pos = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool TypingInField() => GUIUtility.keyboardControl != 0;

    // ========================================================
    void Update()
    {
        float dt = Time.deltaTime;
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(dt, 0.0001f), 0.05f);
        FitCamera();

        float t = Time.time;
        foreach (var s in snow)
        {
            var p = s.tr.position;
            p.y -= s.speed * dt;
            p.x += Mathf.Sin(t * 0.8f + s.sway) * dt * 0.25f;
            if (p.y < -5.3f) { p.y = 5.4f; p.x = Random.Range(-worldW / 2, worldW / 2); }
            s.tr.position = p;
        }
        foreach (var g in glows)
        {
            float a = g.baseA * (0.75f + 0.25f * Mathf.Sin(t * 1.3f + g.phase) + 0.1f * Mathf.Sin(t * 5.1f + g.phase * 2));
            g.sr.color = new Color(1, 1, 1, a);
        }

        if (KeyDown("f1") && !TypingInField()) showStats = !showStats;

        if (joined)
        {
            if (openZone == null) UpdateMe(dt); else { me.moving = false; PlaceCharacter(me, me.pos, dt); }
            UpdateZones();
        }
        else PlaceCharacter(me, me.pos, dt);

        if (running)
        {
            lock (lk)
            {
                sharedMe.name = uiName;
                sharedMe.look = LookStr(myLook);
                sharedMe.x = me.pos.x;
                sharedMe.z = me.pos.y;
                sharedMe.face = me.face;
                sharedMe.moving = me.moving;
                sharedMe.zone = openZone ?? "";
                sharedMe.wish = myWish;
                peersView = new Dictionary<int, PeerInfo>();
                foreach (var kv in sharedPeers) peersView[kv.Key] = kv.Value.Clone();
                if (sharedStatus != null) { status = sharedStatus; sharedStatus = null; }
            }
            UpdateRemotes(dt);
        }
        UpdateWishes();
    }

    void UpdateMe(float dt)
    {
        Vector2 mv = Vector2.zero;
        var axis = TypingInField() ? Vector2.zero : MoveAxis();
        if (axis.sqrMagnitude > 0) { moveTarget = null; mv = new Vector2(axis.x, axis.y * 0.6f).normalized; }
        else if (ClickDown(out var mp) && !uiBlockRect.Contains(new Vector2(mp.x, Screen.height - mp.y)))
        {
            var w = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 10));
            moveTarget = new Vector2(Mathf.Clamp(w.x, -XLimit, XLimit), Mathf.Clamp(w.y, StreetBottom, StreetTop));
        }
        if (mv == Vector2.zero && moveTarget.HasValue)
        {
            var to = moveTarget.Value - me.pos;
            if (to.magnitude < 0.05f) moveTarget = null; else mv = to.normalized;
        }
        me.moving = mv != Vector2.zero;
        var p = me.pos;
        if (me.moving)
        {
            p += mv * Speed * dt;
            if (Mathf.Abs(mv.x) > 0.1f) me.face = mv.x > 0 ? 1 : -1;
        }
        // 입장 연출: 화면 아래에서 걸어 들어오는 동안은 길 아래쪽도 허용
        bool entering = p.y < StreetBottom - 0.01f && moveTarget.HasValue;
        p.x = Mathf.Clamp(p.x, -XLimit, XLimit);
        p.y = entering ? Mathf.Min(p.y, StreetTop) : Mathf.Clamp(p.y, StreetBottom, StreetTop);
        PlaceCharacter(me, p, dt);
    }

    void UpdateZones()
    {
        nearZone = null;
        if (openZone == null)
        {
            if (me.pos.y > StreetTop - 0.65f)
                foreach (var z in Zones)
                    if (Mathf.Abs(me.pos.x - z.x) < z.halfW) { nearZone = z; break; }
            if (nearZone != null && KeyDown("e") && !TypingInField()) OpenZone(nearZone.id);
        }
        else if (KeyDown("esc")) CloseZone();
    }

    void OpenZone(string id)
    {
        openZone = id;
        moveTarget = null;
        if (id == "tree") uiWish = myWish;
    }

    void CloseZone()
    {
        openZone = null;
        GUIUtility.keyboardControl = 0;
    }

    void UpdateRemotes(float dt)
    {
        long now = NowMs;
        var seen = new HashSet<int>();
        foreach (var kv in peersView)
        {
            var p = kv.Value;
            if (p.data == null || now - p.seqAt > 10000) continue;
            seen.Add(kv.Key);
            remotes.TryGetValue(kv.Key, out var ch);
            if (ch != null && ch.lookStr != p.data.look)
            {
                Destroy(ch.root);
                remotes.Remove(kv.Key);
                ch = null;
            }
            if (ch == null)
            {
                ch = MakeCharacter(ParseLook(p.data.look));
                ch.pos = new Vector2(p.data.x, p.data.z);
                remotes[kv.Key] = ch;
            }
            ch.name = p.data.name;
            ch.zone = p.data.zone;
            ch.face = p.data.face >= 0 ? 1 : -1;
            ch.moving = p.data.moving;
            float k = 1 - Mathf.Exp(-dt * 10f);
            PlaceCharacter(ch, Vector2.Lerp(ch.pos, new Vector2(p.data.x, p.data.z), k), dt);

            if (!string.IsNullOrEmpty(p.data.wish)) wishes[p.data.id] = (p.data.name, p.data.wish);
            else wishes.Remove(p.data.id);
        }
        var drop = new List<int>();
        foreach (var kv in remotes) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var key in drop) { Destroy(remotes[key].root); remotes.Remove(key); }
    }

    static int StableHash(string s)
    {
        unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; }
    }

    // 소원마다 트리에 반짝이는 불빛 하나
    void UpdateWishes()
    {
        if (!string.IsNullOrEmpty(myWish)) wishes[myId] = (uiName, myWish); else wishes.Remove(myId);
        float treeX = ImgX(642f / 1280f), treeTop = (0.5f - 300f / 714f) * WorldH, treeBottom = (0.5f - 510f / 714f) * WorldH;
        foreach (var kv in wishes)
        {
            if (ornaments.ContainsKey(kv.Key)) continue;
            int h = StableHash(kv.Key);
            float v = 0.15f + (h % 100) / 100f * 0.8f;
            float y = Mathf.Lerp(treeTop, treeBottom, v);
            float half = Mathf.Lerp(0.1f, 0.62f, v);
            float x = treeX + ((h / 100 % 200) / 100f - 1f) * half;
            var o = new GameObject("Wish-" + kv.Key).AddComponent<SpriteRenderer>();
            o.sprite = SprGlow();
            o.sortingOrder = -28000;
            o.transform.position = new Vector3(x, y, 0);
            o.color = Color.HSVToRGB((h % 97) / 97f, 0.35f, 1f);
            ornaments[kv.Key] = o;
        }
        var gone = new List<string>();
        foreach (var kv in ornaments) if (!wishes.ContainsKey(kv.Key)) gone.Add(kv.Key);
        foreach (var key in gone) { Destroy(ornaments[key].gameObject); ornaments.Remove(key); }
        float pulse = 0.28f + Mathf.Sin(Time.time * 3f) * 0.05f;
        foreach (var o in ornaments.Values) o.transform.localScale = Vector3.one * pulse;
    }

    IEnumerator TakePhoto()
    {
        hideUI = true;
        yield return null;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "XmasPlaza");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "photo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        ScreenCapture.CaptureScreenshot(path);
        yield return null;
        yield return null;
        hideUI = false;
        Toast("찰칵! 저장했어요: " + path);
    }

    void Toast(string msg) { toast = msg; toastUntil = Time.time + 4f; }

    // ========================================================
    // 입장
    void Join()
    {
        var p = uiPath.Trim();
        if (p.Length == 0) { status = "경로를 입력하세요."; return; }
        if (!Directory.Exists(p)) { status = "폴더를 찾을 수 없어요: " + p; return; }
        try
        {
            var test = Path.Combine(p, "_write_test_" + myId + ".tmp");
            File.WriteAllText(test, "ok");
            File.Delete(test);
        }
        catch (Exception e) { status = "쓰기 권한이 없어요: " + e.Message; return; }

        if (uiName.Trim().Length == 0) uiName = "익명";
        PlayerPrefs.SetString("xmas_path", p);
        PlayerPrefs.SetString("xmas_name", uiName);
        PlayerPrefs.SetString("xmas_look", LookStr(myLook));
        PlayerPrefs.Save();

        // 화면 아래(광장 입구)에서 걸어 들어오기
        me.pos = new Vector2(Random.Range(-2f, 2f), -5.8f);
        moveTarget = new Vector2(me.pos.x, -4.2f);
        GUIUtility.keyboardControl = 0;

        dir = p;
        lock (lk) sharedMe = new NetState { id = myId, name = uiName, look = LookStr(myLook), x = me.pos.x, z = me.pos.y };
        joined = true;
        running = true;
        status = "빈 자리 찾는 중...";
        thread = new Thread(SyncMain) { IsBackground = true, Name = "XmasSync" };
        thread.Start();
    }

    void OnApplicationQuit() { StopSync(); }
    void OnDestroy() { StopSync(); }

    void StopSync()
    {
        if (!running) return;
        running = false;
        if (thread != null && thread.IsAlive) thread.Join(2000);
    }

    // ========================================================
    // 동기화 스레드
    string SlotPath(int i) { return Path.Combine(dir, $"player{i:00}.json"); }

    int ReadSlot(int i, out NetState st) // 0=정상, 1=없음, 2=오류
    {
        st = null;
        var path = SlotPath(i);
        try
        {
            if (!File.Exists(path)) return 1;
            string txt;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs)) txt = sr.ReadToEnd();
            if (string.IsNullOrWhiteSpace(txt)) { st = new NetState { empty = true }; return 0; }
            st = JsonUtility.FromJson<NetState>(txt);
            return st == null ? 2 : 0;
        }
        catch (FileNotFoundException) { return 1; }
        catch { return 2; }
    }

    bool WriteSlot(int i, NetState s)
    {
        var path = SlotPath(i);
        var tmp = Path.Combine(dir, $"player{i:00}_{myId}.tmp");
        var json = JsonUtility.ToJson(s);
        try
        {
            File.WriteAllText(tmp, json);
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch
            {
                try { File.WriteAllText(path, json); }
                finally { try { File.Delete(tmp); } catch { } }
            }
            return true;
        }
        catch { return false; }
    }

    NetState CopyMe()
    {
        lock (lk) return JsonUtility.FromJson<NetState>(JsonUtility.ToJson(sharedMe));
    }

    int ClaimSlot()
    {
        var a = new int[MaxSlots + 1]; var aS = new NetState[MaxSlots + 1];
        var b = new int[MaxSlots + 1]; var bS = new NetState[MaxSlots + 1];
        for (int i = 1; i <= MaxSlots; i++) a[i] = ReadSlot(i, out aS[i]);
        Thread.Sleep(1200);
        for (int i = 1; i <= MaxSlots; i++) b[i] = ReadSlot(i, out bS[i]);

        var free = new List<int>();
        for (int i = 1; i <= MaxSlots; i++)
        {
            if (b[i] == 2) continue;
            if (b[i] == 1 || bS[i].empty || bS[i].left) { free.Add(i); continue; }
            if (a[i] == 0 && aS[i].seq == bS[i].seq) free.Add(i);
        }
        var rnd = new System.Random();
        foreach (var i in free)
        {
            if (!running) return -1;
            var st = CopyMe();
            st.seq = 0; st.t = UnixMs;
            WriteSlot(i, st);
            Thread.Sleep(800 + rnd.Next(700));
            if (ReadSlot(i, out var c) == 0 && c.id == myId) return i;
        }
        return -1;
    }

    static float Ema(float old, float v) { return old == 0 ? v : old + (v - old) * 0.2f; }

    void SyncMain()
    {
        int slot;
        try { slot = ClaimSlot(); }
        catch (Exception e) { lock (lk) sharedStatus = "자리 배정 오류: " + e.Message; return; }
        if (slot < 0) { lock (lk) sharedStatus = "빈 자리가 없어요 (최대 " + MaxSlots + "명)."; return; }
        lock (lk) { sharedSlot = slot; sharedStatus = "입장 완료!"; }

        long seq = 0, nextW = 0, nextR = 0, lastSelf = 0;
        var missing = new Dictionary<int, long>();
        var local = new Dictionary<int, PeerInfo>();
        var sw = new Stopwatch();

        while (running)
        {
            long now = NowMs;
            if (now >= nextW)
            {
                nextW = now + WriteMs;
                var st = CopyMe();
                st.seq = ++seq; st.t = UnixMs;
                sw.Restart();
                bool ok = WriteSlot(slot, st);
                float ms = (float)sw.Elapsed.TotalMilliseconds;
                lock (lk) { writeMsAvg = Ema(writeMsAvg, ms); if (!ok) writeErr++; }
            }
            if (now >= nextR)
            {
                nextR = now + ReadMs;
                sw.Restart();
                int errs = 0;
                for (int i = 1; i <= MaxSlots; i++)
                {
                    if (i == slot) continue;
                    if (!local.ContainsKey(i) && missing.TryGetValue(i, out var mt) && now - mt < 2000) continue;
                    int r = ReadSlot(i, out var d);
                    if (r == 1)
                    {
                        if (!local.ContainsKey(i)) missing[i] = now;
                        continue;
                    }
                    if (r == 2) { errs++; if (local.TryGetValue(i, out var pe)) pe.err++; continue; }
                    missing.Remove(i);
                    if (d.empty || d.left || d.id == myId) { local.Remove(i); continue; }
                    if (!local.TryGetValue(i, out var p))
                    {
                        p = new PeerInfo { seqAt = now };
                        local[i] = p;
                    }
                    if (d.seq != p.seq)
                    {
                        if (p.seq != -1) p.gap = Ema(p.gap, now - p.seqAt);
                        p.lat = Ema(p.lat, (float)(UnixMs - d.t));
                        p.seq = d.seq;
                        p.seqAt = now;
                        p.data = d;
                    }
                }
                if (now - lastSelf > 2000)
                {
                    lastSelf = now;
                    if (ReadSlot(slot, out var mine) == 0 && !string.IsNullOrEmpty(mine.id) && mine.id != myId)
                        lock (lk) conflict = true;
                }
                float ms = (float)sw.Elapsed.TotalMilliseconds;
                lock (lk)
                {
                    readMsAvg = Ema(readMsAvg, ms);
                    readErr += errs;
                    sharedPeers = new Dictionary<int, PeerInfo>();
                    foreach (var kv in local) sharedPeers[kv.Key] = kv.Value.Clone();
                }
            }
            Thread.Sleep(5);
        }
        WriteSlot(slot, new NetState { id = myId, left = true });
    }

    // ========================================================
    // 화면 UI (그림책 톤: 크림색 종이 + 갈색 글씨)
    static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
    static readonly Color Ink = new Color(0.29f, 0.22f, 0.17f);
    static readonly Color Paper = new Color(0.99f, 0.96f, 0.9f, 0.96f);
    static readonly Color Berry = new Color(0.72f, 0.2f, 0.2f, 0.95f);

    void InitStyles()
    {
        if (sLabel != null) return;
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
        sLabel.normal.textColor = Ink;
        sSmall = new GUIStyle(sLabel) { fontSize = 13 };
        sSmall.normal.textColor = new Color(0.45f, 0.38f, 0.32f);
        sTitle = new GUIStyle(sLabel) { fontSize = 26, fontStyle = FontStyle.Bold };
        sTitle.normal.textColor = new Color(0.6f, 0.15f, 0.15f);
        sPanel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(26, 26, 22, 22) };
        sPanel.normal.background = Solid(Paper);
        sDark = new GUIStyle(sLabel) { fontSize = 13, padding = new RectOffset(12, 12, 8, 8) };
        sDark.normal.background = Solid(new Color(0.15f, 0.18f, 0.25f, 0.8f));
        sDark.normal.textColor = Color.white;
        sTag = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = false };
        sTag.normal.background = Solid(new Color(0.99f, 0.97f, 0.92f, 0.88f));
        sTagMe = new GUIStyle(sTag);
        sTagMe.normal.background = Solid(Berry);
        sTagMe.normal.textColor = Color.white;
        sSign = new GUIStyle(sTag) { fontSize = 12 };
        sSign.normal.background = Solid(new Color(0.35f, 0.24f, 0.17f, 0.85f));
        sSign.normal.textColor = new Color(1f, 0.93f, 0.78f);
        sPrompt = new GUIStyle(sTag) { fontSize = 18, padding = new RectOffset(20, 20, 10, 10) };
        sPrompt.normal.background = Solid(Berry);
        sPrompt.normal.textColor = Color.white;
        sPrompt.hover.background = Solid(new Color(0.8f, 0.25f, 0.25f, 1f));
        sPrompt.hover.textColor = Color.white;
        sWishCard = new GUIStyle(sLabel) { padding = new RectOffset(12, 12, 8, 8), margin = new RectOffset(0, 0, 4, 4) };
        sWishCard.normal.background = Solid(new Color(0.95f, 0.88f, 0.76f, 0.7f));
        sBtn = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        GUI.skin.textField.fontSize = 16;
    }

    void Tag(Vector2 world, string text, GUIStyle style)
    {
        var sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0));
        var size = style.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(sp.x - size.x / 2 - 8, Screen.height - sp.y - size.y / 2, size.x + 16, size.y + 6), text, style);
    }

    void OnGUI()
    {
        if (hideUI) return;
        InitStyles();
        uiBlockRect = Rect.zero;
        panelRect = Rect.zero;

        // 문 위 간판
        if (openZone == null)
            foreach (var z in Zones)
                Tag(new Vector2(z.x, z.id == "tree" ? -2.15f : -1.9f), z.label, sSign);

        // 이름표
        Tag(me.pos + Vector2.up * 1.4f * CharScale(me.pos), joined ? uiName + " (나)" : (uiName.Length > 0 ? uiName : "나"), sTagMe);
        foreach (var ch in remotes.Values)
        {
            string label = ch.name ?? "?";
            if (!string.IsNullOrEmpty(ch.zone)) label += " · " + ZoneShort(ch.zone);
            Tag(ch.pos + Vector2.up * 1.4f * CharScale(ch.pos), label, sTag);
            if (joined && openZone == null && Vector2.Distance(ch.pos, me.pos) < 0.7f)
                Tag((ch.pos + me.pos) / 2 + Vector2.up * 1.6f, "안녕!", sTagMe);
        }

        if (!joined) { DrawJoinPanel(); return; }

        var top = new Rect(16, 16, 320, 34);
        GUI.Label(top, $"{status}   ·   {1 + remotes.Count}명 접속 중", sDark);
        uiBlockRect = top;

        if (openZone == null && nearZone != null)
        {
            string txt = $"[E]  {nearZone.label}";
            var size = sPrompt.CalcSize(new GUIContent(txt));
            var r = new Rect(Screen.width / 2f - size.x / 2, Screen.height - 80, size.x, size.y);
            if (GUI.Button(r, txt, sPrompt)) OpenZone(nearZone.id);
            uiBlockRect = r;
        }

        if (openZone != null) DrawZonePanel();

        if (toast != null && Time.time < toastUntil)
        {
            var size = sTag.CalcSize(new GUIContent(toast));
            GUI.Label(new Rect(Screen.width / 2f - size.x / 2 - 14, 64, size.x + 28, size.y + 14), toast, sTag);
        }

        if (showStats) DrawStats();
        GUI.Label(new Rect(Screen.width - 210, Screen.height - 30, 200, 24), "F1: 동기화 상태", sDark);
    }

    static string ZoneShort(string id)
    {
        switch (id)
        {
            case "spot": return "틀린 그림 찾기";
            case "mines": return "지뢰찾기";
            case "tree": return "소원 트리";
            case "ark": return "선물 깨기";
            case "photo": return "포토존";
        }
        return id;
    }

    bool ColorRow(string label, Color[] colors, ref int sel)
    {
        bool changed = false;
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sLabel, GUILayout.Width(64));
        for (int i = 0; i < colors.Length; i++)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = colors[i];
            if (GUILayout.Button(i == sel ? "V" : "", GUILayout.Width(30), GUILayout.Height(26)) && sel != i) { sel = i; changed = true; }
            GUI.backgroundColor = old;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return changed;
    }

    void DrawJoinPanel()
    {
        float w = 460, h = 430;
        panelRect = new Rect(30, Screen.height / 2f - h / 2, w, h);
        GUILayout.BeginArea(panelRect, sPanel);
        GUILayout.Label("크리스마스 광장", sTitle);
        GUILayout.Label("캐릭터를 꾸미고 광장으로 들어가요.", sSmall);
        GUILayout.Space(8);
        bool changed = false;
        changed |= ColorRow("코트", CoatColors, ref myLook.coat);
        changed |= ColorRow("목도리", ScarfColors, ref myLook.scarf);
        changed |= ColorRow("피부", SkinColors, ref myLook.skin);
        GUILayout.BeginHorizontal();
        GUILayout.Label("모자", sLabel, GUILayout.Width(64));
        int newHat = GUILayout.SelectionGrid(myLook.hat, HatNames, 3, sBtn, GUILayout.Height(54));
        GUILayout.EndHorizontal();
        if (newHat != myLook.hat) { myLook.hat = newHat; changed = true; }
        if (changed)
        {
            var pos = me.pos; var face = me.face;
            Destroy(me.root);
            me = MakeCharacter(myLook);
            me.face = face;
            PlaceCharacter(me, pos, 0);
        }
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름", sLabel, GUILayout.Width(64));
        uiName = GUILayout.TextField(uiName, 12, GUILayout.Height(28));
        GUILayout.EndHorizontal();
        GUILayout.Label("공유 폴더  <color=#8a7b6c>(예: \\\\서버\\공유\\xmas 또는 Z:\\xmas)</color>", sSmall);
        uiPath = GUILayout.TextField(uiPath, GUILayout.Height(28));
        GUILayout.Space(10);
        if (GUILayout.Button("광장으로 들어가기", sBtn, GUILayout.Height(40))) Join();
        GUILayout.Label(status, sSmall);
        GUILayout.EndArea();
        uiBlockRect = panelRect;
    }

    void DrawZonePanel()
    {
        Zone z = null;
        foreach (var zz in Zones) if (zz.id == openZone) z = zz;
        if (z == null) { openZone = null; return; }

        float w = Mathf.Min(560, Screen.width - 40);
        float h = openZone == "tree" ? Mathf.Min(520, Screen.height - 60) : 300;
        panelRect = new Rect(Screen.width / 2f - w / 2, Screen.height / 2f - h / 2 - 40, w, h);
        GUILayout.BeginArea(panelRect, sPanel);
        GUILayout.BeginHorizontal();
        GUILayout.Label(z.label, sTitle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기 (ESC)", sBtn, GUILayout.Width(110), GUILayout.Height(32))) { CloseZone(); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
        GUILayout.EndHorizontal();
        GUILayout.Label(z.desc, sSmall);
        GUILayout.Space(10);

        switch (openZone)
        {
            case "tree": DrawWishTree(); break;
            case "photo":
                GUILayout.Label("친구들을 불러 모아 보세요. 버튼을 누르면 화면 그대로 사진을 찍어요.", sLabel);
                GUILayout.Space(8);
                if (GUILayout.Button("찰칵! 사진 찍기", sBtn, GUILayout.Height(44))) StartCoroutine(TakePhoto());
                break;
            default:
                GUILayout.Label("<b>여기 모인 사람</b>", sLabel);
                var here = new List<string> { uiName + " (나)" };
                foreach (var ch in remotes.Values) if (ch.zone == openZone) here.Add(ch.name);
                GUILayout.Label(string.Join(", ", here), sLabel);
                GUILayout.Space(14);
                GUILayout.Label("<color=#b5532f>미니게임은 다음 업데이트에서 열려요. 조금만 기다려 주세요!</color>", sLabel);
                break;
        }
        GUILayout.EndArea();
        uiBlockRect = panelRect;
    }

    void DrawWishTree()
    {
        GUILayout.Label("<b>내 소원</b>", sLabel);
        uiWish = GUILayout.TextField(uiWish, 40, GUILayout.Height(32));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(string.IsNullOrEmpty(myWish) ? "소원 걸기" : "소원 고치기", sBtn, GUILayout.Height(34)))
        {
            myWish = uiWish.Trim();
            Toast(string.IsNullOrEmpty(myWish) ? "소원을 내렸어요." : "트리에 소원을 걸었어요!");
        }
        if (!string.IsNullOrEmpty(myWish) && GUILayout.Button("내리기", sBtn, GUILayout.Width(90), GUILayout.Height(34))) { myWish = ""; uiWish = ""; }
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        GUILayout.Label($"<b>트리에 걸린 소원</b>  <color=#8a7b6c>{wishes.Count}개</color>", sLabel);
        wishScroll = GUILayout.BeginScrollView(wishScroll);
        if (wishes.Count == 0) GUILayout.Label("아직 걸린 소원이 없어요. 첫 번째로 걸어 보세요!", sSmall);
        foreach (var kv in wishes) GUILayout.Label($"<b>{kv.Value.name}</b>  {kv.Value.text}", sWishCard);
        GUILayout.EndScrollView();
    }

    void DrawStats()
    {
        float wms, rms; int we, re, slot; bool conf;
        lock (lk) { wms = writeMsAvg; rms = readMsAvg; we = writeErr; re = readErr; slot = sharedSlot; conf = conflict; }
        var r = new Rect(Screen.width - 396, 16, 380, 110 + peersView.Count * 22);
        GUILayout.BeginArea(r, sDark);
        GUILayout.Label($"FPS {fps:0} · {SystemInfo.graphicsDeviceName}", sDark);
        GUILayout.Label($"내 자리 player{slot:00} · 쓰기 {wms:0}ms · 읽기 {rms:0}ms · 오류 {we}/{re}" + (conf ? "  자리 충돌!" : ""), sDark);
        long now = NowMs;
        foreach (var kv in peersView)
        {
            var p = kv.Value;
            if (p.data == null) continue;
            bool idle = now - p.seqAt > 5000;
            GUILayout.Label($"{p.data.name}{(idle ? " (응답 없음)" : "")}  지연 {p.lat:0}ms · 갱신 {p.gap:0}ms · 오류 {p.err}", sDark);
        }
        GUILayout.EndArea();
    }
}
