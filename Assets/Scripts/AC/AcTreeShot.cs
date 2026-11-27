// AcTreeShot.cs - 로딩 화면용 트리 사진 (11-27)
// 거실 트리를 정면에서 세로 사진(540x960)으로 찍어 orn/x~shot~tree 에 올림 → index.html 로딩 화면이 이 사진을 배경으로 씀.
// 언제: 트리 꾸미기를 마치고 나왔을 때(꾸민 사람이 바로), 그 밖에는 몇 분마다 한 번 확인해서 장식이 바뀌었는데 사진이 예전 것이면.
// 찍을 때만 사람·트리 앞을 가리는 벽을 잠깐 숨김 (화면에는 보이지 않음: 한 프레임 안에서 찍고 바로 되돌림)
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class AcRoom
{
    const int ShotW = 540, ShotH = 960;
    float shotCheckAt = -1, shotSoonAt = -1; bool shotWasDecorating; string shotLastH;

    void TreeShotUpdate()
    {
        if (net == null || net.Offline || phase != Phase.Play || treeCol == null || me == null) return;
        if (shotCheckAt < 0) shotCheckAt = Time.unscaledTime + UnityEngine.Random.Range(25f, 70f);   // 여럿이 동시에 찍지 않게 흩어 놓음
        if (shotWasDecorating && !decorating) shotSoonAt = Time.unscaledTime + 1.2f;                 // 꾸미기를 마침 → 곧 찍음
        shotWasDecorating = decorating;
        bool soon = shotSoonAt > 0 && Time.unscaledTime > shotSoonAt;
        if (!soon && Time.unscaledTime < shotCheckAt) return;
        if (decorating || building || kitchenOpen || playingGame || treeOverview || showCustom || GiftBusy) return;   // 다른 화면 중이면 다음에
        shotSoonAt = -1; shotCheckAt = Time.unscaledTime + UnityEngine.Random.Range(150f, 240f);
        string h = "v4-" + net.OrnHash();   // v2: 사진 구도를 바꾸면 숫자를 올려서 새로 찍게
        if (h == shotLastH) return;
        var cur = net.Get("shot"); string oldH = "";
        if (cur != null && cur.TryGetValue("tree", out var v) && v is Dictionary<string, object> d) oldH = AcJson.S(d, "h");
        if (h == oldH) { shotLastH = h; return; }
        var jpg = CaptureTree();
        if (jpg == null) return;
        shotLastH = h;
        net.Put("shot/tree", "{\"t\":" + Math.Floor(NowMs).ToString("0") + ",\"h\":" + AcJson.Esc(h) + ",\"by\":" + AcJson.Esc(uiName.Trim()) +
                              ",\"img\":" + AcJson.Esc(Convert.ToBase64String(jpg)) + "}");
    }

    byte[] CaptureTree()
    {
        if (cam == null) return null;
        float aspect = ShotW / (float)ShotH, fov = 42f;
        // 트리 전체(별까지)가 화면 아래쪽 2/3 안에 들어오게: 위쪽 1/3은 제목 자리
        float vfov = fov * Mathf.Deg2Rad;
        float frameH = Mathf.Max((TreeTop + 0.6f) / 0.66f, (TreeR * 2.5f) / aspect);
        float dist = frameH / (2f * Mathf.Tan(vfov / 2f)) + TreeR * 0.6f;
        var focus = TreePos + Vector3.up * (TreeTop * 0.5f + frameH * 0.13f);
        var rot = Quaternion.Euler(4f, 180f, 0);
        var pos = focus - rot * Vector3.forward * dist;

        var oldPos = cam.transform.position; var oldRot = cam.transform.rotation; float oldFov = cam.fieldOfView, oldFar = cam.farClipPlane;
        var hiddenGo = new List<GameObject>();
        var hiddenR = new List<(Renderer r, ShadowCastingMode m)>();
        RenderTexture rt = null; Texture2D tex = null;
        try
        {
            RestoreOccluders();   // 걸어 다닐 때 카메라를 가려서 반투명하게 숨겨 둔 것(트리 포함)을 먼저 되돌림 — 다음 확인 때 다시 숨김
            // 사람들은 잠깐 숨김
            foreach (var go in new[] { me ? me.gameObject : null }) if (go && go.activeSelf) { go.SetActive(false); hiddenGo.Add(go); }
            foreach (var r in remotes.Values) if (r.c && r.c.gameObject.activeSelf) { r.c.gameObject.SetActive(false); hiddenGo.Add(r.c.gameObject); }
            // 카메라와 트리 사이를 가리는 것(벽·지붕·가구)은 그림자만 남기고 숨김
            var targets = new[] { TreePos + Vector3.up * 0.6f, TreePos + Vector3.up * (TreeTop * 0.5f), TreePos + Vector3.up * TreeTop,
                                  TreePos + new Vector3(TreeR, TreeTop * 0.3f, 0), TreePos + new Vector3(-TreeR, TreeTop * 0.3f, 0) };
            var tb = treeCol.bounds;
            foreach (var r in world.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var b = r.bounds;
                if (b.size.y < 0.25f || (b.Intersects(tb) && b.size.x < tb.size.x * 2f)) continue;   // 바닥·트리와 그 장식은 그대로
                // 트리보다 카메라 쪽(앞)에 있는 벽·기둥·가구는 모두 숨김 (사진 아래쪽을 가리지 않게)
                if (b.min.z > TreePos.z + TreeR + 0.6f && b.max.y > 0.3f) { hiddenR.Add((r, r.shadowCastingMode)); r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; continue; }
                foreach (var t in targets)
                {
                    var dir = t - pos; float len = dir.magnitude;
                    if (b.IntersectRay(new Ray(pos, dir / len), out float hit) && hit < len - TreeR * 0.9f) { hiddenR.Add((r, r.shadowCastingMode)); r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; break; }
                }
            }
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov; cam.farClipPlane = Mathf.Max(oldFar, dist + 40f); cam.aspect = aspect;
            rt = RenderTexture.GetTemporary(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            bool done = false;
            try
            {
                var req = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); done = true; }
            }
            catch { }
            if (!done) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var prev = RenderTexture.active; RenderTexture.active = rt;
            tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            var c = tex.GetPixel(ShotW / 2, ShotH / 2); var c2 = tex.GetPixel(10, 10);
            if (c.r + c.g + c.b < 0.03f && c2.r + c2.g + c2.b < 0.03f) return null;   // 그려지지 않음
            return tex.EncodeToJPG(70);
        }
        catch (Exception e) { Debug.LogWarning("[TreeShot] " + e.Message); return null; }
        finally
        {
            foreach (var (r, m) in hiddenR) if (r) r.shadowCastingMode = m;
            foreach (var go in hiddenGo) if (go) go.SetActive(true);
            cam.transform.SetPositionAndRotation(oldPos, oldRot); cam.fieldOfView = oldFov; cam.farClipPlane = oldFar; cam.ResetAspect();
            if (rt) RenderTexture.ReleaseTemporary(rt);
            if (tex) Destroy(tex);
        }
    }
}
