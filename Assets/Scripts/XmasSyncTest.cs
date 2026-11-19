// XmasSyncTest.cs — 크리스마스 광장 (Unity 6 + URP)
// 이 파일 하나와 Assets/Resources/Holiday (Kenney Holiday Kit, CC0) 모델로 장면이 자동으로 만들어져요.
//
// 조작: WASD/방향키 또는 클릭으로 이동 · 구역 근처에서 E 키로 열기 · ESC로 닫기
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

[Serializable]
public class NetState
{
    public string id = "";
    public string name = "";
    public string color = "E74C3C";
    public float x, z, ry;
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
    const float Speed = 4.5f;
    const float NearDist = 1.6f;
    const float TreeBlock = 3.2f;     // 트리 둘레 못 들어가는 반경

    static readonly string[] Palette = { "E74C3C", "27AE60", "2E86DE", "F39C12", "8E44AD", "E84393" };
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static long NowMs => Clock.ElapsedMilliseconds;
    static double UnixMs => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<XmasSyncTest>() != null) return;
        new GameObject("XmasSyncTest").AddComponent<XmasSyncTest>();
    }

    // ===== 구역 =====
    class Zone
    {
        public string id, label, sub, desc;
        public Vector3 pos;
        public float radius;
        public Color color;
    }

    static readonly Zone[] Zones =
    {
        new Zone { id = "tree",  label = "소원 트리",      sub = "소원을 걸어요",   pos = Vector3.zero,              radius = 4.8f, color = new Color(1f, 0.85f, 0.4f),
                   desc = "올해의 소원을 트리에 걸어 보세요. 다른 사람의 소원도 볼 수 있어요." },
        new Zone { id = "spot",  label = "틀린 그림 찾기", sub = "2인 대결",        pos = new Vector3(-10, 0, 6),    radius = 3.2f, color = new Color(0.55f, 0.75f, 1f),
                   desc = "같은 그림 두 장에서 다른 곳을 먼저 찾는 사람이 이겨요." },
        new Zone { id = "mines", label = "눈 지뢰밭",      sub = "지뢰찾기 대결",   pos = new Vector3(10, 0, 6),     radius = 3.2f, color = new Color(1f, 0.6f, 0.55f),
                   desc = "같은 지뢰밭을 동시에 풀어요. 먼저 다 찾으면 승리, 지뢰를 밟으면 패배!" },
        new Zone { id = "ark",   label = "선물 깨기",      sub = "알카노이드 대결", pos = new Vector3(-10, 0, -7),   radius = 3.2f, color = new Color(0.6f, 0.9f, 0.55f),
                   desc = "각자 선물 벽돌을 깨면서 상대에게 방해 선물을 보내요." },
        new Zone { id = "photo", label = "포토존",         sub = "단체 사진",       pos = new Vector3(10, 0, -7),    radius = 3.2f, color = new Color(1f, 0.85f, 0.5f),
                   desc = "모여서 찰칵! 사진은 내 PC의 '사진' 폴더에 저장돼요." },
    };

    // ===== UI 상태 =====
    string uiPath = "", uiName = "", uiWish = "";
    int uiColor;
    string status = "공유 폴더 경로와 이름을 입력하고 입장하세요.";
    bool joined;
    string openZone;      // 열려 있는 구역 패널
    Zone nearZone;        // 지금 서 있는 구역
    string myWish = "";
    string toast; float toastUntil;
    bool hideUI;

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
    Transform me, meVisual;
    Vector3? moveTarget;
    bool meMoving;
    float walkT;
    Material baseMat, kenneyMat;
    Texture2D colormap;
    readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
    readonly List<Light> treeLights = new List<Light>();
    readonly List<Transform> snow = new List<Transform>();
    readonly List<Transform> trainCars = new List<Transform>();
    readonly List<Transform> spinners = new List<Transform>();
    readonly Dictionary<int, RemoteView> remotes = new Dictionary<int, RemoteView>();
    readonly Dictionary<string, (string name, string text)> wishes = new Dictionary<string, (string, string)>();
    readonly Dictionary<string, GameObject> ornaments = new Dictionary<string, GameObject>();
    Dictionary<int, PeerInfo> peersView = new Dictionary<int, PeerInfo>();
    float fps;
    float trainAngle;
    bool showStats;
    GUIStyle sLabel, sSmall, sBox, sPanel, sTag, sTagMe, sTagZone, sBig, sTitle, sPrompt, sWishCard;
    Rect panelRect, uiBlockRect;
    Vector2 wishScroll;

    class RemoteView
    {
        public GameObject go;
        public Transform visual;
        public string name, zone;
        public float walkT;
    }

    // ========================================================
    void Awake()
    {
        myId = Guid.NewGuid().ToString("N").Substring(0, 8);
        uiPath = PlayerPrefs.GetString("xmas_path", "");
        uiName = PlayerPrefs.GetString("xmas_name", "");
        uiColor = PlayerPrefs.GetInt("xmas_color", 0);
        Application.targetFrameRate = 60;

        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
        Destroy(tmp);
        colormap = Resources.Load<Texture2D>("Holiday/Textures/colormap");

        BuildScene();
        me = MakeCharacter(HexColor(Palette[uiColor]), out meVisual).transform;
        me.position = new Vector3(0, 0, -12);
        me.name = "Me";
    }

    // ===== 재질/도형 헬퍼 =====
    Material Mat(Color c, bool glow = false)
    {
        var key = glow ? new Color(c.r, c.g, c.b, 0.5f) : c;
        if (matCache.TryGetValue(key, out var m)) return m;
        m = new Material(baseMat) { color = c };
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        if (glow && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 2.5f);
        }
        matCache[key] = m;
        return m;
    }

    Material KenneyMat()
    {
        if (kenneyMat != null) return kenneyMat;
        kenneyMat = new Material(baseMat) { color = Color.white };
        if (colormap != null) kenneyMat.mainTexture = colormap;
        if (kenneyMat.HasProperty("_Smoothness")) kenneyMat.SetFloat("_Smoothness", 0.2f);
        return kenneyMat;
    }

    GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Transform parent = null, bool glow = false, bool shadows = true)
    {
        var g = GameObject.CreatePrimitive(t);
        Destroy(g.GetComponent<Collider>());
        if (parent) g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = Mat(c, glow);
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return g;
    }

    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Kenney 모델 배치: 바닥을 pos.y에, 가운데를 pos.x/z에 맞춰요.
    GameObject Model(string name, Vector3 pos, float scale, float rotY = 0, Transform parent = null)
    {
        var prefab = Resources.Load<GameObject>("Holiday/" + name);
        if (prefab == null) { Debug.LogWarning("[Xmas] 모델을 찾을 수 없어요: " + name); return null; }
        var go = Instantiate(prefab);
        go.name = name;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, rotY, 0));
        go.transform.localScale = Vector3.one;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = KenneyMat();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
        }
        var b = WorldBounds(go);
        float fix = b.size.magnitude > 40f ? 0.01f : 1f;   // 단위가 cm로 들어온 경우 대비
        go.transform.localScale = Vector3.one * scale * fix;
        b = WorldBounds(go);
        go.transform.position = new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        if (parent) go.transform.SetParent(parent, true);
        return go;
    }

    Light PointLight(Vector3 pos, Color c, float intensity, float range, Transform parent = null)
    {
        var g = new GameObject("Light");
        if (parent) g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static Color HexColor(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.red;
    }

    static int StableHash(string s)
    {
        unchecked { int h = 23; foreach (char ch in s) h = h * 31 + ch; return h & 0x7fffffff; }
    }

    // ===== 장면 구성 =====
    void BuildScene()
    {
        var rnd = new System.Random(7);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        // 카메라
        cam = Camera.main;
        if (!cam)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.1f, 0.2f);
        cam.fieldOfView = 30f;
        cam.farClipPlane = 200f;
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) { camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; }

        // 조명: 은은한 달빛 + 밝은 환경광 (귀여운 톤)
        foreach (var l in FindObjectsByType<Light>()) l.enabled = false;
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.85f, 0.88f, 1f);
        moon.intensity = 1.15f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.6f;
        moon.transform.rotation = Quaternion.Euler(50, -30, 0);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.38f, 0.5f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = cam.backgroundColor;
        RenderSettings.fogStartDistance = 38f;
        RenderSettings.fogEndDistance = 80f;

        // 후처리
        var vol = new GameObject("XmasVolume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(1.1f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.75f);
        var tone = prof.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.15f); ca.saturation.Override(18f); ca.contrast.Override(8f);
        var vig = prof.Add<Vignette>(true); vig.intensity.Override(0.28f); vig.smoothness.Override(0.5f);
        vol.profile = prof;

        // 바닥 (눈)
        Prim(PrimitiveType.Cube, new Vector3(0, -0.25f, 0), new Vector3(46, 0.5f, 36), new Color(0.9f, 0.94f, 1f));

        // 돌길: 트리 둘레 원형 광장 + 입구까지 길 (매끈한 돌바닥)
        var stone = new Color(0.78f, 0.74f, 0.73f);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(15.6f, 0.01f, 15.6f), stone, null, false, false);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.012f, 0), new Vector3(15.0f, 0.01f, 15.0f), new Color(0.84f, 0.8f, 0.79f), null, false, false);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.014f, 0), new Vector3(10.2f, 0.01f, 10.2f), stone, null, false, false);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.016f, 0), new Vector3(9.6f, 0.01f, 9.6f), new Color(0.92f, 0.95f, 1f), null, false, false);
        Prim(PrimitiveType.Cube, new Vector3(0, 0.01f, -11.6f), new Vector3(3.2f, 0.02f, 8.4f), new Color(0.84f, 0.8f, 0.79f), null, false, false);

        // 큰 트리 (소원 트리)
        Model("tree-decorated-snow", Vector3.zero, 3.2f);
        Color[] bulbCols = { new Color(1f, 0.35f, 0.35f), new Color(1f, 0.8f, 0.3f), new Color(0.3f, 0.75f, 1f), new Color(1f, 0.5f, 0.9f), new Color(0.3f, 1f, 0.6f) };
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            treeLights.Add(PointLight(new Vector3(Mathf.Cos(a) * 2.4f, 2.0f + (i % 2), Mathf.Sin(a) * 2.4f), bulbCols[i], 1.6f, 4.5f));
        }
        PointLight(new Vector3(0, 7.6f, 0), new Color(1f, 0.85f, 0.4f), 3f, 7f);
        string[] presents = { "present-a-cube", "present-b-round", "present-a-rectangle", "present-b-cube", "present-a-round", "present-b-rectangle" };
        for (int i = 0; i < 10; i++)
        {
            float a = 0.3f + i * 0.63f;
            Model(presents[i % presents.Length], new Vector3(Mathf.Cos(a) * 2.5f, 0, Mathf.Sin(a) * 2.5f), R(1.6f, 2.3f), R(0, 360));
        }

        // 트리 둘레를 도는 장난감 기차
        const float railR = 4.3f;
        for (int i = 0; i < 40; i++)
        {
            float a = i * Mathf.PI * 2 / 40;
            var p = new Vector3(Mathf.Cos(a) * railR, 0.02f, Mathf.Sin(a) * railR);
            // 레일의 긴 축(X)을 원의 접선 방향(-sin a, 0, cos a)에 맞추는 회전각
            float yaw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
            Model("trainset-rail-straight", p, 1.4f, yaw);
        }
        foreach (var car in new[] { "train-locomotive", "train-tender", "train-wagon", "train-wagon-logs" })
        {
            var holder = new GameObject("Car-" + car).transform;
            var m = Model(car, Vector3.zero, 1.9f, 0);
            if (m) m.transform.SetParent(holder, true);
            trainCars.Add(holder);
        }

        // 구역 바닥 표시 + 소품
        foreach (var z in Zones)
        {
            if (z.id == "tree") continue;
            var disc = Prim(PrimitiveType.Cylinder, z.pos + new Vector3(0, 0.015f, 0), new Vector3(z.radius * 1.8f, 0.01f, z.radius * 1.8f),
                Color.Lerp(z.color, Color.white, 0.55f), null, false, false);
            disc.name = "Zone-" + z.id;
        }
        // 틀린 그림 찾기: 벤치, 호두까기 인형, 가로등
        Model("bench", new Vector3(-10, 0, 7.2f), 1.8f, 180);
        Model("nutcracker", new Vector3(-12, 0, 6.5f), 1.7f, 150);
        Model("nutcracker", new Vector3(-8, 0, 6.5f), 1.7f, 210);
        Model("candy-cane-red", new Vector3(-11.2f, 0, 4.6f), 2.2f, 20);
        Model("candy-cane-green", new Vector3(-8.8f, 0, 4.6f), 2.2f, -20);
        // 눈 지뢰밭: 눈 요새, 눈더미, 썰매, 깃발 대신 지팡이 사탕
        Model("snow-bunker", new Vector3(10, 0, 7.4f), 2.4f, 180);
        Model("snow-pile", new Vector3(8.4f, 0, 5f), 1.8f, 30);
        Model("snow-pile", new Vector3(11.8f, 0, 4.8f), 1.5f, 70);
        Model("sled", new Vector3(12.2f, 0, 6.6f), 1.7f, 120);
        Model("candy-cane-red", new Vector3(9.2f, 0, 5.6f), 2f, 0);
        Model("candy-cane-red", new Vector3(10.8f, 0, 5.2f), 2f, 40);
        // 선물 깨기: 선물 탑 + 진저브레드
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < 3 - row; i++)
                Model(presents[(row * 2 + i) % presents.Length], new Vector3(-11f + i * 1.0f + row * 0.5f, row * 0.62f, -5.6f), 2.0f, R(-15, 15));
        Model("gingerbread-man", new Vector3(-8.6f, 0, -6f), 2.6f, 200);
        Model("gingerbread-woman", new Vector3(-12.2f, 0, -7.6f), 2.6f, 150);
        // 포토존: 리스 액자(남쪽) + 루돌프 + 썰매. 카메라는 광장 쪽(북쪽)에서 찍어요.
        Prim(PrimitiveType.Cube, new Vector3(8.6f, 1.3f, -8.4f), new Vector3(0.25f, 2.6f, 0.25f), new Color(0.5f, 0.3f, 0.16f));
        Prim(PrimitiveType.Cube, new Vector3(11.4f, 1.3f, -8.4f), new Vector3(0.25f, 2.6f, 0.25f), new Color(0.5f, 0.3f, 0.16f));
        Prim(PrimitiveType.Cube, new Vector3(10f, 2.65f, -8.4f), new Vector3(3.1f, 0.3f, 0.3f), new Color(0.16f, 0.45f, 0.25f));
        Model("wreath-decorated", new Vector3(10f, 2.75f, -8.25f), 1.6f, 180);
        Model("lights-colored", new Vector3(10f, 2.45f, -8.2f), 2.8f, 180);
        Model("reindeer", new Vector3(12.6f, 0, -7.6f), 1.9f, 200);
        Model("sled-long", new Vector3(7.4f, 0, -7.6f), 1.8f, 160);

        // 가로등 (Kenney lantern) + 불빛
        foreach (var p in new[] { new Vector3(-5, 0, 9), new Vector3(5, 0, 9), new Vector3(-5, 0, -9), new Vector3(5, 0, -9), new Vector3(-2.2f, 0, -14), new Vector3(2.2f, 0, -14) })
        {
            Model("lantern", p, 1.7f, p.x < 0 ? 90 : -90);
            PointLight(p + new Vector3(0, 2.7f, 0), new Color(1f, 0.78f, 0.45f), 2.4f, 7f);
        }

        // 눈사람
        Model("snowman-hat", new Vector3(-16, 0, 0), 2.0f, 110);
        Model("snowman", new Vector3(16, 0, 1), 2.0f, 250);

        // 둘레 숲 (눈 덮인 나무 + 바위 + 눈더미)
        string[] trees = { "tree-snow-a", "tree-snow-b", "tree-snow-c" };
        for (int i = 0; i < 70; i++)
        {
            float x = R(-22f, 22f), z = R(-17f, 17f);
            bool edge = Mathf.Abs(x) > 17.5f || Mathf.Abs(z) > 12.8f;
            if (!edge) continue;
            if (z < 0 && Mathf.Abs(x) < 3.2f) continue;  // 입구 길 비움
            Model(trees[i % 3], new Vector3(x, 0, z), R(1.5f, 2.6f), R(0, 360));
        }
        for (int i = 0; i < 14; i++)
        {
            float a = R(0, Mathf.PI * 2);
            var p = new Vector3(Mathf.Cos(a) * R(14f, 18f), 0, Mathf.Sin(a) * R(10f, 13f));
            if (p.z < 0 && Mathf.Abs(p.x) < 3.2f) continue;
            Model(i % 3 == 0 ? "rocks-medium" : "snow-pile", p, R(1.2f, 2f), R(0, 360));
        }

        // 입구 아치
        Prim(PrimitiveType.Cube, new Vector3(-2f, 1.6f, -15.5f), new Vector3(0.4f, 3.2f, 0.4f), new Color(0.5f, 0.3f, 0.15f));
        Prim(PrimitiveType.Cube, new Vector3(2f, 1.6f, -15.5f), new Vector3(0.4f, 3.2f, 0.4f), new Color(0.5f, 0.3f, 0.15f));
        Prim(PrimitiveType.Cube, new Vector3(0, 3.3f, -15.5f), new Vector3(4.8f, 0.45f, 0.5f), new Color(0.14f, 0.42f, 0.23f));
        Model("wreath-decorated", new Vector3(0, 3.45f, -15.2f), 1.7f, 0);
        Model("lights-colored", new Vector3(-1.2f, 3.0f, -15.2f), 2.2f, 0);
        Model("lights-colored", new Vector3(1.2f, 3.0f, -15.2f), 2.2f, 0);

        // 하늘에서 천천히 도는 눈송이 장식
        string[] flakes = { "snowflake-a", "snowflake-b", "snowflake-c" };
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3;
            var f = Model(flakes[i % 3], new Vector3(Mathf.Cos(a) * 9f, 5.5f + (i % 2) * 1.2f, Mathf.Sin(a) * 7f), 1.6f);
            if (f) spinners.Add(f.transform);
        }

        // 내리는 눈
        for (int i = 0; i < 260; i++)
        {
            var s = Prim(PrimitiveType.Sphere, new Vector3(R(-22f, 22f), R(0f, 12f), R(-17f, 17f)), Vector3.one * R(0.05f, 0.11f), Color.white, null, false, false);
            snow.Add(s.transform);
        }
    }

    // 캐릭터 (임시: 기본 도형. 다음 단계에서 캐릭터 에셋으로 교체 예정)
    GameObject MakeCharacter(Color body, out Transform visual)
    {
        var root = new GameObject("Character");
        visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var skin = new Color(1f, 0.86f, 0.74f);
        Prim(PrimitiveType.Capsule, new Vector3(0, 0.55f, 0), new Vector3(0.78f, 0.5f, 0.72f), body, visual);
        Prim(PrimitiveType.Sphere, new Vector3(0, 0.2f, 0.18f), new Vector3(0.62f, 0.18f, 0.4f), Color.white, visual);
        Prim(PrimitiveType.Sphere, new Vector3(0, 1.38f, 0), Vector3.one * 0.74f, skin, visual);
        Prim(PrimitiveType.Sphere, new Vector3(-0.13f, 1.42f, 0.33f), Vector3.one * 0.09f, new Color(0.15f, 0.1f, 0.08f), visual);
        Prim(PrimitiveType.Sphere, new Vector3(0.13f, 1.42f, 0.33f), Vector3.one * 0.09f, new Color(0.15f, 0.1f, 0.08f), visual);
        Prim(PrimitiveType.Sphere, new Vector3(-0.22f, 1.32f, 0.29f), new Vector3(0.12f, 0.07f, 0.03f), new Color(1f, 0.6f, 0.6f), visual, false, false);
        Prim(PrimitiveType.Sphere, new Vector3(0.22f, 1.32f, 0.29f), new Vector3(0.12f, 0.07f, 0.03f), new Color(1f, 0.6f, 0.6f), visual, false, false);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 1.04f, 0), new Vector3(0.66f, 0.07f, 0.66f), new Color(0.95f, 0.95f, 0.95f), visual);
        // 산타 모자
        Prim(PrimitiveType.Cylinder, new Vector3(0, 1.68f, 0), new Vector3(0.7f, 0.07f, 0.7f), Color.white, visual);
        var hat = Prim(PrimitiveType.Capsule, new Vector3(0, 1.9f, -0.05f), new Vector3(0.5f, 0.28f, 0.5f), new Color(0.85f, 0.15f, 0.15f), visual);
        hat.transform.localRotation = Quaternion.Euler(-20, 0, 0);
        Prim(PrimitiveType.Sphere, new Vector3(0, 2.2f, -0.2f), Vector3.one * 0.16f, Color.white, visual);
        return root;
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
        switch (key)
        {
            case "e": return kb.eKey.wasPressedThisFrame;
            case "esc": return kb.escapeKey.wasPressedThisFrame;
            case "f1": return kb.f1Key.wasPressedThisFrame;
        }
        return false;
#else
        switch (key)
        {
            case "e": return Input.GetKeyDown(KeyCode.E);
            case "esc": return Input.GetKeyDown(KeyCode.Escape);
            case "f1": return Input.GetKeyDown(KeyCode.F1);
        }
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

    // ========================================================
    void Update()
    {
        float dt = Time.deltaTime;
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(dt, 0.0001f), 0.05f);
        float t = Time.time;

        for (int i = 0; i < treeLights.Count; i++)
            treeLights[i].intensity = 1.2f + Mathf.Max(0, Mathf.Sin(t * 3f + i * 1.7f)) * 1.5f;

        foreach (var s in snow)
        {
            var p = s.position;
            p.y -= dt * 0.8f;
            p.x += Mathf.Sin(t + p.z) * dt * 0.3f;
            if (p.y < 0) p.y = 12f;
            s.position = p;
        }
        foreach (var f in spinners) f.Rotate(0, 25f * dt, 0, Space.World);

        // 기차
        trainAngle += dt * 0.35f;
        for (int i = 0; i < trainCars.Count; i++)
        {
            float a = trainAngle - i * 0.33f;
            var pos = new Vector3(Mathf.Cos(a) * 4.3f, 0.05f, Mathf.Sin(a) * 4.3f);
            var tangent = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            trainCars[i].position = pos;
            trainCars[i].rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0, -90, 0);  // 모델 앞쪽이 +X
        }

        if (KeyDown("f1")) showStats = !showStats;

        if (joined)
        {
            if (openZone == null) UpdateMe(dt); else meMoving = false;
            UpdateZones();
        }
        UpdateCamera(dt);

        // 동기화 데이터 교환
        if (running)
        {
            lock (lk)
            {
                sharedMe.name = uiName;
                sharedMe.color = Palette[uiColor];
                sharedMe.x = me.position.x;
                sharedMe.z = me.position.z;
                sharedMe.ry = me.eulerAngles.y;
                sharedMe.moving = meMoving;
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
        var axis = MoveAxis();
        Vector3 mv = Vector3.zero;
        var yaw = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);
        if (axis.sqrMagnitude > 0)
        {
            moveTarget = null;
            mv = yaw * new Vector3(axis.x, 0, axis.y).normalized;
        }
        else if (ClickDown(out var mp) && !uiBlockRect.Contains(new Vector2(mp.x, Screen.height - mp.y)) && !panelRect.Contains(new Vector2(mp.x, Screen.height - mp.y)))
        {
            var ray = cam.ScreenPointToRay(mp);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)) moveTarget = ray.GetPoint(d);
        }
        if (mv == Vector3.zero && moveTarget.HasValue)
        {
            var to = moveTarget.Value - me.position; to.y = 0;
            if (to.magnitude < 0.1f) moveTarget = null; else mv = to.normalized;
        }

        meMoving = mv != Vector3.zero;
        if (meMoving)
        {
            var p = me.position + mv * Speed * dt;
            p.x = Mathf.Clamp(p.x, -19f, 19f);
            p.z = Mathf.Clamp(p.z, -15.5f, 13f);
            var flat = new Vector3(p.x, 0, p.z);
            if (flat.magnitude < TreeBlock) p = flat.normalized * TreeBlock;
            me.position = p;
            me.rotation = Quaternion.Slerp(me.rotation, Quaternion.LookRotation(mv), dt * 12f);
            walkT += dt;
        }
        meVisual.localPosition = new Vector3(0, meMoving ? Mathf.Abs(Mathf.Sin(walkT * 12f)) * 0.12f : 0, 0);
        meVisual.localRotation = Quaternion.Euler(0, 0, meMoving ? Mathf.Sin(walkT * 12f) * 4f : 0);
    }

    void UpdateZones()
    {
        nearZone = null;
        if (openZone == null)
        {
            foreach (var z in Zones)
            {
                var d = me.position - z.pos; d.y = 0;
                if (d.magnitude < z.radius) { nearZone = z; break; }
            }
            if (nearZone != null && KeyDown("e")) OpenZone(nearZone.id);
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

    void UpdateCamera(float dt)
    {
        var target = me ? me.position : Vector3.zero;
        float dist = 28f;
        var rot = Quaternion.Euler(42, 45, 0);
        if (openZone == "photo") { target = Zones[4].pos + new Vector3(0, 0.6f, 0.6f); dist = 14f; rot = Quaternion.Euler(24, 180, 0); }
        var want = target + rot * new Vector3(0, 0, -dist) + Vector3.up * 0.8f;
        float k = 1 - Mathf.Exp(-dt * 5f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, want, k);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, k);
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
            if (!remotes.TryGetValue(kv.Key, out var rv))
            {
                rv = new RemoteView();
                rv.go = MakeCharacter(HexColor(p.data.color), out rv.visual);
                rv.go.transform.position = new Vector3(p.data.x, 0, p.data.z);
                remotes[kv.Key] = rv;
            }
            rv.name = p.data.name;
            rv.zone = p.data.zone;
            var tr = rv.go.transform;
            float k = 1 - Mathf.Exp(-dt * 10f);
            tr.position = Vector3.Lerp(tr.position, new Vector3(p.data.x, 0, p.data.z), k);
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.Euler(0, p.data.ry, 0), k);
            if (p.data.moving) rv.walkT += dt;
            rv.visual.localPosition = new Vector3(0, p.data.moving ? Mathf.Abs(Mathf.Sin(rv.walkT * 12f)) * 0.12f : 0, 0);

            if (!string.IsNullOrEmpty(p.data.wish)) wishes[p.data.id] = (p.data.name, p.data.wish);
            else wishes.Remove(p.data.id);
        }
        var drop = new List<int>();
        foreach (var kv in remotes) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var key in drop) { Destroy(remotes[key].go); remotes.Remove(key); }
    }

    // 소원 오너먼트: 소원마다 트리에 반짝이는 장식 하나
    void UpdateWishes()
    {
        if (!string.IsNullOrEmpty(myWish)) wishes[myId] = (uiName, myWish); else wishes.Remove(myId);
        foreach (var kv in wishes)
        {
            if (ornaments.ContainsKey(kv.Key)) continue;
            int h = StableHash(kv.Key);
            float a = (h % 360) * Mathf.Deg2Rad;
            float y = 1.4f + (h / 360 % 100) / 100f * 4.2f;
            float r = Mathf.Lerp(2.0f, 0.5f, (y - 1.4f) / 4.6f) + 0.15f;
            var c = Color.HSVToRGB((h % 97) / 97f, 0.55f, 1f);
            var o = Prim(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Vector3.one * 0.32f, c, null, true, false);
            o.name = "Wish-" + kv.Key;
            ornaments[kv.Key] = o;
        }
        var gone = new List<string>();
        foreach (var kv in ornaments) if (!wishes.ContainsKey(kv.Key)) gone.Add(kv.Key);
        foreach (var key in gone) { Destroy(ornaments[key]); ornaments.Remove(key); }
        float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
        foreach (var o in ornaments.Values) o.transform.localScale = Vector3.one * 0.32f * pulse;
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
        PlayerPrefs.SetInt("xmas_color", uiColor);
        PlayerPrefs.Save();

        Destroy(me.gameObject);
        me = MakeCharacter(HexColor(Palette[uiColor]), out meVisual).transform;
        me.position = new Vector3(0, 0, -15.5f);
        moveTarget = new Vector3(0, 0, -10f);   // 문을 지나 걸어 들어오기

        dir = p;
        lock (lk) sharedMe = new NetState { id = myId, name = uiName, color = Palette[uiColor], x = me.position.x, z = me.position.z };
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
    // 화면 UI
    static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }

    void InitStyles()
    {
        if (sLabel != null) return;
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
        sLabel.normal.textColor = Color.white;
        sSmall = new GUIStyle(sLabel) { fontSize = 13 };
        sSmall.normal.textColor = new Color(0.75f, 0.82f, 0.95f);
        sBig = new GUIStyle(sLabel) { fontSize = 22, fontStyle = FontStyle.Bold };
        sTitle = new GUIStyle(sLabel) { fontSize = 26, fontStyle = FontStyle.Bold };
        sBox = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
        sBox.normal.background = Solid(new Color(0.08f, 0.11f, 0.2f, 0.85f));
        sPanel = new GUIStyle(sBox) { padding = new RectOffset(24, 24, 20, 20) };
        sPanel.normal.background = Solid(new Color(0.1f, 0.13f, 0.24f, 0.95f));
        sTag = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = false };
        sTag.normal.background = Solid(new Color(0.1f, 0.14f, 0.24f, 0.8f));
        sTagMe = new GUIStyle(sTag);
        sTagMe.normal.background = Solid(new Color(0.84f, 0.19f, 0.19f, 0.92f));
        sTagZone = new GUIStyle(sTag) { fontSize = 15 };
        sTagZone.normal.background = Solid(new Color(1f, 1f, 1f, 0.9f));
        sTagZone.normal.textColor = new Color(0.15f, 0.18f, 0.25f);
        sPrompt = new GUIStyle(sTag) { fontSize = 18, padding = new RectOffset(18, 18, 10, 10) };
        sPrompt.normal.background = Solid(new Color(0.84f, 0.19f, 0.19f, 0.95f));
        sWishCard = new GUIStyle(sLabel) { padding = new RectOffset(12, 12, 8, 8), margin = new RectOffset(0, 0, 4, 4) };
        sWishCard.normal.background = Solid(new Color(1f, 1f, 1f, 0.08f));
        GUI.skin.textField.fontSize = 16;
        GUI.skin.textArea.fontSize = 16;
        GUI.skin.button.fontSize = 16;
    }

    void Tag(Vector3 world, string text, GUIStyle style)
    {
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0) return;
        var size = style.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(sp.x - size.x / 2 - 8, Screen.height - sp.y - size.y / 2, size.x + 16, size.y + 4), text, style);
    }

    void OnGUI()
    {
        if (hideUI) return;
        InitStyles();
        uiBlockRect = Rect.zero;
        panelRect = Rect.zero;

        // 구역 라벨 / 이름표
        if (openZone == null)
        {
            foreach (var z in Zones)
                Tag(z.pos + Vector3.up * (z.id == "tree" ? 8.3f : 3.6f), z.label, sTagZone);
        }
        if (joined && me) Tag(me.position + Vector3.up * 2.7f, uiName + " (나)", sTagMe);
        foreach (var kv in remotes)
        {
            if (kv.Value.go == null) continue;
            var pos = kv.Value.go.transform.position;
            string label = kv.Value.name ?? "?";
            if (!string.IsNullOrEmpty(kv.Value.zone)) label += "  · " + ZoneLabel(kv.Value.zone);
            Tag(pos + Vector3.up * 2.7f, label, sTag);
            if (joined && me && openZone == null && Vector3.Distance(pos, me.position) < NearDist)
                Tag((pos + me.position) / 2 + Vector3.up * 3.4f, "가까이 있어요!", sTagMe);
        }

        if (!joined) { DrawJoinPanel(); return; }

        // 상단 상태 줄
        int peopleCount = 1 + remotes.Count;
        var top = new Rect(16, 16, 330, 40);
        GUI.Label(top, $"  {status}   ·   {peopleCount}명 접속 중", sBox);
        uiBlockRect = top;

        // 구역 안내 (E 키)
        if (openZone == null && nearZone != null)
        {
            string txt = $"[E]  {nearZone.label} 열기";
            var size = sPrompt.CalcSize(new GUIContent(txt));
            var r = new Rect(Screen.width / 2f - size.x / 2, Screen.height - 110, size.x, size.y);
            if (GUI.Button(r, txt, sPrompt)) OpenZone(nearZone.id);
            uiBlockRect = r;
        }

        if (openZone != null) DrawZonePanel();

        if (toast != null && Time.time < toastUntil)
        {
            var size = sTag.CalcSize(new GUIContent(toast));
            GUI.Label(new Rect(Screen.width / 2f - size.x / 2 - 12, 70, size.x + 24, size.y + 12), toast, sTag);
        }

        if (showStats) DrawStats();
        GUI.Label(new Rect(Screen.width - 230, Screen.height - 30, 220, 24), "<color=#9fb0c8>F1: 동기화 상태 보기</color>", sSmall);
    }

    string ZoneLabel(string id)
    {
        foreach (var z in Zones) if (z.id == id) return z.label;
        return id;
    }

    void DrawJoinPanel()
    {
        panelRect = new Rect(Screen.width / 2f - 260, Screen.height / 2f - 170, 520, 340);
        GUILayout.BeginArea(panelRect, sPanel);
        GUILayout.Label("크리스마스 광장", sTitle);
        GUILayout.Space(6);
        GUILayout.Label("공유 폴더 경로  <color=#9fb0c8>(예: \\\\서버\\공유\\xmas  또는  Z:\\xmas)</color>", sSmall);
        uiPath = GUILayout.TextField(uiPath, GUILayout.Height(30));
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름", sLabel, GUILayout.Width(44));
        uiName = GUILayout.TextField(uiName, 12, GUILayout.Width(180), GUILayout.Height(30));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label("옷 색", sLabel, GUILayout.Width(44));
        for (int i = 0; i < Palette.Length; i++)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = HexColor(Palette[i]);
            if (GUILayout.Button(i == uiColor ? "V" : "", GUILayout.Width(36), GUILayout.Height(30)))
            {
                uiColor = i;
                var pos = me.position; var rot = me.rotation;
                Destroy(me.gameObject);
                me = MakeCharacter(HexColor(Palette[uiColor]), out meVisual).transform;
                me.SetPositionAndRotation(pos, rot);
            }
            GUI.backgroundColor = old;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        if (GUILayout.Button("문으로 입장", GUILayout.Height(40))) Join();
        GUILayout.Label(status, sSmall);
        GUILayout.EndArea();
    }

    void DrawZonePanel()
    {
        Zone z = null;
        foreach (var zz in Zones) if (zz.id == openZone) z = zz;
        if (z == null) { openZone = null; return; }

        float w = Mathf.Min(560, Screen.width - 40);
        float h = openZone == "tree" ? Mathf.Min(560, Screen.height - 60) : 330;
        panelRect = openZone == "photo"
            ? new Rect(Screen.width - w - 20, Screen.height - h - 20, w, h)
            : new Rect(Screen.width / 2f - w / 2, Screen.height / 2f - h / 2, w, h);
        GUILayout.BeginArea(panelRect, sPanel);
        GUILayout.BeginHorizontal();
        GUILayout.Label(z.label, sTitle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기 (ESC)", GUILayout.Width(120), GUILayout.Height(32))) { CloseZone(); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
        GUILayout.EndHorizontal();
        GUILayout.Label(z.desc, sSmall);
        GUILayout.Space(10);

        switch (openZone)
        {
            case "tree": DrawWishTree(); break;
            case "photo":
                GUILayout.Label("카메라가 포토존을 비추고 있어요. 친구들을 불러 모아 보세요!", sLabel);
                GUILayout.Space(8);
                if (GUILayout.Button("찰칵! 사진 찍기", GUILayout.Height(44))) StartCoroutine(TakePhoto());
                break;
            default:
                GUILayout.Label("<b>여기 모인 사람</b>", sLabel);
                var here = new List<string> { uiName + " (나)" };
                foreach (var rv in remotes.Values) if (rv.zone == openZone) here.Add(rv.name);
                GUILayout.Label(string.Join(", ", here), sLabel);
                GUILayout.Space(14);
                GUILayout.Label("<color=#f5c26b>미니게임은 다음 업데이트에서 열려요. 조금만 기다려 주세요!</color>", sLabel);
                break;
        }
        GUILayout.EndArea();
    }

    void DrawWishTree()
    {
        GUILayout.Label("<b>내 소원</b>", sLabel);
        GUI.SetNextControlName("wish");
        uiWish = GUILayout.TextField(uiWish, 40, GUILayout.Height(34));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(string.IsNullOrEmpty(myWish) ? "소원 걸기" : "소원 고치기", GUILayout.Height(36)))
        {
            myWish = uiWish.Trim();
            Toast(string.IsNullOrEmpty(myWish) ? "소원을 내렸어요." : "트리에 소원을 걸었어요!");
        }
        if (!string.IsNullOrEmpty(myWish) && GUILayout.Button("내리기", GUILayout.Width(90), GUILayout.Height(36))) { myWish = ""; uiWish = ""; }
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        GUILayout.Label($"<b>트리에 걸린 소원</b>  <color=#9fb0c8>{wishes.Count}개</color>", sLabel);
        wishScroll = GUILayout.BeginScrollView(wishScroll);
        if (wishes.Count == 0) GUILayout.Label("<color=#9fb0c8>아직 걸린 소원이 없어요. 첫 번째로 걸어 보세요!</color>", sSmall);
        foreach (var kv in wishes)
            GUILayout.Label($"<b>{kv.Value.name}</b>  {kv.Value.text}", sWishCard);
        GUILayout.EndScrollView();
    }

    void DrawStats()
    {
        float wms, rms; int we, re, slot; bool conf;
        lock (lk) { wms = writeMsAvg; rms = readMsAvg; we = writeErr; re = readErr; slot = sharedSlot; conf = conflict; }
        var r = new Rect(Screen.width - 396, 16, 380, 120 + peersView.Count * 24);
        GUILayout.BeginArea(r, sBox);
        GUILayout.Label($"FPS <b>{fps:0}</b> · {SystemInfo.graphicsDeviceName}", sSmall);
        GUILayout.Label($"내 자리 player{slot:00} · 쓰기 {wms:0}ms · 읽기 {rms:0}ms · 오류 {we}/{re}" + (conf ? "  <color=#ff7777>자리 충돌!</color>" : ""), sSmall);
        GUILayout.Label("<b>다른 참가자</b>  지연 / 갱신 간격 / 오류", sSmall);
        long now = NowMs;
        foreach (var kv in peersView)
        {
            var p = kv.Value;
            if (p.data == null) continue;
            bool idle = now - p.seqAt > 5000;
            string gapCol = p.gap < 400 ? "#7ee2a8" : p.gap < 1500 ? "#f5c26b" : "#ff7777";
            GUILayout.Label($"{p.data.name}{(idle ? " (응답 없음)" : "")}   {p.lat:0}ms / <color={gapCol}>{p.gap:0}ms</color> / {p.err}", sSmall);
        }
        GUILayout.EndArea();
    }
}
