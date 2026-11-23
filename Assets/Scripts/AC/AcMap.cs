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
        R(new Rect(0, 0, SW, SH));
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.05f, 0.07f, 0.12f, 0.45f)));
        float W = WorldX1 - WorldX0, H = WorldZ1 - WorldZ0;
        float pw = Mathf.Min(SW - 24, 980), mh = pw * H / W;
        if (mh > SH - 120) { mh = SH - 120; pw = mh * W / H; }
        var panel = new Rect(SW / 2 - pw / 2 - 10, SH / 2 - mh / 2 - 34, pw + 20, mh + 62);
        GUI.Box(panel, "", sBox);
        GUI.Label(new Rect(panel.x + 14, panel.y + 8, panel.width - 120, 26), "지도  <size=12>가고 싶은 곳을 누르면 바로 가요</size>", sLabel);
        if (GUI.Button(new Rect(panel.xMax - 82, panel.y + 6, 70, 28), "닫기", sBtn) || Key("esc")) { showMap = false; return; }
        var mr = new Rect(panel.x + 10, panel.y + 44, pw, mh);
        // 화면과 같은 방향: 화면 왼쪽 = +x, 위쪽 = 뒤(-z)
        Rect WR(float x0, float x1, float z0, float z1)
        {
            float u0 = (WorldX1 - x1) / W, u1 = (WorldX1 - x0) / W, v0 = (z0 - WorldZ0) / H, v1 = (z1 - WorldZ0) / H;
            return new Rect(mr.x + u0 * mr.width, mr.y + v0 * mr.height, (u1 - u0) * mr.width, (v1 - v0) * mr.height);
        }
        Vector2 WP(float x, float z) => new Vector2(mr.x + (WorldX1 - x) / W * mr.width, mr.y + (z - WorldZ0) / H * mr.height);

        GUI.DrawTexture(mr, AcPalette.Tex(new Color(0.9f, 0.94f, 0.99f)));
        // 길
        var pb = WR(DoorPos.x - 0.8f, DoorPos.x + 0.8f, -HalfZ - 12f, -HalfZ); GUI.DrawTexture(pb, AcPalette.Tex(new Color(0.8f, 0.76f, 0.7f)));
        var pf = WR(FrontDoorPos.x - 0.8f, FrontDoorPos.x + 0.8f, HalfZ, HalfZ + 10f); GUI.DrawTexture(pf, AcPalette.Tex(new Color(0.8f, 0.76f, 0.7f)));
        // 집과 방
        var house = WR(-HouseHalf, HouseHalf, -HalfZ, HalfZ);
        GUI.DrawTexture(new Rect(house.x - 3, house.y - 3, house.width + 6, house.height + 6), AcPalette.Tex(new Color(0.45f, 0.3f, 0.2f)));
        var lab = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = mr.width < 500 ? 10 : 12 };
        var left = LeftRooms(); var right = RightRooms();
        foreach (var a in areas)
        {
            Color c = new Color(0.98f, 0.92f, 0.8f);
            foreach (var d in left) if (d.name == a.name) c = d.wall;
            foreach (var d in right) if (d.name == a.name) c = d.wall;
            var rr = WR(a.x0, a.x1, -HalfZ, HalfZ);
            GUI.DrawTexture(new Rect(rr.x + 1, rr.y, rr.width - 2, rr.height), AcPalette.Tex(c));
            bool here = !Outside && AreaAt(me.transform.position.x) == a.name;
            if (here) GUI.DrawTexture(new Rect(rr.x + 1, rr.yMax - 4, rr.width - 2, 4), AcPalette.Tex(new Color(0.85f, 0.3f, 0.3f)));
            GUI.Label(new Rect(rr.x, rr.y + rr.height * 0.55f, rr.width, rr.height * 0.42f), a.name, lab);
        }
        // 트리
        var tc = WP(TreePos.x, TreePos.z); float tr = TreeR / W * mr.width;
        GUI.DrawTexture(new Rect(tc.x - tr, tc.y - tr, tr * 2, tr * 2), AcPalette.Tex(new Color(0.25f, 0.55f, 0.32f)));
        GUI.Label(new Rect(tc.x - 40, tc.y - 10, 80, 20), "<color=#ffffff>트리</color>", lab);
        // 문
        var db = WP(DoorPos.x, -HalfZ); GUI.DrawTexture(new Rect(db.x - 6, db.y - 3, 12, 6), AcPalette.Tex(new Color(0.55f, 0.32f, 0.2f)));
        var dfL = WP(FrontGapX1, HalfZ); var dfR = WP(FrontGapX0, HalfZ);
        GUI.DrawTexture(new Rect(dfL.x, dfL.y - 3, dfR.x - dfL.x, 6), AcPalette.Tex(new Color(0.9f, 0.94f, 0.99f)));
        // 마당 이름
        GUI.Label(WR(-HouseHalf, HouseHalf, WorldZ0, -HalfZ - 1), "뒷마당", lab);
        GUI.Label(WR(-HouseHalf, HouseHalf, HalfZ + 1, WorldZ1), "앞마당", lab);
        GUI.Label(WR(HouseHalf, WorldX1, -HalfZ, HalfZ), "마당", lab);
        GUI.Label(WR(WorldX0, -HouseHalf, -HalfZ, HalfZ), "마당", lab);
        // 눈사람·친구·나
        foreach (var c in snowCircles) { var p = WP(c.x, c.y); GUI.DrawTexture(new Rect(p.x - 3, p.y - 3, 6, 6), AcPalette.Tex(Color.white)); }
        foreach (var r in remotes.Values)
        {
            if (r.c == null) continue;
            var p = WP(r.c.transform.position.x, r.c.transform.position.z);
            GUI.DrawTexture(new Rect(p.x - 5, p.y - 5, 10, 10), AcPalette.Tex(new Color(0.2f, 0.45f, 0.9f)));
            if (r.net != null) GUI.Label(new Rect(p.x - 50, p.y - 22, 100, 18), "<color=#2050c0>" + r.net.name + "</color>", lab);
        }
        var mp = WP(me.transform.position.x, me.transform.position.z);
        float pulse = 6 + Mathf.Sin(Time.time * 6f) * 2;
        GUI.DrawTexture(new Rect(mp.x - pulse, mp.y - pulse, pulse * 2, pulse * 2), AcPalette.Tex(new Color(0.9f, 0.2f, 0.2f)));
        GUI.Label(new Rect(mp.x - 30, mp.y + 6, 60, 18), "<color=#c02020><b>나</b></color>", lab);
        // 누르면 이동
        var e = Event.current;
        if (e.type == EventType.MouseUp && mr.Contains(e.mousePosition))
        {
            float x = WorldX1 - (e.mousePosition.x - mr.x) / mr.width * W;
            float z = WorldZ0 + (e.mousePosition.y - mr.y) / mr.height * H;
            TeleportTo(new Vector3(x, 0, z));
            e.Use();
        }
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
        decorating = false; treeOverview = false; followId = null; walkTarget = null; showMap = false;
        if (building) StopBuilding();
        if (InHouse(q) && me.skating) { me.SetSkates(false); skateVel = Vector3.zero; }
        me.transform.position = q;
        UpdateCamera(0, true);
        string where = InHouse(q) ? AreaAt(q.x) : q.z < -HalfZ ? "뒷마당" : q.z > HalfZ ? "앞마당" : "옆마당";
        Toast(where + "(으)로 왔어요");
        CheckArea();
    }
}
