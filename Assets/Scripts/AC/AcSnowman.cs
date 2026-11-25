// AcSnowman.cs - 바깥에서 눈사람 만들기: 2단/3단, 단마다 크기, 맨 위 모양(기본 모양 + 캐릭터 얼굴),
//               장식을 눈덩이에 눌러서 붙이기, 모자·목도리. 완성하면 그 자리에 세워지고 모두에게 보임 (rooms/<방>/snow/<id>)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable] public class SnowDeco { public string m; public int t; public float x, y, z; public string c = ""; public float s = 1f; }
[Serializable] public class SnowData
{
    public int tiers = 3;
    public float r0 = 0.5f, r1 = 0.38f, r2 = 0.28f;
    public string head = "round", hat = "sm_bucket", hatC = "", scarfC = "E53935";
    public List<SnowDeco> d = new List<SnowDeco>();
    public float R(int i) => i == 0 ? r0 : i == 1 ? r1 : r2;
    public void SetR(int i, float v) { if (i == 0) r0 = v; else if (i == 1) r1 = v; else r2 = v; }
}

public partial class AcRoom
{
    // ===== 고를 수 있는 것들 =====
    static readonly string[] HeadIds = { "round", "tall", "wide", "egg", "bear", "cat", "rabbit", "dog", "fox", "panda", "lion", "pig", "human" };
    static readonly string[] HeadKo = { "동그라미", "길쭉한 동그라미", "납작한 동그라미", "달걀", "곰 얼굴", "고양이 얼굴", "토끼 얼굴", "강아지 얼굴", "여우 얼굴", "판다 얼굴", "사자 얼굴", "돼지 얼굴", "사람 얼굴" };
    static readonly string[] SnowDecoIds = { "sm_coal", "sm_carrot", "sm_smile", "sm_button", "sm_arm", "sm_mitten", "sm_nose_round", "sm_blush", "sm_glasses",
                                             "sm_mustache", "sm_pipe", "sm_bowtie", "sm_flower", "sm_heart", "sm_star", "sm_holly", "sm_twig_hair", "sm_broom" };
    static readonly string[] SnowHatIds = { "", "sm_bucket", "hat_santa", "hat_beanie", "hat_earmuffs", "hat_antlers", "hat_tophat", "hat_elf" };
    static readonly string[] SnowHatKo = { "없음", "양동이", "산타 모자", "털모자", "귀마개", "순록 뿔", "중절모", "요정 모자" };
    static readonly float[] SizeSteps = { 0.22f, 0.27f, 0.32f, 0.38f, 0.44f, 0.5f, 0.56f, 0.62f };
    static readonly string[] SizeKo = { "아주 작게", "작게", "조금 작게", "보통", "조금 크게", "크게", "아주 크게", "제일 크게" };
    static readonly string[] TierKo = { "아래 눈덩이", "가운데 눈덩이", "맨 위 눈덩이" };

    static Vector3 HeadScale(string head)
    {
        switch (head)
        {
            case "tall": return new Vector3(0.86f, 1.24f, 0.86f);
            case "wide": return new Vector3(1.16f, 0.8f, 1.1f);
            case "egg": return new Vector3(0.93f, 1.12f, 0.93f);
            case "round": return Vector3.one;
            default: return new Vector3(1.06f, 0.97f, 0.97f);   // 캐릭터 머리
        }
    }
    static bool CharHead(string head) => Array.IndexOf(HeadIds, head) >= 4;

    // 단마다 중심 높이와 반지름(세 축)
    static void Layout(SnowData s, List<Vector3> centers, List<Vector3> radii)
    {
        centers.Clear(); radii.Clear();
        float top = 0;
        for (int i = 0; i < s.tiers; i++)
        {
            float r = s.R(i);
            Vector3 sc = i == s.tiers - 1 ? HeadScale(s.head) : Vector3.one;
            var rad = sc * r;
            float cy = i == 0 ? rad.y * 0.9f : top + rad.y * 0.72f;
            centers.Add(new Vector3(0, cy, 0)); radii.Add(rad);
            top = cy + rad.y * 0.78f;
        }
    }

    static float DecoBase(float r) => Mathf.Clamp(r / 0.22f, 0.9f, 2.8f);

    // ===== 눈사람 한 개 만들기 =====
    GameObject BuildSnowman(SnowData s, Transform parent)
    {
        var root = new GameObject("Snowman").transform;
        root.SetParent(parent, false);
        var cs = new List<Vector3>(); var rs = new List<Vector3>();
        Layout(s, cs, rs);
        var snowWhite = new Dictionary<string, Color> { { "fur", new Color(0.98f, 0.985f, 1f) }, { "mane", new Color(0.95f, 0.96f, 1f) }, { "sweater", Color.white } };
        for (int i = 0; i < s.tiers; i++)
        {
            bool headTier = i == s.tiers - 1;
            if (headTier && CharHead(s.head))
            {
                float sc = s.R(i) / 0.32f;
                var g = AcmLoader.Spawn("char_" + s.head, root, snowWhite);
                g.transform.localPosition = cs[i] - new Vector3(0, 0.74f * sc, 0);
                g.transform.localScale = Vector3.one * sc;
                foreach (var part in new[] { "torso", "armL", "armR", "handL", "handR", "legL", "legR", "tail" }) HidePart(g, part);
                if (s.head == "human")
                {
                    var f = AcmLoader.Spawn("face_f1", root, null, null, false);
                    f.transform.localPosition = g.transform.localPosition; f.transform.localScale = g.transform.localScale;
                }
            }
            else
            {
                var b = AcmLoader.Spawn("sm_snowball", root);
                b.transform.localPosition = cs[i];
                b.transform.localScale = rs[i] * 2f;
                b.transform.localRotation = Quaternion.Euler(0, i * 77, 0);
            }
        }
        foreach (var dd in s.d)
        {
            if (dd.t < 0 || dd.t >= s.tiers || !AcmLoader.Exists(dd.m)) continue;
            PlaceDeco(root, dd, cs[dd.t], rs[dd.t], s.R(dd.t));
        }
        int ht = s.tiers - 1;
        if (!string.IsNullOrEmpty(s.hat) && AcmLoader.Exists(s.hat))
        {
            var tints = string.IsNullOrEmpty(s.hatC) ? null : new Dictionary<string, Color> { { "hat", Hex(s.hatC) } };
            var h = AcmLoader.Spawn(s.hat, root, tints);
            float topY = cs[ht].y + rs[ht].y;
            if (s.hat == "sm_bucket")
            {
                float sc = rs[ht].x / 0.17f * 0.92f;
                h.transform.localScale = Vector3.one * sc;
                h.transform.localPosition = new Vector3(0, topY - 0.32f * rs[ht].y, 0);
                h.transform.localRotation = Quaternion.Euler(-8, 0, 6);
            }
            else
            {
                float sc = rs[ht].x / 0.32f;
                h.transform.localScale = Vector3.one * sc;
                h.transform.localPosition = new Vector3(0, topY - 1.04f * sc - 0.02f * sc, 0);
            }
        }
        if (!string.IsNullOrEmpty(s.scarfC) && s.tiers >= 2)
        {
            var sc = AcmLoader.Spawn("sm_scarf", root, new Dictionary<string, Color> { { "scarf", Hex(s.scarfC) } });
            float neckR = Mathf.Min(rs[ht].x, rs[ht - 1].x) * 0.8f;
            sc.transform.localScale = Vector3.one * (neckR / 0.2f);
            sc.transform.localPosition = new Vector3(0, cs[ht].y - rs[ht].y * 0.8f, 0);
        }
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        return root.gameObject;
    }

    static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.white;
    static string HexOf(Color c) => ColorUtility.ToHtmlStringRGB(c);

    // 표면의 점(방향 d)에 장식 붙이기: +z = 바깥, +y = 위쪽
    GameObject PlaceDeco(Transform root, SnowDeco dd, Vector3 center, Vector3 rad, float r)
    {
        var dir = new Vector3(dd.x, dd.y, dd.z);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        dir.Normalize();
        var p = center + Vector3.Scale(dir, rad);
        var n = new Vector3(dir.x / rad.x, dir.y / rad.y, dir.z / rad.z).normalized;   // 타원 표면의 법선
        var up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
        if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
        Dictionary<string, Color> tints = null;
        var it = AcCatalog.Get(dd.m);
        if (!string.IsNullOrEmpty(dd.c) && it != null && it.groups != null && it.groups.Length > 0) tints = new Dictionary<string, Color> { { it.groups[0], Hex(dd.c) } };
        var g = AcmLoader.Spawn(dd.m, root, tints);
        g.transform.localPosition = p - n * 0.01f;
        g.transform.localRotation = Quaternion.LookRotation(n, up.normalized);
        g.transform.localScale = Vector3.one * DecoBase(r) * dd.s;
        return g;
    }

    // ===== 마당의 눈사람들 (모두 보기) =====
    readonly Dictionary<string, (GameObject go, string ver)> snowObjs = new Dictionary<string, (GameObject, string)>();
    readonly List<Vector3> snowCircles = new List<Vector3>();   // x, z, 반지름
    float nextSnowPoll;

    void PollSnowmen()
    {
        if (net == null || Time.unscaledTime < nextSnowPoll) return;
        nextSnowPoll = Time.unscaledTime + 0.6f;
        var node = net.Get("snow");
        var alive = new HashSet<string>();
        if (node != null)
            foreach (var kv in node)
            {
                if (!(kv.Value is Dictionary<string, object> d)) continue;
                alive.Add(kv.Key);
                string ver = AcJson.S(d, "ct") + AcJson.S(d, "d").Length;
                if (snowObjs.TryGetValue(kv.Key, out var cur) && cur.ver == ver) continue;
                if (cur.go) Destroy(cur.go);
                SnowData sd = null;
                try { sd = JsonUtility.FromJson<SnowData>(AcJson.S(d, "d")); } catch { }
                if (sd == null) continue;
                sd.tiers = Mathf.Clamp(sd.tiers, 2, 3);
                var go = BuildSnowman(sd, world);
                go.name = "Snowman " + AcJson.S(d, "name");
                go.transform.position = new Vector3(AcJson.F(d, "x"), 0, AcJson.F(d, "z"));
                go.transform.rotation = Quaternion.Euler(0, AcJson.F(d, "ry"), 0);
                snowObjs[kv.Key] = (go, ver);
            }
        var gone = new List<string>();
        foreach (var kv in snowObjs) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var k in gone) { if (snowObjs[k].go) Destroy(snowObjs[k].go); snowObjs.Remove(k); }
        snowCircles.Clear();
        foreach (var kv in snowObjs)
            if (kv.Value.go)
            {
                var p = kv.Value.go.transform.position;
                float r = 0.5f;
                if (node != null && node.TryGetValue(kv.Key, out var v) && v is Dictionary<string, object> d)
                    try { r = JsonUtility.FromJson<SnowData>(AcJson.S(d, "d")).r0; } catch { }
                snowCircles.Add(new Vector3(p.x, p.z, r + 0.05f));
            }
    }

    // ===== 만들기 모드 =====
    bool building;
    SnowData draft;
    Transform draftRoot;
    Vector3 draftPos; float draftRy;
    int snowTab, snowDecoSel, snowColSel, snowSizeSel = 1;
    readonly List<Vector3> dCenters = new List<Vector3>(), dRadii = new List<Vector3>();
    GameObject snowGhost; string snowGhostKey = "";

    bool CanBuildHere(out Vector3 pos, out float ry, out string why)
    {
        var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
        var right = Vector3.Cross(Vector3.up, fwd);
        pos = me.transform.position + fwd * 1.3f + right * 1.2f; pos.y = 0;
        ry = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg;   // 카메라를 바라보게
        why = "";
        if (DistToHouse(pos) < 1.4f) { why = "집에서 조금 더 떨어져서 만들어요"; return false; }
        if (pos.x < WorldX0 + 1 || pos.x > WorldX1 - 1 || pos.z < WorldZ0 + 1 || pos.z > WorldZ1 - 1) { why = "마당 안쪽에서 만들어요"; return false; }
        foreach (var c in snowCircles) if (new Vector2(c.x - pos.x, c.y - pos.z).magnitude < c.z + 0.8f) { why = "옆에 눈사람이 있어요. 조금 옮겨서 만들어요"; return false; }
        return true;
    }

    void StartBuilding()
    {
        if (!CanBuildHere(out var pos, out var ry, out var why)) { Toast(why); return; }
        building = true; decorating = false; showCustom = false;
        draftPos = pos; draftRy = ry;
        draft = new SnowData();
        var saved = PlayerPrefs.GetString("ac_snow_last", "");
        if (saved != "") try { var old = JsonUtility.FromJson<SnowData>(saved); if (old != null) { draft.tiers = old.tiers; draft.r0 = old.r0; draft.r1 = old.r1; draft.r2 = old.r2; draft.head = old.head; } } catch { }
        snowTab = 0; snowPicked = true; decoZoom = 1f; snowYaw = draftRy + 180f;
        RebuildDraft();
        walkTarget = null; followId = null;
        var look = draftPos - me.transform.position; look.y = 0;
        if (look.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
    }

    void RebuildDraft()
    {
        if (draftRoot) Destroy(draftRoot.gameObject);
        draftRoot = BuildSnowman(draft, world).transform;
        draftRoot.name = "SnowDraft";
        draftRoot.position = draftPos; draftRoot.rotation = Quaternion.Euler(0, draftRy, 0);
        Layout(draft, dCenters, dRadii);
    }

    void StopBuilding()
    {
        building = false;
        if (draftRoot) Destroy(draftRoot.gameObject);
        if (snowGhost) Destroy(snowGhost);
        snowGhostKey = "";
    }

    void FinishSnowman()
    {
        if (net == null) return;
        string id = "s" + Guid.NewGuid().ToString("N").Substring(0, 10);
        string dj = JsonUtility.ToJson(draft);
        string json = "{\"id\":" + AcJson.Esc(id) + ",\"owner\":" + AcJson.Esc(myId) + ",\"name\":" + AcJson.Esc(uiName.Trim()) +
                      ",\"x\":" + F(draftPos.x) + ",\"z\":" + F(draftPos.z) + ",\"ry\":" + F(draftRy) + ",\"ct\":" + AcSync.UnixMs.ToString("0") +
                      ",\"d\":" + AcJson.Esc(dj) + "}";
        net.Put("snow/" + id, json);
        PlayerPrefs.SetString("ac_snow_last", dj); PlayerPrefs.Save();
        StopBuilding();
        nextSnowPoll = 0;
        Emote("cheer");
        Toast("눈사람 완성! 모두가 볼 수 있어요");
    }

    static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    string CurDeco => SnowDecoIds[Wrap(snowDecoSel, SnowDecoIds.Length)];
    bool DecoTintable(string m) { var it = AcCatalog.Get(m); return it != null && it.groups != null && it.groups.Length > 0; }
    static readonly float[] DecoSizes = { 0.75f, 1f, 1.35f };
    static readonly string[] DecoSizeKo = { "작게", "보통", "크게" };

    // 포인터가 가리키는 눈덩이 찾기 (타원과 광선 만나는 점)
    bool PickTier(Vector2 screen, out int tier, out Vector3 localDir, out float hitDist)
    {
        tier = -1; localDir = Vector3.zero; hitDist = float.MaxValue;
        if (!draftRoot) return false;
        var ray = cam.ScreenPointToRay(screen);
        var inv = Quaternion.Inverse(draftRoot.rotation);
        var o = inv * (ray.origin - draftRoot.position); var dv = inv * ray.direction;
        for (int i = 0; i < dCenters.Count; i++)
        {
            var rad = dRadii[i] * 1.02f;
            var oo = Vector3.Scale(o - dCenters[i], new Vector3(1 / rad.x, 1 / rad.y, 1 / rad.z));
            var dd = Vector3.Scale(dv, new Vector3(1 / rad.x, 1 / rad.y, 1 / rad.z));
            float a = Vector3.Dot(dd, dd), b = 2 * Vector3.Dot(oo, dd), c = Vector3.Dot(oo, oo) - 1;
            float disc = b * b - 4 * a * c;
            if (disc < 0) continue;
            float t = (-b - Mathf.Sqrt(disc)) / (2 * a);
            if (t < 0 || t >= hitDist) continue;
            hitDist = t; tier = i;
            localDir = (oo + dd * t).normalized;   // 단위 구 위의 방향 = 타원 위 방향
        }
        return tier >= 0;
    }

    int PickDeco(Vector2 screen, float maxDist)
    {
        if (!draftRoot) return -1;
        var ray = cam.ScreenPointToRay(screen);
        int best = -1; float bd = 0.12f;
        for (int i = 0; i < draft.d.Count; i++)
        {
            var dd = draft.d[i];
            if (dd.t >= dCenters.Count) continue;
            var dir = new Vector3(dd.x, dd.y, dd.z).normalized;
            var wp = draftRoot.TransformPoint(dCenters[dd.t] + Vector3.Scale(dir, dRadii[dd.t]));
            float along = Vector3.Dot(wp - ray.origin, ray.direction);
            if (along < 0 || along > maxDist + 0.3f) continue;
            float dist = Vector3.Cross(ray.direction, wp - ray.origin).magnitude;
            if (dist < bd) { bd = dist; best = i; }
        }
        return best;
    }

    void BuildingUpdate()
    {
        if (!draftRoot) { building = false; return; }
        me.speed = 0;
        // 카메라: 끌어서 눈사람 둘레를 돌기, 두 손가락·휠로 확대·축소 (장식을 고른 채 눈사람을 누르면 돌지 않고 장식 자리 잡기)
        bool pin = PinchZoom();
        if (ptrDown)
        {
            longUsed = false; if (!pinching) pinchUsed = false;
            snowPressOn = snowTab == 1 && !OverUI(ptrStart) && PickTier(ptrStart, out _, out _, out _);
            var u = new Vector2(ptrStart.x / UiK, (Screen.height - ptrStart.y) / UiK);
            if (snowTab == 1) foreach (var (rr, idx) in decoIconRects) if (rr.Contains(u)) dragCand = idx;
        }
        if (!pin && ptrPressed && ptrMoved && !ptrOnUI && dragCand < 0 && !(snowTab == 1 && snowPicked && snowPressOn)) snowYaw += (ptrPos - lastPtr).x * 0.25f;
        lastPtr = ptrPos;
        { float sc = OverUI(ptrPos) ? 0 : Scroll(); if (Mathf.Abs(sc) > 0.01f) decoZoom = Mathf.Clamp(decoZoom * (sc > 0 ? 0.9f : 1.1f), 0.5f, 3.5f); }
        // 장식 그림을 끌어 와서 놓기
        if (snowTab == 1 && dragCand >= 0 && (ptrPressed || ptrUp) && !OverUI(ptrPos) && !dragOrn && !pinching) { dragOrn = true; snowDecoSel = dragCand; snowPicked = true; }
        if (dragOrn && ptrUp) { SnowDropAt(ptrPos); dragOrn = false; dragCand = -1; return; }
        if (!ptrPressed && !ptrUp && !ptrDown) { dragCand = -1; dragOrn = false; }
        bool over = !OverUI(ptrPos) && snowTab == 1 && snowPicked;
        int tier = -1; Vector3 dir = Vector3.forward; float dist = 0;
        bool hit = over && PickTier(ptrPos, out tier, out dir, out dist);
        // 미리보기 장식 (가리키는 곳)
        string key = CurDeco + snowColSel + snowSizeSel;
        if (hit && !IsMobileTouchIdle())
        {
            if (snowGhost == null || snowGhostKey != key)
            {
                if (snowGhost) Destroy(snowGhost);
                snowGhost = PlaceDeco(draftRoot, NewDeco(0, Vector3.forward), Vector3.zero, Vector3.one * 0.3f, 0.3f);
                snowGhostKey = key;
            }
            snowGhost.SetActive(true);
            var tmp = NewDeco(tier, dir);
            var n = new Vector3(dir.x / dRadii[tier].x, dir.y / dRadii[tier].y, dir.z / dRadii[tier].z).normalized;
            var up = Vector3.up - n * Vector3.Dot(Vector3.up, n); if (up.sqrMagnitude < 1e-4f) up = Vector3.forward;
            snowGhost.transform.localPosition = dCenters[tier] + Vector3.Scale(dir, dRadii[tier]) - n * 0.01f;
            snowGhost.transform.localRotation = Quaternion.LookRotation(n, up.normalized);
            snowGhost.transform.localScale = Vector3.one * DecoBase(draft.R(tier)) * tmp.s;
        }
        else if (snowGhost) snowGhost.SetActive(false);

        if (snowTab == 1)
        {
            bool remove = MouseDown(1) || (LongPress && !longUsed);
            if (remove)
            {
                longUsed = true;
                int di = PickDeco(ptrPos, hit ? dist : 50f);
                if (di >= 0) { draft.d.RemoveAt(di); RebuildDraft(); }
            }
            else if (snowPicked && ptrUp && !ptrOnUI && snowPressOn && !pinching && !pinchUsed && !longUsed && hit && draft.d.Count < 60)
            {
                draft.d.Add(NewDeco(tier, dir));   // 누른 채 자리 잡고 손을 떼면 붙음
                RebuildDraft();
            }
        }
        if (Key("esc")) StopBuilding();
    }

    bool IsMobileTouchIdle() => IsMobile && !ptrPressed && !ptrUp;
    bool snowPicked = true, snowPressOn; float snowYaw;

    // 그림을 끌어다 눈사람 위에 놓기
    void SnowDropAt(Vector2 screen)
    {
        if (!building || !draftRoot || OverUI(screen) || draft.d.Count >= 60) return;
        if (!PickTier(screen, out int tier, out var dir, out _)) return;
        draft.d.Add(NewDeco(tier, dir)); RebuildDraft();
    }

    SnowDeco NewDeco(int tier, Vector3 dir)
    {
        string m = CurDeco;
        return new SnowDeco { m = m, t = tier, x = dir.x, y = dir.y, z = dir.z, s = DecoSizes[Mathf.Clamp(snowSizeSel, 0, 2)],
                              c = DecoTintable(m) ? HexOf(AcPalette.Vivid[Wrap(snowColSel, AcPalette.Vivid.Length)]) : "" };
    }

    // 카메라: 눈사람 전체가 보이게
    bool SnowCamera(ref Vector3 focus, ref float dist, ref float pitch)
    {
        if (!building || !draftRoot) return false;
        float h = dCenters.Count > 0 ? dCenters[dCenters.Count - 1].y + dRadii[dRadii.Count - 1].y + 0.3f : 1.5f;
        bool portrait = Screen.height > Screen.width;
        // 아래 패널(화면 약 40%) 위쪽에 눈사람 전체가 들어오게: 화면 가운데는 눈사람 아래쪽을 보게 함
        focus = draftPos + Vector3.up * (h * (portrait ? 0.05f : 0.14f));
        float vfov = cam.fieldOfView * Mathf.Deg2Rad;
        float need = h / (portrait ? 0.42f : 0.55f);                 // 보여야 할 세로 길이
        if (portrait) need = Mathf.Max(need, 2.2f / cam.aspect);    // 세로 화면은 좌우도 들어오게
        dist = need / (2f * Mathf.Tan(vfov / 2f)) * decoZoom + 0.6f;
        pitch = 10f;
        return true;
    }

    // ===== 아이콘 (실제 모양) =====
    // 아이콘은 화면 그리기(OnGUI) 중에 만들지 않고, 필요한 것만 적어 두었다가 Update에서 만듦 (11-25: 색을 바꾸면 까맣게 나오던 문제)
    readonly Dictionary<string, Texture2D> snowIcons = new Dictionary<string, Texture2D>();
    readonly Dictionary<string, (string model, Color c)> snowIconWant = new Dictionary<string, (string, Color)>();
    readonly Dictionary<string, Texture2D> snowIconLast = new Dictionary<string, Texture2D>();   // 모양별 마지막으로 성공한 그림 (새 색 그리는 동안 보여 줌)

    Texture SnowIcon(string model, Color c)
    {
        string key = model + HexOf(c);
        if (snowIcons.TryGetValue(key, out var tex) && tex) return tex;
        snowIconWant[key] = (model, c);
        return snowIconLast.TryGetValue(model, out var last) && last ? last : null;
    }

    void ProcessSnowIcons()
    {
        if (snowIconWant.Count == 0) return;
        EnsureIconCam();
        int n = 0;
        foreach (var kv in snowIconWant.ToList())
        {
            if (n++ >= 8) break;   // 한 프레임에 너무 많이 만들지 않기
            var tex = MakeSnowIcon(kv.Value.model, kv.Value.c);
            if (tex == null) continue;   // 실패하면 남겨 두고 다음 프레임에 다시
            snowIcons[kv.Key] = tex; snowIconLast[kv.Value.model] = tex;
            snowIconWant.Remove(kv.Key);
        }
    }

    Texture2D MakeSnowIcon(string model, Color c)
    {
        var it = AcCatalog.Get(model);
        var tints = c.a > 0 && it != null && it.groups != null && it.groups.Length > 0 ? new Dictionary<string, Color> { { it.groups[0], c } } : null;
        var obj = AcCatalog.Spawn(model, null, tints);
        obj.transform.rotation = Quaternion.Euler(8, 25, 0);
        obj.transform.position = Vector3.zero;
        var bounds = new Bounds(); bool first = true;
        foreach (var r in obj.GetComponentsInChildren<MeshRenderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
        obj.transform.position = new Vector3(0, -500, 0) - bounds.center;
        var tex = RenderIconTex(obj, new Vector3(0, -500, 2.5f), Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.25f + 0.005f, 6f);
        DestroyImmediate(obj);
        return tex;
    }

    // ===== 화면 아래 패널 =====
    Vector2 snowDecoScroll;
    void SnowBar()
    {
        bool narrow = SW < 640;
        float h = narrow ? 300 : 220, y = SH - h - 8;
        var box = new Rect(10, y, SW - 20, h);
        GUI.Box(R(box), "", sBox);
        string[] tabs = { "눈덩이", "장식 붙이기", "모자/목도리" };
        float tw = narrow ? (SW - 44 - 12) / 3f : 120;
        for (int i = 0; i < tabs.Length; i++)
        {
            var tr = new Rect(22 + i * (tw + 6), y + 8, tw, 30);
            if (i == snowTab) AcUiTheme.Ring(tr, AcUiTheme.Accent, 3);
            if (GUI.Button(tr, tabs[i], sBtn)) snowTab = i;
        }
        float bw = narrow ? 84 : 90;
        // 좁은 화면: 취소·완성은 패널 위 오른쪽에 따로 (탭과 겹치지 않게)
        float by = narrow ? y - 48 : y + 8, bh = narrow ? 38 : 30;
        var cr = new Rect(SW - 20 - bw * 2 - 18, by, bw, bh); var fr = new Rect(SW - 20 - bw - 8, by, bw, bh);
        R(cr); R(fr);
        if (GUI.Button(cr, "취소", sBtn)) { StopBuilding(); return; }
        if (GUI.Button(fr, "<b>완성!</b>", sBtn)) { FinishSnowman(); return; }

        float cy = y + 48, lx = 26, labelW = narrow ? 100 : 130, valW = narrow ? 140 : 170;
        bool changed = false;
        if (snowTab == 0)
        {
            ArrowRow(ref cy, lx, labelW, valW, "단", draft.tiers + "단", d => { draft.tiers = draft.tiers == 2 ? 3 : 2; changed = true; });
            for (int i = 0; i < draft.tiers; i++)
            {
                int ti = i == draft.tiers - 1 ? 2 : i;
                int si = NearestIdx(SizeSteps, draft.R(i));
                int ii = i;
                ArrowRow(ref cy, lx, labelW, valW, TierKo[ti] + " 크기", SizeKo[si], d => { draft.SetR(ii, SizeSteps[Mathf.Clamp(si + d, 0, SizeSteps.Length - 1)]); changed = true; });
            }
            int hi = Mathf.Max(0, Array.IndexOf(HeadIds, draft.head));
            ArrowRow(ref cy, lx, labelW, valW, "맨 위 모양", HeadKo[hi], d => { draft.head = HeadIds[Wrap(hi + d, HeadIds.Length)]; changed = true; });
            if (draft.tiers == 3 && draft.r2 > draft.r1 + 0.01f) GUI.Label(new Rect(lx, cy, SW - 60, 22), "<size=12>맨 위가 더 크면 오뚝이처럼 보여요!</size>", sSmall);
        }
        else if (snowTab == 1)
        {
            GUI.Label(new Rect(lx, cy - 4, SW - 60, 22), IsMobile ? "<size=13>누른 채 자리 잡고 떼면 붙이기, 길게 눌러 떼기, 두 손가락 확대</size>" : "<size=13>클릭해 붙이기, 오른쪽 클릭으로 떼기, 끌어서 돌려 보기</size>", sSmall);
            cy += 20;
            // 장식 모양 (그림): 옆으로 끌어 넘기기 · 누르면 고르기(다시 누르면 해제) · 끌어서 눈사람에 놓기
            float size = narrow ? 54 : 60;
            var col = AcPalette.Vivid[Wrap(snowColSel, AcPalette.Vivid.Length)];
            int ci = IconStrip(snowStrip, new Rect(lx - 3, cy, SW - 2 * lx, size + 6), SnowDecoIds.Length, size, i => SnowIcon(SnowDecoIds[i], col), snowDecoSel, snowPicked);
            if (ci >= 0) { if (ci == snowDecoSel && snowPicked) snowPicked = false; else { snowDecoSel = ci; snowPicked = true; } }
            cy += size + 12;
            var it = AcCatalog.Get(CurDeco);
            if (snowPicked) { GUI.Label(new Rect(lx, cy + 3, 130, 22), it != null ? it.desc : CurDeco, sLabelSel); if (GUI.Button(R(new Rect(lx + 140, cy, 82, 28)), "선택 해제", sBtn)) snowPicked = false; }
            else GUI.Label(new Rect(lx, cy + 3, SW - 60, 22), "<size=13>둘러보기: 끌어서 돌려 보기, 장식을 고르면 붙일 수 있어요</size>", sSmall);
            cy += 34;
            float x2 = lx;
            if (DecoTintable(CurDeco))
            {
                GUI.Label(new Rect(x2, cy + 3, 30, 22), "색", sSmall);
                if (GUI.Button(new Rect(x2 + 28, cy, 30, 28), "<", sBtn)) snowColSel--;
                GUI.DrawTexture(new Rect(x2 + 62, cy + 2, 46, 24), AcPalette.Tex(col));
                if (GUI.Button(new Rect(x2 + 112, cy, 30, 28), ">", sBtn)) snowColSel++;
                GUI.Label(new Rect(x2 + 146, cy + 3, 60, 22), AcPalette.VividNames[Wrap(snowColSel, AcPalette.Vivid.Length)], sSmall);
                x2 += 210;
            }
            if (narrow && x2 > lx) { cy += 34; x2 = lx; }
            GUI.Label(new Rect(x2, cy + 3, 40, 22), "크기", sSmall);
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(x2 + 40 + i * 58, cy, 54, 28);
                if (i == snowSizeSel) AcUiTheme.Ring(r, AcUiTheme.Accent, 3);
                if (GUI.Button(r, DecoSizeKo[i], sBtn)) snowSizeSel = i;
            }
            if (draft.d.Count > 0 && GUI.Button(new Rect(SW - 20 - 110, cy, 100, 28), "하나 되돌리기", sBtn)) { draft.d.RemoveAt(draft.d.Count - 1); RebuildDraft(); }
        }
        else
        {
            int hi = Mathf.Max(0, Array.IndexOf(SnowHatIds, draft.hat));
            ArrowRow(ref cy, lx, labelW, valW, "모자", SnowHatKo[hi], d => { draft.hat = SnowHatIds[Wrap(hi + d, SnowHatIds.Length)]; draft.hatC = ""; changed = true; });
            if (draft.hat != "")
            {
                Color hc = draft.hatC != "" ? Hex(draft.hatC) : DefaultHatColor(draft.hat);
                int ci = AcPalette.Nearest(AcPalette.Vivid, hc);
                ArrowRow(ref cy, lx, labelW, valW, "모자 색", draft.hatC != "" ? AcPalette.VividNames[ci] : "기본", d => { draft.hatC = HexOf(AcPalette.Vivid[Wrap(ci + d, AcPalette.Vivid.Length)]); changed = true; }, hc);
            }
            bool hasScarf = !string.IsNullOrEmpty(draft.scarfC);
            ArrowRow(ref cy, lx, labelW, valW, "목도리", hasScarf ? "두르기" : "없음", d => { draft.scarfC = hasScarf ? "" : "E53935"; changed = true; });
            if (hasScarf)
            {
                int ci = AcPalette.Nearest(AcPalette.Vivid, Hex(draft.scarfC));
                ArrowRow(ref cy, lx, labelW, valW, "목도리 색", AcPalette.VividNames[ci], d => { draft.scarfC = HexOf(AcPalette.Vivid[Wrap(ci + d, AcPalette.Vivid.Length)]); changed = true; }, Hex(draft.scarfC));
            }
        }
        if (changed) RebuildDraft();
    }

    Color DefaultHatColor(string hat)
    {
        var md = AcCatalog.Get(hat);
        return hat == "sm_bucket" ? new Color(0.55f, 0.62f, 0.72f) : hat == "hat_tophat" ? new Color(0.85f, 0.2f, 0.22f) : new Color(0.86f, 0.2f, 0.2f);
    }

    static int NearestIdx(float[] arr, float v) { int b = 0; for (int i = 1; i < arr.Length; i++) if (Mathf.Abs(arr[i] - v) < Mathf.Abs(arr[b] - v)) b = i; return b; }

    void ArrowRow(ref float cy, float x, float labelW, float valW, string label, string value, Action<int> step, Color? swatch = null)
    {
        GUI.Label(new Rect(x, cy + 3, labelW, 24), label, sSmall);
        if (GUI.Button(new Rect(x + labelW, cy, 32, 28), "<", sBtn)) step(-1);
        var vb = new Rect(x + labelW + 36, cy, valW, 28);
        GUI.DrawTexture(vb, AcPalette.Tex(new Color(0.97f, 0.94f, 0.88f)));
        float tx = vb.x + 8;
        if (swatch.HasValue) { GUI.DrawTexture(new Rect(vb.x + 5, vb.y + 4, 30, 20), AcPalette.Tex(swatch.Value)); tx += 32; }
        GUI.Label(new Rect(tx, vb.y + 4, vb.width - (tx - vb.x) - 4, 22), value, sSmall);
        if (GUI.Button(new Rect(x + labelW + 40 + valW, cy, 32, 28), ">", sBtn)) step(1);
        cy += 33;
    }
}

// ===== 마당의 눈사람 보기: 눈사람을 누르면 그 눈사람을 가까이서 돌려 보고, 없앨 수도 있음 =====
public partial class AcRoom
{
    string snowFocusId; float snowFocusYaw; bool snowDelAsk;

    bool PickSnowman(Vector2 screen, out string id)
    {
        id = null; float best = 999f;
        var ray = cam.ScreenPointToRay(screen);
        foreach (var kv in snowObjs)
        {
            if (!kv.Value.go) continue;
            var p = kv.Value.go.transform.position;
            for (float hgt = 0.3f; hgt <= 1.5f; hgt += 0.3f)
            {
                var c = p + Vector3.up * hgt;
                float along = Vector3.Dot(c - ray.origin, ray.direction);
                if (along < 0) continue;
                float d = Vector3.Cross(ray.direction, c - ray.origin).magnitude;
                if (d < 0.55f && along < best) { best = along; id = kv.Key; }
            }
        }
        return id != null;
    }

    void FocusSnowman(string id)
    {
        snowFocusId = id; snowDelAsk = false; walkTarget = null; followId = null; decoZoom = 1f;
        var p = snowObjs[id].go.transform.position; var d = p - cam.transform.position; d.y = 0;
        snowFocusYaw = d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 180f;
    }

    bool SnowFocusCamera(ref Vector3 focus, ref float dist, ref float pitch, ref float targetYaw)
    {
        if (snowFocusId == null) return false;
        if (!snowObjs.TryGetValue(snowFocusId, out var o) || !o.go) { snowFocusId = null; return false; }
        bool portrait = Screen.height > Screen.width;
        float vfov = cam.fieldOfView * Mathf.Deg2Rad;
        float need = Mathf.Max(2.6f, 1.9f / Mathf.Max(0.3f, cam.aspect));    // 눈사람 전체(약 1.8m)가 위아래·좌우로 들어오게
        focus = o.go.transform.position + Vector3.up * (portrait ? 0.55f : 0.8f);
        dist = need / (2f * Mathf.Tan(vfov / 2f)) * decoZoom; pitch = 12f; targetYaw = snowFocusYaw;
        return true;
    }

    void SnowFocusUpdate()
    {
        if (snowFocusId == null) return;
        if (phase != Phase.Play || building || playingGame || Key("esc")) { snowFocusId = null; return; }
        me.speed = 0;
        bool pin = PinchZoom();
        if (!pin && ptrPressed && ptrMoved && !ptrOnUI) snowFocusYaw += (ptrPos - lastPtr).x * 0.25f;
        lastPtr = ptrPos;
        float sc = OverUI(ptrPos) ? 0 : Scroll(); if (Mathf.Abs(sc) > 0.01f) decoZoom = Mathf.Clamp(decoZoom * (sc > 0 ? 0.9f : 1.1f), 0.5f, 2.5f);
        var a = MoveAxis(); snowFocusYaw -= a.x * 70f * Time.unscaledDeltaTime;
    }

    void SnowFocusGUI()
    {
        string who = "";
        var node = net != null ? net.Get("snow") : null;
        if (node != null && node.TryGetValue(snowFocusId, out var v) && v is Dictionary<string, object> d) who = AcJson.S(d, "name");
        float w = Mathf.Min(SW - 20, 460), h = 96;
        var r = new Rect(SW / 2 - w / 2, SH - h - 12, w, h);
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 16, r.y + 10, w - 32, 26), (who != "" ? who + " 님의 눈사람" : "눈사람"), sLabel);
        GUI.Label(new Rect(r.x + 16, r.y + 36, w - 32, 22), snowDelAsk ? "<size=13>정말 없앨까요? 모두의 마당에서 사라져요</size>" : (IsMobile ? "<size=13>끌어서 돌려 보기, 두 손가락 확대</size>" : "<size=13>끌어서 돌려 보기, 휠 확대</size>"), sSmall);
        float bw = 96, by = r.y + 58;
        if (!snowDelAsk)
        {
            if (GUI.Button(new Rect(r.xMax - 16 - bw * 2 - 8, by, bw, 30), "없애기", sBtn)) snowDelAsk = true;
            if (GUI.Button(new Rect(r.xMax - 16 - bw, by, bw, 30), "<b>닫기</b>", sBtn)) snowFocusId = null;
        }
        else
        {
            if (GUI.Button(new Rect(r.xMax - 16 - bw * 2 - 8, by, bw, 30), "<b>없애기</b>", sBtn))
            {
                net?.Delete("snow/" + snowFocusId);
                if (snowObjs.TryGetValue(snowFocusId, out var o) && o.go) Destroy(o.go);
                snowObjs.Remove(snowFocusId); snowFocusId = null; nextSnowPoll = 0; Toast("눈사람을 없앴어요");
            }
            if (GUI.Button(new Rect(r.xMax - 16 - bw, by, bw, 30), "그대로 두기", sBtn)) snowDelAsk = false;
        }
    }
}
