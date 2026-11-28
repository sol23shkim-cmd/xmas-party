// AcRoom2.cs - 거실 장면의 보조 기능: 선명한 색상표, 화살표로 고르는 꾸미기 창, 오너먼트 아이콘,
//              가구와 부딪히기, 오너먼트 놓을 자리 계산, 포인터 강조, 바깥 눈
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AcPalette
{
    static Color H(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    public static readonly string[] VividNames = {
        "빨강", "진홍", "분홍", "연분홍", "주황", "살구", "노랑", "크림", "연두", "초록", "민트", "청록",
        "하늘", "파랑", "남색", "라벤더", "보라", "금색", "갈색", "고동", "베이지", "하양", "은색", "회색", "검정" };
    public static readonly Color[] Vivid = {
        H("E53935"), H("B71C1C"), H("F06292"), H("F8BBD0"), H("FB8C00"), H("FFAB91"), H("FDD835"), H("FFF3C4"), H("C0CA33"), H("43A047"), H("81C784"), H("26A69A"),
        H("4FC3F7"), H("1E88E5"), H("283593"), H("B39DDB"), H("8E24AA"), H("FFC107"), H("8D6E63"), H("5D4037"), H("D7CCC8"), H("FAFAFA"), H("CFD8DC"), H("757575"), H("212121") };
    public static readonly string[] FurNames = {
        "갈색", "고동색", "꿀색", "주황", "크림", "하양", "회색", "검정", "분홍", "살구", "하늘", "민트",
        "연살구", "모래색", "캐러멜", "코코아" };
    public static readonly Color[] Fur = {
        H("B8805A"), H("8A5A3C"), H("F5C27A"), H("F28C47"), H("F7E7C9"), H("FAF8F5"), H("A9A9AE"), H("3A3436"), H("FBBFC0"), H("FFD4B0"), H("B7DDF2"), H("BFE8D5"),
        H("FFDCC2"), H("F2C7A0"), H("D19E72"), H("9A6B4C") };
    public static readonly string[] HairNames = { "검정", "흑갈색", "갈색", "밤색", "금발", "주황", "분홍", "파랑", "은발", "보라" };
    public static readonly Color[] Hair = { H("2B2326"), H("4A3226"), H("744A30"), H("9B5A36"), H("F0C46A"), H("E07B39"), H("F4A6C6"), H("5B8DDB"), H("D9DCE2"), H("8E6CC0") };

    static readonly Dictionary<Color, Texture2D> texCache = new Dictionary<Color, Texture2D>();
    public static Texture2D Tex(Color c)
    {
        if (texCache.TryGetValue(c, out var t) && t) return t;
        t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c); t.Apply();
        texCache[c] = t; return t;
    }

    public static int Nearest(Color[] list, Color c)
    {
        int best = 0; float bd = 99;
        for (int i = 0; i < list.Length; i++)
        {
            float d = Mathf.Abs(list[i].r - c.r) + Mathf.Abs(list[i].g - c.g) + Mathf.Abs(list[i].b - c.b);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }
}

public partial class AcRoom
{
    // ========================================================
    // 꾸미기 창: 종류는 글자 목록, 나머지는 < > (키보드 위아래 항목, 좌우 바꾸기)
    int custRow;
    class CRow { public string label, value; public Color? swatch; public Action<int> step; }

    static readonly string[] HairAll = { "short", "spiky", "sidepart", "slick", "curly", "buzz", "wavy", "long", "bob", "twin", "pony", "bun" };
    static readonly string[] HairAllKo = { "짧은머리", "덥수룩", "가르마", "올백", "곱슬", "까까머리", "웨이브", "긴머리", "단발", "양갈래", "포니테일", "올림머리" };
    static readonly string[] FaceAll = { "f1", "f2", "f3", "f4", "f5" }, FaceAllKo = { "또랑또랑", "방긋", "반짝반짝", "졸린 눈", "속눈썹" };

    static int Wrap(int i, int n) => ((i % n) + n) % n;

    List<CRow> BuildRows(AcLook l)
    {
        var rows = new List<CRow>();
        if (l.species == "human")
        {
            int hi = Mathf.Max(0, Array.IndexOf(HairAll, l.hair));
            rows.Add(new CRow { label = "헤어", value = HairAllKo[hi], step = d => l.hair = HairAll[Wrap(hi + d, HairAll.Length)] });
            int fi = Mathf.Max(0, Array.IndexOf(FaceAll, l.face));
            rows.Add(new CRow { label = "얼굴", value = FaceAllKo[fi], step = d => l.face = FaceAll[Wrap(fi + d, FaceAll.Length)] });
        }
        int ti = Mathf.Max(0, Array.IndexOf(HatId, l.hat));
        rows.Add(new CRow { label = "모자", value = HatKo[ti], step = d => l.hat = HatId[Wrap(ti + d, HatId.Length)] });
        rows.Add(ColorRowDef(l.species == "human" ? "피부색" : "털 색", l.fur, AcPalette.Fur, AcPalette.FurNames, c => l.fur = c, true));
        rows.Add(ColorRowDef("스웨터 색", l.sweater, AcPalette.Vivid, AcPalette.VividNames, c => l.sweater = c));
        rows.Add(ColorRowDef("목도리 색", l.scarf, AcPalette.Vivid, AcPalette.VividNames, c => l.scarf = c));
        if (l.hat != "none") rows.Add(ColorRowDef("모자 색", l.hatColor, AcPalette.Vivid, AcPalette.VividNames, c => l.hatColor = c));
        if (l.species == "human") rows.Add(ColorRowDef("머리 색", l.hairColor, AcPalette.Hair, AcPalette.HairNames, c => l.hairColor = c));
        if (l.species == "lion") rows.Add(ColorRowDef("갈기 색", l.mane, AcPalette.Fur, AcPalette.FurNames, c => l.mane = c));
        return rows;
    }

    CRow ColorRowDef(string label, Color cur, Color[] list, string[] names, Action<Color> set, bool furPlusVivid = false)
    {
        // 털은 털 색 + 선명한 색 모두 고를 수 있게
        Color[] all = list; string[] allN = names;
        if (furPlusVivid)
        {
            all = new Color[list.Length + AcPalette.Vivid.Length]; list.CopyTo(all, 0); AcPalette.Vivid.CopyTo(all, list.Length);
            allN = new string[all.Length]; names.CopyTo(allN, 0); AcPalette.VividNames.CopyTo(allN, names.Length);
        }
        int i = AcPalette.Nearest(all, cur);
        bool exact = Mathf.Abs(all[i].r - cur.r) + Mathf.Abs(all[i].g - cur.g) + Mathf.Abs(all[i].b - cur.b) < 0.02f;
        return new CRow { label = label, value = exact ? allN[i] : "기본", swatch = cur, step = d => set(all[Wrap(i + d, all.Length)]) };
    }

    void CustomControls()
    {
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
        GUILayout.Space(6);
        var rows = BuildRows(l);
        custRow = Mathf.Clamp(custRow, 0, rows.Count - 1);
        // 키보드 (글 입력 칸에 커서가 없을 때)
        var e = Event.current;
        if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0)
        {
            if (e.keyCode == KeyCode.UpArrow) { custRow = Wrap(custRow - 1, rows.Count); e.Use(); }
            else if (e.keyCode == KeyCode.DownArrow) { custRow = Wrap(custRow + 1, rows.Count); e.Use(); }
            else if (e.keyCode == KeyCode.LeftArrow) { rows[custRow].step(-1); ch = true; e.Use(); }
            else if (e.keyCode == KeyCode.RightArrow) { rows[custRow].step(1); ch = true; e.Use(); }
        }
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            bool sel = i == custRow;
            GUILayout.BeginHorizontal(GUILayout.Height(30));
            GUILayout.Label((sel ? "> " : "   ") + r.label, sel ? sLabelSel : sSmall, GUILayout.Width(92), GUILayout.Height(28));
            if (GUILayout.Button("<", sBtn, GUILayout.Width(30), GUILayout.Height(28))) { custRow = i; r.step(-1); ch = true; }
            var box = GUILayoutUtility.GetRect(150, 28, GUILayout.Width(150), GUILayout.Height(28));
            GUI.DrawTexture(box, AcPalette.Tex(sel ? new Color(1f, 0.93f, 0.8f) : new Color(0.97f, 0.94f, 0.88f)));
            if (r.swatch.HasValue)
            {
                var sw = new Rect(box.x + 5, box.y + 4, 34, 20);
                GUI.DrawTexture(new Rect(sw.x - 1, sw.y - 1, sw.width + 2, sw.height + 2), AcPalette.Tex(new Color(0.45f, 0.35f, 0.28f)));
                GUI.DrawTexture(sw, AcPalette.Tex(r.swatch.Value));
                GUI.Label(new Rect(box.x + 46, box.y + 4, box.width - 50, 22), r.value, sSmall);
            }
            else GUI.Label(new Rect(box.x + 10, box.y + 4, box.width - 14, 22), r.value, sSmall);
            if (GUI.Button(box, "", GUIStyle.none)) custRow = i;
            if (GUILayout.Button(">", sBtn, GUILayout.Width(30), GUILayout.Height(28))) { custRow = i; r.step(1); ch = true; }
            GUILayout.EndHorizontal();
        }
        if (!IsMobile) GUILayout.Label("<size=11>위아래 항목 고르기 / 좌우 바꾸기</size>", sSmall);
        PetCustomControls();
        if (ch) { me.Rebuild(l); PlayerPrefs.SetString("ac_look", l.Encode()); PlayerPrefs.Save(); }
    }

    GUIStyle sLabelSel;

    // ========================================================
    // 오너먼트 아이콘 (실제 모양을 작은 사진으로)
    const int IconLayer = 30;
    Camera iconCam;
    // 장식 아이콘: 렌더 텍스처를 그대로 쓰면 색을 바꿀 때 가끔 까맣게 남음(렌더가 안 된 채 비어 있음)
    //  → 매번 임시 렌더 텍스처에 그린 뒤 Texture2D로 복사해 보관하고, 실패(까만 칸)면 예전 그림을 두고 다음 프레임에 다시 (11-25)
    readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
    Color iconsColor = new Color(-1, 0, 0);

    void RenderIcons(Color c)
    {
        if (iconsColor == c && icons.Count == ornTypes.Count) return;
        EnsureIconCam();
        bool allOk = true;
        foreach (var t in ornTypes)
        {
            GameObject obj;
            if (IsGarland(t)) obj = GarlandIcon(t, c);
            else
            {
                obj = AcCatalog.Spawn(t, null, new Dictionary<string, Color> { { "orn", c } });
                obj.transform.position = new Vector3(0, -500, 0);
                obj.transform.rotation = Quaternion.Euler(0, 15, 0);
            }
            var tex = RenderIconTex(obj, null, 0, 0);
            DestroyImmediate(obj);
            if (tex == null) { allOk = false; continue; }
            if (icons.TryGetValue(t, out var old) && old) Destroy(old);
            icons[t] = tex;
        }
        if (allOk) iconsColor = c;   // 하나라도 실패하면 다음 프레임에 다시
    }

    // obj를 아이콘 카메라로 찍어 96px Texture2D로 돌려줌. 렌더가 안 됐으면(가장자리가 까맘) null
    Texture2D RenderIconTex(GameObject obj, Vector3? camPos, float ortho, float far)
    {
        foreach (var tr in obj.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = IconLayer;
        foreach (var r in obj.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        var oldPos = iconCam.transform.position; float oldSize = iconCam.orthographicSize, oldFar = iconCam.farClipPlane;
        if (camPos.HasValue) iconCam.transform.position = camPos.Value;
        if (ortho > 0) iconCam.orthographicSize = ortho;
        if (far > 0) iconCam.farClipPlane = far;
        var rt = RenderTexture.GetTemporary(96, 96, 16, RenderTextureFormat.ARGB32);
        bool done = false;
        try
        {
            var req = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(iconCam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(iconCam, req); done = true; }
        }
        catch { }
        if (!done) { iconCam.targetTexture = rt; iconCam.Render(); iconCam.targetTexture = null; }
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false) { name = "icon", hideFlags = HideFlags.DontSave };
        tex.ReadPixels(new Rect(0, 0, 96, 96), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        iconCam.transform.position = oldPos; iconCam.orthographicSize = oldSize; iconCam.farClipPlane = oldFar;
        // 배경은 크림색이라 네 귀퉁이가 새까맣다면 그려지지 않은 것
        var p0 = tex.GetPixel(1, 1); var p1 = tex.GetPixel(94, 94);
        if (p0.r + p0.g + p0.b < 0.05f && p1.r + p1.g + p1.b < 0.05f) { Destroy(tex); return null; }
        return tex;
    }

    void EnsureIconCam()
    {
        if (iconCam == null)
        {
            var go = new GameObject("IconCam");
            iconCam = go.AddComponent<Camera>();
            iconCam.enabled = false;
            iconCam.orthographic = true; iconCam.orthographicSize = 0.17f;
            iconCam.clearFlags = CameraClearFlags.SolidColor;
            iconCam.backgroundColor = new Color(1f, 0.97f, 0.9f, 1f);
            iconCam.cullingMask = 1 << IconLayer;
            iconCam.nearClipPlane = 0.1f; iconCam.farClipPlane = 5f;
            go.transform.position = new Vector3(0, -500.135f, 1.2f);
            go.transform.rotation = Quaternion.Euler(0, 180, 0);
            var ad = go.AddComponent<UniversalAdditionalCameraData>(); ad.renderShadows = false; ad.renderPostProcessing = false;
            cam.cullingMask &= ~(1 << IconLayer);
        }
    }

    // ========================================================
    // 부딪히기: 가구/소품마다 바닥 위 상자(회전 포함)를 만들어 캐릭터를 밀어냄
    struct Obb { public Vector3 c; public Vector2 h; public Quaternion rot, inv; }
    readonly List<Obb> obstacles = new List<Obb>();
    static readonly string[] NonSolid = { "rug", "floor", "wall_", "ceiling", "string_lights", "garland", "wreath", "stocking", "mistletoe", "bell_pair",
        "picture", "curtain", "window", "ground", "path", "orn_", "tree_star", "tree_skirt", "tree_giant", "cookie_plate", "cocoa", "candle", "star_lantern",
        "snow_globe", "holly", "table_lamp", "skates", "pond", "light_arch", "sm_" };

    // 아치·문 통로에 놓인 가구는 치움 (지나다닐 수 있게)
    readonly List<(GameObject go, Obb o)> solidObjs = new List<(GameObject, Obb)>();
    void ClearPassages()
    {
        var zones = new List<Rect>();   // x, z 평면
        foreach (var w in walls)
        {
            if (w.kind != "door" && w.kind != "arch") continue;
            zones.Add(w.alongX ? new Rect(w.pos.x - 1.1f, w.pos.z - 1.9f, 2.2f, 3.8f) : new Rect(w.pos.x - 1.9f, w.pos.z - 1.1f, 3.8f, 2.2f));
        }
        zones.Add(new Rect(FrontGapX0 + 0.3f, HalfZ - 1.9f, FrontGapX1 - FrontGapX0 - 0.6f, 3.8f));
        var remove = new HashSet<GameObject>();
        foreach (var (go, o) in solidObjs)
        {
            if (go == null) continue;
            foreach (var z in zones)
            {
                // 상자와 통로 사각형이 겹치는지 (상자 모서리·중심 + 통로 모서리·중심 검사)
                bool hit = false;
                var pts = new List<Vector3> { o.c };
                for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2) pts.Add(o.c + o.rot * new Vector3(sx * o.h.x, 0, sz * o.h.y));
                foreach (var p in pts) if (z.Contains(new Vector2(p.x, p.z))) { hit = true; break; }
                if (!hit)
                    foreach (var q in new[] { z.center, new Vector2(z.xMin, z.yMin), new Vector2(z.xMax, z.yMin), new Vector2(z.xMin, z.yMax), new Vector2(z.xMax, z.yMax) })
                    {
                        var l = o.inv * (new Vector3(q.x, 0, q.y) - o.c);
                        if (Mathf.Abs(l.x) < o.h.x && Mathf.Abs(l.z) < o.h.y) { hit = true; break; }
                    }
                if (hit) { remove.Add(go); break; }
            }
        }
        if (remove.Count == 0) return;
        var keep = new List<Obb>();
        foreach (var (go, o) in solidObjs) if (!remove.Contains(go)) keep.Add(o);
        foreach (var g in remove) Destroy(g);
        obstacles.Clear(); obstacles.AddRange(keep);
        // 바깥 소품 등 solidObjs에 없는 장애물은 원래 없음 (모두 Put으로 만들어짐)
    }

    void AddObstacle(string name, Vector3 pos, float rotY, float scale)
    {
        if (pos.y > 0.05f) return;
        foreach (var n in NonSolid) if (name.Contains(n)) return;
        var md = AcmLoader.Load(name); if (md == null) return;
        var b = md.bounds;
        var rot = Quaternion.Euler(0, rotY, 0);
        float shrink = name.Contains("tree") || name.Contains("pine") || name.Contains("birch") || name.Contains("cedar") ? 0.25f : 0.88f;   // 나무는 줄기 근처만
        var o = new Obb
        {
            c = pos + rot * new Vector3(b.center.x * scale, 0, b.center.z * scale),
            h = new Vector2(b.extents.x * scale * shrink, b.extents.z * scale * shrink),
            rot = rot, inv = Quaternion.Inverse(rot),
        };
        if (o.h.x < 0.05f || o.h.y < 0.05f) return;
        obstacles.Add(o);
    }

    Vector3 Collide(Vector3 p, float r)
    {
        foreach (var o in obstacles)
        {
            var l = o.inv * (p - o.c); l.y = 0;
            float qx = Mathf.Clamp(l.x, -o.h.x, o.h.x), qz = Mathf.Clamp(l.z, -o.h.y, o.h.y);
            float dx = l.x - qx, dz = l.z - qz, d2 = dx * dx + dz * dz;
            if (d2 >= r * r) continue;
            if (d2 > 1e-8f) { float d = Mathf.Sqrt(d2); l.x = qx + dx / d * r; l.z = qz + dz / d * r; }
            else
            {   // 상자 안쪽: 가장 가까운 면으로
                float px = o.h.x - Mathf.Abs(l.x), pz = o.h.y - Mathf.Abs(l.z);
                if (px < pz) l.x = Mathf.Sign(l.x) * (o.h.x + r); else l.z = Mathf.Sign(l.z) * (o.h.y + r);
            }
            var w = o.c + o.rot * l; p.x = w.x; p.z = w.z;
        }
        if (slots != null)   // 온실: 심은 화분만 막힘 (빈 화분은 지나감)
            foreach (var sl in slots)
            {
                if (!sl.planted) continue;
                var d = new Vector2(p.x - sl.pos.x, p.z - sl.pos.z); float rr = 0.32f + r;
                if (d.sqrMagnitude < rr * rr && d.sqrMagnitude > 1e-8f) { d = d.normalized * rr; p.x = sl.pos.x + d.x; p.z = sl.pos.z + d.y; }
            }
        foreach (var c in snowCircles)   // 마당의 눈사람
        {
            var d = new Vector2(p.x - c.x, p.z - c.y); float rr = c.z + r;
            if (d.sqrMagnitude < rr * rr && d.sqrMagnitude > 1e-8f) { d = d.normalized * rr; p.x = c.x + d.x; p.z = c.y + d.y; }
        }
        return PushFromPeers(p, 0.28f);   // 다른 참가자와 겹치지 않게
    }

    // 이동 범위: 마당 끝 + 벽 (문·아치·앞 출입구로만 지나감)
    Vector3 ClampRegion(Vector3 prev, Vector3 p)
    {
        p.x = Mathf.Clamp(p.x, WorldX0, WorldX1);
        p.z = Mathf.Clamp(p.z, WorldZ0, WorldZ1);
        return WallCollide(prev, p, 0.36f);
    }

    bool Outside => me != null && !InHouse(me.transform.position);
    bool BackYard => me != null && !InHouse(me.transform.position) && me.transform.position.z < -HalfZ;

    // ========================================================
    // 오너먼트 놓을 자리: 몸통이 트리 표면 밖으로 살짝 나오게 (깊은 곳은 살짝 묻히고, 튀어나온 곳은 그대로)
    float SurfaceR(Vector3 radial, float y)
    {
        var from = TreePos + radial * 8f + Vector3.up * y;
        if (treeCol.Raycast(new Ray(from, -radial), out var h, 8f))
        {
            var d = h.point - TreePos; d.y = 0; return d.magnitude;
        }
        return -1;
    }

    void OrnPlacement(RaycastHit hit, out Vector3 pos, out float ry)
    {
        var hp = hit.point;
        var radial = hp - TreePos; radial.y = 0;
        if (radial.sqrMagnitude < 1e-4f) radial = Vector3.forward;
        radial.Normalize();
        ry = Mathf.Atan2(radial.x, radial.z) * Mathf.Rad2Deg;
        float drop = 0.11f * OrnScale, bodyR = 0.07f * OrnScale;
        float hookR = new Vector2(hp.x - TreePos.x, hp.z - TreePos.z).magnitude;
        float need = hookR;
        float s1 = SurfaceR(radial, hp.y - drop), s2 = SurfaceR(radial, hp.y - drop * 0.5f);
        if (s1 > 0) need = Mathf.Max(need, s1 + bodyR * 0.35f);
        if (s2 > 0) need = Mathf.Max(need, s2 + bodyR * 0.1f);
        pos = hp + radial * Mathf.Max(0, need - hookR) + Vector3.up * 0.0f;
    }

    // 포인터 강조 (반짝 고리 + 작은 빛)
    GameObject focusRing; Light focusLight;
    void FocusAt(Vector3 bodyCenter, bool on)
    {
        if (focusRing == null)
        {
            focusRing = new GameObject("FocusRing");
            var mf = focusRing.AddComponent<MeshFilter>(); mf.sharedMesh = RingMesh(0.27f, 0.33f, 48);
            var mr = focusRing.AddComponent<MeshRenderer>();
            var mat = new Material(AcmLoader.Mat); mat.SetFloat("_Emit", 1); mat.SetFloat("_HasTint", 0);
            mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            focusLight = new GameObject("FocusLight").AddComponent<Light>();
            focusLight.type = LightType.Point; focusLight.range = 2.6f; focusLight.intensity = 1.6f; focusLight.color = new Color(1f, 0.92f, 0.7f);
            focusLight.shadows = LightShadows.None;
        }
        focusRing.SetActive(on); focusLight.gameObject.SetActive(on);
        if (!on) return;
        float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.07f;
        focusRing.transform.position = bodyCenter + (cam.transform.position - bodyCenter).normalized * 0.3f;
        focusRing.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up) * Quaternion.Euler(0, 0, Time.time * 40f);
        focusRing.transform.localScale = Vector3.one * OrnScale * 0.42f * pulse;
        focusLight.transform.position = bodyCenter + (cam.transform.position - bodyCenter).normalized * 0.6f;
    }

    static Mesh RingMesh(float r0, float r1, int n)
    {
        var v = new List<Vector3>(); var c = new List<Color32>(); var uv = new List<Vector2>(); var nr = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float a = i * Mathf.PI * 2 / n;
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            // 점선처럼 반짝이게: 짝수 칸은 밝게, 홀수 칸은 살짝 어둡게
            var col = (i / 3) % 2 == 0 ? new Color32(255, 246, 200, 255) : new Color32(255, 214, 120, 255);
            v.Add(d * r0); v.Add(d * r1); c.Add(col); c.Add(col);
            uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 0)); nr.Add(Vector3.back); nr.Add(Vector3.back);
            if (i < n) { int k = i * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
        }
        var m = new Mesh { name = "ring" };
        m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetNormals(nr); m.SetTriangles(t, 0);
        return m;
    }

    // ========================================================
    // 바깥에 내리는 눈 (실내 위에는 안 내림)
    void BuildSnow()
    {
        var flake = SnowFlakeMesh();
        var mat = new Material(AcmLoader.Mat) { name = "Snow" }; snowMat = mat;
        snowSystems.Clear();
        mat.SetFloat("_Emit", 0.55f); mat.SetFloat("_HasTint", 0);
        float wx = 2 * HouseHalf + 2 * YardW + 8, sy = 20f, houseD = HalfZ - HouseZ0;
        // (중심, 크기, 개수/초) — 높은 곳(20m)에서 내려서 트리 전경을 볼 때도 위에서부터 내림
        var boxes = new[]
        {
            (new Vector3(0, sy, HouseZ0 - YardBack / 2 - 1f), new Vector3(wx, 0.1f, YardBack + 2f), 520f),     // 뒤
            (new Vector3(0, sy, HalfZ + YardFront / 2 + 1f), new Vector3(wx, 0.1f, YardFront + 2f), 430f),     // 앞
            (new Vector3(-HouseHalf - YardW / 2 - 1f, sy, (HalfZ + HouseZ0) / 2), new Vector3(YardW + 2f, 0.1f, houseD), 110f),  // 왼쪽
            (new Vector3(HouseHalf + YardW / 2 + 1f, sy, (HalfZ + HouseZ0) / 2), new Vector3(YardW + 2f, 0.1f, houseD), 110f),   // 오른쪽
            (new Vector3(0, sy, (-HalfZ + HouseZ0) / 2), new Vector3(2 * HalfX, 0.1f, RoomD), 90f),                              // 안뜰
        };
        foreach (var (c, size, rate) in boxes)
        {
            var go = new GameObject("Snow");
            go.transform.SetParent(world, false);
            go.transform.position = c;
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.duration = 18f;
            main.startLifetime = 17f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = (int)(rate * 19);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;
            var em = ps.emission; em.rateOverTime = rate * (IsMobile ? 0.5f : 1f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            vel.y = new ParticleSystem.MinMaxCurve(-1.05f, -1.45f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = flake; r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            snowSystems.Add(ps);
            go.SetActive(true);
            ps.Play();
        }
    }

    Material snowMat;
    // 집 안으로 날려 들어온 눈 지우기: 프레임마다 눈 무리 하나씩 돌아가며, 집(거실·방) 위에 있는 눈송이를 없앰
    // (파티클 트리거는 웹에서 일부 방에만 먹어서 직접 검사로 바꿈, 11-28)
    readonly List<ParticleSystem> snowSystems = new List<ParticleSystem>();
    ParticleSystem.Particle[] snowBuf; int snowCullIdx;
    void SnowCull()
    {
        if (snowSystems.Count == 0) return;
        snowCullIdx = (snowCullIdx + 1) % snowSystems.Count;
        var ps = snowSystems[snowCullIdx]; if (!ps) return;
        int max = ps.main.maxParticles;
        if (snowBuf == null || snowBuf.Length < max) snowBuf = new ParticleSystem.Particle[max];
        int n = ps.GetParticles(snowBuf);
        bool changed = false;
        for (int k = 0; k < n; k++)
        {
            var p = snowBuf[k].position;
            if (p.y < 14f && InHouse(p)) { snowBuf[k].remainingLifetime = 0f; changed = true; }   // 집 지붕 높이 위쪽(14m 이상)은 그대로 둬서 멀리서 볼 때 하늘엔 눈이 보이게
        }
        if (changed) ps.SetParticles(snowBuf, n);
    }

    static Mesh SnowFlakeMesh()
    {
        // 작은 이십면체에 가까운 공 (지름 약 7cm)
        var m = new Mesh { name = "flake" };
        var verts = new List<Vector3>(); var tris = new List<int>();
        int seg = 8, ring = 4; float rad = 0.04f;   // 5x3이면 가까이서 육각형으로 보여서 조금 더 둥글게
        for (int j = 0; j <= ring; j++)
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI * j / ring, ph = 2 * Mathf.PI * i / seg;
                verts.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * rad);
            }
        for (int j = 0; j < ring; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * (seg + 1) + i, b = a + seg + 1;
                tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        var cols = new Color32[verts.Count]; var uvs = new Vector2[verts.Count]; var nrm = new Vector3[verts.Count];
        for (int i = 0; i < verts.Count; i++) { cols[i] = new Color32(255, 255, 255, 0); uvs[i] = new Vector2(1, 0); nrm[i] = verts[i].normalized; }
        m.SetVertices(verts); m.SetTriangles(tris, 0); m.colors32 = cols; m.uv = uvs; m.normals = nrm;
        return m;
    }
}
