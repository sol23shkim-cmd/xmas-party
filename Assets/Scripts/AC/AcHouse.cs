// AcHouse.cs - 큰 집 (ㄷ자): 앞줄 = 방2 + 트리 거실 + 방2, 뒷줄 = 양쪽 날개 뒤에 방 2개씩 (모두 8개).
// 트리 거실 뒤는 눈 덮인 안뜰. 방 사이는 아치 문, 앞·뒷줄 사이도 문으로 이어짐.
// 카메라는 남쪽(+z)에서 보므로, 나와 카메라 사이에 있는 가로 벽은 낮게 내려서 안이 보이게 함 (잘라 보기).
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    const int RoomSegs = 3;                                   // 방 하나 가로 3칸 (9.6m)
    static float RoomW => RoomSegs * SEG;
    static float RoomD => NZ * SEG;                           // 방 세로 (12.8m)
    static float HouseHalf => HalfX + 2 * RoomW;              // 집 반폭 (28.8m)
    static float HouseZ0 => -HalfZ - RoomD;                   // 뒷줄 방 뒷벽 (-19.2m)
    static float ArchZ => -HalfZ + SEG * 2.5f;                // 앞줄 방 사이 아치 문 위치
    static float BackArchZ => HouseZ0 + SEG * 2.5f;           // 뒷줄 방 사이 아치 문 위치
    // 앞쪽(+z) 낮은 벽의 출입구: 트리 거실 앞 한 칸
    static float FrontGapX0 => -HalfX + SEG * 3;
    static float FrontGapX1 => -HalfX + SEG * 4;

    // 방 꾸미기 좌표는 예전 큰 방(12.8 x 16m) 기준으로 적혀 있어서, 벽 가까운 것은 벽과의 거리를 지키고 가운데는 살짝 모아 줌
    const float OldHW = 6.4f, OldHZ = 8f;
    static float MapAxis(float v, float oldH, float newH, float keep)
    {
        float a = Mathf.Abs(v), s = Mathf.Sign(v);
        float wallBand = oldH - 1f;   // 이보다 벽에 가까우면 벽 기준
        if (a <= keep) return v;
        if (a >= wallBand) return s * (newH - (oldH - a));
        float t = (a - keep) / (wallBand - keep);
        return s * Mathf.Lerp(keep, newH - (oldH - wallBand), t);
    }
    static Vector3 V(float x, float y, float z) => new Vector3(MapAxis(x, OldHW, RoomW / 2, 2.4f), y, MapAxis(z, OldHZ, HalfZ, 2.5f));

    class RoomDef
    {
        public string name, floor = "floor_planks";
        public Color wall, wain, floorTint;
        public System.Action<Vector3, Transform> fill;        // 방 가운데(바닥) 기준으로 가구 채우기
    }

    class Area { public string name; public Rect r; public RoomDef def; public GameObject root; }   // r: x → x, y → z
    readonly List<Area> areasR = new List<Area>();
    string curArea = "";
    float nextCull;

    // 방 배치 (x0, z0) — 앞줄 z0 = -HalfZ, 뒷줄 z0 = HouseZ0
    (RoomDef def, float x0, float z0)[] Layout()
    {
        RoomDef R(string n, string wall, string wain, System.Action<Vector3, Transform> f, string floor = "floor_planks", string ft = null)
            => new RoomDef { name = n, wall = C(wall), wain = C(wain), fill = f, floor = floor, floorTint = ft != null ? C(ft) : default };
        float fz = -HalfZ, bz = HouseZ0;
        return new[]
        {
            (R("부엌", "EAF4EC", "6E9C87", FillKitchen, "floor_tiles"), -HalfX - RoomW, fz),
            (R("서재", "EFE6D2", "5B3E2E", FillLibrary), -HalfX - 2 * RoomW, fz),
            (R("쿠키 공방", "FBEBDD", "A8693F", FillBakery), -HalfX - RoomW, bz),
            (R("겨울 온실", "E3F1F6", "8FB3A2", FillGreenhouse, "floor_tiles", "DCE6DA"), -HalfX - 2 * RoomW, bz),
            (R("놀이방", "FFF0F3", "D97C8C", FillPlayroom), HalfX, fz),
            (R("음악실", "F6EEDB", "8C5A3C", FillMusic), HalfX + RoomW, fz),
            (R("선물 포장실", "FDF3DC", "B8463F", FillGiftRoom), HalfX, bz),
            (R("침실", "ECEBF7", "7D7BAE", FillBedroom), HalfX + RoomW, bz),
        };
    }

    static Color C(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    Dictionary<string, Color> T(params (string k, string hex)[] kv) { var d = new Dictionary<string, Color>(); foreach (var (k, h) in kv) d[k] = C(h); return d; }

    // ===== 벽 조각 =====
    class WallPiece
    {
        public Vector3 pos; public bool alongX; public string kind;   // plain window door arch
        public bool exterior, lowBase; public float cur = 1f, baseH = 1f;
        public readonly List<GameObject> gos = new List<GameObject>();
        public readonly List<GameObject> decos = new List<GameObject>();
    }
    readonly List<WallPiece> walls = new List<WallPiece>();
    readonly List<(Vector2 a, Vector2 b)> wallSegs = new List<(Vector2, Vector2)>();   // 부딪히는 벽 선분 (x,z)
    readonly List<(string name, GameObject go)> builtObjs = new List<(string, GameObject)>();
    bool recordPuts;
    const float DoorHalf = 0.95f;

    Area AreaRect(Vector3 p) { foreach (var a in areasR) if (a.r.Contains(new Vector2(p.x, p.z))) return a; return null; }
    string AreaAt(Vector3 p) { var a = AreaRect(p); return a != null ? a.name : (InHall(p) ? "트리 거실" : ""); }
    static bool InHall(Vector3 p) => Mathf.Abs(p.x) < HalfX && p.z > -HalfZ && p.z < HalfZ;
    bool InHouse(Vector3 p) => InHall(p) || AreaRect(p) != null;
    bool InHouseStatic(float x, float z) => InHouse(new Vector3(x, 0, z));

    // 집 바깥까지의 거리 (안이면 0)
    float DistToHouse(Vector3 p)
    {
        float best = float.MaxValue;
        void Try(float x0, float z0, float x1, float z1)
        {
            float dx = Mathf.Max(x0 - p.x, 0, p.x - x1), dz = Mathf.Max(z0 - p.z, 0, p.z - z1);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        Try(-HalfX, -HalfZ, HalfX, HalfZ);
        foreach (var a in areasR) Try(a.r.xMin, a.r.yMin, a.r.xMax, a.r.yMax);
        return best;
    }

    Dictionary<string, Color> TintAt(float x, float z)
    {
        if (InHall(new Vector3(x, 0, z))) return null;
        var a = AreaRect(new Vector3(x, 0, z));
        if (a != null) return new Dictionary<string, Color> { { "wall", a.def.wall }, { "wainscot", a.def.wain } };
        return new Dictionary<string, Color> { { "wall", C("F3E6D3") }, { "wainscot", C("8C5A3C") } };   // 바깥 벽
    }

    // 멀리 있는 방은 꺼서 휴대폰에서도 가볍게
    void CullRooms()
    {
        if (Time.unscaledTime < nextCull || me == null) return;
        nextCull = Time.unscaledTime + 0.3f;
        var p = me.transform.position; float reach = IsMobile ? 16f : 26f;
        foreach (var a in areasR)
        {
            float dx = Mathf.Max(a.r.xMin - p.x, 0, p.x - a.r.xMax), dz = Mathf.Max(a.r.yMin - p.z, 0, p.z - a.r.yMax);
            bool on = Mathf.Sqrt(dx * dx + dz * dz) < reach || treeOverview;
            if (a.root.activeSelf != on) a.root.SetActive(on);
        }
    }

    WallPiece AddWall(Vector3 pos, bool alongX, string kind, bool lowBase = false)
    {
        var w = new WallPiece { pos = pos, alongX = alongX, kind = kind, lowBase = lowBase, baseH = lowBase ? 0.3f : 1f };
        w.cur = w.baseH;
        // 양쪽 방 색
        Vector3 nrm = alongX ? Vector3.forward : Vector3.right;
        Vector3 pA = pos + nrm * 0.5f, pB = pos - nrm * 0.5f;
        bool inA = InHouse(pA), inB = InHouse(pB);
        w.exterior = inA != inB;
        string model = kind == "window" ? "wall_window_big" : kind == "door" || kind == "arch" ? "wall_door" : "wall_plain";
        var house = houseTr;
        for (int side = 0; side < 2; side++)
        {
            var p = side == 0 ? pA : pB;
            float ry = alongX ? (side == 0 ? 0 : 180) : (side == 0 ? 90 : -90);
            var g = Put(model, pos + (side == 0 ? nrm : -nrm) * 0.06f, ry, S, TintAt(p.x, p.z), house);
            if (model == "wall_window_big") HidePart(g, "glass");
            bool realDoor = kind == "door" && side == 0 && Mathf.Abs(pos.x - DoorPos.x) < 0.1f && Mathf.Abs(pos.z - DoorPos.z) < 0.1f;
            if (model == "wall_door")
            {
                if (realDoor) doorTr = AcmLoader.Find(g, "door");
                else HidePart(g, "door");
            }
            if (lowBase) g.transform.localScale = new Vector3(S, S * w.baseH, S);
            w.gos.Add(g);
        }
        // 부딪히는 선분 (문은 가운데를 비움)
        Vector2 c = new Vector2(pos.x, pos.z), u = alongX ? Vector2.right : Vector2.up;
        float h = SEG / 2f;
        if (kind == "door" || kind == "arch") { wallSegs.Add((c - u * h, c - u * DoorHalf)); wallSegs.Add((c + u * DoorHalf, c + u * h)); }
        else wallSegs.Add((c - u * h, c + u * h));
        walls.Add(w);
        return w;
    }
    Transform houseTr;

    void BuildHouse()
    {
        houseTr = new GameObject("House").transform; houseTr.SetParent(world, false);
        areasR.Clear();
        foreach (var (d, x0, z0) in Layout())
        {
            var room = new GameObject(d.name).transform; room.SetParent(houseTr, false);
            var a = new Area { name = d.name, r = new Rect(x0, z0, RoomW, RoomD), def = d, root = room.gameObject };
            areasR.Add(a);
        }
        recordPuts = true;
        // 바닥·가구·조명
        foreach (var a in areasR)
        {
            var d = a.def; var room = a.root.transform;
            var floorT = d.floorTint.a > 0 ? new Dictionary<string, Color> { { "floor", d.floorTint } } : null;
            for (int ix = 0; ix < RoomSegs; ix++)
                for (int iz = 0; iz < NZ; iz++)
                    Put(d.floor, new Vector3(a.r.x + SEG * (ix + 0.5f), 0, a.r.y + SEG * (iz + 0.5f)), (ix + iz) % 2 * 90, S, floorT, room);
            var c = new Vector3(a.r.center.x, 0, a.r.center.y);
            d.fill(c, room);
            PointLight(new Vector3(c.x, 4.5f, c.z), new Color(1f, 0.9f, 0.75f), 11f, 0.5f);
            // 뒷벽 위 전구 줄
            for (int ix = 0; ix < RoomSegs; ix++)
                Put("string_lights", new Vector3(a.r.x + SEG * (ix + 0.5f), 3.35f * S, a.r.y + 0.15f), 0, S * 0.62f, null, room);
        }
        // 트리 거실 뒷벽 (안뜰 쪽: 큰 창 2개 + 진짜 문) + 전구 줄 + 문 위 화환
        for (int ix = 0; ix < NX; ix++)
        {
            float x = -HalfX + SEG * (ix + 0.5f);
            string k = ix == NX - 1 ? "door" : ix == 1 || ix == 3 ? "window" : "plain";
            AddWall(new Vector3(x, 0, -HalfZ), true, k);
            Put("string_lights", new Vector3(x, 3.35f * S, -HalfZ + 0.15f), 0, S * 0.62f, null, houseTr);
            if (k == "door") Put("wreath", new Vector3(x, 1.75f * S + 1.2f, -HalfZ + 0.16f), 0, 1.1f, null, houseTr);
        }
        foreach (int sd in new[] { -1, 1 })
        {
            float xin = sd * HalfX, xmid = sd * (HalfX + RoomW), xout = sd * HouseHalf;
            // 날개: 앞·뒷줄 사이 벽 (방마다 가운데 칸이 문)
            for (int k = 0; k < 2 * RoomSegs; k++)
            {
                float x = sd < 0 ? -HalfX - SEG * (k + 0.5f) : HalfX + SEG * (k + 0.5f);
                AddWall(new Vector3(x, 0, -HalfZ), true, k % RoomSegs == 1 ? "arch" : "plain");
            }
            // 날개 뒷벽 (바깥, 큰 창)
            for (int k = 0; k < 2 * RoomSegs; k++)
            {
                float x = sd < 0 ? -HalfX - SEG * (k + 0.5f) : HalfX + SEG * (k + 0.5f);
                AddWall(new Vector3(x, 0, HouseZ0), true, k % RoomSegs == 1 ? "plain" : "window");
            }
            for (int iz = 0; iz < NZ; iz++)
            {
                float zf = -HalfZ + SEG * (iz + 0.5f), zb = HouseZ0 + SEG * (iz + 0.5f);
                // 트리 거실 옆 (앞줄) / 안뜰 쪽 (뒷줄)
                AddWall(new Vector3(xin, 0, zf), false, iz == 2 ? "arch" : "plain");
                AddWall(new Vector3(xin, 0, zb), false, iz == 2 ? "arch" : iz == 0 || iz == 3 ? "window" : "plain");
                // 날개 가운데 칸막이
                AddWall(new Vector3(xmid, 0, zf), false, iz == 2 ? "arch" : "plain");
                AddWall(new Vector3(xmid, 0, zb), false, iz == 2 ? "arch" : "plain");
                // 집 양 끝 (바깥, 큰 창)
                AddWall(new Vector3(xout, 0, zf), false, iz == 1 || iz == 2 ? "window" : "plain");
                AddWall(new Vector3(xout, 0, zb), false, iz == 1 || iz == 2 ? "window" : "plain");
            }
        }
        // 앞쪽(+z)은 카메라가 넘겨볼 수 있게 낮은 벽 (트리 거실 앞 한 칸은 출입구)
        for (float x = -HouseHalf + SEG / 2; x < HouseHalf; x += SEG)
        {
            if (x > FrontGapX0 && x < FrontGapX1) continue;
            AddWall(new Vector3(x, 0, HalfZ), true, "plain", true);
        }
        // 모서리 기둥
        var corners = new HashSet<Vector2>();
        foreach (float x in new[] { -HouseHalf, -HalfX - RoomW, -HalfX, HalfX, HalfX + RoomW, HouseHalf })
            foreach (float z in new[] { HouseZ0, -HalfZ, HalfZ })
            {
                if (Mathf.Abs(x) < HalfX + 0.1f && z < -HalfZ - 1) { }   // 안뜰 쪽 모서리도 둠
                corners.Add(new Vector2(x, z));
            }
        foreach (var cp in corners)
        {
            var g = Put("wall_corner", new Vector3(cp.x, 0, cp.y), 0, S, null, houseTr); frontPosts.Add(g);
            if (cp.y > HalfZ - 0.1f) g.transform.localScale = new Vector3(S, S * 0.32f, S);
        }
        foreach (var gx in new[] { FrontGapX0, FrontGapX1 })
        {
            var post = Put("wall_corner", new Vector3(gx, 0, HalfZ + 0.06f), 0, S, null, houseTr); post.transform.localScale = new Vector3(S * 1.3f, S * 0.42f, S * 1.3f);
            frontPosts.Add(post);
            frontPosts.Add(Put("lantern_ground", new Vector3(gx, S * 0.42f * 3.5f, HalfZ + 0.06f), 0, 0.9f, null, houseTr));
        }
    }

    // 벽에 붙은 장식(전구 줄·선반·액자·시계 등)을 그 벽 조각에 묶음 (벽을 낮추면 같이 숨김)
    static readonly string[] WallDecoNames = { "string_lights", "kitchen_shelf", "picture_frame", "wall_clock", "bell_pair", "wreath", "mistletoe" };
    void AttachWallDecos()
    {
        recordPuts = false;
        foreach (var (name, go) in builtObjs)
        {
            if (go == null) continue;
            bool deco = false; foreach (var n in WallDecoNames) if (name == n) deco = true;
            if (!deco) continue;
            var p = go.transform.position;
            WallPiece best = null; float bd = 1.2f;
            foreach (var w in walls)
            {
                float perp = w.alongX ? Mathf.Abs(p.z - w.pos.z) : Mathf.Abs(p.x - w.pos.x);
                float along = w.alongX ? Mathf.Abs(p.x - w.pos.x) : Mathf.Abs(p.z - w.pos.z);
                if (along > SEG / 2 + 0.2f) continue;
                if (perp < bd) { bd = perp; best = w; }
            }
            if (best != null) best.decos.Add(go);
            else looseDecos.Add(go);
        }
        builtObjs.Clear();
    }
    readonly List<GameObject> looseDecos = new List<GameObject>();
    readonly List<GameObject> frontPosts = new List<GameObject>();   // 출입구 기둥·등불 (트리를 볼 땐 숨김)

    // 잘라 보기: 나와 카메라 사이의 가로 벽은 낮게, 트리 전경 볼 때는 안쪽 벽과 벽 장식을 모두 숨김
    void UpdateWalls(float dt)
    {
        if (me == null || walls.Count == 0) return;
        var p = me.transform.position;
        bool overview = treeOverview;
        foreach (var w in walls)
        {
            float target = w.baseH;
            if (overview) target = 0f;   // 트리 전경: 바깥벽까지 모든 벽을 숨김
            else if (w.alongX && !w.lowBase && phase != Phase.Lobby)
            {
                bool between = w.pos.z > p.z + 0.25f && w.pos.z < p.z + 15f && Mathf.Abs(w.pos.x - p.x) < 15f;
                if (between) target = 0.3f;
            }
            if (Mathf.Abs(w.cur - target) < 0.001f) continue;
            w.cur = Mathf.MoveTowards(w.cur, target, dt * 3.5f);
            foreach (var g in w.gos)
            {
                bool vis = w.cur > 0.02f;
                if (g.activeSelf != vis) g.SetActive(vis);
                g.transform.localScale = new Vector3(S, S * Mathf.Max(0.02f, w.cur), S);
            }
        }
        // 벽 장식: 전경 볼 때는 모두 숨김, 아니면 벽이 다 올라와 있을 때만
        foreach (var w in walls)
            foreach (var d in w.decos) if (d) { bool vis = !overview && w.cur > 0.92f; if (d.activeSelf != vis) d.SetActive(vis); }
        if (overview != decosHidden) { decosHidden = overview; foreach (var d in looseDecos) if (d) d.SetActive(!overview); }
        foreach (var g in frontPosts) { if (!g) continue; bool gate = g.transform.position.z > HalfZ - 0.1f && g.transform.position.x > FrontGapX0 - 0.1f && g.transform.position.x < FrontGapX1 + 0.1f; bool vis = !(overview || (decorating && gate)); if (g.activeSelf != vis) g.SetActive(vis); }
    }
    bool decosHidden;

    // ===== 벽에 부딪히기 (원 vs 선분, 지나쳐 버리지 않게 이전 위치 쪽으로) =====
    Vector3 WallCollide(Vector3 prev, Vector3 p, float r)
    {
        for (int it = 0; it < 2; it++)
            foreach (var (a, b) in wallSegs)
            {
                if (p.x < Mathf.Min(a.x, b.x) - r - 0.2f || p.x > Mathf.Max(a.x, b.x) + r + 0.2f || p.z < Mathf.Min(a.y, b.y) - r - 0.2f || p.z > Mathf.Max(a.y, b.y) + r + 0.2f) continue;
                bool alongX = Mathf.Abs(a.y - b.y) < 0.01f;
                float rr = r + 0.12f;
                if (alongX)
                {
                    float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z = a.y;
                    float qx = Mathf.Clamp(p.x, x0, x1);
                    float side = Mathf.Sign(prev.z - z); if (side == 0) side = 1;
                    float dz = p.z - z;
                    if (p.x >= x0 && p.x <= x1 && Mathf.Sign(dz) != side) dz = side * 0.001f;
                    float dx = p.x - qx, d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < rr) { if (d < 1e-5f) { dz = side; d = 1; } p.x = qx + dx / d * rr; p.z = z + dz / d * rr; }
                }
                else
                {
                    float z0 = Mathf.Min(a.y, b.y), z1 = Mathf.Max(a.y, b.y), x = a.x;
                    float qz = Mathf.Clamp(p.z, z0, z1);
                    float side = Mathf.Sign(prev.x - x); if (side == 0) side = 1;
                    float dx = p.x - x;
                    if (p.z >= z0 && p.z <= z1 && Mathf.Sign(dx) != side) dx = side * 0.001f;
                    float dz = p.z - qz, d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < rr) { if (d < 1e-5f) { dx = side; d = 1; } p.x = x + dx / d * rr; p.z = qz + dz / d * rr; }
                }
            }
        return p;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    void CheckArea()
    {
        CullRooms();
        if (me == null || phase != Phase.Play || Outside) return;
        var a = AreaAt(me.transform.position);
        if (a != "" && a != curArea) { if (curArea != "") Toast(a); curArea = a; }
    }

    // ===== 방 꾸미기 (c = 방 가운데 바닥, 뒷벽 z = -OldHZ) =====
    float Back => -OldHZ + 0.6f;   // 예전 좌표 기준 (V로 옮김)

    void FillKitchen(Vector3 c, Transform t)
    {
        float b = Back;
        Put("fridge", c + V(-5.2f, 0, b), 0, 1.25f, null, t);
        for (int i = 0; i < 3; i++) Put("kitchen_counter", c + V(-3.0f + i * 2.05f, 0, b + 0.05f), 0, 1.25f, null, t);
        Put("oven", c + V(3.3f, 0, b), 0, 1.25f, null, t);
        Put("kitchen_counter", c + V(5.0f, 0, b + 0.05f), 0, 1.0f, null, t);
        Put("kitchen_shelf", c + V(-1.0f, 2.3f, -OldHZ + 0.2f), 0, 1.3f, null, t);
        Put("kitchen_shelf", c + V(2.0f, 2.3f, -OldHZ + 0.2f), 0, 1.3f, null, t);
        Put("cookie_plate", c + V(-1.0f, 1.17f, b), 0, 1.3f, null, t);
        Put("cocoa_mug", c + V(-3.4f, 1.17f, b), 0, 1.5f, null, t);
        Put("cocoa_mug", c + V(-3.1f, 1.17f, b + 0.1f), 50, 1.5f, T(("mug", "43A047")), t);
        Put("rug_rect", c + V(0, 0.004f, 2.4f), 0, 1.3f, T(("fabric", "6E9C87")), t);
        Put("dining_table", c + V(0, 0, 2.4f), 0, 1.2f, null, t);
        for (int i = -1; i <= 1; i += 2) { Put("chair", c + V(i * 0.7f, 0, 1.4f), 0, 1.15f, null, t); Put("chair", c + V(i * 0.7f, 0, 3.4f), 180, 1.15f, null, t); }
        Put("candle_set", c + V(0, 0.95f, 2.4f), 0, 1.1f, null, t);
        Put("gingerbread_man", c + V(5.0f, 0, 3.5f), -120, 1.1f, null, t);
        Put("poinsettia", c + V(-5.3f, 0, 5.2f), 0, 1.3f, null, t);
        PointLight(c + V(3.3f, 0.6f, b + 0.6f), new Color(1f, 0.65f, 0.35f), 3.5f, 0.8f);
    }

    void FillBakery(Vector3 c, Transform t)
    {
        float b = Back;
        Put("oven", c + V(-4.8f, 0, b), 0, 1.25f, T(("body", "E8B04B")), t);
        Put("oven", c + V(-3.6f, 0, b), 0, 1.25f, T(("body", "E8B04B")), t);
        Put("kitchen_counter", c + V(-1.6f, 0, b + 0.05f), 0, 1.25f, T(("cabinet", "FBE3C8"), ("top", "C98B5A")), t);
        Put("kitchen_shelf", c + V(1.2f, 2.3f, -OldHZ + 0.2f), 0, 1.3f, null, t);
        for (int r = 0; r < 2; r++)
        {
            var tc = c + V(-2.2f + r * 4.4f, 0, 1.6f);
            Put("dining_table", tc, 90, 1.15f, T(("fabric", r == 0 ? "E53935" : "43A047")), t);
            Put("cookie_plate", tc + V(0, 0.92f, -0.4f), 0, 1.4f, null, t);
            Put("cookie_plate", tc + V(0, 0.92f, 0.5f), 40, 1.4f, null, t);
            Put("cocoa_mug", tc + V(0.3f, 0.92f, 0.1f), 20, 1.5f, null, t);
            for (int s = -1; s <= 1; s += 2) Put("chair", tc + V(s * 0.85f, 0, 0), -90 * s, 1.15f, null, t);
        }
        Put("gingerbread_man", c + V(4.6f, 0, b + 0.6f), -10, 1.4f, null, t);
        Put("gingerbread_woman", c + V(3.4f, 0, b + 0.6f), 10, 1.4f, null, t);
        Put("candy_cane_big", c + V(5.6f, 0, 4.5f), -90, 1.4f, null, t);
        Put("candy_cane_big", c + V(-5.6f, 0, 4.5f), 90, 1.4f, null, t);
        Put("wall_clock", c + V(1.8f, 0, b - 0.25f), 0, 1.2f, null, t);
        Put("rug_round", c + V(0, 0.004f, 5.0f), 0, 1.3f, T(("rug", "E8B04B")), t);
    }

    void FillLibrary(Vector3 c, Transform t)
    {
        float b = Back;
        for (int i = 0; i < 5; i++) Put("bookshelf", c + V(-4.6f + i * 2.3f, 0, b - 0.25f), 0, 1.35f, i % 2 == 0 ? null : T(("wood", "8A5A3C")), t);
        Put("bookshelf", c + V(-OldHW + 0.45f, 0, -3.0f), 90, 1.35f, null, t);
        Put("bookshelf", c + V(-OldHW + 0.45f, 0, 3.0f), 90, 1.35f, null, t);
        Put("rug_round", c + V(0, 0.004f, 1.5f), 0, 1.6f, T(("rug", "7A3E3A")), t);
        Put("armchair", c + V(-1.6f, 0, 1.0f), 30, 1.2f, T(("fabric", "8A3B34")), t);
        Put("armchair", c + V(1.6f, 0, 1.0f), -30, 1.2f, T(("fabric", "8A3B34")), t);
        Put("coffee_table", c + V(0, 0, 2.4f), 0, 1.0f, T(("wood", "6B4630")), t);
        Put("snow_globe", c + V(0.3f, 0.47f, 2.4f), 0, 1.3f, null, t);
        Put("cocoa_mug", c + V(-0.4f, 0.47f, 2.3f), 0, 1.5f, null, t);
        Put("floor_lamp", c + V(-3.0f, 0, 0.2f), 0, 1.15f, null, t);
        Put("side_table", c + V(3.0f, 0, 0.4f), 0, 1.1f, null, t);
        Put("table_lamp", c + V(3.0f, 0.68f, 0.4f), 0, 1.1f, null, t);
        Put("ottoman", c + V(0, 0, -0.4f), 0, 1.1f, T(("fabric", "6E5A8A")), t);
        Put("picture_frame", c + V(4.5f, 3.4f, -OldHZ + 0.12f), 0, 1.3f, null, t);
        PointLight(c + V(-3.0f, 1.8f, 0.2f), new Color(1f, 0.85f, 0.6f), 5f, 1.0f);
    }

    void FillGreenhouse(Vector3 c, Transform t)
    {
        // 뒤쪽은 나무, 가운데 돌길 양옆으로 화분 12개 (AcGreenhouse.cs, 11-26)
        string[] trees = { "pine_small", "pine_snowy", "birch", "fruit_tree" };
        var pos = new[] { new Vector3(-4.6f, 0, -5.4f), new Vector3(-1.5f, 0, -5.6f), new Vector3(1.8f, 0, -5.5f), new Vector3(4.7f, 0, -5.2f) };
        for (int i = 0; i < pos.Length; i++) Put(trees[i], c + V(pos[i].x, 0, pos[i].z), i * 47, i == 1 ? 0.65f : 0.75f, null, t);
        for (int i = 0; i < 6; i++) Put("path_stones", c + V(0.2f * (i % 2), 0, -3.6f + i * 1.9f), i * 51, 1.0f, null, t);
        foreach (float lz in new[] { -1.5f, 1.5f })
        {
            Put("lantern_ground", c + new Vector3(-0.85f, 0, lz), 0, 1.1f, null, t);
            Put("lantern_ground", c + new Vector3(0.85f, 0, lz), 0, 1.1f, null, t);
        }
        Put("poinsettia", c + new Vector3(-4.1f, 0, 5.3f), 20, 1.3f, null, t);
        Put("poinsettia", c + new Vector3(4.1f, 0, 5.3f), -20, 1.3f, null, t);
        Put("bush_holly", c + new Vector3(-4.0f, 0, 4.4f), 0, 0.9f, null, t);
        Put("bush_round", c + new Vector3(4.0f, 0, 4.4f), 0, 0.9f, null, t);
        GreenFill(c, t);
    }

    void FillPlayroom(Vector3 c, Transform t)
    {
        Put("rug_round", c + V(0, 0.004f, 0.5f), 0, 1.9f, T(("rug", "4FC3F7")), t);
        Put("toy_train", c + V(-1.6f, 0, 0.0f), 30, 1.8f, null, t);
        Put("toy_train", c + V(1.8f, 0, 1.6f), 200, 1.8f, T(("body", "1E88E5")), t);
        Put("teddy_bear", c + V(-0.4f, 0, 2.2f), -10, 1.7f, null, t);
        Put("teddy_bear", c + V(4.6f, 0, -5.4f), 10, 2.2f, T(("fur", "F5C27A")), t);
        Put("teddy_bear", c + V(-4.8f, 0, -5.3f), -20, 2.0f, T(("fur", "FAF8F5")), t);
        Put("nutcracker", c + V(-2.6f, 0, -5.6f), 0, 1.4f, null, t);
        Put("nutcracker", c + V(2.6f, 0, -5.6f), 0, 1.4f, T(("coat", "1E88E5")), t);
        Put("reindeer_plush", c + V(0, 0, -5.4f), 0, 1.5f, null, t);
        Put("gift_box", c + V(3.6f, 0, 3.6f), 20, 1.2f, T(("box", "F06292")), t);
        Put("gift_tall", c + V(4.3f, 0, 4.2f), -10, 1.2f, null, t);
        Put("ottoman", c + V(-3.6f, 0, 3.4f), 0, 1.1f, T(("fabric", "FDD835")), t);
        Put("cushion", c + V(-4.6f, 0, 2.2f), 30, 1.3f, T(("fabric", "F06292")), t);
        Put("cushion", c + V(-3.4f, 0, 1.6f), -20, 1.3f, T(("fabric", "81C784")), t);
        Put("sled", c + V(4.6f, 0, -1.4f), 80, 1.1f, null, t);
        Put("candy_cane_big", c + V(-5.6f, 0, -2.0f), 90, 1.4f, null, t);
    }

    void FillBedroom(Vector3 c, Transform t)
    {
        float b = Back;
        Put("bed", c + V(-2.4f, 0, b + 1.4f), 0, 1.25f, null, t);
        Put("bed", c + V(1.6f, 0, b + 1.4f), 0, 1.25f, T(("fabric", "F06292")), t);
        Put("nightstand_lamp", c + V(-4.0f, 0, b), 0, 1.2f, null, t);
        Put("nightstand_lamp", c + V(-0.4f, 0, b), 0, 1.2f, null, t);
        Put("nightstand_lamp", c + V(3.2f, 0, b), 0, 1.2f, null, t);
        Put("wardrobe", c + V(5.1f, 0, b + 0.1f), 0, 1.25f, null, t);
        Put("dresser", c + V(OldHW - 0.5f, 0, 2.0f), -90, 1.25f, null, t);
        Put("rug_rect", c + V(-0.4f, 0.004f, 2.6f), 0, 1.4f, T(("fabric", "7D7BAE")), t);
        Put("armchair", c + V(-4.6f, 0, 3.6f), 40, 1.15f, T(("fabric", "B39DDB")), t);
        Put("floor_lamp", c + V(-5.5f, 0, 2.2f), 0, 1.1f, null, t);
        Put("teddy_bear", c + V(1.6f, 0.62f, b + 0.4f), 0, 1.2f, null, t);
        Put("cushion", c + V(0.8f, 0, 4.0f), 15, 1.3f, T(("fabric", "B39DDB")), t);
        Put("picture_frame", c + V(-0.4f, 3.6f, -OldHZ + 0.12f), 0, 1.4f, null, t);
        PointLight(c + V(-0.4f, 1.2f, b + 0.3f), new Color(1f, 0.7f, 0.55f), 4f, 0.8f);
    }

    void FillMusic(Vector3 c, Transform t)
    {
        float b = Back;
        Put("piano", c + V(-2.6f, 0, b - 0.1f), 0, 1.3f, null, t);
        Put("piano", c + V(2.0f, 0, b - 0.1f), 0, 1.3f, T(("wood", "F5F1E8")), t);
        Put("wall_clock", c + V(5.2f, 0, b - 0.25f), 0, 1.3f, null, t);
        Put("rug_round", c + V(0, 0.004f, 2.0f), 0, 1.7f, T(("rug", "8C5A3C")), t);
        Put("sofa", c + V(0, 0, 4.6f), 180, 1.15f, T(("fabric", "B8463F")), t);
        Put("armchair", c + V(-3.2f, 0, 2.6f), 60, 1.15f, null, t);
        Put("armchair", c + V(3.2f, 0, 2.6f), -60, 1.15f, null, t);
        Put("floor_lamp", c + V(-5.2f, 0, 4.2f), 0, 1.15f, null, t);
        Put("floor_lamp", c + V(5.2f, 0, 4.2f), 0, 1.15f, null, t);
        Put("bookshelf", c + V(OldHW - 0.45f, 0, 0.6f), -90, 1.3f, null, t);
        Put("poinsettia", c + V(-5.3f, 0, b + 0.4f), 0, 1.3f, null, t);
        Put("star_lantern", c + V(-2.6f, 1.75f, b - 0.2f), 0, 1.0f, null, t);
        Put("bell_pair", c + V(0, 4.1f, -OldHZ + 0.2f), 0, 1.4f, null, t);
        Put("coffee_table", c + V(0, 0, 2.6f), 0, 1.0f, null, t);
        Put("candle_set", c + V(0, 0.47f, 2.6f), 0, 1.2f, null, t);
    }

    void FillGiftRoom(Vector3 c, Transform t)
    {
        Put("gift_pile", c + V(-4.0f, 0, -5.0f), 0, 1.6f, null, t);
        Put("gift_pile", c + V(0.4f, 0, -5.3f), 30, 1.7f, null, t);
        Put("gift_pile", c + V(4.3f, 0, -4.8f), -20, 1.5f, null, t);
        Put("present_sack", c + V(-5.0f, 0, -2.0f), 40, 1.4f, null, t);
        Put("present_sack", c + V(5.0f, 0, -1.6f), -40, 1.4f, null, t);
        Put("dining_table", c + V(0, 0, 1.4f), 90, 1.2f, T(("fabric", "FDD835")), t);
        Put("gift_flat", c + V(0, 0.95f, 1.0f), 10, 1.2f, null, t);
        Put("gift_box", c + V(0.1f, 0.95f, 2.0f), -20, 0.9f, T(("box", "8E24AA")), t);
        var cols = new[] { "E53935", "1E88E5", "43A047", "FDD835", "F06292", "26A69A" };
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60 * Mathf.Deg2Rad;
            Put(i % 2 == 0 ? "gift_box" : "gift_tall", c + V(Mathf.Cos(a) * 3.6f, 0, 3.4f + Mathf.Sin(a) * 1.4f), i * 33, 1.1f, T(("box", cols[i])), t);
        }
        Put("sled", c + V(-3.6f, 0, 4.8f), 20, 1.3f, null, t);
        Put("reindeer_plush", c + V(-1.6f, 0, 5.3f), 30, 1.5f, null, t);
        Put("candy_cane_big", c + V(5.5f, 0, 4.8f), -90, 1.4f, null, t);
    }
}
