// AcRoom.cs - 동물의 숲풍 크리스마스 거실 (모델 미리보기 + 트리 꾸미기)
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
    const int NX = 8, NZ = 5;                        // 트리 거실: 가로 8칸(25.6m), 세로 5칸(16m)
    static float HalfX => NX * SEG / 2f;
    static float HalfZ => NZ * SEG / 2f;
    static readonly Vector3 TreePos = new Vector3(0, 0, -1.6f);
    const float TreeScale = 0.82f;

    Camera cam;
    Transform world;
    AcCharacter me;
    readonly List<AcCharacter> npcs = new List<AcCharacter>();
    readonly List<Vector3> npcTargets = new List<Vector3>();
    Collider treeCol;
    Vector3 camVel;
    float yaw;

    // ===== 오너먼트 =====
    [Serializable] public class Orn { public string id, type, color, owner; public float x, y, z, ry; public double t; }
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
    class Remote { public AcCharacter c; public string look; public AcNet net; public Vector3 target; public double lastEmoteT, lastChatT; }
    readonly Dictionary<string, Remote> remotes = new Dictionary<string, Remote>();
    readonly List<string> chatLog = new List<string>();
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
        if (IsMobile) QualityForMobile();

        // 웹: 모델 묶음과 목록을 내려받음 (파일을 직접 못 읽음)
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            loadingMsg = "목록 받는 중...";
            using (var r = UnityWebRequest.Get(Application.streamingAssetsPath + "/models/catalog.json"))
            {
                yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { loadingMsg = "불러오기 실패: " + r.error; yield break; }
                AcCatalog.PendingText = r.downloadHandler.text;
            }
            using (var r = UnityWebRequest.Get(Application.streamingAssetsPath + "/models/models.pack"))
            {
                var op = r.SendWebRequest();
                while (!op.isDone) { loadingMsg = $"크리스마스 거실 불러오는 중... {Mathf.RoundToInt(r.downloadProgress * 100)}%"; yield return null; }
                if (r.result != UnityWebRequest.Result.Success) { loadingMsg = "불러오기 실패: " + r.error; yield break; }
                loadingMsg = "집 짓는 중...";
                yield return null;
                AcmLoader.SetPack(r.downloadHandler.data);
            }
        }
        loadingMsg = "집 짓는 중...";
        yield return null;
        AcCatalog.Load();
        world = new GameObject("World").transform;
        SetupLights();
        BuildRoom();
        BuildHouse();
        BuildOutdoors();
        BuildFurniture();

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
    }

    string loadingMsg = "불러오는 중...";
    bool Ready => loadingMsg == null && me != null;

    // 입장 버튼
    void Enter(bool solo)
    {
        string code = new string(uiPath.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
        if (!solo && code == "") { lobbyMsg = "방 코드를 입력하세요 (같은 코드끼리 만나요)"; return; }
        if (uiName.Trim() == "") { lobbyMsg = "이름을 입력하세요"; return; }
        uiPath = code;
        PlayerPrefs.SetString("ac_room", code); PlayerPrefs.SetString("xmas_name", uiName.Trim()); PlayerPrefs.Save();
        online = !solo;
        if (online)
        {
            net = gameObject.AddComponent<AcFirebase>();
            net.Begin(code, myId, MyNet());
        }
        else { LoadMine(); PollOrnaments(true); SpawnDemoNpcs(); }
        showCustom = false;
        GUIUtility.keyboardControl = 0;
        // 현관 문간에서 걸어 들어오기
        me.transform.position = DoorPos + new Vector3(0, 0, 0.1f);
        me.transform.rotation = Quaternion.Euler(0, 0, 0); yaw = 0;
        autoWalk = DoorPos + new Vector3(0, 0, 2.8f);
        phase = Phase.Entering;
        Toast(solo ? "혼자 둘러보기 - 트리 가까이 가서 오너먼트를 달아 보세요!" : $"'{code}' 방에 입장! 함께 트리를 꾸며요");
    }

    AcNet MyNet()
    {
        return new AcNet
        {
            name = uiName.Trim(), look = me.look.Encode(), x = me.transform.position.x, z = me.transform.position.z, ry = yaw, speed = me.speed,
            emote = myEmote, emoteT = myEmoteT, chat = myChat, chatT = myChatT, decorating = decorating,
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
    GameObject Put(string name, Vector3 pos, float rotY = 0, float scale = 1, Dictionary<string, Color> tints = null, Transform parent = null)
    {
        var go = AcCatalog.Spawn(name, parent ? parent : world, tints);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, rotY, 0);
        go.transform.localScale = Vector3.one * scale;
        AddObstacle(name, pos, rotY, scale);
        return go;
    }

    void HidePart(GameObject go, string part) { var t = AcmLoader.Find(go, part); if (t) t.gameObject.SetActive(false); }

    void BuildRoom()
    {
        var room = new GameObject("Room").transform; room.SetParent(world, false);
        for (int ix = 0; ix < NX; ix++)
            for (int iz = 0; iz < NZ; iz++)
                Put("floor_planks", new Vector3(-HalfX + SEG * (ix + 0.5f), 0, -HalfZ + SEG * (iz + 0.5f)), (ix + iz) % 2 * 90, S, null, room);
        // 뒷벽 (창 2개 + 문 1개)
        for (int ix = 0; ix < NX; ix++)
        {
            string w = ix == NX - 1 ? "wall_door" : ix == 1 || ix == 3 || ix == 5 ? "wall_window" : "wall_plain";
            var g = Put(w, new Vector3(-HalfX + SEG * (ix + 0.5f), 0, -HalfZ), 0, S, null, room);
            if (w == "wall_window") { HidePart(g, "glass"); Put("curtain", new Vector3(g.transform.position.x, 2.72f * S - 2.02f * S * 0.85f, -HalfZ + 0.16f), 0, S * 0.85f, null, room); }   // 커튼 봉이 창틀 바로 위
            if (w == "wall_door")
            {
                Put("wreath", new Vector3(g.transform.position.x, 1.75f * S + 1.2f, -HalfZ + 0.16f), 0, 1.1f, null, room);
                doorTr = AcmLoader.Find(g, "door");
                var shell = AcmLoader.Find(g, "wall"); // 문 뒤는 바깥이 보이게 둠
            }
        }
        // 벽 위 전구 줄
        for (int ix = 0; ix < NX; ix += 1)
            Put("string_lights", new Vector3(-HalfX + SEG * (ix + 0.5f), 3.35f * S, -HalfZ + 0.15f), 0, S * 0.62f, null, room);
    }

    void BuildOutdoors()
    {
        var o = new GameObject("Outdoors").transform; o.SetParent(world, false);
        int gx = Mathf.CeilToInt((HouseHalf + 20f) / 8f);
        for (int ix = -gx; ix <= gx; ix++)
            for (int iz = 0; iz < 4; iz++)
            {
                Put("ground_snow", new Vector3(ix * 8f, -0.05f, -HalfZ - 4f - iz * 8f), Random.Range(0, 4) * 90, 2f, null, o);
                Put("ground_snow", new Vector3(ix * 8f, -0.05f, HalfZ + 4f + iz * 8f), 0, 2f, null, o);
            }
        for (int iz = -2; iz <= 2; iz++)
            for (int k = 0; k < 3; k++)
            {
                Put("ground_snow", new Vector3(-HouseHalf - 4f - k * 8f, -0.05f, iz * 8f), 0, 2f, null, o);
                Put("ground_snow", new Vector3(HouseHalf + 4f + k * 8f, -0.05f, iz * 8f), 0, 2f, null, o);
            }
        string[] trees = { "pine_snowy", "pine_tall", "pine_small", "bare_tree", "birch", "palm_cedar", "round_tree" };
        var rnd = new System.Random(5);
        for (int i = 0; i < (IsMobile ? 45 : 150); i++)
        {
            float side = rnd.Next(5);
            Vector3 p = side <= 2 ? new Vector3(-HouseHalf - 12 + (float)rnd.NextDouble() * (2 * HouseHalf + 24), 0, -HalfZ - 4 - (float)rnd.NextDouble() * 24)
                      : side == 3 ? new Vector3(-HouseHalf - 4 - (float)rnd.NextDouble() * 16, 0, -HalfZ - 4 + (float)rnd.NextDouble() * (2 * HalfZ + 8))
                                  : new Vector3(HouseHalf + 4 + (float)rnd.NextDouble() * 16, 0, -HalfZ - 4 + (float)rnd.NextDouble() * (2 * HalfZ + 8));
            if (Mathf.Abs(p.x) < 6 && p.z > -HalfZ - 9 && p.z < -HalfZ - 2) continue;   // 창밖 눈사람 자리
            if (Mathf.Abs(p.x - DoorPos.x) < 3.5f && p.z > -HalfZ - 12) continue;          // 현관 앞 길
            Put(trees[rnd.Next(trees.Length)], p, rnd.Next(360), 0.9f + (float)rnd.NextDouble() * 0.6f, null, o);
        }
        Put("house_cottage", new Vector3(-4f, 0, -HalfZ - 16f), 15, 1.2f, null, o);
        Put("snowman_big", new Vector3(-3.6f, 0, -HalfZ - 4.2f), 15, 1.1f, null, o);
        Put("snowman_small", new Vector3(-2.3f, 0, -HalfZ - 3.6f), -10, 1.1f, null, o);
        Put("lamp_post", new Vector3(3.2f, 0, -HalfZ - 3.0f), 0, 1.2f, null, o);
        Put("snow_pile", new Vector3(1.2f, 0, -HalfZ - 5f), 0, 1.4f, null, o);
        Put("snowball_pile", new Vector3(-1.0f, 0, -HalfZ - 3.2f), 0, 1.2f, null, o);
        Put("sled", new Vector3(5.6f, 0, -HalfZ - 4.6f), 70, 1.2f, null, o);
        Put("mailbox", new Vector3(DoorPos.x + 2.6f, 0, -HalfZ - 1.8f), 180, 1.2f, null, o);
        for (int i = -3; i <= 2; i++) Put("fence", new Vector3(i * 2.1f * 1.2f, 0, -HalfZ - 9.5f), 0, 1.2f, null, o);
        // 각 방 창밖 풍경
        var outs = new[] { "snowman_bucket", "lamp_post", "snow_pile", "igloo", "snowman_small", "sled", "bench", "snowball_pile" };
        for (int i = 0; i < 8; i++)
        {
            float x = (i < 4 ? -HalfX - (i + 0.5f) * RoomW : HalfX + (i - 4 + 0.5f) * RoomW);
            Put(outs[i], new Vector3(x + (i % 2 == 0 ? -2f : 2.5f), 0, -HalfZ - 4.5f - (i % 3)), i * 40, outs[i] == "igloo" ? 1.1f : 1.15f, null, o);
            Put("lamp_post", new Vector3(x + 4.5f, 0, -HalfZ - 2.6f), 0, 1.1f, null, o);
        }
        Put("igloo", new Vector3(10f, 0, -HalfZ - 12f), -30, 1.5f, null, o);
        Put("bush_snowy", new Vector3(-6f, 0, -HalfZ - 2.5f), 0, 1.3f, null, o);
        Put("bush_holly", new Vector3(DoorPos.x - 2.4f, 0, -HalfZ - 1.5f), 0, 1.3f, null, o);
        for (int i = 0; i < 5; i++) Put("path_stones", new Vector3(DoorPos.x, 0.0f, -HalfZ - 1.6f - i * 2.0f), i * 37, 1.0f, null, o);
        Put("lantern_ground", new Vector3(DoorPos.x + 1.3f, 0, -HalfZ - 0.9f), 0, 1.3f, null, o);
        Put("lantern_ground", new Vector3(DoorPos.x - 1.3f, 0, -HalfZ - 0.9f), 0, 1.3f, null, o);
        Put("bench", new Vector3(DoorPos.x - 4.5f, 0, -HalfZ - 6f), 30, 1.2f, null, o);
        Put("signpost", new Vector3(DoorPos.x + 2.2f, 0, -HalfZ - 7f), -20, 1.2f, null, o);
        Put("snowman_bucket", new Vector3(DoorPos.x + 4.5f, 0, -HalfZ - 9f), -30, 1.1f, null, o);
        BuildSnow();
        PointLight(new Vector3(3.2f, 2.7f, -HalfZ - 3.0f), new Color(1f, 0.85f, 0.55f), 6f, 1.5f);
    }

    void BuildFurniture()
    {
        var f = new GameObject("Furniture").transform; f.SetParent(world, false);
        // 트리
        Put("tree_skirt", TreePos, 0, 1.15f, null, f);
        var tree = Put("tree_giant", TreePos + new Vector3(0, 0.04f, 0), 0, TreeScale, null, f);
        Put("tree_star_topper", TreePos + new Vector3(0, 6.85f * TreeScale, 0), 0, TreeScale * 1.1f, null, f);
        var leaves = AcmLoader.Find(tree, "leaves");
        if (leaves) { var mc = leaves.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = leaves.GetComponent<MeshFilter>().sharedMesh; treeCol = mc; }
        var trunk = AcmLoader.Find(tree, "trunk");
        if (trunk) { var bc = trunk.gameObject.AddComponent<CapsuleCollider>(); bc.radius = 0.6f; bc.height = 4; }
        Put("gift_pile", TreePos + new Vector3(1.6f, 0, 1.3f), -20, 1.2f, null, f);
        Put("gift_box", TreePos + new Vector3(-1.7f, 0, 1.4f), 25, 1.2f, null, f);
        Put("gift_tall", TreePos + new Vector3(-2.2f, 0, 0.6f), 10, 1.2f, null, f);
        Put("gift_flat", TreePos + new Vector3(0.6f, 0, 2.1f), 40, 1.2f, null, f);
        Put("teddy_bear", TreePos + new Vector3(-0.8f, 0, 2.2f), -15, 1.3f, null, f);
        Put("toy_train", TreePos + new Vector3(2.4f, 0, 0.2f), -60, 1.3f, null, f);
        Put("present_sack", TreePos + new Vector3(2.2f, 0, -1.4f), -40, 1.2f, null, f);
        Put("rug_round", TreePos + new Vector3(0, 0.005f, 0), 0, 1.6f, null, f);

        // 벽난로 구역 (왼쪽 뒤)
        float fx = -HalfX + SEG * 0.5f + 0.4f, fz = -HalfZ + 0.55f;
        Put("fireplace", new Vector3(fx, 0, fz), 0, 1.25f, null, f);
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
        Put("gingerbread_man", new Vector3(HalfX - 3.4f, 0, 6.6f), -160, 1.1f, null, f);
        Put("gingerbread_woman", new Vector3(HalfX - 4.6f, 0, 6.8f), -170, 1.1f, null, f);
        Put("reindeer_plush", new Vector3(HalfX - 0.9f, 0, 5.2f), -110, 1.2f, null, f);
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
            c.transform.position = TreePos + new Vector3(Mathf.Cos(a) * 3.6f, 0, Mathf.Sin(a) * 3.0f + 1.5f);
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
        else if (!showCustom && !chatting) { MovePlayer(dt); TapMove(dt); }
        else me.speed = 0;

        UpdateNpcs(dt);
        UpdateRemotes(dt);
        UpdateCamera(dt, false);

        if (!chatting)
        {
            if (Key("c")) showCustom = !showCustom;
            if (Key("f2")) { showNpcs = !showNpcs; foreach (var n in npcs) n.gameObject.SetActive(showNpcs); }
            if (Key("esc")) showCustom = false;
            if (Key("z")) Emote("wave");
            if (Key("x")) Emote("heart");
            if (Key("v")) Emote("cheer");
            if (Key("enter")) OpenChat();
        }

        float treeDist = Vector2.Distance(new Vector2(me.transform.position.x, me.transform.position.z), new Vector2(TreePos.x, TreePos.z));
        decorating = phase == Phase.Play && treeDist < 5.2f && !showCustom && !chatting;
        if (decorating) Decorate(); else { if (ghost) ghost.SetActive(false); if (focusRing) FocusAt(Vector3.zero, false); pointerFocus = null; }

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

    void Log(string line) { chatLog.Add(line); if (chatLog.Count > 6) chatLog.RemoveAt(0); }

    // 다른 참가자
    void UpdateRemotes(float dt)
    {
        if (net == null) return;
        var peers = net.Peers();
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
                Toast(n.name + " 님이 들어왔어요");
                Log("- " + n.name + " 님 입장");
                if (npcs.Count > 0) foreach (var c in npcs) c.gameObject.SetActive(false);
            }
            if (r.look != n.look) { r.look = n.look; r.c.Rebuild(AcLook.Decode(n.look)); }
            r.net = n; r.target = new Vector3(n.x, 0, n.z);
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
        foreach (var id in gone) { Log("- " + (remotes[id].net?.name ?? "") + " 님 퇴장"); Destroy(remotes[id].c.gameObject); remotes.Remove(id); }
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

    void MovePlayer(float dt)
    {
        var a = MoveAxis();
        // 카메라 방향 기준 (실내: 카메라가 -z를 봄, 실외: +z를 봄)
        Vector3 dir = Quaternion.Euler(0, Mathf.Round(camYawCur / 180f) * 180f, 0) * new Vector3(a.x, 0, a.y);
        if (dir.sqrMagnitude > 1) dir.Normalize();
        float spd = 3.6f;
        var prev = me.transform.position;
        var p = prev + dir * spd * dt;
        p = Collide(p, 0.36f);
        p = ClampRegion(prev, p);
        var d2 = new Vector2(p.x - TreePos.x, p.z - TreePos.z);
        if (d2.magnitude < 2.6f) { d2 = d2.normalized * 2.6f; p.x = TreePos.x + d2.x; p.z = TreePos.z + d2.y; }
        foreach (var r in remotes.Values)
        {
            var dd = new Vector2(p.x - r.c.transform.position.x, p.z - r.c.transform.position.z);
            if (dd.magnitude < 0.75f && dd.magnitude > 0.001f) { dd = dd.normalized * 0.75f; p.x = r.c.transform.position.x + dd.x; p.z = r.c.transform.position.z + dd.y; }
        }
        p = ClampRegion(prev, p);
        var moved = p - prev; moved.y = 0;
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
                    float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(3.2f, 6f);
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

    void UpdateCamera(float dt, bool snap)
    {
        Vector3 focus = me.transform.position + new Vector3(0, 1.0f, 0);
        float dist = Screen.height > Screen.width ? 18f : 14f, pitch = 36f, targetYaw = Outside ? 0f : 180f;   // 실내: 방 앞(+z)에서, 실외: 집 반대편에서 / 세로 화면은 좌우가 좁아서 더 멀리
        if (decorating)
        {
            // 트리 전체(꼭대기 별까지)가 보이게
            focus = Vector3.Lerp(me.transform.position, TreePos, 0.8f) + new Vector3(0, 3.2f, 0);
            dist = 17f; pitch = 13f;
            if (pointerFocus.HasValue) { focus = Vector3.Lerp(focus, pointerFocus.Value, 0.18f); dist -= 1.6f; }   // 가리키는 곳으로 살짝 다가감
        }
        if (phase == Phase.Lobby)
        {
            // 입장 화면: 캐릭터를 앞에서 가까이 (뒤로 현관문이 보임). 왼쪽 패널을 피해 캐릭터를 오른쪽에
            if (Screen.height > Screen.width) { focus = me.transform.position + new Vector3(0, -0.3f, 0); dist = 5.2f; }   // 세로 화면: 캐릭터를 위쪽에, 아래는 패널
            else { focus = me.transform.position + new Vector3(0.6f, 0.72f, 0); dist = 4.0f; }
            pitch = 8f; targetYaw = 180f;
            me.transform.rotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 0.6f) * 35f, 0);
        }
        camYawCur = snap ? targetYaw : Mathf.LerpAngle(camYawCur, targetYaw, 1 - Mathf.Exp(-dt * 3f));
        var rotq = Quaternion.Euler(pitch, camYawCur, 0);
        var want = focus + rotq * new Vector3(0, 0, -dist);
        if (snap) { cam.transform.position = want; cam.transform.rotation = rotq; return; }
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
        for (int i = 1; i <= 9; i++) if (Key(i.ToString())) SetSel(i - 1);
        float sc = Scroll(); if (Mathf.Abs(sc) > 0.1f) SetSel(Wrap(ornSel + (sc > 0 ? -1 : 1), ornTypes.Count));
        if (Key("up")) SetSel(Wrap(ornSel - 1, ornTypes.Count));
        if (Key("down")) SetSel(Wrap(ornSel + 1, ornTypes.Count));
        if (Key("left") || Key("q")) { colSel = Wrap(colSel - 1, AcPalette.Vivid.Length); RefreshGhost(); }
        if (Key("right") || Key("e")) { colSel = Wrap(colSel + 1, AcPalette.Vivid.Length); RefreshGhost(); }

        pointerFocus = null;
        var mp = ptrPos;
        if (OverUI(mp)) { if (ghost) ghost.SetActive(false); FocusAt(Vector3.zero, false); return; }   // 화면 UI 위
        var ray = cam.ScreenPointToRay(mp);
        // 떼기: 오너먼트를 오른쪽 클릭
        bool removeGesture = MouseDown(1) || (LongPress && !longUsed);   // 오른쪽 클릭 또는 길게 누르기
        if (removeGesture && Physics.Raycast(ray, out var hitO, 100f) && hitO.collider.GetComponentInParent<AcOrnTag>() is AcOrnTag tag)
        {
            longUsed = true; ptrMoved = true;   // 손을 뗄 때 다시 달리지 않게
            RemoveOrn(tag.id); Toast("장식을 뗐어요"); return;
        }
        if (treeCol.Raycast(ray, out var hit, 100f))
        {
            if (IsGarland(ornTypes[ornSel]))
            {
                float gy = hit.point.y + 0.08f;
                GhostGarland(gy);
                pointerFocus = hit.point;
                FocusAt(hit.point, true);
                if (Tap) AddOrn(ornTypes[ornSel], CurColor, new Vector3(TreePos.x, gy, TreePos.z), 0);
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
            if (Tap) AddOrn(ornTypes[ornSel], CurColor, pos, ry);
        }
        else { if (ghost) ghost.SetActive(false); FocusAt(Vector3.zero, false); }
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
        var o = new Orn { id = myId + "_" + DateTime.UtcNow.Ticks.ToString("x"), type = type, color = ColorUtility.ToHtmlStringRGB(c), owner = myId,
                          x = pos.x, y = pos.y, z = pos.z, ry = ry, t = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds };
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
        go.transform.position = new Vector3(o.x, o.y, o.z);
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

    void Toast(string m) { toast = m; toastUntil = Time.time + 5f; }

    // ========================================================
    // 화면 UI
    static Font uiFont;
    void Styles()
    {
        if (sBox != null) return;
        if (uiFont == null) uiFont = Resources.Load<Font>("Fonts/Jua-Regular");   // 둥근 한글 글꼴 (웹에서도 한글이 나오게)
        if (uiFont != null) GUI.skin.font = uiFont;
        var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(1f, 0.98f, 0.93f, 0.94f)); bg.Apply();
        sBox = new GUIStyle(GUI.skin.box) { normal = { background = bg }, padding = new RectOffset(14, 14, 10, 10) };
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f, 0.3f, 0.22f) }, richText = true };
        sSmall = new GUIStyle(sLabel) { fontSize = 13, fontStyle = FontStyle.Normal };
        sBtn = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        sLabelSel = new GUIStyle(sSmall) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.75f, 0.25f, 0.2f) } };
    }

    static float UiK => IsMobile ? Mathf.Max(Mathf.Min(Screen.height / 560f, Screen.width / 400f), 0.6f) : Mathf.Clamp(Screen.height / 860f, 0.75f, 2.2f);   // 휴대폰은 손가락 크기에 맞게 크게
    static float SW => Screen.width / UiK;
    static float SH => Screen.height / UiK;

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(UiK, UiK, 1));
        Styles();
        uiRects.Clear();
        if (!Ready)
        {
            GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.12f, 0.2f, 0.17f)));
            GUI.Label(new Rect(0, SH / 2 - 30, SW, 40), "<size=26><color=#fff3d6>크리스마스 거실</color></size>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(0, SH / 2 + 14, SW, 30), "<color=#d8e8d0>" + loadingMsg + "</color>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            return;
        }
        if (phase == Phase.Lobby) { LobbyGUI(); return; }
        NameTags();
        if (toast != null && Time.time < toastUntil)
        {
            float tw = Mathf.Min(520, SW - 20);
            var r = new Rect(SW / 2 - tw / 2, 18, tw, 40);
            GUI.Box(R(r), "", sBox); GUI.Label(new Rect(r.x + 16, r.y + 9, r.width - 32, 24), toast, sSmall);
        }
        // 대화 기록
        if (chatLog.Count > 0)
        {
            var lr = new Rect(14, 14, 360, 22 * chatLog.Count + 16);
            GUI.Box(R(lr), "", sBox);
            for (int i = 0; i < chatLog.Count; i++) GUI.Label(new Rect(26, 20 + i * 22, 340, 22), chatLog[i], sSmall);
        }
        if (decorating) DecorateBar();
        else
        {
            if (!IsMobile)
            {
                GUI.Box(R(new Rect(14, SH - 46, 640, 34)), "", sBox);
                GUI.Label(new Rect(28, SH - 40, 630, 24), "WASD/클릭 이동 / 트리 근처에서 꾸미기 / Enter 말하기 / Z 인사 / X 하트 / V 만세 / C 꾸미기", sSmall);
            }
            ActionButtons();
        }
        // 친구 가까이: 함께 하기
        var near = NearestRemote(2.0f);
        if (near != null && !decorating)
        {
            var r = new Rect(SW / 2 - 200, SH - (IsMobile ? 150 : 120) - (SW < 640 ? 10 : 0), 400, 60);
            GUI.Box(R(r), "", sBox);
            GUI.Label(new Rect(r.x + 14, r.y + 6, 380, 22), near.net.name + " 님과", sSmall);
            if (GUI.Button(new Rect(r.x + 14, r.y + 28, 120, 26), "하이파이브", sBtn)) { Emote("highfive", near.net.id); FaceTo(near.c.transform.position); }
            if (GUI.Button(new Rect(r.x + 140, r.y + 28, 120, 26), "선물 주기", sBtn)) { Emote("gift", near.net.id); FaceTo(near.c.transform.position); }
            if (GUI.Button(new Rect(r.x + 266, r.y + 28, 120, 26), "인사", sBtn)) { Emote("wave", near.net.id); FaceTo(near.c.transform.position); }
        }
        if (chatting) ChatBox();
        if (showCustom) CustomWindow();
        if (net != null && net.Status != "") GUI.Label(new Rect(SW - 330, 14, 320, 24), net.Status, sSmall);
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
        Tag(me.transform.position, uiName, true, myChat, myChatT, myEmote, myEmoteT);
        foreach (var r in remotes.Values) if (r.net != null) Tag(r.c.transform.position, r.net.name, false, r.net.chat, r.net.chatT, r.net.emote, r.net.emoteT);
    }

    static readonly Dictionary<string, string> EmoteText = new Dictionary<string, string>
        { { "wave", "안녕!" }, { "heart", "♥" }, { "cheer", "메리 크리스마스!" }, { "highfive", "하이파이브!" }, { "gift", "선물이에요!" } };

    void Tag(Vector3 pos, string name, bool isMe, string chat, double chatT, string emote, double emoteT)
    {
        var sp = cam.WorldToScreenPoint(pos + new Vector3(0, 1.55f, 0));
        if (sp.z < 0) return;
        float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
        var size = sTag.CalcSize(new GUIContent(name));
        GUI.Label(new Rect(x - size.x / 2 - 8, y - 12, size.x + 16, 22), name, isMe ? sTagMe : sTag);
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
    void LobbyGUI()
    {
        if (Event.current.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;   // 글 칸 밖을 누르면 화살표로 꾸미기
        float lw = Mathf.Min(440, SW - 20);
        bool portrait = Screen.height > Screen.width;
        var r = portrait ? new Rect((SW - lw) / 2, SH * 0.46f, lw, SH * 0.54f - 10) : new Rect(Mathf.Min(20, (SW - lw) / 2), 16, lw, SH - 32);
        GUI.Box(R(r), "", sBox);
        GUILayout.BeginArea(new Rect(r.x + 16, r.y + 10, r.width - 32, r.height - 20));
        GUILayout.Label(portrait ? "<size=20>크리스마스 거실</size>" : "<size=24>크리스마스 거실</size>", sLabel);
        if (!portrait) { GUILayout.Label("캐릭터를 꾸미고 현관으로 들어가세요. 다 함께 커다란 트리를 꾸며요!", sSmall); GUILayout.Space(8); }
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름", sSmall, GUILayout.Width(60));
        uiName = TextBox("이름을 입력하세요", uiName, 12);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("방 코드", sSmall, GUILayout.Width(60));
        uiPath = TextBox("방 코드 (같은 코드를 쓴 사람끼리 만나요)", uiPath, 20);
        GUILayout.EndHorizontal();
        lobbyScroll = GUILayout.BeginScrollView(lobbyScroll, false, false);
        CustomControls();
        GUILayout.EndScrollView();
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
        float h = narrow ? 216 : 150, y = SH - h - 8;
        GUI.Box(R(new Rect(10, y, SW - 20, h)), "", sBox);
        GUI.Label(new Rect(26, y + 6, SW - 60, 24), IsMobile ? "트리 꾸미기  <size=13>트리를 눌러 달기 / 장식을 길게 눌러 떼기</size>" : "트리 꾸미기  <size=13>클릭 달기 / 오른쪽 클릭 떼기 / 위아래/휠 모양 / 좌우 색</size>", sLabel);
        // 모양 (실제 오너먼트 그림)
        float x = 26, size = 62;
        int perRow = Mathf.Max(1, (int)((SW - (narrow ? 52 : 330)) / (size + 6)));
        int start = Mathf.Clamp(ornSel - perRow / 2, 0, Mathf.Max(0, ornTypes.Count - perRow));
        if (start > 0) GUI.Label(new Rect(x - 14, y + 52, 14, 24), "<", sLabel);
        for (int i = start; i < Mathf.Min(ornTypes.Count, start + perRow); i++)
        {
            var r = new Rect(x, y + 34, size, size);
            bool sel = i == ornSel;
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), AcPalette.Tex(sel ? new Color(0.85f, 0.3f, 0.3f) : new Color(0.86f, 0.8f, 0.72f)));
            if (icons.TryGetValue(ornTypes[i], out var rt) && rt != null) GUI.DrawTexture(r, rt, ScaleMode.ScaleToFit, false);
            if (GUI.Button(r, "", GUIStyle.none)) SetSel(i);
            x += size + 6;
        }
        if (start + perRow < ornTypes.Count) GUI.Label(new Rect(x - 2, y + 52, 14, 24), ">", sLabel);
        var it = AcCatalog.Get(ornTypes[ornSel]);
        GUI.Label(new Rect(26, y + 102, Mathf.Min(420, SW - 52), 22), IsGarland(ornTypes[ornSel]) ? GarlandName(ornTypes[ornSel]) + " - 트리를 누른 높이에 한 바퀴 둘러요" : it != null ? it.desc : ornTypes[ornSel], sSmall);
        // 색: < ■ 이름 >
        float cx = narrow ? 26 : SW - 290, cy = narrow ? y + 128 : y + 40;
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
        GUI.Label(narrow ? new Rect(SW - 120, cy - 6, 110, 22) : new Rect(SW - 150, y + 6, 130, 22), $"장식 {ornObjs.Count}개", sSmall);
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
        customScroll = GUILayout.BeginScrollView(customScroll, false, false, GUILayout.Height(r.height - 100));
        CustomControls();
        GUILayout.EndScrollView();
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
