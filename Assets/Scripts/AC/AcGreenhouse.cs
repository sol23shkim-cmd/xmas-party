// AcGreenhouse.cs - 겨울 온실 화분 키우기 (11-26)
// 빈 화분 12개. 식물 5가지(포인세티아·아기 전나무·아마릴리스·크리스마스 선인장·호랑가시나무) 중 골라 심고 화분 색도 고름.
// 물은 준 뒤 2시간이 지나면 다시 줄 수 있음 (11-27): 주인이 주면 +4단계, 다른 사람이 주면 +1단계. 최대 100단계.
// (예전 기록은 4시간 칸 번호, 새 기록은 '분' 단위 시각을 칸 자리에 씀 → 한 번 줄 때마다 따로 셈)
// 물을 안 줘도 심은 뒤 8시간마다 저절로 +1단계 (11-27).
// 다 자라면 '다 자랐어요!' + 반짝임, 주인은 눈사람처럼 장식을 붙여 꾸밀 수 있음.
// 저장: orn/x~plant~p<번호> = {slot, owner, name, kind, pot, ct, d(장식 json)}
//       orn/x~pw~p<번호>~<심은 시각>~<4시간 칸 번호(예전) 또는 분 단위 시각(새)>~<물 준 사람> = {name, t}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable] public class PlantDeco { public string m = "", c = ""; public float x, y, z, nx, ny, nz, s = 1f; }
[Serializable] public class PlantDecoSet { public List<PlantDeco> d = new List<PlantDeco>(); }

public partial class AcRoom
{
    static readonly string[] PlantKo = { "포인세티아", "아기 전나무", "아마릴리스", "크리스마스 선인장", "호랑가시나무" };
    static readonly string[] PlantDesc = {
        "빨간 별 모양 잎이 피는 크리스마스 꽃",
        "자라면 꼭대기에 별이 뜨는 작은 트리",
        "키가 쑥쑥, 커다란 빨간 나팔꽃이 피어요",
        "마디마디 늘어지다 끝에 분홍 꽃이 펴요",
        "뾰족한 잎 사이로 빨간 열매가 열려요" };
    const int PlantMax = 100;
    const double WaterWinMs = 4 * 3600 * 1000.0;     // 예전 기록용
    const double WaterCoolMs = 2 * 3600 * 1000.0;    // 물 준 뒤 다시 줄 수 있을 때까지
    const double PassiveGrowMs = 8 * 3600 * 1000.0;   // 저절로 자라기: 8시간마다 1단계
    const float PotTop = 0.29f;
    // 온실 안 화분 자리 (방 가운데 기준, 미터). 가운데 돌길과 옆방 문 앞은 비워 둠
    static readonly Vector2[] SlotLocal = {
        new Vector2(-3.4f, -2.7f), new Vector2(-1.9f, -2.7f), new Vector2(1.9f, -2.7f), new Vector2(3.2f, -2.7f),
        new Vector2(-3.4f, -0.2f), new Vector2(-1.9f, -0.2f), new Vector2(1.9f, -0.2f), new Vector2(3.2f, -0.2f),
        new Vector2(-3.4f, 2.9f), new Vector2(-1.9f, 2.9f), new Vector2(1.9f, 2.9f), new Vector2(3.2f, 2.9f) };
    static readonly string[] PlantDecoIds = { "orn_ball", "orn_ball_glitter", "orn_star", "orn_heart", "orn_bell", "orn_snowflake", "orn_candycane", "orn_bow",
                                              "orn_light_bulb", "orn_bird", "orn_gift", "orn_pinecone", "orn_moon", "orn_mushroom", "sm_star", "sm_heart", "sm_flower", "sm_bowtie", "sm_holly" };
    static readonly Color PotDefault = new Color(0.82f, 0.45f, 0.3f);

    class PlantSlot
    {
        public int i; public Vector3 pos;
        public Transform root; public GameObject pot, plant, decoRoot;
        public MeshCollider col; public Mesh mesh;
        public bool planted; public int kind = -1, stage = -1; public string potHex = "", owner = "", name = "", decoJson = "", builtKey = "", decoBuilt = "";
        public double ct, myLastW; public bool iWatered; public readonly List<string> waterNames = new List<string>();
        public float pop, height = 0.3f, nextSparkle;
    }
    PlantSlot[] slots;
    Vector3 ghCenter; Transform ghRoot;
    float nextPlantPoll;

    bool InGreenhouse => phase == Phase.Play && me != null && !Outside && AreaAt(me.transform.position) == "겨울 온실";
    double NowMs => net != null && net.ServerNow > 0 ? net.ServerNow : AcSync.UnixMs;
    long CurWin => (long)Math.Floor(NowMs / WaterWinMs);
    int MyPlantCount => slots == null ? 0 : slots.Count(s => s.planted && s.owner == myKey);

    // ===== 온실 꾸미기 (집을 만들 때 FillGreenhouse에서 부름) =====
    void GreenFill(Vector3 c, Transform t)
    {
        ghCenter = c; ghRoot = t;
        slots = new PlantSlot[SlotLocal.Length];
        var saucer = SaucerMesh();
        for (int i = 0; i < SlotLocal.Length; i++)
        {
            var s = new PlantSlot { i = i, pos = c + new Vector3(SlotLocal[i].x, 0, SlotLocal[i].y) };
            var r = new GameObject("Pot" + i).transform; r.SetParent(t, false); r.position = s.pos;
            r.rotation = Quaternion.Euler(0, i * 37f, 0);
            s.root = r;
            ProcObj("saucer", saucer, r);
            s.pot = ProcObj("pot", PotMesh(PotDefault), r);
            slots[i] = s;
        }
    }

    // ===== 화분 그림 =====
    static Mesh SaucerMesh()
    {
        var b = new AcPB();
        b.Cylinder(Vector3.zero, new Vector3(0, 0.035f, 0), 0.25f, 0.28f, new Color(0.72f, 0.42f, 0.3f), 18);
        return b.M("saucer");
    }
    static readonly Dictionary<string, Mesh> potMeshes = new Dictionary<string, Mesh>();
    static Mesh PotMesh(Color col)
    {
        string key = ColorUtility.ToHtmlStringRGB(col);
        if (potMeshes.TryGetValue(key, out var m)) return m;
        var b = new AcPB();
        b.Tube(new[] { new Vector3(0, 0.03f, 0), new Vector3(0, 0.15f, 0), new Vector3(0, 0.27f, 0) }, new[] { 0.165f, 0.195f, 0.225f }, col, 18, new[] { col * 0.85f, col, col * 1.05f }, true, false);
        b.Torus(new Vector3(0, 0.275f, 0), Vector3.up, 0.228f, 0.028f, Color.Lerp(col, Color.white, 0.25f), 24, 6);
        b.Disk(new Vector3(0, 0.272f, 0), Vector3.up, 0.215f, new Color(0.33f, 0.22f, 0.15f), 18, false);
        // 흙 알갱이
        var rnd = new System.Random(7);
        for (int i = 0; i < 9; i++) { float a = (float)rnd.NextDouble() * 6.28f, r = 0.05f + (float)rnd.NextDouble() * 0.13f; b.Sphere(new Vector3(Mathf.Cos(a) * r, 0.274f, Mathf.Sin(a) * r), 0.012f, new Color(0.42f, 0.29f, 0.2f), 3, 5); }
        m = b.M("pot_" + key);
        potMeshes[key] = m;
        return m;
    }

    // ===== 식물 모양 (단계마다 다시 만듦. 같은 화분은 같은 씨앗 → 자라는 모습이 이어짐) =====
    static void Basis(Vector3 dir, out Vector3 s1, out Vector3 s2)
    {
        dir.Normalize();
        s1 = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        s2 = Vector3.Cross(dir, s1).normalized;
    }

    static Mesh PlantMesh(int kind, int stage, int seed)
    {
        var b = new AcPB();
        var rnd = new System.Random(seed * 31 + kind * 977 + 5);
        float R() => (float)rnd.NextDouble();
        float g = Mathf.Clamp01(stage / (float)PlantMax);
        float S = 0.2f + 0.8f * Mathf.Pow(g, 0.6f);
        var up = Vector3.up;
        if (stage <= 0)
        {   // 막 심은 씨앗: 흙 둔덕 + 작은 떡잎
            b.Ellipsoid(new Vector3(0, 0.0f, 0), new Vector3(0.06f, 0.02f, 0.06f), new Color(0.4f, 0.28f, 0.2f), 4, 8);
            b.Cylinder(Vector3.zero, new Vector3(0, 0.04f, 0), 0.004f, 0.003f, new Color(0.45f, 0.7f, 0.3f), 5);
            b.Leaf(new Vector3(0, 0.04f, 0), new Vector3(1, 0.5f, 0), Vector3.forward, 0.03f, 0.022f, new Color(0.45f, 0.78f, 0.35f), new Color(0.6f, 0.85f, 0.4f), 0.2f);
            b.Leaf(new Vector3(0, 0.04f, 0), new Vector3(-1, 0.5f, 0), Vector3.forward, 0.03f, 0.022f, new Color(0.45f, 0.78f, 0.35f), new Color(0.6f, 0.85f, 0.4f), 0.2f);
            return b.M("plant");
        }
        switch (kind)
        {
            case 0: // 포인세티아
                {
                    const int MS = 7;
                    var ang = new float[MS]; var tilt = new float[MS]; var lk = new float[MS];
                    for (int i = 0; i < MS; i++) { ang[i] = i * 2.4f + R() * 0.4f; tilt[i] = i == 0 ? 0.12f : 0.45f + R() * 0.45f; lk[i] = 0.8f + R() * 0.35f; }
                    int nS = 1 + Mathf.RoundToInt(6 * g);
                    float redK = Mathf.Clamp01((g - 0.28f) / 0.4f);
                    Color stemC = new Color(0.36f, 0.55f, 0.26f), leafG = new Color(0.16f, 0.45f, 0.2f), red = new Color(0.86f, 0.07f, 0.12f);
                    for (int i = 0; i < nS; i++)
                    {
                        float L = (0.14f + 0.34f * S) * lk[i];
                        var dir = new Vector3(Mathf.Cos(ang[i]) * Mathf.Sin(tilt[i]), Mathf.Cos(tilt[i]), Mathf.Sin(ang[i]) * Mathf.Sin(tilt[i])).normalized;
                        var tip = dir * L + up * (L * 0.15f);
                        b.Tube(new[] { Vector3.zero, dir * L * 0.5f + up * L * 0.1f, tip }, new[] { 0.011f, 0.009f, 0.007f }, stemC, 6);
                        var outH = new Vector3(dir.x, 0, dir.z); if (outH.sqrMagnitude < 0.01f) outH = new Vector3(Mathf.Cos(ang[i]), 0, Mathf.Sin(ang[i])); outH.Normalize();
                        if (g > 0.06f)
                            for (int k = 0; k < 2; k++)
                            {
                                var bp = dir * L * (0.4f + 0.25f * k) + up * L * 0.06f;
                                var side = Vector3.Cross(up, outH);
                                var ld = (outH * 0.9f + side * (k == 0 ? 0.5f : -0.5f) + up * 0.15f).normalized;
                                b.Leaf(bp, ld, Vector3.Cross(up, ld), 0.06f + 0.1f * S, 0.035f + 0.05f * S, leafG, leafG * 1.15f, 0.08f, 1, 5, 0.25f);
                            }
                        // 끝의 별 모양 포엽
                        int nb = 5 + i % 3;
                        float bl = (0.04f + 0.12f * S) * (0.6f + 0.4f * redK);
                        var colB = Color.Lerp(leafG, red, redK); var colT = Color.Lerp(leafG * 1.1f, red * 1.08f, redK);
                        Basis(dir, out var s1, out var s2);
                        for (int j = 0; j < nb; j++)
                        {
                            float a = j * Mathf.PI * 2 / nb + i;
                            var radial = (s1 * Mathf.Cos(a) + s2 * Mathf.Sin(a));
                            var bd = (radial + dir * 0.3f).normalized;
                            b.Leaf(tip, bd, Vector3.Cross(dir, bd), bl, bl * 0.5f, colB, colT, 0.1f, 1, 5, 0.1f);
                        }
                        if (g > 0.5f)
                            for (int j = 0; j < nb; j++)
                            {
                                float a = (j + 0.5f) * Mathf.PI * 2 / nb + i;
                                var radial = (s1 * Mathf.Cos(a) + s2 * Mathf.Sin(a));
                                var bd = (radial + dir * 0.7f).normalized;
                                b.Leaf(tip + dir * 0.004f, bd, Vector3.Cross(dir, bd), bl * 0.6f, bl * 0.32f, colB * 1.05f, colT, 0.12f, 1, 4, 0f);
                            }
                        if (g > 0.55f) for (int k = 0; k < 3; k++) b.Sphere(tip + dir * 0.012f + (s1 * Mathf.Cos(k * 2.1f) + s2 * Mathf.Sin(k * 2.1f)) * 0.008f, 0.0065f, new Color(0.98f, 0.82f, 0.2f), 3, 5);
                    }
                    break;
                }
            case 1: // 아기 전나무
                {
                    float H = 0.1f + 1.05f * S * S;
                    var trunk = new Color(0.45f, 0.3f, 0.2f);
                    b.Cylinder(Vector3.zero, new Vector3(0, H * 0.25f, 0), 0.012f + 0.02f * S, 0.008f + 0.012f * S, trunk, 8);
                    int tiers = Mathf.Clamp(1 + Mathf.FloorToInt(5.99f * g), 1, 6);
                    var tw = new float[6]; for (int i = 0; i < 6; i++) tw[i] = R();
                    for (int t = 0; t < tiers; t++)
                    {
                        float f = tiers == 1 ? 0f : (float)t / tiers;
                        float y0 = H * (0.15f + 0.68f * f);
                        float r = (0.08f + 0.36f * S) * (1f - f * 0.82f);
                        float hT = H * (tiers == 1 ? 0.85f : 0.34f);
                        var g1 = Color.Lerp(new Color(0.12f, 0.42f, 0.24f), new Color(0.2f, 0.55f, 0.3f), tw[t]);
                        b.Tube(new[] { new Vector3(0, y0, 0), new Vector3(0, y0 + hT * 0.1f, 0), new Vector3(0, y0 + hT, 0) }, new[] { r * 0.9f, r, 0.002f }, g1, 14,
                               new[] { g1 * 0.85f, g1, g1 * 1.15f }, true, false);
                        // 아래 가장자리에 눈 조금
                        if (g > 0.3f) for (int k = 0; k < 6; k++) { float a = k * 1.05f + t; b.Sphere(new Vector3(Mathf.Cos(a) * r * 0.85f, y0 + hT * 0.12f, Mathf.Sin(a) * r * 0.85f), 0.012f + 0.01f * S, new Color(0.97f, 0.98f, 1f), 3, 5); }
                    }
                    if (stage >= PlantMax) b.Star(new Vector3(0, H + 0.07f, 0), Vector3.forward, Vector3.up, 0.08f, 0.035f, 0.02f, new Color(1f, 0.82f, 0.2f));
                    break;
                }
            case 2: // 아마릴리스
                {
                    float bs = 0.6f + 0.4f * S;
                    b.Ellipsoid(new Vector3(0, 0.03f * bs, 0), new Vector3(0.07f, 0.06f, 0.07f) * bs, new Color(0.78f, 0.62f, 0.46f), 6, 10);
                    const int ML = 6; var la = new float[ML]; var lk = new float[ML];
                    for (int i = 0; i < ML; i++) { la[i] = i * 2.6f + R() * 0.5f; lk[i] = 0.75f + R() * 0.4f; }
                    int nL = 2 + Mathf.RoundToInt(4 * g);
                    Color lg = new Color(0.2f, 0.5f, 0.22f);
                    for (int i = 0; i < nL; i++)
                    {
                        var d = new Vector3(Mathf.Cos(la[i]) * 0.3f, 1f, Mathf.Sin(la[i]) * 0.3f);
                        var side = Vector3.Cross(d, new Vector3(Mathf.Cos(la[i]), 0, Mathf.Sin(la[i]))).normalized;
                        b.Leaf(new Vector3(0, 0.07f * bs, 0), d, side, (0.12f + 0.42f * S) * lk[i], 0.03f + 0.03f * S, lg, lg * 1.2f, 0.05f, 2, 7, 0.55f);
                    }
                    if (g > 0.22f)
                    {
                        float sh = (g - 0.22f) / 0.78f;
                        float Hs = 0.12f + 0.72f * Mathf.Pow(sh, 0.8f);
                        var top = new Vector3(0.01f, Hs, 0.005f);
                        b.Cylinder(new Vector3(0, 0.05f, 0), top, 0.016f, 0.012f, new Color(0.35f, 0.6f, 0.3f), 8);
                        if (g < 0.55f)
                        {   // 꽃봉오리
                            float bk = Mathf.Clamp01((g - 0.3f) / 0.25f);
                            b.Ellipsoid(top + up * 0.05f, new Vector3(0.025f, 0.06f, 0.025f) * (0.6f + 0.6f * bk), Color.Lerp(new Color(0.35f, 0.6f, 0.3f), new Color(0.75f, 0.15f, 0.15f), bk), 6, 8);
                        }
                        else
                        {
                            int nF = stage >= PlantMax ? 4 : Mathf.Clamp(1 + Mathf.FloorToInt((g - 0.55f) / 0.45f * 3.99f), 1, 4);
                            float open = Mathf.Clamp01((g - 0.55f) / 0.35f);
                            Color red = new Color(0.86f, 0.1f, 0.12f), tipC = new Color(0.98f, 0.35f, 0.35f);
                            for (int f = 0; f < nF; f++)
                            {
                                float a = f * Mathf.PI * 0.5f + 0.6f;
                                var fd = new Vector3(Mathf.Cos(a), 0.45f, Mathf.Sin(a)).normalized;
                                var fb = top + fd * 0.03f;
                                b.Cylinder(top, fb, 0.008f, 0.012f, new Color(0.4f, 0.62f, 0.3f), 6);
                                Basis(fd, out var s1, out var s2);
                                float spread = 0.35f + 0.85f * open;
                                for (int p = 0; p < 6; p++)
                                {
                                    float pa = p * Mathf.PI / 3f + (p % 2) * 0.1f;
                                    var pd = (fd + (s1 * Mathf.Cos(pa) + s2 * Mathf.Sin(pa)) * spread).normalized;
                                    b.Leaf(fb, pd, Vector3.Cross(fd, pd), 0.06f + 0.09f * open, 0.05f + 0.03f * open, red, tipC, -0.18f * open, 3, 5);
                                }
                                for (int k = 0; k < 4; k++) { var sp = fb + fd * (0.06f + 0.03f * open) + (s1 * Mathf.Cos(k * 1.6f) + s2 * Mathf.Sin(k * 1.6f)) * 0.012f; b.Sphere(sp, 0.006f, new Color(1f, 0.92f, 0.5f), 3, 4); }
                            }
                        }
                    }
                    break;
                }
            case 3: // 크리스마스 선인장
                {
                    const int MC = 6;
                    var ca = new float[MC]; var cr = new float[MC * 6];
                    for (int i = 0; i < MC; i++) ca[i] = i * Mathf.PI * 2 / MC + R() * 0.5f;
                    for (int i = 0; i < cr.Length; i++) cr[i] = R() - 0.5f;
                    int nC = 3 + Mathf.RoundToInt(3 * g);
                    int nSeg = 1 + Mathf.RoundToInt(5 * g);
                    float sk = 0.75f + 0.25f * S;
                    var rad = new Vector3(0.034f, 0.05f, 0.012f) * sk;
                    Color gr = new Color(0.25f, 0.55f, 0.28f), pink = new Color(0.95f, 0.3f, 0.55f), pinkT = new Color(1f, 0.6f, 0.75f);
                    for (int c = 0; c < nC; c++)
                    {
                        var outH = new Vector3(Mathf.Cos(ca[c]), 0, Mathf.Sin(ca[c]));
                        var tang = new Vector3(-outH.z, 0, outH.x);
                        var dir = (outH * 0.45f + up * 0.9f).normalized;
                        var pos = new Vector3(0, 0.005f, 0);
                        for (int s = 0; s < nSeg; s++)
                        {
                            var center = pos + dir * rad.y;
                            var flatN = Vector3.Cross(dir, tang).normalized;
                            b.Ellipsoid(center, rad, Color.Lerp(gr, gr * 1.2f, s / 6f), 5, 10, Quaternion.LookRotation(flatN, dir));
                            pos = center + dir * rad.y * 0.92f;
                            dir = (dir + outH * 0.22f - up * 0.28f * (s + 1) / 6f + tang * cr[c * 6 + s] * 0.3f).normalized;
                        }
                        if (g > 0.55f && (c % 2 == 0 || g > 0.8f))
                        {
                            float open = Mathf.Clamp01((g - 0.55f) / 0.35f);
                            var fb = pos;
                            var fd = (dir + up * 0.2f).normalized;
                            b.Cylinder(fb, fb + fd * 0.035f, 0.007f, 0.012f, pink, 6);
                            Basis(fd, out var s1, out var s2);
                            for (int p = 0; p < 6; p++)
                            {
                                float pa = p * Mathf.PI / 3f;
                                var pd = (fd * 0.6f + (s1 * Mathf.Cos(pa) + s2 * Mathf.Sin(pa)) * (0.3f + 0.6f * open)).normalized;
                                b.Leaf(fb + fd * 0.03f, pd, Vector3.Cross(fd, pd), 0.03f + 0.025f * open, 0.016f, pink, pinkT, 0.35f * open, 1, 4);
                            }
                        }
                    }
                    break;
                }
            default: // 호랑가시나무
                {
                    float Rb = 0.1f + 0.3f * S;
                    var c0 = new Vector3(0, Rb * 0.95f, 0);
                    b.Cylinder(Vector3.zero, c0 * 0.6f, 0.015f + 0.01f * S, 0.01f, new Color(0.4f, 0.3f, 0.22f), 6);
                    const int ML = 50, MB2 = 18;
                    var ld = new Vector3[ML]; var lr = new float[ML];
                    for (int i = 0; i < ML; i++) { var d = new Vector3(R() - 0.5f, R() * 0.9f - 0.25f, R() - 0.5f); if (d.sqrMagnitude < 0.01f) d = Vector3.up; ld[i] = d.normalized; lr[i] = 0.6f + R() * 0.4f; }
                    var bd = new Vector3[MB2]; for (int i = 0; i < MB2; i++) { var d = new Vector3(R() - 0.5f, R() * 0.8f - 0.1f, R() - 0.5f); if (d.sqrMagnitude < 0.01f) d = Vector3.up; bd[i] = d.normalized; }
                    int nL = 6 + Mathf.RoundToInt(44 * g);
                    Color dg = new Color(0.08f, 0.36f, 0.16f), lgC = new Color(0.2f, 0.5f, 0.25f);
                    for (int i = 0; i < nL; i++)
                    {
                        var bp = c0 + ld[i] * Rb * 0.7f * lr[i];
                        var dd = (ld[i] + up * 0.25f).normalized;
                        var side = Vector3.Cross(dd, up); if (side.sqrMagnitude < 1e-3f) side = Vector3.right;
                        b.Leaf(bp, dd, side.normalized, 0.05f + 0.06f * S, 0.035f + 0.035f * S, dg, lgC, 0.18f, 4, 6);
                    }
                    if (g > 0.45f)
                    {
                        int nB = Mathf.RoundToInt(MB2 * (g - 0.45f) / 0.55f);
                        for (int i = 0; i < nB; i++)
                        {
                            var bp = c0 + bd[i] * Rb * 0.92f;
                            Basis(bd[i], out var s1, out var s2);
                            for (int k = 0; k < 3; k++) b.Sphere(bp + (s1 * Mathf.Cos(k * 2.1f) + s2 * Mathf.Sin(k * 2.1f)) * 0.014f, 0.013f, new Color(0.88f, 0.08f, 0.1f), 4, 6);
                        }
                    }
                    break;
                }
        }
        return b.M("plant");
    }

    // ===== 서버에서 읽기 =====
    void PollPlants()
    {
        if (slots == null || net == null || Time.unscaledTime < nextPlantPoll) return;
        nextPlantPoll = Time.unscaledTime + 0.5f;
        var node = net.Get("plants"); var pw = net.Get("pw");
        long win = CurWin; _ = win;
        foreach (var s in slots)
        {
            Dictionary<string, object> d = null;
            if (node != null && node.TryGetValue("p" + s.i, out var v)) d = v as Dictionary<string, object>;
            if (d == null)
            {
                if (s.planted) { ClearPlant(s); }
                continue;
            }
            s.planted = true;
            s.owner = AcJson.S(d, "owner"); s.name = AcJson.S(d, "name");
            int kind = Mathf.Clamp((int)AcJson.D(d, "kind"), 0, PlantKo.Length - 1);
            string pot = AcJson.S(d, "pot"); double ct = AcJson.D(d, "ct");
            s.decoJson = AcJson.S(d, "d");
            // 물 준 기록으로 단계 계산
            string pre = "p" + s.i + "~" + ((long)ct).ToString() + "~";
            var byWin = new Dictionary<long, (bool owner, int others)>();
            s.waterNames.Clear(); s.iWatered = false; s.myLastW = 0; int perWater = 0;
            if (pw != null)
                foreach (var kv in pw)
                {
                    if (!kv.Key.StartsWith(pre, StringComparison.Ordinal)) continue;
                    var parts = kv.Key.Substring(pre.Length).Split('~');
                    if (parts.Length < 2 || !long.TryParse(parts[0], out long w)) continue;
                    string who = parts[1];
                    var wd = kv.Value as Dictionary<string, object>;
                    bool isNew = w > 1000000;   // 새 기록: 분 단위 시각
                    double wt = isNew ? w * 60000.0 : w * WaterWinMs;
                    if (wd != null && AcJson.D(wd, "t") > 0) wt = AcJson.D(wd, "t");
                    if (isNew) perWater += who == s.owner ? 4 : 1;
                    else
                    {
                        byWin.TryGetValue(w, out var cur);
                        if (who == s.owner) cur.owner = true; else cur.others++;
                        byWin[w] = cur;
                    }
                    if (who == myKey && wt > s.myLastW) s.myLastW = wt;
                    if (NowMs - wt < WaterCoolMs)
                    {
                        string nm = wd != null ? AcJson.S(wd, "name") : who; nm = nm != "" ? nm : who;
                        if (!s.waterNames.Contains(nm)) s.waterNames.Add(nm);
                    }
                }
            s.iWatered = s.myLastW > 0 && NowMs - s.myLastW < WaterCoolMs;
            int stage = perWater;
            foreach (var kv in byWin) stage += (kv.Value.owner ? 4 : 0) + kv.Value.others;
            if (ct > 0 && NowMs > ct) stage += (int)Math.Floor((NowMs - ct) / PassiveGrowMs);   // 물을 안 줘도 심은 뒤 8시간마다 +1
            stage = Mathf.Clamp(stage, 0, PlantMax);
            if (s.stage >= 0 && stage > s.stage && s.kind == kind && Math.Abs(s.ct - ct) < 1) { s.pop = 1f; FxBurst("star", s.pos + Vector3.up * (PotTop + s.height * 0.6f), 10, 1.6f, 0.05f, FxGold, 1.0f, 1f); Sfx("grow", 0.55f); }
            string key = kind + "|" + stage + "|" + pot + "|" + ((long)ct);
            s.kind = kind; s.stage = stage; s.ct = ct; s.potHex = pot;
            if (key != s.builtKey) { s.builtKey = key; BuildPlant(s); }
            if (s.decoJson != s.decoBuilt) { s.decoBuilt = s.decoJson; BuildPlantDeco(s, ParseDeco(s.decoJson)); }
        }
        // 들어와서 처음 한 번: 물 줄 수 있는 내 화분 알림 (11-28)
        if (!plantHintDone && phase == Phase.Play && node != null && me != null && !string.IsNullOrEmpty(myKey) && Time.unscaledTime > plantHintAt)
        {
            plantHintDone = true;
            int mine = 0; foreach (var s in slots) if (s.planted && s.owner == myKey && !s.iWatered && s.stage < PlantMax) mine++;
            if (mine > 0) Toast($"겨울 온실: 물 줄 수 있는 내 화분이 {mine}개 있어요!");
        }
    }
    bool plantHintDone; float plantHintAt = 6f;

    static PlantDecoSet ParseDeco(string j)
    {
        if (string.IsNullOrEmpty(j)) return new PlantDecoSet();
        try { return JsonUtility.FromJson<PlantDecoSet>(j) ?? new PlantDecoSet(); } catch { return new PlantDecoSet(); }
    }

    void ClearPlant(PlantSlot s)
    {
        s.planted = false; s.kind = -1; s.stage = -1; s.owner = ""; s.name = ""; s.builtKey = ""; s.decoBuilt = ""; s.decoJson = "";
        if (s.plant) Destroy(s.plant); s.plant = null; s.col = null;
        if (s.mesh) Destroy(s.mesh); s.mesh = null;
        if (s.decoRoot) Destroy(s.decoRoot); s.decoRoot = null;
        s.pot.GetComponent<MeshFilter>().sharedMesh = PotMesh(PotDefault);
        if (plantFocus == s.i) plantFocus = -1;
    }

    void BuildPlant(PlantSlot s)
    {
        var potCol = s.potHex != "" ? Hex(s.potHex) : PotDefault;
        s.pot.GetComponent<MeshFilter>().sharedMesh = PotMesh(potCol);
        if (s.mesh) Destroy(s.mesh);
        s.mesh = PlantMesh(s.kind, s.stage, s.i * 13 + (int)(s.ct % 9973));
        if (!s.plant)
        {
            s.plant = ProcObj("plant", s.mesh, s.root);
            s.plant.transform.localPosition = new Vector3(0, PotTop, 0);
            s.col = s.plant.AddComponent<MeshCollider>();
        }
        s.plant.GetComponent<MeshFilter>().sharedMesh = s.mesh;
        try { s.col.sharedMesh = null; s.col.sharedMesh = s.mesh; } catch { }
        s.height = Mathf.Max(0.1f, s.mesh.bounds.max.y);
    }

    void BuildPlantDeco(PlantSlot s, PlantDecoSet set)
    {
        if (s.decoRoot) Destroy(s.decoRoot);
        if (!s.plant || set == null || set.d.Count == 0) return;
        s.decoRoot = new GameObject("deco");
        s.decoRoot.transform.SetParent(s.plant.transform, false);
        foreach (var d in set.d) PlacePlantDeco(s.decoRoot.transform, d);
    }

    GameObject PlacePlantDeco(Transform parent, PlantDeco d)
    {
        if (!AcmLoader.Exists(d.m)) return null;
        Dictionary<string, Color> tints = null;
        var it = AcCatalog.Get(d.m);
        if (!string.IsNullOrEmpty(d.c) && it != null && it.groups != null && it.groups.Length > 0) tints = new Dictionary<string, Color> { { it.groups[0], Hex(d.c) } };
        var g = AcCatalog.Spawn(d.m, parent, tints);
        var p = new Vector3(d.x, d.y, d.z); var n = new Vector3(d.nx, d.ny, d.nz); if (n.sqrMagnitude < 1e-4f) n = Vector3.forward; n.Normalize();
        bool orn = d.m.StartsWith("orn_", StringComparison.Ordinal);
        if (orn)
        {   // 트리 장식처럼 똑바로 매달림, 바깥을 봄
            var h = new Vector3(n.x, 0, n.z); if (h.sqrMagnitude < 1e-3f) h = Vector3.forward;
            g.transform.localRotation = Quaternion.LookRotation(h.normalized, Vector3.up);
            g.transform.localScale = Vector3.one * 0.32f * d.s;
            g.transform.localPosition = p + n * 0.01f;
        }
        else
        {
            var upv = Vector3.up - n * Vector3.Dot(Vector3.up, n); if (upv.sqrMagnitude < 1e-4f) upv = Vector3.forward;
            g.transform.localRotation = Quaternion.LookRotation(n, upv.normalized);
            g.transform.localScale = Vector3.one * 0.75f * d.s;
            g.transform.localPosition = p - n * 0.003f;
        }
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    void PutPlant(PlantSlot s, int kind, string pot, double ct, string deco, string owner, string name)
    {
        if (net == null) return;
        string json = "{\"slot\":" + s.i + ",\"owner\":" + AcJson.Esc(owner) + ",\"name\":" + AcJson.Esc(name) + ",\"kind\":" + kind +
                      ",\"pot\":" + AcJson.Esc(pot) + ",\"ct\":" + ((long)ct).ToString() + ",\"d\":" + AcJson.Esc(deco ?? "") + "}";
        net.Put("plants/p" + s.i, json);
        nextPlantPoll = 0;
    }

    // ===== 매 프레임 =====
    int plantFocus = -1, plantPicker = -1; float plantYaw; bool plantDeco, plantDelAsk;
    int pickKind; int pickPot = 0;
    static readonly Color[] PotCols = { new Color(0.82f, 0.45f, 0.3f), new Color(0.95f, 0.95f, 0.93f), new Color(0.85f, 0.2f, 0.22f), new Color(0.2f, 0.5f, 0.3f),
                                        new Color(0.35f, 0.6f, 0.9f), new Color(0.98f, 0.8f, 0.3f), new Color(0.95f, 0.6f, 0.75f), new Color(0.6f, 0.45f, 0.8f), new Color(0.25f, 0.27f, 0.32f) };
    static readonly string[] PotColKo = { "토분", "하양", "빨강", "초록", "하늘", "노랑", "분홍", "보라", "검정" };
    int waterSlot = -1; float waterT; GameObject waterCan;
    PlantDecoSet plantDraft; int plantDecoSel, plantColSel, plantSizeSel = 1; bool plantPicked = true;
    readonly IconStripState plantStrip = new IconStripState();
    bool GreenBusy => plantFocus >= 0 || plantPicker >= 0;

    void GreenUpdate(float dt)
    {
        if (slots == null) return;
        if (phase == Phase.Play) PollPlants();
        ProcessPlantIcons();
        // 자라는 순간 통 튀는 모습 + 다 자란 식물 반짝임
        foreach (var s in slots)
        {
            if (!s.plant) continue;
            if (s.pop > 0) { s.pop = Mathf.Max(0, s.pop - dt * 1.6f); float k = 1 + Mathf.Sin(s.pop * Mathf.PI * 3) * 0.12f * s.pop; s.plant.transform.localScale = new Vector3(1 / Mathf.Sqrt(k), k, 1 / Mathf.Sqrt(k)); }
            else s.plant.transform.localScale = Vector3.one;
            if (s.stage >= PlantMax && Time.time > s.nextSparkle && me != null && (s.pos - me.transform.position).sqrMagnitude < 150f && s.root.gameObject.activeInHierarchy)
            {
                s.nextSparkle = Time.time + UnityEngine.Random.Range(0.8f, 1.6f);
                var p = s.pos + Vector3.up * (PotTop + s.height * UnityEngine.Random.Range(0.3f, 1f)) + UnityEngine.Random.insideUnitSphere * 0.25f;
                FxBurst("star", p, 1, 0.3f, 0.045f, FxGold, 1.2f, -0.2f);
            }
        }
        UpdateWatering(dt);
        if (plantPicker >= 0)
        {
            me.speed = 0;
            if (Key("esc")) ClosePicker(false);
        }
        if (plantFocus >= 0) PlantFocusUpdate();
    }

    // ===== 심기 =====
    void OpenPlantPicker(int i)
    {
        if (MyPlantCount >= 2) { Toast("화분은 한 사람이 2개까지 키울 수 있어요"); return; }
        plantPicker = i; pickKind = 0; plantYaw = YawToward(slots[i].pos) ; decoZoom = 1f; walkTarget = null; followId = null;
    }
    float YawToward(Vector3 p) { var d = p - cam.transform.position; d.y = 0; return d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 180f; }

    void ClosePicker(bool plant)
    {
        if (plantPicker < 0) return;
        var s = slots[plantPicker];
        if (plant && !s.planted && net != null)
        {
            double ct = Math.Floor(NowMs);
            PutPlant(s, pickKind, HexOf(PotCols[Wrap(pickPot, PotCols.Length)]), ct, "", myKey, uiName.Trim());
            s.stage = 0; s.kind = pickKind; s.ct = ct;
            Emote("cheer");
            Toast(PlantKo[pickKind] + "를 심었어요! 물을 주면 쑥쑥 자라요");
            Sfx("candy_collect", 0.5f, 1.1f);
            FxBurst("dot", s.pos + Vector3.up * PotTop, 12, 1.2f, 0.03f, new[] { new Color(0.45f, 0.32f, 0.22f), new Color(0.55f, 0.4f, 0.28f) }, 0.7f, 6f);
        }
        else if (!s.planted) s.pot.GetComponent<MeshFilter>().sharedMesh = PotMesh(PotDefault);
        plantPicker = -1;
    }

    // ===== 물 주기 =====
    void StartWatering(int i)
    {
        var s = slots[i];
        if (!s.planted || s.iWatered || waterSlot >= 0) return;
        waterSlot = i; waterT = 0;
        if (!waterCan) waterCan = ProcObj("wateringCan", CanMesh(), null);
        waterCan.SetActive(true);
        FaceTo(s.pos);
        Sfx("water", 0.55f);
    }

    static Mesh CanMesh()
    {
        var b = new AcPB();
        Color body = new Color(0.35f, 0.7f, 0.55f), dark = new Color(0.25f, 0.5f, 0.4f);
        b.Cylinder(Vector3.zero, new Vector3(0, 0.16f, 0), 0.08f, 0.075f, body, 14);
        b.Torus(new Vector3(0, 0.16f, 0), Vector3.up, 0.072f, 0.01f, dark, 16, 5);
        b.Tube(new[] { new Vector3(0.06f, 0.04f, 0), new Vector3(0.14f, 0.1f, 0), new Vector3(0.22f, 0.19f, 0) }, new[] { 0.016f, 0.012f, 0.01f }, body, 6);
        b.Cylinder(new Vector3(0.21f, 0.18f, 0), new Vector3(0.24f, 0.21f, 0), 0.014f, 0.026f, dark, 8);
        b.Torus(new Vector3(-0.07f, 0.12f, 0), Vector3.forward, 0.05f, 0.011f, dark, 14, 5);
        return b.M("can");
    }

    void UpdateWatering(float dt)
    {
        if (waterSlot < 0) { if (waterCan && waterCan.activeSelf) waterCan.SetActive(false); return; }
        var s = slots[waterSlot];
        waterT += dt;
        // 화분 위쪽(내 쪽)에서 기울여 붓기
        var toMe = me.transform.position - s.pos; toMe.y = 0; if (toMe.sqrMagnitude < 0.01f) toMe = Vector3.back; toMe.Normalize();
        float tilt = Mathf.SmoothStep(0, 1, Mathf.Clamp01(waterT / 0.35f)) * (waterT > 1.45f ? Mathf.Clamp01((1.75f - waterT) / 0.3f) : 1f);
        // 캐릭터가 오른손으로 물뿌리개 손잡이를 쥐고 앞으로 기울여 부음
        me.pour = tilt;
        FaceTo(s.pos);
        var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, -toMe), Vector3.up) * Quaternion.Euler(0, 90, 0) * Quaternion.Euler(0, 0, -50f * tilt);
        const float canK = 1.5f;
        waterCan.transform.localScale = Vector3.one * canK;
        waterCan.transform.rotation = rot;
        waterCan.transform.position = me.HandR - rot * (new Vector3(-0.07f, 0.12f, 0) * canK);
        // 물줄기
        if (waterT > 0.3f && waterT < 1.5f)
        {
            var spout = waterCan.transform.TransformPoint(new Vector3(0.24f, 0.2f, 0));
            var aim = s.pos + Vector3.up * (PotTop + 0.05f);
            const float T = 0.42f;   // 꼭지에서 화분 흙까지 날아가는 시간 (중력 5로 포물선)
            var v0 = (aim - spout) / T + Vector3.up * (0.5f * 5f * T);
            for (int k = 0; k < 3; k++)
                FxOne("drop", spout + UnityEngine.Random.insideUnitSphere * 0.015f, v0 + UnityEngine.Random.insideUnitSphere * 0.12f, 0.025f, FxWater[UnityEngine.Random.Range(0, 2)], T + 0.15f, 5f);
        }
        if (waterT > 1.0f && waterT - dt <= 1.0f && net != null)
        {
            string key = "p" + s.i + "~" + ((long)s.ct).ToString() + "~" + ((long)Math.Floor(NowMs / 60000.0)).ToString() + "~" + myKey;
            QuestProgress("water", 1);
            net.Put("pw/" + key, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"t\":" + Math.Floor(NowMs).ToString("0") + "}");
            s.iWatered = true; s.myLastW = NowMs; nextPlantPoll = 0;
            int add = s.owner == myKey ? 4 : 1;
            int after = Mathf.Min(PlantMax, Mathf.Max(0, s.stage) + add);
            FxBurst("drop", s.pos + Vector3.up * (PotTop + 0.02f), 8, 1.0f, 0.03f, FxWater, 0.5f, 5f);
            Toast(after >= PlantMax ? "다 자랐어요! 반짝반짝" : (s.owner == myKey ? $"물을 줬어요! 쑥쑥 +4 ({after}/{PlantMax})" : $"{s.name} 님 화분에 물을 줬어요! +1 ({after}/{PlantMax})"));
            if (after >= PlantMax && s.stage < PlantMax) { FxBurst("star", s.pos + Vector3.up * (PotTop + s.height), 24, 2.4f, 0.06f, FxGold, 1.4f, 1f); Sfx("candy_win", 0.5f); }
        }
        if (waterT > 1.8f) { waterSlot = -1; waterCan.SetActive(false); me.pour = -1f; }
    }

    string NextWaterText(PlantSlot s)
    {
        double left = s.myLastW + WaterCoolMs - NowMs;
        int mins = Mathf.Max(1, (int)Math.Ceiling(left / 60000.0));
        return mins >= 60 ? $"{mins / 60}시간 {mins % 60}분 뒤" : $"{mins}분 뒤";
    }

    int NearestSlot(float within)
    {
        if (slots == null || me == null) return -1;
        int best = -1; float bd = within;
        foreach (var s in slots) { var d = s.pos - me.transform.position; d.y = 0; if (d.magnitude < bd) { bd = d.magnitude; best = s.i; } }
        return best;
    }

    // ===== 화분 보기 / 꾸미기 =====
    void FocusPlant(int i)
    {
        plantFocus = i; plantDeco = false; plantDelAsk = false; decoZoom = 1f; walkTarget = null; followId = null;
        plantYaw = YawToward(slots[i].pos);
    }

    bool PlantCamera(ref Vector3 focus, ref float dist, ref float pitch, ref float targetYaw)
    {
        int i = plantFocus >= 0 ? plantFocus : plantPicker;
        if (i < 0 || slots == null) return false;
        var s = slots[i];
        float h = PotTop + (s.planted ? s.height : 0.1f) + 0.1f;
        bool portrait = Screen.height > Screen.width;
        float vfov = cam.fieldOfView * Mathf.Deg2Rad;
        float need = Mathf.Max(h / (portrait ? 0.4f : 0.55f), 1.1f / Mathf.Max(0.3f, cam.aspect));
        focus = s.pos + Vector3.up * (h * (portrait ? 0.1f : 0.3f));
        dist = need / (2f * Mathf.Tan(vfov / 2f)) * decoZoom + 0.4f;
        pitch = 30f; targetYaw = plantYaw;
        return true;
    }

    bool plantPressOn;
    void PlantFocusUpdate()
    {
        var s = slots[plantFocus];
        me.speed = 0;
        if (!s.planted || phase != Phase.Play) { plantFocus = -1; return; }
        if (Key("esc")) { if (plantDeco) plantDeco = false; else plantFocus = -1; return; }
        bool pin = PinchZoom();
        if (ptrDown) { longUsed = false; if (!pinching) pinchUsed = false; plantPressOn = plantDeco && plantPicked && !OverUI(ptrStart) && PickPlant(s, ptrStart, out _, out _); }
        if (!pin && ptrPressed && ptrMoved && !ptrOnUI && !(plantDeco && plantPressOn)) plantYaw += (ptrPos - lastPtr).x * 0.25f;
        lastPtr = ptrPos;
        float sc = OverUI(ptrPos) ? 0 : Scroll(); if (Mathf.Abs(sc) > 0.01f) decoZoom = Mathf.Clamp(decoZoom * (sc > 0 ? 0.9f : 1.1f), 0.5f, 2.5f);
        if (!plantDeco) return;
        bool remove = MouseDown(1) || (LongPress && !longUsed);
        if (remove)
        {
            longUsed = true;
            int di = PickPlantDeco(s, ptrPos);
            if (di >= 0) { plantDraft.d.RemoveAt(di); BuildPlantDeco(s, plantDraft); }
        }
        else if (plantPicked && ptrUp && !ptrOnUI && plantPressOn && !pinching && !pinchUsed && !longUsed && plantDraft.d.Count < 40 && PickPlant(s, ptrPos, out var lp, out var ln))
        {
            string m = PlantDecoIds[Wrap(plantDecoSel, PlantDecoIds.Length)];
            plantDraft.d.Add(new PlantDeco { m = m, x = lp.x, y = lp.y, z = lp.z, nx = ln.x, ny = ln.y, nz = ln.z, s = DecoSizes[Mathf.Clamp(plantSizeSel, 0, 2)],
                                             c = DecoTintable(m) ? HexOf(AcPalette.Vivid[Wrap(plantColSel, AcPalette.Vivid.Length)]) : "" });
            BuildPlantDeco(s, plantDraft);
            Sfx("click", 0.4f, 1.3f);
        }
    }

    bool PickPlant(PlantSlot s, Vector2 screen, out Vector3 localP, out Vector3 localN)
    {
        localP = localN = Vector3.zero;
        if (!s.col || !s.plant) return false;
        var ray = cam.ScreenPointToRay(screen);
        if (!s.col.Raycast(ray, out var hit, 60f)) return false;
        localP = s.plant.transform.InverseTransformPoint(hit.point);
        localN = s.plant.transform.InverseTransformDirection(hit.normal).normalized;
        if (Vector3.Dot(localN, ray.direction) > 0) localN = -localN;   // 잎 뒷면을 눌러도 바깥쪽으로
        return true;
    }

    int PickPlantDeco(PlantSlot s, Vector2 screen)
    {
        if (plantDraft == null || !s.plant) return -1;
        var ray = cam.ScreenPointToRay(screen);
        int best = -1; float bd = 0.08f;
        for (int i = 0; i < plantDraft.d.Count; i++)
        {
            var wp = s.plant.transform.TransformPoint(new Vector3(plantDraft.d[i].x, plantDraft.d[i].y, plantDraft.d[i].z));
            float dist = Vector3.Cross(ray.direction, wp - ray.origin).magnitude;
            if (dist < bd) { bd = dist; best = i; }
        }
        return best;
    }

    // ===== 아이콘 (식물 고르기 카드) =====
    readonly Texture2D[] plantIcons = new Texture2D[5];
    bool plantIconWant;
    void ProcessPlantIcons()
    {
        if (!plantIconWant) return;
        plantIconWant = false;
        EnsureIconCam();
        for (int k = 0; k < plantIcons.Length; k++)
        {
            if (plantIcons[k]) continue;
            var holder = new GameObject("plantIcon");
            ProcObj("pot", PotMesh(PotDefault), holder.transform);
            var pm = PlantMesh(k, PlantMax, 3);
            var pl = ProcObj("plant", pm, holder.transform); pl.transform.localPosition = new Vector3(0, PotTop, 0);
            holder.transform.rotation = Quaternion.Euler(8, 25, 0);
            plantIcons[k] = ObjIcon(holder);
            Destroy(pm);
            if (!plantIcons[k]) plantIconWant = true;
        }
    }

    // 물체 하나를 아이콘으로 찍기 (찍고 나면 없앰)
    Texture2D ObjIcon(GameObject obj)
    {
        obj.transform.position = Vector3.zero;
        var bounds = new Bounds(); bool first = true;
        foreach (var r in obj.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
        obj.transform.position = new Vector3(0, -500, 0) - bounds.center;
        var tex = RenderIconTex(obj, new Vector3(0, -500, 2.5f), Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f + 0.005f, 8f);
        DestroyImmediate(obj);
        return tex;
    }

    // ===== 화면 =====
    // 온실 화분 위 이름표
    void GreenTags()
    {
        if (slots == null || !InGreenhouse || GreenBusy || sTag == null) return;
        foreach (var s in slots)
        {
            var d = s.pos - me.transform.position; if (d.sqrMagnitude > 90f) continue;
            var sp = cam.WorldToScreenPoint(s.pos + Vector3.up * (PotTop + (s.planted ? s.height : 0) + 0.25f));
            if (sp.z < 0) continue;
            float x = sp.x / UiK, y = (Screen.height - sp.y) / UiK;
            string t;
            if (!s.planted) { if (d.sqrMagnitude > 9f) continue; t = "<size=12>빈 화분</size>"; }   // 빈 화분 이름표는 바로 옆에서만 (12개가 한꺼번에 떠서 어수선했음)
            else if (s.stage >= PlantMax) t = $"<size=12>{s.name}  <color=#c0392b>다 자랐어요!</color></size>";
            else t = $"<size=12>{s.name}  {Mathf.Max(0, s.stage)}/{PlantMax}{(s.iWatered ? "" : "  <color=#1f6fb8>물!</color>")}</size>";
            var size = sTag.CalcSize(new GUIContent(t));
            GUI.Label(new Rect(x - size.x / 2 - 6, y - 10, size.x + 12, 20), t, sTag);
        }
    }

    void GreenGUI()
    {
        if (plantPicker >= 0) PickerGUI();
        else if (plantFocus >= 0) { if (plantDeco) PlantDecoGUI(); else PlantFocusGUI(); }
    }

    void PickerGUI()
    {
        bool narrow = SW < 640;
        float w = Mathf.Min(SW - 20, 560), h = narrow ? 296 : 250;
        var r = new Rect(SW / 2 - w / 2, SH - h - 10, w, h);
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 16, r.y + 10, w - 32, 26), "어떤 식물을 심을까요?", sLabel);
        if (plantIcons.Any(t => t == null)) plantIconWant = true;
        int n = PlantKo.Length;
        float cw = Mathf.Min(96, (w - 32 - (n - 1) * 6) / n), ch = cw + 22;
        for (int k = 0; k < n; k++)
        {
            var cr = new Rect(r.x + 16 + k * (cw + 6), r.y + 42, cw, ch);
            AcUiTheme.Fill(cr, k == pickKind ? new Color(1f, 0.93f, 0.8f) : new Color(1f, 1f, 1f, 0.85f), 3);
            if (k == pickKind) AcUiTheme.Ring(cr, AcUiTheme.Accent, 3);
            if (plantIcons[k]) GUI.DrawTexture(new Rect(cr.x + 4, cr.y + 2, cw - 8, cw - 8), plantIcons[k], ScaleMode.ScaleToFit);
            GUI.Label(new Rect(cr.x, cr.y + cw - 6, cw, 24), "<size=11>" + PlantKo[k] + "</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow });
            if (GUI.Button(cr, "", GUIStyle.none)) pickKind = k;
        }
        float cy = r.y + 42 + ch + 8;
        GUI.Label(new Rect(r.x + 16, cy, w - 32, 22), "<size=13>" + PlantDesc[pickKind] + "</size>", sSmall);
        cy += 28;
        int pi = Wrap(pickPot, PotCols.Length);
        GUI.Label(new Rect(r.x + 16, cy + 3, 70, 24), "화분 색", sSmall);
        if (GUI.Button(new Rect(r.x + 86, cy, 32, 28), "<", sBtn)) pickPot--;
        GUI.DrawTexture(new Rect(r.x + 122, cy + 3, 40, 22), AcPalette.Tex(PotCols[pi]));
        GUI.Label(new Rect(r.x + 168, cy + 3, 60, 24), PotColKo[pi], sSmall);
        if (GUI.Button(new Rect(r.x + 222, cy, 32, 28), ">", sBtn)) pickPot++;
        var s = slots[plantPicker];
        if (!s.planted) s.pot.GetComponent<MeshFilter>().sharedMesh = PotMesh(PotCols[Wrap(pickPot, PotCols.Length)]);
        cy += 36;
        GUI.Label(new Rect(r.x + 16, cy, w - 32, 22), "<size=12>물 주기: 내가 주면 +4, 친구가 주면 +1 (100이면 다 자라요)</size>", sSmall);
        float bw = 96, by = r.yMax - 42;
        if (GUI.Button(R(new Rect(r.xMax - 16 - bw * 2 - 8, by, bw, 32)), "취소", sBtn)) ClosePicker(false);
        if (GUI.Button(R(new Rect(r.xMax - 16 - bw, by, bw, 32)), "<b>심기!</b>", sBtn)) ClosePicker(true);
    }

    void PlantFocusGUI()
    {
        var s = slots[plantFocus];
        float w = Mathf.Min(SW - 20, 480), h = 150;
        var r = new Rect(SW / 2 - w / 2, SH - h - 10, w, h);
        GUI.Box(R(r), "", sBox);
        bool mine = s.owner == myKey, grown = s.stage >= PlantMax;
        GUI.Label(new Rect(r.x + 16, r.y + 10, w - 32, 26), (mine ? "내 " : s.name + " 님의 ") + PlantKo[s.kind] + (grown ? "  <color=#c0392b>다 자랐어요!</color>" : ""), sLabel);
        // 자란 정도 막대
        var bar = new Rect(r.x + 16, r.y + 42, w - 32 - 70, 14);
        AcUiTheme.Fill(bar, new Color(0.9f, 0.9f, 0.88f), 0);
        float k = Mathf.Clamp01(Mathf.Max(0, s.stage) / (float)PlantMax);
        if (k > 0) AcUiTheme.Fill(new Rect(bar.x, bar.y, Mathf.Max(14, bar.width * k), bar.height), grown ? new Color(1f, 0.75f, 0.2f) : new Color(0.4f, 0.75f, 0.4f), 0);
        GUI.Label(new Rect(bar.xMax + 8, bar.y - 4, 70, 22), $"<size=13>{Mathf.Max(0, s.stage)}/{PlantMax}</size>", sSmall);
        string who = s.waterNames.Count > 0 ? string.Join(", ", s.waterNames.Take(4)) + (s.waterNames.Count > 4 ? $" 외 {s.waterNames.Count - 4}명" : "") : "아직 없어요";
        GUI.Label(new Rect(r.x + 16, r.y + 62, w - 32, 22), "<size=12>최근 물 준 사람: " + who + "</size>", sSmall);
        GUI.Label(new Rect(r.x + 16, r.y + 82, w - 32, 22), "<size=12>" + (s.iWatered ? "다음 물 주기: " + NextWaterText(s) : grown ? "물을 주면 반짝반짝 기뻐해요" : (mine ? "지금 물을 주면 +4" : "지금 물을 주면 +1")) + "</size>", sSmall);
        float by = r.yMax - 42, bw = narrowBtn(w, 4);
        float x = r.x + 16;
        if (plantDelAsk)
        {
            GUI.Label(new Rect(x, by + 6, w - 32 - bw * 2 - 16, 22), "<size=13>정말 뽑을까요?</size>", sSmall);
            if (GUI.Button(R(new Rect(r.xMax - 16 - bw * 2 - 8, by, bw, 32)), "<b>뽑기</b>", sBtn)) RemovePlant(s);
            if (GUI.Button(R(new Rect(r.xMax - 16 - bw, by, bw, 32)), "그대로", sBtn)) plantDelAsk = false;
            return;
        }
        if (!s.iWatered && waterSlot < 0) { if (GUI.Button(R(new Rect(x, by, bw, 32)), "<b>물 주기</b>", sBtn)) StartWatering(s.i); x += bw + 8; }
        if (mine && grown) { if (GUI.Button(R(new Rect(x, by, bw, 32)), "꾸미기", sBtn)) { plantDeco = true; plantDraft = ParseDeco(s.decoJson); plantPicked = true; } x += bw + 8; }
        if (mine) { if (GUI.Button(R(new Rect(x, by, bw, 32)), "뽑기", sBtn)) plantDelAsk = true; x += bw + 8; }
        if (GUI.Button(R(new Rect(r.xMax - 16 - bw, by, bw, 32)), "<b>닫기</b>", sBtn)) plantFocus = -1;
    }
    static float narrowBtn(float w, int n) => Mathf.Min(96, (w - 32 - 8 * (n - 1)) / n);

    void RemovePlant(PlantSlot s)
    {
        if (net == null) return;
        var pw = net.Get("pw");
        string pre = "p" + s.i + "~";
        if (pw != null) foreach (var key in pw.Keys.Where(k => k.StartsWith(pre, StringComparison.Ordinal)).ToList()) net.Delete("pw/" + key);
        net.Delete("plants/p" + s.i);
        ClearPlant(s);
        plantFocus = -1; nextPlantPoll = 0;
        Toast("화분을 비웠어요. 새로 심을 수 있어요");
    }

    void PlantDecoGUI()
    {
        var s = slots[plantFocus];
        bool narrow = SW < 640;
        float h = narrow ? 230 : 200, y = SH - h - 8;
        var box = new Rect(10, y, SW - 20, h);
        GUI.Box(R(box), "", sBox);
        GUI.Label(new Rect(26, y + 10, SW - 260, 24), "화분 꾸미기", sLabel);
        float bw = narrow ? 80 : 90;
        var cr = new Rect(SW - 20 - bw * 2 - 18, y + 8, bw, 32); var fr = new Rect(SW - 20 - bw - 8, y + 8, bw, 32);
        R(cr); R(fr);
        if (GUI.Button(cr, "취소", sBtn)) { plantDeco = false; BuildPlantDeco(s, ParseDeco(s.decoJson)); return; }
        if (GUI.Button(fr, "<b>완성!</b>", sBtn))
        {
            string dj = JsonUtility.ToJson(plantDraft);
            PutPlant(s, s.kind, s.potHex, s.ct, dj, s.owner, s.name);
            s.decoJson = dj; s.decoBuilt = dj; plantDeco = false;
            Emote("cheer"); Toast("예쁘게 꾸몄어요!");
            FxBurst("star", s.pos + Vector3.up * (PotTop + s.height * 0.6f), 16, 1.8f, 0.05f, FxGold, 1.1f, 1f);
            return;
        }
        float cy = y + 44, lx = 26;
        GUI.Label(new Rect(lx, cy - 2, SW - 60, 22), IsMobile ? "<size=13>장식을 고르고 식물을 눌러 붙이기, 길게 눌러 떼기</size>" : "<size=13>장식을 고르고 클릭해 붙이기, 오른쪽 클릭으로 떼기</size>", sSmall);
        cy += 22;
        float size = narrow ? 52 : 58;
        var col = AcPalette.Vivid[Wrap(plantColSel, AcPalette.Vivid.Length)];
        int ci = IconStrip(plantStrip, new Rect(lx - 3, cy, SW - 2 * lx, size + 6), PlantDecoIds.Length, size, i => SnowIcon(PlantDecoIds[i], col), plantDecoSel, plantPicked);
        if (ci >= 0) { if (ci == plantDecoSel && plantPicked) plantPicked = false; else { plantDecoSel = ci; plantPicked = true; } }
        cy += size + 12;
        float x2 = lx;
        string cur = PlantDecoIds[Wrap(plantDecoSel, PlantDecoIds.Length)];
        if (DecoTintable(cur))
        {
            GUI.Label(new Rect(x2, cy + 3, 30, 22), "색", sSmall);
            if (GUI.Button(new Rect(x2 + 28, cy, 30, 28), "<", sBtn)) plantColSel--;
            GUI.DrawTexture(new Rect(x2 + 62, cy + 2, 46, 24), AcPalette.Tex(col));
            if (GUI.Button(new Rect(x2 + 112, cy, 30, 28), ">", sBtn)) plantColSel++;
            x2 += 152;
        }
        GUI.Label(new Rect(x2, cy + 3, 40, 22), "크기", sSmall);
        for (int i = 0; i < 3; i++)
        {
            var rr = new Rect(x2 + 40 + i * 50, cy, 46, 28);
            if (i == plantSizeSel) AcUiTheme.Ring(rr, AcUiTheme.Accent, 3);
            if (GUI.Button(rr, DecoSizeKo[i], sBtn)) plantSizeSel = i;
        }
        cy += 34;
        if (plantDraft != null && plantDraft.d.Count > 0 && GUI.Button(R(new Rect(lx, cy, 110, 28)), "하나 되돌리기", sBtn)) { plantDraft.d.RemoveAt(plantDraft.d.Count - 1); BuildPlantDeco(s, plantDraft); }
    }

    // 상황 버튼: 온실에서 화분 가까이 가면
    bool GreenContext(out string label, out Action act, List<(string, Action, float)> extra)
    {
        label = null; act = null;
        if (!InGreenhouse) return false;
        int i = NearestSlot(1.7f);
        if (i < 0) return false;
        var s = slots[i];
        if (!s.planted) { label = "화분 심기"; act = () => OpenPlantPicker(i); }
        else if (!s.iWatered && waterSlot < 0) { label = "물 주기"; act = () => StartWatering(i); extra.Add(("화분 보기", () => FocusPlant(i), IsMobile ? 112 : 100)); }
        else { label = "화분 보기"; act = () => FocusPlant(i); }
        return true;
    }
}
