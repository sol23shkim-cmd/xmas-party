// AcFx.cs - 작은 효과 조각들 (반짝이 별·색종이·물방울·종이 조각): 온실·선물 포장실에서 함께 씀 (11-26)
// 물체 몇십 개를 돌려 쓰며 직접 움직임 (파티클 시스템 없이 → 색·모양을 마음대로)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class AcRoom
{
    class FxBit { public Transform tr; public Vector3 vel, spinAx; public float life, max, spin, s0, grav, drag; public string key; public bool fade; }
    readonly List<FxBit> fxBits = new List<FxBit>();
    readonly Dictionary<string, Stack<Transform>> fxPool = new Dictionary<string, Stack<Transform>>();
    readonly Dictionary<string, Mesh> fxMeshes = new Dictionary<string, Mesh>();

    // kind: star 별, conf 색종이, drop 물방울, dot 동그라미, heart 하트
    Mesh FxMesh(string kind, Color col)
    {
        string key = kind + ColorUtility.ToHtmlStringRGB(col);
        if (fxMeshes.TryGetValue(key, out var m)) return m;
        var b = new AcPB();
        switch (kind)
        {
            case "star": b.Star(Vector3.zero, Vector3.forward, Vector3.up, 0.5f, 0.22f, 0.08f, col); break;
            case "conf": b.Box(Vector3.zero, new Vector3(0.8f, 0.5f, 0.03f), col); break;
            case "drop": b.Ellipsoid(Vector3.zero, new Vector3(0.3f, 0.45f, 0.3f), col, 5, 7); break;
            case "heart": b.Heart(Vector3.zero, 0.9f, col, 0.4f, 18); break;
            default: b.Sphere(Vector3.zero, 0.4f, col, 5, 7); break;
        }
        m = b.M("fx_" + key);
        fxMeshes[key] = m;
        return m;
    }

    Transform FxTake(string kind, Color col)
    {
        string key = kind + ColorUtility.ToHtmlStringRGB(col);
        if (fxPool.TryGetValue(key, out var st) && st.Count > 0) { var t0 = st.Pop(); t0.gameObject.SetActive(true); return t0; }
        var go = ProcObj("fx", FxMesh(kind, col), null, kind == "star" || kind == "heart" ? 0.55f : kind == "drop" ? 0.25f : 0.15f, false);
        return go.transform;
    }

    // pos 둘레에 n개 흩뿌리기
    void FxBurst(string kind, Vector3 pos, int n, float speed, float size, Color[] cols, float life = 1.1f, float grav = 4f, Transform parent = null, Vector3? dirBias = null)
    {
        for (int i = 0; i < n; i++)
        {
            var col = cols[Random.Range(0, cols.Length)];
            var tr = FxTake(kind, col);
            tr.SetParent(parent, true);
            tr.position = pos + Random.insideUnitSphere * size * 0.6f;
            var dir = Random.onUnitSphere; if (dir.y < -0.2f) dir.y = -dir.y;
            if (dirBias.HasValue) dir = (dir + dirBias.Value).normalized;
            float s = size * Random.Range(0.6f, 1.15f);
            tr.localScale = Vector3.one * s;
            tr.rotation = Random.rotation;
            fxBits.Add(new FxBit { tr = tr, vel = dir * speed * Random.Range(0.5f, 1.1f), life = 0, max = life * Random.Range(0.7f, 1.2f), spin = Random.Range(180f, 720f), spinAx = Random.onUnitSphere, s0 = s, grav = grav, drag = kind == "conf" ? 2.2f : 1.2f, key = kind + ColorUtility.ToHtmlStringRGB(col), fade = true });
        }
    }

    // 한 개 (물방울 줄기처럼 정해진 속도로)
    void FxOne(string kind, Vector3 pos, Vector3 vel, float size, Color col, float life, float grav, Transform parent = null)
    {
        var tr = FxTake(kind, col);
        tr.SetParent(parent, true);
        tr.position = pos; tr.localScale = Vector3.one * size; tr.rotation = Quaternion.LookRotation(vel.sqrMagnitude > 1e-6f ? vel : Vector3.forward);
        fxBits.Add(new FxBit { tr = tr, vel = vel, life = 0, max = life, spin = 0, spinAx = Vector3.up, s0 = size, grav = grav, drag = 0.2f, key = kind + ColorUtility.ToHtmlStringRGB(col), fade = false });
    }

    void FxUpdate(float dt)
    {
        for (int i = fxBits.Count - 1; i >= 0; i--)
        {
            var f = fxBits[i];
            f.life += dt;
            if (f.tr == null) { fxBits.RemoveAt(i); continue; }
            if (f.life >= f.max)
            {
                f.tr.gameObject.SetActive(false); f.tr.SetParent(null, false);
                if (!fxPool.TryGetValue(f.key, out var st)) fxPool[f.key] = st = new Stack<Transform>();
                st.Push(f.tr);
                fxBits.RemoveAt(i); continue;
            }
            f.vel += Vector3.down * f.grav * dt;
            f.vel *= Mathf.Exp(-f.drag * dt);
            f.tr.position += f.vel * dt;
            if (f.spin > 0) f.tr.Rotate(f.spinAx, f.spin * dt, Space.World);
            else if (f.vel.sqrMagnitude > 1e-4f) f.tr.rotation = Quaternion.LookRotation(f.vel);
            float k = f.life / f.max;
            float sc = f.fade ? (k < 0.15f ? k / 0.15f : 1 - Mathf.Pow((k - 0.15f) / 0.85f, 2f)) : 1f;
            f.tr.localScale = Vector3.one * f.s0 * Mathf.Max(0.01f, sc);
        }
    }

    static readonly Color[] FxGold = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.95f, 0.6f), Color.white };
    static readonly Color[] FxParty = { new Color(0.95f, 0.25f, 0.3f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.75f, 1f), new Color(0.45f, 0.85f, 0.4f), new Color(0.8f, 0.5f, 1f), new Color(1f, 0.55f, 0.75f) };
    static readonly Color[] FxWater = { new Color(0.55f, 0.8f, 1f), new Color(0.75f, 0.9f, 1f) };
}
