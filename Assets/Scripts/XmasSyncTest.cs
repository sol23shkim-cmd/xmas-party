// XmasSyncTest.cs — 크리스마스 광장 동기화/성능 테스트 (Unity 6 + URP)
// 사용법: Universal 3D 템플릿 프로젝트의 Assets 폴더에 이 파일 하나만 넣으면
//         Play 시 장면이 자동으로 만들어집니다. (씬에 따로 붙일 필요 없음)
//
// 확인할 것
//  1) exe가 회사 PC에서 실행되는지
//  2) 공유 폴더 동기화 지연 (오른쪽 위 표: 갱신 간격/지연)
//  3) 사무용 PC에서 FPS (왼쪽 위)

using System;
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

    // ===== UI 상태 =====
    string uiPath = "", uiName = "";
    int uiColor;
    string status = "공유 폴더 경로와 이름을 입력하고 입장하세요.";
    bool joined;

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
    Material baseMat;
    readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
    readonly List<Light> treeLights = new List<Light>();
    readonly List<Transform> snow = new List<Transform>();
    readonly Dictionary<int, RemoteView> remotes = new Dictionary<int, RemoteView>();
    Dictionary<int, PeerInfo> peersView = new Dictionary<int, PeerInfo>();
    float fps;
    GUIStyle sLabel, sBox, sTag, sTagMe, sBig;
    Rect panelRect;

    class RemoteView
    {
        public GameObject go;
        public Transform visual;
        public string name;
        public float walkT;
    }

    static readonly (string label, Vector3 pos, Color c)[] Zones =
    {
        ("틀린 그림 찾기", new Vector3(-10, 0, 6), new Color(0.45f, 0.65f, 0.95f)),
        ("지뢰찾기", new Vector3(10, 0, 6), new Color(0.95f, 0.55f, 0.45f)),
        ("선물 깨기", new Vector3(-10, 0, -7), new Color(0.6f, 0.85f, 0.5f)),
        ("포토존", new Vector3(10, 0, -7), new Color(0.95f, 0.8f, 0.4f)),
    };

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
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        if (glow && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 2.5f);
        }
        matCache[key] = m;
        return m;
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

    GameObject Cone(Vector3 pos, float radius, float height, Color c, Transform parent = null)
    {
        const int seg = 24;
        var v = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
            var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
            var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
            int b = v.Count;
            v.Add(p0); v.Add(new Vector3(0, height, 0)); v.Add(p1);   // 옆면
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            b = v.Count;
            v.Add(p0); v.Add(p1); v.Add(Vector3.zero);                  // 바닥
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }
        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var g = new GameObject("Cone");
        if (parent) g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = Mat(c);
        return g;
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

    // ===== 장면 구성 =====
    void BuildScene()
    {
        // 카메라
        cam = Camera.main;
        if (!cam)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.09f, 0.18f);
        cam.fieldOfView = 30f;
        cam.farClipPlane = 200f;
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;

        // 기존 조명 끄고 달빛 생성
        foreach (var l in FindObjectsByType<Light>()) l.enabled = false;
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.75f, 0.82f, 1f);
        moon.intensity = 0.9f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(55, -35, 0);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.33f, 0.38f, 0.55f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = cam.backgroundColor;
        RenderSettings.fogStartDistance = 35f;
        RenderSettings.fogEndDistance = 75f;

        // 후처리 (블룸 + 비네트)
        var volGo = new GameObject("XmasVolume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = prof.Add<Bloom>(true);
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(0.9f);
        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(0.3f);
        vol.profile = prof;

        // 바닥 (눈)
        Prim(PrimitiveType.Cube, new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 32), new Color(0.88f, 0.92f, 0.98f));
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.005f, 0), new Vector3(15, 0.01f, 15), new Color(0.72f, 0.70f, 0.72f), null, false, false);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(11, 0.01f, 11), new Color(0.9f, 0.93f, 0.98f), null, false, false);
        Prim(PrimitiveType.Cube, new Vector3(0, 0.005f, -11.5f), new Vector3(3, 0.01f, 9), new Color(0.72f, 0.70f, 0.72f), null, false, false);

        // 큰 트리
        var tree = new GameObject("Tree").transform;
        Prim(PrimitiveType.Cylinder, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.5f, 0.6f), new Color(0.4f, 0.26f, 0.18f), tree);
        Color[] greens = { new Color(0.10f, 0.38f, 0.20f), new Color(0.12f, 0.43f, 0.23f), new Color(0.14f, 0.48f, 0.26f), new Color(0.16f, 0.53f, 0.29f) };
        for (int i = 0; i < 4; i++) Cone(new Vector3(0, 0.9f + i * 1.15f, 0), 2.6f - i * 0.55f, 2.2f - i * 0.2f, greens[i], tree);
        Prim(PrimitiveType.Sphere, new Vector3(0, 6.1f, 0), Vector3.one * 0.55f, new Color(1f, 0.85f, 0.2f), tree, true);
        PointLight(new Vector3(0, 6.1f, 0), new Color(1f, 0.85f, 0.4f), 3f, 6f, tree);
        Color[] bulbCols = { new Color(1f, 0.35f, 0.35f), new Color(1f, 0.8f, 0.3f), new Color(0.3f, 0.75f, 1f), new Color(1f, 0.5f, 0.9f), new Color(0.3f, 1f, 0.6f) };
        for (int i = 0; i < 40; i++)
        {
            float h = 1.0f + (i / 40f) * 4.6f;
            float r = Mathf.Lerp(2.3f, 0.35f, (h - 1f) / 4.8f);
            float a = i * 2.4f;
            Prim(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r), Vector3.one * 0.16f, bulbCols[i % 5], tree, true, false);
        }
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            treeLights.Add(PointLight(new Vector3(Mathf.Cos(a) * 2.2f, 2.2f, Mathf.Sin(a) * 2.2f), bulbCols[i], 1.6f, 4.5f, tree));
        }
        Color[] giftCols = { new Color(0.9f, 0.25f, 0.25f), new Color(0.2f, 0.5f, 0.9f), new Color(0.6f, 0.3f, 0.8f), new Color(0.2f, 0.7f, 0.4f) };
        for (int i = 0; i < 6; i++)
        {
            float a = 0.5f + i * 1.05f, s = 0.45f + (i % 3) * 0.15f;
            var gift = Prim(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 2.6f, s / 2, Mathf.Sin(a) * 2.6f), Vector3.one * s, giftCols[i % 4], tree);
            gift.transform.rotation = Quaternion.Euler(0, i * 37, 0);
            Prim(PrimitiveType.Cube, Vector3.zero, new Vector3(1.02f, 1.02f, 0.18f), new Color(1f, 0.85f, 0.3f), gift.transform);
            Prim(PrimitiveType.Cube, Vector3.zero, new Vector3(0.18f, 1.02f, 1.02f), new Color(1f, 0.85f, 0.3f), gift.transform);
        }

        // 구역 표시
        foreach (var z in Zones)
        {
            Prim(PrimitiveType.Cylinder, z.pos + new Vector3(0, 0.02f, 0), new Vector3(5, 0.02f, 5), z.c * 0.9f + Color.white * 0.1f, null, false, false);
            Prim(PrimitiveType.Cube, z.pos + new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1f, 1f), new Color(0.55f, 0.35f, 0.2f));
        }

        // 가로등
        foreach (var p in new[] { new Vector3(-5, 0, 9), new Vector3(5, 0, 9), new Vector3(-5, 0, -9), new Vector3(5, 0, -9) })
        {
            Prim(PrimitiveType.Cylinder, p + new Vector3(0, 1.4f, 0), new Vector3(0.12f, 1.4f, 0.12f), new Color(0.15f, 0.15f, 0.18f));
            Prim(PrimitiveType.Sphere, p + new Vector3(0, 2.9f, 0), Vector3.one * 0.4f, new Color(1f, 0.9f, 0.6f), null, true, false);
            PointLight(p + new Vector3(0, 2.7f, 0), new Color(1f, 0.8f, 0.5f), 2.2f, 7f);
        }

        // 눈사람
        foreach (var p in new[] { new Vector3(-16, 0, 0), new Vector3(16, 0, 0) })
        {
            Prim(PrimitiveType.Sphere, p + new Vector3(0, 0.7f, 0), Vector3.one * 1.4f, Color.white);
            Prim(PrimitiveType.Sphere, p + new Vector3(0, 1.75f, 0), Vector3.one * 0.95f, Color.white);
            Prim(PrimitiveType.Cylinder, p + new Vector3(0, 2.35f, 0), new Vector3(0.6f, 0.25f, 0.6f), new Color(0.15f, 0.15f, 0.18f));
        }

        // 주변 나무
        for (int i = 0; i < 14; i++)
        {
            float x = (i % 7 - 3) * 6f + ((i * 13) % 5) * 0.4f;
            float z = i < 7 ? 14.5f : -14.5f;
            if (Mathf.Abs(x) < 2.5f && z < 0) continue;  // 입구 자리 비움
            var t = new GameObject("Pine").transform;
            t.position = new Vector3(x, 0, z);
            Prim(PrimitiveType.Cylinder, new Vector3(0, 0.3f, 0), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.4f, 0.26f, 0.18f), t);
            for (int k = 0; k < 3; k++) Cone(new Vector3(0, 0.5f + k * 0.7f, 0), 1.2f - k * 0.3f, 1.4f, greens[k], t);
        }

        // 입구 아치
        Prim(PrimitiveType.Cube, new Vector3(-1.8f, 1.4f, -15.5f), new Vector3(0.35f, 2.8f, 0.35f), new Color(0.45f, 0.27f, 0.13f));
        Prim(PrimitiveType.Cube, new Vector3(1.8f, 1.4f, -15.5f), new Vector3(0.35f, 2.8f, 0.35f), new Color(0.45f, 0.27f, 0.13f));
        Prim(PrimitiveType.Cube, new Vector3(0, 2.9f, -15.5f), new Vector3(4.2f, 0.4f, 0.45f), new Color(0.12f, 0.42f, 0.23f));
        for (int i = 0; i < 9; i++)
            Prim(PrimitiveType.Sphere, new Vector3(-1.9f + i * 0.475f, 3.15f, -15.5f), Vector3.one * 0.14f, bulbCols[i % 5], null, true, false);

        // 눈송이
        for (int i = 0; i < 250; i++)
        {
            var s = Prim(PrimitiveType.Sphere, new Vector3(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(0f, 12f), UnityEngine.Random.Range(-16f, 16f)),
                Vector3.one * UnityEngine.Random.Range(0.05f, 0.11f), Color.white, null, false, false);
            snow.Add(s.transform);
        }
    }

    GameObject MakeCharacter(Color body, out Transform visual)
    {
        var root = new GameObject("Character");
        visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var skin = new Color(1f, 0.86f, 0.74f);
        Prim(PrimitiveType.Capsule, new Vector3(0, 0.55f, 0), new Vector3(0.75f, 0.5f, 0.7f), body, visual);
        Prim(PrimitiveType.Cube, new Vector3(0, 0.55f, 0.33f), new Vector3(0.08f, 0.6f, 0.05f), Color.white, visual);
        Prim(PrimitiveType.Sphere, new Vector3(0, 1.35f, 0), Vector3.one * 0.68f, skin, visual);
        Prim(PrimitiveType.Sphere, new Vector3(-0.12f, 1.4f, 0.31f), Vector3.one * 0.08f, new Color(0.15f, 0.1f, 0.08f), visual);
        Prim(PrimitiveType.Sphere, new Vector3(0.12f, 1.4f, 0.31f), Vector3.one * 0.08f, new Color(0.15f, 0.1f, 0.08f), visual);
        Prim(PrimitiveType.Sphere, new Vector3(-0.2f, 1.3f, 0.27f), new Vector3(0.1f, 0.06f, 0.03f), new Color(1f, 0.6f, 0.6f), visual, false, false);
        Prim(PrimitiveType.Sphere, new Vector3(0.2f, 1.3f, 0.27f), new Vector3(0.1f, 0.06f, 0.03f), new Color(1f, 0.6f, 0.6f), visual, false, false);
        Prim(PrimitiveType.Cylinder, new Vector3(0, 1.03f, 0), new Vector3(0.62f, 0.06f, 0.62f), new Color(0.95f, 0.95f, 0.95f), visual);
        // 산타 모자
        Prim(PrimitiveType.Cylinder, new Vector3(0, 1.62f, 0), new Vector3(0.66f, 0.06f, 0.66f), Color.white, visual);
        var hat = Cone(new Vector3(0, 1.64f, 0), 0.3f, 0.55f, new Color(0.85f, 0.15f, 0.15f), visual);
        hat.transform.localRotation = Quaternion.Euler(-15, 0, 0);
        Prim(PrimitiveType.Sphere, new Vector3(0, 2.18f, -0.15f), Vector3.one * 0.14f, Color.white, visual);
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

        // 트리 전구 깜빡임
        for (int i = 0; i < treeLights.Count; i++)
            treeLights[i].intensity = 1.2f + Mathf.Max(0, Mathf.Sin(t * 3f + i * 1.7f)) * 1.4f;

        // 눈
        foreach (var s in snow)
        {
            var p = s.position;
            p.y -= dt * 0.8f;
            p.x += Mathf.Sin(t + p.z) * dt * 0.3f;
            if (p.y < 0) p.y = 12f;
            s.position = p;
        }

        if (joined) UpdateMe(dt);
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
                peersView = new Dictionary<int, PeerInfo>();
                foreach (var kv in sharedPeers) peersView[kv.Key] = kv.Value.Clone();
                if (sharedStatus != null) { status = sharedStatus; sharedStatus = null; }
            }
            UpdateRemotes(dt);
        }
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
        else if (ClickDown(out var mp) && !panelRect.Contains(new Vector2(mp.x, Screen.height - mp.y)))
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
            p.z = Mathf.Clamp(p.z, -15f, 15f);
            var fromTree = new Vector3(p.x, 0, p.z);
            if (fromTree.magnitude < 3f) p = fromTree.normalized * 3f;
            me.position = p;
            me.rotation = Quaternion.Slerp(me.rotation, Quaternion.LookRotation(mv), dt * 12f);
            walkT += dt;
        }
        meVisual.localPosition = new Vector3(0, meMoving ? Mathf.Abs(Mathf.Sin(walkT * 12f)) * 0.12f : 0, 0);
    }

    void UpdateCamera(float dt)
    {
        var target = me ? me.position : Vector3.zero;
        var rot = Quaternion.Euler(42, 45, 0);
        var want = target + rot * new Vector3(0, 0, -32f) + Vector3.up * 0.8f;
        cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1 - Mathf.Exp(-dt * 5f));
        cam.transform.rotation = rot;
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
            if (!remotes.TryGetValue(kv.Key, out var rv) || rv.name == null)
            {
                rv = new RemoteView();
                rv.go = MakeCharacter(HexColor(p.data.color), out rv.visual);
                rv.go.transform.position = new Vector3(p.data.x, 0, p.data.z);
                rv.name = p.data.name;
                remotes[kv.Key] = rv;
            }
            rv.name = p.data.name;
            var tr = rv.go.transform;
            float k = 1 - Mathf.Exp(-dt * 10f);
            tr.position = Vector3.Lerp(tr.position, new Vector3(p.data.x, 0, p.data.z), k);
            tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.Euler(0, p.data.ry, 0), k);
            if (p.data.moving) rv.walkT += dt;
            rv.visual.localPosition = new Vector3(0, p.data.moving ? Mathf.Abs(Mathf.Sin(rv.walkT * 12f)) * 0.12f : 0, 0);
        }
        var drop = new List<int>();
        foreach (var kv in remotes) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var k in drop) { Destroy(remotes[k].go); remotes.Remove(k); }
    }

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

        // 내 캐릭터 색 반영
        Destroy(me.gameObject);
        me = MakeCharacter(HexColor(Palette[uiColor]), out meVisual).transform;
        me.position = new Vector3(0, 0, -15f);
        moveTarget = new Vector3(0, 0, -10f);   // 문을 지나 걸어 들어오기

        dir = p;
        lock (lk)
        {
            sharedMe = new NetState { id = myId, name = uiName, color = Palette[uiColor], x = me.position.x, z = me.position.z };
        }
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
                // 교체 실패 시 직접 쓰기 (읽는 쪽은 깨진 JSON을 건너뜀)
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
        // 1.2초 간격으로 두 번 읽어 seq가 안 바뀐 슬롯 = 비었거나 버려진 슬롯
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
        lock (lk) { sharedSlot = slot; sharedStatus = $"입장 완료 - player{slot:00}.json · WASD/방향키 또는 클릭으로 이동"; }

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
                        if (!local.ContainsKey(i)) missing[i] = now;   // 아는 사람이면 교체 중 순간 누락으로 보고 유지
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
        // 퇴장 표시
        WriteSlot(slot, new NetState { id = myId, left = true });
    }

    // ========================================================
    // 화면 UI
    void InitStyles()
    {
        if (sLabel != null) return;
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
        sLabel.normal.textColor = Color.white;
        sBig = new GUIStyle(sLabel) { fontSize = 22, fontStyle = FontStyle.Bold };
        sBox = new GUIStyle(GUI.skin.box);
        var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.08f, 0.11f, 0.2f, 0.82f)); bg.Apply();
        sBox.normal.background = bg;
        sTag = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
        var tagBg = new Texture2D(1, 1); tagBg.SetPixel(0, 0, new Color(0.1f, 0.14f, 0.24f, 0.8f)); tagBg.Apply();
        sTag.normal.background = tagBg;
        sTagMe = new GUIStyle(sTag);
        var meBg = new Texture2D(1, 1); meBg.SetPixel(0, 0, new Color(0.84f, 0.19f, 0.19f, 0.9f)); meBg.Apply();
        sTagMe.normal.background = meBg;
        GUI.skin.textField.fontSize = 15;
        GUI.skin.button.fontSize = 15;
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
        InitStyles();

        // 이름표/구역 라벨
        foreach (var z in Zones) Tag(z.pos + Vector3.up * 1.6f, z.label, sTag);
        Tag(new Vector3(0, 7f, 0), "소원 트리", sTag);
        if (joined && me) Tag(me.position + Vector3.up * 2.6f, uiName + " (나)", sTagMe);
        foreach (var kv in remotes)
        {
            if (kv.Value.go == null) continue;
            var pos = kv.Value.go.transform.position;
            Tag(pos + Vector3.up * 2.6f, kv.Value.name ?? "?", sTag);
            if (joined && me && Vector3.Distance(pos, me.position) < NearDist)
                Tag((pos + me.position) / 2 + Vector3.up * 3.3f, "대화 가능!", sTagMe);
        }

        // 왼쪽 위 패널
        if (!joined)
        {
            panelRect = new Rect(16, 16, 460, 250);
            GUILayout.BeginArea(panelRect, sBox);
            GUILayout.Label("크리스마스 광장 - 동기화 테스트", sBig);
            GUILayout.Label("공유 폴더 경로 (예: \\\\서버\\공유\\xmas  또는  Z:\\xmas)", sLabel);
            uiPath = GUILayout.TextField(uiPath, GUILayout.Height(28));
            GUILayout.BeginHorizontal();
            GUILayout.Label("이름", sLabel, GUILayout.Width(40));
            uiName = GUILayout.TextField(uiName, 12, GUILayout.Width(150), GUILayout.Height(28));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("색", sLabel, GUILayout.Width(40));
            for (int i = 0; i < Palette.Length; i++)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = HexColor(Palette[i]);
                if (GUILayout.Button(i == uiColor ? "V" : "", GUILayout.Width(34), GUILayout.Height(28))) uiColor = i;
                GUI.backgroundColor = old;
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("문으로 입장", GUILayout.Height(34))) Join();
            GUILayout.Label(status, sLabel);
            GUILayout.EndArea();
        }
        else
        {
            panelRect = new Rect(16, 16, 380, 150);
            GUILayout.BeginArea(panelRect, sBox);
            float wms, rms; int we, re, slot; bool conf;
            lock (lk) { wms = writeMsAvg; rms = readMsAvg; we = writeErr; re = readErr; slot = sharedSlot; conf = conflict; }
            GUILayout.Label(status, sLabel);
            GUILayout.Label($"FPS <b>{fps:0}</b> · {SystemInfo.graphicsDeviceName}", sLabel);
            GUILayout.Label($"쓰기 1회 <b>{wms:0}ms</b> · 읽기 1바퀴 <b>{rms:0}ms</b>", sLabel);
            GUILayout.Label($"쓰기 오류 {we} · 읽기 오류 {re}" + (conf ? "  <color=#ff7777>! 자리 충돌 — 재시작 필요</color>" : ""), sLabel);
            GUILayout.EndArea();

            // 오른쪽 위: 다른 참가자 표
            var r = new Rect(Screen.width - 396, 16, 380, 60 + peersView.Count * 24);
            GUILayout.BeginArea(r, sBox);
            GUILayout.Label("<b>다른 참가자</b>   지연* / 갱신 간격 / 오류", sLabel);
            long now = NowMs;
            if (peersView.Count == 0) GUILayout.Label("<color=#9fb0c8>아직 없음</color>", sLabel);
            foreach (var kv in peersView)
            {
                var p = kv.Value;
                if (p.data == null) continue;
                bool idle = now - p.seqAt > 5000;
                string gapCol = p.gap < 400 ? "#7ee2a8" : p.gap < 1500 ? "#f5c26b" : "#ff7777";
                GUILayout.Label($"{p.data.name}{(idle ? " (응답 없음)" : "")}   {p.lat:0}ms / <color={gapCol}>{p.gap:0}ms</color> / {p.err}", sLabel);
            }
            GUILayout.EndArea();
            GUI.Label(new Rect(Screen.width - 396, r.yMax + 4, 380, 40),
                "<color=#9fb0c8>* 지연은 PC 시계가 맞아야 정확해요. 갱신 간격이 150ms 근처면 아주 좋음.</color>", sLabel);
        }
    }
}
