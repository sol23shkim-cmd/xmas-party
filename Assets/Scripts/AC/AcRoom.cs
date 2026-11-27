// AcRoom.cs - 동물의 숲풍 크리스마스 파티 (모델 미리보기 + 트리 꾸미기)
// StreamingAssets/models/catalog.json 이 있으면 이 장면이 켜져요 (예전 디오라마 대신).
//
// 흐름: 입장 화면(캐릭터 꾸미기 + 공유 폴더/이름) → 현관문으로 들어옴 → 거실에서 함께 트리 꾸미기
// 조작: WASD/방향키 이동 / 트리 가까이 가면 꾸미기 모드
//       꾸미기: 왼쪽 클릭 = 오너먼트 달기, 오른쪽 클릭 = 떼기, 1~9/휠 = 종류, Q/E = 색
//       Enter = 말하기 / Z 인사 / X 하트 / V 만세 / 친구 가까이에서 하이파이브/선물 / C 캐릭터 꾸미기
// 동기화: 공유 폴더\room\playerNN.json (위치), 공유 폴더\tree\ornaments_<ID>.json (장식) - 각자 자기 파일만 씀
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Random = UnityEngine.Random;

public partial class AcRoom : MonoBehaviour
{
    public static bool Enabled => Application.platform == RuntimePlatform.WebGLPlayer || File.Exists(Path.Combine(Application.streamingAssetsPath, "models", "catalog.json"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || FindAnyObjectByType<AcRoom>() != null) return;
        new GameObject("AcRoom").AddComponent<AcRoom>();
    }

    // ===== 방 크기 (벽 조각 1.6배 = 3.2m 단위) =====
    const float S = 1.6f, SEG = 2f * S;
    const int NX = 6, NZ = 4;                        // 트리 거실: 가로 6칸(19.2m), 세로 4칸(12.8m)
    static float HalfX => NX * SEG / 2f;
    static float HalfZ => NZ * SEG / 2f;
    static readonly Vector3 TreePos = new Vector3(0, 0, -0.9f);
    const float TreeScale = 1.2f;                     // 크고 웅장한 트리 (예전 0.82)
    static float TreeK => TreeScale / 0.82f;           // 예전 크기 대비 배율
    static float TreeR => 2.6f * TreeK;                // 트리 둘레 (부딪히는 반지름)
    static float TreeTop => 6.85f * TreeScale;
    // 저장된 장식 위치 → 지금 트리 위의 위치 (예전 장식은 옛 트리 자리·크기 기준이라 옮겨 줌)
    static Vector3 OrnWorld(Orn o) => o.v >= 2 ? TreePos + new Vector3(o.x, o.y, o.z) * TreeScale
                                                : TreePos + (new Vector3(o.x, o.y, o.z) - new Vector3(0, 0, -1.6f)) * TreeK;

    Camera cam;
    Transform world;
    AcCharacter me;
    readonly List<AcCharacter> npcs = new List<AcCharacter>();
    readonly List<Vector3> npcTargets = new List<Vector3>();
    Collider treeCol;
    Vector3 camVel;
    float yaw;

    // ===== 오너먼트 =====
    [Serializable] public class Orn { public string id, type, color, owner; public float x, y, z, ry; public double t; public int v; }   // v=2: 트리 기준 좌표(트리 크기로 나눔)
    [Serializable] class OrnFile { public string owner; public List<Orn> placed = new List<Orn>(); public List<string> removed = new List<string>(); }
    OrnFile mine = new OrnFile();
    readonly Dictionary<string, GameObject> ornObjs = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Orn> ornData = new Dictionary<string, Orn>();
    List<string> ornTypes = new List<string>();
    int ornSel, colSel;
    GameObject ghost;
    string myId, ornDir;
    float nextPoll;
    readonly Dictionary<string, DateTime> seen = new Dictionary<string, DateTime>();

    bool decorating, showCustom, showNpcs = true;
    enum Phase { Lobby, Entering, Play }
    Phase phase = Phase.Lobby;
    string uiPath = "", uiName = "", lobbyMsg = "", chatText = "";
    bool chatting; float chatOpenedAt;
    AcFirebase net;
    bool online;
    Transform doorTr; float doorOpen;
    Vector3 DoorPos => new Vector3(HalfX - SEG * 0.5f, 0, -HalfZ);
    Vector3? autoWalk;
    string myEmote = ""; double myEmoteT; string myChat = ""; double myChatT;
    class Remote { public AcCharacter c; public string look; public AcNet net; public Vector3 target; public double lastEmoteT, lastChatT; public bool nameChecked; }
    readonly Dictionary<string, Remote> remotes = new Dictionary<string, Remote>();
    readonly List<string> chatLog = new List<string>();
    bool ChatLogVisible => chatLog.Count > 0 && (Time.time - chatLogT < 9f || chatting);   // 새 말이 오면 9초 동안만 보임 (11-25)
    string toast; float toastUntil;
    GUIStyle sBox, sLabel, sSmall, sBtn;

    // ========================================================
    IEnumerator Start()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;   // 창을 내려도 동기화 계속
        IsMobile = DetectMobile();
        if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer && Screen.fullScreenMode != FullScreenMode.Windowed)
            Screen.SetResolution(Mathf.Min(1600, Display.main.systemWidth - 80), Mathf.Min(900, Display.main.systemHeight - 120), FullScreenMode.Windowed);
        foreach (var c in FindObjectsByType<Camera>()) if (c.gameObject.name != "AcCam") c.gameObject.SetActive(false);
        foreach (var l in FindObjectsByType<Light>()) l.gameObject.SetActive(false);
        foreach (var v in FindObjectsByType<Volume>()) v.enabled = false;   // 템플릿 톤매핑이 색을 바래게 해서 끔
        SetupCamera();
        InitAudio();
        if (IsMobile) QualityForMobile();

        // 웹: 모델 묶음과 목록을 내려받음 (파일을 직접 못 읽음)
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            loadingMsg = "목록 받는 중...";
            using (var r = UnityWebRequest.Get(Application.streamingAssetsPath + "/models/catalog.json"))
            {
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { loadingMsg = "불러오기 실패: " + r.error; WebLoading(-1); yield break; }
                AcCatalog.PendingText = r.downloadHandler.text;
            }
            using (var r = UnityWebRequest.Get(Application.streamingAssetsPath + "/models/models.pack"))
            {
                var op = r.SendWebRequest();
                while (!op.isDone) { loadingMsg = $"크리스마스 파티 불러오는 중... {Mathf.RoundToInt(r.downloadProgress * 100)}%"; WebLoading(r.downloadProgress); yield return null; }
                if (r.result != UnityWebRequest.Result.Success) { loadingMsg = "불러오기 실패: " + r.error; WebLoading(-1); yield break; }
                loadingMsg = "집 짓는 중...";
                yield return null;
                AcmLoader.SetPack(r.downloadHandler.data);
            }
        }
        loadingMsg = "집 짓는 중..."; WebLoading(2);
        yield return null;
        AcCatalog.Load();
        world = new GameObject("World").transform;
        SetupLights();
        BuildRoom();
        BuildHouse();
        BuildOutdoors();
        BuildFurniture();
        AttachWallDecos();
        ClearPassages();
        CollectOverviewOccluders();
        BuildNav();

        myId = PlayerPrefs.GetString("ac_id2", "");
        if (myId == "") { myId = "p" + Guid.NewGuid().ToString("N").Substring(0, 10); PlayerPrefs.SetString("ac_id2", myId); }
        mine.owner = myId;
        uiPath = PlayerPrefs.GetString("ac_room", "xmas");
        uiName = PlayerPrefs.GetString("xmas_name", "");
        var look = AcLook.Decode(PlayerPrefs.GetString("ac_look", ""));
        me = AcCharacter.Create(look, world);
        me.name = "Me";
        me.transform.position = DoorPos + new Vector3(0, 0, 1.3f);
        me.transform.rotation = Quaternion.Euler(0, 0, 0);
        yaw = 0;
        foreach (var it in AcCatalog.ByCat("ornament")) ornTypes.Add(it.name);
        ornTypes.Sort((a, b) => OrnOrder(a).CompareTo(OrnOrder(b)));
        ornTypes.Insert(0, BeadsId); ornTypes.Insert(0, TinselId);   // 두르는 줄은 맨 앞
        UpdateCamera(0, true);
        loadingMsg = null;
        yield return null; yield return null;   // 첫 화면이 그려진 다음에 로딩 영상을 걷음
        WebLoading(-1);
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void AcWeb_Loading(float p);
    static void WebLoading(float p) { AcWeb_Loading(p); }
#else
    static void WebLoading(float p) { }
#endif

    string loadingMsg = "불러오는 중...";
    bool Ready => loadingMsg == null && me != null;

    // 입장 버튼
    void Enter(bool solo)
    {
        string code = new string(uiPath.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
        if (!solo && code == "") { lobbyMsg = "방 코드를 입력하세요 (같은 코드끼리 만나요)"; return; }
        if (uiName.Trim() == "") { lobbyMsg = "이름을 입력하세요"; return; }
        if (uiName.Trim() == SantaName && !SantaAllowed) { lobbyMsg = "'산타'는 주인이 정해진 이름이라 쓸 수 없어요"; return; }
        if (CheckOldLookFirst(code, solo)) return;   // 같은 이름이면 예전 모습을 불러올지 먼저 물어봄
        uiPath = code;
        PlayerPrefs.SetString("ac_room", code); PlayerPrefs.SetString("xmas_name", uiName.Trim()); PlayerPrefs.Save();
        online = !solo;
        if (online)
        {
            net = gameObject.AddComponent<AcFirebase>();
            net.Begin(code, myId, MyNet());
        }
        else
        {
            LoadMine(); PollOrnaments(true); SpawnDemoNpcs();
            net = gameObject.AddComponent<AcFirebase>();   // 서버 없이 눈사람·편지를 이 기기 안에서만
            net.Begin("solo", myId, MyNet(), true);
        }
        joinedAt = AcSync.UnixMs;
        showCustom = false;
        GUIUtility.keyboardControl = 0;
        // 현관 문간에서 걸어 들어오기
        me.transform.position = DoorPos + new Vector3(0, 0, 0.1f);
        me.transform.rotation = Quaternion.Euler(0, 0, 0); yaw = 0;
        autoWalk = DoorPos + new Vector3(0, 0, 2.8f);
        phase = Phase.Entering;
        Toast(solo ? "혼자 둘러보기 - 트리 가까이 가서 오너먼트를 달아 보세요!" : "크리스마스 파티에 왔어요! 지도에서 가고 싶은 곳을 골라 봐요");
    }

    AcNet MyNet()
    {
        return new AcNet
        {
            name = uiName.Trim(), look = me.look.Encode(), x = me.transform.position.x, z = me.transform.position.z, ry = yaw, speed = me.speed,
            emote = myEmote, emoteT = myEmoteT, chat = myChat, chatT = myChatT, decorating = decorating, skate = me.skating, act = ActString(),
            pet = myPet != null && myPet.root && myPet.root.gameObject.activeSelf ? myPetId : "", qp = questPoints,
        };
    }

    static int OrnOrder(string n) => n.StartsWith("orn_ball") ? 0 : n == "orn_star" || n == "orn_bell" || n == "orn_drop" ? 1 : 2;

    // ========================================================
    // 카메라/빛
    void SetupCamera()
    {
        var go = new GameObject("AcCam");
        cam = go.AddComponent<Camera>();
        go.tag = "MainCamera";
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.78f, 0.92f);
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        go.AddComponent<AudioListener>();
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.82f, 0.88f, 0.95f);
        RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 110f;
    }

    void SetupLights()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.intensity = 1.0f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(52f, -28f, 0);
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null) { urp.shadowDistance = 45f; }
    }

    void PointLight(Vector3 pos, Color c, float range, float intensity)
    {
        var l = new GameObject("Glow").AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        l.transform.SetParent(world, false); l.transform.position = pos;
    }

    // ========================================================
    // 방
    // 오두막(house_cottage) 박공벽: 벽 위(y 3)와 지붕 아래 사이 오각형을 앞뒤로 막고, 앞쪽엔 둥근 다락 창
    static void CottageGable(Transform cot)
    {
        var b = new AcPB { detail = 1.2f };
        Color wall = new Color(0.93f, 0.86f, 0.73f), trim = new Color(0.98f, 0.96f, 0.92f);
        var pent = new[] { new Vector2(-2.48f, 2.95f), new Vector2(2.48f, 2.95f), new Vector2(2.48f, 3.38f), new Vector2(0f, 5.36f), new Vector2(-2.48f, 3.38f) };
        foreach (float zf in new[] { 2.17f, -2.17f })
        {
            var nrm = new Vector3(0, 0, Mathf.Sign(zf));
            int c0 = b.V(new Vector3(0, 3.8f, zf), nrm, wall, 1f);
            var idx = new int[pent.Length];
            for (int i = 0; i < pent.Length; i++) idx[i] = b.V(new Vector3(pent[i].x, pent[i].y, zf), nrm, wall, 0.92f);
            for (int i = 0; i < pent.Length; i++) { b.Tri(c0, idx[i], idx[(i + 1) % pent.Length]); b.Tri(c0, idx[(i + 1) % pent.Length], idx[i]); }
            // 나무 판자 느낌의 가로 줄
            for (int k = 0; k < 5; k++) { float y = 3.25f + k * 0.38f; float hw = Mathf.Min(2.45f, (5.36f - y) / 0.79f); if (hw > 0.15f) b.Box(new Vector3(0, y, zf + 0.012f * Mathf.Sign(zf)), new Vector3(hw * 2f, 0.025f, 0.01f), new Color(0.86f, 0.78f, 0.64f)); }
        }
        // 위아래 사이를 막는 몸통(지붕 밑 다락) — 옆에서 봐도 비지 않게
        b.Box(new Vector3(0, 3.15f, 0), new Vector3(4.96f, 0.4f, 4.34f), wall);
        var go = ProcObj("cottage_gable", b.M("cottage_gable"), cot);
        // 둥근 다락 창 (밤에 불빛)
        var wb = new AcPB { detail = 1.4f };
        wb.Torus(new Vector3(0, 4.15f, 2.2f), Vector3.forward, 0.34f, 0.05f, trim, 24, 6);
        wb.Disk(new Vector3(0, 4.15f, 2.19f), Vector3.forward, 0.32f, new Color(1f, 0.86f, 0.5f), 20, false);
        wb.Box(new Vector3(0, 4.15f, 2.2f), new Vector3(0.6f, 0.04f, 0.02f), trim);
        wb.Box(new Vector3(0, 4.15f, 2.2f), new Vector3(0.04f, 0.6f, 0.02f), trim);
        ProcObj("cottage_attic", wb.M("cottage_attic"), cot, 0.6f);
    }

    GameObject Put(string name, Vector3 pos, float rotY = 0, float scale = 1, Dictionary<string, Color> tints = null, Transform parent = null)
    {
        var go = AcCatalog.Spawn(name, parent ? parent : world, tints);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, rotY, 0);
        go.transform.localScale = Vector3.one * scale;
        int before = obstacles.Count;
        AddObstacle(name, pos, rotY, scale);
        if (obstacles.Count > before) solidObjs.Add((go, obstacles[obstacles.Count - 1]));
        if (recordPuts) builtObjs.Add((name, go));
        return go;
    }

    void HidePart(GameObject go, string part) { var t = AcmLoader.Find(go, part); if (t) t.gameObject.SetActive(false); }

    void BuildRoom()
    {
        var room = new GameObject("Room").transform; room.SetParent(world, false);
        for (int ix = 0; ix < NX; ix++)
            for (int iz = 0; iz < NZ; iz++)
                Put("floor_planks", new Vector3(-HalfX + SEG * (ix + 0.5f), 0, -HalfZ + SEG * (iz + 0.5f)), (ix + iz) % 2 * 90, S, null, room);
        // 뒷벽·전구 줄은 AcHouse에서 (벽 잘라 보기와 같이 다룸)
    }

    // 마당 크기 (집 둘레 사방)
    const float YardW = 16f, YardBack = 20f, YardFront = 16f;
    static float WorldX0 => -HouseHalf - YardW;
    static float WorldX1 => HouseHalf + YardW;
    static float WorldZ0 => HouseZ0 - YardBack;
    static float WorldZ1 => HalfZ + YardFront;

    void BuildOutdoors()
    {
        var o = new GameObject("Outdoors").transform; o.SetParent(world, false);
        // 눈 바닥: 넓고 평평한 눈판 + 집과 겹치지 않는 곳에만 울퉁불퉁한 눈 (집 바닥을 뚫고 올라오지 않게)
        {
            var plane = new GameObject("SnowBase"); plane.transform.SetParent(o, false);
            float bw = WorldX1 - WorldX0 + 60, bd = WorldZ1 - WorldZ0 + 60;
            var bm = BoxMesh(new Vector3(bw, 0.2f, bd), new Color(0.94f, 0.95f, 1f), new Color(0.85f, 0.88f, 0.96f));
            plane.AddComponent<MeshFilter>().sharedMesh = bm;
            var pr = plane.AddComponent<MeshRenderer>(); pr.sharedMaterial = AcmLoader.Mat;
            plane.transform.position = new Vector3((WorldX0 + WorldX1) / 2, -0.16f, (WorldZ0 + WorldZ1) / 2);
        }
        for (float x = WorldX0 - 4; x <= WorldX1 + 4; x += 8f)
            for (float z = WorldZ0 - 4; z <= WorldZ1 + 4; z += 8f)
            {
                bool touches = false;
                for (int sx = -2; sx <= 2 && !touches; sx++) for (int sz = -2; sz <= 2 && !touches; sz++) if (InHouseStatic(x + sx * 2.4f, z + sz * 2.4f)) touches = true;
                if (!touches) Put("ground_snow", new Vector3(x, -0.06f, z), Random.Range(0, 4) * 90, 2f, null, o);
            }

        // 바깥 띠에 나무 (집에서 9m 넘게 떨어진 곳 — 가까운 마당은 눈사람 만들 자리로 비워 둠)
        string[] trees = { "pine_snowy", "pine_tall", "pine_small", "bare_tree", "birch", "palm_cedar", "round_tree" };
        var rnd = new System.Random(5);
        int want = IsMobile ? 60 : 170, made = 0;
        for (int tries = 0; tries < want * 6 && made < want; tries++)
        {
            var p = new Vector3(Mathf.Lerp(WorldX0 - 6, WorldX1 + 6, (float)rnd.NextDouble()), 0, Mathf.Lerp(WorldZ0 - 6, WorldZ1 + 6, (float)rnd.NextDouble()));
            float gx = Mathf.Max(0, Mathf.Abs(p.x) - HouseHalf), gz = p.z < 0 ? Mathf.Max(0, HouseZ0 - p.z) : Mathf.Max(0, p.z - HalfZ);
            if (Mathf.Max(gx, gz) < 9f) continue;
            if (Mathf.Abs(p.x - DoorPos.x) < 3f && p.z < -HalfZ) continue;           // 뒷문 길
            if (Mathf.Abs(p.x - FrontDoorPos.x) < 3f && p.z > HalfZ) continue;        // 앞문 길
            Put(trees[rnd.Next(trees.Length)], p, rnd.Next(360), 0.9f + (float)rnd.NextDouble() * 0.6f, null, o);
            made++;
        }

        // 길
        for (int i = 0; i < 6; i++) Put("path_stones", new Vector3(DoorPos.x, 0.0f, -HalfZ - 1.6f - i * 2.0f), i * 37, 1.0f, null, o);
        for (int i = 0; i < 5; i++) Put("path_stones", new Vector3(FrontDoorPos.x, 0.0f, HalfZ + 1.6f + i * 2.0f), i * 53, 1.0f, null, o);
        Put("light_arch", new Vector3(DoorPos.x, 0, -HalfZ - 4.2f), 0, 1.0f, null, o);
        Put("light_arch", new Vector3(FrontDoorPos.x, 0, HalfZ + 4.2f), 0, 1.0f, null, o);
        Put("lantern_ground", new Vector3(DoorPos.x + 1.3f, 0, -HalfZ - 0.9f), 0, 1.3f, null, o);
        Put("lantern_ground", new Vector3(DoorPos.x - 1.3f, 0, -HalfZ - 0.9f), 0, 1.3f, null, o);
        Put("mailbox", new Vector3(FrontDoorPos.x + 2.4f, 0, HalfZ + 1.8f), 0, 1.2f, null, o);
        // 입구(현관문) 옆 우편함: 가까이 가면 '편지함' 버튼
        mailPos = new Vector3(HalfX - 0.5f, 0, -HalfZ + 2.9f);   // 현관문 안쪽 벽가 (문 통로 1.9m 밖: ClearPassages가 지우지 않게)
        Put("mailbox", mailPos, -90, 1.3f, new Dictionary<string, Color> { { "box", new Color(0.86f, 0.22f, 0.24f) } }, o);
        mailPos2 = new Vector3(FrontDoorPos.x + 2.4f, 0, HalfZ + 1.8f);
        Put("snowman_big", new Vector3(DoorPos.x - 3.2f, 0, -HalfZ - 3.4f), 15, 1.1f, null, o);
        Put("signpost", new Vector3(FrontDoorPos.x - 2.4f, 0, HalfZ + 2.2f), 20, 1.2f, null, o);

        // 집 둘레 조형물·자연물 (뒤·앞·양옆을 돌아가며)
        string[] props = { "lamp_post", "reindeer_light", "bench", "bush_snowy", "ice_sculpture", "bird_feeder", "lamp_post", "sled", "bush_holly",
                           "snow_pile", "rock_big", "snowball_pile", "igloo", "reindeer_light", "bench", "lamp_post", "rock_small", "ice_sculpture" };
        int pi = 0;
        for (float x = -HouseHalf + 4f; x < HouseHalf - 2f; x += 8.5f)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float baseZ = side < 0 ? (Mathf.Abs(x) < HalfX - 1.5f ? -HalfZ - 3.2f : HouseZ0 - 3.2f) : HalfZ + 3.0f;
                if (Mathf.Abs(x - (side < 0 ? DoorPos.x : FrontDoorPos.x)) < 3.5f) continue;
                if (side < 0 && Mathf.Abs(Mathf.Abs(x) - HalfX) < 2.2f) continue;
                string n = props[pi++ % props.Length];
                float z = baseZ + side * (n == "igloo" ? 3.5f : (pi % 3) * 1.2f);
                float ry = side < 0 ? 180 : 0;
                Put(n, new Vector3(x + ((pi * 7) % 5 - 2) * 0.4f, 0, z), n == "bench" || n == "lamp_post" ? ry : ry + pi * 37, n == "igloo" ? 1.2f : 1.15f, null, o);
                if (n == "lamp_post") PointLight(new Vector3(x, 2.7f, z), new Color(1f, 0.85f, 0.55f), 6f, 1.2f);
            }
        }
        foreach (int sx in new[] { -1, 1 })
        {
            float x = sx * (HouseHalf + 4.5f);
            Put("lamp_post", new Vector3(x, 0, -2f), 0, 1.2f, null, o);
            Put(sx < 0 ? "igloo" : "reindeer_light", new Vector3(x + sx * 2.5f, 0, 3.5f), sx * 90, sx < 0 ? 1.3f : 1.2f, null, o);
            Put("bush_snowy", new Vector3(x - sx * 1.5f, 0, HalfZ + 1.5f), 0, 1.2f, null, o);
            Put("bush_holly", new Vector3(x - sx * 1.5f, 0, HouseZ0 - 1.5f), 0, 1.2f, null, o);
            Put("bird_feeder", new Vector3(x + sx * 1.5f, 0, -4.5f), 0, 1.2f, null, o);
        }
        Put("frozen_pond", new Vector3(-HouseHalf * 0.55f, 0, HalfZ + 10f), 10, 1.0f, null, o);
        Put("frozen_pond", new Vector3(HouseHalf * 0.6f, 0, HouseZ0 - 9f), -15, 1.0f, null, o);
        var cot = Put("house_cottage", new Vector3(-6f, 0, HouseZ0 - 13f), 15, 1.2f, null, o);
        if (cot) CottageGable(cot.transform);   // 지붕 밑 삼각형 벽(박공)이 모델에 없어 하늘이 비쳐 보이던 것 메움
        for (int i = -3; i <= 2; i++) Put("fence", new Vector3(-6f + i * 2.1f * 1.2f, 0, HouseZ0 - 8.5f), 0, 1.2f, null, o);
        // 안뜰: 벤치·가로등·작은 나무
        Put("bench", new Vector3(-4.5f, 0, -HalfZ - 7f), 90, 1.2f, null, o);
        Put("lamp_post", new Vector3(-6.5f, 0, -HalfZ - 4f), 0, 1.2f, null, o);
        PointLight(new Vector3(-6.5f, 2.7f, -HalfZ - 4f), new Color(1f, 0.85f, 0.55f), 6f, 1.2f);
        Put("pine_snowy", new Vector3(-7.2f, 0, -HalfZ - 10.5f), 30, 0.9f, null, o);
        Put("bird_feeder", new Vector3(3.5f, 0, -HalfZ - 9.5f), 0, 1.2f, null, o);
        BuildSnow();
    }

    Vector3 FrontDoorPos => new Vector3((FrontGapX0 + FrontGapX1) / 2f, 0, HalfZ);

    void BuildFurniture()
    {
        var f = new GameObject("Furniture").transform; f.SetParent(world, false);
        // 트리
        Put("tree_skirt", TreePos, 0, 1.15f * TreeK, null, f);
        var tree = Put("tree_giant", TreePos + new Vector3(0, 0.04f, 0), 0, TreeScale, null, f);
        Put("tree_star_topper", TreePos + new Vector3(0, 6.85f * TreeScale, 0), 0, TreeScale * 1.1f, null, f);
        var leaves = AcmLoader.Find(tree, "leaves");
        if (leaves) { var mc = leaves.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = leaves.GetComponent<MeshFilter>().sharedMesh; treeCol = mc; }
        var trunk = AcmLoader.Find(tree, "trunk");
        if (trunk) { var bc = trunk.gameObject.AddComponent<CapsuleCollider>(); bc.radius = 0.6f; bc.height = 4; }
        Put("gift_pile", TreePos + new Vector3(1.6f, 0, 1.3f) * TreeK, -20, 1.2f, null, f);
        Put("gift_box", TreePos + new Vector3(-1.7f, 0, 1.4f) * TreeK, 25, 1.2f, null, f);
        Put("gift_tall", TreePos + new Vector3(-2.2f, 0, 0.6f) * TreeK, 10, 1.2f, null, f);
        Put("gift_flat", TreePos + new Vector3(0.6f, 0, 2.1f) * TreeK, 40, 1.2f, null, f);
        Put("teddy_bear", TreePos + new Vector3(-0.8f, 0, 2.2f) * TreeK, -15, 1.3f, null, f);
        Put("toy_train", TreePos + new Vector3(2.4f, 0, 0.2f) * TreeK, -60, 1.3f, null, f);
        Put("present_sack", TreePos + new Vector3(2.2f, 0, -1.4f) * TreeK, -40, 1.2f, null, f);
        Put("rug_round", TreePos + new Vector3(0, 0.005f, 0), 0, 1.6f * TreeK, null, f);

        // 벽난로 구역 (왼쪽 뒤)
        float fx = -HalfX + SEG * 0.5f + 0.4f, fz = -HalfZ + 0.55f;
        Put("fireplace", new Vector3(fx, 0, fz), 0, 1.25f, null, f);
        // 벽난로 옆 편지함 (서재 편지함과 같은 편지)
        firePos = new Vector3(fx, 0, fz + 1.6f);
        HidePart(Put("mailbox", new Vector3(fx - 1.55f, 0, fz + 1.3f), 90, 1.1f, new Dictionary<string, Color> { { "box", new Color(0.86f, 0.22f, 0.24f) } }, f), "snow");
        Put("garland", new Vector3(fx, 1.62f, fz + 0.55f), 0, 1.05f, null, f);
        for (int i = 0; i < 3; i++) Put("stocking", new Vector3(fx - 0.7f + i * 0.7f, 1.48f, fz + 0.56f), 0, 1.0f,
            new Dictionary<string, Color> { { "sock", i == 1 ? new Color(0.3f, 0.6f, 0.38f) : new Color(0.88f, 0.22f, 0.24f) } }, f);
        Put("candle_set", new Vector3(fx - 0.9f, 1.62f, fz + 0.1f), 0, 1.2f, null, f);
        Put("star_lantern", new Vector3(fx + 0.9f, 1.62f, fz + 0.1f), 0, 1.0f, null, f);
        Put("picture_frame", new Vector3(fx, 2.5f, -HalfZ + 0.12f), 0, 1.3f, null, f);
        PointLight(new Vector3(fx, 0.7f, fz + 1.0f), new Color(1f, 0.6f, 0.3f), 6f, 2.5f);
        Put("nutcracker", new Vector3(fx + 1.75f, 0, fz + 0.3f), -10, 1.2f, null, f);
        Put("rug_rect", new Vector3(fx + 0.3f, 0.004f, fz + 3.2f), 0, 1.2f, null, f);
        Put("sofa", new Vector3(fx + 0.3f, 0, fz + 4.6f), 180, 1.15f, null, f);
        Put("coffee_table", new Vector3(fx + 0.3f, 0, fz + 2.9f), 0, 1.1f, null, f);
        Put("cookie_plate", new Vector3(fx + 0.1f, 0.42f * 1.1f, fz + 2.9f), 0, 1.2f, null, f);
        Put("cocoa_mug", new Vector3(fx + 0.7f, 0.42f * 1.1f, fz + 3.0f), 30, 1.4f, null, f);
        Put("armchair", new Vector3(fx + 2.8f, 0, fz + 2.6f), -90, 1.15f, new Dictionary<string, Color> { { "fabric", new Color(0.4f, 0.6f, 0.45f) } }, f);
        Put("floor_lamp", new Vector3(fx - 1.3f, 0, fz + 4.9f), 0, 1.15f, null, f);
        PointLight(new Vector3(fx - 1.3f, 1.6f, fz + 4.9f), new Color(1f, 0.85f, 0.6f), 5f, 1.2f);
        Put("side_table", new Vector3(fx + 1.8f, 0, fz + 4.9f), 0, 1.1f, null, f);
        Put("table_lamp", new Vector3(fx + 1.8f, 0.62f * 1.1f, fz + 4.9f), 0, 1.1f, null, f);
        Put("cushion", new Vector3(fx - 1.2f, 0, fz + 2.6f), 20, 1.3f, null, f);
        Put("bookshelf", new Vector3(-HalfX + 0.45f, 0, 2.6f), 90, 1.25f, null, f);
        Put("bookshelf", new Vector3(-HalfX + 0.45f, 0, 4.4f), 90, 1.25f, new Dictionary<string, Color> { { "wood", new Color(0.62f, 0.42f, 0.28f) } }, f);
        Put("poinsettia", new Vector3(-HalfX + 0.7f, 0, -2.4f), 0, 1.3f, null, f);

        // 피아노/시계 (오른쪽 뒤)
        Put("piano", new Vector3(HalfX - SEG * 1.5f + 0.3f, 0, -HalfZ + 0.5f), 0, 1.2f, null, f);
        Put("wall_clock", new Vector3(HalfX - SEG * 1.9f, 0, -HalfZ + 0.3f), 0, 1.25f, null, f);
        Put("poinsettia", new Vector3(HalfX - 0.7f, 0, -HalfZ + 0.9f), 0, 1.3f, null, f);
        Put("snow_globe", new Vector3(HalfX - SEG * 1.5f + 0.9f, 1.4f * 1.2f, -HalfZ + 0.4f), 0, 1.2f, null, f);

        // 식탁 (오른쪽 앞)
        Vector3 dt = new Vector3(HalfX - 3.6f, 0, 2.2f);
        Put("dining_table", dt, 90, 1.15f, null, f);
        for (int i = 0; i < 2; i++)
        {
            Put("chair", dt + new Vector3(-0.85f, 0, -0.55f + i * 1.1f), 90, 1.15f, null, f);
            Put("chair", dt + new Vector3(0.85f, 0, -0.55f + i * 1.1f), -90, 1.15f, null, f);
        }
        Put("cookie_plate", dt + new Vector3(0, 0.8f * 1.15f, -0.3f), 0, 1.3f, null, f);
        Put("candle_set", dt + new Vector3(0, 0.8f * 1.15f, 0.4f), 90, 1.2f, null, f);
        PointLight(dt + new Vector3(0, 1.5f, 0.4f), new Color(1f, 0.8f, 0.5f), 4f, 0.8f);
        Put("gingerbread_man", new Vector3(HalfX - 3.4f, 0, HalfZ - 1.0f), -160, 1.1f, null, f);
        Put("gingerbread_woman", new Vector3(HalfX - 4.6f, 0, HalfZ - 0.8f), -170, 1.1f, null, f);
        Put("reindeer_plush", new Vector3(HalfX - 0.9f, 0, HalfZ - 1.4f), -110, 1.2f, null, f);
        Put("candy_cane_big", new Vector3(HalfX - 0.6f, 0, 2.6f), -90, 1.3f, null, f);
        Put("mistletoe", new Vector3(HalfX - SEG * 0.5f, 3.2f * S, -HalfZ + 1.6f), 0, 1.4f, null, f);
        Put("tv_retro", new Vector3(HalfX - 0.7f, 0, 3.8f), -90, 1.15f, null, f);
        Put("ottoman", new Vector3(HalfX - 2.2f, 0, 3.8f), 0, 1.1f, null, f);
        Put("bell_pair", new Vector3(HalfX - SEG * 0.5f, 2.55f * S, -HalfZ + 0.2f), 0, 1.3f, null, f);
    }

    void SpawnDemoNpcs()
    {
        var looks = new[]
        {
            new AcLook { species = "cat", fur = new Color(0.98f, 0.76f, 0.48f), sweater = new Color(0.3f, 0.55f, 0.4f), scarf = new Color(0.9f, 0.3f, 0.3f), hat = "beanie", hatColor = new Color(0.95f, 0.82f, 0.35f) },
            new AcLook { species = "rabbit", fur = new Color(0.98f, 0.97f, 0.96f), sweater = new Color(0.55f, 0.7f, 0.92f), scarf = new Color(0.3f, 0.5f, 0.85f), hat = "none" },
            new AcLook { species = "human", female = true, hair = "twin", face = "f3", fur = new Color(1f, 0.86f, 0.76f), sweater = new Color(0.95f, 0.55f, 0.65f), scarf = new Color(0.98f, 0.96f, 0.92f), hairColor = new Color(0.95f, 0.75f, 0.4f), hat = "earmuffs", hatColor = new Color(0.97f, 0.6f, 0.7f) },
            new AcLook { species = "fox", fur = new Color(0.95f, 0.55f, 0.28f), sweater = new Color(0.98f, 0.8f, 0.35f), scarf = new Color(0.98f, 0.96f, 0.92f), hat = "antlers", hatColor = new Color(0.62f, 0.42f, 0.26f) },
        };
        for (int i = 0; i < looks.Length; i++)
        {
            var c = AcCharacter.Create(looks[i], world);
            float a = i * 1.6f + 0.5f;
            c.transform.position = TreePos + new Vector3(Mathf.Cos(a) * (TreeR + 1.2f), 0, Mathf.Sin(a) * (TreeR + 0.6f) + 1.0f);
            npcs.Add(c); npcTargets.Add(c.transform.position);
        }
    }

    // ========================================================
    // 입력 (새 입력 시스템 / 예전 입력 둘 다)
    Vector2 MoveAxis()
    {
        float h = 0, v = 0;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            bool arrows = !decorating;   // 꾸미는 중에는 화살표를 오너먼트 고르기에 씀
            if (kb.aKey.isPressed || (arrows && kb.leftArrowKey.isPressed)) h -= 1;
            if (kb.dKey.isPressed || (arrows && kb.rightArrowKey.isPressed)) h += 1;
            if (kb.sKey.isPressed || (arrows && kb.downArrowKey.isPressed)) v -= 1;
            if (kb.wKey.isPressed || (arrows && kb.upArrowKey.isPressed)) v += 1;
        }
#else
        bool arrows = !decorating;
        if (Input.GetKey(KeyCode.A) || (arrows && Input.GetKey(KeyCode.LeftArrow))) h -= 1;
        if (Input.GetKey(KeyCode.D) || (arrows && Input.GetKey(KeyCode.RightArrow))) h += 1;
        if (Input.GetKey(KeyCode.S) || (arrows && Input.GetKey(KeyCode.DownArrow))) v -= 1;
        if (Input.GetKey(KeyCode.W) || (arrows && Input.GetKey(KeyCode.UpArrow))) v += 1;
#endif
        return new Vector2(h, v);
    }

    bool Key(string k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; if (kb == null) return false;
        switch (k)
        {
            case "q": return kb.qKey.wasPressedThisFrame; case "e": return kb.eKey.wasPressedThisFrame;
            case "c": return kb.cKey.wasPressedThisFrame; case "f2": return kb.f2Key.wasPressedThisFrame;
            case "esc": return kb.escapeKey.wasPressedThisFrame;
            case "f11": return kb.f11Key.wasPressedThisFrame;
            case "z": return kb.zKey.wasPressedThisFrame; case "x": return kb.xKey.wasPressedThisFrame; case "v": return kb.vKey.wasPressedThisFrame;
            case "enter": return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            case "left": return kb.leftArrowKey.wasPressedThisFrame; case "right": return kb.rightArrowKey.wasPressedThisFrame;
            case "up": return kb.upArrowKey.wasPressedThisFrame; case "down": return kb.downArrowKey.wasPressedThisFrame;
            case "space": return kb.spaceKey.wasPressedThisFrame; case "j": return kb.jKey.wasPressedThisFrame; case "k": return kb.kKey.wasPressedThisFrame;
        }
        if (k.Length == 1 && k[0] >= '1' && k[0] <= '9')
        {
            var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
            return digits[k[0] - '1'].wasPressedThisFrame;
        }
        return false;
#else
        switch (k)
        {
            case "q": return Input.GetKeyDown(KeyCode.Q); case "e": return Input.GetKeyDown(KeyCode.E);
            case "c": return Input.GetKeyDown(KeyCode.C); case "f2": return Input.GetKeyDown(KeyCode.F2);
            case "esc": return Input.GetKeyDown(KeyCode.Escape);
            case "f11": return Input.GetKeyDown(KeyCode.F11);
            case "z": return Input.GetKeyDown(KeyCode.Z); case "x": return Input.GetKeyDown(KeyCode.X); case "v": return Input.GetKeyDown(KeyCode.V);
            case "enter": return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            case "left": return Input.GetKeyDown(KeyCode.LeftArrow); case "right": return Input.GetKeyDown(KeyCode.RightArrow);
            case "up": return Input.GetKeyDown(KeyCode.UpArrow); case "down": return Input.GetKeyDown(KeyCode.DownArrow);
            case "space": return Input.GetKeyDown(KeyCode.Space); case "j": return Input.GetKeyDown(KeyCode.J); case "k": return Input.GetKeyDown(KeyCode.K);
        }
        if (k.Length == 1 && k[0] >= '1' && k[0] <= '9') return Input.GetKeyDown(KeyCode.Alpha1 + (k[0] - '1'));
        return false;
#endif
    }

    Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
    bool MouseDown(int b)
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current; if (m == null) return false;
        return b == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(b);
#endif
    }
    float Scroll()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    // ========================================================
    void Update()
    {
        if (!Ready) return;
        float dt = Time.deltaTime;
        UpdatePointer();
        // 문 여닫기
        float wantOpen = phase == Phase.Entering || Vector3.Distance(me.transform.position, DoorPos) < 2.3f ? 1 : 0;
        foreach (var r in remotes.Values) if (Vector3.Distance(r.c.transform.position, DoorPos) < 2.2f) wantOpen = 1;
        doorOpen = Mathf.MoveTowards(doorOpen, wantOpen, dt * 1.8f);
        if (doorTr) doorTr.localRotation = Quaternion.Euler(0, -Mathf.SmoothStep(0, 1, doorOpen) * 95f, 0);

        if (phase == Phase.Lobby)
        {
            CullRooms();
            me.speed = 0;
            UpdateCamera(dt, false);
            return;
        }
        if (phase == Phase.Entering && autoWalk.HasValue)
        {
            var d = autoWalk.Value - me.transform.position; d.y = 0;
            if (d.magnitude < 0.08f) { autoWalk = null; phase = Phase.Play; }
            else { me.transform.position += d.normalized * Mathf.Min(2.4f * dt, d.magnitude); me.speed = 2.4f; }
        }
        else if (!showCustom && !chatting && !building && !lettersOpen && !askOpen && !decorating && !playingGame && !showMap && !treeOverview && !pianoOpen && !quizOpen && snowFocusId == null && !candyOpen && !GreenBusy && !GiftBusy && waterSlot < 0 && !kitchenOpen) { UpdateFollow(); MovePlayer(dt); TapMove(dt); }
        else me.speed = 0;

        UpdateNpcs(dt);
        UpdateRemotes(dt);
        UpdateCamera(dt, false);
        OccluderUpdate();
        OutdoorTone(dt);

        if (!chatting && !askOpen && !lettersOpen && !building && !playingGame && !showMap && !GiftBusy && !GreenBusy && !kitchenOpen)
        {
            if (Key("c") && !decorating) showCustom = !showCustom;
            if (Key("f2")) { showNpcs = !showNpcs; foreach (var n in npcs) n.gameObject.SetActive(showNpcs); }
            if (Key("esc")) showCustom = false;
            if (Key("z")) Emote("wave");
            if (Key("x")) Emote("heart");
            if (Key("v")) Emote("cheer");
            if (Key("enter")) OpenChat();
        }

        // 트리 꾸미기는 버튼으로 들어가고 '나오기'로 나옴 (멀어지면 저절로 나옴)
        if (decorating && (phase != Phase.Play || TreeDist > TreeR + 6f || Outside || showCustom || building || lettersOpen)) decorating = false;
        if (decorating && !chatting && !askOpen && Key("esc")) decorating = false;
        if (treeOverview && (Key("esc") || phase != Phase.Play || playingGame)) treeOverview = false;
        if (treeOverview) OverviewInput();
        UpdateWalls(dt);
        if (decorating && !chatting && !askOpen && !treeOverview) Decorate(); else { if (ghost) ghost.SetActive(false); if (focusRing) FocusAt(Vector3.zero, false); pointerFocus = null; }
        if (building) BuildingUpdate();
        GreenUpdate(dt);
        TreeShotUpdate();
        if (me) me.SetHidden(plantFocus >= 0 || plantPicker >= 0 || building || snowFocusId != null);
        QuestUpdate(dt);
        PetsUpdate(dt);
        if (me) me.SetSanta(IsSantaMe);   // 산타: 전용 모습
        foreach (var rr in remotes.Values) if (rr.c && rr.net != null) rr.c.SetSanta((rr.net.name ?? "").Trim() == SantaName);
        if (IsSantaMe && phase == Phase.Play && me && Outside && !me.skating && !playingGame && !building) { me.SetSkates(true); skateVel = Vector3.zero; }   // 산타는 밖에 나가면 늘 썰매
        UpdateCloseupQuality();
        GiftUpdate(dt);
        KitchenUpdate(dt);
        FxUpdate(dt);
        ProcessSnowIcons();
        if (playingGame) CookieGameUpdate(dt);
        PlayUpdate(dt);
        UpdateAudio(dt);
        if (phase == Phase.Play) { PollSnowmen(); PollNotes(); PollLetters(); UpdateMember(); }

        if (Time.time > nextPoll) { nextPoll = Time.time + (online ? 0.5f : 2f); PollOrnaments(false); }
        CheckArea();
        if (Key("f11")) Screen.fullScreenMode = Screen.fullScreenMode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (net != null) net.SetMe(MyNet());
    }

    void Emote(string kind, string target = "")
    {
        myEmote = target == "" ? kind : kind + ":" + target;
        myEmoteT = AcSync.UnixMs;
        me.PlayEmote(kind);
    }

    void Say(string text)
    {
        text = text.Trim(); if (text == "") return;
        if (text.Length > 60) text = text.Substring(0, 60);
        myChat = text; myChatT = AcSync.UnixMs;
        Log(uiName + ": " + text);
    }

    void Log(string line) { chatLog.Add(line); if (chatLog.Count > 6) chatLog.RemoveAt(0); chatLogT = Time.time; }
    float chatLogT = -99;
    readonly Dictionary<string, float> recentLeft = new Dictionary<string, float>();   // 이름 → 나간 시각 (금방 다시 들어오면 알림 안 띄움)

    // 다른 참가자
    float remotesSince = -1;
    void UpdateRemotes(float dt)
    {
        if (net == null) return;
        var peers = net.Peers();
        if (remotesSince < 0 && net.ServerNow > 0) remotesSince = Time.time;
        bool quiet = remotesSince < 0 || Time.time - remotesSince < 4f;   // 처음 들어왔을 때 이미 있던 사람은 알리지 않음
        var alive = new HashSet<string>();
        foreach (var n in peers)
        {
            if (string.IsNullOrEmpty(n.id)) continue;
            alive.Add(n.id);
            if (!remotes.TryGetValue(n.id, out var r))
            {
                r = new Remote { c = AcCharacter.Create(AcLook.Decode(n.look), world), look = n.look, target = new Vector3(n.x, 0, n.z),
                                 lastEmoteT = n.emoteT, lastChatT = n.chatT };
                r.c.transform.position = r.target; r.c.name = "Peer " + n.name;
                remotes[n.id] = r;
                // 들어옴 알림: 잠깐 끊겼다 다시 들어온 경우(5분 안)는 조용히. 대화 기록에는 넣지 않음
                if (!quiet && !(recentLeft.TryGetValue(n.name ?? "", out var lt) && Time.time - lt < 300f)) Toast(n.name + " 님이 들어왔어요");
                if (npcs.Count > 0) foreach (var c in npcs) c.gameObject.SetActive(false);
            }
            if (r.look != n.look) { r.look = n.look; r.c.Rebuild(AcLook.Decode(n.look)); }
            if (!r.nameChecked && !string.IsNullOrEmpty(n.name)) { r.nameChecked = true; if (NameKey(n.name) == myKey) Toast("나와 같은 이름(" + n.name + ")으로 들어온 사람이 있어요. 편지/점수가 섞일 수 있어요"); }
            r.net = n; r.target = new Vector3(n.x, 0, n.z);
            if (r.c.skating != n.skate) r.c.SetSkates(n.skate);
            if (n.emoteT > r.lastEmoteT && AcSync.UnixMs - n.emoteT < 5000)
            {
                r.lastEmoteT = n.emoteT;
                var parts = n.emote.Split(':');
                r.c.PlayEmote(parts[0]);
                if (parts.Length > 1 && parts[1] == myId)
                {
                    if (parts[0] == "highfive") { me.PlayEmote("highfive"); Toast(n.name + " 님과 하이파이브!"); }
                    if (parts[0] == "gift") { me.PlayEmote("heart"); Toast(n.name + " 님이 선물을 줬어요!"); }
                }
            }
            if (n.chatT > r.lastChatT) { r.lastChatT = n.chatT; Log(n.name + ": " + n.chat); }
        }
        var gone = new List<string>();
        foreach (var kv in remotes) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone) { recentLeft[remotes[id].net?.name ?? ""] = Time.time; Destroy(remotes[id].c.gameObject); remotes.Remove(id); }
        foreach (var r in remotes.Values)
        {
            var p = r.c.transform.position;
            var d = r.target - p; d.y = 0;
            float dist = d.magnitude;
            if (dist > 6f) r.c.transform.position = r.target;   // 너무 멀면 순간이동
            else r.c.transform.position = Vector3.MoveTowards(p, r.target, Mathf.Max(dist * 6f, 1.2f) * dt);
            r.c.speed = Mathf.Lerp(r.c.speed, dist > 0.05f ? Mathf.Max(r.net.speed, 1.5f) : 0, 1 - Mathf.Exp(-dt * 10f));
            r.c.transform.rotation = Quaternion.Slerp(r.c.transform.rotation, Quaternion.Euler(0, r.net.ry, 0), 1 - Mathf.Exp(-dt * 12f));
        }
    }

    Remote NearestRemote(float within)
    {
        Remote best = null; float bd = within;
        foreach (var r in remotes.Values)
        {
            float d = Vector3.Distance(r.c.transform.position, me.transform.position);
            if (d < bd) { bd = d; best = r; }
        }
        return best;
    }


    float camYawCur = 180f;
    Vector3 skateVel;

    void MovePlayer(float dt)
    {
        var a = MoveAxis();
        // 카메라 방향 기준 (실내: 카메라가 -z를 봄, 실외: +z를 봄)
        Vector3 dir = Quaternion.Euler(0, Mathf.Round(camYawCur / 180f) * 180f, 0) * new Vector3(a.x, 0, a.y);
        if (dir.sqrMagnitude > 1) dir.Normalize();
        float spd = 4.4f;
        var prev = me.transform.position;
        var p = prev + dir * spd * dt;
        if (me.skating)
        {
            // 스케이트: 밀면 빨라지고, 손을 떼도 쭉 미끄러짐
            if (walkTarget.HasValue && dir.sqrMagnitude < 0.01f) { var wd = walkTarget.Value - prev; wd.y = 0; if (wd.magnitude > 0.4f) dir = wd.normalized; else walkTarget = null; }
            float sk = IsSantaMe ? 1.6f : 1f;   // 산타 썰매: 스케이트보다 1.6배
            if (dir.sqrMagnitude > 0.01f) skateVel += dir * 10f * sk * dt; else skateVel = Vector3.MoveTowards(skateVel, Vector3.zero, 1.6f * dt);
            if (skateVel.magnitude > 9f * sk) skateVel = skateVel.normalized * 9f * sk;
            p = prev + skateVel * dt;
            if (dir.sqrMagnitude < 0.01f && skateVel.sqrMagnitude > 0.05f) dir = skateVel.normalized * 0.001f;
        }
        bool ghostWalk = IsSantaMe;   // 산타는 벽·가구를 지나 어느 방이든 들어감
        if (!ghostWalk) { p = Collide(p, 0.36f); p = ClampRegion(prev, p); } else { p.x = Mathf.Clamp(p.x, WorldX0, WorldX1); p.z = Mathf.Clamp(p.z, WorldZ0, WorldZ1); }
        var d2 = new Vector2(p.x - TreePos.x, p.z - TreePos.z);
        if (d2.magnitude < TreeR) { d2 = d2.normalized * TreeR; p.x = TreePos.x + d2.x; p.z = TreePos.z + d2.y; }
        foreach (var r in remotes.Values)
        {
            var dd = new Vector2(p.x - r.c.transform.position.x, p.z - r.c.transform.position.z);
            if (dd.magnitude < 0.75f && dd.magnitude > 0.001f) { dd = dd.normalized * 0.75f; p.x = r.c.transform.position.x + dd.x; p.z = r.c.transform.position.z + dd.y; }
        }
        if (!ghostWalk) p = ClampRegion(prev, p);
        var moved = p - prev; moved.y = 0;
        if (me.skating)
        {
            if (dt > 0) skateVel = Vector3.Lerp(skateVel, moved / dt, 0.5f);   // 벽에 부딪히면 속도도 줄어듦
            me.speed = dt > 0 ? moved.magnitude / dt : 0;
            if (moved.sqrMagnitude > 1e-6f) yaw = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
            if (InHouse(p)) { me.SetSkates(false); skateVel = Vector3.zero; Toast(IsSantaMe ? "썰매는 문 앞에 세워 두고 들어가요" : "집 안에서는 스케이트를 벗어요"); }
            me.transform.position = p;
            me.transform.rotation = Quaternion.Slerp(me.transform.rotation, Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-dt * 8f));
            return;
        }
        me.speed = dt > 0 ? Mathf.Min(moved.magnitude / dt, spd) : 0;
        if (dir.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        me.transform.position = p;
        me.transform.rotation = Quaternion.Slerp(me.transform.rotation, Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-dt * 14f));
    }

    void UpdateNpcs(float dt)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            var c = npcs[i]; if (!c.gameObject.activeSelf) continue;
            var p = c.transform.position; var tgt = npcTargets[i];
            var d = tgt - p; d.y = 0;
            if (d.magnitude < 0.1f)
            {
                c.speed = 0;
                if (Random.value < dt * 0.3f)
                {
                    float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(TreeR + 0.8f, TreeR + 3.5f);
                    npcTargets[i] = new Vector3(Mathf.Clamp(TreePos.x + Mathf.Cos(a) * r, -HalfX + 1, HalfX - 1), 0, Mathf.Clamp(TreePos.z + Mathf.Sin(a) * r, -HalfZ + 1.5f, HalfZ - 1));
                }
                else
                {
                    var look = TreePos - p; look.y = 0;
                    c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(look), 1 - Mathf.Exp(-dt * 3));
                }
                continue;
            }
            var step = d.normalized * Mathf.Min(1.6f * dt, d.magnitude);
            c.transform.position = Collide(p + step, 0.36f); c.speed = 1.6f;
            c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-dt * 8));
        }
    }

    Vector3? pointerFocus;

    bool orbitOn; Vector3 orbFocus; float orbDist, orbPitch, skateZoom = 1f;
    void UpdateCamera(float dt, bool snap)
    {
        Vector3 focus = me.transform.position + new Vector3(0, 1.0f, 0);
        if (GiftCamera(dt, snap)) { orbitOn = false; return; }
        if (KitchenCamera(dt, snap)) { orbitOn = false; return; }   // 주방 게임: 위에서 내려다보기   // 선물 포장·풀어보기: 따로 떨어진 무대
        float dist = Screen.height > Screen.width ? 18f : 14f, pitch = 36f, targetYaw = 180f;   // 실내·앞마당·옆마당: 남쪽(+z)에서, 뒷마당: 집 반대편에서 / 세로 화면은 좌우가 좁아서 더 멀리
        if (SnowCamera(ref focus, ref dist, ref pitch)) { targetYaw = snowYaw; }
        else if (SnowFocusCamera(ref focus, ref dist, ref pitch, ref targetYaw)) { }
        else if (PlantCamera(ref focus, ref dist, ref pitch, ref targetYaw)) { }
        else if (treeOverview)
        {
            // 트리 전경: 꼭대기 별까지 한눈에 (끌어서 돌려 봄)
            float vfov = cam.fieldOfView * Mathf.Deg2Rad;
            float need = Mathf.Max((TreeTop + 1.2f) / 0.78f, (TreeR * 2.4f) / Mathf.Max(cam.aspect, 0.3f));
            dist = need / (2f * Mathf.Tan(vfov / 2f)) * decoZoom + TreeR;
            focus = TreePos + Vector3.up * (TreeTop * 0.45f);
            pitch = 6f; targetYaw = decoYaw;
        }
        else if (decorating)
        {
            // 가까이서 트리를 보며 위아래로 스크롤, 좌우로 돌기
            focus = TreePos + Vector3.up * decoH;
            bool portrait = Screen.height > Screen.width;
            dist = TreeR + (portrait ? 7.5f : 5.2f) * decoZoom;
            pitch = 8f; targetYaw = decoYaw;
            focus -= Vector3.up * 0.9f;   // 아래 패널을 피해서 살짝 위쪽이 보이게
        }
        if (PetGallery && phase == Phase.Play && !decorating && !treeOverview) { focus = me.transform.position + new Vector3(0, 0.25f, 1.8f); dist = 5.2f; pitch = 24f; targetYaw = 180f; }   // 펫 미리보기: 가까이
        // 스케이트를 타면 조금 멀리서 (빨리 다니니까 넓게 보이게)
        bool special = building || snowFocusId != null || treeOverview || decorating || GreenBusy;
        skateZoom = Mathf.MoveTowards(skateZoom, !special && me != null && (me.skating || snowMode) ? 1.45f : 1f, dt * 0.9f);   // 스케이트·눈싸움: 넓게
        if (!special) dist *= skateZoom;
        if (phase == Phase.Lobby)
        {
            // 입장 화면: 캐릭터를 앞에서 가까이 (뒤로 현관문이 보임). 왼쪽 패널을 피해 캐릭터를 오른쪽에
            if (Screen.height > Screen.width) { focus = me.transform.position + new Vector3(0, -0.45f, 0); dist = 7.5f; }   // 세로 화면: 캐릭터를 위쪽에, 아래는 패널
            else { focus = me.transform.position + new Vector3(0.6f, 0.72f, 0); dist = 4.0f; }
            pitch = 8f; targetYaw = 180f;
            me.transform.rotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 0.6f) * 35f, 0);
        }
        // 트리를 볼 때는 카메라와 트리 사이 기둥·벽을 잘라냄 (가까운 면을 트리 바로 앞까지 당김)
        bool treeView = decorating || treeOverview;
        float wantNear = GreenBusy ? Mathf.Max(0.3f, dist - 1.1f) : building || snowFocusId != null ? Mathf.Max(0.3f, dist - 1.6f) : treeOverview ? 0.3f : treeView ? Mathf.Max(0.3f, dist - TreeR - 0.9f) : phase == Phase.Lobby ? 0.3f : Mathf.Min(2.0f, dist * 0.15f);   // 바로 앞을 지나는 눈송이는 안 그림
        cam.nearClipPlane = snap ? wantNear : Mathf.Lerp(cam.nearClipPlane, wantNear, 1 - Mathf.Exp(-dt * 4f));
        cam.farClipPlane = 200f;
        camYawCur = snap ? targetYaw : Mathf.LerpAngle(camYawCur, targetYaw, 1 - Mathf.Exp(-dt * 3f));
        var rotq = Quaternion.Euler(pitch, camYawCur, 0);
        var want = focus + rotq * new Vector3(0, 0, -dist);
        if (snap) { cam.transform.position = want; cam.transform.rotation = rotq; return; }
        // 트리·눈사람을 볼 때: 그 중심축을 기준으로 궤도처럼 돌기 (항상 화면 가운데)
        if (treeView || building || snowFocusId != null || GreenBusy)
        {
            if (!orbitOn)
            {
                orbitOn = true;
                var f = cam.transform.forward; float d0 = Vector3.Distance(cam.transform.position, focus);
                orbFocus = cam.transform.position + f * d0; orbDist = d0;
                camYawCur = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; orbPitch = Mathf.Asin(Mathf.Clamp(-f.y, -1, 1)) * Mathf.Rad2Deg;
            }
            float k = 1 - Mathf.Exp(-dt * 5f);
            orbFocus = Vector3.Lerp(orbFocus, focus, k); orbDist = Mathf.Lerp(orbDist, dist, k); orbPitch = Mathf.LerpAngle(orbPitch, pitch, k);
            var oq = Quaternion.Euler(orbPitch, camYawCur, 0);
            cam.transform.position = orbFocus + oq * new Vector3(0, 0, -orbDist); cam.transform.rotation = oq; camVel = Vector3.zero;
            return;
        }
        orbitOn = false;
        float smooth = phase == Phase.Entering ? 0.6f : 0.25f;
        cam.transform.position = Vector3.SmoothDamp(cam.transform.position, want, ref camVel, smooth);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rotq, 1 - Mathf.Exp(-dt * (phase == Phase.Entering ? 2.5f : 6f)));
    }

    // ========================================================
    // 트리 꾸미기
    Color CurColor => AcPalette.Vivid[Wrap(colSel, AcPalette.Vivid.Length)];

    void Decorate()
    {
        if (ornTypes.Count == 0 || treeCol == null) return;
        RenderIcons(CurColor);
        DecoCameraInput();
        for (int i = 1; i <= 9; i++) if (Key(i.ToString())) SetSel(i - 1);
        if (Key("up")) SetSel(Wrap(ornSel - 1, ornTypes.Count));
        if (Key("down")) SetSel(Wrap(ornSel + 1, ornTypes.Count));
        if (Key("left") || Key("q")) { colSel = Wrap(colSel - 1, AcPalette.Vivid.Length); RefreshGhost(); }
        if (Key("right") || Key("e")) { colSel = Wrap(colSel + 1, AcPalette.Vivid.Length); RefreshGhost(); }

        pointerFocus = null;
        var mp = ptrPos;
        if (ptrDown) { longUsed = false; if (!pinching) pinchUsed = false; decoPressTree = !OverUI(ptrStart) && treeCol.Raycast(cam.ScreenPointToRay(ptrStart), out _, 100f); }
        // 아이콘을 눌러 끌기 시작
        if (ptrDown && !pinching)
        {
            var u = new Vector2(ptrStart.x / UiK, (Screen.height - ptrStart.y) / UiK);
            foreach (var (rr, idx) in decoIconRects) if (rr.Contains(u)) dragCand = idx;
        }
        if (dragCand >= 0 && (ptrPressed || ptrUp) && !OverUI(mp) && !dragOrn && !pinching) { dragOrn = true; SetSel(dragCand); ornPicked = true; }
        if (dragOrn && ptrUp) { DropOrnAt(mp); dragOrn = false; dragCand = -1; return; }   // 끌어 온 장식: 손을 떼자마자 달기
        bool dropNow = false;
        if (!ptrPressed && !ptrUp && !ptrDown) { dragCand = -1; dragOrn = false; }
        if (OverUI(mp)) { if (ghost) ghost.SetActive(false); FocusAt(Vector3.zero, false); if (dropNow) dragOrn = false; return; }   // 화면 UI 위
        var ray = cam.ScreenPointToRay(mp);
        // 떼기: 오너먼트를 오른쪽 클릭
        bool removeGesture = MouseDown(1) || (LongPress && !longUsed);   // 오른쪽 클릭 또는 길게 누르기
        if (removeGesture && Physics.Raycast(ray, out var hitO, 100f) && hitO.collider.GetComponentInParent<AcOrnTag>() is AcOrnTag tag)
        {
            longUsed = true; ptrMoved = true;   // 손을 뗄 때 다시 달리지 않게
            RemoveOrn(tag.id); Toast("장식을 뗐어요"); return;
        }
        if (!ornPicked) { if (ghost) ghost.SetActive(false); FocusAt(Vector3.zero, false); return; }   // 고른 장식이 없으면 둘러보기만
        // 고른 장식이 있으면: 트리를 누른 채 움직여 자리를 잡고, 손을 떼면 그 자리에 달기 (트리를 끌어도 돌지 않음)
        bool place = (ptrUp && !ptrOnUI && decoPressTree && !pinching && !pinchUsed && !longUsed) || dropNow;
        if (dropNow) dragOrn = false;
        if (treeCol.Raycast(ray, out var hit, 100f))
        {
            if (IsGarland(ornTypes[ornSel]))
            {
                float gy = hit.point.y + 0.08f;
                GhostGarland(gy);
                pointerFocus = hit.point;
                FocusAt(hit.point, true);
                if (place) AddOrn(ornTypes[ornSel], CurColor, new Vector3(TreePos.x, gy, TreePos.z), 0);
                return;
            }
            if (ghost == null || ghost.name != ornTypes[ornSel]) RefreshGhost();
            ghost.SetActive(true);
            OrnPlacement(hit, out var pos, out var ry);
            ghost.transform.position = pos;
            ghost.transform.rotation = Quaternion.Euler(0, ry, 0);
            var body = pos - Vector3.up * 0.11f * OrnScale;
            pointerFocus = body;
            FocusAt(body, true);
            if (place) AddOrn(ornTypes[ornSel], CurColor, pos, ry);
        }
        else { if (ghost) ghost.SetActive(false); FocusAt(Vector3.zero, false); }
    }

    // 장식 아이콘을 끌어다 놓은 곳에 달기
    void DropOrnAt(Vector2 screen)
    {
        if (!decorating || treeCol == null || ornTypes.Count == 0 || OverUI(screen)) return;
        var ray = cam.ScreenPointToRay(screen);
        if (!treeCol.Raycast(ray, out var hit, 100f)) return;
        if (IsGarland(ornTypes[ornSel])) { AddOrn(ornTypes[ornSel], CurColor, new Vector3(TreePos.x, hit.point.y + 0.08f, TreePos.z), 0); return; }
        OrnPlacement(hit, out var pos, out var ry);
        AddOrn(ornTypes[ornSel], CurColor, pos, ry);
    }

    // ===== 꾸미기 카메라: 끌어서 위아래 스크롤·좌우 돌기, 휠·WASD도 =====
    float decoYaw = 180f, decoH = 2f; bool treeOverview, playingGame;
    Vector2 lastPtr;
    // 두 손가락 벌리기·모으기로 확대·축소 (트리 꾸미기·트리 전경)
    bool pinching; float pinchD, pinchZ; float decoZoom = 1f;
    bool PinchZoom()
    {
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current; int cnt = 0; Vector2 a = default, b = default;
        if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) { if (cnt == 0) a = t.position.ReadValue(); else if (cnt == 1) b = t.position.ReadValue(); cnt++; }
        if (cnt >= 2)
        {
            float d = (a - b).magnitude;
            if (!pinching) { pinching = true; pinchD = Mathf.Max(20, d); pinchZ = decoZoom; }
            else decoZoom = Mathf.Clamp(pinchZ * pinchD / Mathf.Max(20, d), 0.5f, 3.5f);
            ptrMoved = true; lastPtr = ptrPos; dragCand = -1; pinchUsed = true;
            return true;
        }
#endif
        if (pinching && !ptrPressed) pinching = false;
        return pinching;
    }

    void DecoCameraInput()
    {
        float dt = Time.unscaledDeltaTime;
        if (PinchZoom()) return;
        if (ptrPressed && ptrMoved && !ptrOnUI && dragCand < 0 && !(ornPicked && decoPressTree))
        {
            var d = ptrPos - lastPtr;
            decoYaw += d.x * 0.22f;
            decoH -= d.y * 0.012f;
        }
        lastPtr = ptrPos;
        float sc = OverUI(ptrPos) ? 0 : Scroll(); if (Mathf.Abs(sc) > 0.01f) decoZoom = Mathf.Clamp(decoZoom * (sc > 0 ? 0.9f : 1.1f), 0.5f, 3.5f);   // 휠: 확대·축소 (장식 줄 위에선 장식 스크롤)
        var a = MoveAxis();
        decoYaw -= a.x * 70f * dt; decoH += a.y * 3f * dt;
        if (decoButtons != 0) { decoYaw -= (decoButtons == 3 ? 1 : decoButtons == 4 ? -1 : 0) * 80f * dt; decoH += (decoButtons == 1 ? 1 : decoButtons == 2 ? -1 : 0) * 3.2f * dt; }
        decoH = Mathf.Clamp(decoH, 0.8f, TreeTop - 0.3f);
    }
    int decoButtons;   // 화면의 위/아래/왼쪽/오른쪽 버튼을 누르고 있는 동안
    bool ornPicked = true, dragOrn; int dragCand = -1; bool decoPressTree, pinchUsed;
    float ornScroll, barX0, barY0, barS0; bool barDrag, barMoved; int ornSelSeen = -1;
    readonly List<(Rect r, int i)> decoIconRects = new List<(Rect, int)>();

    void StartDecorating()
    {
        decorating = true; treeOverview = false; ornPicked = false; decoZoom = 3.5f;   // 처음엔 가장 멀리서 트리 전체 + 장식은 고르지 않은 상태 (11-25) walkTarget = null; followId = null;
        var d = cam.transform.position - TreePos; d.y = 0;
        decoYaw = d.sqrMagnitude > 0.01f ? Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg : 180f;
        decoH = Mathf.Clamp(2.2f, 0.8f, TreeTop);
    }

    void SetSel(int i) { ornSel = Mathf.Clamp(i, 0, ornTypes.Count - 1); RefreshGhost(); }

    void RefreshGhost()
    {
        if (ghost) Destroy(ghost);
        ghost = null; ghostGarlandY = -99;
        if (ornTypes.Count == 0 || IsGarland(ornTypes[ornSel])) return;
        ghost = AcCatalog.Spawn(ornTypes[ornSel], world, new Dictionary<string, Color> { { "orn", CurColor } });
        ghost.name = ornTypes[ornSel];
        ghost.transform.localScale = Vector3.one * OrnScale;
        foreach (var r in ghost.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
    }

    const float OrnScale = 2.4f;

    void AddOrn(string type, Color c, Vector3 pos, float ry)
    {
        QuestProgress("orn", 1);
        var lp = (pos - TreePos) / TreeScale;
        var o = new Orn { id = myId + "_" + DateTime.UtcNow.Ticks.ToString("x"), type = type, color = ColorUtility.ToHtmlStringRGB(c), owner = myId,
                          x = lp.x, y = lp.y, z = lp.z, ry = ry, v = 2, t = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds };
        if (online) net.PutOrn(o); else { mine.placed.Add(o); SaveMine(); }
        ShowOrn(o);
    }

    void RemoveOrn(string id)
    {
        if (online) net.DeleteOrn(id);
        else { mine.placed.RemoveAll(o => o.id == id); SaveMine(); }
        if (ornObjs.TryGetValue(id, out var go)) { Destroy(go); ornObjs.Remove(id); ornData.Remove(id); }
    }

    void ShowOrn(Orn o)
    {
        if (ornObjs.ContainsKey(o.id)) return;
        if (IsGarland(o.type)) { ShowGarland(o); return; }
        ColorUtility.TryParseHtmlString("#" + o.color, out var c);
        var go = AcCatalog.Spawn(o.type, world, new Dictionary<string, Color> { { "orn", c } });
        go.transform.position = OrnWorld(o);
        go.transform.rotation = Quaternion.Euler(0, o.ry, 0);
        go.transform.localScale = Vector3.one * OrnScale;
        var col = go.AddComponent<SphereCollider>(); col.center = new Vector3(0, -0.11f, 0); col.radius = 0.1f;
        go.AddComponent<AcOrnTag>().id = o.id;
        go.AddComponent<AcOrnSwing>();   // 매달린 채 살짝 흔들
        ornObjs[o.id] = go; ornData[o.id] = o;
    }

    static string Safe(string s) { foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_'); return s; }

    void LoadMine()
    {
        try { mine = JsonUtility.FromJson<OrnFile>(PlayerPrefs.GetString("ac_solo_orn", "")) ?? new OrnFile(); } catch { mine = new OrnFile(); }
        mine.owner = myId;
    }

    void SaveMine() { PlayerPrefs.SetString("ac_solo_orn", JsonUtility.ToJson(mine)); PlayerPrefs.Save(); }

    // 화면의 장식을 저장된 목록과 맞춤
    void PollOrnaments(bool force)
    {
        Dictionary<string, Orn> all;
        if (online) all = net.Ornaments();
        else { all = new Dictionary<string, Orn>(); foreach (var o in mine.placed) all[o.id] = o; }
        foreach (var kv in all) ShowOrn(kv.Value);
        var gone = new List<string>();
        foreach (var kv in ornObjs) if (!all.ContainsKey(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone) { Destroy(ornObjs[id]); ornObjs.Remove(id); ornData.Remove(id); }
    }

    void Toast(string m) { toast = m; toastUntil = Time.time + 4f; }

    // ========================================================
    // 화면 UI
    static Font uiFont;
    void Styles()
    {
        if (sBox != null) return;
        if (uiFont == null) uiFont = Resources.Load<Font>("Fonts/Jua-Regular");   // 둥근 한글 글꼴 (웹에서도 한글이 나오게)
        if (uiFont != null) GUI.skin.font = uiFont;
        // 11-25 디자인 다듬기: 아이보리 네모 판·회색 기본 버튼 → 둥근 유리판·둥근 칩 (AcUiTheme)
        sBox = AcUiTheme.Panel(GUI.skin.box);
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = AcUiTheme.Ink.linear }, richText = true };
        sSmall = new GUIStyle(sLabel) { fontSize = 13, fontStyle = FontStyle.Normal };
        sBtn = AcUiTheme.Button(GUI.skin.button, 14);
        sLabelSel = new GUIStyle(sSmall) { fontStyle = FontStyle.Bold, normal = { textColor = AcUiTheme.Accent.linear } };
    }

    static float UiK => IsMobile ? Mathf.Max(Mathf.Min(Screen.height / 560f, Screen.width / 400f), 0.6f) : Mathf.Clamp(Screen.height / 860f, 0.75f, 2.2f);   // 휴대폰은 손가락 크기에 맞게 크게
    static float SW => Screen.width / UiK;
    static float SH => Screen.height / UiK;

    void OnGUI()
    {
        var ev = Event.current; var et = ev.type; bool down = et == EventType.MouseDown || et == EventType.TouchDown;
        var m0 = GUI.matrix; GUI.matrix = Matrix4x4.identity; Vector2 raw = Event.current.mousePosition; GUI.matrix = m0;   // 화면 픽셀(위가 0)
        Vector2 mpos = raw / UiK;
        // 화면 이벤트로도 누르기·끌기·떼기를 기록 (입력 장치 쪽에서 못 받는 경우: 휴대폰 화면 흉내 등)
        var sp = new Vector2(raw.x, Screen.height - raw.y);
        if (down) { gPressed = true; gDown = true; gPos = sp; gSeen = true; }
        else if (et == EventType.MouseDrag || et == EventType.TouchMove || et == EventType.MouseMove) { if (gPressed || et == EventType.MouseMove) gPos = sp; }
        else if (et == EventType.MouseUp || et == EventType.TouchUp) { if (gPressed) { gUp = true; gPos = sp; } gPressed = false; }
        OnGUIInner();
        // 장식 끌어 놓기 (IMGUI 이벤트로: 아주 빠른 끌기도 처리)
        if (decorating && !treeOverview && !pinching)
        {
            var scr = new Vector2(raw.x, Screen.height - raw.y);
            if ((et == EventType.MouseDrag || et == EventType.TouchMove) && dragCand >= 0 && !dragOrn && !OverUI(scr)) { dragOrn = true; SetSel(dragCand); ornPicked = true; }
            if (et == EventType.MouseUp || et == EventType.TouchUp)
            {
                if (dragOrn) DropOrnAt(scr);
                dragOrn = false; dragCand = -1;
            }
        }
        if (building && snowTab == 1 && !pinching)
        {
            var scr = new Vector2(raw.x, Screen.height - raw.y);
            if ((et == EventType.MouseDrag || et == EventType.TouchMove) && dragCand >= 0 && !dragOrn && !OverUI(scr)) { dragOrn = true; snowDecoSel = dragCand; snowPicked = true; }
            if (et == EventType.MouseUp || et == EventType.TouchUp) { if (dragOrn) SnowDropAt(scr); dragOrn = false; dragCand = -1; }
        }
        // 화면 UI 위에서 누르기 시작했는지 IMGUI 쪽에서도 확인 (UI 위에서 끌 때 카메라가 돌지 않게)
        if (down)
        {
            foreach (var r in uiRects) if (r.Contains(mpos)) { ptrOnUI = true; break; }
            if (decorating || (building && snowTab == 1)) foreach (var (rr, idx) in decoIconRects) if (rr.Contains(mpos)) { dragCand = idx; ptrOnUI = true; }
        }
    }

    void OnGUIInner()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(UiK, UiK, 1));
        Styles(); SocialStyles();
        uiRects.Clear();
        if (!Ready)
        {
            GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.12f, 0.2f, 0.17f)));
            GUI.Label(new Rect(0, SH / 2 - 30, SW, 40), "<size=26><color=#fff3d6>크리스마스 파티</color></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(0, SH / 2 + 14, SW, 30), "<color=#d8e8d0>" + loadingMsg + "</color>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            return;
        }
        if (phase == Phase.Lobby) { LobbyGUI(); return; }
        if (playingGame) { CookieGameGUI(); return; }
        if (candyOpen) { CandyGUI(); return; }
        if (GiftBusy) { GiftGUI(); return; }
        if (kitchenOpen) { if (sTag == null) NameTags(); KitchenGUI(); AskWindow(); return; }
        NameTags();
        GreenTags();
        if (toast != null && Time.time < toastUntil)
        {
            float tw = Mathf.Min(520, SW - 20);
            float ty = ChatLogVisible && SW / 2 - tw / 2 < 380 ? 30 + 22 * chatLog.Count : SW / 2 + tw / 2 > SW - 250 ? 64 : 18;   // 위 오른쪽 버튼과 겹치면 그 아래
            var r = new Rect(SW / 2 - tw / 2, ty, tw, 40);
            GUI.Box(R(r), "", sBox); GUI.Label(new Rect(r.x + 16, r.y + 9, r.width - 32, 24), toast, sSmall);
        }
        // 대화 기록
        if (ChatLogVisible)
        {
            var lr = new Rect(14, 14, 360, 22 * chatLog.Count + 16);
            GUI.Box(R(lr), "", sBox);
            for (int i = 0; i < chatLog.Count; i++) GUI.Label(new Rect(26, 20 + i * 22, 340, 22), chatLog[i], sSmall);
        }
        PlayBanners();
        if (lettersOpen) { LettersWindow(); AskWindow(); return; }
        if (playingGame) { CookieGameGUI(); return; }
        if (treeOverview) { OverviewGUI(); return; }
        if (snowFocusId != null) { SnowFocusGUI(); AskWindow(); return; }
        if (pianoOpen) { PianoGUI(); return; }
        if (quizOpen) { QuizGUI(); return; }
        if (GreenBusy) { GreenGUI(); return; }
        if (decorating) DecorateBar();
        else if (building) SnowBar();
        else
        {
            if (!IsMobile && SW > 900)
            {
                GUI.Box(R(new Rect(14, SH - 46, 520, 34)), "", sBox);
                GUI.Label(new Rect(28, SH - 40, 510, 24), "WASD/클릭 이동 / Enter 말하기 / Z 인사 / X 하트 / V 만세 / C 캐릭터 꾸미기", sSmall);
            }
            ActionButtons();
            ContextButtons();
            GiftHUD();
            QuestHUD();
            SnowFightHUD();
            PillowHUD();
        }
        PeopleUI();
        // 친구 가까이: 함께 하기
        var near = NearestRemote(2.0f);
        if (near != null && !decorating && !building && !NearTree && !InStudy && !Outside)
        {
            var r = new Rect(SW / 2 - 200, SH - (IsMobile ? 150 : 120) - (SW < 640 ? 10 : 0), 400, 60);
            GUI.Box(R(r), "", sBox);
            GUI.Label(new Rect(r.x + 14, r.y + 6, 380, 22), near.net.name + " 님과", sSmall);
            if (GUI.Button(new Rect(r.x + 14, r.y + 28, 120, 26), "하이파이브", sBtn)) { Emote("highfive", near.net.id); FaceTo(near.c.transform.position); }
            if (GUI.Button(new Rect(r.x + 140, r.y + 28, 120, 26), HeldGift != null ? "<b>선물 전달하기</b>" : "선물 주기", sBtn)) { if (HeldGift != null) GiveGift(near); else Emote("gift", near.net.id); FaceTo(near.c.transform.position); }
            if (GUI.Button(new Rect(r.x + 266, r.y + 28, 120, 26), "인사", sBtn)) { Emote("wave", near.net.id); FaceTo(near.c.transform.position); }
        }
        if (chatting) ChatBox();
        if (showCustom) CustomWindow();
        MapWindow();
        QuestWindow();
        AskWindow();
        QuitWindow();
        if (net != null && net.Status != "") GUI.Label(new Rect(SW - 330, IsMobile ? 60 : 50, 320, 24), net.Status, sSmall);
    }

    void FaceTo(Vector3 p) { var d = p - me.transform.position; d.y = 0; if (d.sqrMagnitude > 0.001f) { yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; } }

    void ChatBox()
    {
        var r = new Rect(SW / 2 - 260, SH - 190, 520, 44);
        GUI.Box(R(r), "", sBox);
        GUI.SetNextControlName("chat");
        chatText = GUI.TextField(new Rect(r.x + 12, r.y + 10, r.width - 24, 26), chatText, 60);
        GUI.FocusControl("chat");
        var e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && Time.unscaledTime - chatOpenedAt < 0.2f) e.Use();
        else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        { Say(chatText); chatting = false; chatText = ""; GUIUtility.keyboardControl = 0; e.Use(); }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { chatting = false; GUIUtility.keyboardControl = 0; e.Use(); }
    }

    GUIStyle sTag, sTagMe, sBubble;
    void NameTags()
    {
        if (sTag == null)
        {
            sTag = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { background = sBox.normal.background, textColor = new Color(0.42f, 0.3f, 0.22f) } };
            var red = new Texture2D(1, 1); red.SetPixel(0, 0, new Color(0.78f, 0.25f, 0.25f, 0.95f)); red.Apply();
            sTagMe = new GUIStyle(sTag) { normal = { background = red, textColor = Color.white } };
            sBubble = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15, normal = { background = sBox.normal.background, textColor = new Color(0.3f, 0.22f, 0.16f) }, padding = new RectOffset(10, 10, 6, 6) };
        }
        Tag(me.transform.position, uiName, true, myChat, myChatT, myEmote, myEmoteT, questPoints);
        foreach (var r in remotes.Values) if (r.net != null) Tag(r.c.transform.position, r.net.name, false, r.net.chat, r.net.chatT, r.net.emote, r.net.emoteT, r.net.qp);
    }

    static readonly Dictionary<string, string> EmoteText = new Dictionary<string, string>
        { { "wave", "안녕!" }, { "heart", "♥" }, { "cheer", "메리 크리스마스!" }, { "highfive", "하이파이브!" }, { "gift", "선물이에요!" } };

    GUIStyle sTagFancy, sTagFancyMe;
    void Tag(Vector3 pos, string name, bool isMe, string chat, double chatT, string emote, double emoteT, int qp = 0)
    {
        var sp = cam.WorldToScreenPoint(pos + new Vector3(0, 1.55f, 0));
        if (sp.z < 0) return;
        float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
        if (qp >= 3)
        {   // 퀘스트 포인트 3 이상: 금빛 테두리에 별이 반짝이는 이름표
            if (sTagFancy == null)
            {
                var gold = new Texture2D(1, 2) { wrapMode = TextureWrapMode.Clamp }; gold.SetPixel(0, 0, new Color(0.98f, 0.78f, 0.3f)); gold.SetPixel(0, 1, new Color(1f, 0.93f, 0.62f)); gold.Apply();
                var ruby = new Texture2D(1, 2) { wrapMode = TextureWrapMode.Clamp }; ruby.SetPixel(0, 0, new Color(0.7f, 0.12f, 0.2f)); ruby.SetPixel(0, 1, new Color(0.9f, 0.3f, 0.35f)); ruby.Apply();
                sTagFancy = new GUIStyle(sTag) { normal = { background = gold, textColor = new Color(0.5f, 0.2f, 0.12f) } };
                sTagFancyMe = new GUIStyle(sTag) { normal = { background = ruby, textColor = new Color(1f, 0.95f, 0.8f) } };
            }
            var fs = sTagFancy.CalcSize(new GUIContent(name));
            var fr = new Rect(x - fs.x / 2 - 20, y - 14, fs.x + 40, 26);
            AcUiTheme.DrawGlow(fr, 0.35f + 0.25f * Mathf.Sin(Time.time * 2.5f + x * 0.01f));
            AcUiTheme.Fill(new Rect(fr.x - 2, fr.y - 2, fr.width + 4, fr.height + 4), new Color(1f, 0.85f, 0.4f), 0);
            GUI.Label(fr, name, isMe ? sTagFancyMe : sTagFancy);
            float tw = Mathf.Sin(Time.time * 3f + x * 0.02f) * 0.5f + 0.5f;
            AcUiTheme.Star(new Rect(fr.x + 4, fr.y + 6, 13, 13), Color.Lerp(new Color(1f, 0.95f, 0.7f), Color.white, tw));
            AcUiTheme.Star(new Rect(fr.xMax - 17, fr.y + 6, 13, 13), Color.Lerp(Color.white, new Color(1f, 0.95f, 0.7f), tw));
        }
        else
        {
            var size = sTag.CalcSize(new GUIContent(name));
            GUI.Label(new Rect(x - size.x / 2 - 8, y - 12, size.x + 16, 22), name, isMe ? sTagMe : sTag);
        }
        double now = AcSync.UnixMs;
        string bubble = null;
        if (!string.IsNullOrEmpty(chat) && now - chatT < 5000) bubble = chat;
        else if (!string.IsNullOrEmpty(emote) && now - emoteT < 2500) EmoteText.TryGetValue(emote.Split(':')[0], out bubble);
        if (bubble != null)
        {
            float w = Mathf.Min(260, sBubble.CalcSize(new GUIContent(bubble)).x + 4);
            float h = sBubble.CalcHeight(new GUIContent(bubble), w);
            GUI.Label(new Rect(x - w / 2, y - 20 - h, w, h), bubble, sBubble);
        }
    }

    // 입장 화면
    Vector2 lobbyScroll;
    // 처음 온 사람만 이름·꾸미기 화면. 한 번 들어온 적이 있으면 저장된 이름·모습으로 바로 입장 (바꾸고 싶을 때만 '꾸미기·이름 바꾸기')
    bool lobbyEdit, lobbyEditInit;
    void LobbyGUI()
    {
        if (!lobbyEditInit) { lobbyEditInit = true; lobbyEdit = string.IsNullOrWhiteSpace(uiName); }
        if (LookAskGUI()) return;
        float lw = Mathf.Min(440, SW - 20);
        bool portrait = Screen.height > Screen.width;
        if (!lobbyEdit)
        {
            float h = 196;
            var q = portrait ? new Rect((SW - lw) / 2, SH - h - 24, lw, h) : new Rect(Mathf.Min(20, (SW - lw) / 2), SH / 2 - h / 2, lw, h);
            GUI.Box(R(q), "", sBox);
            GUI.Label(new Rect(q.x + 18, q.y + 12, q.width - 36, 32), "<size=22>크리스마스 파티</size>", sLabel);
            GUI.Label(new Rect(q.x + 18, q.y + 48, q.width - 36, 26), "<b>" + uiName.Trim() + "</b> 님, 다시 오셨네요!", sSmall);
            GUI.Label(new Rect(q.x + 18, q.y + 72, q.width - 36, 22), "<size=12>친구들과 함께 꾸미는 크리스마스 집이에요</size>", sSmall);
            float bw = (q.width - 36 - 10) / 2;
            if (lobbyMsg != "") GUI.Label(new Rect(q.x + 18, q.y + 92, q.width - 36, 22), "<color=#b03030>" + lobbyMsg + "</color>", sSmall);
            if (GUI.Button(new Rect(q.x + 18, q.y + 108, bw, 46), "<b>입장하기</b>", sBtnBig)) Enter(false);   // 주 버튼은 빨갛게
            if (GUI.Button(new Rect(q.x + 18 + bw + 10, q.y + 112, bw, 42), "혼자 둘러보기", sBtn)) Enter(true);
            if (GUI.Button(new Rect(q.x + 18, q.y + 160, q.width - 36, 28), "꾸미기, 이름 바꾸기", sBtn)) lobbyEdit = true;
            return;
        }
        if (Event.current.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;   // 글 칸 밖을 누르면 화살표로 꾸미기
        var r = portrait ? new Rect((SW - lw) / 2, SH * 0.46f, lw, SH * 0.54f - 10) : new Rect(Mathf.Min(20, (SW - lw) / 2), 16, lw, SH - 32);
        GUI.Box(R(r), "", sBox);
        GUILayout.BeginArea(new Rect(r.x + 16, r.y + 10, r.width - 32, r.height - 20));
        GUILayout.Label(portrait ? "<size=20>크리스마스 파티</size>" : "<size=24>크리스마스 파티</size>", sLabel);
        if (!portrait) { GUILayout.Label("캐릭터를 꾸미고 현관으로 들어가세요. 다 함께 커다란 트리를 꾸며요!", sSmall); GUILayout.Space(8); }
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름", sSmall, GUILayout.Width(60));
        uiName = TextBox("이름을 입력하세요", uiName, 12);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("방 코드", sSmall, GUILayout.Width(60));
        uiPath = TextBox("방 코드 (같은 코드를 쓴 사람끼리 만나요)", uiPath, 20);
        GUILayout.EndHorizontal();
        lobbyScroll = BeginDragScroll("lobby", lobbyScroll);
        CustomControls();
        EndDragScroll("lobby");
        if (lobbyMsg != "") GUILayout.Label("<color=#b03030>" + lobbyMsg + "</color>", sSmall);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("입장하기", sBtn, GUILayout.Height(38))) Enter(false);
        if (GUILayout.Button("혼자 둘러보기", sBtn, GUILayout.Height(38), GUILayout.Width(130))) Enter(true);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DecorateBar()
    {
        bool narrow = SW < 640;   // 세로 화면: 모양 줄 아래에 색 줄
        float h = narrow ? 262 : 158, y = SH - h - 8;
        GUI.Box(R(new Rect(10, y, SW - 20, h)), "", sBox);
        // 머리줄: 제목·장식 수 (왼쪽) / 트리 전경·나오기 (오른쪽)
        GUI.Label(new Rect(26, y + 8, 120, 26), "트리 꾸미기", sLabel);
        if (!narrow) GUI.Label(new Rect(136, y + 12, 90, 22), $"장식 {ornObjs.Count}개", sSmall);
        if (GUI.Button(new Rect(SW - 20 - 90, y + 6, 82, 32), "<b>나오기</b>", sBtn)) { decorating = false; return; }
        if (GUI.Button(new Rect(SW - 20 - 90 - 100, y + 6, 92, 32), "트리 전경", sBtn)) { treeOverview = true; return; }
        // 도움말은 맨 아래 줄
        GUI.Label(new Rect(26, y + h - 26, SW - 52, 22), (narrow ? $"장식 {ornObjs.Count}개, " : "") + (IsMobile ? "누른 채 자리 잡고 떼면 달기, 길게 눌러 떼기" : "클릭으로 달기, 오른쪽 클릭 떼기, 해제 후 끌어서 둘러보기, 휠 확대"), sSmall);
        // 화면 오른쪽, 패널 위: 카메라 움직이기 버튼 (누르고 있는 동안) — 2x2로 패널과 떨어뜨려 둠
        decoButtons = 0;
        CamPad(SW - 16 - 2 * 58 - 6, y - 2 * 40 - 14, true);
        // 모양 (실제 오너먼트 그림) — 누르면 고르기, 다시 누르면 해제, 끌어서 트리에 놓기
        int perRowW = (int)(SW - (narrow ? 52 : 330));
        int ci = IconStrip(ornStrip, new Rect(23, y + 37, perRowW + 6, 68), ornTypes.Count, 62, i => icons.TryGetValue(ornTypes[i], out var rt) ? rt : null, ornSel, ornPicked);
        if (ci >= 0) { if (ci == ornSel && ornPicked) ornPicked = false; else { SetSel(ci); ornPicked = true; } }
        var it = AcCatalog.Get(ornTypes[ornSel]);
        if (!ornPicked) GUI.Label(new Rect(26, y + 108, Mathf.Min(460, SW - 52), 22), "장식을 고르면 달 수 있어요 (끌어서 돌려 보기)", sSmall);
        else
        {
            GUI.Label(new Rect(26, y + 108, narrow ? SW - 150 : Mathf.Min(420, SW - 290 - 130), 22), IsGarland(ornTypes[ornSel]) ? GarlandName(ornTypes[ornSel]) + " - 누른 높이에 한 바퀴" : it != null ? it.desc : ornTypes[ornSel], sSmall);
            var ur = new Rect(narrow ? SW - 20 - 90 : SW - 290 - 110, y + 104, 82, 28);
            if (GUI.Button(R(ur), "선택 해제", sBtn)) ornPicked = false;
        }
        // 색: < ■ 이름 >
        float cx = narrow ? 26 : SW - 290, cy = narrow ? y + 142 : y + 44;   // 세로 화면: 장식 이름 줄과 겹치지 않게 조금 아래
        GUI.Label(new Rect(cx, cy - 6, 260, 22), "색", sSmall);
        if (GUI.Button(new Rect(cx, cy + 16, 34, 40), "<", sBtn)) { colSel = Wrap(colSel - 1, AcPalette.Vivid.Length); RefreshGhost(); }
        GUI.DrawTexture(new Rect(cx + 41, cy + 15, 102, 42), AcPalette.Tex(new Color(0.45f, 0.35f, 0.28f)));
        GUI.DrawTexture(new Rect(cx + 42, cy + 16, 100, 40), AcPalette.Tex(CurColor));
        if (GUI.Button(new Rect(cx + 150, cy + 16, 34, 40), ">", sBtn)) { colSel = Wrap(colSel + 1, AcPalette.Vivid.Length); RefreshGhost(); }
        GUI.Label(new Rect(cx + 192, cy + 26, 90, 22), AcPalette.VividNames[Wrap(colSel, AcPalette.Vivid.Length)], sSmall);
        // 이웃 색 미리보기
        for (int k = -3; k <= 3; k++)
        {
            if (k == 0) continue;
            var c = AcPalette.Vivid[Wrap(colSel + k, AcPalette.Vivid.Length)];
            GUI.DrawTexture(new Rect(cx + 82 + k * 22, cy + 66, 18, 12), AcPalette.Tex(c));
        }
    }

    // 카메라 버튼 묶음: [위로][아래로] / [< 돌기][돌기 >]
    void CamPad(float x, float y, bool upDown)
    {
        string[] bl = { "위로", "아래로", "< 돌기", "돌기 >" };
        var rects = new[] { new Rect(x, y, 56, 34), new Rect(x + 62, y, 56, 34), new Rect(x, y + 40, 56, 34), new Rect(x + 62, y + 40, 56, 34) };   // 작게 (트리를 덜 가리게)
        int from = upDown ? 0 : 2;
        if (!upDown) for (int k = 2; k < 4; k++) rects[k].y = y;
        var bg = upDown ? new Rect(x - 4, y - 4, 126, 82) : new Rect(x - 4, y - 4, 126, 42);
        R(bg);
        for (int k = from; k < 4; k++)
        {
            GUI.Box(R(rects[k]), "", sBox);
            GUI.Label(rects[k], bl[k], new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            if (ptrPressed && ptrOnUI && rects[k].Contains(new Vector2(ptrPos.x / UiK, (Screen.height - ptrPos.y) / UiK))) decoButtons = k + 1;
        }
    }

    // 트리 전경: 꾸미기 화면 없이 트리만, 끌어서 돌려 보고 '나가기'로 나옴
    // 트리 전경: 바닥까지 다 보이게 가까운 면을 자르지 않는 대신, 카메라와 트리 사이를 가리는 바깥 소품(나무 등)만 잠시 숨김
    readonly List<(GameObject go, Bounds b)> ovOccluders = new List<(GameObject, Bounds)>();
    readonly List<GameObject> ovHidden = new List<GameObject>();
    float ovNext;
    void CollectOverviewOccluders()
    {
        ovOccluders.Clear();
        var o = world.Find("Outdoors"); if (o == null) return;
        foreach (Transform c in o)
        {
            if (c.name.StartsWith("SnowBase") || c.name.Contains("ground_snow") || c.name.Contains("path_stones")) continue;
            var rs = c.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
            ovOccluders.Add((c.gameObject, b));
        }
    }
    void UpdateOverviewOccluders()
    {
        if (!treeOverview)
        {
            if (ovHidden.Count > 0) { foreach (var g in ovHidden) if (g) g.SetActive(true); ovHidden.Clear(); }
            return;
        }
        if (Time.unscaledTime < ovNext) return;
        ovNext = Time.unscaledTime + 0.12f;
        var cp = cam.transform.position;
        var right = cam.transform.right; right.y = 0; right.Normalize();
        var targets = new List<Vector3>();
        for (float h = 0.3f; h <= TreeTop; h += TreeTop / 4f)
            for (int s = -1; s <= 1; s++) targets.Add(TreePos + Vector3.up * h + right * (s * TreeR * 0.8f * (1 - h / (TreeTop + 0.5f))));
        foreach (var (go, b) in ovOccluders)
        {
            if (!go) continue;
            var bb = b; bb.Expand(0.3f);
            bool block = Vector2.Distance(new Vector2(bb.center.x, bb.center.z), new Vector2(cp.x, cp.z)) < 7f;   // 카메라 바로 앞 소품도 숨김
            if (!block) foreach (var t in targets)
            {
                var d = t - cp; float len = d.magnitude;
                if (bb.IntersectRay(new Ray(cp, d / len), out float hit) && hit < len - TreeR * 0.5f) { block = true; break; }
            }
            if (block && go.activeSelf) { go.SetActive(false); ovHidden.Add(go); }
            else if (!block && !go.activeSelf && ovHidden.Contains(go)) { go.SetActive(true); ovHidden.Remove(go); }
        }
    }

    void OverviewGUI()
    {
        var tr = new Rect(SW / 2 - 110, 14, 220, 40);
        GUI.Box(R(tr), "", sBox);
        GUI.Label(tr, "<b>트리 전경</b>  <size=12>끌어서 돌려 보기</size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
        decoButtons = 0;
        float bw = IsMobile ? 150 : 130, bh = IsMobile ? 50 : 42;
        if (!IsMobile) CamPad(SW / 2 - 69, SH - bh - 20 - 58, false);   // 휴대폰은 끌어서 돌리면 되니 버튼 없음
        var r = new Rect(SW / 2 - bw / 2, SH - bh - 14, bw, bh);
        R(r);
        if (GUI.Button(r, "<b>나가기</b>", sBtnBig)) treeOverview = false;
    }

    void OverviewInput()
    {
        float dt = Time.unscaledDeltaTime;
        if (PinchZoom()) return;
        float sc = Scroll(); if (Mathf.Abs(sc) > 0.01f) decoZoom = Mathf.Clamp(decoZoom * (sc > 0 ? 0.9f : 1.1f), 0.5f, 3.5f);
        if (ptrPressed && ptrMoved && !ptrOnUI) decoYaw += (ptrPos - lastPtr).x * 0.22f;
        lastPtr = ptrPos;
        var a = MoveAxis(); decoYaw -= a.x * 70f * dt;
        if (decoButtons == 3) decoYaw -= 80f * dt; else if (decoButtons == 4) decoYaw += 80f * dt;
    }

    // 캐릭터 꾸미기
    static readonly string[] SpeciesKo = { "곰", "고양이", "토끼", "강아지", "여우", "판다", "사자", "돼지", "사람" };
    static readonly string[] SpeciesId = { "bear", "cat", "rabbit", "dog", "fox", "panda", "lion", "pig", "human" };
    static readonly string[] HatId = { "santa", "beanie", "earmuffs", "antlers", "tophat", "elf", "none" };
    static readonly string[] HatKo = { "산타", "털모자", "귀마개", "뿔", "중절모", "요정", "없음" };

    Vector2 customScroll;
    void CustomWindow()
    {
        float cw = Mathf.Min(450, SW - 20);
        var r = new Rect(SW - cw - 10, 20, cw, Mathf.Min(SH - 40, 640));
        GUI.Box(R(r), "", sBox);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 12, r.width - 28, r.height - 24));
        GUILayout.Label("캐릭터 꾸미기", sLabel);
        customScroll = BeginDragScroll("custom", customScroll, GUILayout.Height(r.height - 100));
        CustomControls();
        EndDragScroll("custom");
        GUILayout.Space(8);
        if (GUILayout.Button("완료", sBtn, GUILayout.Height(30))) showCustom = false;
        GUILayout.EndArea();
    }

}

public class AcOrnTag : MonoBehaviour { public string id; }

public class AcOrnSwing : MonoBehaviour
{
    float ph, baseY;
    void Start() { ph = UnityEngine.Random.value * 10; baseY = transform.eulerAngles.y; }
    void Update() { transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + ph) * 3f, baseY, Mathf.Sin(Time.time * 1.7f + ph) * 4f); }
}
