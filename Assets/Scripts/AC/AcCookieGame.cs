// AcCookieGame.cs - 쿠키 공방의 '쿠키 달리기' (쿠키런 같은 3D 횡스크롤 달리기)
// 내 캐릭터가 크리스마스 저녁 눈길(쿠키 반죽 길)을 달려요. 점프·2단 점프·슬라이드, 젤리, 아이템(거대화·자석·질주·하트), 피버 타임.
// 점수: orn/x~score~<이름>~<시각> = {name, best, look, t} (판마다 따로, 예전 기록 x~score~<이름>도 함께 읽음) → 명예의 전당(1~3등 시상대 + 4~10등 목록)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class AcRoom
{
    enum CgState { Menu, Count, Play, Dead, Result, Board }
    CgState cg = CgState.Menu;
    float cgStateT;

    // ===== 무대 =====
    static readonly Vector3 CgO = new Vector3(0, -2000, 0);   // 게임 세상은 집 아래 멀리
    Transform cgRoot, cgCamTr, cgSkyTr, cgHillsTr, cgHill1, cgHill2;
    Camera cgCam;
    AcCharacter cgHero; Transform cgPet;
    Mesh cgTileMesh, cgPoleMesh, cgRingMesh, cgBoxMesh;
    readonly List<ParticleSystem> cgFx = new List<ParticleSystem>();   // 0 금색, 1 흰색, 2 분홍, 3 하늘
    ParticleSystem cgSnow, cgTrail;
    Material cgMat;
    MaterialPropertyBlock cgMpb;

    // ===== 진행 =====
    float cgX, cgY, cgVy, cgSpeed, cgTime, cgHp, cgFever, cgFeverT, cgInvT, cgHurtT, cgGiantT, cgMagnetT, cgRushT, cgShake, cgCamY, cgGiantK = 1;
    int cgScore, cgJelly, cgJumps, cgLastScore, cgLastBest, cgDistM;
    bool cgGrounded, cgSlide, cgNewBest, cgFalling;
    float cgCursor, cgGroundEnd, cgNextItem, cgDecoNear, cgDecoMid, cgShownScore;
    int cgChunkN;
    readonly System.Random cgRnd = new System.Random();

    class CgThing
    {
        public int kind; public GameObject go; public string pool;
        public Vector3 p; public Vector2 half; public float baseY, spin, vx, vy, t; public bool dead, flying; public int value;
    }
    readonly List<CgThing> cgThings = new List<CgThing>();
    readonly List<(float x0, float x1, GameObject go)> cgTiles = new List<(float, float, GameObject)>();
    readonly List<(Transform t, float span)> cgScenery = new List<(Transform, float)>();
    readonly Dictionary<string, Stack<GameObject>> cgPool = new Dictionary<string, Stack<GameObject>>();
    readonly Dictionary<string, (Vector3 size, float minY, float cx)> cgFit = new Dictionary<string, (Vector3, float, float)>();
    readonly List<(Vector3 w, string text, Color col, float t)> cgPops = new List<(Vector3, string, Color, float)>();

    // 쿠키 모습: 몸은 진저브레드, 모자·모자색·목도리·단추(스웨터 색)는 내 캐릭터 그대로
    static AcLook CookieLook(AcLook l)
    {
        var c = AcLook.Decode(l.Encode());
        c.species = "cookie"; c.fur = new Color(0.8f, 0.52f, 0.28f); c.hair = ""; c.female = true;
        return c;
    }
    static AcLook CookieLook(string look) => CookieLook(AcLook.Decode(look));

    const float CgGrav = 30f, CgJump1 = 10.2f, CgJump2 = 9.4f, HeroH = 1.05f;

    // ========================================================
    // 열기 / 닫기
    void OpenCookieGame()
    {
        playingGame = true; walkTarget = null; followId = null; me.speed = 0;
        BuildCookieWorld();
        cgRoot.gameObject.SetActive(true);
        cam.enabled = false; cgCam.enabled = true;
        RenderSettings.fog = false;   // 게임 하늘은 안개 없이
        cgHero.Rebuild(CookieLook(me.look));   // 달리는 건 쿠키, 모자·목도리·단추 색은 내 캐릭터와 같게
        ResetRun();
        SetCg(CgState.Menu);
        net?.Get("scores");
    }

    void CloseCookieGame()
    {
        playingGame = false;
        if (cgRoot) cgRoot.gameObject.SetActive(false);
        if (cgCam) cgCam.enabled = false;
        cam.enabled = true;
        RenderSettings.fog = true;
        ClearPodium();
        UpdateCamera(0, true);
    }

    void SetCg(CgState s)
    {
        cg = s; cgStateT = 0;
        if (s == CgState.Board) ShowPodium(); else ClearPodium();
    }

    // ========================================================
    // 세상 만들기 (한 번만)
    void BuildCookieWorld()
    {
        if (cgRoot != null) return;
        cgRoot = new GameObject("CookieGame").transform; cgRoot.position = CgO;
        cgMpb = new MaterialPropertyBlock();
        cgMat = new Material(AcmLoader.Mat) { name = "CgMat" };
        cgMat.SetFloat("_HasTint", 0);
        // 카메라
        var cgo = new GameObject("CgCam"); cgo.transform.SetParent(cgRoot, false);
        cgCam = cgo.AddComponent<Camera>(); cgCamTr = cgo.transform;
        cgCam.fieldOfView = 40; cgCam.nearClipPlane = 0.3f; cgCam.farClipPlane = 260f;
        cgCam.clearFlags = CameraClearFlags.SolidColor; cgCam.backgroundColor = new Color(0.12f, 0.1f, 0.2f);
        cgCam.cullingMask = ~(1 << IconLayer);
        var ad = cgo.AddComponent<UniversalAdditionalCameraData>(); ad.renderPostProcessing = false; ad.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cgCam.enabled = false;
        // 하늘 (카메라를 따라다님)
        cgSkyTr = new GameObject("Sky").transform; cgSkyTr.SetParent(cgCamTr, false);
        var sky = MeshObj("SkyQuad", SkyMesh(), cgSkyTr, 1f);
        sky.transform.localPosition = new Vector3(0, 0, 240); sky.transform.localScale = new Vector3(520, 300, 1);
        var moon = AcCatalog.Spawn("orn_moon", cgSkyTr, new Dictionary<string, Color> { { "orn", new Color(1f, 0.96f, 0.8f) } });
        moon.transform.localPosition = new Vector3(55, 48, 230); moon.transform.localScale = Vector3.one * 60; moon.transform.localRotation = Quaternion.Euler(0, 180, -20);
        SetEmit(moon, 1f);
        var stars = MeshObj("Stars", StarsMesh(), cgSkyTr, 1f); stars.transform.localPosition = new Vector3(0, 0, 235);
        // 먼 산 (두 겹, 카메라를 따라 천천히)
        cgHillsTr = new GameObject("Hills").transform; cgHillsTr.SetParent(cgRoot, false);
        var h1 = MeshObj("HillsFar", HillsMesh(HillP1, 34, 3, 11, new Color(0.62f, 0.62f, 0.85f), new Color(0.86f, 0.84f, 0.96f)), cgHillsTr, 0.15f); h1.transform.localPosition = new Vector3(0, -1, 120); cgHill1 = h1.transform;
        var h2 = MeshObj("HillsNear", HillsMesh(HillP2, 20, 3, 23, new Color(0.72f, 0.78f, 0.95f), new Color(0.97f, 0.97f, 1f)), cgHillsTr, 0.05f); h2.transform.localPosition = new Vector3(0, -0.5f, 70); cgHill2 = h2.transform;
        // 눈밭 (길 뒤쪽 바닥) + 구덩이 밑바닥
        var snowField = MeshObj("SnowField", BoxMesh(new Vector3(400, 0.2f, 70), new Color(0.95f, 0.96f, 1f), new Color(0.85f, 0.88f, 0.98f)), cgHillsTr, 0f);
        snowField.transform.localPosition = new Vector3(0, -0.15f, 36.4f);
        var front = MeshObj("SnowFront", BoxMesh(new Vector3(400, 0.2f, 40), new Color(0.93f, 0.95f, 1f), new Color(0.8f, 0.84f, 0.96f)), cgHillsTr, 0f);
        front.transform.localPosition = new Vector3(0, -0.6f, -21.3f);
        var pitFloor = MeshObj("PitFloor", BoxMesh(new Vector3(400, 0.2f, 3), new Color(0.25f, 0.18f, 0.32f), new Color(0.2f, 0.15f, 0.28f)), cgHillsTr, 0f);
        pitFloor.transform.localPosition = new Vector3(0, -5f, 0);
        // 공용 메시
        cgTileMesh = TileMesh(); cgPoleMesh = PoleMesh(16f, 0.11f); cgRingMesh = CgRingMesh(0.62f, 0.06f); cgBoxMesh = BoxMesh(Vector3.one, Color.white, new Color(0.85f, 0.85f, 0.9f));
        // 주인공 + 쿠키 짝꿍
        cgHero = AcCharacter.Create(CookieLook(me.look), cgRoot);
        cgPet = AcCatalog.Spawn("reindeer_plush", cgRoot, null).transform; cgPet.localScale = Vector3.one * 0.6f;   // 하늘을 나는 꼬마 순록 짝꿍
        // 반짝이 효과 4색 + 눈 + 꼬리 반짝이
        foreach (var col in new[] { new Color(1f, 0.85f, 0.3f), Color.white, new Color(1f, 0.55f, 0.75f), new Color(0.5f, 0.85f, 1f) }) cgFx.Add(MakeBurst(col));
        cgSnow = MakeSnow();
        cgTrail = MakeBurst(new Color(1f, 0.95f, 0.6f));
        cgRoot.gameObject.SetActive(false);
    }

    GameObject MeshObj(string name, Mesh m, Transform parent, float emit)
    {
        var go = new GameObject(name); if (parent) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = cgMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        if (emit > 0) { var b = new MaterialPropertyBlock(); b.SetFloat("_Emit", emit); r.SetPropertyBlock(b); }
        return go;
    }

    void SetEmit(GameObject g, float e)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.GetPropertyBlock(cgMpb); cgMpb.SetFloat("_Emit", e); r.SetPropertyBlock(cgMpb); }
    }

    // ===== 메시 만들기 (정점 색: 감마 RGB, A=0 / UV.x = 밝기) =====
    class MB
    {
        public List<Vector3> v = new List<Vector3>(); public List<Vector3> n = new List<Vector3>(); public List<Color32> c = new List<Color32>(); public List<int> t = new List<int>(); public List<Vector2> uv = new List<Vector2>();
        public int V(Vector3 p, Vector3 nn, Color col, float ao = 1) { v.Add(p); n.Add(nn); c.Add(new Color32((byte)(Mathf.Clamp01(col.r) * 255), (byte)(Mathf.Clamp01(col.g) * 255), (byte)(Mathf.Clamp01(col.b) * 255), 0)); uv.Add(new Vector2(ao, 0)); return v.Count - 1; }
        public void Q(int a, int b, int cc, int d) { t.AddRange(new[] { a, b, cc, a, cc, d }); }
        public Mesh M(string name) { var m = new Mesh { name = name }; if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32; m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); return m; }
    }

    static Mesh BoxMesh(Vector3 size, Color top, Color side)
    {
        var b = new MB(); var h = size * 0.5f;
        void F(Vector3 nrm, Vector3 u, Vector3 w, Color col)
        {
            var c0 = Vector3.Scale(nrm, h);
            var hu = Vector3.Scale(u, h); var hw = Vector3.Scale(w, h);
            int a = b.V(c0 - hu - hw, nrm, col), bb = b.V(c0 + hu - hw, nrm, col), cc = b.V(c0 + hu + hw, nrm, col), d = b.V(c0 - hu + hw, nrm, col);
            if (Vector3.Dot(Vector3.Cross(hu, hw), nrm) > 0) b.Q(a, bb, cc, d); else b.Q(a, d, cc, bb);
        }
        F(Vector3.up, Vector3.right, Vector3.forward, top);
        F(Vector3.down, Vector3.right, Vector3.forward, side * 0.8f);
        F(Vector3.back, Vector3.right, Vector3.up, side);
        F(Vector3.forward, Vector3.right, Vector3.up, side);
        F(Vector3.right, Vector3.forward, Vector3.up, side * 0.92f);
        F(Vector3.left, Vector3.forward, Vector3.up, side * 0.92f);
        return b.M("box");
    }

    // 쿠키 길 한 칸 (2m): 위는 하얀 아이싱(흘러내림), 옆은 구운 쿠키색 + 빨간 리본 띠
    static Mesh TileMesh()
    {
        var b = new MB();
        const float W = 2f, D = 2.4f, H = 3.2f; const int nx = 24, ny = 16;
        Color icing = new Color(1f, 0.98f, 0.97f), dough = new Color(0.78f, 0.5f, 0.27f), dark = new Color(0.55f, 0.32f, 0.17f), ribbon = new Color(0.86f, 0.18f, 0.22f), ribbonW = new Color(0.98f, 0.95f, 0.92f);
        Color Front(float x, float y)
        {
            float drip = 0.16f + 0.1f * Mathf.Max(0, Mathf.Sin(x * 7.3f)) + 0.07f * Mathf.Max(0, Mathf.Sin(x * 13.1f + 1.3f));
            if (-y < drip) return icing;
            if (-y > 0.62f && -y < 0.76f) return (Mathf.Repeat(x * 2.5f + y * 2.5f, 1f) < 0.5f) ? ribbon : ribbonW;
            float speck = Mathf.Sin(x * 31f) * Mathf.Sin(y * 27f) > 0.86f ? 0.85f : 1f;
            return Color.Lerp(dough, dark, Mathf.Clamp01((-y - 0.8f) / 2.4f)) * speck;
        }
        // 앞면 (z = -D/2, 카메라 쪽)
        int[,] id = new int[nx + 1, ny + 1];
        for (int i = 0; i <= nx; i++) for (int j = 0; j <= ny; j++)
            {
                float x = -W / 2 + W * i / nx, y = -H * Mathf.Pow((float)j / ny, 1.6f);
                id[i, j] = b.V(new Vector3(x, y, -D / 2), Vector3.back, Front(x, y), 1 - 0.4f * j / ny);
            }
        for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) b.Q(id[i, j], id[i + 1, j], id[i + 1, j + 1], id[i, j + 1]);
        // 윗면
        int a0 = b.V(new Vector3(-W / 2, 0, -D / 2), Vector3.up, icing), a1 = b.V(new Vector3(W / 2, 0, -D / 2), Vector3.up, icing),
            a2 = b.V(new Vector3(W / 2, 0, D / 2), Vector3.up, icing * 0.96f), a3 = b.V(new Vector3(-W / 2, 0, D / 2), Vector3.up, icing * 0.96f);
        b.Q(a0, a3, a2, a1);
        // 양옆 (구덩이 가장자리에서 보임)
        foreach (int sx in new[] { -1, 1 })
        {
            var nrm = new Vector3(sx, 0, 0);
            int[,] sid = new int[9, ny + 1];
            for (int i = 0; i <= 8; i++) for (int j = 0; j <= ny; j++)
                {
                    float z = -D / 2 + D * i / 8, y = -H * Mathf.Pow((float)j / ny, 1.6f);
                    sid[i, j] = b.V(new Vector3(sx * W / 2, y, z), nrm, Front(z * 1.3f + sx, y) * 0.9f, 1 - 0.4f * j / ny);
                }
            for (int i = 0; i < 8; i++) for (int j = 0; j < ny; j++)
                {
                    if (sx > 0) b.Q(sid[i, j], sid[i + 1, j], sid[i + 1, j + 1], sid[i, j + 1]);
                    else b.Q(sid[i, j], sid[i, j + 1], sid[i + 1, j + 1], sid[i + 1, j]);
                }
        }
        return b.M("cookieTile");
    }

    // 하늘에서 내려오는 사탕 줄무늬 기둥 (슬라이드 장애물)
    static Mesh PoleMesh(float h, float r)
    {
        var b = new MB(); const int seg = 12; int rows = Mathf.CeilToInt(h / 0.08f);
        for (int j = 0; j <= rows; j++)
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg, y = h * j / rows;
                var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                bool red = Mathf.Repeat(a / (Mathf.PI * 2) + y * 1.6f, 1f) < 0.5f;
                b.V(n * r + Vector3.up * y, n, red ? new Color(0.88f, 0.16f, 0.2f) : new Color(1f, 0.97f, 0.95f));
            }
        for (int j = 0; j < rows; j++) for (int i = 0; i < seg; i++)
            {
                int a = j * (seg + 1) + i, c = a + seg + 1;
                b.Q(a, c, c + 1, a + 1);
            }
        return b.M("pole");
    }

    static Mesh CgRingMesh(float R, float r)
    {
        var b = new MB(); const int s1 = 32, s2 = 8;
        for (int i = 0; i <= s1; i++) for (int j = 0; j <= s2; j++)
            {
                float u = i * Mathf.PI * 2 / s1, w = j * Mathf.PI * 2 / s2;
                var c = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0) * R;
                var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(w), Mathf.Sin(u) * Mathf.Cos(w), Mathf.Sin(w));
                b.V(c + n * r, n, new Color(1f, 0.86f, 0.35f));
            }
        for (int i = 0; i < s1; i++) for (int j = 0; j < s2; j++)
            {
                int a = i * (s2 + 1) + j, c = a + s2 + 1;
                b.Q(a, c, c + 1, a + 1);
            }
        return b.M("ring");
    }

    static Mesh SkyMesh()
    {
        // 크리스마스 저녁 하늘: 위는 짙은 남색, 가운데 보라, 지평선은 복숭아빛 (빛나는 셰이더라 미리 어둡게)
        var b = new MB();
        Color[] cs = { new Color(0.06f, 0.07f, 0.24f), new Color(0.2f, 0.16f, 0.4f), new Color(0.48f, 0.3f, 0.52f), new Color(0.74f, 0.48f, 0.5f), new Color(0.76f, 0.56f, 0.5f) };
        float[] ys = { 0.5f, 0.18f, -0.02f, -0.14f, -0.5f };
        int[] l = new int[5], r = new int[5];
        for (int k = 0; k < 5; k++) { l[k] = b.V(new Vector3(-0.5f, ys[k], 0), Vector3.back, cs[k]); r[k] = b.V(new Vector3(0.5f, ys[k], 0), Vector3.back, cs[k]); }
        for (int k = 0; k < 4; k++) b.Q(l[k], r[k], r[k + 1], l[k + 1]);
        return b.M("sky");
    }

    static Mesh StarsMesh()
    {
        var b = new MB(); var rnd = new System.Random(11);
        for (int i = 0; i < 160; i++)
        {
            float x = (float)rnd.NextDouble() * 500 - 250, y = 20 + (float)Math.Pow(rnd.NextDouble(), 0.7) * 130, s = 0.25f + (float)rnd.NextDouble() * 0.6f;
            var col = Color.Lerp(new Color(0.8f, 0.8f, 0.7f), new Color(0.75f, 0.8f, 0.95f), (float)rnd.NextDouble());
            int a = b.V(new Vector3(x - s, y, 0), Vector3.back, col), bb = b.V(new Vector3(x, y + s * 1.6f, 0), Vector3.back, col), c = b.V(new Vector3(x + s, y, 0), Vector3.back, col), d = b.V(new Vector3(x, y - s * 1.6f, 0), Vector3.back, col);
            b.Q(a, bb, c, d);
        }
        return b.M("stars");
    }

    const float HillP1 = 320f, HillP2 = 240f;
    // 주기(period)마다 똑같이 반복되는 산 (두 주기 길이로 만들어서 끊김 없이 이어 붙임)
    static Mesh HillsMesh(float period, float height, int k, int seed, Color baseC, Color topC)
    {
        var b = new MB(); var rnd = new System.Random(seed); int n = 260;
        float p1 = (float)rnd.NextDouble() * 6, p2 = (float)rnd.NextDouble() * 6, p3 = (float)rnd.NextDouble() * 6;
        for (int i = 0; i <= n; i++)
        {
            float x = -period + 2 * period * i / n, u = x / period * Mathf.PI * 2;
            float y = height * (0.45f + 0.3f * Mathf.Sin(u * k + p1) + 0.18f * Mathf.Sin(u * k * 2 + p2) + 0.07f * Mathf.Sin(u * k * 5 + p3));
            b.V(new Vector3(x, y, 0), Vector3.back, topC);
            b.V(new Vector3(x, y * 0.55f, 0), Vector3.back, Color.Lerp(baseC, topC, 0.55f));
            b.V(new Vector3(x, -2, 0), Vector3.back, baseC);
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 3, c = a + 3;
            b.Q(a, c, c + 1, a + 1); b.Q(a + 1, c + 1, c + 2, a + 2);
        }
        return b.M("hills");
    }

    static Mesh SparkMesh(Color col)
    {
        var b = new MB(); float s = 0.09f;
        for (int k = 0; k < 2; k++)
        {
            var nrm = k == 0 ? Vector3.back : Vector3.forward;
            int c0 = b.V(Vector3.zero, nrm, col);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, rr = i % 2 == 0 ? s : s * 0.38f;
                b.V(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0), nrm, col);
            }
            for (int i = 0; i < 8; i++)
            {
                int a = c0 + 1 + i, bb = c0 + 1 + (i + 1) % 8;
                if (k == 0) b.t.AddRange(new[] { c0, bb, a }); else b.t.AddRange(new[] { c0, a, bb });
            }
        }
        return b.M("spark");
    }

    ParticleSystem MakeBurst(Color col)
    {
        var go = new GameObject("Burst"); go.transform.SetParent(cgRoot, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); main.startSpeed = 0; main.gravityModifier = 0.6f; main.maxParticles = 600;
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); main.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
        var em = ps.emission; em.enabled = false;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = SparkMesh(col); r.sharedMaterial = cgMat; r.shadowCastingMode = ShadowCastingMode.Off;
        r.alignment = ParticleSystemRenderSpace.View;
        var b = new MaterialPropertyBlock(); b.SetFloat("_Emit", 1f); r.SetPropertyBlock(b);
        ps.Play();
        return ps;
    }

    ParticleSystem MakeSnow()
    {
        var go = new GameObject("CgSnow"); go.transform.SetParent(cgCamTr, false); go.transform.localPosition = new Vector3(0, 9, 14);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 9; main.startSpeed = 0; main.maxParticles = 1400;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        var em = ps.emission; em.rateOverTime = 140;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60, 0.1f, 26);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-1.6f, -0.8f); vel.y = new ParticleSystem.MinMaxCurve(-1.3f, -1.9f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = SnowFlakeMesh();
        var mat = new Material(AcmLoader.Mat); mat.SetFloat("_Emit", 0.6f); mat.SetFloat("_HasTint", 0); r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    void Burst(int fx, Vector3 local, int count, float speed = 3.5f)
    {
        if (fx < 0 || fx >= cgFx.Count) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = CgO + local;
            var d = UnityEngine.Random.onUnitSphere; d.z *= 0.3f;
            ep.velocity = d * speed * UnityEngine.Random.Range(0.5f, 1.2f) + Vector3.up * 1.2f;
            cgFx[fx].Emit(ep, 1);
        }
    }

    // ===== 모델 맞추기·재사용 =====
    (Vector3 size, float minY, float cx) Fit(string model)
    {
        if (cgFit.TryGetValue(model, out var f)) return f;
        var g = AcCatalog.Spawn(model, null, null); g.transform.position = new Vector3(0, -3000, 0);
        var bs = new Bounds(); bool first = true;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (first) { bs = r.bounds; first = false; } else bs.Encapsulate(r.bounds); }
        f = (bs.size, bs.min.y + 3000, bs.center.x);
        Destroy(g);
        cgFit[model] = f;
        return f;
    }

    GameObject Take(string key, Func<GameObject> make)
    {
        if (cgPool.TryGetValue(key, out var st) && st.Count > 0) { var g = st.Pop(); g.SetActive(true); return g; }
        var n = make(); n.transform.SetParent(cgRoot, false);
        return n;
    }
    void Give(string key, GameObject g)
    {
        if (g == null) return;
        g.SetActive(false);
        if (!cgPool.TryGetValue(key, out var st)) cgPool[key] = st = new Stack<GameObject>();
        st.Push(g);
    }

    GameObject ModelH(string model, float h, Dictionary<string, Color> tints = null, float emit = 0)
    {
        var f = Fit(model);
        float k = h / Mathf.Max(0.01f, f.size.y);
        var holder = new GameObject(model);
        var g = AcCatalog.Spawn(model, holder.transform, tints);
        g.transform.localScale = Vector3.one * k;
        g.transform.localPosition = new Vector3(-f.cx * k, -f.minY * k, 0);
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        if (emit > 0) SetEmit(g, emit);
        return holder;
    }

    // ========================================================
    // 판 시작
    void ResetRun()
    {
        foreach (var t in cgThings) Give(t.pool, t.go);
        cgThings.Clear();
        foreach (var t in cgTiles) Give("tile", t.go);
        cgTiles.Clear();
        foreach (var s in cgScenery) if (s.t) Destroy(s.t.gameObject);
        cgScenery.Clear();
        cgPops.Clear();
        cgX = 0; cgY = 0; cgVy = 0; cgSpeed = 8.5f; cgTime = 0; cgHp = 100; cgFever = 0; cgFeverT = 0; cgInvT = 0; cgHurtT = 0; cgGiantT = 0; cgMagnetT = 0; cgRushT = 0;
        cgScore = 0; cgShownScore = 0; cgJelly = 0; cgJumps = 0; cgGrounded = true; cgSlide = false; cgFalling = false; cgNewBest = false; cgGiantK = 1; cgCamY = 0;
        cgCursor = 18; cgGroundEnd = -30; cgNextItem = 70; cgDecoNear = -20; cgDecoMid = -40; cgChunkN = 0;
        GroundTo(cgCursor);
        for (int i = 0; i < 10; i++) Jelly(4 + i * 1.2f, 0.55f, i % 4 == 3 ? 1 : 0);   // 처음엔 젤리 줄
        SpawnScenery();
        cgHero.SetRunPose(8);
        PlaceHero();
        UpdateCgCamera(0, true);
    }

    void StartRun()
    {
        ResetRun();
        SetCg(CgState.Count);
        Sfx("count");
    }

    // ========================================================
    // 길·물건 놓기
    void GroundTo(float x)
    {
        while (cgGroundEnd < x)
        {
            float x0 = cgGroundEnd;
            var g = Take("tile", () => MeshObj("tile", cgTileMesh, null, 0f));
            g.transform.localPosition = new Vector3(x0 + 1f, 0, 0);
            cgTiles.Add((x0, x0 + 2f, g));
            cgGroundEnd += 2f;
        }
    }
    void Pit(float len) { cgGroundEnd += len; }

    bool CgGroundAt(float x)
    {
        foreach (var t in cgTiles) if (x >= t.x0 - 0.05f && x <= t.x1 + 0.05f) return true;
        return false;
    }

    static readonly Color[] JellyCols = { new Color(1f, 0.35f, 0.55f), new Color(1f, 0.8f, 0.2f), new Color(0.35f, 0.75f, 1f), new Color(0.45f, 0.9f, 0.45f), new Color(0.75f, 0.5f, 1f) };
    void Jelly(float x, float y, int kind = 0)
    {
        string key; Func<GameObject> make; Vector2 half; int val;
        switch (kind)
        {
            case 1: key = "candy"; make = () => ModelH("orn_candycane", 0.62f, null, 0.15f); half = new Vector2(0.25f, 0.32f); val = 30; break;
            case 2: key = "bear"; make = () => ModelH("orn_gingerbread", 0.62f, null, 0.12f); half = new Vector2(0.28f, 0.32f); val = 50; break;
            case 3: key = "star"; make = () => ModelH("orn_star", 0.9f, new Dictionary<string, Color> { { "orn", new Color(1f, 0.82f, 0.2f) } }, 0.45f); half = new Vector2(0.42f, 0.45f); val = 200; break;
            default:
                int ci = cgRnd.Next(JellyCols.Length); key = "jelly" + ci;
                make = () => ModelH("orn_ball", 0.42f, new Dictionary<string, Color> { { "orn", JellyCols[ci] } }, 0.25f); half = new Vector2(0.24f, 0.24f); val = 10; break;
        }
        var g = Take(key, make);
        cgThings.Add(new CgThing { kind = kind, go = g, pool = key, p = new Vector3(x, y, 0), baseY = y, half = half, value = val, spin = (float)cgRnd.NextDouble() * 360 });
        g.transform.localPosition = new Vector3(x, y - half.y, 0);
    }

    void Item(float x, float y, int kind)   // 10 하트 11 거대화 12 자석 13 질주
    {
        string key = "item" + kind;
        var g = Take(key, () =>
        {
            GameObject m;
            switch (kind)
            {
                case 10: m = ModelH("orn_heart", 0.9f, new Dictionary<string, Color> { { "orn", new Color(0.95f, 0.2f, 0.3f) } }, 0.3f); break;
                case 11: m = ModelH("orn_gift", 0.95f, new Dictionary<string, Color> { { "orn", new Color(1f, 0.75f, 0.15f) } }, 0.25f); break;
                case 12: m = ModelH("orn_bell", 0.95f, new Dictionary<string, Color> { { "orn", new Color(0.9f, 0.2f, 0.25f) } }, 0.25f); break;
                default: m = ModelH("orn_snowflake", 0.95f, new Dictionary<string, Color> { { "orn", new Color(0.5f, 0.85f, 1f) } }, 0.45f); break;
            }
            var ring = MeshObj("ring", cgRingMesh, m.transform, 0.9f); ring.transform.localPosition = new Vector3(0, 0.48f, 0);
            return m;
        });
        cgThings.Add(new CgThing { kind = kind, go = g, pool = key, p = new Vector3(x, y, 0), baseY = y, half = new Vector2(0.5f, 0.5f), spin = 0 });
    }

    static readonly string[] LowObs = { "candy_cane_big", "snowman_small", "gift_box", "present_sack", "snowball_pile" };
    static readonly string[] TallObs = { "snowman_big", "pine_small", "gift_tall" };
    void Obstacle(float x, int kind)   // 20 낮은(점프) 21 높은(2단 점프) 22 매달린(슬라이드)
    {
        string key; Func<GameObject> make; Vector2 half; float cy;
        if (kind == 20)
        {
            int i = cgRnd.Next(LowObs.Length); key = "low" + i;
            float h = LowObs[i] == "snowball_pile" ? 0.8f : 1.0f;
            make = () => ModelH(LowObs[i], h, LowObs[i] == "gift_box" ? new Dictionary<string, Color> { { "box", JellyCols[cgRnd.Next(5)] } } : null);
            half = new Vector2(0.38f, h / 2 - 0.05f); cy = h / 2;
        }
        else if (kind == 21)
        {
            int i = cgRnd.Next(TallObs.Length); key = "tall" + i;
            make = () =>
            {
                if (TallObs[i] == "gift_tall")
                {   // 선물 탑 (3층)
                    var holder = new GameObject("giftTower");
                    for (int k = 0; k < 3; k++)
                    {
                        var b = ModelH(k == 1 ? "gift_tall" : "gift_box", k == 1 ? 0.9f : 0.62f, new Dictionary<string, Color> { { "box", JellyCols[(k * 2 + 1) % 5] } });
                        b.transform.SetParent(holder.transform, false); b.transform.localPosition = new Vector3(0, k == 0 ? 0 : k == 1 ? 0.62f : 1.52f, 0); b.transform.localRotation = Quaternion.Euler(0, k * 25, 0);
                    }
                    return holder;
                }
                return ModelH(TallObs[i], 2.1f);
            };
            half = new Vector2(0.45f, 1.0f); cy = 1.05f;
        }
        else
        {
            key = "hang";
            make = () =>
            {
                var holder = new GameObject("hang");
                var pole = MeshObj("pole", cgPoleMesh, holder.transform, 0f); pole.transform.localPosition = new Vector3(0, 1.45f, 0);
                var ball = ModelH("orn_ball_glitter", 0.8f, new Dictionary<string, Color> { { "orn", new Color(0.85f, 0.15f, 0.25f) } }, 0.15f);
                ball.transform.SetParent(holder.transform, false); ball.transform.localPosition = new Vector3(0, 0.78f, 0);
                var bow = ModelH("orn_bow", 0.45f, null, 0.1f); bow.transform.SetParent(holder.transform, false); bow.transform.localPosition = new Vector3(0, 1.45f, 0);
                return holder;
            };
            half = new Vector2(0.38f, 9f); cy = 0.82f + 9f;
        }
        var g = Take(key, make);
        g.transform.localPosition = new Vector3(x, 0, 0); g.transform.localRotation = Quaternion.Euler(0, kind == 22 ? 0 : -20 + cgRnd.Next(40), 0);
        cgThings.Add(new CgThing { kind = kind, go = g, pool = key, p = new Vector3(x, cy, 0), half = half });
    }

    void JellyArc(float xc, float halfW, float top, int n, int kind = 0)
    {
        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0 : -1 + 2f * i / (n - 1);
            Jelly(xc + u * halfW, 0.55f + top * (1 - u * u), kind);
        }
    }
    void JellyLine(float x0, int n, float y = 0.55f, float gap = 1.1f, int every = 5)
    {
        for (int i = 0; i < n; i++) Jelly(x0 + i * gap, y, (i % every == every - 1) ? 1 : 0);
    }

    // 다음 조각 (어려움은 시간에 따라)
    void NextChunk()
    {
        float x = cgCursor; int tier = Mathf.Min(4, (int)(cgTime / 22f));
        float air = cgSpeed * (2 * CgJump1 / CgGrav);      // 한 번 점프로 가는 거리
        float gap = Mathf.Max(3.5f, cgSpeed * 0.6f);
        cgChunkN++;
        if (cgFeverT > 0)
        {   // 피버: 장애물 없이 무지개 젤리
            GroundTo(x + 14);
            for (int i = 0; i < 12; i++) { Jelly(x + i * 1.1f, 0.55f + Mathf.Abs(Mathf.Sin(i * 0.5f)) * 1.6f, i % 6 == 5 ? 2 : 0); if (i % 3 == 0) Jelly(x + i * 1.1f, 3.0f, 0); }
            cgCursor = x + 14; return;
        }
        if (x > cgNextItem)
        {
            cgNextItem = x + 85 + cgRnd.Next(50);
            int it = cgHp < 45 && cgRnd.NextDouble() < 0.65 ? 10 : new[] { 10, 11, 12, 13 }[cgRnd.Next(4)];
            GroundTo(x + 10);
            JellyLine(x, 3); Item(x + 4.5f, 1.4f, it); JellyLine(x + 6.5f, 3);
            cgCursor = x + 10; return;
        }
        int[] pool = tier switch
        {
            0 => new[] { 0, 1, 1, 2, 3, 4, 8 },
            1 => new[] { 0, 1, 2, 3, 4, 5, 6, 8 },
            2 => new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            _ => new[] { 1, 2, 3, 4, 5, 6, 7, 9, 10, 11 },
        };
        int type = pool[cgRnd.Next(pool.Length)];
        if (cgChunkN < 3) type = cgChunkN == 1 ? 0 : 1;
        switch (type)
        {
            case 0: GroundTo(x + 11); JellyLine(x, 10, 0.55f, 1.1f); cgCursor = x + 11; break;
            case 1:   // 낮은 장애물 하나
                {
                    float ox = x + 4; GroundTo(ox + gap + 4);
                    JellyLine(x, 3); Obstacle(ox, 20); JellyArc(ox, air * 0.42f, 1.35f, 7); JellyLine(ox + air * 0.5f + 0.6f, 3);
                    cgCursor = ox + gap + 2; break;
                }
            case 2:   // 낮은 장애물 두 개
                {
                    float ox = x + 3, ox2 = ox + Mathf.Max(air + 1.6f, 7.5f); GroundTo(ox2 + gap + 2);
                    Obstacle(ox, 20); JellyArc(ox, air * 0.42f, 1.35f, 6); Obstacle(ox2, 20); JellyArc(ox2, air * 0.42f, 1.35f, 6, 0);
                    cgCursor = ox2 + gap; break;
                }
            case 3:   // 슬라이드
                {
                    float ox = x + 4; GroundTo(ox + gap + 4);
                    JellyLine(x, 3); Obstacle(ox, 22); for (int i = -2; i <= 2; i++) Jelly(ox + i * 0.9f, 0.32f, i == 0 ? 2 : 0);
                    cgCursor = ox + gap + 1; break;
                }
            case 4:   // 구덩이
                {
                    float pit = Mathf.Min(air * 0.62f, 2.5f + tier * 0.6f);
                    GroundTo(x + 4); JellyLine(x, 3);
                    float px = cgGroundEnd; Pit(pit);
                    JellyArc(px + pit / 2, air * 0.45f, 1.5f, 7);
                    GroundTo(px + pit + gap + 2); cgCursor = px + pit + gap; break;
                }
            case 5:   // 높은 장애물 (2단 점프)
                {
                    float ox = x + 4; GroundTo(ox + gap + 5);
                    JellyLine(x, 3); Obstacle(ox, 21); JellyArc(ox, air * 0.55f, 2.7f, 9); Jelly(ox, 3.4f, 3);
                    cgCursor = ox + gap + 2.5f; break;
                }
            case 6:   // 낮은 + 슬라이드
                {
                    float ox = x + 3, ox2 = ox + Mathf.Max(air + 1.5f, 7); GroundTo(ox2 + gap + 3);
                    Obstacle(ox, 20); JellyArc(ox, air * 0.42f, 1.35f, 6); Obstacle(ox2, 22); for (int i = -1; i <= 1; i++) Jelly(ox2 + i * 0.9f, 0.32f);
                    cgCursor = ox2 + gap; break;
                }
            case 7:   // 슬라이드 두 개
                {
                    float ox = x + 3, ox2 = ox + Mathf.Max(cgSpeed * 0.7f, 5.5f); GroundTo(ox2 + gap + 3);
                    Obstacle(ox, 22); Obstacle(ox2, 22); for (float jx = ox - 1.2f; jx <= ox2 + 1.2f; jx += 1.1f) Jelly(jx, 0.32f);
                    cgCursor = ox2 + gap; break;
                }
            case 8:   // 별 사다리 (2단 점프로 별)
                {
                    GroundTo(x + 12);
                    for (int i = 0; i < 6; i++) Jelly(x + 2 + i * 0.9f, 0.6f + i * 0.5f, i == 3 ? 1 : 0);
                    Jelly(x + 8, 3.5f, 3); JellyLine(x + 9.5f, 2);
                    cgCursor = x + 12; break;
                }
            case 9:   // 구덩이 + 낮은 장애물
                {
                    float pit = Mathf.Min(air * 0.55f, 3.2f);
                    GroundTo(x + 3); float px = cgGroundEnd; Pit(pit); JellyArc(px + pit / 2, air * 0.45f, 1.5f, 6);
                    float ox = px + pit + Mathf.Max(air * 0.9f, 5.5f); GroundTo(ox + gap + 3);
                    Obstacle(ox, 20); JellyArc(ox, air * 0.42f, 1.35f, 6);
                    cgCursor = ox + gap; break;
                }
            case 10:  // 높은 + 낮은
                {
                    float ox = x + 3, ox2 = ox + Mathf.Max(air * 1.25f + 2, 9); GroundTo(ox2 + gap + 3);
                    Obstacle(ox, 21); JellyArc(ox, air * 0.55f, 2.7f, 8); Obstacle(ox2, 20); JellyArc(ox2, air * 0.42f, 1.35f, 6);
                    cgCursor = ox2 + gap; break;
                }
            default:  // 11: 구덩이 두 개
                {
                    float pit = Mathf.Min(air * 0.5f, 3f);
                    GroundTo(x + 3); float p1 = cgGroundEnd; Pit(pit); JellyArc(p1 + pit / 2, air * 0.45f, 1.5f, 5);
                    GroundTo(p1 + pit + Mathf.Max(air * 0.9f, 5)); float p2 = cgGroundEnd; Pit(pit); JellyArc(p2 + pit / 2, air * 0.45f, 1.5f, 5);
                    GroundTo(p2 + pit + gap + 2); cgCursor = p2 + pit + gap; break;
                }
        }
    }

    // 배경 소품: 가까운 줄(길 뒤) + 가운데 줄(나무·집)
    static readonly string[] DecoNear = { "candy_cane_big", "lamp_post", "gift_pile", "snowman_small", "pine_small", "bush_snowy", "bench", "reindeer_light", "lamp_post", "bush_holly" };
    static readonly string[] DecoMid = { "pine_snowy", "pine_tall", "house_cottage", "pine_snowy", "igloo", "reindeer_light", "round_tree", "pine_tall", "ice_sculpture", "light_arch", "snowman_big", "pine_snowy" };
    void SpawnScenery()
    {
        float ahead = cgX + 60;
        while (cgDecoNear < ahead)
        {
            string n = DecoNear[cgRnd.Next(DecoNear.Length)];
            var g = AcCatalog.Spawn(n, cgRoot, null);
            float z = 2.4f + (float)cgRnd.NextDouble() * 2.2f;
            g.transform.localPosition = new Vector3(cgDecoNear, -0.05f, z); g.transform.localRotation = Quaternion.Euler(0, cgRnd.Next(360), 0);
            g.transform.localScale = Vector3.one * (n == "lamp_post" ? 1.2f : 1.0f + (float)cgRnd.NextDouble() * 0.3f);
            cgScenery.Add((g.transform, 0));
            cgDecoNear += 3.5f + (float)cgRnd.NextDouble() * 4f;
        }
        while (cgDecoMid < ahead + 30)
        {
            string n = DecoMid[cgRnd.Next(DecoMid.Length)];
            var g = AcCatalog.Spawn(n, cgRoot, null);
            float z = 13f + (float)cgRnd.NextDouble() * 16f;
            g.transform.localPosition = new Vector3(cgDecoMid, -0.05f, z); g.transform.localRotation = Quaternion.Euler(0, n == "house_cottage" ? 200 : cgRnd.Next(360), 0);
            g.transform.localScale = Vector3.one * (n == "house_cottage" ? 1.0f : 1.0f + (float)cgRnd.NextDouble() * 0.7f);
            cgScenery.Add((g.transform, 0));
            cgDecoMid += 2.5f + (float)cgRnd.NextDouble() * 4.5f;
        }
        for (int i = cgScenery.Count - 1; i >= 0; i--)
            if (cgScenery[i].t.localPosition.x < cgX - 30) { Destroy(cgScenery[i].t.gameObject); cgScenery.RemoveAt(i); }
    }

    // ========================================================
    // 매 프레임
    void CookieGameUpdate(float dt)
    {
        if (!playingGame || cgRoot == null) return;
        dt = Mathf.Min(dt, 0.05f);
        cgStateT += dt;
        CgInput(out bool jump, out bool slideHeld);
        if (Key("esc")) { if (cg == CgState.Play) Die(); else if (cg == CgState.Board || cg == CgState.Result) SetCg(CgState.Menu); else { CloseCookieGame(); return; } }
        switch (cg)
        {
            case CgState.Menu:
                cgHero.SetRunPose(8);
                break;
            case CgState.Count:
                {
                    int before = Mathf.CeilToInt(3 - (cgStateT - dt) / 0.75f), after = Mathf.CeilToInt(3 - cgStateT / 0.75f);
                    if (after != before && after > 0) Sfx("count");
                    if (cgStateT >= 2.25f) { SetCg(CgState.Play); Sfx("go"); cgHero.SetRunPose(1); }
                    break;
                }
            case CgState.Play: PlayStep(dt, jump, slideHeld); break;
            case CgState.Dead:
                cgSpeed = Mathf.MoveTowards(cgSpeed, 0, dt * 9f); cgX += cgSpeed * dt;
                if (cgY > 0 || !CgGroundAt(cgX)) { cgVy -= CgGrav * dt; cgY = Mathf.Max(CgGroundAt(cgX) ? 0 : -4, cgY + cgVy * dt); }
                if (cgStateT > 1.8f) { SetCg(CgState.Result); if (cgNewBest) Sfx("record"); }
                break;
        }
        // 젤리 빙글빙글, 아이템 둥실, 부서진 장애물 날아감
        foreach (var t in cgThings)
        {
            if (t.go == null) continue;
            if (t.flying)
            {
                t.t += dt; t.vy -= CgGrav * dt; t.p.x += t.vx * dt; t.p.y += t.vy * dt;
                t.go.transform.localPosition = t.p;
                t.go.transform.localRotation *= Quaternion.Euler(0, 0, -540 * dt);
                continue;
            }
            if (t.kind < 20)
            {
                t.spin += dt * (t.kind >= 10 ? 90 : 150);
                float bob = t.kind >= 10 ? Mathf.Sin(Time.time * 3 + t.p.x) * 0.12f : 0;
                t.go.transform.localPosition = new Vector3(t.p.x, t.p.y - t.half.y + bob, t.p.z);
                t.go.transform.localRotation = Quaternion.Euler(0, t.spin, 0);
            }
        }
        // 짝꿍 쿠키 (뒤에서 둥실둥실 따라옴)
        var petWant = new Vector3(cgX - 1.3f * cgGiantK, cgY + 1.6f * cgGiantK + 0.3f + Mathf.Sin(Time.time * 4f) * 0.15f, -0.4f);
        cgPet.localPosition = Vector3.Lerp(cgPet.localPosition, petWant, 1 - Mathf.Exp(-dt * 8));
        cgPet.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 6f) * 8f, 90 + Mathf.Sin(Time.time * 2f) * 15f, Mathf.Sin(Time.time * 3f) * 10f);
        PlaceHero();
        UpdateCgCamera(dt, false);
        for (int i = cgPops.Count - 1; i >= 0; i--) { var p = cgPops[i]; p.t += dt; p.w.y += dt * 1.2f; cgPops[i] = p; if (p.t > 0.9f) cgPops.RemoveAt(i); }
        float target = cg == CgState.Result ? cgLastScore : cgScore;
        cgShownScore = Mathf.MoveTowards(cgShownScore, target, Mathf.Max(60, Mathf.Abs(target - cgShownScore) * 4) * dt);
        if (cg == CgState.Board) UpdatePodium(dt);
    }

    void PlayStep(float dt, bool jump, bool slideHeld)
    {
        cgTime += dt;
        float baseSpeed = 8.5f + Mathf.Min(7.5f, cgTime * 0.1f);
        cgSpeed = Mathf.MoveTowards(cgSpeed, baseSpeed * (cgRushT > 0 ? 1.75f : 1f), dt * 12f);
        cgX += cgSpeed * dt;
        cgDistM = (int)cgX;
        // 체력은 조금씩 닳음
        cgHp -= dt * (1.35f + cgTime * 0.011f);
        cgInvT -= dt; cgHurtT -= dt; cgMagnetT -= dt; cgRushT -= dt;
        if (cgGiantT > 0) cgGiantT -= dt;
        cgGiantK = Mathf.MoveTowards(cgGiantK, cgGiantT > 0 ? 2.1f : 1f, dt * 4f);
        if (cgFeverT > 0) { cgFeverT -= dt; if (cgFeverT <= 0) cgFever = 0; }
        // 점프·슬라이드
        if (jump && !cgFalling)
        {
            if (cgGrounded) { cgVy = CgJump1; cgJumps = 1; cgGrounded = false; Sfx("jump", 0.8f); cgHero.SetRunPose(2); }
            else if (cgJumps < 2) { cgVy = CgJump2; cgJumps = 2; Sfx("jump2", 0.85f); cgHero.SetRunPose(3); Burst(1, new Vector3(cgX, cgY + 0.2f, 0), 8, 2.5f); }
        }
        bool wantSlide = slideHeld && cgGrounded && !cgFalling;
        if (wantSlide && !cgSlide) Sfx("slide", 0.6f);
        cgSlide = wantSlide;
        // 중력·땅
        cgVy -= CgGrav * dt * (cgVy < 0 ? 1.15f : 1f);
        float ny = cgY + cgVy * dt;
        bool onGround = CgGroundAt(cgX - 0.2f) || CgGroundAt(cgX + 0.2f) || cgFeverT > 0 || cgRushT > 0;   // 피버·질주 땐 구덩이 위도 달림
        if (onGround && cgY >= -0.05f && ny <= 0)
        {
            ny = 0; cgVy = 0;
            if (!cgGrounded) { cgGrounded = true; cgJumps = 0; Burst(1, new Vector3(cgX, 0.05f, 0), 5, 1.5f); }
        }
        else if (cgGrounded && !onGround) { cgGrounded = false; cgJumps = 1; }
        cgY = ny;
        if (cgY < -0.3f && !onGround) cgFalling = true;
        if (cgY < -4.5f) FellIntoPit();
        // 자세
        if (cgHurtT > 0.25f) cgHero.SetRunPose(5);
        else if (cgSlide) cgHero.SetRunPose(4);
        else if (cgGrounded) cgHero.SetRunPose(1);
        else if (cgJumps == 2) { if (cgHero.runPose != 3) cgHero.SetRunPose(3); }
        else if (cgHero.runPose != 2) cgHero.SetRunPose(2);
        // 만들기·치우기
        while (cgCursor < cgX + 48) NextChunk();
        if (cgGroundEnd < cgX + 48) GroundTo(cgX + 48);
        for (int i = cgTiles.Count - 1; i >= 0; i--) if (cgTiles[i].x1 < cgX - 18) { Give("tile", cgTiles[i].go); cgTiles.RemoveAt(i); }
        SpawnScenery();
        // 부딪힘
        float hh = (cgSlide ? 0.5f : HeroH) * cgGiantK, hw = 0.26f * cgGiantK;
        var hero = new Rect(cgX - hw, cgY, hw * 2, hh);
        bool strong = cgGiantT > 0 || cgRushT > 0 || cgFeverT > 0;
        for (int i = cgThings.Count - 1; i >= 0; i--)
        {
            var t = cgThings[i];
            if (t.p.x < cgX - 16) { Give(t.pool, t.go); cgThings.RemoveAt(i); continue; }
            if (t.flying) { if (t.t > 1.6f) { Give(t.pool, t.go); cgThings.RemoveAt(i); } continue; }
            if (t.dead) continue;
            if (t.p.x > cgX + 12) continue;
            // 자석: 가까운 젤리가 날아옴
            if (t.kind < 10 && (cgMagnetT > 0 || cgFeverT > 0))
            {
                var d = new Vector2(cgX + 0.3f - t.p.x, cgY + hh * 0.5f - t.p.y);
                if (d.magnitude < 5.5f) { var mv = d.normalized * Mathf.Min(d.magnitude, dt * 18f); t.p.x += mv.x; t.p.y += mv.y; }
            }
            var box = new Rect(t.p.x - t.half.x, t.p.y - t.half.y, t.half.x * 2, t.half.y * 2);
            if (!hero.Overlaps(box)) continue;
            if (t.kind < 10) Collect(t);
            else if (t.kind < 20) TakeItem(t);
            else if (strong) Smash(t);
            else if (cgInvT <= 0) Hurt(t);
        }
        if (strong && cgTrail != null)
        {
            var ep = new ParticleSystem.EmitParams { position = CgO + new Vector3(cgX - 0.3f, cgY + 0.5f * cgGiantK, 0), velocity = new Vector3(-2, UnityEngine.Random.Range(-0.5f, 1f), 0) };
            cgTrail.Emit(ep, 1);
        }
        cgScore = cgDistM + cgJelly;
        if (cgHp <= 0) { cgHp = 0; Die(); }
    }

    float cgPickSfxT;   // 먹는 소리는 작게, 너무 자주 겹치지 않게
    void Collect(CgThing t)
    {
        t.dead = true; Give(t.pool, t.go); t.go = null;
        int v = t.value * (cgFeverT > 0 ? 2 : 1);
        cgJelly += v;
        if (cgFeverT <= 0) cgFever = Mathf.Min(100, cgFever + (t.kind == 3 ? 6 : t.kind == 0 ? 0.45f : 1.5f));
        if (t.kind == 3) { Sfx("star", 0.5f); Burst(0, t.p, 18, 5); }
        else { if (Time.time >= cgPickSfxT) { cgPickSfxT = Time.time + 0.12f; Sfx(t.kind == 0 ? "jelly" : "coin", t.kind == 0 ? 0.14f : 0.2f, 0.92f + (float)cgRnd.NextDouble() * 0.08f); } Burst(t.kind == 2 ? 0 : (cgRnd.Next(2) + 1), t.p, 4, 2.5f); }
        if (t.kind > 0) cgPops.Add((CgO + t.p, "+" + v, t.kind == 3 ? new Color(1f, 0.85f, 0.2f) : Color.white, 0));
        if (cgFever >= 100 && cgFeverT <= 0) StartFever();
    }

    void StartFever()
    {
        cgFeverT = 7f; Sfx("fever");
        foreach (var o in cgThings) if (!o.dead && !o.flying && o.kind >= 20 && o.p.x > cgX - 2 && o.go != null) Smash(o, false);
        cgPops.Add((CgO + new Vector3(cgX + 2, 3, 0), "피버 타임!", new Color(1f, 0.85f, 0.25f), 0));
    }

    void TakeItem(CgThing t)
    {
        t.dead = true; Give(t.pool, t.go); t.go = null;
        string msg;
        switch (t.kind)
        {
            case 10: cgHp = Mathf.Min(100, cgHp + 30); Sfx("heart"); msg = "체력 +30"; Burst(2, t.p, 16, 4); break;
            case 11: cgGiantT = 6.5f; Sfx("power"); msg = "거대화!"; Burst(0, t.p, 20, 5); break;
            case 12: cgMagnetT = 8f; Sfx("power"); msg = "젤리 자석!"; Burst(3, t.p, 20, 5); break;
            default: cgRushT = 4.5f; Sfx("power"); msg = "질주!"; Burst(3, t.p, 20, 5); break;
        }
        cgPops.Add((CgO + t.p + Vector3.up * 0.6f, msg, new Color(1f, 0.92f, 0.5f), 0));
    }

    void Smash(CgThing t, bool score = true)
    {
        t.flying = true; t.t = 0; t.vx = cgSpeed + 6; t.vy = t.kind == 22 ? 14 : 9;
        t.p = t.go.transform.localPosition;
        Sfx("smash", 0.7f); Burst(1, new Vector3(t.p.x, 0.8f, 0), 10, 5); cgShake = 0.25f;
        if (score) { cgJelly += 50; cgPops.Add((CgO + t.p + Vector3.up * 1.5f, "+50", new Color(1f, 0.85f, 0.4f), 0)); }
    }

    void Hurt(CgThing t)
    {
        cgHp -= 22; cgInvT = 1.6f; cgHurtT = 0.5f; cgShake = 0.4f; cgFlash = 0.35f;
        Sfx("hit"); Burst(2, new Vector3(cgX, cgY + 0.6f, 0), 10, 3);
        t.dead = true;
    }
    float cgFlash;

    void FellIntoPit()
    {
        cgHp -= 30; Sfx("fall"); cgShake = 0.3f; cgFlash = 0.3f;
        if (cgHp <= 0) { cgHp = 0; Die(); return; }
        // 다음 땅 위로 구출
        float nx = cgX; for (int k = 0; k < 40 && !(CgGroundAt(nx) && CgGroundAt(nx + 1.2f)); k++) nx += 0.5f;
        cgX = nx + 0.6f; cgY = 2.2f; cgVy = 2; cgFalling = false; cgGrounded = false; cgJumps = 1; cgInvT = 2f;
        cgPops.Add((CgO + new Vector3(cgX, 3f, 0), "구출!", new Color(0.7f, 0.9f, 1f), 0));
    }

    void Die()
    {
        if (cg != CgState.Play) return;
        SetCg(CgState.Dead);
        cgHero.SetRunPose(6); Sfx("gameover");
        cgLastScore = cgScore; cgShownScore = 0;
        cgLastBest = MyBest();
        cgNewBest = cgScore > cgLastBest;
        // 판마다 따로 저장 (같은 사람이 명예의 전당에 여러 번 오를 수 있음)
        if (cgScore > 0 && net != null)
            net.Put("scores/" + myKey + "~" + AcSync.UnixMs.ToString("0"), "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"best\":" + cgScore + ",\"look\":" + AcJson.Esc(me.look.Encode()) + ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
    }

    void PlaceHero()
    {
        if (cgHero == null) return;
        cgHero.transform.localPosition = new Vector3(cgX, cgY, 0);
        cgHero.transform.localRotation = Quaternion.Euler(0, cg == CgState.Menu || cg == CgState.Count ? 140 : 90, 0);
        cgHero.transform.localScale = Vector3.one * cgGiantK;
        bool blink = cgInvT > 0 && cg == CgState.Play && Mathf.Repeat(Time.time * 12, 1) < 0.4f;
        foreach (var r in cgHero.GetComponentsInChildren<Renderer>()) r.enabled = !blink;
    }

    void UpdateCgCamera(float dt, bool snap)
    {
        if (cgCam == null) return;
        // 세로 화면이면 가운데 띠에만 그림
        var band = CgBand();
        cgCam.rect = new Rect(0, 1 - (band.yMax / SH), 1, band.height / SH);
        float aspect = Mathf.Max(0.5f, band.width / Mathf.Max(1, band.height));
        float hfov = 2 * Mathf.Atan(Mathf.Tan(20 * Mathf.Deg2Rad) * aspect);
        float wantW = cg == CgState.Board ? 10f : Mathf.Lerp(9f, 15f, Mathf.Clamp01((aspect - 0.8f) / 1.0f));
        float dist = Mathf.Clamp(wantW / 2 / Mathf.Tan(hfov / 2), 8f, 17f);
        float targetY = cg == CgState.Play ? Mathf.Clamp((cgY - 1.2f) * 0.45f, 0, 2.2f) : 0;
        cgCamY = snap ? targetY : Mathf.Lerp(cgCamY, targetY, 1 - Mathf.Exp(-dt * 3));
        cgShake = Mathf.Max(0, cgShake - dt);
        cgFlash = Mathf.Max(0, cgFlash - dt);
        var shake = cgShake > 0 ? (Vector3)(UnityEngine.Random.insideUnitCircle * cgShake * 0.35f) : Vector3.zero;
        Vector3 look = new Vector3(cgX + wantW * 0.27f, 1.45f + Mathf.Lerp(1.0f, 0.25f, Mathf.Clamp01((aspect - 0.8f) / 0.8f)) + cgCamY, 0);   // 세로에 가까우면 땅을 아래쪽에
        Vector3 pos = look + new Vector3(0, dist * 0.08f + 0.35f, -dist);
        if (cg == CgState.Menu || cg == CgState.Count && cgStateT < 0.6f) { look = new Vector3(cgX + 1.6f, 1.2f, 0); pos = look + new Vector3(-1.5f, 1.2f, -dist * 0.55f); }
        if (cg == CgState.Board) { look = PodiumPos + new Vector3(CgIsPortrait ? 0 : 2.6f, 1.7f, 0); pos = look + new Vector3(0, 1.0f, -dist); }
        cgCamTr.localPosition = snap ? pos + shake : Vector3.Lerp(cgCamTr.localPosition, pos, 1 - Mathf.Exp(-dt * 6)) + shake;
        cgCamTr.localRotation = snap ? Quaternion.LookRotation(look - pos) : Quaternion.Slerp(cgCamTr.localRotation, Quaternion.LookRotation(look - cgCamTr.localPosition), 1 - Mathf.Exp(-dt * 10));
        float cx = cgCamTr.localPosition.x;
        cgHillsTr.localPosition = new Vector3(cx, 0, 0);
        cgHill1.localPosition = new Vector3(-Mathf.Repeat(cx, HillP1), -1, 120);
        cgHill2.localPosition = new Vector3(-Mathf.Repeat(cx, HillP2), -0.5f, 70);
    }

    // ===== 입력 (여러 손가락: 슬라이드 누른 채로 점프 가능) =====
    bool cgPointerOnUi;
    readonly List<Rect> cgUi = new List<Rect>();
    Rect cgJumpBtn, cgSlideBtn;
    bool cgSlideShown;
    void CgInput(out bool jump, out bool slide)
    {
        bool j = false, s = false, onUi = false;
        void Classify(Vector2 p, bool down)
        {
            var u = new Vector2(p.x / UiK, (Screen.height - p.y) / UiK);
            if (cg == CgState.Play && cgSlideBtn.Contains(u)) { s = true; return; }
            if (cg == CgState.Play && cgJumpBtn.Contains(u)) { if (down) j = true; return; }
            foreach (var r in cgUi) if (r.Contains(u)) { if (down) onUi = true; return; }
            if (down && cg == CgState.Play) j = true;
        }
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) j = true;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed || kb.leftShiftKey.isPressed) s = true;
        }
        bool any = false;
        var ts = Touchscreen.current;
        if (ts != null)
            foreach (var t in ts.touches)
            {
                bool down = t.press.wasPressedThisFrame;
                if (!t.press.isPressed && !down) continue;
                any = true; Classify(t.position.ReadValue(), down);
            }
        if (!any)
        {
            var m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.leftButton.wasPressedThisFrame)) Classify(m.position.ReadValue(), m.leftButton.wasPressedThisFrame);
        }
#else
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) j = true;
        if (Input.GetKey(KeyCode.DownArrow)) s = true;
        if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0)) Classify(Input.mousePosition, Input.GetMouseButtonDown(0));
#endif
        jump = j && !onUi; slide = s; cgPointerOnUi = onUi; cgSlideShown = s;
    }

    // 게임 화면 띠 (UI 단위). 세로 화면: 위 점수판, 가운데 게임, 아래 큰 버튼
    bool CgIsPortrait => SH > SW * 1.1f;
    Rect CgBand()
    {
        if (!CgIsPortrait) return new Rect(0, 0, SW, SH);
        float h = Mathf.Min(SW * 1.05f, SH * 0.58f);
        float top = Mathf.Max(SH * 0.15f, (SH - h) * 0.36f);
        return new Rect(0, top, SW, h);
    }

    int MyBest()
    {
        var s = net != null ? net.Get("scores") : null; int best = 0;
        if (s != null) foreach (var kv in s) if (kv.Value is Dictionary<string, object> d && NameKey(AcJson.S(d, "name")) == myKey) best = Mathf.Max(best, (int)AcJson.D(d, "best"));
        return best;
    }

    List<(string name, int best, string look)> TopScores()
    {
        var res = new List<(string, int, string)>();
        var s = net != null ? net.Get("scores") : null;
        var mem = net != null ? net.Get("members") : null;
        if (s != null)
            foreach (var kv in s)
                if (kv.Value is Dictionary<string, object> d)
                {
                    string look = AcJson.S(d, "look");
                    if (string.IsNullOrEmpty(look) && mem != null && mem.TryGetValue(NameKey(AcJson.S(d, "name")), out var mv) && mv is Dictionary<string, object> md) look = AcJson.S(md, "look");
                    res.Add((AcJson.S(d, "name"), (int)AcJson.D(d, "best"), look));
                }
        return res.OrderByDescending(x => x.Item2).ToList();
    }
}
