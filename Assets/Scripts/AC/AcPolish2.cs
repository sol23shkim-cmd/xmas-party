// AcPolish2.cs - 11-28 그래픽 다듬기: 매끈한 나무 마루, 방마다 벽지 무늬, 깔끔한 러그·식탁보, 울퉁불퉁 눈밭,
// 카메라와 내 캐릭터 사이를 가리는 소품 잠깐 숨기기
// (예전 모델들은 꼭짓점 색으로 무늬를 칠해서 가까이서 톱니처럼 자글거렸음 → 무늬를 면으로 따로 그림)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class AcRoom
{
    static readonly Color FloorWood = new Color(0.78f, 0.56f, 0.36f);

    // ===== 나무 마루: 판자 줄 + 판자마다 살짝 다른 색 + 가는 이음선 (면으로 그려서 깔끔) =====
    void PlankFloor(Rect r, Transform parent, string name)
    {
        var b = new AcPB { detail = 1f };
        var rng = new System.Random(name.GetHashCode());
        float pw = 0.32f, seam = 0.014f, y = 0.0f;
        Color seamC = new Color(0.55f, 0.37f, 0.23f);
        int rows = Mathf.CeilToInt(r.height / pw);
        for (int i = 0; i < rows; i++)
        {
            float z0 = r.yMin + i * pw, z1 = Mathf.Min(r.yMax, z0 + pw);
            float x = r.xMin - (float)rng.NextDouble() * 2.2f;
            while (x < r.xMax)
            {
                float len = 1.4f + (float)rng.NextDouble() * 1.8f;
                float xa = Mathf.Max(r.xMin, x), xb = Mathf.Min(r.xMax, x + len);
                if (xb > xa + 0.01f)
                {
                    float k = 0.93f + (float)rng.NextDouble() * 0.12f;
                    var c = new Color(FloorWood.r * k, FloorWood.g * k, FloorWood.b * k);
                    var c2 = new Color(c.r * 0.97f, c.g * 0.96f, c.b * 0.95f);   // 판자 끝으로 갈수록 아주 살짝 어둡게 (결 느낌)
                    int v0 = b.V(new Vector3(xa, y, z0 + seam * 0.5f), Vector3.up, c, 1f), v1 = b.V(new Vector3(xa, y, z1 - seam * 0.5f), Vector3.up, c, 1f);
                    int v2 = b.V(new Vector3(xb, y, z1 - seam * 0.5f), Vector3.up, c2, 1f), v3 = b.V(new Vector3(xb, y, z0 + seam * 0.5f), Vector3.up, c2, 1f);
                    b.Quad(v0, v1, v2, v3);
                    // 판자 끝 이음선
                    if (xb < r.xMax - 0.01f)
                    {
                        int s0 = b.V(new Vector3(xb, y, z0), Vector3.up, seamC, 1f), s1 = b.V(new Vector3(xb, y, z1), Vector3.up, seamC, 1f);
                        int s2 = b.V(new Vector3(xb + seam, y, z1), Vector3.up, seamC, 1f), s3 = b.V(new Vector3(xb + seam, y, z0), Vector3.up, seamC, 1f);
                        b.Quad(s0, s1, s2, s3);
                    }
                }
                x += len + seam;
            }
            // 줄 사이 이음선
            int q0 = b.V(new Vector3(r.xMin, y, z1 - seam * 0.5f), Vector3.up, seamC, 1f), q1 = b.V(new Vector3(r.xMin, y, z1 + seam * 0.5f), Vector3.up, seamC, 1f);
            int q2 = b.V(new Vector3(r.xMax, y, z1 + seam * 0.5f), Vector3.up, seamC, 1f), q3 = b.V(new Vector3(r.xMax, y, z1 - seam * 0.5f), Vector3.up, seamC, 1f);
            b.Quad(q0, q1, q2, q3);
        }
        // 바닥 아래 판 (벽을 낮췄을 때 바닥 두께가 보이게)
        b.Box(new Vector3(r.center.x, -0.06f, r.center.y), new Vector3(r.width, 0.12f, r.height), new Color(0.6f, 0.42f, 0.27f));
        var go = ProcObj("floor_" + name, b.M("floor_" + name), parent);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // ===== 벽지: 징두리(아래 판자) 위로 방마다 다른 잔무늬. 벽 조각의 자식이라 벽을 낮추면 같이 낮아짐 =====
    static void Wallpaper(GameObject wall, string room, Color wallC)
    {
        if (!wall) return;
        var b = new AcPB { detail = 1f };
        Color pc = new Color(wallC.r * 0.9f, wallC.g * 0.89f, wallC.b * 0.88f);
        Color pc2 = Color.Lerp(wallC, Color.white, 0.45f);
        const float z = 0.004f, y0 = 1.08f, y1 = 3.38f;
        int kind = room switch { "트리 거실" => 0, "침실" => 1, "선물 포장실" => 2, "음악실" => 3, "놀이방" => 4, "쿠키 공방" => 5, "겨울 온실" => 6, "부엌" => 7, "서재" => 8, _ => 9 };
        var n = Vector3.forward;
        void RectQ(float xa, float ya, float xb, float yb, Color c) { int a0 = b.V(new Vector3(xa, ya, z), n, c, 1f), a1 = b.V(new Vector3(xa, yb, z), n, c, 1f), a2 = b.V(new Vector3(xb, yb, z), n, c, 1f), a3 = b.V(new Vector3(xb, ya, z), n, c, 1f); b.Quad(a0, a3, a2, a1); }
        void Dia(float cx, float cy, float s, Color c) { int a0 = b.V(new Vector3(cx, cy - s, z), n, c, 1f), a1 = b.V(new Vector3(cx - s * 0.7f, cy, z), n, c, 1f), a2 = b.V(new Vector3(cx, cy + s, z), n, c, 1f), a3 = b.V(new Vector3(cx + s * 0.7f, cy, z), n, c, 1f); b.Quad(a0, a3, a2, a1); }
        void Flake(float cx, float cy, float s, Color c) { for (int k = 0; k < 3; k++) { float a = k * Mathf.PI / 3f; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * s; var w = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0) * s * 0.12f; var cc = new Vector3(cx, cy, z); int a0 = b.V(cc - d - w, n, c, 1f), a1 = b.V(cc - d + w, n, c, 1f), a2 = b.V(cc + d + w, n, c, 1f), a3 = b.V(cc + d - w, n, c, 1f); b.Quad(a0, a3, a2, a1); } }
        switch (kind)
        {
            case 0: case 5:   // 거실·쿠키 공방: 굵은 세로 줄무늬
                for (float x = -0.9f; x < 1f; x += 0.4f) RectQ(x, y0, x + 0.16f, y1, pc);
                break;
            case 1: case 6:   // 침실·온실: 물방울 무늬
                for (int iy = 0; iy < 6; iy++) for (float x = -0.8f + (iy % 2) * 0.2f; x < 0.95f; x += 0.4f) b.Disk(new Vector3(x, y0 + 0.2f + iy * 0.36f, z), n, 0.05f, pc, 10, false);
                break;
            case 2: case 4:   // 선물 포장실·놀이방: 마름모
                for (int iy = 0; iy < 5; iy++) for (float x = -0.75f + (iy % 2) * 0.25f; x < 0.95f; x += 0.5f) Dia(x, y0 + 0.25f + iy * 0.45f, 0.11f, pc);
                break;
            case 3: case 8:   // 음악실·서재: 가는 세로줄 두 개씩 (클래식)
                for (float x = -0.95f; x < 1f; x += 0.5f) { RectQ(x, y0, x + 0.03f, y1, pc); RectQ(x + 0.08f, y0, x + 0.11f, y1, pc); }
                break;
            case 7:   // 부엌: 작은 바둑판 줄
                for (float yy = y0 + 0.3f; yy < y1; yy += 0.45f) RectQ(-1f, yy, 1f, yy + 0.025f, pc);
                for (float x = -0.9f; x < 1f; x += 0.45f) RectQ(x, y0, x + 0.025f, y1, pc);
                break;
            default:          // 바깥 벽 등: 눈꽃
                for (int iy = 0; iy < 4; iy++) for (float x = -0.7f + (iy % 2) * 0.3f; x < 0.95f; x += 0.6f) Flake(x, y0 + 0.3f + iy * 0.55f, 0.1f, pc2);
                break;
        }
        var go = ProcObj("wallpaper", b.M("wallpaper_" + kind), wall.transform, 0f, false);
        go.GetComponent<MeshRenderer>().receiveShadows = true;
    }

    // ===== 둥근 러그: 바탕 + 크림색 고리 두 줄 + 가운데 (면으로 깔끔하게) =====
    GameObject RugRound(Vector3 pos, float scale, string hex, Transform parent)
    {
        var b = new AcPB { detail = 1f };
        Color baseC = C(hex), ring = new Color(0.98f, 0.94f, 0.86f), mid = Color.Lerp(baseC, ring, 0.25f);
        float R = 1.55f * scale; int seg = 64;
        var y = new float[] { 0.012f, 0.0125f, 0.013f };
        void Annulus(float r0, float r1, Color c, float yy)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 P(float r, float a) => pos + new Vector3(Mathf.Cos(a) * r, yy, Mathf.Sin(a) * r);
                int v0 = b.V(P(r0, a0), Vector3.up, c, 1f), v1 = b.V(P(r1, a0), Vector3.up, c, 1f), v2 = b.V(P(r1, a1), Vector3.up, c, 1f), v3 = b.V(P(r0, a1), Vector3.up, c, 1f);
                b.Quad(v0, v3, v2, v1);
            }
        }
        b.Cylinder(pos, pos + Vector3.up * 0.012f, R, R, baseC, seg);
        Annulus(1.33f * scale, 1.45f * scale, ring, 0.0135f);
        Annulus(0.84f * scale, 0.94f * scale, ring, 0.0135f);
        b.Disk(pos + Vector3.up * 0.0135f, Vector3.up, 0.48f * scale, mid, seg, false);
        b.Torus(pos + Vector3.up * 0.012f, Vector3.up, R, 0.018f * scale, Color.Lerp(baseC, Color.black, 0.15f), seg, 5);   // 테두리
        var go = ProcObj("rug_round_p", b.M("rug_round_p"), parent, 0f, false);
        return go;
    }

    // ===== 네모 러그: 바탕 + 넓은 크림 테두리 + 연한 체크 줄 (대비 낮게) =====
    GameObject RugRect(Vector3 pos, float rotY, float scale, string hex, Transform parent)
    {
        var b = new AcPB { detail = 1f };
        Color baseC = C(hex), border = new Color(0.98f, 0.94f, 0.86f), line = Color.Lerp(baseC, border, 0.3f);
        float hx = 1.5f * scale, hz = 1.0f * scale, yy = 0.0135f;
        var q = Quaternion.Euler(0, rotY, 0);
        void Rc(float xa, float za, float xb, float zb, Color c) { int v0 = b.V(pos + q * new Vector3(xa, yy, za), Vector3.up, c, 1f), v1 = b.V(pos + q * new Vector3(xa, yy, zb), Vector3.up, c, 1f), v2 = b.V(pos + q * new Vector3(xb, yy, zb), Vector3.up, c, 1f), v3 = b.V(pos + q * new Vector3(xb, yy, za), Vector3.up, c, 1f); b.Quad(v0, v1, v2, v3); }
        b.Box(pos + Vector3.up * 0.006f, new Vector3(hx * 2, 0.012f, hz * 2), border, q);
        float bw = 0.14f * scale;
        Rc(-hx + bw, -hz + bw, hx - bw, hz - bw, baseC);
        yy = 0.014f;
        for (float x = -hx + bw + 0.5f * scale; x < hx - bw - 0.05f; x += 0.5f * scale) Rc(x - 0.03f * scale, -hz + bw, x + 0.03f * scale, hz - bw, line);
        for (float z = -hz + bw + 0.5f * scale; z < hz - bw - 0.05f; z += 0.5f * scale) Rc(-hx + bw, z - 0.03f * scale, hx - bw, z + 0.03f * scale, line);
        return ProcObj("rug_rect_p", b.M("rug_rect_p"), parent, 0f, false);
    }

    // ===== 식탁보: 모델의 자글거리는 체크 위에 큰 체크(대비 낮게)를 덮음. 식탁의 자식(모델 단위) =====
    static void ClothOverlay(GameObject table, Color fabric)
    {
        if (!table) return;
        var b = new AcPB { detail = 1f };
        Color a = fabric, c2 = Color.Lerp(fabric, new Color(0.98f, 0.96f, 0.92f), 0.55f);
        float hx = 0.962f, hz = 0.612f, top = 0.812f, bot = 0.645f, sq = 0.24f;
        // 윗면
        int nx = Mathf.CeilToInt(hx * 2 / sq), nz = Mathf.CeilToInt(hz * 2 / sq);
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
        {
            float xa = -hx + i * sq, xb = Mathf.Min(hx, xa + sq), za = -hz + j * sq, zb = Mathf.Min(hz, za + sq);
            var c = (i + j) % 2 == 0 ? a : c2;
            int v0 = b.V(new Vector3(xa, top, za), Vector3.up, c, 1f), v1 = b.V(new Vector3(xa, top, zb), Vector3.up, c, 1f), v2 = b.V(new Vector3(xb, top, zb), Vector3.up, c, 1f), v3 = b.V(new Vector3(xb, top, za), Vector3.up, c, 1f);
            b.Quad(v0, v1, v2, v3);
        }
        // 늘어진 옆 자락 (네 면)
        void Side(Vector3 o, Vector3 along, float len, Vector3 nrm)
        {
            int n = Mathf.CeilToInt(len / sq);
            for (int i = 0; i < n; i++)
            {
                float s0 = i * sq, s1 = Mathf.Min(len, s0 + sq);
                var c = i % 2 == 0 ? a : c2;
                var p0 = o + along * s0; var p1 = o + along * s1;
                int v0 = b.V(new Vector3(p0.x, bot, p0.z), nrm, c, 0.9f), v1 = b.V(new Vector3(p0.x, top, p0.z), nrm, c, 1f), v2 = b.V(new Vector3(p1.x, top, p1.z), nrm, c, 1f), v3 = b.V(new Vector3(p1.x, bot, p1.z), nrm, c, 0.9f);
                b.Quad(v0, v1, v2, v3); b.Quad(v0, v3, v2, v1);
            }
        }
        Side(new Vector3(-hx, 0, hz), Vector3.right, hx * 2, Vector3.forward);
        Side(new Vector3(-hx, 0, -hz), Vector3.right, hx * 2, Vector3.back);
        Side(new Vector3(hx, 0, -hz), Vector3.forward, hz * 2, Vector3.right);
        Side(new Vector3(-hx, 0, -hz), Vector3.forward, hz * 2, Vector3.left);
        ProcObj("cloth_overlay", b.M("cloth_overlay"), table.transform, 0f, false);
    }

    // ===== 눈밭: 한 장으로 이어진 부드러운 눈 언덕 (이음선 없음). 오목한 곳은 연한 하늘색, 볼록한 곳은 흰색 =====
    void SnowTerrain(Transform parent)
    {
        float x0 = WorldX0 - 30f, x1 = WorldX1 + 30f, z0 = WorldZ0 - 30f, z1 = WorldZ1 + 30f, step = 1.5f;
        int nx = Mathf.CeilToInt((x1 - x0) / step) + 1, nz = Mathf.CeilToInt((z1 - z0) / step) + 1;
        var verts = new Vector3[nx * nz]; var cols = new Color[nx * nz]; var uv = new Vector2[nx * nz];
        Color hi = new Color(0.93f, 0.955f, 1f), lo = new Color(0.74f, 0.81f, 0.95f);   // 살짝 푸른 기를 넣어 따뜻한 햇빛에도 하얗게 날아가지 않게
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = x0 + i * step, z = z0 + j * step;
                float n1 = Mathf.PerlinNoise(x * 0.07f + 13.1f, z * 0.07f + 7.7f), n2 = Mathf.PerlinNoise(x * 0.33f + 3.3f, z * 0.33f + 1.9f);
                float n = n1 * 0.6f + n2 * 0.4f;
                float d = DistToHouse(new Vector3(x, 0, z));
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.6f) / 5f));   // 집 가까이는 평평하고 낮게 (바닥을 뚫지 않게)
                float far = Mathf.Clamp01((Mathf.Max(x - WorldX1, WorldX0 - x, z - WorldZ1, WorldZ0 - z) + 2f) / 20f);   // 마당 밖은 더 크게 출렁
                float h = -0.05f + (n - 0.5f) * 0.09f * fade + far * n1 * 0.9f;
                verts[j * nx + i] = new Vector3(x, h, z);
                float shade = Mathf.Clamp01(0.6f + (n - 0.5f) * 2.4f);
                cols[j * nx + i] = Color.Lerp(lo, hi, shade);
                uv[j * nx + i] = new Vector2(1f, 0f);
            }
        var tris = new int[(nx - 1) * (nz - 1) * 6]; int t = 0;
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b2 = a + nx;
                tris[t++] = a; tris[t++] = b2; tris[t++] = b2 + 1;
                tris[t++] = a; tris[t++] = b2 + 1; tris[t++] = a + 1;
            }
        var m = new Mesh { name = "snow_terrain", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.vertices = verts; m.colors = cols; m.uv = uv; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
        var go = new GameObject("SnowBase"); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = AcmLoader.Mat; mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ===== 카메라와 내 캐릭터 사이를 가리는 소품(스탠드·의자·책장 등)은 잠깐 숨김 =====
    readonly List<(GameObject go, Bounds b, Renderer[] rs)> fgProps = new List<(GameObject, Bounds, Renderer[])>();
    readonly HashSet<GameObject> fgHidden = new HashSet<GameObject>();
    float fgNext;
    void CollectFgProps()
    {
        fgProps.Clear();
        foreach (var go in putRoots)
        {
            if (!go) continue;
            string n = go.name;
            if (n.StartsWith("wall") || n == "floor_planks" || n == "floor_tiles" || n.StartsWith("floor_") && !n.Contains("lamp") || n.Contains("rug") || n.Contains("string_lights") || n.Contains("tree_") || n.Contains("ground")) continue;
            var rs = go.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.y < 0.5f || b.size.x > 7f || b.size.z > 7f) continue;
            fgProps.Add((go, b, rs));
        }
    }
    void UpdateFgProps()
    {
        bool active = phase == Phase.Play && me != null && !treeOverview && !decorating && !kitchenOpen && !GiftBusy && !playingGame && !GreenBusy && cam != null;
        if (!active)
        {
            if (fgHidden.Count > 0) { foreach (var (go, _, rs) in fgProps) if (fgHidden.Contains(go)) foreach (var r in rs) if (r) r.enabled = true; fgHidden.Clear(); }
            return;
        }
        if (Time.unscaledTime < fgNext) return;
        fgNext = Time.unscaledTime + 0.12f;
        if (fgProps.Count == 0) CollectFgProps();
        var cp = cam.transform.position; var pp = me.transform.position;
        var right = cam.transform.right; right.y = 0; right.Normalize();
        float pd = (pp - cp).magnitude;
        var targets = new[] { pp + Vector3.up * 0.5f, pp + Vector3.up * 1.1f, pp + Vector3.up * 0.6f + right * 0.6f, pp + Vector3.up * 0.6f - right * 0.6f };
        var psp = cam.WorldToScreenPoint(pp);
        foreach (var (go, b0, rs) in fgProps)
        {
            if (!go) continue;
            var b = b0; b.Expand(0.15f);
            float bd = (b.center - cp).magnitude;
            bool hide = false;
            if (bd < pd - 0.8f)
            {
                foreach (var t in targets)
                {
                    var d = t - cp; float len = d.magnitude;
                    if (b.IntersectRay(new Ray(cp, d / len), out float hit) && hit < len - 0.7f) { hide = true; break; }
                }
                // 화면 아래쪽 앞에 크게 걸리는 키 큰 소품(스탠드 등)
                if (!hide && b0.size.y > 1.3f && bd < pd - 2.5f)
                {
                    var sp = cam.WorldToScreenPoint(new Vector3(b0.center.x, b0.max.y, b0.center.z));
                    if (sp.z > 0 && sp.y < psp.y && sp.x > -80 && sp.x < Screen.width + 80) hide = true;
                }
            }
            bool was = fgHidden.Contains(go);
            if (hide != was)
            {
                foreach (var r in rs) if (r) r.enabled = !hide;
                if (hide) fgHidden.Add(go); else fgHidden.Remove(go);
            }
        }
    }
}
