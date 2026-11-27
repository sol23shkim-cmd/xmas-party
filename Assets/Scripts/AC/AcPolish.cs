// AcPolish.cs - 11-27 점검 후 다듬기: 카메라를 가리는 물체 숨기기, 캐릭터끼리 살짝 밀어내기
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class AcRoom
{
    // ===== 카메라와 내 캐릭터 사이를 가리는 큰 물체(안뜰 큰 트리·별 등)는 잠깐 안 보이게 (그림자는 남김) =====
    readonly List<Renderer> occCands = new List<Renderer>();
    readonly Dictionary<Renderer, ShadowCastingMode> occHidden = new Dictionary<Renderer, ShadowCastingMode>();
    float occNext, occGatherAt = -99f;
    static readonly string[] OccSkip = { "wall", "floor", "ground", "rug", "path", "pond", "ceiling", "roof", "fence", "Peer", "Snowman", "dome", "carpet" };

    void GatherOccluders()
    {
        occCands.Clear();
        if (!world) return;
        foreach (var r in world.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer)) continue;
            bool skip = false;
            for (var t = r.transform; t != null && t != world; t = t.parent)
            {
                foreach (var k in OccSkip) if (t.name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) { skip = true; break; }
                if (skip) break;
            }
            if (skip || r.GetComponentInParent<AcCharacter>() != null) continue;
            occCands.Add(r);
        }
        occGatherAt = Time.unscaledTime;
    }

    void OccluderUpdate()
    {
        bool active = phase == Phase.Play && me != null && !kitchenOpen && !GiftBusy && !playingGame && !decorating && !treeOverview && !building && snowFocusId == null && !GreenBusy;
        if (!active) { RestoreOccluders(); return; }
        if (Time.unscaledTime < occNext) return;
        occNext = Time.unscaledTime + 0.15f;
        if (Time.unscaledTime - occGatherAt > 12f) GatherOccluders();
        var camPos = cam.transform.position;
        var still = new HashSet<Renderer>();
        // 머리·가슴·발 세 점으로 확인 (조금만 걸쳐도 캐릭터가 보이게)
        foreach (float h in new[] { 0.25f, 0.9f, 1.6f })
        {
            var target = me.transform.position + Vector3.up * h;
            var dir = target - camPos; float dist = dir.magnitude; if (dist < 0.5f) continue;
            var ray = new Ray(camPos, dir / dist);
            foreach (var r in occCands)
            {
                if (!r || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.y < 0.9f || b.extents.magnitude > 9f || b.max.y < 0.8f) continue;
                if (b.Contains(target)) continue;   // 캐릭터가 그 안에 있으면(방 전체 같은 큰 것) 무시
                b.Expand(0.15f);
                if (b.IntersectRay(ray, out float d) && d < dist - 0.6f) still.Add(r);
            }
        }
        foreach (var r in still)
            if (!occHidden.ContainsKey(r)) { occHidden[r] = r.shadowCastingMode; r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; }
        var back = new List<Renderer>();
        foreach (var kv in occHidden) if (!still.Contains(kv.Key)) back.Add(kv.Key);
        foreach (var r in back) { if (r) r.shadowCastingMode = occHidden[r]; occHidden.Remove(r); }
    }

    void RestoreOccluders()
    {
        if (occHidden.Count == 0) return;
        foreach (var kv in occHidden) if (kv.Key) kv.Key.shadowCastingMode = kv.Value;
        occHidden.Clear();
    }

    // ===== 다른 참가자와 겹치지 않게 내 캐릭터를 살짝 밀어냄 =====
    Vector3 PushFromPeers(Vector3 p, float r)
    {
        foreach (var rm in remotes.Values)
        {
            if (rm.c == null) continue;
            var q = rm.c.transform.position; var d = new Vector2(p.x - q.x, p.z - q.z); float rr = r * 2f;
            if (d.sqrMagnitude < rr * rr && d.sqrMagnitude > 1e-6f) { d = d.normalized * rr; p.x = q.x + d.x; p.z = q.z + d.y; }
        }
        return p;
    }

    // ===== 빛: 안팎 모두 늘 낮 (11-27: 밖에서 어두워지던 것 없앰). 트리 전경 '야경'일 때만 밤 =====
    float outdoorK = -1f;
    void OutdoorTone(float dt)
    {
        var m = AcmLoader.Mat; if (m == null) return;
        float want = NightOn ? 1f : 0f;
        float k = outdoorK < 0 ? want : Mathf.MoveTowards(outdoorK, want, dt * 1.4f);
        if (Mathf.Abs(k - outdoorK) > 1e-4f)
        {
            outdoorK = k;
            m.SetVector("_LightTint", Vector4.Lerp(new Vector4(1.04f, 1.0f, 0.94f, 1), new Vector4(0.40f, 0.44f, 0.70f, 1), k));
            m.SetVector("_AmbTop", Vector4.Lerp(new Vector4(1.0f, 1.0f, 1.04f, 1), new Vector4(0.38f, 0.42f, 0.64f, 1), k));
            m.SetVector("_AmbBottom", Vector4.Lerp(new Vector4(0.86f, 0.82f, 0.86f, 1), new Vector4(0.17f, 0.16f, 0.27f, 1), k));
            m.SetVector("_ShadowTint", Vector4.Lerp(new Vector4(0.8f, 0.8f, 0.95f, 1), new Vector4(0.13f, 0.15f, 0.30f, 1), k));
            if (cam) cam.backgroundColor = Color.Lerp(new Color(0.62f, 0.78f, 0.92f), new Color(0.04f, 0.06f, 0.15f), k);
            RenderSettings.fogColor = Color.Lerp(new Color(0.82f, 0.88f, 0.95f), new Color(0.06f, 0.08f, 0.18f), k);
            if (snowMat) snowMat.SetFloat("_Emit", Mathf.Lerp(0.55f, 0.85f, k));
        }
        NightLights(dt, k);
    }
}
