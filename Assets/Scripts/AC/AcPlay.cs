// AcPlay.cs - 함께 노는 미니게임: 피아노 연주(음악실·거실 피아노), 크리스마스 퀴즈(서재), 눈싸움(바깥)
// 다른 사람에게 보이는 짧은 사건(피아노 음·눈덩이·퀴즈 답)은 players/<id>.act 에 "번호|종류|내용;..."으로 실어 보냄
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class AcRoom
{
    // ===== 사건 주고받기 =====
    readonly List<(int id, string kind, string data, float at)> myActs = new List<(int, string, string, float)>();
    int actSeq = (int)(DateTime.UtcNow.Ticks % 100000);
    readonly Dictionary<string, int> lastActSeen = new Dictionary<string, int>();

    void AddAct(string kind, string data)
    {
        myActs.Add((++actSeq, kind, data, Time.unscaledTime));
        if (myActs.Count > 12) myActs.RemoveAt(0);
    }

    string ActString()
    {
        myActs.RemoveAll(a => Time.unscaledTime - a.at > 2.5f);
        return string.Join(";", myActs.Select(a => a.id + "|" + a.kind + "|" + a.data));
    }

    void ReadActs()
    {
        foreach (var kv in remotes)
        {
            var r = kv.Value; if (r.net == null || r.c == null) continue;
            string s = r.net.act; if (string.IsNullOrEmpty(s)) continue;
            bool first = !lastActSeen.TryGetValue(kv.Key, out int last);
            int maxId = last;
            foreach (var ev in s.Split(';'))
            {
                var p = ev.Split('|'); if (p.Length < 3 || !int.TryParse(p[0], out int id)) continue;
                if (id > maxId) maxId = id;
                if (first || id <= last) continue;
                OnRemoteAct(kv.Key, r, p[1], p[2]);
            }
            lastActSeen[kv.Key] = maxId;
        }
    }

    void OnRemoteAct(string rid, Remote r, string kind, string data)
    {
        switch (kind)
        {
            case "p":   // 피아노 음
                if (int.TryParse(data, out int note))
                {
                    float d = Vector3.Distance(r.c.transform.position, me.transform.position);
                    if (d < 16f) PlayPiano(note, Mathf.Lerp(0.75f, 0.15f, d / 16f));
                    pianoGlow[note] = Time.unscaledTime;
                }
                break;
            case "s":   // 눈덩이
                {
                    var v = data.Split(',');
                    if (v.Length >= 4 && float.TryParse(v[0], out float fx) && float.TryParse(v[1], out float fz) && float.TryParse(v[2], out float tx) && float.TryParse(v[3], out float tz))
                        SpawnSnowball(new Vector3(fx, 1.1f, fz), new Vector3(tx, 0, tz), rid, r.net.name);
                }
                break;
            case "q":   // 퀴즈 답 "회차:고른 번호"
                {
                    var v = data.Split(':');
                    if (v.Length == 2 && long.TryParse(v[0], out long round) && int.TryParse(v[1], out int ch))
                    {
                        if (!quizAnswers.TryGetValue(round, out var dict)) quizAnswers[round] = dict = new Dictionary<string, (string, int)>();
                        dict[rid] = (r.net.name, ch);
                    }
                }
                break;
        }
    }

    double NetNow => net != null && net.ServerNow > 0 ? net.ServerNow : AcSync.UnixMs;

    void PlayUpdate(float dt)
    {
        if (phase != Phase.Play) return;
        ReadActs();
        if (pianoOpen) PianoUpdate();
        if (quizOpen) QuizUpdate();
        SnowballUpdate(dt);
        if (snowMode && (!Outside || building || playingGame)) snowMode = false;
    }

    // ========================================================
    // 피아노
    bool pianoOpen; int pianoSong = -1, pianoStep;
    readonly Dictionary<int, float> pianoGlow = new Dictionary<int, float>();   // 건반을 누른 때 (빛남)
    readonly List<(Rect r, int note, bool black)> pianoKeys = new List<(Rect, int, bool)>();
    static readonly int[] WhiteSemis = { 0, 2, 4, 5, 7, 9, 11 };
    static readonly (string name, int[] notes)[] PianoSongs =
    {
        ("징글벨", new[] { 64, 64, 64, 64, 64, 64, 64, 67, 60, 62, 64, 65, 65, 65, 65, 65, 64, 64, 64, 64, 62, 62, 64, 62, 67 }),
        ("고요한 밤", new[] { 67, 69, 67, 64, 67, 69, 67, 64, 74, 74, 71, 72, 72, 67, 69, 69, 72, 71, 69, 67, 69, 67, 64 }),
        ("기쁘다 구주 오셨네", new[] { 74, 73, 71, 69, 67, 66, 64, 62, 69, 71, 71, 73, 73, 74 }),
        ("반짝반짝 작은 별", new[] { 60, 60, 67, 67, 69, 69, 67, 65, 65, 64, 64, 62, 62, 60 }),
    };

    bool NearPiano(out bool musicRoom)
    {
        musicRoom = !Outside && AreaAt(me.transform.position) == "음악실";
        if (musicRoom) return true;
        var hp = new Vector3(HalfX - SEG * 1.5f + 0.3f, 0, -HalfZ + 0.5f);   // 거실 피아노
        return !Outside && Flat(me.transform.position, hp) < 2.8f;
    }

    void OpenPiano() { pianoOpen = true; pianoSong = -1; pianoStep = 0; walkTarget = null; decorating = false; treeOverview = false; }

    void PlayPiano(int note, float vol)
    {
        string s = note < 54 ? "piano_c3" : note < 66 ? "piano_c4" : "piano_c5";
        int baseN = note < 54 ? 48 : note < 66 ? 60 : 72;
        Sfx(s, vol, Mathf.Pow(2f, (note - baseN) / 12f));
    }

    void PressKey(int note)
    {
        PlayPiano(note, 0.8f);
        pianoGlow[note] = Time.unscaledTime;
        AddAct("p", note.ToString());
        if (pianoSong >= 0)
        {
            var song = PianoSongs[pianoSong].notes;
            if (pianoStep < song.Length && song[pianoStep] == note)
            {
                pianoStep++;
                if (pianoStep >= song.Length) { Toast($"'{PianoSongs[pianoSong].name}' 끝까지 연주했어요!"); Sfx("record", 0.5f); Emote("cheer"); pianoStep = 0; }
            }
        }
    }

    void PianoUpdate()
    {
        if (Key("esc")) { pianoOpen = false; return; }
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            // 컴퓨터 자판: 흰 건반 A S D F G H J K L ; ' / 검은 건반 W E T Y U O P
            UnityEngine.InputSystem.Key[] whites = { UnityEngine.InputSystem.Key.A, UnityEngine.InputSystem.Key.S, UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.F, UnityEngine.InputSystem.Key.G, UnityEngine.InputSystem.Key.H, UnityEngine.InputSystem.Key.J, UnityEngine.InputSystem.Key.K, UnityEngine.InputSystem.Key.L, UnityEngine.InputSystem.Key.Semicolon, UnityEngine.InputSystem.Key.Quote };
            for (int i = 0; i < whites.Length; i++) if (kb[whites[i]].wasPressedThisFrame) PressKey(60 + (i / 7) * 12 + WhiteSemis[i % 7]);
            (UnityEngine.InputSystem.Key k, int n)[] blacks = { (UnityEngine.InputSystem.Key.W, 61), (UnityEngine.InputSystem.Key.E, 63), (UnityEngine.InputSystem.Key.T, 66), (UnityEngine.InputSystem.Key.Y, 68), (UnityEngine.InputSystem.Key.U, 70), (UnityEngine.InputSystem.Key.O, 73), (UnityEngine.InputSystem.Key.P, 75) };
            foreach (var (k, n) in blacks) if (kb[k].wasPressedThisFrame) PressKey(n);
        }
        // 손가락 여러 개 / 마우스
        var downs = new List<Vector2>();
        var ts = Touchscreen.current; bool any = false;
        if (ts != null) foreach (var t in ts.touches) { if (t.press.isPressed || t.press.wasPressedThisFrame) any = true; if (t.press.wasPressedThisFrame) downs.Add(t.position.ReadValue()); }
        if (!any && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) downs.Add(Mouse.current.position.ReadValue());
        foreach (var sp in downs)
        {
            var u = new Vector2(sp.x / UiK, (Screen.height - sp.y) / UiK);
            int hit = -1;
            foreach (var k in pianoKeys) if (k.black && k.r.Contains(u)) { hit = k.note; break; }
            if (hit < 0) foreach (var k in pianoKeys) if (!k.black && k.r.Contains(u)) { hit = k.note; break; }
            if (hit >= 0) PressKey(hit);
        }
#endif
    }

    void PianoGUI()
    {
        bool narrow = SW < 640;
        int whites = narrow ? 10 : 15;           // 좁으면 C4~E5, 넓으면 C4~C6
        float kw = Mathf.Min(54, (SW - 30) / whites), kh = narrow ? 170 : 190;
        float x0 = SW / 2 - kw * whites / 2, y0 = SH - kh - 16;
        var panel = new Rect(x0 - 12, y0 - 108, kw * whites + 24, kh + 120);
        GUI.Box(R(panel), "", sBox);
        GUI.Label(new Rect(panel.x + 14, panel.y + 8, 200, 26), "피아노", sLabel);
        if (GUI.Button(new Rect(panel.xMax - 90, panel.y + 6, 80, 30), "<b>나가기</b>", sBtn)) { pianoOpen = false; return; }
        // 곡 고르기 (따라 치기)
        float bx = panel.x + 14, by = panel.y + 42, bw = (panel.width - 28 - 6 * 4) / 5f;
        string[] names = new[] { "자유 연주" }.Concat(PianoSongs.Select(s => s.name)).ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(bx + i * (bw + 6), by, bw, 30);
            bool sel = pianoSong == i - 1;
            if (sel) GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), AcPalette.Tex(new Color(0.85f, 0.3f, 0.3f)));
            if (GUI.Button(r, Short(names[i], narrow ? 4 : 9), sBtn)) { pianoSong = i - 1; pianoStep = 0; }
        }
        int next = pianoSong >= 0 && pianoStep < PianoSongs[pianoSong].notes.Length ? PianoSongs[pianoSong].notes[pianoStep] : -1;
        GUI.Label(new Rect(panel.x + 14, by + 34, panel.width - 28, 22), pianoSong < 0 ? (IsMobile ? "건반을 눌러 연주해요. 가까이 있는 사람에게도 들려요" : "건반을 누르거나 자판 A~' (흰 건반), W E T Y U O P (검은 건반)") : $"빛나는 건반을 차례로 눌러요  ({pianoStep}/{PianoSongs[pianoSong].notes.Length})", sSmall);
        // 건반
        if (Event.current.type == EventType.Repaint) pianoKeys.Clear();
        var ws = new List<(Rect, int)>(); var bs = new List<(Rect, int)>();
        for (int i = 0; i < whites; i++)
        {
            int n = 60 + (i / 7) * 12 + WhiteSemis[i % 7];
            ws.Add((new Rect(x0 + i * kw, y0, kw - 2, kh), n));
            int semi = WhiteSemis[i % 7];
            if (i < whites - 1 && semi != 4 && semi != 11) bs.Add((new Rect(x0 + (i + 1) * kw - kw * 0.32f, y0, kw * 0.62f, kh * 0.6f), n + 1));
        }
        float now = Time.unscaledTime;
        foreach (var (r, n) in ws)
        {
            bool lit = pianoGlow.TryGetValue(n, out float t) && now - t < 0.18f;
            var c = n == next ? Color.Lerp(new Color(1f, 0.9f, 0.5f), Color.white, 0.5f + 0.5f * Mathf.Sin(now * 8)) : lit ? new Color(0.85f, 0.9f, 1f) : new Color(0.99f, 0.98f, 0.95f);
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), AcPalette.Tex(new Color(0.35f, 0.25f, 0.2f)));
            GUI.DrawTexture(lit ? new Rect(r.x, r.y + 3, r.width, r.height - 3) : r, AcPalette.Tex(c));
            if (n % 12 == 0) GUI.Label(new Rect(r.x, r.yMax - 22, r.width, 20), "도", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            if (Event.current.type == EventType.Repaint) pianoKeys.Add((r, n, false));
        }
        foreach (var (r, n) in bs)
        {
            bool lit = pianoGlow.TryGetValue(n, out float t) && now - t < 0.18f;
            var c = n == next ? new Color(1f, 0.75f, 0.3f) : lit ? new Color(0.35f, 0.38f, 0.5f) : new Color(0.15f, 0.12f, 0.12f);
            GUI.DrawTexture(r, AcPalette.Tex(c));
            if (Event.current.type == EventType.Repaint) pianoKeys.Add((r, n, true));
        }
        R(new Rect(x0, y0, kw * whites, kh));
    }

    // ========================================================
    // 크리스마스 퀴즈 (모두 같은 문제를 같은 시간에: 20초마다 한 문제, 14초 동안 답하고 6초 동안 정답 공개)
    bool quizOpen; int quizScore; long quizLastScored = -1; long quizLastTick = -1;
    readonly Dictionary<long, int> myQuizAnswer = new Dictionary<long, int>();
    readonly Dictionary<long, Dictionary<string, (string name, int ch)>> quizAnswers = new Dictionary<long, Dictionary<string, (string, int)>>();
    const double QuizRoundMs = 20000, QuizAnswerMs = 14000;

    static readonly (string q, string a, string[] wrong)[] QuizBank =
    {
        ("루돌프 사슴의 코는 무슨 색일까요?", "빨간색", new[] { "파란색", "노란색", "초록색" }),
        ("크리스마스는 몇 월 며칠일까요?", "12월 25일", new[] { "12월 24일", "12월 31일", "1월 1일" }),
        ("산타클로스의 썰매를 끄는 동물은?", "순록", new[] { "말", "썰매개", "북극곰" }),
        ("캐럴 '고요한 밤 거룩한 밤'이 처음 불린 나라는?", "오스트리아", new[] { "독일", "영국", "미국" }),
        ("크리스마스트리 꼭대기에 주로 다는 장식은?", "별", new[] { "종", "양말", "눈사람" }),
        ("진저브레드 쿠키에 들어가는 대표 향신료는?", "생강", new[] { "후추", "계피만", "고추" }),
        ("산타클로스의 모델로 알려진 성인은?", "성 니콜라스", new[] { "성 발렌타인", "성 패트릭", "성 조지" }),
        ("'징글벨'의 '징글'은 원래 무슨 소리일까요?", "썰매 방울 소리", new[] { "교회 종소리", "눈 밟는 소리", "말발굽 소리" }),
        ("핀란드의 산타 마을이 있는 도시는?", "로바니에미", new[] { "헬싱키", "오슬로", "스톡홀름" }),
        ("지팡이 모양 사탕(캔디 케인)의 대표 색 조합은?", "빨강과 하양", new[] { "파랑과 하양", "초록과 노랑", "분홍과 보라" }),
        ("영화 '나 홀로 집에'의 주인공 이름은?", "케빈", new[] { "해리", "토미", "찰리" }),
        ("크리스마스 선물을 받으려고 벽난로에 거는 것은?", "양말", new[] { "장갑", "모자", "목도리" }),
        ("크리스마스 꽃 포인세티아의 원산지는?", "멕시코", new[] { "네덜란드", "일본", "이집트" }),
        ("디킨스의 소설 '크리스마스 캐럴'에 나오는 구두쇠는?", "스크루지", new[] { "그린치", "제페토", "피터 팬" }),
        ("산타가 산다고 알려진 곳은?", "북극", new[] { "남극", "적도", "사막" }),
        ("루돌프까지 합한 산타 썰매의 순록은 모두 몇 마리?", "9마리", new[] { "6마리", "8마리", "12마리" }),
        ("'Merry Christmas'의 Merry는 무슨 뜻일까요?", "즐거운", new[] { "조용한", "추운", "거룩한" }),
        ("눈사람의 코로 가장 많이 쓰는 채소는?", "당근", new[] { "오이", "감자", "고추" }),
        ("크리스마스트리로 가장 흔히 쓰는 나무는?", "전나무", new[] { "벚나무", "소나무만", "은행나무" }),
        ("발레 '호두까기 인형'의 작곡가는?", "차이콥스키", new[] { "모차르트", "베토벤", "쇼팽" }),
        ("크리스마스 다음 날, 영국 등에서 쉬는 날의 이름은?", "박싱 데이", new[] { "블랙 프라이데이", "보너스 데이", "리본 데이" }),
        ("'Feliz Navidad'는 어느 나라 말일까요?", "스페인어", new[] { "프랑스어", "이탈리아어", "독일어" }),
        ("눈 결정은 보통 몇 갈래로 뻗어 있을까요?", "6갈래", new[] { "4갈래", "5갈래", "8갈래" }),
        ("산타의 장난감 공장에서 일한다고 알려진 도우미는?", "엘프", new[] { "드워프", "트롤", "요정 여왕" }),
        ("문에 거는 크리스마스 리스(화환)는 어떤 모양일까요?", "동그란 고리", new[] { "세모", "별", "네모" }),
        ("미국 아이들이 산타를 위해 두는 간식은?", "우유와 쿠키", new[] { "콜라와 피자", "차와 떡", "주스와 사탕" }),
        ("크리스마스를 대표하는 두 가지 색은?", "빨강과 초록", new[] { "파랑과 노랑", "검정과 하양", "보라와 주황" }),
        ("노래 '루돌프 사슴코'에서 루돌프를 놀리던 것은?", "다른 순록들", new[] { "눈사람들", "엘프들", "산타" }),
        ("겨우살이(미슬토) 아래에서 하는 전통은?", "입맞춤", new[] { "춤추기", "선물 열기", "노래하기" }),
        ("크리스마스 전날 밤을 부르는 말은?", "크리스마스 이브", new[] { "박싱 데이", "새해 전야", "추수감사절" }),
    };

    long QuizRound => (long)(NetNow / QuizRoundMs);
    double QuizInRound => NetNow % QuizRoundMs;

    (string q, string[] choices, int correct) QuizFor(long round)
    {
        var rnd = new System.Random((int)(round % 1000003) * 7919 + 17);
        var item = QuizBank[(int)((round * 7) % QuizBank.Length)];
        var list = new List<string> { item.a }; list.AddRange(item.wrong);
        var order = list.OrderBy(_ => rnd.Next()).ToList();
        return (item.q, order.ToArray(), order.IndexOf(item.a));
    }

    void OpenQuiz() { quizOpen = true; walkTarget = null; quizScore = MyQuizTotal(); }

    int MyQuizTotal() { var s = net != null ? net.Get("quiz") : null; return s != null && s.TryGetValue(myKey, out var v) && v is Dictionary<string, object> d ? (int)AcJson.D(d, "score") : 0; }

    void QuizUpdate()
    {
        if (Key("esc")) { quizOpen = false; return; }
        long round = QuizRound; double inr = QuizInRound;
        int secLeft = (int)Math.Ceiling((QuizAnswerMs - inr) / 1000);
        if (inr < QuizAnswerMs && secLeft <= 3 && secLeft > 0 && quizLastTick != round * 10 + secLeft) { quizLastTick = round * 10 + secLeft; Sfx("tick", 0.5f); }
        if (inr >= QuizAnswerMs && quizLastScored != round)
        {
            quizLastScored = round;
            if (myQuizAnswer.TryGetValue(round, out int mine))
            {
                var qz = QuizFor(round);
                if (mine == qz.correct)
                {
                    Sfx("correct", 0.7f); quizScore = MyQuizTotal() + 1;
                    net?.Put("quiz/" + myKey, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"score\":" + quizScore + ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
                }
                else Sfx("wrong", 0.6f);
            }
        }
        foreach (var k in quizAnswers.Keys.Where(k => k < round - 3).ToList()) quizAnswers.Remove(k);
    }

    void QuizGUI()
    {
        long round = QuizRound; double inr = QuizInRound; bool reveal = inr >= QuizAnswerMs;
        var qz = QuizFor(round);
        float pw = Mathf.Min(SW - 20, 640), ph = Mathf.Min(SH - 90, 470);
        var p = new Rect(SW / 2 - pw / 2, SH / 2 - ph / 2 + 20, pw, ph);
        GUI.Box(R(p), "", sBox);
        GUI.Label(new Rect(p.x + 16, p.y + 10, 300, 28), "크리스마스 퀴즈", sLabel);
        GUI.Label(new Rect(p.x + 150, p.y + 14, 200, 22), $"내 점수 {quizScore}점", sSmall);
        if (GUI.Button(new Rect(p.xMax - 90, p.y + 8, 80, 30), "<b>나가기</b>", sBtn)) { quizOpen = false; return; }
        // 남은 시간 막대
        float frac = reveal ? (float)((QuizRoundMs - inr) / (QuizRoundMs - QuizAnswerMs)) : (float)(1 - inr / QuizAnswerMs);
        GUI.DrawTexture(new Rect(p.x + 16, p.y + 44, p.width - 32, 8), AcPalette.Tex(new Color(0.85f, 0.8f, 0.72f)));
        GUI.DrawTexture(new Rect(p.x + 16, p.y + 44, (p.width - 32) * Mathf.Clamp01(frac), 8), AcPalette.Tex(reveal ? new Color(0.35f, 0.65f, 0.4f) : frac < 0.25f ? new Color(0.9f, 0.35f, 0.3f) : new Color(0.95f, 0.65f, 0.25f)));
        var qs = new GUIStyle(sLabel) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(p.x + 20, p.y + 58, p.width - 40, 70), qz.q, qs);
        // 보기 2x2
        myQuizAnswer.TryGetValue(round, out int mine); bool answered = myQuizAnswer.ContainsKey(round);
        float cw = (p.width - 48) / 2, chh = 52;
        for (int i = 0; i < 4; i++)
        {
            var r = new Rect(p.x + 16 + (i % 2) * (cw + 16), p.y + 136 + (i / 2) * (chh + 10), cw, chh);
            Color border = new Color(0.86f, 0.8f, 0.72f);
            if (answered && mine == i) border = new Color(0.25f, 0.5f, 0.9f);
            if (reveal && i == qz.correct) border = new Color(0.3f, 0.7f, 0.35f);
            else if (reveal && answered && mine == i) border = new Color(0.9f, 0.3f, 0.3f);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), AcPalette.Tex(border));
            if (GUI.Button(r, "<b>" + "①②③④"[i] + " " + qz.choices[i] + "</b>", sBtn) && !reveal && !answered)
            {
                myQuizAnswer[round] = i; AddAct("q", round + ":" + i); Sfx("click", 0.6f);
            }
        }
        // 다른 사람 답 / 맞힌 사람
        quizAnswers.TryGetValue(round, out var others);
        int cnt = (others?.Count ?? 0) + (answered ? 1 : 0);
        float ly = p.y + 136 + 2 * (chh + 10) + 6;
        string info;
        if (!reveal) info = answered ? $"답했어요! 다른 사람을 기다려요 ({cnt}명 답함)" : $"{Mathf.CeilToInt((float)((QuizAnswerMs - inr) / 1000))}초 안에 골라요  ({cnt}명 답함)";
        else
        {
            var right = new List<string>();
            if (answered && mine == qz.correct) right.Add(uiName.Trim());
            if (others != null) foreach (var o in others.Values) if (o.ch == qz.correct) right.Add(o.name);
            info = "정답: " + qz.choices[qz.correct] + "   " + (right.Count > 0 ? "맞힌 사람: " + string.Join(", ", right.Take(6)) : "아무도 못 맞혔어요");
        }
        GUI.Label(new Rect(p.x + 16, ly, p.width - 32, 24), info, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        // 순위 (누적 정답 수)
        var s = net != null ? net.Get("quiz") : null;
        if (s != null && s.Count > 0)
        {
            var top = s.Values.OfType<Dictionary<string, object>>().Select(d => (AcJson.S(d, "name"), (int)AcJson.D(d, "score"))).OrderByDescending(x => x.Item2).Take(5).ToList();
            string line = "퀴즈 왕: " + string.Join("   ", top.Select((x, i) => $"{i + 1}. {Short(x.Item1, 6)} {x.Item2}"));
            GUI.Label(new Rect(p.x + 16, ly + 28, p.width - 32, 40), line, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
        }
    }

    // ========================================================
    // 눈싸움
    bool snowMode; int sbHits, sbTaken; float sbCool, sbNextWrite;
    class Snowball { public Vector3 a, b; public float t, T, h; public GameObject go; public string owner, ownerName; }
    readonly List<Snowball> snowballs = new List<Snowball>();
    readonly List<(GameObject go, Vector3 v, float life)> sbBits = new List<(GameObject, Vector3, float)>();

    void ToggleSnowFight()
    {
        snowMode = !snowMode;
        if (snowMode) Toast(IsMobile ? "눈싸움! 친구나 땅을 톡 누르면 눈덩이를 던져요" : "눈싸움! 클릭한 곳으로 눈덩이를 던져요 (F: 앞으로 던지기)");
    }

    void ThrowSnowball(Vector3 target)
    {
        if (sbCool > 0) return;
        sbCool = 0.45f;
        var from = me.transform.position;
        var d = target - from; d.y = 0;
        if (d.magnitude > 18f) target = from + d.normalized * 18f;
        if (d.sqrMagnitude > 0.01f) { yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; me.transform.rotation = Quaternion.Euler(0, yaw, 0); }
        var start = from + Vector3.up * 1.1f + me.transform.forward * 0.3f;
        Emote("throw"); Sfx("throw", 0.6f);
        AddAct("s", $"{start.x:F2},{start.z:F2},{target.x:F2},{target.z:F2}");
        SpawnSnowball(start, target, myId, uiName.Trim());
    }

    void SpawnSnowball(Vector3 a, Vector3 b, string owner, string ownerName)
    {
        a.y = 1.1f;
        float dist = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        var go = AcCatalog.Spawn("sm_snowball", world, null); go.transform.localScale = Vector3.one * 1.3f;
        snowballs.Add(new Snowball { a = a, b = new Vector3(b.x, 0.5f, b.z), T = Mathf.Clamp(dist / 13f, 0.25f, 1.0f), h = 0.6f + dist * 0.12f, go = go, owner = owner, ownerName = ownerName });
    }

    void SnowballUpdate(float dt)
    {
        sbCool -= dt;
        if (snowMode && !chatting && !askOpen)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) ThrowSnowball(me.transform.position + me.transform.forward * 8f);
#endif
        }
        for (int i = snowballs.Count - 1; i >= 0; i--)
        {
            var s = snowballs[i];
            s.t += dt;
            float u = Mathf.Clamp01(s.t / s.T);
            var p = Vector3.Lerp(s.a, s.b, u); p.y += Mathf.Sin(u * Mathf.PI) * s.h;
            if (s.go) s.go.transform.position = p;
            if (u < 1) continue;
            Splash(s.b);
            if (s.go) Destroy(s.go);
            snowballs.RemoveAt(i);
            // 맞았는지: 던진 사람이 나면 다른 사람을, 남이 던졌으면 나를 확인
            if (s.owner == myId)
            {
                foreach (var kv in remotes)
                    if (kv.Value.c && Flat(kv.Value.c.transform.position, s.b) < 0.9f)
                    {
                        sbHits++; kv.Value.c.PlayEmote("hit"); Sfx("splat", 0.8f);
                        Toast($"{kv.Value.net?.name} 님을 맞혔어요! ({sbHits})");
                        if (Time.unscaledTime > sbNextWrite) { sbNextWrite = Time.unscaledTime + 1f; SaveSnowFight(); }
                        break;
                    }
                foreach (var n in npcs) if (n && n.gameObject.activeSelf && Flat(n.transform.position, s.b) < 0.9f) { n.PlayEmote("hit"); Sfx("splat", 0.7f); }
            }
            else
            {
                if (Flat(me.transform.position, s.b) < 0.9f) { sbTaken++; me.PlayEmote("hit"); Sfx("splat", 0.9f); Toast($"{s.ownerName} 님의 눈덩이에 맞았어요!"); }
                foreach (var kv in remotes) if (kv.Key != s.owner && kv.Value.c && Flat(kv.Value.c.transform.position, s.b) < 0.9f) kv.Value.c.PlayEmote("hit");
            }
        }
        for (int i = sbBits.Count - 1; i >= 0; i--)
        {
            var b = sbBits[i]; b.life -= dt; b.v.y -= 9f * dt;
            if (b.go) { b.go.transform.position += b.v * dt; b.go.transform.localScale *= 1 - dt * 1.5f; }
            sbBits[i] = b;
            if (b.life <= 0) { if (b.go) Destroy(b.go); sbBits.RemoveAt(i); }
        }
    }

    void Splash(Vector3 at)
    {
        for (int k = 0; k < 6; k++)
        {
            var g = AcCatalog.Spawn("sm_snowball", world, null); g.transform.position = at; g.transform.localScale = Vector3.one * 0.5f;
            var v = new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(1.5f, 3f), UnityEngine.Random.Range(-1.6f, 1.6f));
            sbBits.Add((g, v, 0.5f));
        }
    }

    void SaveSnowFight()
    {
        int prev = 0;
        var s = net != null ? net.Get("sbf") : null;
        if (s != null && s.TryGetValue(myKey, out var v) && v is Dictionary<string, object> d) prev = (int)AcJson.D(d, "hits");
        net?.Put("sbf/" + myKey, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"hits\":" + (prev + 1) + ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
    }

    void SnowFightHUD()
    {
        if (!snowMode) return;
        float w = 230, top = chatLog.Count > 0 ? 30 + 22 * chatLog.Count : 60;
        var r = new Rect(12, top, w, 96);
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 12, r.y + 6, w - 20, 24), $"<b>눈싸움</b>  맞힘 {sbHits} · 맞음 {sbTaken}", sSmall);
        var s = net != null ? net.Get("sbf") : null;
        if (s != null)
        {
            var top3 = s.Values.OfType<Dictionary<string, object>>().Select(d => (AcJson.S(d, "name"), (int)AcJson.D(d, "hits"))).OrderByDescending(x => x.Item2).Take(3).ToList();
            for (int i = 0; i < top3.Count; i++) GUI.Label(new Rect(r.x + 12, r.y + 30 + i * 20, w - 20, 20), $"{i + 1}. {Short(top3[i].Item1, 8)}  {top3[i].Item2}번 맞힘", sSmall);
        }
    }
}
