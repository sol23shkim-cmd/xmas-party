// AcGarland.cs — 트리에 빙 두르는 장식 줄: 털 가랜드(반짝이는 털실)와 구슬 줄.
// 트리 표면을 따라 한 바퀴 돌며, 가지 끝마다 살짝 늘어지게 걸림.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class AcRoom
{
    public const string TinselId = "garland_tinsel", BeadsId = "garland_beads";
    static bool IsGarland(string t) => t == TinselId || t == BeadsId;
    static string GarlandName(string t) => t == TinselId ? "털 가랜드" : "구슬 줄";

    List<Vector3> GarlandPath(float y)
    {
        var pts = new List<Vector3>();
        const int n = 120, swag = 10;                  // 12번 늘어짐
        float sagMax = Mathf.Lerp(0.18f, 0.42f, Mathf.InverseLerp(6f, 1f, y));   // 아래쪽일수록 크게
        for (int i = 0; i <= n; i++)
        {
            float a = i * Mathf.PI * 2 / n;
            float frac = (i % swag) / (float)swag;
            float yy = y - sagMax * Mathf.Sin(Mathf.PI * frac);
            var radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            float r = SurfaceR(radial, yy);
            if (r < 0.1f) { if (pts.Count > 0 && pts[pts.Count - 1] != Vector3.zero) pts.Add(Vector3.zero); continue; }   // 끊김 표시
            pts.Add(TreePos + radial * (r + 0.06f) + Vector3.up * yy);
        }
        return pts;
    }

    // 경로(월드 좌표, Vector3.zero = 끊김) → 메시
    static Mesh GarlandMesh(List<Vector3> path, string kind, Color col, float scale = 1f)
    {
        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var c = new List<Color32>(); var uv = new List<Vector2>(); var tri = new List<int>();
        var rnd = new System.Random(7);
        float R() => (float)rnd.NextDouble();
        Color Var(Color b, float k) { float f = 1 + (R() - 0.5f) * k; return new Color(Mathf.Clamp01(b.r * f), Mathf.Clamp01(b.g * f), Mathf.Clamp01(b.b * f)); }
        var gold = new Color(1f, 0.82f, 0.35f);
        float step = kind == TinselId ? 0.022f * scale : 0.1f * scale;
        float carry = 0; int bead = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var p0 = path[i]; var p1 = path[i + 1];
            if (p0 == Vector3.zero || p1 == Vector3.zero) continue;
            float len = Vector3.Distance(p0, p1);
            var dir = (p1 - p0).normalized;
            float d = carry;
            for (; d < len; d += step)
            {
                var p = Vector3.Lerp(p0, p1, d / len);
                if (kind == TinselId)
                {
                    // 털: 사방으로 뻗은 가는 가시 (앞뒤 모두 보이게 두 번)
                    for (int k = 0; k < 8; k++)
                    {
                        var r = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f);
                        r -= dir * Vector3.Dot(r, dir); r = r.sqrMagnitude < 1e-4f ? Vector3.up : r.normalized;
                        float L = (0.05f + R() * 0.045f) * scale, w = 0.008f * scale;
                        var side = Vector3.Cross(r, dir).normalized * w;
                        var tip = p + r * L + dir * (R() - 0.5f) * 0.02f * scale;
                        var cc = (Color32)Var(R() < 0.12f ? Color.Lerp(col, Color.white, 0.6f) : col, 0.35f);
                        int b0 = v.Count;
                        v.Add(p - side); v.Add(p + side); v.Add(tip);
                        for (int q = 0; q < 3; q++) { nrm.Add(r); c.Add(cc); uv.Add(new Vector2(1, 0)); }
                        tri.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 1 });
                    }
                }
                else
                {
                    // 구슬: 작은 공, 세 개마다 금색
                    var bc = (Color32)(bead++ % 3 == 2 ? gold : col);
                    float rad = (bead % 3 == 0 ? 0.034f : 0.042f) * scale;
                    int seg = 7, ring = 5, b0 = v.Count;
                    for (int j = 0; j <= ring; j++)
                        for (int k = 0; k <= seg; k++)
                        {
                            float th = Mathf.PI * j / ring, ph = 2 * Mathf.PI * k / seg;
                            var nn = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                            v.Add(p + nn * rad); nrm.Add(nn); c.Add(bc); uv.Add(new Vector2(1, 0));
                        }
                    for (int j = 0; j < ring; j++)
                        for (int k = 0; k < seg; k++)
                        {
                            int a0 = b0 + j * (seg + 1) + k, a1 = a0 + seg + 1;
                            tri.AddRange(new[] { a0, a0 + 1, a1, a0 + 1, a1 + 1, a1 });
                        }
                }
            }
            carry = d - len;
        }
        var m = new Mesh { name = kind, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetNormals(nrm); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    Material garlandMat;
    GameObject GarlandObject(List<Vector3> path, string kind, Color col, float scale = 1f, bool colliders = true)
    {
        if (garlandMat == null) { garlandMat = new Material(AcmLoader.Mat) { name = "Garland" }; garlandMat.SetFloat("_HasTint", 0); garlandMat.SetFloat("_Gloss", 0.9f); }
        var go = new GameObject(kind);
        go.AddComponent<MeshFilter>().sharedMesh = GarlandMesh(path, kind, col, scale);
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = garlandMat; mr.shadowCastingMode = ShadowCastingMode.Off;
        if (colliders)
            for (int i = 0; i < path.Count; i += 4)
            {
                if (path[i] == Vector3.zero) continue;
                var sc = new GameObject("hit").AddComponent<SphereCollider>();
                sc.transform.SetParent(go.transform, false); sc.transform.position = path[i]; sc.radius = 0.12f;
            }
        return go;
    }

    // 아이콘용 짧은 줄 (아이콘 카메라 앞, 월드 y = -500 근처)
    GameObject GarlandIcon(string kind, Color col)
    {
        var path = new List<Vector3>();
        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            path.Add(new Vector3(-0.15f + 0.3f * t, -500.06f - 0.11f * Mathf.Sin(Mathf.PI * t), 0));
        }
        return GarlandObject(path, kind, col, 0.9f, false);
    }

    float ghostGarlandY = -99; string ghostGarlandKind = ""; Color ghostGarlandCol;
    void GhostGarland(float y)
    {
        string kind = ornTypes[ornSel];
        if (ghost != null && ghost.name == kind && Mathf.Abs(y - ghostGarlandY) < 0.08f && ghostGarlandCol == CurColor) { ghost.SetActive(true); return; }
        if (ghost) Destroy(ghost);
        ghost = GarlandObject(GarlandPath(y), kind, CurColor, 1f, false);
        ghost.name = kind; ghostGarlandY = y; ghostGarlandCol = CurColor;
        ghost.transform.SetParent(world, true);
    }

    void ShowGarland(Orn o)
    {
        ColorUtility.TryParseHtmlString("#" + o.color, out var c);
        var go = GarlandObject(GarlandPath(o.y), o.type, c);
        go.transform.SetParent(world, true);
        go.AddComponent<AcOrnTag>().id = o.id;
        ornObjs[o.id] = go; ornData[o.id] = o;
    }
}
