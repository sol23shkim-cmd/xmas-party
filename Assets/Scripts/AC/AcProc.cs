// AcProc.cs - 코드로 모양 만들기 도우미 (온실 식물·선물 상자·선물 내용물에서 함께 씀, 11-26)
// 정점 색 = 감마 RGB, A = 0 (색바꿈 없음). UV0.x = 밝기(AO, ACToon) 또는 그림 좌표(ACToonTex).
// 삼각형 방향은 정점 법선을 보고 저절로 맞춤 → 만들 때 감는 순서를 신경 쓰지 않아도 됨.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AcPB
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector3> n = new List<Vector3>();
    public readonly List<Color32> c = new List<Color32>();
    public readonly List<Vector2> uv = new List<Vector2>();
    public readonly List<int> t = new List<int>();
    public Matrix4x4 xf = Matrix4x4.identity;
    public bool inward;   // 안쪽에서 보는 모양 (배경 둥근 벽)

    static Color32 C32(Color col) => new Color32((byte)(Mathf.Clamp01(col.r) * 255), (byte)(Mathf.Clamp01(col.g) * 255), (byte)(Mathf.Clamp01(col.b) * 255), 0);

    public int V(Vector3 p, Vector3 nn, Color col, float ao = 1f) => V(p, nn, col, new Vector2(ao, 0));
    public int V(Vector3 p, Vector3 nn, Color col, Vector2 u)
    {
        v.Add(xf.MultiplyPoint3x4(p)); n.Add(xf.MultiplyVector(nn).normalized); c.Add(C32(col)); uv.Add(u);
        return v.Count - 1;
    }

    // 정점 법선 평균 쪽이 바깥이 되게
    public void Tri(int a, int b, int d)
    {
        var f = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
        if (f.sqrMagnitude < 1e-14f) return;
        var avg = n[a] + n[b] + n[d];
        if ((Vector3.Dot(f, avg) >= 0) != inward) { t.Add(a); t.Add(b); t.Add(d); }
        else { t.Add(a); t.Add(d); t.Add(b); }
    }
    public void Quad(int a, int b, int d, int e) { Tri(a, b, d); Tri(a, d, e); }

    public Mesh M(string name)
    {
        var m = new Mesh { name = name };
        if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    // ===== 기본 모양 =====
    // 타원체 (rot: 회전), col 함수가 있으면 위치별 색
    public void Ellipsoid(Vector3 center, Vector3 rad, Color col, int lat = 10, int lon = 14, Quaternion? rot = null, System.Func<Vector3, Color> colf = null, float aoBottom = 0.7f)
    {
        var q = rot ?? Quaternion.identity;
        int start = v.Count;
        for (int i = 0; i <= lat; i++)
        {
            float th = Mathf.PI * i / lat;
            for (int j = 0; j <= lon; j++)
            {
                float ph = Mathf.PI * 2 * j / lon;
                var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                var p = Vector3.Scale(u, rad);
                var nn = new Vector3(u.x / Mathf.Max(rad.x, 1e-4f), u.y / Mathf.Max(rad.y, 1e-4f), u.z / Mathf.Max(rad.z, 1e-4f)).normalized;
                var cc = colf != null ? colf(u) : col;
                V(center + q * p, q * nn, cc, Mathf.Lerp(aoBottom, 1f, (u.y + 1) * 0.5f));
            }
        }
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = start + i * (lon + 1) + j, b = a + lon + 1;
                Quad(a, b, b + 1, a + 1);
            }
    }

    public void Sphere(Vector3 center, float r, Color col, int lat = 8, int lon = 12) => Ellipsoid(center, Vector3.one * r, col, lat, lon);

    // 길을 따라가는 관 (pts·rads 같은 개수). cols가 없으면 col 하나. capEnds면 양끝 막음
    public void Tube(IList<Vector3> pts, IList<float> rads, Color col, int seg = 8, IList<Color> cols = null, bool capStart = true, bool capEnd = true)
    {
        int nP = pts.Count; if (nP < 2) return;
        var up = Vector3.up;
        var dir0 = (pts[1] - pts[0]).normalized;
        if (Mathf.Abs(Vector3.Dot(dir0, up)) > 0.95f) up = Vector3.right;
        var side = Vector3.Cross(dir0, up).normalized; var bin = Vector3.Cross(side, dir0).normalized;
        int start = v.Count;
        for (int i = 0; i < nP; i++)
        {
            var dir = i == 0 ? pts[1] - pts[0] : i == nP - 1 ? pts[nP - 1] - pts[nP - 2] : pts[i + 1] - pts[i - 1];
            dir.Normalize();
            // 앞 단면 축을 이번 방향에 맞게 비틀림 없이 옮김
            side = Vector3.Cross(dir, bin).normalized; if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(dir, Vector3.forward).normalized;
            bin = Vector3.Cross(side, dir).normalized;
            var cc = cols != null ? cols[i] : col;
            for (int j = 0; j <= seg; j++)
            {
                float a = Mathf.PI * 2 * j / seg;
                var nn = side * Mathf.Cos(a) + bin * Mathf.Sin(a);
                V(pts[i] + nn * rads[i], nn, cc, 1f);
            }
        }
        for (int i = 0; i < nP - 1; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = start + i * (seg + 1) + j, b = a + seg + 1;
                Quad(a, b, b + 1, a + 1);
            }
        if (capStart && rads[0] > 1e-4f) Cap(pts[0], -(pts[1] - pts[0]).normalized, start, seg, cols != null ? cols[0] : col);
        if (capEnd && rads[nP - 1] > 1e-4f) Cap(pts[nP - 1], (pts[nP - 1] - pts[nP - 2]).normalized, start + (nP - 1) * (seg + 1), seg, cols != null ? cols[nP - 1] : col);
    }
    void Cap(Vector3 center, Vector3 nrm, int ring, int seg, Color col)
    {
        // 링 정점 위치를 다시 써서 평평한 뚜껑 (xf는 이미 적용된 값이므로 잠시 끔)
        var save = xf; xf = Matrix4x4.identity;
        int cI = V(save.MultiplyPoint3x4(center), save.MultiplyVector(nrm), col, 1f);
        var idx = new int[seg + 1];
        for (int j = 0; j <= seg; j++) idx[j] = V(v[ring + j], save.MultiplyVector(nrm), col, 1f);
        for (int j = 0; j < seg; j++) Tri(cI, idx[j], idx[j + 1]);
        xf = save;
    }

    public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, Color col, int seg = 10, bool caps = true)
        => Tube(new[] { a, b }, new[] { ra, rb }, col, seg, null, caps, caps);

    // 평평한 원판 (양면)
    public void Disk(Vector3 center, Vector3 nrm, float r, Color col, int seg = 16, bool twoSided = true)
    {
        nrm.Normalize();
        var s = Vector3.Cross(nrm, Mathf.Abs(nrm.y) > 0.9f ? Vector3.right : Vector3.up).normalized; var b = Vector3.Cross(nrm, s);
        foreach (float sg in twoSided ? new[] { 1f, -1f } : new[] { 1f })
        {
            int cI = V(center + nrm * 0.0005f * sg, nrm * sg, col);
            int first = v.Count;
            for (int j = 0; j <= seg; j++) { float a = Mathf.PI * 2 * j / seg; V(center + nrm * 0.0005f * sg + (s * Mathf.Cos(a) + b * Mathf.Sin(a)) * r, nrm * sg, col); }
            for (int j = 0; j < seg; j++) Tri(cI, first + j, first + j + 1);
        }
    }

    // 잎·꽃잎: base에서 dir 방향으로 len, 넓이 wid. side = 잎의 가로 방향, curl = 앞으로 휘는 정도(+면 위로 오목)
    // shape: 0 둥근 잎, 1 뾰족 잎(포인세티아), 2 긴 띠잎(아마릴리스), 3 하트 꽃잎, 4 톱니 잎(호랑가시)
    public void Leaf(Vector3 basePos, Vector3 dir, Vector3 side, float len, float wid, Color col, Color tip, float curl = 0.15f, int shape = 0, int segs = 6, float droop = 0f)
    {
        dir.Normalize(); side = (side - dir * Vector3.Dot(side, dir)).normalized;
        var up = Vector3.Cross(dir, side).normalized;   // 잎 앞면 방향
        float W(float s)
        {
            switch (shape)
            {
                case 1: return Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.8f)), 0.9f) * (1 - 0.25f * s);
                case 2: return s < 0.85f ? 0.55f + 0.45f * Mathf.Sin(Mathf.PI * s * 0.6f) : (1 - s) / 0.15f * 0.9f;
                case 3: return Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.6f)) * (s > 0.85f ? 0.8f + 0.2f * Mathf.Cos((s - 0.85f) * 20f) : 1f);
                case 4: return Mathf.Sin(Mathf.PI * s) * (0.82f + 0.18f * Mathf.Abs(Mathf.Sin(s * Mathf.PI * 3.5f)));
                default: return Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.75f));
            }
        }
        foreach (float sg in new[] { 1f, -1f })
        {
            int start = v.Count;
            for (int i = 0; i <= segs; i++)
            {
                float s = (float)i / segs;
                // 휘어짐: 앞으로 굽고(droop: 아래로 처짐)
                var center = basePos + dir * (len * s) + up * (curl * len * s * s) - Vector3.up * (droop * len * s * s);
                float w = W(s) * wid * 0.5f;
                var cc = Color.Lerp(col, tip, s * s);
                var nUp = (up - dir * (2 * curl * s)).normalized;
                V(center - side * w + nUp * (w * curl * 0.6f), nUp * sg, cc * (sg > 0 ? 1f : 0.85f), 0.85f + 0.15f * s);
                V(center + nUp * 0.002f * sg, nUp * sg, cc * (sg > 0 ? 1.05f : 0.9f), 1f);
                V(center + side * w + nUp * (w * curl * 0.6f), nUp * sg, cc * (sg > 0 ? 1f : 0.85f), 0.85f + 0.15f * s);
            }
            for (int i = 0; i < segs; i++)
            {
                int a = start + i * 3;
                Quad(a, a + 3, a + 4, a + 1);
                Quad(a + 1, a + 4, a + 5, a + 2);
            }
        }
    }

    public void Box(Vector3 center, Vector3 size, Color col, Quaternion? rot = null, float uvScale = 0f)
    {
        var q = rot ?? Quaternion.identity; var h = size * 0.5f;
        void F(Vector3 nrm, Vector3 u, Vector3 w, float su, float sw)
        {
            var c0 = Vector3.Scale(nrm, h); var hu = Vector3.Scale(u, h); var hw = Vector3.Scale(w, h);
            Vector2 U(float a, float b) => uvScale > 0 ? new Vector2(a * su * uvScale, b * sw * uvScale) : new Vector2(1, 0);
            int a0 = V(center + q * (c0 - hu - hw), q * nrm, col, U(0, 0)), a1 = V(center + q * (c0 + hu - hw), q * nrm, col, U(1, 0)),
                a2 = V(center + q * (c0 + hu + hw), q * nrm, col, U(1, 1)), a3 = V(center + q * (c0 - hu + hw), q * nrm, col, U(0, 1));
            Quad(a0, a1, a2, a3);
        }
        F(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
        F(Vector3.down, Vector3.right, Vector3.forward, size.x, size.z);
        F(Vector3.forward, Vector3.right, Vector3.up, size.x, size.y);
        F(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
        F(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
        F(Vector3.left, Vector3.forward, Vector3.up, size.z, size.y);
    }

    public void Torus(Vector3 center, Vector3 axis, float R, float r, Color col, int s1 = 24, int s2 = 8)
    {
        axis.Normalize();
        var a1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized; var a2 = Vector3.Cross(axis, a1);
        int start = v.Count;
        for (int i = 0; i <= s1; i++)
        {
            float u = Mathf.PI * 2 * i / s1; var cdir = a1 * Mathf.Cos(u) + a2 * Mathf.Sin(u);
            for (int j = 0; j <= s2; j++)
            {
                float w = Mathf.PI * 2 * j / s2; var nn = cdir * Mathf.Cos(w) + axis * Mathf.Sin(w);
                V(center + cdir * R + nn * r, nn, col);
            }
        }
        for (int i = 0; i < s1; i++) for (int j = 0; j < s2; j++) { int a = start + i * (s2 + 1) + j, b = a + s2 + 1; Quad(a, b, b + 1, a + 1); }
    }

    // 별 모양 납작 기둥 (꼭짓점 n개)
    public void Star(Vector3 center, Vector3 nrm, Vector3 upDir, float rOut, float rIn, float thick, Color col, int points = 5)
    {
        nrm.Normalize(); upDir = (upDir - nrm * Vector3.Dot(upDir, nrm)).normalized; var sd = Vector3.Cross(nrm, upDir);
        var ring = new List<Vector3>();
        for (int i = 0; i < points * 2; i++)
        {
            float a = Mathf.PI * i / points; float r = i % 2 == 0 ? rOut : rIn;
            ring.Add(upDir * Mathf.Cos(a) * r + sd * Mathf.Sin(a) * r);
        }
        foreach (float sg in new[] { 1f, -1f })
        {
            int cI = V(center + nrm * thick * sg, nrm * sg, col * 1.05f);
            int first = v.Count;
            for (int i = 0; i <= ring.Count; i++) { var p = ring[i % ring.Count]; V(center + p, (nrm * sg + p.normalized * 0.6f).normalized, col); }
            for (int i = 0; i < ring.Count; i++) Tri(cI, first + i, first + i + 1);
        }
    }

    // 하트 모양 볼록 덩어리 (풍선·하트)
    public void Heart(Vector3 center, float size, Color col, float depth = 0.45f, int seg = 28)
    {
        var outline = new List<Vector3>();
        for (int i = 0; i < seg; i++)
        {
            float tt = Mathf.PI * 2 * i / seg;
            float x = 16 * Mathf.Pow(Mathf.Sin(tt), 3), y = 13 * Mathf.Cos(tt) - 5 * Mathf.Cos(2 * tt) - 2 * Mathf.Cos(3 * tt) - Mathf.Cos(4 * tt);
            outline.Add(new Vector3(x, y + 2, 0) * (size / 34f));
        }
        const int rings = 4;
        foreach (float sg in new[] { 1f, -1f })
        {
            int cI = V(center + Vector3.forward * sg * size * depth * 0.5f, Vector3.forward * sg, col * 1.08f);
            int prev = -1;
            for (int k = 1; k <= rings; k++)
            {
                float f = (float)k / rings; float z = Mathf.Cos(f * Mathf.PI * 0.5f) * size * depth * 0.5f;
                int first = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    var p = outline[i % seg] * Mathf.Sin(f * Mathf.PI * 0.5f);
                    var nn = (p.normalized * f + Vector3.forward * sg * (1 - f) * 1.4f).normalized;
                    V(center + p + Vector3.forward * sg * z, nn, col, 0.9f + 0.1f * (1 - f));
                }
                if (prev < 0) for (int i = 0; i < seg; i++) Tri(cI, first + i, first + i + 1);
                else for (int i = 0; i < seg; i++) Quad(prev + i, first + i, first + i + 1, prev + i + 1);
                prev = first;
            }
        }
    }
}

public partial class AcRoom
{
    // 코드로 만든 메시를 물체로 (ACToon 공유 재질)
    static GameObject ProcObj(string name, Mesh m, Transform parent, float emit = 0f, bool shadows = true)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = AcmLoader.Mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (emit > 0) { var b = new MaterialPropertyBlock(); b.SetFloat("_Emit", emit); r.SetPropertyBlock(b); }
        return go;
    }

    // 모델(카탈로그)·물체를 높이 h에 맞춰 바닥 가운데가 원점이 되게 감싸기
    static GameObject FitHolder(GameObject g, float h, string name = "fit")
    {
        var holder = new GameObject(name);
        var rs = g.GetComponentsInChildren<Renderer>();
        g.transform.SetParent(holder.transform, false);
        if (rs.Length == 0) return holder;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float k = h / Mathf.Max(0.01f, b.size.y);
        g.transform.localScale *= k;
        g.transform.localPosition = new Vector3(-b.center.x * k, -b.min.y * k, -b.center.z * k);
        return holder;
    }
}
