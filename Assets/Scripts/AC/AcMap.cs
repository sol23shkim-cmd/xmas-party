// AcMap.cs - 지도: 집(방 8개 + 트리 거실)과 마당 사방, 나·친구·눈사람 위치. 누르는 곳으로 바로 이동
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    bool showMap;

    void MapButton(Rect peopleBtn)
    {
        if (phase != Phase.Play) return;
        var r = new Rect(peopleBtn.x - 78, peopleBtn.y, 70, peopleBtn.height);
        R(r);
        if (GUI.Button(r, "지도", sBtn)) { showMap = !showMap; if (showMap) showPeople = false; }
    }

    void MapWindow()
    {
        if (!showMap) return;
        CgTextures();
        R(new Rect(0, 0, SW, SH));
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.05f, 0.07f, 0.12f, 0.5f)));
        // 집 둘레 8m까지만 크게 보여 줌
        float MX0 = -HouseHalf - 8, MX1 = HouseHalf + 8, MZ0 = HouseZ0 - 8, MZ1 = HalfZ + 8;
        float W = MX1 - MX0, H = MZ1 - MZ0;
        float pw = Mathf.Min(SW - 20, 1000), mh = pw * H / W;
        if (mh > SH - 110) { mh = SH - 110; pw = mh * W / H; }
        var panel = new Rect(SW / 2 - pw / 2 - 10, SH / 2 - mh / 2 - 30, pw + 20, mh + 58);
        GUI.Box(panel, "", sBox);
        CgText(new Rect(panel.x + 14, panel.y + 8, 200, 28), "지도", 22, new Color(0.45f, 0.28f, 0.18f), TextAnchor.MiddleLeft, 0.01f, new Color(0, 0, 0, 0));
        var hint = new GUIStyle(CgStyle(14, TextAnchor.MiddleLeft)) { normal = { textColor = new Color(0.5f, 0.38f, 0.3f) } };
        GUI.Label(new Rect(panel.x + 64, panel.y + 10, panel.width - 300, 26), "가고 싶은 곳을 누르면 바로 가요", hint);
        if (GUI.Button(new Rect(panel.xMax - 82, panel.y + 6, 70, 30), "닫기", sBtn) || Key("esc")) { showMap = false; return; }
        if (AppInstallable && GUI.Button(new Rect(panel.xMax - 82 - 118, panel.y + 6, 110, 30), "앱으로 받기", sBtn)) AppInstall();
        var mr = new Rect(panel.x + 10, panel.y + 42, pw, mh);
        // 화면과 같은 방향: 화면 왼쪽 = +x, 위쪽 = 뒤(-z)
        Rect WR(float x0, float x1, float z0, float z1)
        {
            float u0 = (MX1 - Mathf.Min(x1, MX1)) / W, u1 = (MX1 - Mathf.Max(x0, MX0)) / W, v0 = (Mathf.Max(z0, MZ0) - MZ0) / H, v1 = (Mathf.Min(z1, MZ1) - MZ0) / H;
            return new Rect(mr.x + u0 * mr.width, mr.y + v0 * mr.height, (u1 - u0) * mr.width, (v1 - v0) * mr.height);
        }
        Vector2 WP(float x, float z) => new Vector2(mr.x + (MX1 - x) / W * mr.width, mr.y + (z - MZ0) / H * mr.height);
        float k = mr.width / W;   // 1m 당 화면 크기

        GUI.DrawTexture(mr, AcPalette.Tex(new Color(0.9f, 0.94f, 0.99f)));
        // 길
        var pb = WR(DoorPos.x - 0.8f, DoorPos.x + 0.8f, HouseZ0 - 2f, -HalfZ); GUI.DrawTexture(pb, AcPalette.Tex(new Color(0.82f, 0.78f, 0.72f)));
        var pf = WR(FrontDoorPos.x - 0.8f, FrontDoorPos.x + 0.8f, HalfZ, HalfZ + 10f); GUI.DrawTexture(pf, AcPalette.Tex(new Color(0.82f, 0.78f, 0.72f)));
        // 집 테두리 (ㄷ자)
        var wallC = AcPalette.Tex(new Color(0.42f, 0.28f, 0.19f));
        void Box(float x0, float x1, float z0, float z1, Color c)
        {
            var rr = WR(x0, x1, z0, z1);
            GUI.DrawTexture(new Rect(rr.x - 2, rr.y - 2, rr.width + 4, rr.height + 4), wallC);
            GUI.DrawTexture(rr, AcPalette.Tex(c));
        }
        string here = Outside ? "" : AreaAt(me.transform.position);
        Box(-HalfX, HalfX, -HalfZ, HalfZ, new Color(0.98f, 0.9f, 0.76f));
        foreach (var a in areasR) Box(a.r.xMin, a.r.xMax, a.r.yMin, a.r.yMax, Color.Lerp(a.def.wall, a.def.wain, 0.18f));
        // 방 사이 문 (밝은 틈)
        foreach (var w in walls)
        {
            if (w.kind != "door" && w.kind != "arch") continue;
            var c = WP(w.pos.x, w.pos.z); float s = DoorHalf * 2 * k;
            GUI.DrawTexture(w.alongX ? new Rect(c.x - s / 2, c.y - 3, s, 6) : new Rect(c.x - 3, c.y - s / 2, 6, s), AcPalette.Tex(new Color(0.98f, 0.94f, 0.86f)));
        }
        var dfL = WP(FrontGapX1, HalfZ); var dfR = WP(FrontGapX0, HalfZ);
        GUI.DrawTexture(new Rect(dfL.x, dfL.y - 3, dfR.x - dfL.x, 6), AcPalette.Tex(new Color(0.9f, 0.94f, 0.99f)));
        // 트리
        var tc = WP(TreePos.x, TreePos.z); float tr = TreeR * k;
        GUI.color = (new Color(0.22f, 0.55f, 0.3f)).linear; GUI.DrawTexture(new Rect(tc.x - tr, tc.y - tr, tr * 2, tr * 2), cgDisc); GUI.color = Color.white;
        // 방 이름 (테두리 글자, 좁으면 두 줄)
        int fs = k * RoomW > 70 ? (IsMobile ? 19 : 16) : (IsMobile ? 16 : 13);   // 11-28: 휴대폰에서 방 이름을 더 크게
        var people = new Dictionary<string, int>();   // 방마다 지금 있는 친구 수
        foreach (var rm in remotes.Values) if (rm.c != null) { var an = InHouse(rm.c.transform.position) ? AreaAt(rm.c.transform.position) : ""; if (an != "") people[an] = (people.TryGetValue(an, out int pc) ? pc : 0) + 1; }
        foreach (var a in areasR)
        {
            var rr = WR(a.r.xMin, a.r.xMax, a.r.yMin, a.r.yMax);
            bool me2 = a.name == here;
            string nm = a.name;
            if (rr.width < nm.Length * fs * 0.95f && nm.Contains(" ")) nm = nm.Replace(" ", "\n");
            if (me2) { GUI.color = (new Color(0.9f, 0.3f, 0.3f, 0.25f)).linear; GUI.DrawTexture(rr, Texture2D.whiteTexture); GUI.color = Color.white; }
            MapLabel(rr, nm, fs, me2);
            if (people.TryGetValue(a.name, out int pn) && pn > 0)
                CgText(new Rect(rr.x, rr.y + 4, rr.width - 6, fs + 2), pn + "명", Mathf.Max(11, fs - 3), new Color(0.75f, 0.88f, 1f), TextAnchor.MiddleRight, 1.3f, new Color(0.1f, 0.15f, 0.35f));
            if (RoomActs.TryGetValue(a.name, out var act))   // 방에서 할 수 있는 것 (작은 글씨)
                CgText(new Rect(rr.x - 10, rr.yMax - fs - 6, rr.width + 20, fs + 2), act, Mathf.Max(IsMobile ? 13 : 11, fs - 4), new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter, 1.2f, new Color(0.3f, 0.18f, 0.1f, 0.9f));
        }
        MapLabel(WR(-HalfX, HalfX, -HalfZ, -HalfZ + 4), "트리 거실", fs, here == "트리 거실");
        MapLabel(WR(-HalfX, HalfX, HouseZ0, -HalfZ), "안뜰", fs, false);
        MapLabel(WR(-HouseHalf, HouseHalf, MZ0, HouseZ0), "뒷마당", fs + 2, false);
        MapLabel(WR(-HouseHalf, HouseHalf, HalfZ, MZ1), "앞마당", fs + 2, false);
        MapLabel(WR(HouseHalf, MX1, HouseZ0, HalfZ), "옆마당", fs, false);
        MapLabel(WR(MX0, -HouseHalf, HouseZ0, HalfZ), "옆마당", fs, false);
        // 눈사람·친구·나
        foreach (var c in snowCircles) { var p = WP(c.x, c.y); GUI.DrawTexture(new Rect(p.x - 4, p.y - 4, 8, 8), cgDisc); }
        foreach (var r in remotes.Values)
        {
            if (r.c == null) continue;
            var p = WP(Mathf.Clamp(r.c.transform.position.x, MX0, MX1), Mathf.Clamp(r.c.transform.position.z, MZ0, MZ1));
            GUI.color = (new Color(0.2f, 0.45f, 0.9f)).linear; GUI.DrawTexture(new Rect(p.x - 7, p.y - 7, 14, 14), cgDisc); GUI.color = Color.white;
            if (r.net != null) CgText(new Rect(p.x - 60, p.y - 28, 120, 20), Short(r.net.name, 8), 14, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter, 1.5f, new Color(0.1f, 0.15f, 0.35f));
        }
        var mp = WP(Mathf.Clamp(me.transform.position.x, MX0, MX1), Mathf.Clamp(me.transform.position.z, MZ0, MZ1));
        float pulse = 8 + Mathf.Sin(Time.time * 6f) * 2.5f;
        GUI.color = (new Color(0.95f, 0.25f, 0.25f, 0.35f)).linear; GUI.DrawTexture(new Rect(mp.x - pulse * 1.6f, mp.y - pulse * 1.6f, pulse * 3.2f, pulse * 3.2f), cgDisc);
        GUI.color = (new Color(0.9f, 0.2f, 0.2f)).linear; GUI.DrawTexture(new Rect(mp.x - 7, mp.y - 7, 14, 14), cgDisc); GUI.color = Color.white;
        CgText(new Rect(mp.x - 30, mp.y + 8, 60, 20), "나", 15, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter, 1.5f, new Color(0.5f, 0.08f, 0.08f));
        // 누르면 이동
        var e = Event.current;
        if (e.type == EventType.MouseUp && mr.Contains(e.mousePosition))
        {
            float x = MX1 - (e.mousePosition.x - mr.x) / mr.width * W;
            float z = MZ0 + (e.mousePosition.y - mr.y) / mr.height * H;
            TeleportTo(new Vector3(x, 0, z));
            e.Use();
        }
    }

    static readonly System.Collections.Generic.Dictionary<string, string> RoomActs = new System.Collections.Generic.Dictionary<string, string>
    {
        { "침실", "베개 싸움" }, { "선물 포장실", "선물 만들기" }, { "쿠키 공방", "쿠키 달리기" }, { "겨울 온실", "화분 키우기" },
        { "음악실", "피아노" }, { "놀이방", "캔디팡" }, { "부엌", "주방 게임" }, { "서재", "퀴즈, 편지" },
    };

    // 받침에 따라 '으로' / '로' (받침 없음·ㄹ받침 → 로)
    static string EuroRo(string w)
    {
        if (string.IsNullOrEmpty(w)) return "로";
        char c = w[w.Length - 1];
        if (c < 0xAC00 || c > 0xD7A3) return "로";
        int jong = (c - 0xAC00) % 28;
        return jong == 0 || jong == 8 ? "로" : "으로";
    }

    void MapLabel(Rect r, string text, int size, bool hl)
    {
        var lines = text.Split('\n');
        float lh = size + 3, y0 = r.center.y - lines.Length * lh / 2;
        for (int i = 0; i < lines.Length; i++)
            CgText(new Rect(r.x - 20, y0 + i * lh, r.width + 40, lh), lines[i], size, hl ? new Color(1f, 0.92f, 0.6f) : Color.white, TextAnchor.MiddleCenter, 1.6f, hl ? new Color(0.6f, 0.1f, 0.1f) : new Color(0.32f, 0.2f, 0.12f, 0.95f));
    }

    Vector3 NearestWalkable(Vector3 p)
    {
        if (navStatic == null) return p;
        bool wantIn = InHouse(p);
        int ci = (int)((p.x - navX0) / NavCell), cj = (int)((p.z - navZ0) / NavCell);
        for (int r = 0; r <= 16; r++)
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    if (Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)) != r) continue;
                    if (!NavOk(ci + di, cj + dj)) continue;
                    var q = new Vector3(navX0 + (ci + di + 0.5f) * NavCell, 0, navZ0 + (cj + dj + 0.5f) * NavCell);
                    if (InHouse(q) != wantIn) continue;
                    return q;
                }
        return p;
    }

    void TeleportTo(Vector3 p)
    {
        var q = NearestWalkable(p);
        if (InHouse(q) && AreaAt(q) == "트리 거실") q = NearestWalkable(TreePos + new Vector3(0, 0, TreeR + 2.0f));   // 트리 거실: 트리 바로 앞(남쪽)에 서서 트리가 화면에 보이게
        decorating = false; treeOverview = false; followId = null; walkTarget = null; showMap = false;
        if (building) StopBuilding();
        if (InHouse(q) && me.skating) { me.SetSkates(false); skateVel = Vector3.zero; }
        me.transform.position = q;
        UpdateCamera(0, true);
        string where = InHouse(q) ? AreaAt(q) : q.z > HalfZ ? "앞마당" : Mathf.Abs(q.x) < HalfX && q.z > HouseZ0 ? "안뜰" : q.z < HouseZ0 ? "뒷마당" : "옆마당";
        Toast(where + EuroRo(where) + " 왔어요");
        CheckArea();
    }
}
