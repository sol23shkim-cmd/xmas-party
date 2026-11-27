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
        if (on == skating && ((skL != null) == on)) return;
        skating = on;
        if (skL) Destroy(skL); if (skR) Destroy(skR);
        skL = skR = null;
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
        if (legL) legL.localRotation = Quaternion.Euler(lL);
        if (legR) legR.localRotation = Quaternion.Euler(lR);
        if (tail) tail.localRotation = Quaternion.Euler(0, s * 25f, 0);
        if (body) { body.localRotation = br; body.localPosition = bp; }
    }
    public void SetRunPose(int p) { if (p != runPose) { runPose = p; runT = 0; } }

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
