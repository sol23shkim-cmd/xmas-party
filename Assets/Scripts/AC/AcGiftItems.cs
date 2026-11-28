// AcGiftItems.cs - 11-28 선물 다시 만들기: 더 귀엽고 고급스러운 모양 + 물건마다 어울리는 움직임
//   아기 고양이·강아지 인형(새로), 2단 꼬마 눈사람, 피어나는 포인세티아, 왼쪽 구름부터 생겨나는 무지개, 부풀어 오르는 하트 풍선
//   별 모양 장식은 스스로 반짝 (별 요술봉·미니 트리 별)
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    static void Gloss(GameObject go, float g)
    {
        if (!go) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { var m = new MaterialPropertyBlock(); r.GetPropertyBlock(m); m.SetFloat("_Gloss", g); r.SetPropertyBlock(m); }
    }
    static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; x = Mathf.Clamp01(x); return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }

    // ProcItemInner 맨 앞에서 부름: 새로 만든 물건이면 true
    bool ProcItemExtra(string id, GameObject root)
    {
        GameObject Part(string name, AcPB pb, Vector3 pos, float emit = 0f, Transform parent = null) { var go = ProcObj(name, pb.M(name), parent ? parent : root.transform, emit); go.transform.localPosition = pos; return go; }
        Transform Node(string name, Vector3 pos, Transform parent = null) { var t = new GameObject(name).transform; t.SetParent(parent ? parent : root.transform, false); t.localPosition = pos; return t; }
        Color white = new Color(0.99f, 0.98f, 0.97f), eyeC = new Color(0.1f, 0.08f, 0.1f), pink = new Color(1f, 0.6f, 0.68f), gold = new Color(1f, 0.8f, 0.3f);
        void Eyes(AcPB pb, float x, float y, float z, float s)
        {   // 크고 반짝이는 눈 (하이라이트 두 개)
            foreach (float sg in new[] { -1f, 1f })
            {
                var c = new Vector3(sg * x, y, z);
                pb.Ellipsoid(c, new Vector3(0.026f, 0.032f, 0.014f) * s, eyeC, 10, 14);
                pb.Sphere(c + new Vector3(-0.008f, 0.011f, 0.012f) * s, 0.0085f * s, Color.white, 6, 8);
                pb.Sphere(c + new Vector3(0.007f, -0.008f, 0.013f) * s, 0.0042f * s, Color.white, 5, 6);
            }
        }
        void Smile(AcPB pb, Vector3 c, float s, Color col)
        {   // ω 입
            foreach (float sg in new[] { -1f, 1f })
            {
                var pts = new List<Vector3>(); var rs = new List<float>();
                for (int i = 0; i <= 8; i++) { float a = Mathf.PI * i / 8f; pts.Add(c + new Vector3(sg * (0.01f + Mathf.Cos(a) * 0.01f) * s, -Mathf.Sin(a) * 0.009f * s, 0)); rs.Add(0.0022f * s); }
                pb.Tube(pts, rs, col, 5);
            }
        }
        switch (id)
        {
            case "p_kitty":
            case "p_puppy":
                {   // 11-28 다시: 병아리·펭귄처럼 둥글둥글한 덩어리 + 점 눈 (다른 인형들과 같은 결)
                    bool cat = id == "p_kitty";
                    Color fur = cat ? new Color(1f, 0.78f, 0.5f) : new Color(0.97f, 0.88f, 0.72f);
                    Color ear = cat ? fur : new Color(0.72f, 0.5f, 0.34f);
                    Color muz = new Color(1f, 0.97f, 0.93f);
                    var b = new AcPB();
                    b.Sphere(new Vector3(0, 0.1f, 0), 0.1f, fur, 10, 14);                       // 몸
                    b.Ellipsoid(new Vector3(0, 0.095f, 0.055f), new Vector3(0.06f, 0.065f, 0.05f), muz, 8, 12);   // 배
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        b.Sphere(new Vector3(sg * 0.045f, 0.03f, 0.075f), 0.032f, fur, 6, 8);    // 앞발
                        b.Sphere(new Vector3(sg * 0.065f, 0.02f, 0.02f), 0.035f, fur, 6, 8);     // 뒷발
                    }
                    Part("body", b, Vector3.zero);
                    var deco = new AcPB();
                    if (cat)
                    {   // 귀 옆 빨간 리본
                        Color red = new Color(0.92f, 0.22f, 0.28f);
                        var c0 = new Vector3(0.07f, 0.355f, 0.03f);
                        deco.Ellipsoid(c0 + new Vector3(-0.022f, 0, 0), new Vector3(0.024f, 0.017f, 0.012f), red, 6, 8, Quaternion.Euler(0, 0, 20));
                        deco.Ellipsoid(c0 + new Vector3(0.022f, 0, 0), new Vector3(0.024f, 0.017f, 0.012f), red, 6, 8, Quaternion.Euler(0, 0, -20));
                        deco.Sphere(c0, 0.011f, red * 0.9f, 5, 6);
                    }
                    else
                    {   // 파란 목도리
                        Color blue = new Color(0.3f, 0.6f, 0.95f);
                        deco.Torus(new Vector3(0, 0.178f, 0.003f), Vector3.up, 0.07f, 0.017f, blue, 18, 7);
                        deco.Ellipsoid(new Vector3(0.045f, 0.145f, 0.075f), new Vector3(0.02f, 0.035f, 0.011f), blue, 5, 7, Quaternion.Euler(0, 0, -12));
                    }
                    var head = Node("head", new Vector3(0, 0.19f, 0));
                    var hb = new AcPB();
                    hb.Sphere(new Vector3(0, 0.075f, 0), 0.095f, fur, 12, 16);
                    hb.Ellipsoid(new Vector3(0, 0.045f, 0.07f), new Vector3(0.045f, 0.032f, 0.03f), muz, 8, 10);     // 주둥이
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        if (cat)
                        {
                            hb.Cylinder(new Vector3(sg * 0.055f, 0.14f, 0), new Vector3(sg * 0.075f, 0.19f, -0.005f), 0.034f, 0.008f, ear, 10);
                            hb.Cylinder(new Vector3(sg * 0.056f, 0.145f, 0.012f), new Vector3(sg * 0.072f, 0.18f, 0.006f), 0.02f, 0.005f, new Color(1f, 0.7f, 0.72f), 8);
                        }
                        else hb.Ellipsoid(new Vector3(sg * 0.088f, 0.08f, -0.005f), new Vector3(0.028f, 0.058f, 0.03f), ear, 8, 10, Quaternion.Euler(0, 0, sg * 18f));
                    }
                    if (!cat) hb.Ellipsoid(new Vector3(-0.04f, 0.13f, 0.045f), new Vector3(0.035f, 0.03f, 0.03f), ear, 6, 8);   // 이마 얼룩
                    // 점 눈 + 볼터치 + 웃는 입 (다른 인형들과 같은 얼굴)
                    var fc = new Vector3(0, 0.075f, 0.094f); float fs = 0.085f;
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        hb.Sphere(fc + new Vector3(sg * 0.4f * fs, 0.12f * fs, -0.01f), 0.1f * fs, eyeC, 5, 7);
                        hb.Sphere(fc + new Vector3(sg * 0.4f * fs + 0.03f * fs, 0.16f * fs, 0.05f * fs - 0.01f), 0.03f * fs, Color.white, 3, 4);
                        { float bx = sg * 0.6f * fs, by = 0.065f; hb.Ellipsoid(new Vector3(bx, by, Mathf.Sqrt(0.095f * 0.095f - bx * bx - (by - 0.075f) * (by - 0.075f)) - 0.001f), new Vector3(0.012f, 0.007f, 0.004f), pink, 4, 6); }
                    }
                    hb.Ellipsoid(new Vector3(0, 0.058f, 0.1f), new Vector3(0.012f, 0.009f, 0.008f), cat ? new Color(1f, 0.55f, 0.6f) : eyeC, 5, 6);   // 코
                    Smile(hb, new Vector3(0, 0.044f, 0.1f), 0.8f, eyeC);
                    Part("headmesh", hb, Vector3.zero, 0f, head);
                    Part("deco", deco, cat ? new Vector3(0, -0.19f, 0) : Vector3.zero, 0f, cat ? head : null);
                    var tail = Node("tail", new Vector3(0, 0.06f, -0.09f));
                    var tb = new AcPB();
                    if (cat) { var tp = new[] { Vector3.zero, new Vector3(0.015f, 0.045f, -0.03f), new Vector3(0.04f, 0.1f, -0.03f), new Vector3(0.05f, 0.13f, -0.005f) }; tb.Tube(tp, new[] { 0.022f, 0.021f, 0.02f, 0.019f }, fur, 10); tb.Sphere(tp[3], 0.019f, fur, 6, 8); }
                    else tb.Sphere(new Vector3(0, 0.02f, -0.01f), 0.03f, fur, 6, 8);
                    Part("tailmesh", tb, Vector3.zero, 0f, tail);
                    return true;
                }
            case "p_snowman":
                {   // 2단 동글 눈사람 (점 눈, 짧은 장갑 팔, 털모자·목도리)
                    Color snow = new Color(0.98f, 0.985f, 1f);
                    var b = new AcPB();
                    b.Sphere(new Vector3(0, 0.1f, 0), 0.105f, snow, 12, 16);
                    for (int k = 0; k < 2; k++) b.Sphere(new Vector3(0, 0.085f + k * 0.04f, 0.1f - k * 0.006f), 0.011f, new Color(0.25f, 0.2f, 0.22f), 5, 6);
                    Color mit = new Color(0.92f, 0.22f, 0.28f);
                    foreach (float sg in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(sg * 0.105f, 0.12f, 0.01f), new Vector3(0.028f, 0.035f, 0.028f), mit, 6, 8, Quaternion.Euler(0, 0, sg * 30f));
                    Part("body", b, Vector3.zero);
                    var sc = new AcPB(); Color scarf = new Color(0.25f, 0.6f, 0.4f);
                    sc.Torus(new Vector3(0, 0.198f, 0.003f), Vector3.up, 0.06f, 0.018f, scarf, 18, 7);
                    sc.Ellipsoid(new Vector3(0.04f, 0.165f, 0.07f), new Vector3(0.02f, 0.036f, 0.011f), scarf, 5, 7, Quaternion.Euler(0, 0, -12));
                    Part("scarf", sc, Vector3.zero);
                    var head = Node("head", new Vector3(0, 0.2f, 0));
                    var hb = new AcPB();
                    hb.Sphere(new Vector3(0, 0.07f, 0), 0.078f, snow, 12, 16);
                    var fc = new Vector3(0, 0.068f, 0.076f); float fs = 0.075f;
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        hb.Sphere(fc + new Vector3(sg * 0.42f * fs, 0.15f * fs, -0.006f), 0.1f * fs, eyeC, 5, 7);
                        hb.Sphere(fc + new Vector3(sg * 0.42f * fs + 0.03f * fs, 0.19f * fs, 0.05f * fs - 0.006f), 0.03f * fs, Color.white, 3, 4);
                        { float bx = sg * 0.6f * fs, by = 0.058f; hb.Ellipsoid(new Vector3(bx, by, Mathf.Sqrt(0.078f * 0.078f - bx * bx - (by - 0.07f) * (by - 0.07f)) - 0.001f), new Vector3(0.011f, 0.0065f, 0.004f), pink, 4, 6); }
                    }
                    hb.Cylinder(new Vector3(0, 0.064f, 0.074f), new Vector3(0, 0.06f, 0.105f), 0.011f, 0.003f, new Color(1f, 0.55f, 0.18f), 8);   // 짧은 당근 코
                    Smile(hb, new Vector3(0, 0.045f, 0.074f), 0.75f, eyeC);
                    Color hat = new Color(0.92f, 0.22f, 0.28f);
                    hb.Ellipsoid(new Vector3(0, 0.122f, -0.003f), new Vector3(0.068f, 0.048f, 0.066f), hat, 10, 14);
                    hb.Torus(new Vector3(0, 0.112f, -0.003f), Vector3.up, 0.066f, 0.013f, Color.white, 18, 6);
                    hb.Sphere(new Vector3(0, 0.177f, -0.008f), 0.02f, Color.white, 6, 8);
                    Part("headmesh", hb, Vector3.zero, 0f, head);
                    return true;
                }
            case "p_poinsettia":
                {
                    var pb = new AcPB();
                    Color potC = new Color(0.95f, 0.75f, 0.3f);
                    pb.Cylinder(Vector3.zero, new Vector3(0, 0.11f, 0), 0.06f, 0.075f, potC, 24);
                    pb.Torus(new Vector3(0, 0.11f, 0), Vector3.up, 0.075f, 0.01f, new Color(1f, 0.85f, 0.45f), 28, 6);
                    pb.Disk(new Vector3(0, 0.108f, 0), Vector3.up, 0.07f, new Color(0.35f, 0.24f, 0.18f), 20, false);
                    pb.Torus(new Vector3(0, 0.055f, 0), Vector3.up, 0.069f, 0.005f, new Color(0.85f, 0.2f, 0.25f), 28, 5);   // 리본 띠
                    Gloss(Part("pot", pb, Vector3.zero), 0.75f);
                    var lb = new AcPB();
                    Color leafG = new Color(0.16f, 0.48f, 0.24f);
                    for (int k = 0; k < 7; k++) { float a = k * Mathf.PI * 2 / 7f + 0.2f; var d = new Vector3(Mathf.Cos(a), 0.25f, Mathf.Sin(a)).normalized; lb.Leaf(new Vector3(0, 0.12f, 0), d, Vector3.Cross(Vector3.up, d).normalized, 0.11f, 0.045f, leafG, leafG * 1.2f, 0.12f, 1, 6, 0.35f); }
                    Gloss(Part("leaves", lb, Vector3.zero), 0.35f);
                    // 빨간 포엽: 하나씩 따로 (피어나는 동작)
                    var bm = new AcPB(); Color red = new Color(0.86f, 0.08f, 0.14f), redT = new Color(0.98f, 0.25f, 0.3f);
                    bm.Leaf(Vector3.zero, Vector3.right, Vector3.forward, 0.1f, 0.042f, red, redT, 0.1f, 1, 7, 0.15f);
                    var bractMesh = bm.M("bract");
                    var bm2 = new AcPB(); bm2.Leaf(Vector3.zero, Vector3.right, Vector3.forward, 0.065f, 0.03f, red, redT, 0.12f, 1, 6, 0.1f);
                    var bractMesh2 = bm2.M("bract2");
                    var center = Node("bloom", new Vector3(0, 0.15f, 0));
                    for (int k = 0; k < 8; k++)
                    {
                        var g = ProcObj("bract", bractMesh, center, 0.05f); g.transform.localRotation = Quaternion.Euler(0, k * 45f, 0);
                        Gloss(g, 0.4f);
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        var g = ProcObj("bract2", bractMesh2, center, 0.08f); g.transform.localPosition = new Vector3(0, 0.008f, 0); g.transform.localRotation = Quaternion.Euler(0, k * 60f + 30f, 0);
                        Gloss(g, 0.4f);
                    }
                    var cc = new AcPB();
                    for (int k = 0; k < 7; k++) { float a = k * 0.9f; cc.Sphere(new Vector3(Mathf.Cos(a) * 0.012f * (k > 0 ? 1 : 0), 0.012f, Mathf.Sin(a) * 0.012f * (k > 0 ? 1 : 0)), 0.007f, new Color(1f, 0.85f, 0.25f), 5, 6); }
                    Part("pollen", cc, Vector3.zero, 0.3f, center);
                    return true;
                }
            case "p_rainbow":
                {
                    var cl = new AcPB();
                    Color cw = Color.white, cs = new Color(0.86f, 0.9f, 0.98f);
                    System.Func<Vector3, Color> sh = u => Color.Lerp(cs, cw, Mathf.Clamp01(0.5f + u.y * 0.6f));
                    foreach (float sx in new[] { -0.24f, 0.24f })
                    {
                        cl.Ellipsoid(new Vector3(sx, 0.045f, 0), new Vector3(0.075f, 0.06f, 0.06f), cw, 12, 16, null, sh);
                        cl.Ellipsoid(new Vector3(sx - 0.06f, 0.025f, 0.015f), new Vector3(0.05f, 0.04f, 0.045f), cw, 10, 12, null, sh);
                        cl.Ellipsoid(new Vector3(sx + 0.06f, 0.025f, 0.01f), new Vector3(0.052f, 0.042f, 0.045f), cw, 10, 12, null, sh);
                        cl.Ellipsoid(new Vector3(sx + 0.02f, 0.075f, -0.01f), new Vector3(0.045f, 0.04f, 0.04f), cw, 10, 12, null, sh);
                    }
                    Part("clouds", cl, Vector3.zero);
                    var arcs = new GameObject("arcs"); arcs.transform.SetParent(root.transform, false);
                    arcs.AddComponent<MeshFilter>().sharedMesh = RainbowMesh(40);
                    var mr = arcs.AddComponent<MeshRenderer>(); mr.sharedMaterial = AcmLoader.Mat;
                    var m = new MaterialPropertyBlock(); m.SetFloat("_Gloss", 0.45f); m.SetFloat("_Emit", 0.12f); mr.SetPropertyBlock(m);
                    return true;
                }
            case "p_balloons":
                {
                    Color[] bc = { new Color(0.95f, 0.18f, 0.3f), new Color(1f, 0.52f, 0.7f), new Color(0.98f, 0.32f, 0.5f) };
                    Vector3[] tops = { new Vector3(0, 0.62f, 0), new Vector3(-0.13f, 0.52f, 0.03f), new Vector3(0.13f, 0.5f, -0.02f) };
                    for (int i = 0; i < 3; i++)
                    {
                        var node = Node("b" + i, tops[i]);
                        var hb = new AcPB(); hb.Heart(Vector3.zero, 0.17f, bc[i], 0.55f, 30);
                        hb.Ellipsoid(new Vector3(-0.035f, 0.035f, 0.05f), new Vector3(0.018f, 0.028f, 0.008f), Color.white, 6, 8, Quaternion.Euler(0, 0, 25f));   // 반짝 하이라이트
                        hb.Cylinder(new Vector3(0, -0.085f, 0), new Vector3(0, -0.1f, 0), 0.004f, 0.012f, bc[i], 8);   // 묶은 꼭지
                        var g = Part("balloon", hb, Vector3.zero, 0.06f, node); Gloss(g, 0.95f);
                        var sb = new AcPB(); sb.Tube(new[] { new Vector3(0, 0.02f, 0), (tops[i] - new Vector3(0, 0.1f, 0)) * 0.5f + new Vector3(0.02f, 0, 0), tops[i] - new Vector3(0, 0.1f, 0) }, new[] { 0.002f, 0.002f, 0.002f }, white, 4);
                        Part("s" + i, sb, Vector3.zero);
                    }
                    var w2 = new AcPB(); w2.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.03f, 0.04f), gold, 8, 12);
                    w2.Torus(new Vector3(0, 0.05f, 0), Vector3.up, 0.02f, 0.006f, new Color(0.95f, 0.3f, 0.45f), 14, 5);
                    Gloss(Part("weight", w2, Vector3.zero), 0.7f);
                    return true;
                }
        }
        return false;
    }

    // 무지개 띠: 왼쪽(각도 π)부터 오른쪽(0)까지 step/40 만큼 그림 (단계별로 만들어 둠)
    static readonly Mesh[] rainbowMeshes = new Mesh[41];
    static Mesh RainbowMesh(int step)
    {
        step = Mathf.Clamp(step, 0, 40);
        if (rainbowMeshes[step] != null) return rainbowMeshes[step];
        var b = new AcPB { detail = 1.6f };
        Color[] rc = { new Color(0.95f, 0.25f, 0.3f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.88f, 0.3f), new Color(0.4f, 0.85f, 0.4f), new Color(0.35f, 0.65f, 1f), new Color(0.65f, 0.45f, 0.95f) };
        float p = step / 40f;
        if (step > 0)
            for (int k = 0; k < rc.Length; k++)
            {
                float R = 0.3f - k * 0.032f; var pts = new List<Vector3>(); var rs = new List<float>();
                int n = Mathf.Max(2, Mathf.CeilToInt(40 * p));
                for (int i = 0; i <= n; i++) { float a = Mathf.PI - Mathf.PI * p * i / n; pts.Add(new Vector3(Mathf.Cos(a) * R, 0.04f + Mathf.Sin(a) * R, 0)); rs.Add(0.0165f); }
                b.Tube(pts, rs, rc[k], 8);
            }
        var m = b.M("rainbow_" + step);
        rainbowMeshes[step] = m;
        return m;
    }

    // 새 동작들 (AnimItem에서 부름)
    void AnimExtra(GameObject holder, string anim, float t, ref float y, ref float yaw, ref float rz)
    {
        var tr = holder.transform;
        Transform Find(string n) { foreach (var c in holder.GetComponentsInChildren<Transform>()) if (c.name == n) return c; return null; }
        switch (anim)
        {
            case "kitty":
                {
                    yaw += 20f * Mathf.Sin(t * 0.7f);
                    var h = Find("head"); if (h) h.localRotation = Quaternion.Euler(0, 8f * Mathf.Sin(t * 0.9f), 10f * Mathf.Sin(t * 1.4f));
                    var tl = Find("tail"); if (tl) tl.localRotation = Quaternion.Euler(0, 0, 18f * Mathf.Sin(t * 2.6f));
                    y = 0.01f * Mathf.Abs(Mathf.Sin(t * 1.4f));
                    break;
                }
            case "puppy":
                {
                    yaw += 15f * Mathf.Sin(t * 0.8f);
                    float hop = Mathf.Repeat(t, 1.6f); y = hop < 0.45f ? Mathf.Sin(hop / 0.45f * Mathf.PI) * 0.035f : 0f;
                    var h = Find("head"); if (h) h.localRotation = Quaternion.Euler(0, 0, 14f * Mathf.Sin(t * 1.1f));
                    var tl = Find("tail"); if (tl) tl.localRotation = Quaternion.Euler(0, 0, 35f * Mathf.Sin(t * 14f));
                    break;
                }
            case "snowman":
                {
                    yaw += 18f * Mathf.Sin(t * 0.6f); rz = 4f * Mathf.Sin(t * 1.8f);
                    var h = Find("head"); if (h) h.localRotation = Quaternion.Euler(0, 10f * Mathf.Sin(t * 0.9f), -8f * Mathf.Sin(t * 1.8f + 0.6f));
                    break;
                }
            case "bloomp":
                {   // 포인세티아: 꽃잎이 오므렸다가 확 피어남 (7초마다)
                    yaw += t * 12f;
                    float c = Mathf.Repeat(t, 7f);
                    float open = c < 1.4f ? EaseOutBack(c / 1.4f) : c < 6.3f ? 1f : 1f - Mathf.SmoothStep(0, 1, (c - 6.3f) / 0.7f);
                    var bl = Find("bloom");
                    int i1 = 0, i2 = 0;
                    if (bl)
                        foreach (Transform ch in bl)
                        {
                            if (ch.name != "bract" && ch.name != "bract2") continue;
                            float ay = ch.name == "bract" ? (i1++) * 45f : (i2++) * 60f + 30f;
                            float tilt = Mathf.LerpUnclamped(ch.name == "bract" ? 78f : 85f, ch.name == "bract" ? 8f : 28f, open);
                            ch.localRotation = Quaternion.Euler(0, ay, tilt);
                            ch.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, open);
                        }
                    break;
                }
            case "rainbow":
                {   // 무지개: 왼쪽 구름에서 오른쪽 구름까지 쭉 생겨남 (6초마다)
                    yaw += 12f * Mathf.Sin(t * 0.5f); y = 0.01f * Mathf.Sin(t * 1.5f);
                    float c = Mathf.Repeat(t, 6f);
                    float p = c < 1.8f ? Mathf.SmoothStep(0, 1, c / 1.8f) : c < 5.4f ? 1f : 1f - (c - 5.4f) / 0.6f;
                    var a = Find("arcs"); if (a) a.GetComponent<MeshFilter>().sharedMesh = RainbowMesh(Mathf.RoundToInt(p * 40));
                    break;
                }
            case "inflate":
                {   // 하트 풍선: 하나씩 조금씩 부풀어 올라 커짐 (7초마다)
                    yaw += 10f * Mathf.Sin(t * 0.5f);
                    float c = Mathf.Repeat(t, 7f);
                    for (int i = 0; i < 3; i++)
                    {
                        var bb = Find("b" + i); if (!bb) continue;
                        float s = c < 6.4f ? Mathf.Lerp(0.15f, 1f, EaseOutBack(Mathf.Clamp01((c - i * 0.45f) / 1.6f))) : Mathf.Lerp(1f, 0.15f, (c - 6.4f) / 0.6f);
                        float wob = 1f + 0.03f * Mathf.Sin(t * 3f + i);
                        bb.localScale = new Vector3(s * wob, s / wob, s * wob);
                        var p0 = bb.localPosition; bb.localPosition = new Vector3(p0.x, (i == 0 ? 0.62f : i == 1 ? 0.52f : 0.5f) + 0.012f * Mathf.Sin(t * 1.7f + i * 1.3f), p0.z);
                    }
                    break;
                }
        }
    }
}
