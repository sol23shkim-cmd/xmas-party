// AcPets.cs - 따라다니는 펫 12종 (11-27)
// 모두 이 파일에서 모양을 직접 만듦(관절마다 따로: 몸·머리·다리·꼬리·귀·날개·눈). 눈은 깜빡이고, 걸음걸이는 종류마다 다름.
//   네 발로 걷기(루돌프·고양이·코기·북극곰·여우·판다), 총총 뛰기(토끼·꼬마 눈사람), 뒤뚱뒤뚱(펭귄), 날기(눈부엉이),
//   종종걸음(햄스터), 두 발 걷기(진저브레드맨). 가만히 있으면 숨 쉬고 두리번거리고, 네발 동물은 잠시 뒤 앉음.
// 펫은 주인 뒤쪽 옆에서 따라옴(너무 멀어지면 순간이동). 다른 사람 펫도 AcNet.pet 으로 보임.
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    public static readonly string[] PetIds = { "rudolph", "kitten", "corgi", "bunny", "penguin", "polarbear", "fox", "panda", "owl", "hamster", "snowman", "ginger" };
    public static readonly string[] PetKo = { "루돌프", "아기 고양이", "웰시코기", "아기 토끼", "아기 펭귄", "아기 북극곰", "아기 여우", "아기 판다", "눈부엉이", "햄스터", "꼬마 눈사람", "진저브레드맨" };
    public static string PetName(string id) { int i = Array.IndexOf(PetIds, id); return i >= 0 ? PetKo[i] : ""; }

    enum PetGait { Walk, Hop, Waddle, Fly, Biped }

    class PetRig
    {
        public string id; public PetGait gait;
        public Transform root, body, head, tail, earL, earR, wingL, wingR, armL, armR, eyes, nose;
        public Transform[] legs = new Transform[0];   // 네발: 앞왼·앞오·뒤왼·뒤오 / 두발: 왼·오
        public Vector3 bodyRest, headRest; public float legLen = 0.15f, stepLen = 0.3f;
        public float yaw, spd, phase, idle, blinkAt, lookAt, lookYaw, earAt, earKick, hop, flyH, sitK, noseEmit;
        public Vector3 vel;
        public MaterialPropertyBlock mpb;
        public float[] legRestY;
    }

    // ===== 만들기 도우미 =====
    // 펫 미리보기: 주소에 ?petgallery 를 붙이고 '혼자 둘러보기'로 들어가면 12마리가 모두 뒤를 따라옴 (확인용)
    bool PetGallery => net != null && net.Offline && Application.absoluteURL.Contains("petgallery");
    readonly List<PetRig> galleryPets = new List<PetRig>();

    static Transform PPivot(Transform parent, string name, Vector3 lp)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = lp; return t;
    }
    static Transform PPart(Transform parent, string name, Vector3 lp, Action<AcPB> f, float emit = 0f)
    {
        var b = new AcPB { detail = 1.5f }; f(b);
        var go = ProcObj("pet_" + name, b.M("pet_" + name), parent, emit, true);
        go.transform.localPosition = lp; return go.transform;
    }
    static Color C(float r, float g, float b) => new Color(r, g, b);
    static readonly Color PEye = C(0.08f, 0.06f, 0.07f), PShine = C(1f, 1f, 1f), PBlush = C(1f, 0.62f, 0.66f), PNoseDark = C(0.16f, 0.11f, 0.11f);

    // 눈 한 쌍 (머리 기준): 반짝이는 점 두 개 + 볼터치. 깜빡일 수 있게 따로 묶음
    static Transform PEyes(Transform head, Vector3 c, float gap, float r, float faceZ, Color? iris = null, bool blush = true, float blushGap = 1.75f)
    {
        var g = PPivot(head, "eyes", c);
        PPart(g, "eyeball", Vector3.zero, b =>
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var e = new Vector3(s * gap, 0, faceZ);
                if (iris.HasValue)
                {
                    b.Ellipsoid(e, new Vector3(r * 1.25f, r * 1.25f, r * 0.55f), iris.Value, 10, 16);
                    b.Ellipsoid(e + new Vector3(0, 0, r * 0.38f), new Vector3(r * 0.7f, r * 0.7f, r * 0.35f), PEye, 10, 14);
                }
                else b.Ellipsoid(e, new Vector3(r, r * 1.12f, r * 0.62f), PEye, 10, 16);
                b.Ellipsoid(e + new Vector3(-s * r * 0.08f - r * 0.3f, r * 0.38f, r * 0.62f), Vector3.one * r * 0.3f, PShine, 6, 8);
                b.Ellipsoid(e + new Vector3(r * 0.32f, -r * 0.35f, r * 0.58f), Vector3.one * r * 0.14f, PShine, 5, 6);
            }
        });
        if (blush)
            PPart(head, "blush", c, b => { foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * gap * blushGap, -r * 1.5f, faceZ - r * 0.5f), new Vector3(r * 0.95f, r * 0.55f, r * 0.35f), PBlush, 6, 10); });
        return g;
    }

    // 네 다리 (엉덩이·어깨 높이에서 아래로). 발끝 색 paw
    static void PLegs(PetRig rig, float hipY, float x, float zf, float zb, float len, float r, Color col, Color paw, float pawK = 1.25f)
    {
        rig.legLen = len;
        var ps = new[] { new Vector3(-x, hipY, zf), new Vector3(x, hipY, zf), new Vector3(-x, hipY, zb), new Vector3(x, hipY, zb) };
        rig.legs = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var p = PPivot(rig.root, "leg" + i, ps[i]);
            PPart(p, "legm", Vector3.zero, b =>
            {
                b.Tube(new[] { new Vector3(0, r * 0.4f, 0), new Vector3(0, -len * 0.55f, 0), new Vector3(0, -len + r * 0.6f, 0) }, new[] { r * 1.1f, r * 0.92f, r * 0.85f }, col, 12);
                b.Ellipsoid(new Vector3(0, -len + r * 0.55f, r * 0.25f), new Vector3(r * pawK, r * 0.62f, r * pawK * 1.18f), paw, 8, 12);
            });
            rig.legs[i] = p;
        }
    }

    // 목에 빨간 목도리(목걸이) + 금방울
    static void PCollar(Transform parent, Vector3 c, float R, float tilt = 20f)
    {
        PPart(parent, "collar", c, b =>
        {
            var ax = Quaternion.Euler(tilt, 0, 0) * Vector3.up;
            b.Torus(Vector3.zero, ax, R, R * 0.16f, C(0.86f, 0.16f, 0.2f), 28, 8);
            var fr = Quaternion.Euler(tilt, 0, 0) * new Vector3(0, -R * 0.15f, R * 1.08f);
            b.Sphere(fr + new Vector3(0, -R * 0.12f, 0), R * 0.24f, C(1f, 0.8f, 0.25f), 8, 12);
            b.Torus(fr + new Vector3(0, -R * 0.12f, 0), Vector3.up, R * 0.17f, R * 0.04f, C(0.85f, 0.62f, 0.15f), 12, 5);
        });
    }

    static Func<Vector3, Color> Belly(Color top, Color belly, float edge = -0.15f, float soft = 0.25f, float frontBias = 0.5f)
        => u => Color.Lerp(top, belly, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge + soft, edge - soft, u.y - Mathf.Max(0, u.z) * frontBias)));

    // ===== 펫 12종 =====
    PetRig MakePet(string id)
    {
        var rig = new PetRig { id = id, mpb = new MaterialPropertyBlock() };
        rig.root = new GameObject("Pet_" + id).transform;
        rig.root.SetParent(world, false);
        switch (id)
        {
            case "rudolph": BuildRudolph(rig); break;
            case "kitten": BuildKitten(rig); break;
            case "corgi": BuildCorgi(rig); break;
            case "bunny": BuildBunny(rig); break;
            case "penguin": BuildPenguin(rig); break;
            case "polarbear": BuildBear(rig); break;
            case "fox": BuildFox(rig); break;
            case "panda": BuildPanda(rig); break;
            case "owl": BuildOwl(rig); break;
            case "hamster": BuildHamster(rig); break;
            case "snowman": BuildSnowPet(rig); break;
            default: BuildGinger(rig); break;
        }
        if (rig.body) rig.bodyRest = rig.body.localPosition;
        if (rig.head) rig.headRest = rig.head.localPosition;
        rig.blinkAt = Time.time + UnityEngine.Random.Range(1f, 3f);
        rig.lookAt = Time.time + 2f;
        return rig;
    }

    void BuildRudolph(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.34f;
        Color fur = C(0.6f, 0.4f, 0.26f), cream = C(0.95f, 0.87f, 0.74f), hoof = C(0.3f, 0.2f, 0.15f), antler = C(0.8f, 0.65f, 0.45f);
        PLegs(rig, 0.27f, 0.085f, 0.13f, -0.13f, 0.25f, 0.034f, fur, hoof, 1.15f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.34f, 0));
        PPart(rig.body, "torso", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.15f, 0.135f, 0.23f), fur, 16, 24, null, Belly(fur, cream, -0.25f, 0.3f, 0.3f));
            b.Ellipsoid(new Vector3(0, 0.08f, 0.17f), new Vector3(0.085f, 0.11f, 0.08f), fur, 12, 16, Quaternion.Euler(-30, 0, 0), Belly(fur, cream, 0.1f, 0.35f, 1.2f));   // 목
        });
        PCollar(rig.body, new Vector3(0, 0.1f, 0.19f), 0.072f, -35f);
        rig.tail = PPart(rig.body, "tail", new Vector3(0, 0.07f, -0.22f), b => b.Ellipsoid(new Vector3(0, 0.01f, -0.025f), new Vector3(0.045f, 0.055f, 0.04f), cream, 8, 12));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.22f, 0.25f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.125f, 0.115f, 0.12f), fur, 16, 22);
            b.Ellipsoid(new Vector3(0, -0.045f, 0.09f), new Vector3(0.075f, 0.06f, 0.075f), cream, 12, 18);   // 주둥이
            b.Ellipsoid(new Vector3(0, -0.075f, 0.135f), new Vector3(0.02f, 0.008f, 0.012f), C(0.45f, 0.25f, 0.2f), 6, 8);   // 입
        });
        rig.nose = PPart(rig.head, "nose", new Vector3(0, -0.025f, 0.165f), b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.034f, 0.03f, 0.03f), C(0.95f, 0.12f, 0.12f), 10, 14);
            b.Ellipsoid(new Vector3(-0.01f, 0.012f, 0.022f), Vector3.one * 0.008f, C(1f, 0.75f, 0.75f), 4, 6);
        }, 0.6f);
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.02f, 0), 0.055f, 0.022f, 0.098f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.1f, 0.05f, -0.01f), b => { b.Ellipsoid(new Vector3(-0.04f, 0, 0), new Vector3(0.05f, 0.022f, 0.03f), fur, 8, 12, Quaternion.Euler(0, 0, 20)); b.Ellipsoid(new Vector3(-0.042f, 0.004f, 0.008f), new Vector3(0.035f, 0.012f, 0.02f), cream, 6, 10, Quaternion.Euler(0, 0, 20)); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.1f, 0.05f, -0.01f), b => { b.Ellipsoid(new Vector3(0.04f, 0, 0), new Vector3(0.05f, 0.022f, 0.03f), fur, 8, 12, Quaternion.Euler(0, 0, -20)); b.Ellipsoid(new Vector3(0.042f, 0.004f, 0.008f), new Vector3(0.035f, 0.012f, 0.02f), cream, 6, 10, Quaternion.Euler(0, 0, -20)); });
        PPart(rig.head, "antlers", Vector3.zero, b =>
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var a0 = new Vector3(s * 0.05f, 0.09f, -0.02f);
                var a1 = new Vector3(s * 0.085f, 0.17f, -0.04f); var a2 = new Vector3(s * 0.12f, 0.24f, -0.02f); var a3 = new Vector3(s * 0.13f, 0.3f, 0.02f);
                b.Tube(new[] { a0, a1, a2, a3 }, new[] { 0.016f, 0.014f, 0.012f, 0.008f }, antler, 8);
                b.Tube(new[] { a1 + new Vector3(0, 0.01f, 0), a1 + new Vector3(s * -0.01f, 0.06f, 0.04f), a1 + new Vector3(s * -0.015f, 0.09f, 0.06f) }, new[] { 0.011f, 0.009f, 0.006f }, antler, 7);
                b.Tube(new[] { a2, a2 + new Vector3(s * 0.05f, 0.03f, -0.01f), a2 + new Vector3(s * 0.075f, 0.06f, -0.01f) }, new[] { 0.01f, 0.008f, 0.005f }, antler, 7);
                b.Sphere(a3, 0.008f, antler, 5, 6);
            }
        });
    }

    void BuildKitten(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.24f;
        Color fur = C(0.99f, 0.74f, 0.45f), stripe = C(0.9f, 0.55f, 0.27f), white = C(1f, 0.97f, 0.93f), pink = C(1f, 0.66f, 0.7f);
        PLegs(rig, 0.17f, 0.065f, 0.1f, -0.1f, 0.16f, 0.028f, fur, white, 1.3f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.23f, 0));
        PPart(rig.body, "torso", Vector3.zero, b =>
            b.Ellipsoid(Vector3.zero, new Vector3(0.105f, 0.1f, 0.17f), fur, 16, 24, null,
                u => { var c = Mathf.Repeat(u.z * 4.5f, 1f) < 0.33f && u.y > 0.1f ? stripe : fur; return Color.Lerp(c, white, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.05f, -0.45f, u.y))); }));
        rig.tail = PPivot(rig.body, "tail", new Vector3(0, 0.05f, -0.16f));
        PPart(rig.tail, "tailm", Vector3.zero, b =>
        {
            var pts = new List<Vector3>(); var rr = new List<float>(); var cc = new List<Color>();
            for (int i = 0; i <= 10; i++) { float t = i / 10f; pts.Add(new Vector3(0, Mathf.Sin(t * 1.6f) * 0.2f, -Mathf.Sin(t * 2.2f) * 0.07f - t * 0.03f + t * t * 0.08f)); rr.Add(Mathf.Lerp(0.024f, 0.02f, t)); cc.Add(i % 3 == 1 ? stripe : (i == 10 ? stripe : fur)); }
            b.Tube(pts, rr, fur, 10, cc);
        });
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.14f, 0.15f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.13f, 0.11f, 0.11f), fur, 18, 24, null, u => u.y > 0.55f && Mathf.Abs(u.x) < 0.25f ? stripe : (u.y < -0.25f && u.z > 0.2f ? white : fur));
            b.Ellipsoid(new Vector3(0, -0.035f, 0.08f), new Vector3(0.05f, 0.035f, 0.035f), white, 10, 14);
            b.Ellipsoid(new Vector3(0, -0.018f, 0.112f), new Vector3(0.013f, 0.009f, 0.007f), pink, 6, 8);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.01f, 0), 0.052f, 0.022f, 0.092f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.07f, 0.08f, -0.005f), b => { b.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.05f, 0.016f), fur, 8, 12, Quaternion.Euler(0, 0, 18)); b.Ellipsoid(new Vector3(0.004f, 0.026f, 0.01f), new Vector3(0.024f, 0.034f, 0.008f), pink, 6, 10, Quaternion.Euler(0, 0, 18)); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.07f, 0.08f, -0.005f), b => { b.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.05f, 0.016f), fur, 8, 12, Quaternion.Euler(0, 0, -18)); b.Ellipsoid(new Vector3(-0.004f, 0.026f, 0.01f), new Vector3(0.024f, 0.034f, 0.008f), pink, 6, 10, Quaternion.Euler(0, 0, -18)); });
        PCollar(rig.body, new Vector3(0, 0.08f, 0.12f), 0.058f, -30f);
    }

    void BuildCorgi(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.2f;
        Color fur = C(0.93f, 0.6f, 0.3f), white = C(1f, 0.97f, 0.92f), pink = C(1f, 0.6f, 0.65f);
        PLegs(rig, 0.11f, 0.075f, 0.13f, -0.13f, 0.11f, 0.034f, fur, white, 1.2f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.2f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.12f, 0.11f, 0.22f), fur, 16, 24, null, Belly(fur, white, -0.1f, 0.25f, 0.9f)));
        rig.tail = PPart(rig.body, "tail", new Vector3(0, 0.05f, -0.21f), b => b.Ellipsoid(new Vector3(0, 0.02f, -0.02f), new Vector3(0.05f, 0.045f, 0.045f), fur, 8, 12));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.14f, 0.21f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.12f, 0.11f, 0.11f), fur, 18, 24, null, u => (Mathf.Abs(u.x) < 0.22f && u.z > 0.1f) || (u.y < -0.35f) ? white : fur);
            b.Ellipsoid(new Vector3(0, -0.04f, 0.085f), new Vector3(0.06f, 0.045f, 0.06f), white, 12, 16);
            b.Ellipsoid(new Vector3(0, -0.018f, 0.142f), new Vector3(0.022f, 0.016f, 0.015f), PNoseDark, 8, 10);
            b.Ellipsoid(new Vector3(0, -0.085f, 0.11f), new Vector3(0.018f, 0.01f, 0.022f), pink, 6, 8, Quaternion.Euler(20, 0, 0));   // 혀
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.02f, 0), 0.05f, 0.02f, 0.095f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.065f, 0.08f, -0.01f), b => { b.Ellipsoid(new Vector3(-0.01f, 0.05f, 0), new Vector3(0.042f, 0.07f, 0.016f), fur, 8, 12, Quaternion.Euler(0, 0, 14)); b.Ellipsoid(new Vector3(-0.007f, 0.046f, 0.009f), new Vector3(0.026f, 0.048f, 0.008f), pink, 6, 10, Quaternion.Euler(0, 0, 14)); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.065f, 0.08f, -0.01f), b => { b.Ellipsoid(new Vector3(0.01f, 0.05f, 0), new Vector3(0.042f, 0.07f, 0.016f), fur, 8, 12, Quaternion.Euler(0, 0, -14)); b.Ellipsoid(new Vector3(0.007f, 0.046f, 0.009f), new Vector3(0.026f, 0.048f, 0.008f), pink, 6, 10, Quaternion.Euler(0, 0, -14)); });
        PCollar(rig.body, new Vector3(0, 0.07f, 0.17f), 0.062f, -30f);
    }

    void BuildBunny(PetRig rig)
    {
        rig.gait = PetGait.Hop; rig.stepLen = 0.3f;
        Color fur = C(0.98f, 0.97f, 0.96f), pink = C(1f, 0.7f, 0.76f), shade = C(0.9f, 0.88f, 0.9f);
        rig.legLen = 0.06f;
        rig.legs = new Transform[4];
        // 앞발 작게, 뒷발 길게
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2; float s = i % 2 == 0 ? -1f : 1f;
            var p = PPivot(rig.root, "leg" + i, new Vector3(s * (front ? 0.05f : 0.08f), front ? 0.08f : 0.07f, front ? 0.08f : -0.06f));
            PPart(p, "legm", Vector3.zero, b =>
            {
                if (front) { b.Tube(new[] { Vector3.zero, new Vector3(0, -0.06f, 0.01f) }, new[] { 0.022f, 0.02f }, fur, 10); b.Ellipsoid(new Vector3(0, -0.068f, 0.018f), new Vector3(0.024f, 0.014f, 0.03f), fur, 6, 10); }
                else b.Ellipsoid(new Vector3(0, -0.055f, 0.03f), new Vector3(0.035f, 0.022f, 0.075f), fur, 8, 12, null, u => u.y < -0.5f ? pink : fur);
            });
            rig.legs[i] = p;
        }
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.15f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.12f, 0.12f, 0.135f), fur, 18, 24, null, u => Color.Lerp(fur, shade, Mathf.Clamp01(-u.y * 0.4f))));
        rig.tail = PPart(rig.body, "tail", new Vector3(0, 0.02f, -0.13f), b => b.Ellipsoid(Vector3.zero, Vector3.one * 0.045f, C(1f, 1f, 1f), 10, 14));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.13f, 0.08f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.11f, 0.1f, 0.1f), fur, 18, 24);
            b.Ellipsoid(new Vector3(-0.022f, -0.035f, 0.08f), new Vector3(0.03f, 0.026f, 0.025f), fur, 8, 12);
            b.Ellipsoid(new Vector3(0.022f, -0.035f, 0.08f), new Vector3(0.03f, 0.026f, 0.025f), fur, 8, 12);
            b.Ellipsoid(new Vector3(0, -0.015f, 0.1f), new Vector3(0.014f, 0.01f, 0.008f), pink, 6, 8);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.015f, 0), 0.048f, 0.02f, 0.083f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.04f, 0.08f, -0.02f), b => { b.Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.032f, 0.11f, 0.018f), fur, 10, 14, Quaternion.Euler(-8, 0, 8)); b.Ellipsoid(new Vector3(-0.001f, 0.1f, 0.012f), new Vector3(0.018f, 0.085f, 0.008f), pink, 8, 12, Quaternion.Euler(-8, 0, 8)); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.04f, 0.08f, -0.02f), b => { b.Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.032f, 0.11f, 0.018f), fur, 10, 14, Quaternion.Euler(-8, 0, -8)); b.Ellipsoid(new Vector3(0.001f, 0.1f, 0.012f), new Vector3(0.018f, 0.085f, 0.008f), pink, 8, 12, Quaternion.Euler(-8, 0, -8)); });
        PPart(rig.body, "bow", new Vector3(0, 0.07f, 0.08f), b => { b.Ellipsoid(new Vector3(-0.03f, 0, 0), new Vector3(0.03f, 0.02f, 0.012f), C(0.88f, 0.18f, 0.24f), 6, 10); b.Ellipsoid(new Vector3(0.03f, 0, 0), new Vector3(0.03f, 0.02f, 0.012f), C(0.88f, 0.18f, 0.24f), 6, 10); b.Sphere(Vector3.zero, 0.013f, C(0.78f, 0.12f, 0.18f), 6, 8); });
    }

    void BuildPenguin(PetRig rig)
    {
        rig.gait = PetGait.Waddle; rig.stepLen = 0.12f;
        Color black = C(0.17f, 0.19f, 0.25f), white = C(0.99f, 0.99f, 0.98f), orange = C(1f, 0.62f, 0.2f);
        rig.legLen = 0.03f;
        rig.legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            var p = PPivot(rig.root, "leg" + i, new Vector3(s * 0.055f, 0.03f, 0.02f));
            PPart(p, "foot", Vector3.zero, b => b.Ellipsoid(new Vector3(s * 0.006f, -0.02f, 0.035f), new Vector3(0.035f, 0.012f, 0.05f), orange, 8, 12));
            rig.legs[i] = p;
        }
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.2f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.13f, 0.17f, 0.12f), black, 18, 24, null, u => Color.Lerp(black, white, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.4f, u.z - Mathf.Max(0, u.y) * 0.35f)))));
        PPart(rig.body, "scarf", new Vector3(0, 0.13f, 0), b => { b.Torus(Vector3.zero, Vector3.up, 0.085f, 0.022f, C(0.85f, 0.18f, 0.22f), 26, 8); b.Tube(new[] { new Vector3(0.05f, -0.01f, 0.07f), new Vector3(0.07f, -0.06f, 0.08f), new Vector3(0.075f, -0.1f, 0.075f) }, new[] { 0.02f, 0.019f, 0.018f }, C(0.85f, 0.18f, 0.22f), 8); });
        rig.wingL = PPivot(rig.body, "wingL", new Vector3(-0.12f, 0.07f, 0));
        PPart(rig.wingL, "w", Vector3.zero, b => b.Ellipsoid(new Vector3(-0.012f, -0.07f, 0), new Vector3(0.022f, 0.085f, 0.045f), black, 8, 12, Quaternion.Euler(0, 0, -12)));
        rig.wingR = PPivot(rig.body, "wingR", new Vector3(0.12f, 0.07f, 0));
        PPart(rig.wingR, "w", Vector3.zero, b => b.Ellipsoid(new Vector3(0.012f, -0.07f, 0), new Vector3(0.022f, 0.085f, 0.045f), black, 8, 12, Quaternion.Euler(0, 0, 12)));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.22f, 0.01f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.11f, 0.1f, 0.1f), black, 18, 24, null, u => (u.z > 0.35f && u.y < 0.45f && Mathf.Abs(u.x) < 0.75f) ? white : black);
            b.Tube(new[] { new Vector3(0, -0.02f, 0.085f), new Vector3(0, -0.026f, 0.13f) }, new[] { 0.02f, 0.002f }, orange, 10);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.02f, 0), 0.04f, 0.018f, 0.09f, null, true, 1.9f);
        PPart(rig.head, "hat", new Vector3(0, 0.085f, -0.01f), b => { b.Torus(Vector3.zero, Vector3.up, 0.065f, 0.018f, white, 22, 8); b.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0, 0.06f, -0.01f), new Vector3(0.02f, 0.1f, -0.04f), new Vector3(0.05f, 0.11f, -0.07f) }, new[] { 0.066f, 0.045f, 0.022f, 0.006f }, C(0.85f, 0.15f, 0.2f), 14); b.Sphere(new Vector3(0.055f, 0.105f, -0.075f), 0.02f, white, 8, 10); });
    }

    void BuildBear(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.3f;
        Color fur = C(0.98f, 0.96f, 0.92f), shade = C(0.9f, 0.88f, 0.84f), dark = C(0.14f, 0.12f, 0.13f);
        PLegs(rig, 0.2f, 0.09f, 0.12f, -0.12f, 0.19f, 0.05f, fur, shade, 1.15f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.3f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.155f, 0.14f, 0.21f), fur, 18, 24, null, u => Color.Lerp(fur, shade, Mathf.Clamp01(-u.y * 0.6f))));
        rig.tail = PPart(rig.body, "tail", new Vector3(0, 0.05f, -0.2f), b => b.Sphere(Vector3.zero, 0.035f, fur, 8, 10));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.15f, 0.2f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.135f, 0.125f, 0.125f), fur, 18, 24);
            b.Ellipsoid(new Vector3(0, -0.04f, 0.095f), new Vector3(0.062f, 0.048f, 0.055f), C(1f, 0.99f, 0.97f), 12, 16);
            b.Ellipsoid(new Vector3(0, -0.02f, 0.143f), new Vector3(0.025f, 0.018f, 0.014f), dark, 8, 10);
            b.Ellipsoid(new Vector3(0, -0.07f, 0.135f), new Vector3(0.012f, 0.006f, 0.006f), dark, 4, 6);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.025f, 0), 0.052f, 0.018f, 0.11f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.09f, 0.09f, -0.02f), b => { b.Ellipsoid(Vector3.zero, new Vector3(0.04f, 0.038f, 0.022f), fur, 8, 12); b.Ellipsoid(new Vector3(0, 0, 0.012f), new Vector3(0.022f, 0.02f, 0.01f), C(0.95f, 0.8f, 0.8f), 6, 8); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.09f, 0.09f, -0.02f), b => { b.Ellipsoid(Vector3.zero, new Vector3(0.04f, 0.038f, 0.022f), fur, 8, 12); b.Ellipsoid(new Vector3(0, 0, 0.012f), new Vector3(0.022f, 0.02f, 0.01f), C(0.95f, 0.8f, 0.8f), 6, 8); });
        PPart(rig.body, "scarf", new Vector3(0, 0.1f, 0.15f), b => { b.Torus(Vector3.zero, Quaternion.Euler(-30, 0, 0) * Vector3.up, 0.085f, 0.024f, C(0.2f, 0.55f, 0.35f), 26, 8); b.Tube(new[] { new Vector3(-0.06f, -0.02f, 0.06f), new Vector3(-0.07f, -0.08f, 0.08f), new Vector3(-0.065f, -0.13f, 0.075f) }, new[] { 0.022f, 0.021f, 0.02f }, C(0.85f, 0.2f, 0.22f), 8); });
    }

    void BuildFox(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.28f;
        Color fur = C(0.96f, 0.52f, 0.2f), white = C(1f, 0.97f, 0.93f), dark = C(0.25f, 0.17f, 0.15f);
        PLegs(rig, 0.19f, 0.065f, 0.11f, -0.11f, 0.18f, 0.026f, fur, dark, 1.25f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.27f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.105f, 0.1f, 0.19f), fur, 16, 24, null, Belly(fur, white, -0.05f, 0.25f, 1.0f)));
        rig.tail = PPivot(rig.body, "tail", new Vector3(0, 0.04f, -0.18f));
        PPart(rig.tail, "tailm", Vector3.zero, b =>
        {
            var pts = new List<Vector3>(); var rr = new List<float>(); var cc = new List<Color>();
            for (int i = 0; i <= 12; i++) { float t = i / 12f; pts.Add(new Vector3(0, Mathf.Sin(t * 2.2f) * 0.09f - t * 0.02f, -t * 0.26f)); rr.Add(Mathf.Sin(Mathf.Lerp(0.25f, 3.0f, t)) * 0.075f + 0.012f); cc.Add(t > 0.78f ? white : fur); }
            b.Tube(pts, rr, fur, 14, cc);
        });
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.14f, 0.18f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.12f, 0.105f, 0.105f), fur, 18, 24, null, u => u.y < -0.2f && u.z > -0.2f ? white : fur);
            b.Ellipsoid(new Vector3(0, -0.03f, 0.1f), new Vector3(0.05f, 0.04f, 0.075f), white, 12, 16, null, u => u.y > 0.3f ? fur : white);
            b.Ellipsoid(new Vector3(0, -0.018f, 0.172f), new Vector3(0.02f, 0.015f, 0.013f), dark, 8, 10);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.02f, 0), 0.048f, 0.019f, 0.088f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.06f, 0.08f, -0.01f), b => { b.Ellipsoid(new Vector3(-0.005f, 0.05f, 0), new Vector3(0.045f, 0.07f, 0.016f), fur, 8, 12, Quaternion.Euler(0, 0, 16), u => u.y > 0.55f ? dark : fur); b.Ellipsoid(new Vector3(-0.003f, 0.045f, 0.01f), new Vector3(0.026f, 0.045f, 0.008f), white, 6, 10, Quaternion.Euler(0, 0, 16)); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.06f, 0.08f, -0.01f), b => { b.Ellipsoid(new Vector3(0.005f, 0.05f, 0), new Vector3(0.045f, 0.07f, 0.016f), fur, 8, 12, Quaternion.Euler(0, 0, -16), u => u.y > 0.55f ? dark : fur); b.Ellipsoid(new Vector3(0.003f, 0.045f, 0.01f), new Vector3(0.026f, 0.045f, 0.008f), white, 6, 10, Quaternion.Euler(0, 0, -16)); });
    }

    void BuildPanda(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.26f;
        Color white = C(0.98f, 0.98f, 0.96f), black = C(0.15f, 0.15f, 0.17f), pink = C(1f, 0.7f, 0.72f);
        PLegs(rig, 0.19f, 0.09f, 0.11f, -0.11f, 0.17f, 0.052f, black, black, 1.1f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.29f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.16f, 0.15f, 0.2f), white, 18, 24, null, u => u.z > 0.15f && u.z < 0.65f ? black : white));   // 어깨 띠
        rig.tail = PPart(rig.body, "tail", new Vector3(0, 0.04f, -0.2f), b => b.Sphere(Vector3.zero, 0.032f, white, 8, 10));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.15f, 0.2f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.15f, 0.135f, 0.13f), white, 18, 24);
            b.Ellipsoid(new Vector3(0, -0.045f, 0.1f), new Vector3(0.058f, 0.042f, 0.045f), white, 12, 16);
            b.Ellipsoid(new Vector3(0, -0.028f, 0.142f), new Vector3(0.024f, 0.016f, 0.012f), black, 8, 10);
            foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * 0.05f, 0.012f, 0.098f), new Vector3(0.036f, 0.046f, 0.03f), black, 10, 14, Quaternion.Euler(0, 0, s * -30f));   // 눈 둘레 검은 무늬
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.016f, 0), 0.05f, 0.016f, 0.126f, null, false);
        PPart(rig.head, "blush", Vector3.zero, b => { foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * 0.095f, -0.04f, 0.085f), new Vector3(0.022f, 0.013f, 0.01f), pink, 6, 8); });
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.1f, 0.1f, -0.02f), b => b.Ellipsoid(Vector3.zero, new Vector3(0.045f, 0.042f, 0.025f), black, 8, 12));
        rig.earR = PPart(rig.head, "earR", new Vector3(0.1f, 0.1f, -0.02f), b => b.Ellipsoid(Vector3.zero, new Vector3(0.045f, 0.042f, 0.025f), black, 8, 12));
        PPart(rig.head, "bamboo", new Vector3(0.06f, -0.08f, 0.12f), b => { b.Cylinder(new Vector3(-0.09f, 0, 0), new Vector3(0.05f, 0.02f, 0), 0.011f, 0.011f, C(0.45f, 0.72f, 0.3f), 8); b.Leaf(new Vector3(0.04f, 0.02f, 0), new Vector3(1, 0.6f, 0.2f), Vector3.forward, 0.07f, 0.025f, C(0.4f, 0.75f, 0.35f), C(0.55f, 0.85f, 0.4f)); });
    }

    void BuildOwl(PetRig rig)
    {
        rig.gait = PetGait.Fly; rig.stepLen = 0.3f;
        Color white = C(0.99f, 0.99f, 0.98f), speck = C(0.62f, 0.62f, 0.66f), gold = C(1f, 0.82f, 0.22f), beak = C(0.35f, 0.3f, 0.28f);
        Func<Vector3, Color> speckles = u => (Mathf.Sin(u.x * 37f) * Mathf.Sin(u.y * 41f + u.z * 13f) > 0.82f && u.z < 0.55f) ? speck : white;
        rig.legLen = 0.03f;
        rig.legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            var p = PPivot(rig.root, "leg" + i, new Vector3(s * 0.045f, 0.035f, 0.03f));
            PPart(p, "foot", Vector3.zero, b => { for (int k = -1; k <= 1; k++) b.Tube(new[] { Vector3.zero, new Vector3(k * 0.014f, -0.03f, 0.03f) }, new[] { 0.009f, 0.006f }, C(0.35f, 0.32f, 0.3f), 6); });
            rig.legs[i] = p;
        }
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.17f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.12f, 0.14f, 0.11f), white, 18, 24, null, speckles));
        rig.wingL = PPivot(rig.body, "wingL", new Vector3(-0.1f, 0.06f, -0.01f));
        PPart(rig.wingL, "w", Vector3.zero, b => b.Ellipsoid(new Vector3(-0.015f, -0.06f, -0.01f), new Vector3(0.03f, 0.09f, 0.065f), white, 10, 14, Quaternion.Euler(0, 0, -10), speckles));
        rig.wingR = PPivot(rig.body, "wingR", new Vector3(0.1f, 0.06f, -0.01f));
        PPart(rig.wingR, "w", Vector3.zero, b => b.Ellipsoid(new Vector3(0.015f, -0.06f, -0.01f), new Vector3(0.03f, 0.09f, 0.065f), white, 10, 14, Quaternion.Euler(0, 0, 10), speckles));
        rig.tail = PPart(rig.body, "tail", new Vector3(0, -0.08f, -0.1f), b => b.Ellipsoid(Vector3.zero, new Vector3(0.05f, 0.015f, 0.05f), white, 6, 10, Quaternion.Euler(-30, 0, 0)));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.17f, 0.01f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.12f, 0.1f, 0.1f), white, 18, 24);
            foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * 0.045f, 0.005f, 0.07f), new Vector3(0.045f, 0.045f, 0.03f), C(1f, 1f, 1f), 10, 14);   // 얼굴 둥근 판
            b.Ellipsoid(new Vector3(0, -0.03f, 0.1f), new Vector3(0.012f, 0.02f, 0.012f), beak, 6, 8, Quaternion.Euler(20, 0, 0));
            foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * 0.075f, 0.085f, 0f), new Vector3(0.02f, 0.035f, 0.015f), white, 6, 8, Quaternion.Euler(0, 0, s * -25f));   // 귀깃
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.01f, 0), 0.045f, 0.021f, 0.092f, gold, true, 1.6f);
        PPart(rig.body, "scarf", new Vector3(0, 0.09f, 0), b => b.Torus(Vector3.zero, Vector3.up, 0.08f, 0.018f, C(0.2f, 0.5f, 0.85f), 24, 8));
    }

    void BuildHamster(PetRig rig)
    {
        rig.gait = PetGait.Walk; rig.stepLen = 0.1f;
        Color fur = C(0.96f, 0.72f, 0.42f), white = C(1f, 0.98f, 0.94f), pink = C(1f, 0.7f, 0.74f);
        PLegs(rig, 0.05f, 0.06f, 0.07f, -0.07f, 0.05f, 0.018f, pink, pink, 1.2f);
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.12f, 0));
        PPart(rig.body, "torso", Vector3.zero, b => b.Ellipsoid(Vector3.zero, new Vector3(0.115f, 0.1f, 0.135f), fur, 18, 24, null, Belly(fur, white, -0.05f, 0.3f, 0.8f)));
        rig.tail = PPart(rig.body, "tail", new Vector3(0, 0.0f, -0.13f), b => b.Sphere(Vector3.zero, 0.015f, pink, 6, 8));
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.07f, 0.1f));
        PPart(rig.head, "skull", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.1f, 0.085f, 0.085f), fur, 18, 24, null, u => u.y < 0.0f && u.z > 0.1f ? white : fur);
            foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * 0.06f, -0.03f, 0.04f), new Vector3(0.05f, 0.042f, 0.045f), white, 10, 14);   // 볼주머니
            b.Ellipsoid(new Vector3(0, -0.012f, 0.085f), new Vector3(0.012f, 0.009f, 0.007f), pink, 6, 8);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.018f, 0), 0.042f, 0.016f, 0.07f, null, true, 1.9f);
        rig.earL = PPart(rig.head, "earL", new Vector3(-0.055f, 0.07f, -0.015f), b => { b.Ellipsoid(Vector3.zero, new Vector3(0.024f, 0.024f, 0.01f), fur, 8, 10); b.Ellipsoid(new Vector3(0, 0, 0.006f), new Vector3(0.015f, 0.015f, 0.005f), pink, 6, 8); });
        rig.earR = PPart(rig.head, "earR", new Vector3(0.055f, 0.07f, -0.015f), b => { b.Ellipsoid(Vector3.zero, new Vector3(0.024f, 0.024f, 0.01f), fur, 8, 10); b.Ellipsoid(new Vector3(0, 0, 0.006f), new Vector3(0.015f, 0.015f, 0.005f), pink, 6, 8); });
        PPart(rig.body, "seed", new Vector3(0, -0.02f, 0.13f), b => b.Ellipsoid(Vector3.zero, new Vector3(0.012f, 0.022f, 0.008f), C(0.35f, 0.3f, 0.28f), 6, 8, null, u => Mathf.Abs(u.x) < 0.3f ? C(0.9f, 0.88f, 0.82f) : C(0.3f, 0.27f, 0.25f)));
    }

    void BuildSnowPet(PetRig rig)
    {
        rig.gait = PetGait.Hop; rig.stepLen = 0.26f;
        Color snow = C(0.98f, 0.99f, 1f), shade = C(0.86f, 0.9f, 0.97f), coal = C(0.12f, 0.12f, 0.14f), carrot = C(1f, 0.55f, 0.15f), red = C(0.86f, 0.17f, 0.2f);
        rig.legs = new Transform[0]; rig.legLen = 0;
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.16f, 0));
        PPart(rig.body, "ball", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.16f, 0.15f, 0.16f), snow, 18, 26, null, u => Color.Lerp(snow, shade, Mathf.Clamp01(-u.y * 0.7f)));
            for (int k = 0; k < 2; k++) b.Sphere(new Vector3(0, 0.07f - k * 0.06f, 0.148f - k * 0.006f), 0.013f, coal, 6, 8);
        });
        rig.armL = PPivot(rig.body, "armL", new Vector3(-0.11f, 0.12f, 0));
        PPart(rig.armL, "stick", Vector3.zero, b => { b.Tube(new[] { Vector3.zero, new Vector3(-0.08f, 0.04f, 0), new Vector3(-0.15f, 0.09f, 0) }, new[] { 0.008f, 0.007f, 0.005f }, C(0.45f, 0.3f, 0.2f), 6); b.Tube(new[] { new Vector3(-0.1f, 0.055f, 0), new Vector3(-0.12f, 0.1f, 0.01f) }, new[] { 0.005f, 0.003f }, C(0.45f, 0.3f, 0.2f), 5); });
        rig.armR = PPivot(rig.body, "armR", new Vector3(0.11f, 0.12f, 0));
        PPart(rig.armR, "stick", Vector3.zero, b => { b.Tube(new[] { Vector3.zero, new Vector3(0.08f, 0.04f, 0), new Vector3(0.15f, 0.09f, 0) }, new[] { 0.008f, 0.007f, 0.005f }, C(0.45f, 0.3f, 0.2f), 6); b.Tube(new[] { new Vector3(0.1f, 0.055f, 0), new Vector3(0.12f, 0.1f, 0.01f) }, new[] { 0.005f, 0.003f }, C(0.45f, 0.3f, 0.2f), 5); });
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.22f, 0));
        PPart(rig.head, "ball", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.115f, 0.108f, 0.112f), snow, 18, 24);
            b.Tube(new[] { new Vector3(0, -0.01f, 0.1f), new Vector3(0, -0.018f, 0.2f) }, new[] { 0.02f, 0.002f }, carrot, 12);
            for (int k = -2; k <= 2; k++) b.Sphere(new Vector3(k * 0.022f, -0.05f - (2 - Mathf.Abs(k)) * 0.008f, 0.098f - Mathf.Abs(k) * 0.006f), 0.008f, coal, 5, 6);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.025f, 0), 0.042f, 0.017f, 0.097f, null, true, 1.9f);
        PPart(rig.body, "scarf", new Vector3(0, 0.13f, 0), b => { b.Torus(Vector3.zero, Vector3.up, 0.1f, 0.024f, red, 28, 8); b.Tube(new[] { new Vector3(0.06f, -0.01f, 0.08f), new Vector3(0.085f, -0.07f, 0.1f), new Vector3(0.09f, -0.12f, 0.095f) }, new[] { 0.022f, 0.021f, 0.02f }, red, 8); });
        PPart(rig.head, "hat", new Vector3(0, 0.09f, 0), b =>
        {
            b.Cylinder(Vector3.zero, new Vector3(0, 0.012f, 0), 0.1f, 0.1f, coal, 20);
            b.Cylinder(new Vector3(0, 0.01f, 0), new Vector3(0, 0.1f, 0), 0.065f, 0.068f, coal, 20);
            b.Cylinder(new Vector3(0, 0.02f, 0), new Vector3(0, 0.038f, 0), 0.069f, 0.069f, red, 20, false);
            b.Sphere(new Vector3(0.05f, 0.03f, 0.045f), 0.014f, C(0.2f, 0.6f, 0.3f), 6, 8);
        });
    }

    void BuildGinger(PetRig rig)
    {
        rig.gait = PetGait.Biped; rig.stepLen = 0.18f;
        Color cookie = C(0.78f, 0.47f, 0.24f), icing = C(1f, 1f, 1f), red = C(0.9f, 0.2f, 0.24f), green = C(0.25f, 0.7f, 0.35f), dark = C(0.2f, 0.12f, 0.08f);
        rig.legLen = 0.13f;
        rig.legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            var p = PPivot(rig.root, "leg" + i, new Vector3(s * 0.05f, 0.14f, 0));
            PPart(p, "legm", Vector3.zero, b =>
            {
                b.Ellipsoid(new Vector3(s * 0.006f, -0.07f, 0), new Vector3(0.045f, 0.08f, 0.038f), cookie, 10, 14);
                var z = 0.037f; b.Tube(new[] { new Vector3(s * 0.006f - 0.03f, -0.11f, z), new Vector3(s * 0.006f - 0.01f, -0.12f, z + 0.002f), new Vector3(s * 0.006f + 0.01f, -0.11f, z + 0.002f), new Vector3(s * 0.006f + 0.03f, -0.12f, z) }, new[] { 0.005f, 0.005f, 0.005f, 0.005f }, icing, 5);
            });
            rig.legs[i] = p;
        }
        rig.body = PPivot(rig.root, "body", new Vector3(0, 0.22f, 0));
        PPart(rig.body, "torso", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.095f, 0.1f, 0.04f), cookie, 14, 20);
            b.Sphere(new Vector3(0, 0.04f, 0.036f), 0.013f, red, 6, 8);
            b.Sphere(new Vector3(0, 0.0f, 0.04f), 0.013f, green, 6, 8);
            b.Sphere(new Vector3(0, -0.04f, 0.036f), 0.013f, red, 6, 8);
        });
        rig.armL = PPivot(rig.body, "armL", new Vector3(-0.08f, 0.05f, 0));
        PPart(rig.armL, "arm", Vector3.zero, b => { b.Ellipsoid(new Vector3(-0.06f, -0.015f, 0), new Vector3(0.075f, 0.036f, 0.034f), cookie, 10, 14, Quaternion.Euler(0, 0, 20)); b.Tube(new[] { new Vector3(-0.11f, 0.01f, 0.032f), new Vector3(-0.105f, -0.03f, 0.034f), new Vector3(-0.115f, -0.06f, 0.03f) }, new[] { 0.005f, 0.005f, 0.005f }, icing, 5); });
        rig.armR = PPivot(rig.body, "armR", new Vector3(0.08f, 0.05f, 0));
        PPart(rig.armR, "arm", Vector3.zero, b => { b.Ellipsoid(new Vector3(0.06f, -0.015f, 0), new Vector3(0.075f, 0.036f, 0.034f), cookie, 10, 14, Quaternion.Euler(0, 0, -20)); b.Tube(new[] { new Vector3(0.11f, 0.01f, 0.032f), new Vector3(0.105f, -0.03f, 0.034f), new Vector3(0.115f, -0.06f, 0.03f) }, new[] { 0.005f, 0.005f, 0.005f }, icing, 5); });
        rig.head = PPivot(rig.body, "head", new Vector3(0, 0.15f, 0));
        PPart(rig.head, "face", Vector3.zero, b =>
        {
            b.Ellipsoid(Vector3.zero, new Vector3(0.085f, 0.08f, 0.04f), cookie, 14, 20);
            var pts = new List<Vector3>(); var rr = new List<float>();
            for (int i = 0; i <= 8; i++) { float a = Mathf.Lerp(-0.9f, 0.9f, i / 8f); pts.Add(new Vector3(Mathf.Sin(a) * 0.035f, -0.02f - Mathf.Cos(a) * 0.018f, 0.037f)); rr.Add(0.0045f); }
            b.Tube(pts, rr, icing, 5);
        });
        rig.eyes = PEyes(rig.head, new Vector3(0, 0.02f, 0), 0.032f, 0.014f, 0.034f, null, true, 1.9f);
        PPart(rig.head, "bow", new Vector3(0, -0.085f, 0.035f), b => { b.Ellipsoid(new Vector3(-0.022f, 0, 0), new Vector3(0.022f, 0.014f, 0.01f), red, 6, 10); b.Ellipsoid(new Vector3(0.022f, 0, 0), new Vector3(0.022f, 0.014f, 0.01f), red, 6, 10); b.Sphere(Vector3.zero, 0.01f, C(0.75f, 0.12f, 0.16f), 6, 8); });
    }

    // ===== 움직임 =====
    // owner: 따라갈 캐릭터. 펫 위치·자세를 이번 프레임에 맞게
    void PetStep(PetRig rig, Transform owner, float ownerYaw, float dt, bool snap, Vector2? slot = null)
    {
        if (rig == null || rig.root == null || owner == null) return;
        var fwd = Quaternion.Euler(0, ownerYaw, 0) * Vector3.forward; var right = Quaternion.Euler(0, ownerYaw, 0) * Vector3.right;
        var so = slot ?? new Vector2(0.6f, -0.75f);
        var target = owner.position + fwd * so.y + right * so.x; target.y = owner.position.y;
        var pos = rig.root.position;
        var flat = new Vector3(target.x - pos.x, 0, target.z - pos.z);
        float d = flat.magnitude;
        if (snap || d > 9f) { rig.root.position = target; rig.vel = Vector3.zero; rig.yaw = ownerYaw; pos = target; d = 0; flat = Vector3.zero; }
        // 따라가기: 멀수록 빨리 (최대 8 m/s), 가까우면 멈춤
        Vector3 want = Vector3.zero;
        if (d > 0.35f) want = flat / d * Mathf.Min(8f, (d - 0.25f) * 2.6f + 0.6f);
        rig.vel = Vector3.Lerp(rig.vel, want, 1 - Mathf.Exp(-dt * 7f));
        pos += rig.vel * dt; pos.y = Mathf.Lerp(pos.y, target.y, 1 - Mathf.Exp(-dt * 10f));
        rig.root.position = pos;
        float spd = rig.vel.magnitude; rig.spd = Mathf.Lerp(rig.spd, spd, 1 - Mathf.Exp(-dt * 8f));
        bool moving = rig.spd > 0.12f;
        float wantYaw = moving ? Mathf.Atan2(rig.vel.x, rig.vel.z) * Mathf.Rad2Deg : (rig.idle > 1.2f ? Mathf.Atan2(owner.position.x - pos.x, owner.position.z - pos.z) * Mathf.Rad2Deg : rig.yaw);
        rig.yaw = Mathf.LerpAngle(rig.yaw, wantYaw, 1 - Mathf.Exp(-dt * (moving ? 9f : 3f)));
        rig.root.rotation = Quaternion.Euler(0, rig.yaw, 0);
        rig.idle = moving ? 0 : rig.idle + dt;
        float t = Time.time;
        // 걸음 위상: 한 걸음(stepLen)마다 반 바퀴
        rig.phase += dt * rig.spd / Mathf.Max(0.05f, rig.stepLen) * Mathf.PI;
        float k = Mathf.Clamp01(rig.spd / 1.2f);
        float breathe = 1f + Mathf.Sin(t * 2.4f + rig.yaw) * 0.018f;

        // 앉기 (네발: 1초 넘게 멈추면 천천히 앉음)
        rig.sitK = Mathf.MoveTowards(rig.sitK, rig.gait == PetGait.Walk && rig.idle > 3f ? 1f : 0f, dt * (moving ? 6f : 1.5f));

        Vector3 bodyOff = Vector3.zero; Quaternion bodyRot = Quaternion.identity;
        switch (rig.gait)
        {
            case PetGait.Walk:
                {
                    float sw = Mathf.Sin(rig.phase) * 32f * k;
                    if (rig.legs.Length == 4)
                    {
                        rig.legs[0].localRotation = Quaternion.Euler(sw - rig.sitK * 8f, 0, 0);
                        rig.legs[3].localRotation = Quaternion.Euler(sw - rig.sitK * 75f, 0, 0);
                        rig.legs[1].localRotation = Quaternion.Euler(-sw - rig.sitK * 8f, 0, 0);
                        rig.legs[2].localRotation = Quaternion.Euler(-sw - rig.sitK * 75f, 0, 0);
                        float hipDrop = rig.sitK * rig.legLen * 0.55f;
                        rig.legs[2].localPosition = new Vector3(rig.legs[2].localPosition.x, LegRestY(rig, 2) - hipDrop, rig.legs[2].localPosition.z);
                        rig.legs[3].localPosition = new Vector3(rig.legs[3].localPosition.x, LegRestY(rig, 3) - hipDrop, rig.legs[3].localPosition.z);
                    }
                    bodyOff.y = Mathf.Abs(Mathf.Sin(rig.phase)) * 0.018f * k - rig.sitK * rig.legLen * 0.3f;
                    bodyRot = Quaternion.Euler(-rig.sitK * 22f + Mathf.Sin(rig.phase * 2f) * 2f * k, 0, Mathf.Sin(rig.phase) * 3f * k);
                    if (rig.tail) rig.tail.localRotation = Quaternion.Euler(moving ? 10f : 20f + rig.sitK * 25f, Mathf.Sin(t * (moving ? 10f : 4f)) * (moving ? 22f : 14f), 0);
                    break;
                }
            case PetGait.Hop:
                {
                    if (moving || rig.hop > 0.01f)
                    {
                        rig.hop += dt * (moving ? Mathf.Lerp(2.2f, 3.4f, k) : 3f);
                        if (!moving && Mathf.Repeat(rig.hop, 1f) < dt * 3.5f) rig.hop = 0;
                    }
                    float h = rig.hop > 0 ? Mathf.Sin(Mathf.Repeat(rig.hop, 1f) * Mathf.PI) : 0;
                    bodyOff.y = h * 0.12f;
                    float squash = rig.hop > 0 ? 1f - Mathf.Max(0, 0.25f - Mathf.Repeat(rig.hop, 1f)) * 0.5f : 1f;
                    bodyRot = Quaternion.Euler(-h * 10f, 0, 0);
                    for (int i = 0; i < rig.legs.Length; i++) rig.legs[i].localRotation = Quaternion.Euler(i < 2 ? -h * 35f : h * 40f, 0, 0);
                    foreach (var l in rig.legs) l.localPosition = new Vector3(l.localPosition.x, LegRestY(rig, Array.IndexOf(rig.legs, l)) + h * 0.12f, l.localPosition.z);
                    if (rig.body) rig.body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
                    if (rig.armL) rig.armL.localRotation = Quaternion.Euler(0, 0, 20f + h * 35f + Mathf.Sin(t * 2f) * 5f);
                    if (rig.armR) rig.armR.localRotation = Quaternion.Euler(0, 0, -20f - h * 35f - Mathf.Sin(t * 2f) * 5f);
                    if (rig.tail) rig.tail.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 6f) * 10f, 0);
                    break;
                }
            case PetGait.Waddle:
                {
                    float sw = Mathf.Sin(rig.phase);
                    bodyRot = Quaternion.Euler(4f * k, 0, sw * 13f * Mathf.Max(k, 0.15f * Mathf.Sin(t * 1.3f)));
                    bodyOff.y = Mathf.Abs(sw) * 0.012f * k;
                    if (rig.legs.Length == 2)
                    {
                        rig.legs[0].localPosition = new Vector3(rig.legs[0].localPosition.x, LegRestY(rig, 0) + Mathf.Max(0, sw) * 0.03f * k, rig.legs[0].localPosition.z);
                        rig.legs[1].localPosition = new Vector3(rig.legs[1].localPosition.x, LegRestY(rig, 1) + Mathf.Max(0, -sw) * 0.03f * k, rig.legs[1].localPosition.z);
                    }
                    float flap = moving ? 25f + Mathf.Sin(t * 12f) * 12f : 6f + Mathf.Sin(t * 1.5f) * 4f;
                    if (rig.wingL) rig.wingL.localRotation = Quaternion.Euler(0, 0, -flap);
                    if (rig.wingR) rig.wingR.localRotation = Quaternion.Euler(0, 0, flap);
                    break;
                }
            case PetGait.Fly:
                {
                    // 움직이면 날아오르고, 멈추면 내려앉음
                    rig.flyH = Mathf.MoveTowards(rig.flyH, moving || rig.idle < 0.8f ? 1f : 0f, dt * (moving ? 2.5f : 0.9f));
                    bodyOff.y = rig.flyH * (0.75f + Mathf.Sin(t * 2.6f) * 0.06f);
                    bodyRot = Quaternion.Euler(rig.flyH * 12f * k, 0, Mathf.Sin(t * 1.7f) * 4f * rig.flyH);
                    float flap = rig.flyH > 0.05f ? Mathf.Lerp(10f, 70f, rig.flyH) * (0.5f + 0.5f * Mathf.Sin(t * 16f)) : 0f;
                    if (rig.wingL) rig.wingL.localRotation = Quaternion.Euler(0, rig.flyH * -20f, -flap - rig.flyH * 20f);
                    if (rig.wingR) rig.wingR.localRotation = Quaternion.Euler(0, rig.flyH * 20f, flap + rig.flyH * 20f);
                    foreach (var l in rig.legs) l.localRotation = Quaternion.Euler(rig.flyH * 50f, 0, 0);
                    foreach (var l in rig.legs) l.localPosition = new Vector3(l.localPosition.x, LegRestY(rig, Array.IndexOf(rig.legs, l)) + bodyOff.y, l.localPosition.z);
                    break;
                }
            case PetGait.Biped:
                {
                    float sw = Mathf.Sin(rig.phase) * 34f * k;
                    if (rig.legs.Length == 2) { rig.legs[0].localRotation = Quaternion.Euler(sw, 0, 0); rig.legs[1].localRotation = Quaternion.Euler(-sw, 0, 0); }
                    if (rig.armL) rig.armL.localRotation = Quaternion.Euler(-sw * 0.8f, 0, moving ? 10f : -25f + Mathf.Sin(t * 2f) * 4f);
                    if (rig.armR) rig.armR.localRotation = Quaternion.Euler(sw * 0.8f, 0, moving ? -10f : 25f - Mathf.Sin(t * 2f) * 4f);
                    bodyOff.y = Mathf.Abs(Mathf.Sin(rig.phase)) * 0.02f * k;
                    bodyRot = Quaternion.Euler(0, 0, Mathf.Sin(rig.phase) * 4f * k + (moving ? 0 : Mathf.Sin(t * 1.6f) * 3f));
                    break;
                }
        }
        if (rig.body)
        {
            rig.body.localPosition = rig.bodyRest + bodyOff;
            rig.body.localRotation = bodyRot;
            if (rig.gait != PetGait.Hop) rig.body.localScale = new Vector3(1f, breathe, 1f);
        }
        // 머리: 걸을 때 살짝 끄덕, 가만히 있으면 두리번
        if (rig.head)
        {
            if (Time.time > rig.lookAt) { rig.lookAt = Time.time + UnityEngine.Random.Range(1.5f, 4f); rig.lookYaw = moving ? 0 : UnityEngine.Random.Range(-40f, 40f); }
            float nod = moving ? Mathf.Sin(rig.phase * 2f) * 4f * k : Mathf.Sin(t * 1.1f) * 3f;
            float tilt = moving ? 0 : Mathf.Sin(t * 0.7f + 1.3f) * 8f;
            var hq = Quaternion.Euler(nod + (rig.gait == PetGait.Walk ? rig.sitK * 18f : 0), moving ? 0 : rig.lookYaw, tilt);
            rig.head.localRotation = Quaternion.Slerp(rig.head.localRotation, hq, 1 - Mathf.Exp(-dt * 5f));
        }
        // 귀: 가끔 쫑긋
        if (Time.time > rig.earAt) { rig.earAt = Time.time + UnityEngine.Random.Range(1.5f, 5f); rig.earKick = 1f; }
        rig.earKick = Mathf.MoveTowards(rig.earKick, 0, dt * 4f);
        float ek = Mathf.Sin(rig.earKick * Mathf.PI) * 18f;
        if (rig.earL) rig.earL.localRotation = Quaternion.Euler(-ek * 0.5f - (moving ? 8f * k : 0), 0, ek * 0.6f + Mathf.Sin(rig.phase * 2f) * 6f * k);
        if (rig.earR) rig.earR.localRotation = Quaternion.Euler(-(moving ? 8f * k : 0), 0, -Mathf.Sin(rig.phase * 2f) * 6f * k);
        // 눈 깜빡
        if (rig.eyes)
        {
            float bl = Time.time - rig.blinkAt;
            if (bl > 0.14f) { rig.blinkAt = Time.time + UnityEngine.Random.Range(2f, 5f); bl = -1; }
            float sy = bl >= 0 ? Mathf.Lerp(1f, 0.1f, Mathf.Sin(bl / 0.14f * Mathf.PI)) : 1f;
            rig.eyes.localScale = new Vector3(1f, sy, 1f);
        }
        // 루돌프 코: 반짝반짝
        if (rig.nose && rig.id == "rudolph")
        {
            float e = 0.55f + 0.35f * Mathf.Sin(t * 3.2f);
            var r = rig.nose.GetComponent<MeshRenderer>(); r.GetPropertyBlock(rig.mpb); rig.mpb.SetFloat("_Emit", e); r.SetPropertyBlock(rig.mpb);
        }
    }

    static float LegRestY(PetRig rig, int i)
    {
        if (rig.legRestY == null) { rig.legRestY = new float[rig.legs.Length]; for (int j = 0; j < rig.legs.Length; j++) rig.legRestY[j] = rig.legs[j].localPosition.y; }
        return i >= 0 && i < rig.legRestY.Length ? rig.legRestY[i] : 0;
    }

    // ===== 내 펫·다른 사람 펫 =====
    PetRig myPet; string myPetId = "";
    readonly Dictionary<string, PetRig> remotePets = new Dictionary<string, PetRig>();

    void SetPetVisible(PetRig rig, bool on) { if (rig != null && rig.root && rig.root.gameObject.activeSelf != on) rig.root.gameObject.SetActive(on); }

    void PetsUpdate(float dt)
    {
        if (world == null || me == null) return;
        if (PetGallery && phase != Phase.Lobby)
        {
            if (galleryPets.Count == 0) foreach (var id in PetIds) galleryPets.Add(MakePet(id));
            for (int i = 0; i < galleryPets.Count; i++)
            {
                var slot = new Vector2((i % 4 - 1.5f) * 0.85f, -1.2f - (i / 4) * 0.9f);
                PetStep(galleryPets[i], me.transform, yaw, dt, false, slot);
            }
            return;
        }
        // 내 펫
        string want = PetUnlocked ? CurPet : "";
        if (want != myPetId || (myPet != null && myPet.root == null))
        {
            if (myPet != null && myPet.root) Destroy(myPet.root.gameObject);
            myPet = null; myPetId = want;
            if (want != "") { myPet = MakePet(want); PetStep(myPet, me.transform, yaw, dt, true); }
        }
        if (myPet != null)
        {
            bool vis = phase != Phase.Lobby && me.gameObject.activeInHierarchy && !me.Hidden && !kitchenOpen && !GiftBusy && !playingGame;
            if (vis && !myPet.root.gameObject.activeSelf) { SetPetVisible(myPet, true); PetStep(myPet, me.transform, yaw, dt, true); }
            else SetPetVisible(myPet, vis);
            if (vis) PetStep(myPet, me.transform, yaw, dt, false);
        }
        // 다른 사람 펫
        foreach (var kv in remotes)
        {
            var r = kv.Value; string pid = r.net != null ? r.net.pet ?? "" : "";
            remotePets.TryGetValue(kv.Key, out var rp);
            if (rp != null && (rp.id != pid || rp.root == null)) { if (rp.root) Destroy(rp.root.gameObject); remotePets.Remove(kv.Key); rp = null; }
            if (rp == null && pid != "" && Array.IndexOf(PetIds, pid) >= 0 && r.c) { rp = MakePet(pid); remotePets[kv.Key] = rp; PetStep(rp, r.c.transform, r.c.transform.eulerAngles.y, dt, true); }
            if (rp == null || !r.c) continue;
            bool vis = r.c.gameObject.activeInHierarchy && !r.c.Hidden && (r.c.transform.position - KO).sqrMagnitude > 1600f;
            if (vis && !rp.root.gameObject.activeSelf) { SetPetVisible(rp, true); PetStep(rp, r.c.transform, r.c.transform.eulerAngles.y, dt, true); }
            else SetPetVisible(rp, vis);
            if (vis) PetStep(rp, r.c.transform, r.c.transform.eulerAngles.y, dt, false);
        }
        // 나간 사람 펫 정리
        List<string> gone = null;
        foreach (var kv in remotePets) if (!remotes.ContainsKey(kv.Key)) (gone ??= new List<string>()).Add(kv.Key);
        if (gone != null) foreach (var k in gone) { if (remotePets[k].root) Destroy(remotePets[k].root.gameObject); remotePets.Remove(k); }
    }
}
