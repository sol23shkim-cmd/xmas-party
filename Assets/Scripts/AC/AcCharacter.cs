// AcCharacter.cs — 동물의 숲풍 캐릭터 조립 + 걷기 애니메이션
// 종(species) 몸통 + (사람이면) 얼굴·머리카락 + 모자 + 목도리, 색은 전부 바꿀 수 있어요.
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AcLook
{
    public string species = "bear";            // bear cat rabbit dog fox panda lion pig human
    public bool female = true;                 // 사람일 때
    public string hair = "long";               // 여자: long bob twin pony bun / 남자: short spiky
    public string face = "f1";                 // 사람 얼굴 f1~f5 (남자는 f1, f2)
    public string hat = "santa";               // santa beanie earmuffs antlers tophat elf none
    public Color fur = new Color(0.72f, 0.5f, 0.34f);
    public Color sweater = new Color(0.86f, 0.24f, 0.24f);
    public Color scarf = new Color(0.98f, 0.96f, 0.92f);
    public Color hatColor = new Color(0.86f, 0.2f, 0.2f);
    public Color hairColor = new Color(0.45f, 0.28f, 0.18f);
    public Color mane = new Color(0.72f, 0.36f, 0.16f);

    public string Encode() => string.Join("|", species, female ? "f" : "m", hair, face, hat,
        Hex(fur), Hex(sweater), Hex(scarf), Hex(hatColor), Hex(hairColor), Hex(mane));
    public static AcLook Decode(string s)
    {
        var l = new AcLook();
        if (string.IsNullOrEmpty(s)) return l;
        var p = s.Split('|');
        try
        {
            l.species = p[0]; l.female = p[1] == "f"; l.hair = p[2]; l.face = p[3]; l.hat = p[4];
            l.fur = Col(p[5]); l.sweater = Col(p[6]); l.scarf = Col(p[7]); l.hatColor = Col(p[8]); l.hairColor = Col(p[9]); l.mane = Col(p[10]);
        }
        catch { }
        return l;
    }
    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    static Color Col(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.white;
}

public class AcCharacter : MonoBehaviour
{
    public AcLook look = new AcLook();
    public float speed;              // 현재 이동 속도 (m/s) — 걷기 흔들림 크기
    Transform body, armL, armR, legL, legR, tail, head;
    readonly List<GameObject> attached = new List<GameObject>();
    float phase, bob;
    string emote = ""; float emoteT = -10;
    public void PlayEmote(string kind) { emote = kind; emoteT = Time.time; }

    // 내 캐릭터 숨기기 (가까이 보는 화면에서 카메라가 몸 안으로 들어가 속이 비쳐 보이는 것 방지)
    bool hidden;
    public bool Hidden => hidden;
    public void SetHidden(bool h)
    {
        if (h == hidden) return; hidden = h;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !h;
    }

    // ===== 왼손에 든 물건 (포장한 선물) =====
    GameObject leftHold; float leftHoldSize;
    public bool HoldingLeft => leftHold != null;
    public bool carryBoth;   // 두 팔을 앞으로 뻗어 들기 (주방 게임에서 그릇·재료)
    // obj를 왼손에 쥠. size = 캐릭터 키 기준 크기(1 = 원래 크기). null이면 내려놓기
    public void HoldLeft(GameObject obj, float size)
    {
        if (leftHold && leftHold != obj) Destroy(leftHold);
        leftHold = obj; leftHoldSize = size;
        if (!obj || !armL) return;
        float minY = -0.3f;
        foreach (var mf in armL.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh || mf.gameObject == obj) continue; var b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, ((i & 4) >> 1) - 1));
                minY = Mathf.Min(minY, armL.InverseTransformPoint(mf.transform.TransformPoint(p)).y);
            }
        }
        obj.transform.SetParent(armL, false);
        float ws = transform.lossyScale.x / Mathf.Max(0.01f, armL.lossyScale.x);
        obj.transform.localPosition = new Vector3(0.02f * ws, minY - 0.02f * ws, 0.1f * ws);
        obj.transform.localRotation = Quaternion.Euler(50f, 0, 0);   // 팔을 앞으로 든 만큼 되돌려 상자가 똑바로
        obj.transform.localScale = Vector3.one * size * ws;
    }

    // ===== 베개 (침실 베개 싸움) =====
    public bool holdPillow; public Color pillowColor = new Color(0.98f, 0.86f, 0.9f);
    GameObject pillow; static Mesh pillowMesh; const float PillowK = 0.85f;   // 베개 크기 (캐릭터 키의 약 절반)
    void UpdatePillow()
    {
        if (holdPillow && pillow == null && armR)
        {
            // 팔 끝(손) 위치: 팔 모양의 가장 아래쪽
            float minY = -0.3f;
            foreach (var mf in armR.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue; var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, ((i & 4) >> 1) - 1));
                    var lp = armR.InverseTransformPoint(mf.transform.TransformPoint(p));
                    minY = Mathf.Min(minY, lp.y);
                }
            }
            pillow = new GameObject("Pillow");
            pillow.transform.SetParent(armR, false);
            // 손으로 베개 아래쪽 끝을 쥔 모양: 베개는 팔 방향으로 손 너머까지 뻗음
            float ws0 = transform.lossyScale.x / Mathf.Max(0.01f, armR.lossyScale.x);
            // 팔을 앞으로 뻗으면 베개가 손 위로 방패처럼 서 있음 (얇은 쪽이 앞, 긴 쪽이 위)
            pillow.transform.localPosition = new Vector3(0, minY + 0.01f * ws0, 0.16f * PillowK * ws0);
            pillow.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            pillow.AddComponent<MeshFilter>().sharedMesh = PillowMesh();
            var mr = pillow.AddComponent<MeshRenderer>(); mr.sharedMaterial = AcmLoader.Mat;
            var mpb = new MaterialPropertyBlock(); mr.GetPropertyBlock(mpb);
            pillow.transform.localScale = Vector3.one;
            tintPillow(mr);
        }
        else if (!holdPillow && pillow != null) { Destroy(pillow); pillow = null; }
        if (pillow)
        {
            // 맞거나 휘두를 때 베개도 말랑하게 출렁
            float et = Time.time - emoteT;
            float wob = (emote == "swing" || emote == "bonk") && et < 0.8f ? Mathf.Exp(-et * 6f) * Mathf.Sin(et * 30f) * 0.18f : 0;
            float ws = transform.lossyScale.x / Mathf.Max(0.01f, armR ? armR.lossyScale.x : 1f);   // 팔 크기와 상관없이 캐릭터에 맞는 크기
            pillow.transform.localScale = new Vector3(1 + wob, 1 - wob * 0.6f, 1 + wob) * PillowK * ws;
        }
    }
    void tintPillow(MeshRenderer mr)
    {
        // 정점 색을 베개 색으로 (가장자리는 하얗게, 가운데 줄무늬)
        var m = Instantiate(PillowMesh()); var cols = m.colors; var vs = m.vertices;
        for (int i = 0; i < vs.Length; i++)
        {
            float edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(vs[i].x) / 0.3f, Mathf.Abs(vs[i].y) / 0.21f) - 0.55f) * 2f;
            float stripe = Mathf.Abs(Mathf.Sin(vs[i].y * 30f)) > 0.85f ? 0.08f : 0f;
            cols[i] = Color.Lerp(pillowColor, Color.white, edge * 0.7f) * (1 - stripe);
            cols[i].a = 1;
        }
        m.colors = cols; mr.GetComponent<MeshFilter>().sharedMesh = m;
    }
    // 통통한 베개: 납작한 초타원체 + 네 귀퉁이가 살짝 뾰족
    static Mesh PillowMesh()
    {
        if (pillowMesh) return pillowMesh;
        int nu = 24, nv = 16; var vs = new List<Vector3>(); var ns = new List<Vector3>(); var cs = new List<Color>(); var ts = new List<int>();
        Vector3 R = new Vector3(0.3f, 0.21f, 0.085f);
        for (int j = 0; j <= nv; j++)
        {
            float v = -Mathf.PI / 2 + Mathf.PI * j / nv;
            for (int i = 0; i <= nu; i++)
            {
                float u = -Mathf.PI + 2 * Mathf.PI * i / nu;
                float cv = Mathf.Cos(v), sv = Mathf.Sin(v), cu = Mathf.Cos(u), su = Mathf.Sin(u);
                float e = 0.35f;   // 네모에 가까운 둥근 모양
                float x = Mathf.Sign(cv * cu) * Mathf.Pow(Mathf.Abs(cv * cu), e);
                float y = Mathf.Sign(cv * su) * Mathf.Pow(Mathf.Abs(cv * su), e);
                float z = Mathf.Sign(sv) * Mathf.Pow(Mathf.Abs(sv), 0.8f);
                float corner = Mathf.Abs(x * y);          // 귀퉁이는 얇고 뾰족
                var p = new Vector3(x * R.x * (1 + corner * 0.08f), y * R.y * (1 + corner * 0.08f), z * R.z * (1 - corner * 0.6f));
                vs.Add(p); ns.Add(new Vector3(p.x / (R.x * R.x), p.y / (R.y * R.y), p.z / (R.z * R.z)).normalized); cs.Add(Color.white);
            }
        }
        for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++)
            {
                int a = j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                ts.Add(a); ts.Add(b); ts.Add(c); ts.Add(b); ts.Add(d); ts.Add(c);
            }
        pillowMesh = new Mesh { name = "pillow" };
        pillowMesh.SetVertices(vs); pillowMesh.SetNormals(ns); pillowMesh.SetColors(cs); pillowMesh.SetTriangles(ts, 0);
        var uvs = new List<Vector2>(); for (int k = 0; k < vs.Count; k++) uvs.Add(new Vector2(1f, 0)); pillowMesh.SetUVs(0, uvs);   // uv.x = 그늘(AO) 1 = 밝게
        pillowMesh.RecalculateNormals(); pillowMesh.RecalculateBounds();
        return pillowMesh;
    }

    public static AcCharacter Create(AcLook look, Transform parent = null)
    {
        var go = new GameObject("Character");
        if (parent) go.transform.SetParent(parent, false);
        var c = go.AddComponent<AcCharacter>();
        c.Rebuild(look);
        return c;
    }

    public void Rebuild(AcLook l)
    {
        look = l; hidden = false;
        if (body) Destroy(body.gameObject);
        attached.Clear();
        var tints = new Dictionary<string, Color>
        {
            { "fur", l.fur }, { "sweater", l.sweater }, { "scarf", l.scarf }, { "hat", l.hatColor }, { "hair", l.hairColor }, { "mane", l.mane },
        };
        var bodyGo = AcmLoader.Spawn("char_" + l.species, transform, tints);
        body = bodyGo.transform;
        armL = AcmLoader.Find(bodyGo, "armL"); armR = AcmLoader.Find(bodyGo, "armR");
        legL = AcmLoader.Find(bodyGo, "legL"); legR = AcmLoader.Find(bodyGo, "legR");
        tail = AcmLoader.Find(bodyGo, "tail");
        var sp = AcCatalog.Species(l.species);
        float top = sp != null ? sp.top : 1.04f;
        bool human = l.species == "human";
        if (santa) { BuildSanta(); if (skating) { skating = false; SetSkates(true); } return; }   // 산타: 꾸미기와 상관없는 전용 모습
        attached.Add(AcmLoader.Spawn("scarf", body, tints));
        if (human)
        {
            attached.Add(AcmLoader.Spawn("face_" + l.face, body, tints, null, false));
            if (!string.IsNullOrEmpty(l.hair)) attached.Add(AcmLoader.Spawn("hair_" + l.hair, body, tints));
        }
        if (!string.IsNullOrEmpty(l.hat) && l.hat != "none" && AcmLoader.Exists("hat_" + l.hat))
        {
            var h = AcmLoader.Spawn("hat_" + l.hat, body, tints);
            float hatTop = AcCatalog.Data.hatTop > 0 ? AcCatalog.Data.hatTop : 1.04f;
            if (human && !string.IsNullOrEmpty(l.hair))
            {
                // 사람은 머리카락 부피만큼 모자를 키우고, 이마 쪽으로 깊게 눌러 씌움 (귀마개는 높이 그대로)
                const float sc = 1.12f, py = 0.80f, pz = -0.03f;
                h.transform.localScale = Vector3.one * sc;
                h.transform.localPosition = new Vector3(0, py * (1 - sc) - (l.hat == "earmuffs" ? 0f : 0.075f), pz * (1 - sc) - 0.01f);
            }
            else h.transform.localPosition = new Vector3(0, human ? 0f : top - hatTop, 0);
            attached.Add(h);
        }
        if (skating) { skating = false; SetSkates(true); }   // 다시 만들 때 스케이트도 다시 신김
    }

    // ===== 스케이트 =====
    public bool skating;
    GameObject skL, skR;
    public Color skateColor = new Color(0.98f, 0.97f, 0.95f);
    public void SetSkates(bool on)
    {
        if (on == skating && ((skL != null) == on || (santa && (sled != null) == on))) return;
        skating = on;
        if (skL) Destroy(skL); if (skR) Destroy(skR);
        skL = skR = null;
        if (sled) { Destroy(sled); sled = null; }
        if (on && santa) { BuildSled(); return; }   // 산타는 스케이트 대신 썰매
        if (!on || !legL || !legR || !AcmLoader.Exists("skate_boot")) return;
        var t = new Dictionary<string, Color> { { "boot", skateColor }, { "cuff", look.scarf } };
        skL = AcmLoader.Spawn("skate_boot", legL, t); skL.transform.localPosition = new Vector3(-0.002f, -0.125f, 0.04f);
        skR = AcmLoader.Spawn("skate_boot", legR, t); skR.transform.localPosition = new Vector3(0.002f, -0.125f, 0.04f);
    }

    // ===== 쿠키 달리기 자세 =====
    public int runPose;      // 0 보통, 1 달리기, 2 점프, 3 2단 점프(공중제비), 4 슬라이드, 5 다침, 6 쓰러짐, 7 환호, 8 서서 기다림
    public float runT;       // 지금 자세가 된 지 몇 초
    float runPhase;
    void RunAnim(float dt)
    {
        runT += dt;
        runPhase += dt * 17f;
        float s = Mathf.Sin(runPhase), c = Mathf.Cos(runPhase);
        Quaternion br = Quaternion.identity; Vector3 bp = Vector3.zero;
        Vector3 aL = new Vector3(0, 0, -4), aR = new Vector3(0, 0, 4), lL = Vector3.zero, lR = Vector3.zero;
        const float cy = 0.5f;
        switch (runPose)
        {
            case 1:   // 달리기: 팔다리 크게, 몸은 앞으로
                lL = new Vector3(-s * 58f, 0, 0); lR = new Vector3(s * 58f, 0, 0);
                aL = new Vector3(s * 65f, 0, -8f); aR = new Vector3(-s * 65f, 0, 8f);
                br = Quaternion.Euler(13f, 0, s * 3f); bp = new Vector3(0, Mathf.Abs(c) * 0.07f, 0);
                break;
            case 2:   // 점프: 한 다리 접고 두 팔 위로
                lL = new Vector3(-75f, 0, 0); lR = new Vector3(30f, 0, 0);
                aL = new Vector3(-20f, 0, -150f); aR = new Vector3(-20f, 0, 150f);
                br = Quaternion.Euler(-6f, 0, 0);
                break;
            case 3:   // 2단 점프: 웅크리고 앞으로 한 바퀴
                {
                    float a = Mathf.SmoothStep(0, 1, Mathf.Clamp01(runT / 0.42f)) * 360f;
                    lL = lR = new Vector3(-95f, 0, 0); aL = new Vector3(-110f, 0, -20f); aR = new Vector3(-110f, 0, 20f);
                    br = Quaternion.Euler(a, 0, 0); bp = new Vector3(0, cy, 0) - br * new Vector3(0, cy, 0);
                }
                break;
            case 4:   // 슬라이드: 뒤로 누워 미끄러짐
                br = Quaternion.Euler(-72f, 0, Mathf.Sin(runT * 30f) * 2f); bp = new Vector3(0, 0.16f, -0.05f);
                aL = new Vector3(-150f, 0, -25f); aR = new Vector3(-150f, 0, 25f);
                lL = new Vector3(-8f, 0, 0); lR = new Vector3(8f, 0, 0);
                break;
            case 5:   // 다침
                br = Quaternion.Euler(-22f, 0, Mathf.Sin(runT * 40f) * 8f);
                aL = new Vector3(0, 0, -110f); aR = new Vector3(0, 0, 110f);
                lL = new Vector3(-30f, 0, 0); lR = new Vector3(20f, 0, 0);
                break;
            case 6:   // 쓰러짐
                {
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(runT / 0.6f));
                    br = Quaternion.Euler(-88f * k, 0, 0); bp = new Vector3(0, 0.12f * k, 0);
                    aL = new Vector3(0, 0, -100f * k); aR = new Vector3(0, 0, 100f * k);
                }
                break;
            case 7:   // 환호
                {
                    float w = Mathf.Sin(runT * 12f);
                    aL = new Vector3(0, 0, -150f - w * 15f); aR = new Vector3(0, 0, 150f + w * 15f);
                    bp = new Vector3(0, Mathf.Abs(Mathf.Sin(runT * 6f)) * 0.18f, 0);
                    lL = new Vector3(-Mathf.Abs(Mathf.Sin(runT * 6f)) * 25f, 0, 0); lR = lL;
                }
                break;
            default:  // 8: 서서 기다림 (숨쉬기)
                bp = new Vector3(0, Mathf.Sin(runT * 2.5f) * 0.01f, 0);
                aL = new Vector3(Mathf.Sin(runT * 2f) * 6f, 0, -8f); aR = new Vector3(-Mathf.Sin(runT * 2f) * 6f, 0, 8f);
                break;
        }
        if (armL) armL.localRotation = Quaternion.Euler(aL);
        if (armR) armR.localRotation = Quaternion.Euler(aR);
        ArmsClearHead();
        if (legL) legL.localRotation = Quaternion.Euler(lL);
        if (legR) legR.localRotation = Quaternion.Euler(lR);
        if (tail) tail.localRotation = Quaternion.Euler(0, s * 25f, 0);
        if (body) { body.localRotation = br; body.localPosition = bp; }
    }
    public void SetRunPose(int p) { if (p != runPose) { runPose = p; runT = 0; } }

    // ===== 산타 (이름이 '산타'인 캐릭터만. 꾸미기 칸과 상관없이 전용 모습 + 밖에서는 썰매) =====
    public bool santa; GameObject sled; float santaR = 0.25f, santaFootY = -0.6f;
    public float SantaR => santaR;
    public void SetSanta(bool on) { if (on == santa) return; santa = on; Rebuild(look); }

    static float BoneMinY(Transform bone)
    {
        float minY = -0.12f;
        if (!bone) return minY;
        foreach (var mf in bone.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh) continue; var b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, ((i & 4) >> 1) - 1));
                minY = Mathf.Min(minY, bone.InverseTransformPoint(mf.transform.TransformPoint(p)).y);
            }
        }
        return minY;
    }
    static GameObject PbObj(string name, AcPB b, Transform parent, Vector3 lp)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = lp;
        go.AddComponent<MeshFilter>().sharedMesh = b.M(name);
        go.AddComponent<MeshRenderer>().sharedMaterial = AcmLoader.Mat;
        return go;
    }

    void BuildSanta()
    {
        // 사람 모델의 뼈대(팔·다리 자리)는 그대로 쓰고, 보이는 모양만 산타로 새로 만듦
        var sp = AcCatalog.Species("human"); float top = sp != null ? sp.top : 1.07f;
        // 얼굴 자리를 재려고 얼굴 모양을 잠깐 붙였다가 뗌
        Bounds fb = new Bounds(new Vector3(0, top - 0.27f, 0.27f), new Vector3(0.22f, 0.14f, 0.02f));
        var face = AcmLoader.Spawn("face_f1", body, null, null, false);
        bool has = false;
        foreach (var mf in face.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh) continue; var b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = body.InverseTransformPoint(mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, ((i & 4) >> 1) - 1))));
                if (!has) { fb = new Bounds(p, Vector3.zero); has = true; } else fb.Encapsulate(p);
            }
        }
        DestroyImmediate(face);
        float armLen = Mathf.Abs(BoneMinY(armL)), legLen = Mathf.Abs(BoneMinY(legL));
        Vector3 hipL = legL ? body.InverseTransformPoint(legL.position) : new Vector3(-0.08f, 0.2f, 0);
        // 원래 모양은 지움 (뼈대 Transform은 남김)
        foreach (var r in body.GetComponentsInChildren<Renderer>(true)) Destroy(r);

        float R = Mathf.Clamp((top - fb.center.y) * 0.98f, 0.18f, 0.36f); santaR = R;
        var hc = new Vector3(0, fb.center.y + 0.01f, fb.max.z - R * 0.96f);
        float neckY = hc.y - R * 0.82f, hipY = hipL.y;
        santaFootY = hipY - legLen;
        Color red = new Color(0.84f, 0.12f, 0.14f), redD = new Color(0.66f, 0.07f, 0.1f), fur = new Color(0.99f, 0.98f, 0.96f), furS = new Color(0.88f, 0.89f, 0.92f);
        Color skin = new Color(1f, 0.84f, 0.73f), blush = new Color(1f, 0.6f, 0.62f), black = new Color(0.1f, 0.09f, 0.1f), gold = new Color(1f, 0.8f, 0.25f);
        Color beardC = new Color(0.96f, 0.96f, 0.98f);

        // 몸통: 통통한 빨간 외투 + 흰 털 단 + 검은 허리띠
        var tb = new AcPB { detail = 1.4f };
        float ty = (hipY + neckY) * 0.5f + R * 0.05f, th = (neckY - hipY) * 0.62f + R * 0.12f;
        tb.Ellipsoid(new Vector3(0, ty, hc.z - R * 0.05f), new Vector3(R * 0.98f, th, R * 0.88f), red, 16, 24, null, u => Color.Lerp(red, redD, Mathf.Clamp01(-u.y * 0.6f)));
        tb.Torus(new Vector3(0, ty - th * 0.82f, hc.z - R * 0.05f), Vector3.up, R * 0.62f, R * 0.17f, fur, 28, 10);                  // 아래 털 단
        tb.Torus(new Vector3(0, ty - th * 0.2f, hc.z - R * 0.05f), Vector3.up, R * 0.94f, R * 0.075f, black, 30, 8);                   // 허리띠
        tb.Box(new Vector3(0, ty - th * 0.2f, hc.z - R * 0.05f + R * 0.9f), new Vector3(R * 0.32f, R * 0.24f, R * 0.06f), gold);        // 버클
        tb.Box(new Vector3(0, ty - th * 0.2f, hc.z - R * 0.05f + R * 0.93f), new Vector3(R * 0.18f, R * 0.12f, R * 0.04f), black);
        tb.Ellipsoid(new Vector3(0, ty + th * 0.25f, hc.z + R * 0.74f), new Vector3(R * 0.14f, th * 0.55f, R * 0.08f), fur, 8, 12);    // 앞섶 털
        PbObj("santa_torso", tb, body, Vector3.zero);

        // 머리: 둥근 얼굴 + 웃는 눈, 덥수룩한 눈썹, 동그란 코, 턱선을 따라 감싸는 곱슬 수염, 말려 올라간 콧수염, 늘어진 모자
        var hb = new AcPB { detail = 1.6f };
        hb.Ellipsoid(hc, new Vector3(R, R * 0.97f, R * 0.95f), skin, 20, 28, null, u => Color.Lerp(skin, new Color(1f, 0.78f, 0.7f), Mathf.Clamp01(u.z * 0.6f - 0.1f)));
        float fz = hc.z + R * 0.92f;
        foreach (float sg in new[] { -1f, 1f })
        {
            // 웃는 눈: 작은 검은 눈 + 위 눈꺼풀이 살짝 덮어 반달 모양
            var e = new Vector3(sg * R * 0.3f, hc.y + R * 0.12f, fz - R * 0.05f);
            hb.Ellipsoid(e, new Vector3(R * 0.07f, R * 0.085f, R * 0.05f), black, 8, 12);
            hb.Ellipsoid(e + new Vector3(-R * 0.022f, R * 0.03f, R * 0.04f), Vector3.one * R * 0.022f, Color.white, 5, 6);
            hb.Ellipsoid(e + new Vector3(0, R * 0.065f, R * 0.012f), new Vector3(R * 0.095f, R * 0.045f, R * 0.055f), skin, 8, 12);
            // 덥수룩한 흰 눈썹: 작은 덩어리 세 개가 바깥으로 처짐
            for (int k = 0; k < 3; k++)
                hb.Ellipsoid(new Vector3(sg * R * (0.18f + k * 0.11f), hc.y + R * (0.3f - k * 0.035f), fz - R * (0.07f + k * 0.05f)), new Vector3(R * 0.085f, R * 0.06f, R * 0.06f), k == 1 ? Color.white : beardC, 7, 10);
            hb.Ellipsoid(new Vector3(sg * R * 0.52f, hc.y - R * 0.08f, fz - R * 0.2f), new Vector3(R * 0.16f, R * 0.1f, R * 0.07f), blush, 8, 12);   // 볼
            // 옆·뒷머리: 모자 아래로 흰 곱슬머리
            for (int k = 0; k < 4; k++)
            {
                float a = (35f + k * 30f) * Mathf.Deg2Rad;
                var hd = new Vector3(sg * Mathf.Sin(a), 0, Mathf.Cos(a));
                hb.Ellipsoid(hc + hd * R * 0.93f + Vector3.up * (R * 0.18f - k * R * 0.03f), new Vector3(R * 0.2f, R * 0.22f, R * 0.18f), k % 2 == 0 ? beardC : Color.white, 8, 10);
            }
        }
        hb.Ellipsoid(new Vector3(0, hc.y - R * 0.04f, fz + R * 0.05f), new Vector3(R * 0.14f, R * 0.12f, R * 0.12f), new Color(1f, 0.66f, 0.6f), 12, 16);   // 코
        hb.Ellipsoid(new Vector3(-R * 0.04f, hc.y - R * 0.0f, fz + R * 0.15f), Vector3.one * R * 0.03f, new Color(1f, 0.86f, 0.82f), 5, 6);
        // 수염: 머리 둘레를 따라 붙은 두꺼운 껍질. 옆은 구레나룻부터, 앞은 입 아래부터 시작, 턱 아래로 둥글게 늘어짐. 겉면은 곱슬곱슬 물결
        {
            int nT = 26, nP = 16;
            var outer = new int[nT + 1, nP + 1]; var inner = new int[nT + 1, nP + 1];
            float thMax = 105f * Mathf.Deg2Rad;
            for (int i = 0; i <= nT; i++)
            {
                float bth = Mathf.Lerp(-thMax, thMax, i / (float)nT);
                float side = Mathf.Abs(bth) / thMax;                    // 0 = 앞, 1 = 귀 옆
                float phTop = Mathf.Lerp(26f, -8f, Mathf.SmoothStep(0.25f, 0.85f, side)) * Mathf.Deg2Rad;   // 위쪽 경계: 앞은 입 아래, 옆은 귀 높이
                float phBot = Mathf.Lerp(98f, 70f, side) * Mathf.Deg2Rad;
                for (int j = 0; j <= nP; j++)
                {
                    float v = j / (float)nP;
                    float ph = Mathf.Lerp(phTop, phBot, v);
                    var dir = new Vector3(Mathf.Sin(bth) * Mathf.Cos(ph), -Mathf.Sin(ph), Mathf.Cos(bth) * Mathf.Cos(ph));
                    float edge = Mathf.Min(Mathf.SmoothStep(0, 1, v * 4f), Mathf.SmoothStep(0, 1, (1 - side) * 6f));   // 가장자리는 얇게
                    float curl = 0.5f + 0.5f * Mathf.Sin(bth * 9f + v * 7f) * Mathf.Sin(v * 11f + bth * 3f);
                    float thick = R * (0.08f + 0.14f * edge + 0.05f * curl * edge);
                    float hang = R * 0.62f * Mathf.Pow(Mathf.Max(0, Mathf.Cos(bth * 1.25f)), 2f) * Mathf.SmoothStep(0, 1, v);   // 턱 아래로 늘어짐 (가운데가 가장 길게)
                    var po = hc + dir * (R * 0.99f + thick) + Vector3.down * hang + Vector3.forward * hang * 0.35f;
                    var pi2 = hc + dir * (R * 0.97f) + Vector3.down * hang * 0.6f;
                    var c = Color.Lerp(new Color(0.84f, 0.85f, 0.88f), Color.white, 0.55f + 0.45f * curl) ;
                    outer[i, j] = hb.V(po, (dir + Vector3.down * 0.25f).normalized, c, 0.95f);
                    inner[i, j] = hb.V(pi2, -dir, new Color(0.85f, 0.86f, 0.9f), 0.7f);
                }
            }
            for (int i = 0; i < nT; i++)
                for (int j = 0; j < nP; j++)
                {
                    hb.Quad(outer[i, j], outer[i + 1, j], outer[i + 1, j + 1], outer[i, j + 1]);
                    hb.Quad(inner[i, j], inner[i, j + 1], inner[i + 1, j + 1], inner[i + 1, j]);
                }
            for (int i = 0; i < nT; i++) { hb.Quad(outer[i, nP], outer[i + 1, nP], inner[i + 1, nP], inner[i, nP]); hb.Quad(outer[i, 0], inner[i, 0], inner[i + 1, 0], outer[i + 1, 0]); }
        }
        // 콧수염: 코 밑에서 양쪽으로 통통하게 뻗다가 끝이 살짝 말려 올라감
        foreach (float sg in new[] { -1f, 1f })
        {
            var mp = new List<Vector3>(); var mr2 = new List<float>();
            for (int k = 0; k <= 10; k++)
            {
                float t = k / 10f;
                float x = sg * (R * 0.02f + t * R * 0.5f), y = hc.y - R * 0.2f - Mathf.Sin(t * Mathf.PI * 0.8f) * R * 0.1f + Mathf.Max(0, t - 0.75f) * R * 0.5f;
                float z = fz + R * 0.06f - t * t * R * 0.3f;
                mp.Add(new Vector3(x, y, z)); mr2.Add(R * (0.1f * Mathf.Sin(Mathf.Lerp(0.5f, 2.9f, t)) + 0.012f));
            }
            hb.Tube(mp, mr2, Color.white, 12);
        }
        hb.Ellipsoid(new Vector3(0, hc.y - R * 0.36f, fz - R * 0.02f), new Vector3(R * 0.09f, R * 0.045f, R * 0.04f), new Color(0.78f, 0.32f, 0.34f), 6, 8);   // 입 (수염 사이로 살짝)
        // 모자: 흰 털 테 + 빨간 고깔이 뒤로 축 늘어짐 + 흰 방울
        var brimC = new Vector3(0, hc.y + R * 0.52f, hc.z - R * 0.04f);
        hb.Torus(brimC, Quaternion.Euler(-8f, 0, 0) * Vector3.up, R * 0.88f, R * 0.17f, fur, 32, 10);
        var hp = new List<Vector3>(); var hr = new List<float>();
        for (int i = 0; i <= 12; i++) { float t = i / 12f; hp.Add(brimC + new Vector3(t * R * 0.28f, Mathf.Sin(t * 2.2f) * R * 0.75f, -t * t * R * 0.95f)); hr.Add(Mathf.Lerp(R * 0.86f, R * 0.06f, Mathf.Pow(t, 0.8f))); }
        hb.Tube(hp, hr, red, 20);
        hb.Ellipsoid(hp[12] + new Vector3(0, -R * 0.05f, -R * 0.05f), Vector3.one * R * 0.2f, fur, 10, 14);
        PbObj("santa_head", hb, body, Vector3.zero);

        // 팔: 빨간 소매 + 흰 소맷단 + 흰 장갑
        foreach (var arm in new[] { armL, armR })
        {
            if (!arm) continue;
            float L = Mathf.Max(0.12f, armLen), ar = R * 0.2f;
            var ab = new AcPB { detail = 1.3f };
            ab.Tube(new[] { new Vector3(0, ar * 0.3f, 0), new Vector3(0, -L * 0.5f, 0), new Vector3(0, -L * 0.78f, 0) }, new[] { ar * 1.15f, ar * 1.05f, ar }, red, 14);
            ab.Torus(new Vector3(0, -L * 0.76f, 0), Vector3.up, ar * 0.95f, ar * 0.32f, fur, 18, 8);
            ab.Ellipsoid(new Vector3(0, -L * 0.94f, ar * 0.1f), new Vector3(ar * 1.05f, ar * 1.0f, ar * 0.95f), Color.white, 10, 14);
            PbObj("santa_arm", ab, arm, Vector3.zero);
        }
        // 다리: 빨간 바지 + 검은 장화(흰 털 테)
        foreach (var leg in new[] { legL, legR })
        {
            if (!leg) continue;
            float L = Mathf.Max(0.08f, legLen), lr = R * 0.22f;
            var lb = new AcPB { detail = 1.3f };
            lb.Tube(new[] { new Vector3(0, lr * 0.4f, 0), new Vector3(0, -L * 0.45f, 0) }, new[] { lr * 1.1f, lr }, red, 14);
            lb.Tube(new[] { new Vector3(0, -L * 0.42f, 0), new Vector3(0, -L * 0.85f, 0) }, new[] { lr * 1.02f, lr * 1.02f }, black, 14);
            lb.Torus(new Vector3(0, -L * 0.42f, 0), Vector3.up, lr * 0.98f, lr * 0.3f, fur, 18, 8);
            lb.Ellipsoid(new Vector3(0, -L + lr * 0.45f, lr * 0.45f), new Vector3(lr * 1.1f, lr * 0.62f, lr * 1.6f), black, 10, 14);
            PbObj("santa_leg", lb, leg, Vector3.zero);
        }
    }

    // 썰매: 빨간 몸통, 금색 장식, 앞이 말린 금빛 날 (캐릭터가 안에 서 있음)
    void BuildSled()
    {
        float R = santaR;
        var b = new AcPB { detail = 1.3f };
        Color red = new Color(0.8f, 0.1f, 0.13f), redD = new Color(0.6f, 0.06f, 0.09f), gold = new Color(1f, 0.78f, 0.25f), wood = new Color(0.45f, 0.28f, 0.18f), velvet = new Color(0.3f, 0.12f, 0.35f);
        float L = R * 5.2f, W = R * 2.4f, y0 = R * 0.75f, H = R * 1.25f;
        // 바닥·옆판
        b.Box(new Vector3(0, y0 + R * 0.08f, 0), new Vector3(W, R * 0.16f, L * 0.82f), redD);
        foreach (float sg in new[] { -1f, 1f })
        {
            var pts = new List<Vector3>(); var rr = new List<float>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f, z = Mathf.Lerp(-L * 0.42f, L * 0.42f, t);
                float hh = Mathf.Lerp(H * 1.05f, H * 0.55f, Mathf.SmoothStep(0, 1, t)) + Mathf.Sin(t * Mathf.PI) * R * 0.05f;
                pts.Add(new Vector3(sg * W * 0.5f, y0 + hh * 0.5f, z)); rr.Add(hh * 0.5f);
            }
            b.Tube(pts, rr, red, 12);
            var tp = new List<Vector3>(); var tr = new List<float>();
            for (int i = 0; i <= 12; i++) { float t = i / 12f; float hh = Mathf.Lerp(H * 1.05f, H * 0.55f, Mathf.SmoothStep(0, 1, t)); tp.Add(new Vector3(sg * W * 0.5f, y0 + hh, Mathf.Lerp(-L * 0.42f, L * 0.42f, t))); tr.Add(R * 0.07f); }
            b.Tube(tp, tr, gold, 8);   // 위 테두리 금장식
            // 날: 바닥 아래 금빛, 앞쪽에서 위로 말려 올라감
            var rp = new List<Vector3>(); var rrr = new List<float>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                if (t < 0.75f) rp.Add(new Vector3(sg * W * 0.42f, R * 0.08f, Mathf.Lerp(-L * 0.5f, L * 0.45f, t / 0.75f)));
                else { float a = (t - 0.75f) / 0.25f * Mathf.PI * 1.2f; rp.Add(new Vector3(sg * W * 0.42f, R * 0.08f + R * 0.55f * (1 - Mathf.Cos(a)) * 0.9f, L * 0.45f + Mathf.Sin(a) * R * 0.55f)); }
                rrr.Add(R * 0.06f);
            }
            b.Tube(rp, rrr, gold, 8);
            for (int k = 0; k < 3; k++) { float z = Mathf.Lerp(-L * 0.32f, L * 0.3f, k / 2f); b.Cylinder(new Vector3(sg * W * 0.42f, R * 0.08f, z), new Vector3(sg * W * 0.45f, y0 + R * 0.05f, z), R * 0.05f, R * 0.05f, gold, 8); }
        }
        // 앞판(말린 장식)과 뒤 등받이
        b.Box(new Vector3(0, y0 + H * 0.3f, L * 0.42f), new Vector3(W * 0.98f, H * 0.6f, R * 0.12f), red);
        b.Box(new Vector3(0, y0 + H * 0.55f, -L * 0.42f), new Vector3(W * 0.98f, H * 1.1f, R * 0.14f), red);
        b.Box(new Vector3(0, y0 + R * 0.35f, -L * 0.18f), new Vector3(W * 0.9f, R * 0.4f, L * 0.3f), velvet);   // 의자
        b.Star(new Vector3(0, y0 + H * 0.6f, -L * 0.42f - R * 0.08f), Vector3.back, Vector3.up, R * 0.32f, R * 0.14f, R * 0.04f, gold);
        // 뒤쪽 선물 자루
        b.Ellipsoid(new Vector3(W * 0.12f, y0 + H * 0.95f, -L * 0.3f), new Vector3(R * 0.75f, R * 0.85f, R * 0.6f), new Color(0.72f, 0.55f, 0.35f), 12, 16);
        b.Box(new Vector3(-W * 0.22f, y0 + H * 0.5f, -L * 0.28f), new Vector3(R * 0.55f, R * 0.5f, R * 0.5f), new Color(0.2f, 0.55f, 0.35f));
        b.Box(new Vector3(-W * 0.22f, y0 + H * 0.5f, -L * 0.28f), new Vector3(R * 0.57f, R * 0.12f, R * 0.52f), gold);
        sled = PbObj("santa_sled", b, transform, new Vector3(0, 0, -R * 0.4f));   // 날이 바닥에 닿게 (발은 썰매 바닥 속)
        sled.GetComponent<MeshRenderer>().enabled = !hidden;
    }

    // 팔을 번쩍 들 때 손이 큰 머리 속으로 파묻히지 않게: 머리 위쪽으로 향하면 옆으로 벌려(머리 옆에서 멈춤) 줌
    void ArmsClearHead() { ClearArm(armL, -1f); ClearArm(armR, 1f); }
    static void ClearArm(Transform arm, float side)
    {
        if (!arm) return;
        var d = arm.localRotation * Vector3.down;            // 손이 향하는 방향 (몸 기준: x 옆, y 위, z 앞)
        if (d.y < 0.25f) return;                              // 어깨 높이 아래면 그대로
        float o = d.x * side, f = d.z;
        const float minOut = 0.8f;                            // 머리 옆으로 비켜나는 정도
        if (o >= minOut || f >= 0.62f) return;                // 이미 옆이나 앞으로 충분히 나가 있음
        float nz = Mathf.Max(f, 0f) * 0.6f;
        float ny = Mathf.Sqrt(Mathf.Max(0f, 1f - minOut * minOut - nz * nz));
        var nd = new Vector3(minOut * side, ny, nz);
        arm.localRotation = Quaternion.FromToRotation(Vector3.down, nd);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (runPose > 0) { RunAnim(dt); return; }
        UpdatePillow();
        Quaternion eRot = Quaternion.identity; Vector3 eScale = Vector3.one;
        float s = Mathf.Clamp01(speed / 2.2f);
        phase += dt * (4f + 6f * s);
        float sw = Mathf.Sin(phase) * 38f * s;
        if (armL) armL.localRotation = Quaternion.Euler(sw, 0, -4f);
        if (armR) armR.localRotation = Quaternion.Euler(-sw, 0, 4f);
        if (legL) legL.localRotation = Quaternion.Euler(-sw * 0.9f, 0, 0);
        if (legR) legR.localRotation = Quaternion.Euler(sw * 0.9f, 0, 0);
        if (tail) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(phase * 0.5f + 1) * 12f, 0);
        float et = Time.time - emoteT, jump = 0;
        if (carryBoth) { if (armL) armL.localRotation = Quaternion.Euler(-68f + sw * 0.1f, 0, -14f); if (armR) armR.localRotation = Quaternion.Euler(-68f - sw * 0.1f, 0, 14f); }
        if (leftHold && armL) armL.localRotation = Quaternion.Euler(-50f + sw * 0.12f + Mathf.Sin(Time.time * 2.2f) * 2f, 0, -10f);   // 선물을 앞으로 들고 있는 왼팔
        if (holdPillow && !(et < 1.0f && (emote == "swing" || emote == "bonk"))) if (armR) armR.localRotation = Quaternion.Euler(-72f + sw * 0.15f + Mathf.Sin(Time.time * 2.4f) * 3f, 0, 16f);   // 베개를 앞으로 들고 있는 팔
        if (et < 1.8f)
        {
            float k = Mathf.Sin(Mathf.Clamp01(et / 1.8f) * Mathf.PI);   // 들었다 내리기
            switch (emote)
            {
                case "wave":      // 오른팔 흔들기
                    if (armR) armR.localRotation = Quaternion.Euler(0, 0, 4f + k * (150f + Mathf.Sin(et * 16f) * 25f));
                    break;
                case "highfive":  // 두 팔 번쩍
                case "cheer":
                    if (armL) armL.localRotation = Quaternion.Euler(0, 0, -4f - k * 160f);
                    if (armR) armR.localRotation = Quaternion.Euler(0, 0, 4f + k * 160f);
                    jump = emote == "cheer" ? Mathf.Abs(Mathf.Sin(et * 9f)) * 0.12f * k : 0;
                    break;
                case "throw":     // 눈덩이 던지기: 오른팔을 뒤로 젖혔다가 앞으로
                    {
                        float a = et < 0.18f ? Mathf.Lerp(0, 150, et / 0.18f) : Mathf.Lerp(150, -70, Mathf.Clamp01((et - 0.18f) / 0.15f));
                        if (armR) armR.localRotation = Quaternion.Euler(a * Mathf.Clamp01((1.8f - et) * 3), 0, 12f);
                    }
                    break;
                case "hit":       // 눈덩이에 맞음: 팔 벌리고 움찔
                    if (armL) armL.localRotation = Quaternion.Euler(0, 0, -80f * k);
                    if (armR) armR.localRotation = Quaternion.Euler(0, 0, 80f * k);
                    jump = Mathf.Abs(Mathf.Sin(et * 10f)) * 0.06f * k;
                    break;
                case "swing":     // 베개 휘두르기: 머리 뒤로 크게 젖혔다가 앞으로 내려치기 + 몸 비틀기
                    {
                        // 어깨 위(138) → 더 뒤로 젖힘(185) → 앞으로 휙(-100, 앞쪽 수평보다 살짝 위) → 다시 어깨 위
                        float a;
                        if (et < 0.14f) a = Mathf.Lerp(-72f, 190f, Mathf.SmoothStep(0, 1, et / 0.14f));                      // 머리 위 뒤로 크게 들어 올리기
                        else if (et < 0.27f) { float u = (et - 0.14f) / 0.13f; a = Mathf.Lerp(190f, -115f, 1 - (1 - u) * (1 - u)); }   // 앞으로 크게 휙
                        else if (et < 0.36f) a = -115f;
                        else a = Mathf.Lerp(-115f, -72f, Mathf.SmoothStep(0, 1, Mathf.Clamp01((et - 0.36f) / 0.3f)));
                        if (armR) armR.localRotation = Quaternion.Euler(a, 0, 14f);
                        if (armL) armL.localRotation = Quaternion.Euler(et < 0.14f ? -50f : 40f, 0, -30f);   // 반대 팔로 균형
                        float tw = et < 0.16f ? -28f * (et / 0.16f) : Mathf.Lerp(-28f, 22f, Mathf.Clamp01((et - 0.16f) / 0.16f)) * Mathf.Clamp01((0.9f - et) * 2f);
                        float lunge = et > 0.12f && et < 0.6f ? Mathf.Sin((et - 0.12f) / 0.48f * Mathf.PI) : 0;
                        eRot = Quaternion.Euler(18f * lunge, tw, 0);   // 몸을 앞으로 숙이며
                        jump = et > 0.14f && et < 0.4f ? Mathf.Sin((et - 0.14f) / 0.26f * Mathf.PI) * 0.06f : 0;
                    }
                    break;
                case "bonk":      // 베개에 맞음: 말랑하게 찌그러졌다 튕기고, 뒤로 휘청
                    {
                        float sq = Mathf.Exp(-et * 5.5f) * Mathf.Sin(et * 19f) * 0.3f;
                        eScale = new Vector3(1 + sq * 0.55f, 1 - sq, 1 + sq * 0.55f);
                        float lean = Mathf.Exp(-et * 4f) * Mathf.Sin(Mathf.Min(et * 9f, Mathf.PI * 2.5f));
                        eRot = Quaternion.Euler(-30f * lean, Mathf.Exp(-et * 3f) * 40f * Mathf.Sin(et * 7f), 12f * lean);
                        jump = Mathf.Max(0, Mathf.Sin(Mathf.Min(et * 6f, Mathf.PI))) * 0.16f * Mathf.Exp(-et * 2f);
                        float fl = Mathf.Exp(-et * 3f);
                        if (armL) armL.localRotation = Quaternion.Euler(0, 0, -110f * fl - 10f);
                        if (armR && !holdPillow) armR.localRotation = Quaternion.Euler(0, 0, 110f * fl + 10f);
                        if (armR && holdPillow) armR.localRotation = Quaternion.Euler(-72f + 60f * fl, 0, 16f + 60f * fl);
                    }
                    break;
                case "flinch":    // 작은 눈에 맞음: 짧게 움찔하며 팔로 얼굴 가리기
                    {
                        float kk = et < 0.55f ? Mathf.Sin(et / 0.55f * Mathf.PI) : 0;
                        if (armL) armL.localRotation = Quaternion.Euler(-70f * kk, 0, -15f * kk);
                        if (armR) armR.localRotation = Quaternion.Euler(-70f * kk, 0, 15f * kk);
                        jump = 0.04f * kk;
                    }
                    break;
                case "heart":     // 폴짝
                case "gift":
                    jump = Mathf.Abs(Mathf.Sin(et * 7f)) * 0.15f * k;
                    if (armL) armL.localRotation = Quaternion.Euler(-k * 70f, 0, -20f * k);
                    if (armR) armR.localRotation = Quaternion.Euler(-k * 70f, 0, 20f * k);
                    break;
            }
            ArmsClearHead();
        }
        if (skating && et >= 1.8f)
        {
            // 스케이트: 다리를 번갈아 옆뒤로 밀고, 몸은 앞으로 숙이고 좌우로 실룩
            float st = Mathf.Clamp01(speed / 6f);
            float push = Mathf.Sin(phase * 0.55f);
            float pl = Mathf.Max(0, push), pr = Mathf.Max(0, -push);
            if (legL) legL.localRotation = Quaternion.Euler(-pl * 34f * st, 0, -pl * 24f * st);
            if (legR) legR.localRotation = Quaternion.Euler(-pr * 34f * st, 0, pr * 24f * st);
            if (armL) armL.localRotation = Quaternion.Euler(push * 35f * st, 0, -10f - 30f * st);
            if (armR) armR.localRotation = Quaternion.Euler(-push * 35f * st, 0, 10f + 30f * st);
            if (body)
            {
                body.localPosition = new Vector3(push * 0.035f * st, 0.11f + Mathf.Abs(push) * 0.012f * st, 0);
                body.localRotation = Quaternion.Euler(14f * st, push * 6f * st, -push * 9f * st);
                body.localScale = Vector3.one;
            }
            return;
        }
        // 걸을 때 통통 튀고, 서 있을 때는 숨쉬듯 살짝
        float target = s > 0.05f ? Mathf.Abs(Mathf.Sin(phase)) * 0.045f * s : Mathf.Sin(Time.time * 2.2f) * 0.006f + 0.006f;
        bob = Mathf.Lerp(bob, target, 1 - Mathf.Exp(-dt * 20f));
        if (body)
        {
            body.localPosition = new Vector3(0, bob + jump + (skating ? 0.11f : 0f), 0);
            body.localRotation = Quaternion.Euler(s * 6f, 0, Mathf.Sin(phase) * 3f * s) * eRot;
            body.localScale = eScale;
        }
    }
}
