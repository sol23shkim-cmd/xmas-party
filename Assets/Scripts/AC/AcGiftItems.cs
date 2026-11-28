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
                {
                    Color fur = new Color(1f, 0.82f, 0.6f), furD = new Color(0.96f, 0.66f, 0.4f), furW = new Color(1f, 0.96f, 0.92f);
                    System.Func<Vector3, Color> sh = u => Color.Lerp(fur, furW, Mathf.Clamp01(u.z * 0.8f - u.y * 0.3f));
                    var b = new AcPB();
                    b.Ellipsoid(new Vector3(0, 0.105f, 0), new Vector3(0.095f, 0.1f, 0.088f), fur, 18, 24, null, sh);
                    b.Ellipsoid(new Vector3(0, 0.095f, 0.058f), new Vector3(0.058f, 0.068f, 0.034f), furW, 12, 16);
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        b.Ellipsoid(new Vector3(sg * 0.048f, 0.02f, 0.045f), new Vector3(0.032f, 0.022f, 0.042f), furW, 10, 12);
                        b.Ellipsoid(new Vector3(sg * 0.04f, 0.075f, 0.078f), new Vector3(0.024f, 0.03f, 0.024f), furW, 10, 12);
                        for (int k = 0; k < 3; k++) b.Sphere(new Vector3(sg * 0.048f + (k - 1) * 0.011f, 0.012f, 0.083f), 0.006f, pink, 4, 5);   // 발바닥
                        b.Ellipsoid(new Vector3(sg * 0.07f, 0.16f, -0.02f), new Vector3(0.03f, 0.02f, 0.03f), furD, 8, 10);   // 줄무늬 무늬
                    }
                    Part("body", b, Vector3.zero);
                    // 리본 + 방울 (목)
                    var rb = new AcPB();
                    Color red = new Color(0.9f, 0.18f, 0.25f);
                    rb.Torus(new Vector3(0, 0.197f, 0.005f), Vector3.up, 0.072f, 0.01f, red, 24, 6);
                    foreach (float sg in new[] { -1f, 1f }) rb.Ellipsoid(new Vector3(sg * 0.03f, 0.205f, 0.075f), new Vector3(0.028f, 0.018f, 0.01f), red, 8, 10, Quaternion.Euler(0, 0, sg * 18f));
                    rb.Sphere(new Vector3(0, 0.205f, 0.08f), 0.011f, new Color(0.8f, 0.12f, 0.2f), 6, 8);
                    rb.Sphere(new Vector3(0, 0.183f, 0.083f), 0.014f, gold, 8, 10);
                    Gloss(Part("bow", rb, Vector3.zero), 0.6f);
                    // 머리 (기울이는 동작용으로 따로)
                    var head = Node("head", new Vector3(0, 0.2f, 0));
                    var hb = new AcPB();
                    hb.Ellipsoid(new Vector3(0, 0.08f, 0), new Vector3(0.125f, 0.105f, 0.11f), fur, 20, 28, null, sh);
                    hb.Ellipsoid(new Vector3(0, 0.05f, 0.08f), new Vector3(0.058f, 0.036f, 0.032f), furW, 12, 14);
                    hb.Ellipsoid(new Vector3(0, 0.155f, 0.06f), new Vector3(0.03f, 0.015f, 0.02f), furD, 6, 8);
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        hb.Cylinder(new Vector3(sg * 0.068f, 0.155f, -0.005f), new Vector3(sg * 0.098f, 0.225f, -0.01f), 0.044f, 0.005f, fur, 14);
                        hb.Cylinder(new Vector3(sg * 0.07f, 0.16f, 0.01f), new Vector3(sg * 0.094f, 0.212f, 0.004f), 0.028f, 0.003f, pink, 12);
                        hb.Ellipsoid(new Vector3(sg * 0.082f, 0.052f, 0.086f), new Vector3(0.022f, 0.012f, 0.008f), pink, 8, 10);   // 볼터치
                        for (int k = 0; k < 2; k++) hb.Tube(new[] { new Vector3(sg * 0.05f, 0.052f - k * 0.01f, 0.104f), new Vector3(sg * 0.13f, 0.062f - k * 0.022f, 0.085f) }, new[] { 0.0016f, 0.0012f }, new Color(0.55f, 0.45f, 0.4f), 4);   // 수염
                    }
                    Eyes(hb, 0.048f, 0.088f, 0.097f, 1f);
                    hb.Ellipsoid(new Vector3(0, 0.068f, 0.112f), new Vector3(0.011f, 0.008f, 0.007f), pink, 6, 8);
                    Smile(hb, new Vector3(0, 0.057f, 0.112f), 1f, new Color(0.45f, 0.3f, 0.3f));
                    Part("headmesh", hb, Vector3.zero, 0f, head);
                    // 꼬리 (살랑살랑)
                    var tail = Node("tail", new Vector3(0, 0.06f, -0.08f));
                    var tb = new AcPB();
                    var tp = new[] { Vector3.zero, new Vector3(0.01f, 0.05f, -0.035f), new Vector3(0.035f, 0.12f, -0.045f), new Vector3(0.05f, 0.165f, -0.02f) };
                    tb.Tube(tp, new[] { 0.02f, 0.019f, 0.017f, 0.015f }, fur, 12);
                    tb.Sphere(tp[3], 0.016f, furW, 8, 10);
                    Part("tailmesh", tb, Vector3.zero, 0f, tail);
                    return true;
                }
            case "p_puppy":
                {
                    Color fur = new Color(0.94f, 0.76f, 0.52f), furD = new Color(0.72f, 0.5f, 0.32f), furW = new Color(1f, 0.97f, 0.93f);
                    System.Func<Vector3, Color> sh = u => Color.Lerp(fur, furW, Mathf.Clamp01(u.z * 0.8f - u.y * 0.3f));
                    var b = new AcPB();
                    b.Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.092f, 0.095f, 0.09f), fur, 18, 24, null, sh);
                    b.Ellipsoid(new Vector3(0, 0.09f, 0.06f), new Vector3(0.056f, 0.066f, 0.034f), furW, 12, 16);
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        b.Ellipsoid(new Vector3(sg * 0.048f, 0.02f, 0.048f), new Vector3(0.033f, 0.023f, 0.044f), furW, 10, 12);
                        b.Ellipsoid(new Vector3(sg * 0.042f, 0.07f, 0.08f), new Vector3(0.025f, 0.03f, 0.025f), furW, 10, 12);
                    }
                    b.Ellipsoid(new Vector3(-0.05f, 0.13f, -0.04f), new Vector3(0.04f, 0.035f, 0.04f), furD, 8, 10);   // 등 무늬
                    Part("body", b, Vector3.zero);
                    var cb = new AcPB();
                    cb.Torus(new Vector3(0, 0.19f, 0.003f), Vector3.up, 0.07f, 0.011f, new Color(0.25f, 0.55f, 0.95f), 24, 6);
                    cb.Cylinder(new Vector3(0, 0.172f, 0.078f), new Vector3(0, 0.172f, 0.084f), 0.017f, 0.017f, gold, 14);
                    cb.Star(new Vector3(0, 0.172f, 0.086f), Vector3.forward, Vector3.up, 0.01f, 0.005f, 0.002f, new Color(1f, 0.95f, 0.7f));
                    Gloss(Part("collar", cb, Vector3.zero), 0.7f);
                    var head = Node("head", new Vector3(0, 0.195f, 0));
                    var hb = new AcPB();
                    hb.Ellipsoid(new Vector3(0, 0.08f, 0), new Vector3(0.12f, 0.105f, 0.108f), fur, 20, 28, null, sh);
                    hb.Ellipsoid(new Vector3(0, 0.048f, 0.082f), new Vector3(0.062f, 0.042f, 0.036f), furW, 12, 14);
                    hb.Ellipsoid(new Vector3(0.045f, 0.11f, 0.07f), new Vector3(0.04f, 0.035f, 0.03f), furD, 8, 10);   // 한쪽 눈 얼룩
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        hb.Ellipsoid(new Vector3(sg * 0.112f, 0.07f, -0.005f), new Vector3(0.034f, 0.075f, 0.026f), furD, 10, 12, Quaternion.Euler(0, 0, sg * 22f));   // 늘어진 귀
                        hb.Ellipsoid(new Vector3(sg * 0.08f, 0.045f, 0.085f), new Vector3(0.022f, 0.012f, 0.008f), pink, 8, 10);
                    }
                    Eyes(hb, 0.046f, 0.09f, 0.096f, 1f);
                    hb.Ellipsoid(new Vector3(0, 0.068f, 0.118f), new Vector3(0.021f, 0.016f, 0.014f), eyeC, 8, 10);   // 코
                    hb.Sphere(new Vector3(-0.006f, 0.074f, 0.13f), 0.005f, Color.white, 4, 5);
                    Smile(hb, new Vector3(0, 0.048f, 0.117f), 1f, new Color(0.35f, 0.22f, 0.2f));
                    hb.Ellipsoid(new Vector3(0, 0.033f, 0.112f), new Vector3(0.012f, 0.014f, 0.006f), new Color(1f, 0.5f, 0.55f), 6, 8);   // 혀
                    Part("headmesh", hb, Vector3.zero, 0f, head);
                    var tail = Node("tail", new Vector3(0, 0.1f, -0.085f));
                    var tb = new AcPB();
                    tb.Tube(new[] { Vector3.zero, new Vector3(0, 0.035f, -0.03f), new Vector3(0, 0.07f, -0.035f) }, new[] { 0.016f, 0.014f, 0.01f }, fur, 10);
                    tb.Sphere(new Vector3(0, 0.07f, -0.035f), 0.011f, furW, 6, 8);
                    Part("tailmesh", tb, Vector3.zero, 0f, tail);
                    return true;
                }
            case "p_snowman":
                {
                    Color snow = new Color(0.98f, 0.98f, 1f), snowS = new Color(0.85f, 0.89f, 0.97f), coal = new Color(0.12f, 0.11f, 0.13f);
                    System.Func<Vector3, Color> sh = u => Color.Lerp(snowS, snow, Mathf.Clamp01(0.55f + u.y * 0.5f + u.z * 0.2f));
                    var b = new AcPB();
                    b.Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.105f, 0.1f, 0.1f), snow, 20, 26, null, sh);
                    for (int k = 0; k < 3; k++) b.Sphere(new Vector3(0, 0.07f + k * 0.035f, 0.098f - Mathf.Abs(k - 1) * 0.006f), 0.01f, coal, 6, 8);   // 단추
                    foreach (float sg in new[] { -1f, 1f })
                    {   // 나뭇가지 팔 + 빨간 벙어리장갑
                        var a0 = new Vector3(sg * 0.09f, 0.14f, 0); var a1 = new Vector3(sg * 0.15f, 0.18f, 0.01f); var a2 = new Vector3(sg * 0.19f, 0.22f, 0.02f);
                        b.Tube(new[] { a0, a1, a2 }, new[] { 0.007f, 0.006f, 0.005f }, new Color(0.45f, 0.3f, 0.2f), 6);
                        b.Ellipsoid(a2 + new Vector3(sg * 0.01f, 0.012f, 0), new Vector3(0.018f, 0.022f, 0.014f), new Color(0.9f, 0.2f, 0.25f), 8, 10);
                    }
                    Part("body", b, Vector3.zero);
                    // 목도리
                    var sc = new AcPB(); Color scarf = new Color(0.92f, 0.22f, 0.3f), scarf2 = new Color(1f, 0.95f, 0.9f);
                    sc.Torus(new Vector3(0, 0.19f, 0), Vector3.up, 0.06f, 0.018f, scarf, 26, 8);
                    sc.Box(new Vector3(0.035f, 0.15f, 0.06f), new Vector3(0.03f, 0.07f, 0.012f), scarf, Quaternion.Euler(0, 0, 12f));
                    sc.Box(new Vector3(0.039f, 0.118f, 0.061f), new Vector3(0.031f, 0.008f, 0.013f), scarf2, Quaternion.Euler(0, 0, 12f));
                    Part("scarf", sc, Vector3.zero);
                    var head = Node("head", new Vector3(0, 0.2f, 0));
                    var hb = new AcPB();
                    hb.Ellipsoid(new Vector3(0, 0.07f, 0), new Vector3(0.078f, 0.074f, 0.075f), snow, 20, 26, null, sh);
                    Eyes(hb, 0.03f, 0.085f, 0.068f, 0.8f);
                    hb.Cylinder(new Vector3(0, 0.068f, 0.07f), new Vector3(0, 0.062f, 0.125f), 0.012f, 0.002f, new Color(1f, 0.55f, 0.15f), 10);   // 당근 코
                    foreach (float sg in new[] { -1f, 1f }) hb.Ellipsoid(new Vector3(sg * 0.05f, 0.055f, 0.062f), new Vector3(0.016f, 0.009f, 0.006f), pink, 6, 8);
                    for (int k = 0; k < 5; k++) { float a = Mathf.Lerp(-0.6f, 0.6f, k / 4f); hb.Sphere(new Vector3(Mathf.Sin(a) * 0.03f, 0.042f - Mathf.Cos(a) * 0.008f + 0.008f, 0.07f), 0.0045f, coal, 4, 5); }
                    // 털모자 (빨강 + 흰 테 + 방울)
                    hb.Ellipsoid(new Vector3(0, 0.122f, -0.004f), new Vector3(0.07f, 0.05f, 0.068f), new Color(0.2f, 0.55f, 0.85f), 12, 16);
                    hb.Torus(new Vector3(0, 0.112f, -0.004f), Vector3.up, 0.068f, 0.012f, Color.white, 24, 6);
                    hb.Sphere(new Vector3(0, 0.178f, -0.01f), 0.02f, Color.white, 8, 10);
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
