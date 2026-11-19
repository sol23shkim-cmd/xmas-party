// AcHouse.cs - 큰 집: 가운데 트리 거실 + 양옆으로 방 4개씩 (모두 8개). 방 사이는 아치 문으로 이어짐.
// 카메라가 남쪽(+z)에서 보므로 방들을 가로 한 줄로 배치하고, 앞쪽(+z) 벽은 두지 않음.
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    const int RoomSegs = 4;                                   // 방 하나 가로 4칸 (12.8m)
    const int RoomsPerSide = 4;
    static float RoomW => RoomSegs * SEG;
    static float HouseHalf => HalfX + RoomsPerSide * RoomW;   // 집 전체 반폭 (약 64m)
    static float ArchZ => -HalfZ + SEG * 2.5f;                // 방 사이 아치 문 위치 (가운데)

    class RoomDef
    {
        public string name, floor = "floor_planks";
        public Color wall, wain, floorTint;
        public System.Action<Vector3, Transform> fill;        // 방 가운데(바닥) 기준으로 가구 채우기
    }

    readonly List<(float x0, float x1, string name)> areas = new List<(float, float, string)>();
    string curArea = "";
    readonly List<(float x0, float x1, GameObject go)> roomRoots = new List<(float, float, GameObject)>();
    float nextCull;

    // 멀리 있는 방은 꺼서 휴대폰에서도 가볍게
    void CullRooms()
    {
        if (Time.unscaledTime < nextCull || me == null) return;
        nextCull = Time.unscaledTime + 0.3f;
        float x = me.transform.position.x, reach = IsMobile ? 15f : 24f;
        foreach (var (x0, x1, go) in roomRoots)
        {
            float d = x < x0 ? x0 - x : x > x1 ? x - x1 : 0;
            bool on = d < reach;
            if (go.activeSelf != on) go.SetActive(on);
        }
    }

    static Color C(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    Dictionary<string, Color> T(params (string k, string hex)[] kv) { var d = new Dictionary<string, Color>(); foreach (var (k, h) in kv) d[k] = C(h); return d; }

    // 경계 x 목록 (왼쪽 끝 → 오른쪽 끝)
    List<float> Bounds()
    {
        var b = new List<float>();
        for (int i = RoomsPerSide; i >= 1; i--) b.Add(-HalfX - (i) * RoomW);
        b.Add(-HalfX); b.Add(HalfX);
        for (int i = 1; i <= RoomsPerSide; i++) b.Add(HalfX + i * RoomW);
        return b;
    }

    RoomDef[] LeftRooms() => new[]
    {
        new RoomDef { name = "부엌", floor = "floor_tiles", wall = C("EAF4EC"), wain = C("6E9C87"), fill = FillKitchen },
        new RoomDef { name = "쿠키 공방", wall = C("FBEBDD"), wain = C("A8693F"), fill = FillBakery },
        new RoomDef { name = "서재", wall = C("EFE6D2"), wain = C("5B3E2E"), fill = FillLibrary },
        new RoomDef { name = "겨울 온실", floor = "floor_tiles", wall = C("E3F1F6"), wain = C("8FB3A2"), floorTint = C("DCE6DA"), fill = FillGreenhouse },
    };
    RoomDef[] RightRooms() => new[]
    {
        new RoomDef { name = "놀이방", wall = C("FFF0F3"), wain = C("D97C8C"), fill = FillPlayroom },
        new RoomDef { name = "침실", wall = C("ECEBF7"), wain = C("7D7BAE"), fill = FillBedroom },
        new RoomDef { name = "음악실", wall = C("F6EEDB"), wain = C("8C5A3C"), fill = FillMusic },
        new RoomDef { name = "선물 포장실", wall = C("FDF3DC"), wain = C("B8463F"), fill = FillGiftRoom },
    };

    void BuildHouse()
    {
        var house = new GameObject("House").transform; house.SetParent(world, false);
        var bx = Bounds();
        areas.Clear();
        var left = LeftRooms(); var right = RightRooms();
        // 방 영역 (왼쪽 끝부터)
        for (int i = 0; i < RoomsPerSide; i++) areas.Add((bx[i], bx[i + 1], left[RoomsPerSide - 1 - i].name));
        areas.Add((-HalfX, HalfX, "트리 거실"));
        for (int i = 0; i < RoomsPerSide; i++) areas.Add((bx[RoomsPerSide + 1 + i], bx[RoomsPerSide + 2 + i], right[i].name));

        for (int side = -1; side <= 1; side += 2)
        {
            var defs = side < 0 ? left : right;
            for (int i = 0; i < RoomsPerSide; i++)
            {
                var d = defs[i];
                float x0 = side < 0 ? -HalfX - (i + 1) * RoomW : HalfX + i * RoomW;
                float cx = x0 + RoomW / 2;
                var room = new GameObject(d.name).transform; room.SetParent(house, false);
                roomRoots.Add((x0, x0 + RoomW, room.gameObject));
                var floorT = d.floorTint.a > 0 ? new Dictionary<string, Color> { { "floor", d.floorTint } } : null;
                for (int ix = 0; ix < RoomSegs; ix++)
                    for (int iz = 0; iz < NZ; iz++)
                        Put(d.floor, new Vector3(x0 + SEG * (ix + 0.5f), 0, -HalfZ + SEG * (iz + 0.5f)), (ix + iz) % 2 * 90, S, floorT, room);
                var wt = new Dictionary<string, Color> { { "wall", d.wall }, { "wainscot", d.wain } };
                // 뒷벽: 창 2개
                for (int ix = 0; ix < RoomSegs; ix++)
                {
                    string w = ix == 1 || ix == 2 ? "wall_window" : "wall_plain";
                    var g = Put(w, new Vector3(x0 + SEG * (ix + 0.5f), 0, -HalfZ), 0, S, wt, room);
                    if (w == "wall_window")
                    {
                        HidePart(g, "glass");
                        Put("curtain", new Vector3(g.transform.position.x, 2.72f * S - 2.02f * S * 0.85f, -HalfZ + 0.16f), 0, S * 0.85f,
                            new Dictionary<string, Color> { { "fabric", d.wain } }, room);
                    }
                    Put("string_lights", new Vector3(x0 + SEG * (ix + 0.5f), 3.35f * S, -HalfZ + 0.15f), 0, S * 0.62f, null, room);
                }
                d.fill(new Vector3(cx, 0, 0), room);
                PointLight(new Vector3(cx, 4.5f, 0), new Color(1f, 0.9f, 0.75f), 11f, 0.5f);
            }
        }
        // 경계 벽 (방 사이는 아치 문, 양 끝은 창 있는 벽)
        for (int k = 0; k < bx.Count; k++)
        {
            float x = bx[k];
            bool end = k == 0 || k == bx.Count - 1;
            var tL = AreaTint(x - 1f); var tR = AreaTint(x + 1f);
            for (int iz = 0; iz < NZ; iz++)
            {
                float z = -HalfZ + SEG * (iz + 0.5f);
                bool arch = !end && iz == 2;
                string w = arch ? "wall_door" : end && iz == 1 ? "wall_window" : "wall_plain";
                // 양쪽 방에서 각각 보이도록 등을 맞댄 두 장
                if (k > 0) { var g = Put(w, new Vector3(x - 0.06f, 0, z), -90, S, tL, house); if (arch) HidePart(g, "door"); if (w == "wall_window") HidePart(g, "glass"); }
                if (k < bx.Count - 1) { var g = Put(w, new Vector3(x + 0.06f, 0, z), 90, S, tR, house); if (arch) HidePart(g, "door"); if (w == "wall_window") HidePart(g, "glass"); }
            }
            Put("wall_corner", new Vector3(x, 0, -HalfZ), 0, S, null, house);
        }
    }

    Dictionary<string, Color> AreaTint(float x)
    {
        var bx = Bounds();
        if (x > -HalfX && x < HalfX) return null;
        var left = LeftRooms(); var right = RightRooms();
        for (int i = 0; i < RoomsPerSide; i++)
        {
            float l0 = -HalfX - (i + 1) * RoomW;
            if (x >= l0 && x < l0 + RoomW) return new Dictionary<string, Color> { { "wall", left[i].wall }, { "wainscot", left[i].wain } };
            float r0 = HalfX + i * RoomW;
            if (x >= r0 && x < r0 + RoomW) return new Dictionary<string, Color> { { "wall", right[i].wall }, { "wainscot", right[i].wain } };
        }
        return null;
    }

    string AreaAt(float x)
    {
        foreach (var a in areas) if (x >= a.x0 && x < a.x1) return a.name;
        return "";
    }

    // 실내 이동: 지금 있는 영역 안에서만, 경계의 아치 문으로만 옆방에 감
    Vector3 ClampInside(Vector3 prev, Vector3 p)
    {
        var bx = Bounds(); int n = bx.Count;
        int k = 0;
        for (int i = 0; i < n - 1; i++) if (prev.x >= bx[i]) k = i;
        float lo = bx[k] + 0.6f, hi = bx[k + 1] - 0.6f;
        bool zOk = Mathf.Abs(p.z - ArchZ) < 0.7f;
        if (zOk && k > 0) lo = bx[k] - 0.6f;
        if (zOk && k < n - 2) hi = bx[k + 1] + 0.6f;
        float before = p.x;
        p.x = Mathf.Clamp(p.x, lo, hi);
        // 벽에 막혔는데 아치가 가까우면 아치 쪽으로 살짝 미끄러지게 (문 찾기 쉽게)
        if (Mathf.Abs(before - p.x) > 0.0005f && Mathf.Abs(p.z - ArchZ) < 2.2f)
        {
            bool towardInner = (p.x >= hi - 0.001f && k < n - 2) || (p.x <= lo + 0.001f && k > 0);
            if (towardInner) p.z = Mathf.MoveTowards(p.z, ArchZ, 0.06f);
        }
        for (int i = 1; i < n - 1; i++)
            if (Mathf.Abs(p.x - bx[i]) < 0.6f) p.z = Mathf.Clamp(p.z, ArchZ - 0.69f, ArchZ + 0.69f);   // 문 통로 안
        return p;
    }

    void CheckArea()
    {
        CullRooms();
        if (me == null || phase != Phase.Play || Outside) return;
        var a = AreaAt(me.transform.position.x);
        if (a != "" && a != curArea) { if (curArea != "") Toast(a); curArea = a; }
    }

    // ===== 방 꾸미기 (c = 방 가운데 바닥, 뒷벽 z = -HalfZ) =====
    float Back => -HalfZ + 0.6f;

    void FillKitchen(Vector3 c, Transform t)
    {
        float b = Back;
        Put("fridge", c + new Vector3(-5.2f, 0, b - c.z), 0, 1.25f, null, t);
        for (int i = 0; i < 3; i++) Put("kitchen_counter", c + new Vector3(-3.0f + i * 2.05f, 0, b - c.z + 0.05f), 0, 1.25f, null, t);
        Put("oven", c + new Vector3(3.3f, 0, b - c.z), 0, 1.25f, null, t);
        Put("kitchen_counter", c + new Vector3(5.0f, 0, b - c.z + 0.05f), 0, 1.0f, null, t);
        Put("kitchen_shelf", c + new Vector3(-1.0f, 2.3f, -HalfZ + 0.2f), 0, 1.3f, null, t);
        Put("kitchen_shelf", c + new Vector3(2.0f, 2.3f, -HalfZ + 0.2f), 0, 1.3f, null, t);
        Put("cookie_plate", c + new Vector3(-1.0f, 1.17f, b - c.z), 0, 1.3f, null, t);
        Put("cocoa_mug", c + new Vector3(-3.4f, 1.17f, b - c.z), 0, 1.5f, null, t);
        Put("cocoa_mug", c + new Vector3(-3.1f, 1.17f, b - c.z + 0.1f), 50, 1.5f, T(("mug", "43A047")), t);
        Put("rug_rect", c + new Vector3(0, 0.004f, 2.4f), 0, 1.3f, T(("fabric", "6E9C87")), t);
        Put("dining_table", c + new Vector3(0, 0, 2.4f), 0, 1.2f, null, t);
        for (int i = -1; i <= 1; i += 2) { Put("chair", c + new Vector3(i * 0.7f, 0, 1.4f), 0, 1.15f, null, t); Put("chair", c + new Vector3(i * 0.7f, 0, 3.4f), 180, 1.15f, null, t); }
        Put("candle_set", c + new Vector3(0, 0.95f, 2.4f), 0, 1.1f, null, t);
        Put("gingerbread_man", c + new Vector3(5.0f, 0, 3.5f), -120, 1.1f, null, t);
        Put("poinsettia", c + new Vector3(-5.3f, 0, 5.2f), 0, 1.3f, null, t);
        PointLight(c + new Vector3(3.3f, 0.6f, b - c.z + 0.6f), new Color(1f, 0.65f, 0.35f), 3.5f, 0.8f);
    }

    void FillBakery(Vector3 c, Transform t)
    {
        float b = Back;
        Put("oven", c + new Vector3(-4.8f, 0, b - c.z), 0, 1.25f, T(("body", "E8B04B")), t);
        Put("oven", c + new Vector3(-3.6f, 0, b - c.z), 0, 1.25f, T(("body", "E8B04B")), t);
        Put("kitchen_counter", c + new Vector3(-1.6f, 0, b - c.z + 0.05f), 0, 1.25f, T(("cabinet", "FBE3C8"), ("top", "C98B5A")), t);
        Put("kitchen_shelf", c + new Vector3(1.2f, 2.3f, -HalfZ + 0.2f), 0, 1.3f, null, t);
        for (int r = 0; r < 2; r++)
        {
            var tc = c + new Vector3(-2.2f + r * 4.4f, 0, 1.6f);
            Put("dining_table", tc, 90, 1.15f, T(("fabric", r == 0 ? "E53935" : "43A047")), t);
            Put("cookie_plate", tc + new Vector3(0, 0.92f, -0.4f), 0, 1.4f, null, t);
            Put("cookie_plate", tc + new Vector3(0, 0.92f, 0.5f), 40, 1.4f, null, t);
            Put("cocoa_mug", tc + new Vector3(0.3f, 0.92f, 0.1f), 20, 1.5f, null, t);
            for (int s = -1; s <= 1; s += 2) Put("chair", tc + new Vector3(s * 0.85f, 0, 0), -90 * s, 1.15f, null, t);
        }
        Put("gingerbread_man", c + new Vector3(4.6f, 0, b - c.z + 0.6f), -10, 1.4f, null, t);
        Put("gingerbread_woman", c + new Vector3(3.4f, 0, b - c.z + 0.6f), 10, 1.4f, null, t);
        Put("candy_cane_big", c + new Vector3(5.6f, 0, 4.5f), -90, 1.4f, null, t);
        Put("candy_cane_big", c + new Vector3(-5.6f, 0, 4.5f), 90, 1.4f, null, t);
        Put("wall_clock", c + new Vector3(1.8f, 0, b - c.z - 0.25f), 0, 1.2f, null, t);
        Put("rug_round", c + new Vector3(0, 0.004f, 5.0f), 0, 1.3f, T(("rug", "E8B04B")), t);
    }

    void FillLibrary(Vector3 c, Transform t)
    {
        float b = Back;
        for (int i = 0; i < 5; i++) Put("bookshelf", c + new Vector3(-4.6f + i * 2.3f, 0, b - c.z - 0.25f), 0, 1.35f, i % 2 == 0 ? null : T(("wood", "8A5A3C")), t);
        Put("bookshelf", c + new Vector3(-RoomW / 2 + 0.45f, 0, -3.0f), 90, 1.35f, null, t);
        Put("bookshelf", c + new Vector3(-RoomW / 2 + 0.45f, 0, 3.0f), 90, 1.35f, null, t);
        Put("rug_round", c + new Vector3(0, 0.004f, 1.5f), 0, 1.6f, T(("rug", "7A3E3A")), t);
        Put("armchair", c + new Vector3(-1.6f, 0, 1.0f), 30, 1.2f, T(("fabric", "8A3B34")), t);
        Put("armchair", c + new Vector3(1.6f, 0, 1.0f), -30, 1.2f, T(("fabric", "8A3B34")), t);
        Put("coffee_table", c + new Vector3(0, 0, 2.4f), 0, 1.0f, T(("wood", "6B4630")), t);
        Put("snow_globe", c + new Vector3(0.3f, 0.47f, 2.4f), 0, 1.3f, null, t);
        Put("cocoa_mug", c + new Vector3(-0.4f, 0.47f, 2.3f), 0, 1.5f, null, t);
        Put("floor_lamp", c + new Vector3(-3.0f, 0, 0.2f), 0, 1.15f, null, t);
        Put("side_table", c + new Vector3(3.0f, 0, 0.4f), 0, 1.1f, null, t);
        Put("table_lamp", c + new Vector3(3.0f, 0.68f, 0.4f), 0, 1.1f, null, t);
        Put("ottoman", c + new Vector3(0, 0, -0.4f), 0, 1.1f, T(("fabric", "6E5A8A")), t);
        Put("picture_frame", c + new Vector3(4.5f, 3.4f, -HalfZ + 0.12f), 0, 1.3f, null, t);
        PointLight(c + new Vector3(-3.0f, 1.8f, 0.2f), new Color(1f, 0.85f, 0.6f), 5f, 1.0f);
    }

    void FillGreenhouse(Vector3 c, Transform t)
    {
        string[] trees = { "pine_small", "pine_snowy", "birch", "fruit_tree", "palm_cedar" };
        var pos = new[] { new Vector3(-4.6f, 0, -5.4f), new Vector3(-1.5f, 0, -5.6f), new Vector3(1.8f, 0, -5.5f), new Vector3(4.7f, 0, -5.2f), new Vector3(-5.0f, 0, 4.4f) };
        for (int i = 0; i < pos.Length; i++) Put(trees[i], c + pos[i], i * 47, i == 1 ? 0.65f : 0.75f, null, t);
        Put("bush_round", c + new Vector3(-3.0f, 0, -3.6f), 0, 1.1f, null, t);
        Put("bush_holly", c + new Vector3(3.2f, 0, -3.5f), 0, 1.1f, null, t);
        Put("bush_snowy", c + new Vector3(5.0f, 0, 3.6f), 0, 1.1f, null, t);
        for (int i = 0; i < 4; i++) Put("poinsettia", c + new Vector3(-3.5f + i * 2.3f, 0, 5.6f), i * 30, 1.3f, null, t);
        for (int i = 0; i < 6; i++) Put("path_stones", c + new Vector3(0.2f * (i % 2), 0, -3.6f + i * 1.9f), i * 51, 1.0f, null, t);
        Put("bench", c + new Vector3(-2.4f, 0, 1.0f), 90, 1.2f, null, t);
        Put("bench", c + new Vector3(2.4f, 0, 1.0f), -90, 1.2f, null, t);
        Put("lantern_ground", c + new Vector3(-1.2f, 0, -1.8f), 0, 1.2f, null, t);
        Put("lantern_ground", c + new Vector3(1.4f, 0, 3.2f), 0, 1.2f, null, t);
        Put("snowman_small", c + new Vector3(4.2f, 0, -0.5f), -40, 1.2f, null, t);
        Put("rock_small", c + new Vector3(-4.3f, 0, 0.8f), 30, 1.3f, null, t);
        Put("rock_small", c + new Vector3(-4.8f, 0, 1.5f), 120, 0.9f, null, t);
        Put("snowball_pile", c + new Vector3(3.6f, 0, 0.8f), 0, 1.0f, null, t);
    }

    void FillPlayroom(Vector3 c, Transform t)
    {
        Put("rug_round", c + new Vector3(0, 0.004f, 0.5f), 0, 1.9f, T(("rug", "4FC3F7")), t);
        Put("toy_train", c + new Vector3(-1.6f, 0, 0.0f), 30, 1.8f, null, t);
        Put("toy_train", c + new Vector3(1.8f, 0, 1.6f), 200, 1.8f, T(("body", "1E88E5")), t);
        Put("teddy_bear", c + new Vector3(-0.4f, 0, 2.2f), -10, 1.7f, null, t);
        Put("teddy_bear", c + new Vector3(4.6f, 0, -5.4f), 10, 2.2f, T(("fur", "F5C27A")), t);
        Put("teddy_bear", c + new Vector3(-4.8f, 0, -5.3f), -20, 2.0f, T(("fur", "FAF8F5")), t);
        Put("nutcracker", c + new Vector3(-2.6f, 0, -5.6f), 0, 1.4f, null, t);
        Put("nutcracker", c + new Vector3(2.6f, 0, -5.6f), 0, 1.4f, T(("coat", "1E88E5")), t);
        Put("reindeer_plush", c + new Vector3(0, 0, -5.4f), 0, 1.5f, null, t);
        Put("gift_box", c + new Vector3(3.6f, 0, 3.6f), 20, 1.2f, T(("box", "F06292")), t);
        Put("gift_tall", c + new Vector3(4.3f, 0, 4.2f), -10, 1.2f, null, t);
        Put("ottoman", c + new Vector3(-3.6f, 0, 3.4f), 0, 1.1f, T(("fabric", "FDD835")), t);
        Put("cushion", c + new Vector3(-4.6f, 0, 2.2f), 30, 1.3f, T(("fabric", "F06292")), t);
        Put("cushion", c + new Vector3(-3.4f, 0, 1.6f), -20, 1.3f, T(("fabric", "81C784")), t);
        Put("sled", c + new Vector3(4.6f, 0, -1.4f), 80, 1.1f, null, t);
        Put("candy_cane_big", c + new Vector3(-5.6f, 0, -2.0f), 90, 1.4f, null, t);
    }

    void FillBedroom(Vector3 c, Transform t)
    {
        float b = Back;
        Put("bed", c + new Vector3(-2.4f, 0, b - c.z + 1.4f), 0, 1.25f, null, t);
        Put("bed", c + new Vector3(1.6f, 0, b - c.z + 1.4f), 0, 1.25f, T(("fabric", "F06292")), t);
        Put("nightstand_lamp", c + new Vector3(-4.0f, 0, b - c.z), 0, 1.2f, null, t);
        Put("nightstand_lamp", c + new Vector3(-0.4f, 0, b - c.z), 0, 1.2f, null, t);
        Put("nightstand_lamp", c + new Vector3(3.2f, 0, b - c.z), 0, 1.2f, null, t);
        Put("wardrobe", c + new Vector3(5.1f, 0, b - c.z + 0.1f), 0, 1.25f, null, t);
        Put("dresser", c + new Vector3(RoomW / 2 - 0.5f, 0, 2.0f), -90, 1.25f, null, t);
        Put("rug_rect", c + new Vector3(-0.4f, 0.004f, 2.6f), 0, 1.4f, T(("fabric", "7D7BAE")), t);
        Put("armchair", c + new Vector3(-4.6f, 0, 3.6f), 40, 1.15f, T(("fabric", "B39DDB")), t);
        Put("floor_lamp", c + new Vector3(-5.5f, 0, 2.2f), 0, 1.1f, null, t);
        Put("teddy_bear", c + new Vector3(1.6f, 0.62f, b - c.z + 0.4f), 0, 1.2f, null, t);
        Put("cushion", c + new Vector3(0.8f, 0, 4.0f), 15, 1.3f, T(("fabric", "B39DDB")), t);
        Put("picture_frame", c + new Vector3(-0.4f, 3.6f, -HalfZ + 0.12f), 0, 1.4f, null, t);
        PointLight(c + new Vector3(-0.4f, 1.2f, b - c.z + 0.3f), new Color(1f, 0.7f, 0.55f), 4f, 0.8f);
    }

    void FillMusic(Vector3 c, Transform t)
    {
        float b = Back;
        Put("piano", c + new Vector3(-2.6f, 0, b - c.z - 0.1f), 0, 1.3f, null, t);
        Put("piano", c + new Vector3(2.0f, 0, b - c.z - 0.1f), 0, 1.3f, T(("wood", "F5F1E8")), t);
        Put("wall_clock", c + new Vector3(5.2f, 0, b - c.z - 0.25f), 0, 1.3f, null, t);
        Put("rug_round", c + new Vector3(0, 0.004f, 2.0f), 0, 1.7f, T(("rug", "8C5A3C")), t);
        Put("sofa", c + new Vector3(0, 0, 4.6f), 180, 1.15f, T(("fabric", "B8463F")), t);
        Put("armchair", c + new Vector3(-3.2f, 0, 2.6f), 60, 1.15f, null, t);
        Put("armchair", c + new Vector3(3.2f, 0, 2.6f), -60, 1.15f, null, t);
        Put("floor_lamp", c + new Vector3(-5.2f, 0, 4.2f), 0, 1.15f, null, t);
        Put("floor_lamp", c + new Vector3(5.2f, 0, 4.2f), 0, 1.15f, null, t);
        Put("bookshelf", c + new Vector3(RoomW / 2 - 0.45f, 0, 0.6f), -90, 1.3f, null, t);
        Put("poinsettia", c + new Vector3(-5.3f, 0, b - c.z + 0.4f), 0, 1.3f, null, t);
        Put("star_lantern", c + new Vector3(-2.6f, 1.75f, b - c.z - 0.2f), 0, 1.0f, null, t);
        Put("bell_pair", c + new Vector3(0, 4.1f, -HalfZ + 0.2f), 0, 1.4f, null, t);
        Put("coffee_table", c + new Vector3(0, 0, 2.6f), 0, 1.0f, null, t);
        Put("candle_set", c + new Vector3(0, 0.47f, 2.6f), 0, 1.2f, null, t);
    }

    void FillGiftRoom(Vector3 c, Transform t)
    {
        Put("gift_pile", c + new Vector3(-4.0f, 0, -5.0f), 0, 1.6f, null, t);
        Put("gift_pile", c + new Vector3(0.4f, 0, -5.3f), 30, 1.7f, null, t);
        Put("gift_pile", c + new Vector3(4.3f, 0, -4.8f), -20, 1.5f, null, t);
        Put("present_sack", c + new Vector3(-5.0f, 0, -2.0f), 40, 1.4f, null, t);
        Put("present_sack", c + new Vector3(5.0f, 0, -1.6f), -40, 1.4f, null, t);
        Put("dining_table", c + new Vector3(0, 0, 1.4f), 90, 1.2f, T(("fabric", "FDD835")), t);
        Put("gift_flat", c + new Vector3(0, 0.95f, 1.0f), 10, 1.2f, null, t);
        Put("gift_box", c + new Vector3(0.1f, 0.95f, 2.0f), -20, 0.9f, T(("box", "8E24AA")), t);
        var cols = new[] { "E53935", "1E88E5", "43A047", "FDD835", "F06292", "26A69A" };
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60 * Mathf.Deg2Rad;
            Put(i % 2 == 0 ? "gift_box" : "gift_tall", c + new Vector3(Mathf.Cos(a) * 3.6f, 0, 3.4f + Mathf.Sin(a) * 1.4f), i * 33, 1.1f, T(("box", cols[i])), t);
        }
        Put("sled", c + new Vector3(-3.6f, 0, 4.8f), 20, 1.3f, null, t);
        Put("reindeer_plush", c + new Vector3(-1.6f, 0, 5.3f), 30, 1.5f, null, t);
        Put("candy_cane_big", c + new Vector3(5.5f, 0, 4.8f), -90, 1.4f, null, t);
    }
}
