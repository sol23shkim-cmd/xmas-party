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
        look = l;
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
            h.transform.localPosition = new Vector3(0, top - hatTop + (human && l.hair != "" ? 0.03f : 0f), 0);
            attached.Add(h);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float s = Mathf.Clamp01(speed / 2.2f);
        phase += dt * (4f + 6f * s);
        float sw = Mathf.Sin(phase) * 38f * s;
        if (armL) armL.localRotation = Quaternion.Euler(sw, 0, -4f);
        if (armR) armR.localRotation = Quaternion.Euler(-sw, 0, 4f);
        if (legL) legL.localRotation = Quaternion.Euler(-sw * 0.9f, 0, 0);
        if (legR) legR.localRotation = Quaternion.Euler(sw * 0.9f, 0, 0);
        if (tail) tail.localRotation = Quaternion.Euler(0, Mathf.Sin(phase * 0.5f + 1) * 12f, 0);
        // 걸을 때 통통 튀고, 서 있을 때는 숨쉬듯 살짝
        float target = s > 0.05f ? Mathf.Abs(Mathf.Sin(phase)) * 0.045f * s : Mathf.Sin(Time.time * 2.2f) * 0.006f + 0.006f;
        bob = Mathf.Lerp(bob, target, 1 - Mathf.Exp(-dt * 20f));
        if (body)
        {
            body.localPosition = new Vector3(0, bob, 0);
            body.localRotation = Quaternion.Euler(s * 6f, 0, Mathf.Sin(phase) * 3f * s);
        }
    }
}
