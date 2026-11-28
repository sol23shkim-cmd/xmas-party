// AcNight.cs - 트리 전경 '야경' 모드 (11-27)
// 평소엔 안팎 모두 낮. 트리 전경에서 '야경' 버튼을 누르면 하늘·빛이 밤으로 바뀌고,
// 트리에 꼬마전구 줄이 감기며 색깔별로 반짝반짝 (창문·벽난로처럼 스스로 빛나는 것도 밤에 더 돋보임)
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine;

public partial class AcRoom
{
    const bool NightBulbsOn = false;
    bool nightView;                       // 트리 전경에서 야경 켬 (다음에 전경 볼 때도 기억)
    bool NightOn => nightView && treeOverview && phase == Phase.Play;
    GameObject nightBulbs;
    readonly List<(Renderer r, float phase, float speed)> bulbGroups = new List<(Renderer, float, float)>();
    MaterialPropertyBlock bulbMpb;
    GameObject treeStar;
    float glowK = -1f;
    readonly Dictionary<Renderer, float> glowOrig = new Dictionary<Renderer, float>();

    // 밤엔 꼭대기 별과 트리 장식이 은은하게 빛남 (원래 색(틴트)은 그대로 두고 _Emit만 올림)
    void NightGlow(float k)
    {
        if (Mathf.Abs(k - glowK) < 0.01f && !(k > 0 && glowK <= 0)) { if (k > 0.02f && treeStar) SetEmit(treeStar, k * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f)), true); return; }
        glowK = k;
        if (treeStar) SetEmit(treeStar, k, true);
        foreach (var tag in world.GetComponentsInChildren<AcOrnTag>(true)) SetEmit(tag.gameObject, k * 0.5f, false);
    }
    void SetEmit(GameObject g, float v, bool full)
    {
        bulbMpb ??= new MaterialPropertyBlock();
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(bulbMpb);
            if (!glowOrig.TryGetValue(r, out float o)) { o = bulbMpb.isEmpty ? 0f : bulbMpb.GetFloat("_Emit"); glowOrig[r] = o; }
            bulbMpb.SetFloat("_Emit", Mathf.Max(o, v));
            r.SetPropertyBlock(bulbMpb);
        }
    }

    void NightLights(float dt, float k)
    {
        NightGlow(k);
        if (!NightBulbsOn) return;   // 11-27: 실제로 달지 않은 꼬마전구 줄은 야경에서도 안 보이게 (꾸민 장식·별만 빛남)
        bool show = k > 0.02f;
        if (show && nightBulbs == null) BuildNightBulbs();
        if (nightBulbs == null) return;
        if (nightBulbs.activeSelf != show) nightBulbs.SetActive(show);
        if (!show) return;
        bulbMpb ??= new MaterialPropertyBlock();
        float t = Time.unscaledTime;
        foreach (var (r, ph, sp) in bulbGroups)
        {
            if (!r) continue;
            // 천천히 숨 쉬듯 + 가끔 톡 켜지는 반짝임
            float w = 0.5f + 0.5f * Mathf.Sin(t * sp + ph);
            float e = Mathf.Lerp(0.15f, 1f, w * w) * k;
            bulbMpb.SetFloat("_Emit", e);
            r.SetPropertyBlock(bulbMpb);
        }
    }

    // 트리 겉면을 따라 나선으로 꼬마전구 줄을 감음 (잎 메시에 광선을 쏴서 겉면 위치를 찾음)
    void BuildNightBulbs()
    {
        nightBulbs = new GameObject("NightBulbs");
        nightBulbs.transform.SetParent(world, false);
        var cols = new[] { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.82f, 0.35f), new Color(0.4f, 0.75f, 1f), new Color(0.5f, 1f, 0.55f), new Color(1f, 0.95f, 0.85f), new Color(1f, 0.5f, 0.85f) };
        int G = cols.Length;
        var builders = new AcPB[G * 2];   // 색깔마다 두 무리 → 서로 다른 박자로 반짝
        for (int i = 0; i < builders.Length; i++) builders[i] = new AcPB { detail = 1f };
        var wire = new AcPB { detail = 1f };
        float top = TreeTop, h0 = 0.75f, turns = 7.5f;
        int n = 190;
        Vector3 prev = Vector3.zero; bool hasPrev = false;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)(n - 1);
            float h = Mathf.Lerp(h0, top * 0.9f, Mathf.Pow(u, 0.85f));
            float ang = u * turns * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            var axis = TreePos + Vector3.up * h;
            float coneR = TreeR * 1.15f * Mathf.Clamp01(1f - (h - 0.4f) / (top - 0.2f)) + 0.12f;
            var p = axis + dir * coneR;
            if (treeCol != null)
            {
                var from = axis + dir * (TreeR * 2.5f + 1f);
                if (treeCol.Raycast(new Ray(from, (axis - from).normalized), out var hit, TreeR * 3f + 2f)) p = hit.point + (hit.point - axis).normalized * 0.06f;
            }
            int gi = (i % G) + G * ((i / G) % 2);
            builders[gi].Sphere(p, 0.075f, cols[i % G], 6, 8);
            if (hasPrev && (p - prev).magnitude < 1.2f) wire.Tube(new[] { prev, p }, new[] { 0.008f, 0.008f }, new Color(0.12f, 0.2f, 0.12f), 4);
            prev = p; hasPrev = true;
        }
        ProcObj("bulb_wire", wire.M("bulb_wire"), nightBulbs.transform, 0f, false);
        bulbGroups.Clear();
        for (int i = 0; i < builders.Length; i++)
        {
            var go = ProcObj("bulbs" + i, builders[i].M("bulbs" + i), nightBulbs.transform, 1f, false);
            bulbGroups.Add((go.GetComponent<Renderer>(), i * 1.37f, 1.6f + (i % 4) * 0.55f));
        }
        nightBulbs.SetActive(false);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void AcWeb_SaveImage(string b64, string name);
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int AcWeb_InstallState();
    [DllImport("__Internal")] static extern void AcWeb_Install();
    static int InstallState() => AcWeb_InstallState();
#else
    static int InstallState() => 0;
    static void AcWeb_Install() { }
#endif
    // 앱으로 받기 (파티 끝 창·지도 창): 설치 창이 되면 바로, 아니면 방법 안내
    bool AppInstallable => InstallState() != 0;
    void AppInstall()
    {
        switch (InstallState())
        {
            case 1: AcWeb_Install(); break;
            case 2: Toast("Safari 아래쪽 공유 버튼(□↑) → '홈 화면에 추가'를 누르면 주소창 없이 앱처럼 열려요"); break;
            case 4: Toast("카카오톡 안에서는 설치가 안 돼요. 오른쪽 아래 ⋮ → '다른 브라우저로 열기' 후 다시 눌러 주세요"); break;
            default: Toast("브라우저 메뉴(⋮ 또는 ≡) → '홈 화면에 추가' 또는 '앱 설치'를 누르면 앱처럼 열려요"); break;
        }
    }

    // 트리 전경 사진 저장: 버튼·글씨 없이 지금 보이는 화면 그대로 (낮·야경 모두)
    bool shotPending; float shotFlash;
    void OverviewPhoto()
    {
        if (!shotPending || cam == null) return;
        shotPending = false;
        int W = Screen.width, H = Screen.height; float k = Mathf.Min(1f, 2048f / Mathf.Max(W, H)); W = Mathf.Max(64, (int)(W * k)); H = Mathf.Max(64, (int)(H * k));
        RenderTexture rt = null; Texture2D tex = null;
        try
        {
            rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            bool done = false;
            try
            {
                var req = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, req)) { req.destination = rt; RenderPipeline.SubmitRenderRequest(cam, req); done = true; }
            }
            catch { }
            if (!done) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var prev = RenderTexture.active; RenderTexture.active = rt;
            tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            var jpg = tex.EncodeToJPG(92);
            string name = "크리스마스파티_" + (nightView ? "야경_" : "트리_") + System.DateTime.Now.ToString("MMdd_HHmm") + ".jpg";
#if UNITY_WEBGL && !UNITY_EDITOR
            AcWeb_SaveImage(System.Convert.ToBase64String(jpg), name);
#else
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.persistentDataPath, name), jpg);
#endif
            shotFlash = 1f; Sfx("candy_win", 0.35f);
            Toast(IsMobile ? "사진을 만들었어요! 저장하거나 친구에게 보내 보세요" : "사진을 저장했어요 (내려받기 폴더)");
        }
        catch (System.Exception e) { Debug.LogWarning("[OverviewPhoto] " + e.Message); Toast("사진을 만들지 못했어요"); }
        finally { if (rt) RenderTexture.ReleaseTemporary(rt); if (tex) Destroy(tex); }
    }
    void PhotoFlashGUI()
    {
        if (shotFlash <= 0f) return;
        shotFlash = Mathf.Max(0f, shotFlash - Time.unscaledDeltaTime * 2.5f);
        var old = GUI.color; GUI.color = new Color(1, 1, 1, shotFlash * 0.8f);
        GUI.DrawTexture(new Rect(0, 0, SW, SH), Texture2D.whiteTexture); GUI.color = old;
    }

    // 트리 전경 화면의 '야경 / 낮으로' 버튼
    void NightToggleGUI(Rect r)
    {
        if (GUI.Button(r, nightView ? "<b>낮으로</b>" : "<b>야경 보기</b>", sBtn)) nightView = !nightView;
    }
}
