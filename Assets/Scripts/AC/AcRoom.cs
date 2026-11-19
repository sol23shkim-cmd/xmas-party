// AcRoom.cs — 동물의 숲풍 크리스마스 거실 (모델 미리보기 + 트리 꾸미기)
// StreamingAssets/models/catalog.json 이 있으면 이 장면이 켜져요 (예전 디오라마 대신).
//
// 조작: WASD/방향키 이동 · 트리 가까이 가면 꾸미기 모드
//       꾸미기: 왼쪽 클릭 = 오너먼트 달기, 오른쪽 클릭 = 떼기, 1~9/휠 = 종류, Q/E = 색
//       C = 캐릭터 꾸미기 창 · F2 = 다른 캐릭터(데모) 보이기/숨기기
// 장식 저장: 공유 폴더(입장 때 쓴 경로, PlayerPrefs xmas_path)\tree\ornaments_<내ID>.json — 각자 자기 파일만 씀
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Random = UnityEngine.Random;

public class AcRoom : MonoBehaviour
{
    public static bool Enabled => File.Exists(Path.Combine(Application.streamingAssetsPath, "models", "catalog.json"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || FindAnyObjectByType<AcRoom>() != null) return;
        new GameObject("AcRoom").AddComponent<AcRoom>();
    }

    // ===== 방 크기 (벽 조각 1.6배 = 3.2m 단위) =====
    const float S = 1.6f, SEG = 2f * S;
    const int NX = 6, NZ = 4;                        // 가로 6칸(19.2m), 세로 4칸(12.8m)
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
    string toast; float toastUntil;
    GUIStyle sBox, sLabel, sSmall, sBtn;

    // ========================================================
    void Start()
    {
        Application.targetFrameRate = 60;
        foreach (var c in FindObjectsByType<Camera>()) if (c.gameObject.name != "AcCam") c.gameObject.SetActive(false);
        foreach (var l in FindObjectsByType<Light>()) l.gameObject.SetActive(false);
        foreach (var v in FindObjectsByType<Volume>()) v.enabled = false;   // 템플릿 톤매핑이 색을 바래게 해서 끔

        AcCatalog.Load();
        world = new GameObject("World").transform;
        SetupCamera();
        SetupLights();
        BuildRoom();
        BuildOutdoors();
        BuildFurniture();

        myId = PlayerPrefs.GetString("ac_id", "");
        if (myId == "") { myId = Environment.UserName + "_" + Random.Range(1000, 9999); PlayerPrefs.SetString("ac_id", myId); }
        mine.owner = myId;
        var look = AcLook.Decode(PlayerPrefs.GetString("ac_look", ""));
        me = AcCharacter.Create(look, world);
        me.name = "Me";
        me.transform.position = new Vector3(0, 0, HalfZ - 2.5f);
        me.transform.rotation = Quaternion.Euler(0, 180, 0);
        SpawnDemoNpcs();

        foreach (var it in AcCatalog.ByCat("ornament")) ornTypes.Add(it.name);
        ornTypes.Sort((a, b) => OrnOrder(a).CompareTo(OrnOrder(b)));
        string shared = PlayerPrefs.GetString("xmas_path", "");
        ornDir = (shared != "" && Directory.Exists(shared)) ? Path.Combine(shared, "tree") : Path.Combine(Application.persistentDataPath, "tree");
        try { Directory.CreateDirectory(ornDir); } catch { ornDir = Path.Combine(Application.persistentDataPath, "tree"); Directory.CreateDirectory(ornDir); }
        LoadMine();
        PollOrnaments(true);
        Toast("트리 가까이 가서 오너먼트를 달아 보세요!  (C: 캐릭터 꾸미기)");
    }

    static int OrnOrder(string n) => n.StartsWith("orn_ball") ? 0 : n == "orn_star" || n == "orn_bell" || n == "orn_drop" ? 1 : 2;

    // ========================================================
    // 카메라·빛
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
            string w = ix == 1 || ix == 4 ? "wall_window" : ix == 5 ? "wall_door" : "wall_plain";
            var g = Put(w, new Vector3(-HalfX + SEG * (ix + 0.5f), 0, -HalfZ), 0, S, null, room);
            if (w == "wall_window") { HidePart(g, "glass"); Put("curtain", new Vector3(g.transform.position.x, 1.75f * S - 1.9f, -HalfZ + 0.12f), 0, S * 0.85f, null, room); }
            if (w == "wall_door") Put("wreath", new Vector3(g.transform.position.x, 1.75f * S, -HalfZ + 0.12f), 0, 1.1f, null, room);
        }
        // 옆벽
        for (int iz = 0; iz < NZ; iz++)
        {
            float z = -HalfZ + SEG * (iz + 0.5f);
            var gl = Put(iz == 1 ? "wall_window" : "wall_plain", new Vector3(-HalfX, 0, z), 90, S, null, room);
            var gr = Put(iz == 2 ? "wall_window" : "wall_plain", new Vector3(HalfX, 0, z), -90, S, null, room);
            if (iz == 1) HidePart(gl, "glass");
            if (iz == 2) HidePart(gr, "glass");
        }
        Put("wall_corner", new Vector3(-HalfX, 0, -HalfZ), 0, S, null, room);
        Put("wall_corner", new Vector3(HalfX, 0, -HalfZ), 0, S, null, room);
        // 벽 위 전구 줄
        for (int ix = 0; ix < NX; ix += 1)
            Put("string_lights", new Vector3(-HalfX + SEG * (ix + 0.5f), 3.35f * S, -HalfZ + 0.15f), 0, S * 0.62f, null, room);
    }

    void BuildOutdoors()
    {
        var o = new GameObject("Outdoors").transform; o.SetParent(world, false);
        for (int ix = -3; ix <= 3; ix++)
            for (int iz = 0; iz < 4; iz++)
            {
                Put("ground_snow", new Vector3(ix * 8f, -0.05f, -HalfZ - 4f - iz * 8f), Random.Range(0, 4) * 90, 2f, null, o);
                Put("ground_snow", new Vector3(-HalfX - 4f - iz * 8f, -0.05f, ix * 8f), 0, 2f, null, o);
                Put("ground_snow", new Vector3(HalfX + 4f + iz * 8f, -0.05f, ix * 8f), 0, 2f, null, o);
            }
        string[] trees = { "pine_snowy", "pine_tall", "pine_small", "bare_tree", "birch", "palm_cedar", "round_tree" };
        var rnd = new System.Random(5);
        for (int i = 0; i < 46; i++)
        {
            float side = rnd.Next(3);
            Vector3 p = side == 0 ? new Vector3(-HalfX - 12 + (float)rnd.NextDouble() * (2 * HalfX + 24), 0, -HalfZ - 5 - (float)rnd.NextDouble() * 22)
                      : side == 1 ? new Vector3(-HalfX - 4 - (float)rnd.NextDouble() * 16, 0, -HalfZ - 4 + (float)rnd.NextDouble() * (2 * HalfZ + 8))
                                  : new Vector3(HalfX + 4 + (float)rnd.NextDouble() * 16, 0, -HalfZ - 4 + (float)rnd.NextDouble() * (2 * HalfZ + 8));
            if (Mathf.Abs(p.x) < 6 && p.z > -HalfZ - 9 && p.z < -HalfZ - 2) continue;   // 창밖 눈사람 자리
            Put(trees[rnd.Next(trees.Length)], p, rnd.Next(360), 0.9f + (float)rnd.NextDouble() * 0.6f, null, o);
        }
        Put("house_cottage", new Vector3(-4f, 0, -HalfZ - 16f), 15, 1.2f, null, o);
        Put("snowman_big", new Vector3(-3.6f, 0, -HalfZ - 4.2f), 15, 1.1f, null, o);
        Put("snowman_small", new Vector3(-2.3f, 0, -HalfZ - 3.6f), -10, 1.1f, null, o);
        Put("lamp_post", new Vector3(3.2f, 0, -HalfZ - 3.0f), 0, 1.2f, null, o);
        Put("snow_pile", new Vector3(1.2f, 0, -HalfZ - 5f), 0, 1.4f, null, o);
        Put("snowball_pile", new Vector3(-1.0f, 0, -HalfZ - 3.2f), 0, 1.2f, null, o);
        Put("sled", new Vector3(5.6f, 0, -HalfZ - 4.6f), 70, 1.2f, null, o);
        Put("mailbox", new Vector3(8.8f, 0, -HalfZ - 2.4f), 0, 1.2f, null, o);
        for (int i = -3; i <= 3; i++) Put("fence", new Vector3(i * 2.1f * 1.2f, 0, -HalfZ - 9.5f), 0, 1.2f, null, o);
        Put("igloo", new Vector3(10f, 0, -HalfZ - 12f), -30, 1.5f, null, o);
        Put("bush_snowy", new Vector3(-6f, 0, -HalfZ - 2.5f), 0, 1.3f, null, o);
        Put("bush_holly", new Vector3(6.8f, 0, -HalfZ - 2.5f), 0, 1.3f, null, o);
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
        Put("bookshelf", new Vector3(-HalfX + 0.3f, 0, 1.6f), 90, 1.25f, null, f);
        Put("bookshelf", new Vector3(-HalfX + 0.3f, 0, 3.4f), 90, 1.25f, new Dictionary<string, Color> { { "wood", new Color(0.62f, 0.42f, 0.28f) } }, f);
        Put("poinsettia", new Vector3(-HalfX + 0.6f, 0, -0.6f), 0, 1.3f, null, f);

        // 피아노·시계 (오른쪽 뒤)
        Put("piano", new Vector3(HalfX - SEG * 1.5f + 0.3f, 0, -HalfZ + 0.5f), 0, 1.2f, null, f);
        Put("wall_clock", new Vector3(HalfX - SEG * 2.6f, 0, -HalfZ + 0.3f), 0, 1.25f, null, f);
        Put("poinsettia", new Vector3(HalfX - SEG * 0.5f - 1.1f, 0, -HalfZ + 0.9f), 0, 1.3f, null, f);
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
        Put("gingerbread_man", new Vector3(HalfX - 0.8f, 0, 4.6f), -110, 1.1f, null, f);
        Put("gingerbread_woman", new Vector3(HalfX - 1.6f, 0, 5.3f), -140, 1.1f, null, f);
        Put("reindeer_plush", new Vector3(HalfX - 0.8f, 0, -0.4f), -90, 1.2f, null, f);
        Put("candy_cane_big", new Vector3(HalfX - 0.5f, 0, 0.8f), -90, 1.3f, null, f);
        Put("mistletoe", new Vector3(HalfX - SEG * 0.5f, 3.2f * S, -HalfZ + 1.6f), 0, 1.4f, null, f);
        Put("tv_retro", new Vector3(HalfX - 0.6f, 0, -2.6f), -90, 1.15f, null, f);
        Put("ottoman", new Vector3(HalfX - 2.2f, 0, -2.6f), 0, 1.1f, null, f);
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

    bool Key(string k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; if (kb == null) return false;
        switch (k)
        {
            case "q": return kb.qKey.wasPressedThisFrame; case "e": return kb.eKey.wasPressedThisFrame;
            case "c": return kb.cKey.wasPressedThisFrame; case "f2": return kb.f2Key.wasPressedThisFrame;
            case "esc": return kb.escapeKey.wasPressedThisFrame;
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
        float dt = Time.deltaTime;
        if (!showCustom) MovePlayer(dt);
        UpdateNpcs(dt);
        UpdateCamera(dt, false);

        if (Key("c")) showCustom = !showCustom;
        if (Key("f2")) { showNpcs = !showNpcs; foreach (var n in npcs) n.gameObject.SetActive(showNpcs); }
        if (Key("esc")) showCustom = false;

        float treeDist = Vector2.Distance(new Vector2(me.transform.position.x, me.transform.position.z), new Vector2(TreePos.x, TreePos.z));
        decorating = treeDist < 5.2f && !showCustom;
        if (decorating) Decorate(); else if (ghost) ghost.SetActive(false);

        if (Time.time > nextPoll) { nextPoll = Time.time + 2f; PollOrnaments(false); }
    }

    void MovePlayer(float dt)
    {
        var a = MoveAxis();
        Vector3 dir = new Vector3(a.x, 0, a.y);
        if (dir.sqrMagnitude > 1) dir.Normalize();
        float spd = 3.6f;
        var p = me.transform.position + dir * spd * dt;
        p.x = Mathf.Clamp(p.x, -HalfX + 0.7f, HalfX - 0.7f);
        p.z = Mathf.Clamp(p.z, -HalfZ + 0.9f, HalfZ - 0.5f);
        var d2 = new Vector2(p.x - TreePos.x, p.z - TreePos.z);
        if (d2.magnitude < 2.6f) { d2 = d2.normalized * 2.6f; p.x = TreePos.x + d2.x; p.z = TreePos.z + d2.y; }
        me.speed = dir.magnitude * spd;
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
            c.transform.position = p + step; c.speed = 1.6f;
            c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-dt * 8));
        }
    }

    void UpdateCamera(float dt, bool snap)
    {
        Vector3 focus = me.transform.position + new Vector3(0, 1.0f, 0);
        if (decorating) focus = Vector3.Lerp(focus, TreePos + new Vector3(0, 2.6f, 0), 0.55f);
        float dist = decorating ? 15.5f : 14f, pitch = decorating ? 24f : 36f;
        var want = focus + Quaternion.Euler(pitch, 0, 0) * new Vector3(0, 0, -dist);
        cam.transform.position = Vector3.SmoothDamp(cam.transform.position, want, ref camVel, 0.25f);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.Euler(pitch, 0, 0), 1 - Mathf.Exp(-dt * 5f));
    }

    // ========================================================
    // 트리 꾸미기
    Color CurColor => AcCatalog.Palette.Count > 0 ? AcCatalog.Palette[colSel % AcCatalog.Palette.Count] : Color.red;

    void Decorate()
    {
        if (ornTypes.Count == 0 || treeCol == null) return;
        for (int i = 1; i <= 9; i++) if (Key(i.ToString())) SetSel(i - 1);
        float sc = Scroll(); if (Mathf.Abs(sc) > 0.1f) SetSel((ornSel + (sc > 0 ? -1 : 1) + ornTypes.Count) % ornTypes.Count);
        if (Key("q")) { colSel = (colSel - 1 + AcCatalog.Palette.Count) % AcCatalog.Palette.Count; RefreshGhost(); }
        if (Key("e")) { colSel = (colSel + 1) % AcCatalog.Palette.Count; RefreshGhost(); }

        var mp = MousePos();
        if (mp.y < 120) { if (ghost) ghost.SetActive(false); return; }   // 아래 UI 위
        var ray = cam.ScreenPointToRay(mp);
        // 떼기: 오너먼트를 오른쪽 클릭
        if (MouseDown(1) && Physics.Raycast(ray, out var hitO, 100f) && hitO.collider.GetComponentInParent<AcOrnTag>() is AcOrnTag tag)
        {
            RemoveOrn(tag.id); return;
        }
        if (treeCol.Raycast(ray, out var hit, 100f))
        {
            if (ghost == null || ghost.name != ornTypes[ornSel]) RefreshGhost();
            ghost.SetActive(true);
            var pos = hit.point + hit.normal * 0.02f;
            float ry = Mathf.Atan2(hit.normal.x, hit.normal.z) * Mathf.Rad2Deg;
            ghost.transform.position = pos;
            ghost.transform.rotation = Quaternion.Euler(0, ry, 0);
            if (MouseDown(0)) AddOrn(ornTypes[ornSel], CurColor, pos, ry);
        }
        else if (ghost) ghost.SetActive(false);
    }

    void SetSel(int i) { ornSel = Mathf.Clamp(i, 0, ornTypes.Count - 1); RefreshGhost(); }

    void RefreshGhost()
    {
        if (ghost) Destroy(ghost);
        if (ornTypes.Count == 0) return;
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
        mine.placed.Add(o);
        SaveMine();
        ShowOrn(o);
    }

    void RemoveOrn(string id)
    {
        int idx = mine.placed.FindIndex(o => o.id == id);
        if (idx >= 0) mine.placed.RemoveAt(idx); else if (!mine.removed.Contains(id)) mine.removed.Add(id);
        SaveMine();
        if (ornObjs.TryGetValue(id, out var go)) { Destroy(go); ornObjs.Remove(id); ornData.Remove(id); }
    }

    void ShowOrn(Orn o)
    {
        if (ornObjs.ContainsKey(o.id)) return;
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

    string MyFile => Path.Combine(ornDir, "ornaments_" + Safe(myId) + ".json");
    static string Safe(string s) { foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_'); return s; }

    void LoadMine()
    {
        try { if (File.Exists(MyFile)) mine = JsonUtility.FromJson<OrnFile>(File.ReadAllText(MyFile)) ?? new OrnFile(); } catch { }
        mine.owner = myId;
    }

    void SaveMine()
    {
        try
        {
            var tmp = MyFile + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(mine));
            if (File.Exists(MyFile)) File.Replace(tmp, MyFile, null); else File.Move(tmp, MyFile);
        }
        catch (Exception e) { Toast("저장 실패: " + e.Message); }
    }

    void PollOrnaments(bool force)
    {
        string[] files;
        try { files = Directory.GetFiles(ornDir, "ornaments_*.json"); } catch { return; }
        bool changed = force;
        foreach (var f in files) { var t = File.GetLastWriteTimeUtc(f); if (!seen.TryGetValue(f, out var old) || old != t) { seen[f] = t; changed = true; } }
        if (!changed) return;
        var all = new Dictionary<string, Orn>(); var removed = new HashSet<string>();
        foreach (var f in files)
        {
            try
            {
                var of = JsonUtility.FromJson<OrnFile>(File.ReadAllText(f));
                if (of == null) continue;
                foreach (var o in of.placed) all[o.id] = o;
                foreach (var r in of.removed) removed.Add(r);
            }
            catch { }
        }
        foreach (var r in removed) all.Remove(r);
        foreach (var kv in all) ShowOrn(kv.Value);
        var gone = new List<string>();
        foreach (var kv in ornObjs) if (!all.ContainsKey(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone) { Destroy(ornObjs[id]); ornObjs.Remove(id); ornData.Remove(id); }
    }

    void Toast(string m) { toast = m; toastUntil = Time.time + 5f; }

    // ========================================================
    // 화면 UI
    void Styles()
    {
        if (sBox != null) return;
        var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(1f, 0.98f, 0.93f, 0.94f)); bg.Apply();
        sBox = new GUIStyle(GUI.skin.box) { normal = { background = bg }, padding = new RectOffset(14, 14, 10, 10) };
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f, 0.3f, 0.22f) }, richText = true };
        sSmall = new GUIStyle(sLabel) { fontSize = 13, fontStyle = FontStyle.Normal };
        sBtn = new GUIStyle(GUI.skin.button) { fontSize = 14 };
    }

    void OnGUI()
    {
        Styles();
        if (toast != null && Time.time < toastUntil)
        {
            var r = new Rect(Screen.width / 2 - 260, 18, 520, 40);
            GUI.Box(r, "", sBox); GUI.Label(new Rect(r.x + 16, r.y + 9, r.width - 32, 24), toast, sSmall);
        }
        if (decorating) DecorateBar();
        else
        {
            GUI.Box(new Rect(14, Screen.height - 46, 520, 34), "", sBox);
            GUI.Label(new Rect(28, Screen.height - 40, 500, 24), "WASD 이동 · 트리 근처에서 꾸미기 · C 캐릭터 꾸미기 · F2 데모 친구", sSmall);
        }
        if (showCustom) CustomWindow();
    }

    void DecorateBar()
    {
        float h = 108, y = Screen.height - h - 10;
        GUI.Box(new Rect(10, y, Screen.width - 20, h), "", sBox);
        GUI.Label(new Rect(26, y + 8, 600, 24), "🎄 트리 꾸미기  <size=13>왼쪽 클릭 달기 · 오른쪽 클릭 떼기 · 휠/1~9 종류 · Q/E 색</size>", sLabel);
        float x = 26;
        for (int i = 0; i < ornTypes.Count; i++)
        {
            var it = AcCatalog.Get(ornTypes[i]);
            string label = it != null ? it.desc : ornTypes[i];
            var r = new Rect(x, y + 40, 78, 26);
            bool sel = i == ornSel;
            if (GUI.Toggle(r, sel, label, sBtn) && !sel) SetSel(i);
            x += 82;
            if (x > Screen.width - 120) break;
        }
        x = 26;
        for (int i = 0; i < AcCatalog.Palette.Count; i++)
        {
            var r = new Rect(x, y + 74, 26, 22);
            var old = GUI.color; GUI.color = AcCatalog.Palette[i];
            if (GUI.Button(r, i == colSel ? "●" : "")) { colSel = i; RefreshGhost(); }
            GUI.color = old; x += 30;
        }
        GUI.Label(new Rect(x + 16, y + 74, 300, 24), $"장식 {ornObjs.Count}개", sSmall);
    }

    // 캐릭터 꾸미기
    static readonly string[] SpeciesKo = { "곰", "고양이", "토끼", "강아지", "여우", "판다", "사자", "돼지", "사람" };
    static readonly string[] SpeciesId = { "bear", "cat", "rabbit", "dog", "fox", "panda", "lion", "pig", "human" };
    static readonly string[] HatId = { "santa", "beanie", "earmuffs", "antlers", "tophat", "elf", "none" };
    static readonly string[] HatKo = { "산타", "털모자", "귀마개", "뿔", "중절모", "요정", "없음" };

    void CustomWindow()
    {
        var r = new Rect(Screen.width - 430, 20, 410, 560);
        GUI.Box(r, "", sBox);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 12, r.width - 28, r.height - 24));
        GUILayout.Label("캐릭터 꾸미기", sLabel);
        var l = AcLook.Decode(me.look.Encode());
        bool ch = false;
        GUILayout.Label("종류", sSmall);
        int si = Array.IndexOf(SpeciesId, l.species);
        int ns = GUILayout.SelectionGrid(Mathf.Max(si, 0), SpeciesKo, 5, sBtn);
        if (ns != si)
        {
            l.species = SpeciesId[ns]; ch = true;
            var sp = AcCatalog.Species(l.species);
            if (sp != null && sp.fur != null && sp.fur.Length >= 3) l.fur = new Color(sp.fur[0], sp.fur[1], sp.fur[2]);
        }
        if (l.species == "human")
        {
            int g = GUILayout.Toolbar(l.female ? 0 : 1, new[] { "여자", "남자" }, sBtn);
            if ((g == 0) != l.female) { l.female = g == 0; l.hair = l.female ? "long" : "short"; l.face = "f1"; ch = true; }
            var hairs = l.female ? AcCatalog.Data.hairFemale : AcCatalog.Data.hairMale;
            string[] hairKo = l.female ? new[] { "긴머리", "단발", "양갈래", "포니테일", "올림머리" } : new[] { "짧은머리", "덥수룩" };
            int hi = Mathf.Max(0, Array.IndexOf(hairs, l.hair));
            int nh = GUILayout.Toolbar(hi, hairKo, sBtn);
            if (nh != hi) { l.hair = hairs[nh]; ch = true; }
            string[] faces = l.female ? new[] { "f1", "f2", "f3", "f4", "f5" } : new[] { "f1", "f2" };
            string[] faceKo = l.female ? new[] { "또랑", "방긋", "반짝", "졸린", "속눈썹" } : new[] { "또랑", "방긋" };
            int fi = Mathf.Max(0, Array.IndexOf(faces, l.face));
            int nf = GUILayout.Toolbar(fi, faceKo, sBtn);
            if (nf != fi) { l.face = faces[nf]; ch = true; }
        }
        GUILayout.Label("모자", sSmall);
        int hti = Mathf.Max(0, Array.IndexOf(HatId, l.hat));
        int nht = GUILayout.SelectionGrid(hti, HatKo, 4, sBtn);
        if (nht != hti) { l.hat = HatId[nht]; ch = true; }
        ch |= ColorRow(l.species == "human" ? "피부" : "털", ref l.fur);
        ch |= ColorRow("스웨터", ref l.sweater);
        ch |= ColorRow("목도리", ref l.scarf);
        if (l.hat != "none") ch |= ColorRow("모자", ref l.hatColor);
        if (l.species == "human") ch |= ColorRow("머리", ref l.hairColor);
        if (l.species == "lion") ch |= ColorRow("갈기", ref l.mane);
        GUILayout.Space(8);
        if (GUILayout.Button("완료", sBtn, GUILayout.Height(30))) showCustom = false;
        GUILayout.EndArea();
        if (ch) { me.Rebuild(l); PlayerPrefs.SetString("ac_look", l.Encode()); PlayerPrefs.Save(); }
    }

    static readonly Color[] SkinTones = { new Color(1f, 0.86f, 0.76f), new Color(0.95f, 0.78f, 0.62f), new Color(0.82f, 0.62f, 0.45f), new Color(0.6f, 0.42f, 0.3f),
                                           new Color(0.72f, 0.5f, 0.34f), new Color(0.98f, 0.97f, 0.96f), new Color(0.3f, 0.28f, 0.3f), new Color(0.99f, 0.75f, 0.75f) };
    bool ColorRow(string label, ref Color c)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sSmall, GUILayout.Width(56));
        bool changed = false;
        var list = new List<Color>(AcCatalog.Palette);
        if (label == "털" || label == "피부") list.InsertRange(0, SkinTones);
        int n = 0;
        foreach (var col in list)
        {
            if (n++ >= 18) break;
            var old = GUI.color; GUI.color = col;
            if (GUILayout.Button(ApproxEq(col, c) ? "●" : "", GUILayout.Width(17), GUILayout.Height(18))) { c = col; changed = true; }
            GUI.color = old;
        }
        GUILayout.EndHorizontal();
        return changed;
    }
    static bool ApproxEq(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.03f;
}

public class AcOrnTag : MonoBehaviour { public string id; }

public class AcOrnSwing : MonoBehaviour
{
    float ph, baseY;
    void Start() { ph = UnityEngine.Random.value * 10; baseY = transform.eulerAngles.y; }
    void Update() { transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + ph) * 3f, baseY, Mathf.Sin(Time.time * 1.7f + ph) * 4f); }
}
