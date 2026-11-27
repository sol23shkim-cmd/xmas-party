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
            case "p":   // 피아노 음: 미니게임 중이 아니면 어디서나 들림 (가까우면 조금 더 크게)
                if (int.TryParse(data, out int note))
                {
                    float d = Vector3.Distance(r.c.transform.position, me.transform.position);
                    if (!playingGame && !quizOpen) PlayPiano(note, Mathf.Lerp(0.7f, 0.32f, Mathf.Clamp01(d / 30f)));
                    pianoGlow[note] = Time.unscaledTime;
                    pianoBy[rid] = (r.net.name, Time.unscaledTime);
                }
                break;
            case "w": OnRemoteSwing(rid, r, data); break;   // 베개 휘두르기
            case "qj": quizLobby[rid] = (r.net.name, Time.unscaledTime); break;   // 퀴즈 화면에 있음
            case "qa":   // 퀴즈 초대
                if (!quizOpen) { qzInviteFrom = r.net.name; qzInviteUntil = Time.unscaledTime + 15f; qzInviteStarted = false; }
                break;
            case "qs":   // 퀴즈 시작 "시작시각,씨앗"
                {
                    var v = data.Split(',');
                    if (v.Length == 2 && double.TryParse(v[0], out double t0) && int.TryParse(v[1], out int seed) && t0 > qzT0)
                    {
                        qzT0 = t0; qzSeed = seed; myQuizAnswer.Clear(); qzScored = -1;
                        if (!quizOpen) { qzInviteFrom = r.net.name; qzInviteUntil = Time.unscaledTime + 12f; qzInviteStarted = true; }
                    }
                }
                break;
            case "s":   // 눈덩이
                {
                    var v = data.Split(',');
                    if (v.Length >= 4 && float.TryParse(v[0], out float fx) && float.TryParse(v[1], out float fz) && float.TryParse(v[2], out float tx) && float.TryParse(v[3], out float tz))
                        SpawnSnowball(new Vector3(fx, 1.1f, fz), new Vector3(tx, 0, tz), rid, r.net.name, v.Length < 5 || v[4] != "0");
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
            default: KitchenAct(rid, r, kind, data); break;   // 주방 게임 (kj·ks·ka)
        }
    }

    // 위쪽 알림: 누가 피아노를 치는 중 / 퀴즈 초대 (참여하기)
    readonly Dictionary<string, (string name, float at)> pianoBy = new Dictionary<string, (string, float)>();
    bool qzInviteStarted;
    void PlayBanners()
    {
        if (phase != Phase.Play || playingGame) return;
        float w = Mathf.Min(SW - 20, 360), y = SW / 2 + 180 > SW - 250 ? 112 : 64;
        if (toast != null && Time.time < toastUntil) y += 46;
        if (!quizOpen && Time.unscaledTime < qzInviteUntil && qzInviteFrom != null)
        {
            var r = new Rect(SW / 2 - w / 2, y, w, 40);
            GUI.Box(R(r), "", sBox);
            GUI.Label(new Rect(r.x + 12, r.y + 9, w - 120, 24), Short(qzInviteFrom, 6) + (qzInviteStarted ? " 님이 퀴즈를 시작했어요!" : " 님이 퀴즈에 불러요!"), sSmall);
            if (GUI.Button(new Rect(r.xMax - 100, r.y + 6, 90, 28), "<b>참여하기</b>", sBtn)) { OpenQuiz(); qzInviteUntil = 0; }
            y += 46;
        }
        var players = pianoBy.Where(kv => Time.unscaledTime - kv.Value.at < 2.5f).Select(kv => kv.Value.name).ToList();
        if (players.Count > 0)
        {
            string t = string.Join(", ", players.Take(2).Select(n => Short(n, 6))) + (players.Count > 2 ? " 외" : "") + " 님이 피아노를 치고 있어요";
            var r = new Rect(SW / 2 - w / 2, y, w, 32);
            GUI.Box(R(r), "", sBox);
            GUI.Label(new Rect(r.x + 10, r.y + 5, w - 20, 22), t, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
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
        SnowFocusUpdate();
        UpdateOverviewOccluders();
        PillowUpdate(dt);
        CandyUpdate(dt);
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

    static readonly string[] PianoShort = { "자유", "징글벨", "고요한 밤", "기쁘다", "반짝반짝" };

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
        // 건반마다 따로 만든 소리(piano_n48~n84) → 늘이거나 줄이지 않아 음정이 정확 (11-26)
        int n = Mathf.Clamp(note, 48, 84);
        Sfx("piano_n" + n, vol, Mathf.Pow(2f, (note - n) / 12f));
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
                if (pianoStep >= song.Length) { QuestProgress("piano", 1); Toast($"'{PianoSongs[pianoSong].name}' 끝까지 연주했어요!"); Sfx("record", 0.5f); Emote("cheer"); pianoStep = 0; }
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
            if (sel) AcUiTheme.Ring(r, AcUiTheme.Accent, 3);
            if (GUI.Button(r, narrow ? PianoShort[i] : names[i], sBtn)) { pianoSong = i - 1; pianoStep = 0; }   // 좁은 화면: 잘리지 않는 짧은 이름
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
    // 크리스마스 퀴즈: 서재에서 열고 '모두에게 알리기'로 부르기 → 누군가 '시작'하면 모두 같은 10문제를 같이 풂
    //   한 문제 10초 + 정답 공개 2초. 판 정보(시작 시각·문제 고르는 씨앗)는 qzs/cur 에 저장하고 act "qs"로도 바로 알림
    bool quizOpen; int quizScore;
    readonly Dictionary<long, int> myQuizAnswer = new Dictionary<long, int>();
    readonly Dictionary<long, Dictionary<string, (string name, int ch)>> quizAnswers = new Dictionary<long, Dictionary<string, (string, int)>>();
    readonly Dictionary<string, (string name, float at)> quizLobby = new Dictionary<string, (string, float)>();   // 퀴즈 화면에 있는 사람
    double qzT0; int qzSeed; long qzScored = -1; int qzLastTick = -1; float qzBeat;
    string qzInviteFrom; float qzInviteUntil;
    const int QzCount = 10; const double QzAnswerMs = 10000, QzRevealMs = 2000, QzPerMs = QzAnswerMs + QzRevealMs, QzLeadMs = 3500;

    static readonly (string q, string a, string[] wrong)[] QuizBank =
    {
        ("루돌프 사슴의 코는 무슨 색일까요?", "빨간색", new[] { "파란색", "노란색", "초록색" }),
        ("크리스마스는 몇 월 며칠일까요?", "12월 25일", new[] { "12월 24일", "12월 31일", "1월 1일" }),
        ("산타클로스의 썰매를 끄는 동물은?", "순록", new[] { "말", "썰매개", "북극곰" }),
        ("캐럴 '고요한 밤 거룩한 밤'이 처음 불린 나라는?", "오스트리아", new[] { "독일", "영국", "미국" }),
        ("크리스마스트리 꼭대기에 주로 다는 장식은?", "별", new[] { "종", "양말", "눈사람" }),
        ("진저브레드 쿠키에 들어가는 대표 향신료는?", "생강", new[] { "후추", "바닐라", "고추" }),
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
        ("크리스마스트리로 가장 흔히 쓰는 나무는?", "전나무", new[] { "벚나무", "은행나무", "단풍나무" }),
        ("발레 '호두까기 인형'의 작곡가는?", "차이콥스키", new[] { "모차르트", "베토벤", "쇼팽" }),
        ("크리스마스 다음 날, 영국 등에서 쉬는 날의 이름은?", "박싱 데이", new[] { "블랙 프라이데이", "보너스 데이", "리본 데이" }),
        ("'Feliz Navidad'는 어느 나라 말일까요?", "스페인어", new[] { "프랑스어", "이탈리아어", "독일어" }),
        ("눈 결정은 보통 몇 갈래로 뻗어 있을까요?", "6갈래", new[] { "4갈래", "5갈래", "8갈래" }),
        ("산타의 장난감 공장에서 일한다고 알려진 도우미는?", "엘프", new[] { "드워프", "트롤", "고블린" }),
        ("문에 거는 크리스마스 리스(화환)는 어떤 모양일까요?", "동그란 고리", new[] { "세모", "별", "네모" }),
        ("미국 아이들이 산타를 위해 두는 간식은?", "우유와 쿠키", new[] { "콜라와 피자", "차와 떡", "주스와 사탕" }),
        ("크리스마스를 대표하는 두 가지 색은?", "빨강과 초록", new[] { "파랑과 노랑", "검정과 하양", "보라와 주황" }),
        ("노래 '루돌프 사슴코'에서 루돌프를 놀리던 것은?", "다른 순록들", new[] { "눈사람들", "엘프들", "산타" }),
        ("겨우살이(미슬토) 아래에서 하는 전통은?", "입맞춤", new[] { "춤추기", "선물 열기", "노래하기" }),
        ("크리스마스 전날 밤을 부르는 말은?", "크리스마스 이브", new[] { "박싱 데이", "새해 전야", "추수감사절" }),
        ("동화 '그린치'에서 그린치가 훔치려 한 것은?", "크리스마스", new[] { "생일", "핼러윈", "새해" }),
        ("산타의 웃음소리로 흔히 쓰는 말은?", "호호호", new[] { "하하하", "히히히", "크크크" }),
        ("크리스마스트리 꾸미기 풍습이 시작된 곳으로 알려진 나라는?", "독일", new[] { "미국", "일본", "브라질" }),
        ("노래 '루돌프 사슴코'에서 루돌프가 썰매를 끈 날의 날씨는?", "안개 낀 날", new[] { "비 오는 날", "바람 부는 날", "햇볕 쨍쨍한 날" }),
        ("캐럴에서 산타는 착한 아이 명단을 몇 번 확인할까요?", "두 번", new[] { "한 번", "세 번", "열 번" }),
        ("'X-mas'의 X는 어느 나라 글자에서 왔을까요?", "그리스어", new[] { "라틴어", "영어", "히브리어" }),
        ("호주에서 크리스마스는 어느 계절일까요?", "여름", new[] { "겨울", "봄", "가을" }),
        ("일본에서 크리스마스에 많이 먹기로 유명한 음식은?", "프라이드치킨", new[] { "초밥", "라멘", "떡국" }),
        ("영국의 전통 크리스마스 디저트는?", "크리스마스 푸딩", new[] { "마카롱", "티라미수", "와플" }),
        ("이탈리아의 크리스마스 빵은?", "파네토네", new[] { "바게트", "프레첼", "크루아상" }),
        ("독일의 크리스마스 과일빵 이름은?", "슈톨렌", new[] { "파네토네", "브리오슈", "베이글" }),
        ("프랑스의 통나무 모양 크리스마스 케이크는?", "부쉬 드 노엘", new[] { "마들렌", "에클레르", "크레페" }),
        ("'노엘'은 어느 나라 말로 크리스마스일까요?", "프랑스어", new[] { "독일어", "스페인어", "러시아어" }),
        ("독일어로 '메리 크리스마스'는?", "프로에 바이나흐텐", new[] { "부온 나탈레", "펠리스 나비다드", "주와이외 노엘" }),
        ("눈은 구름 속 무엇이 얼어서 생길까요?", "수증기", new[] { "모래", "소금", "먼지" }),
        ("북극곰의 털 아래 피부 색은?", "검은색", new[] { "하얀색", "분홍색", "노란색" }),
        ("펭귄이 주로 사는 곳은?", "남반구", new[] { "북극", "사하라 사막", "아마존 숲" }),
        ("물이 어는 온도는 섭씨 몇 도일까요?", "0도", new[] { "10도", "영하 10도", "4도" }),
        ("1년 중 밤이 가장 긴 절기는?", "동지", new[] { "하지", "춘분", "입동" }),
        ("동지에 먹는 음식은?", "팥죽", new[] { "떡국", "송편", "냉면" }),
        ("설날에 먹는 대표 음식은?", "떡국", new[] { "팥죽", "송편", "삼계탕" }),
        ("순록의 뿔에 대해 맞는 것은?", "암수 모두 뿔이 난다", new[] { "수컷만 뿔이 난다", "암컷만 뿔이 난다", "뿔이 없다" }),
        ("겨울잠을 자는 동물은?", "곰", new[] { "토끼", "사슴", "참새" }),
        ("크리스마스 양말을 영어로 흔히 뭐라고 할까요?", "스타킹", new[] { "미튼", "부츠", "머플러" }),
        ("엄지만 따로 있는 벙어리장갑을 영어로?", "미튼", new[] { "글러브", "삭스", "스카프" }),
        ("이글루는 무엇으로 지은 집일까요?", "눈 벽돌", new[] { "나무", "흙", "돌" }),
        ("애니메이션 '겨울왕국'의 눈사람 이름은?", "올라프", new[] { "스벤", "한스", "크리스토프" }),
        ("'겨울왕국'에서 엘사의 동생 이름은?", "안나", new[] { "벨", "에리얼", "라푼젤" }),
        ("캐럴 'White Christmas'를 처음 불러 유명해진 가수는?", "빙 크로스비", new[] { "엘비스 프레슬리", "프랭크 시나트라", "마이클 잭슨" }),
        ("머라이어 캐리의 대표 캐럴은?", "All I Want for Christmas Is You", new[] { "Last Christmas", "Jingle Bell Rock", "Silent Night" }),
        ("캐럴 'Last Christmas'를 부른 그룹은?", "왬!", new[] { "비틀스", "아바", "퀸" }),
        ("산타가 집에 들어올 때 쓴다는 통로는?", "굴뚝", new[] { "창문", "지하실", "현관문" }),
        ("트리에 다는 반짝이는 실 장식의 이름은?", "틴셀", new[] { "리본", "털실", "레이스" }),
        ("성탄절 별은 무엇을 기념할까요?", "베들레헴의 별", new[] { "북극성", "금성", "시리우스" }),
        ("예수가 태어났다고 전해지는 마을은?", "베들레헴", new[] { "나사렛", "예루살렘", "로마" }),
        ("동방박사는 보통 몇 명으로 전해질까요?", "3명", new[] { "2명", "5명", "12명" }),
        ("동방박사의 선물은 황금, 유향, 그리고?", "몰약", new[] { "은", "비단", "소금" }),
        ("어드벤트 캘린더는 무엇을 세는 달력일까요?", "크리스마스까지 남은 날", new[] { "새해까지 남은 날", "생일까지 남은 날", "방학까지 남은 날" }),
        ("크리스마스 카드를 처음 팔기 시작한 나라는?", "영국", new[] { "미국", "프랑스", "일본" }),
        ("소설 '크리스마스 캐럴'을 쓴 작가는?", "찰스 디킨스", new[] { "셰익스피어", "안데르센", "마크 트웨인" }),
        ("크리스마스 꽃으로 불리는 빨간 잎 식물은?", "포인세티아", new[] { "튤립", "해바라기", "장미" }),
        ("'호두까기 인형'은 어떤 공연일까요?", "발레", new[] { "오페라", "뮤지컬", "인형극" }),
        ("핫초코의 원료는?", "카카오", new[] { "커피콩", "녹차", "보리" }),
        ("겨울에 많이 먹는 주황색 과일은?", "귤", new[] { "수박", "참외", "복숭아" }),
        ("동지 팥죽에 넣는 동그란 찹쌀떡은?", "새알심", new[] { "가래떡", "절편", "인절미" }),
        ("북극 가까운 밤하늘에서 볼 수 있는 빛의 커튼은?", "오로라", new[] { "무지개", "신기루", "일식" }),
        ("큰 눈 축제(눈 조각 축제)로 유명한 일본 도시는?", "삿포로", new[] { "도쿄", "오사카", "교토" }),
        ("다음 중 겨울 올림픽 종목은?", "컬링", new[] { "수영", "양궁", "탁구" }),
        ("컬링에서 얼음 위로 미는 것은?", "스톤", new[] { "퍽", "공", "원반" }),
        ("2018년 겨울 올림픽이 열린 곳은?", "평창", new[] { "서울", "부산", "제주" }),
        ("평창 겨울 올림픽의 마스코트는?", "수호랑", new[] { "호돌이", "꿈돌이", "해치" }),
        ("아이스하키에서 치는 납작한 원반은?", "퍽", new[] { "스톤", "셔틀콕", "볼" }),
        ("트리에 다는 둥근 장식을 흔히 뭐라고 할까요?", "오너먼트 볼", new[] { "리스", "틴셀", "갈런드" }),
        ("트리를 빙 두르는 긴 장식 줄은?", "갈런드", new[] { "리스", "토퍼", "스커트" }),
        ("트리 꼭대기에 올리는 장식을 통틀어 부르는 말은?", "트리 토퍼", new[] { "트리 스커트", "갈런드", "리스" }),
        ("트리 밑동에 두르는 천의 이름은?", "트리 스커트", new[] { "트리 모자", "트리 양말", "트리 망토" }),
        ("겨울철 손을 따뜻하게 하는 일회용품은?", "핫팩", new[] { "아이스팩", "쿨팩", "물파스" }),
        ("가장 추운 대륙은?", "남극", new[] { "아시아", "유럽", "북아메리카" }),
        ("북극점이 있는 바다는?", "북극해", new[] { "남극해", "대서양", "인도양" }),
        ("영화 '나 홀로 집에' 1편의 배경 도시는?", "시카고 근처", new[] { "뉴욕", "로스앤젤레스", "마이애미" }),
        ("해리 포터가 1편 크리스마스에 받은 특별한 선물은?", "투명 망토", new[] { "빗자루", "지팡이", "부엉이" }),
        ("'찰리 브라운의 크리스마스'에 나오는 강아지는?", "스누피", new[] { "오디", "구피", "플루토" }),
        ("산타의 부인은 흔히 어떻게 부를까요?", "미세스 클로스", new[] { "퀸 산타", "마담 노엘", "레이디 루돌프" }),
        ("과자로 만든 작은 크리스마스 집은?", "진저브레드 하우스", new[] { "캔디 캐슬", "쿠키 타워", "초코 오두막" }),
        ("똑같이 생긴 눈송이 두 개를 찾을 가능성은?", "거의 없다", new[] { "아주 흔하다", "반반이다", "항상 같다" }),
        ("눈이 하얗게 보이는 까닭은?", "빛을 고르게 흩어서", new[] { "하얀 가루가 섞여서", "소금이 들어 있어서", "차가워서" }),
        ("눈 덮인 길에서 미끄러지지 않게 신발에 끼우는 것은?", "아이젠", new[] { "깔창", "뒤꿈치", "구두주걱" }),
        ("크리스마스 이브 자정에 드리는 미사를 흔히?", "자정 미사", new[] { "새벽 미사", "점심 미사", "저녁 기도" }),
        ("눈사람을 영어로?", "스노맨", new[] { "아이스맨", "윈터맨", "콜드맨" }),
        ("겨울에 강이 얼면 그 위에서 즐기는 놀이는?", "썰매", new[] { "수영", "카누", "서핑" }),
        ("산타 썰매 순록 중 노래로 가장 유명한 순록은?", "루돌프", new[] { "대셔", "댄서", "코멧" }),
        ("눈을 굴려 만드는 사람 모양 조각은?", "눈사람", new[] { "눈토끼", "얼음성", "눈꽃" }),
        ("크리스마스를 우리말로 부르는 다른 이름은?", "성탄절", new[] { "추석", "단오", "한식" }),
        ("'메리 크리스마스 앤드 해피 뉴 이어'의 뒷부분 뜻은?", "새해 복 많이 받으세요", new[] { "생일 축하해요", "잘 자요", "안녕히 가세요" }),
        ("영하 20도와 영하 5도 중 더 추운 것은?", "영하 20도", new[] { "영하 5도", "둘이 같다", "알 수 없다" }),
        ("스키장에서 위로 올라갈 때 타는 것은?", "리프트", new[] { "엘리베이터", "썰매", "자전거" }),
        ("캐럴 '징글벨'은 무엇을 타고 달리는 노래일까요?", "말이 끄는 썰매", new[] { "자전거", "기차", "배" }),
    };

    long QzRound(int i) => (long)(qzT0 / 1000) * 100 + i;   // 문제마다 겹치지 않는 번호
    bool QzActive => qzT0 > 0 && NetNow < qzT0 + QzCount * QzPerMs + 600;

    // 이번 판의 10문제 (씨앗으로 모두 같은 문제·같은 보기 순서)
    List<(string q, string[] choices, int correct)> qzSet; int qzSetSeed = int.MinValue;
    List<(string q, string[] choices, int correct)> QzSet()
    {
        if (qzSet != null && qzSetSeed == qzSeed) return qzSet;
        var rnd = new System.Random(qzSeed);
        var idx = Enumerable.Range(0, QuizBank.Length).OrderBy(_ => rnd.Next()).Take(QzCount).ToList();
        qzSet = new List<(string, string[], int)>();
        foreach (var k in idx)
        {
            var it = QuizBank[k];
            var list = new List<string> { it.a }; list.AddRange(it.wrong);
            var order = list.OrderBy(_ => rnd.Next()).ToList();
            qzSet.Add((it.q, order.ToArray(), order.IndexOf(it.a)));
        }
        qzSetSeed = qzSeed;
        return qzSet;
    }

    void OpenQuiz() { quizOpen = true; walkTarget = null; quizScore = MyQuizTotal(); qzInviteUntil = 0; ReadQuizSession(); qzBeat = 0; }

    int MyQuizTotal() { var s = net != null ? net.Get("quiz") : null; return s != null && s.TryGetValue(myKey, out var v) && v is Dictionary<string, object> d ? (int)AcJson.D(d, "score") : 0; }

    void ReadQuizSession()
    {
        var s = net != null ? net.Get("qzs") : null;
        if (s != null && s.TryGetValue("cur", out var v) && v is Dictionary<string, object> d)
        {
            double t0 = AcJson.D(d, "t0");
            if (t0 > qzT0) { qzT0 = t0; qzSeed = (int)AcJson.D(d, "seed"); }
        }
    }

    void StartQuiz()
    {
        qzT0 = Math.Floor(NetNow + QzLeadMs); qzSeed = UnityEngine.Random.Range(1, int.MaxValue);
        net?.Put("qzs/cur", "{\"t0\":" + qzT0.ToString("0") + ",\"seed\":" + qzSeed + ",\"by\":" + AcJson.Esc(uiName.Trim()) + "}");
        AddAct("qs", qzT0.ToString("0") + "," + qzSeed);
        myQuizAnswer.Clear(); qzScored = -1;
    }

    void QuizUpdate()
    {
        if (Key("esc")) { quizOpen = false; return; }
        if (Time.unscaledTime > qzBeat) { qzBeat = Time.unscaledTime + 1.5f; AddAct("qj", ""); ReadQuizSession(); }
        if (!QzActive) return;
        double el = NetNow - qzT0;
        if (el < 0) { int c = (int)Math.Ceiling(-el / 1000); if (c != qzLastTick && c <= 3) { qzLastTick = c; Sfx("tick", 0.5f); } return; }
        int qi = (int)(el / QzPerMs); if (qi >= QzCount) return;
        double inq = el - qi * QzPerMs;
        int left = (int)Math.Ceiling((QzAnswerMs - inq) / 1000);
        if (inq < QzAnswerMs && left <= 3 && left > 0 && qzLastTick != qi * 10 + left) { qzLastTick = qi * 10 + left; Sfx("tick", 0.45f); }
        long round = QzRound(qi);
        if (inq >= QzAnswerMs && qzScored != round)
        {
            qzScored = round;
            if (myQuizAnswer.TryGetValue(round, out int mine))
            {
                if (mine == QzSet()[qi].correct)
                {
                    Sfx("correct", 0.7f); quizScore = MyQuizTotal() + 1;
                    QuestProgress("quiz", 1);
                    net?.Put("quiz/" + myKey, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"score\":" + quizScore + ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
                }
                else Sfx("wrong", 0.6f);
            }
        }
    }

    // 이번 판 점수 (나 + 다른 사람)
    List<(string name, int score)> QzScores()
    {
        var sc = new Dictionary<string, (string name, int s)>();
        var set = QzSet();
        sc[myId] = (uiName.Trim(), 0);
        for (int i = 0; i < QzCount; i++)
        {
            long r = QzRound(i);
            if (myQuizAnswer.TryGetValue(r, out int m) && m == set[i].correct) sc[myId] = (sc[myId].name, sc[myId].s + 1);
            if (quizAnswers.TryGetValue(r, out var o))
                foreach (var kv in o) { var cur = sc.TryGetValue(kv.Key, out var c) ? c : (kv.Value.name, 0); if (kv.Value.ch == set[i].correct) cur.Item2++; sc[kv.Key] = cur; }
        }
        return sc.Values.Select(v => (v.name, v.s)).OrderByDescending(v => v.Item2).ToList();
    }

    void QuizGUI()
    {
        float pw = Mathf.Min(SW - 20, 640), ph = Mathf.Min(SH - 90, 380);
        var p = new Rect(SW / 2 - pw / 2, SH / 2 - ph / 2 + 20, pw, ph);
        GUI.Box(R(p), "", sBox);
        GUI.Label(new Rect(p.x + 16, p.y + 10, 300, 28), "크리스마스 퀴즈", sLabel);
        if (GUI.Button(new Rect(p.xMax - 90, p.y + 8, 80, 30), "<b>나가기</b>", sBtn)) { quizOpen = false; return; }
        var center = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var big = new GUIStyle(sLabel) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        double el = NetNow - qzT0;
        if (QzActive && el >= 0 && el < QzCount * QzPerMs)
        {
            int qi = (int)(el / QzPerMs); double inq = el - qi * QzPerMs; bool reveal = inq >= QzAnswerMs;
            var qz = QzSet()[qi]; long round = QzRound(qi);
            int myNow = 0; foreach (var sc0 in QzScores()) if (sc0.name == uiName.Trim()) { myNow = sc0.score; break; }
            GUI.Label(new Rect(p.x + 140, p.y + 14, 220, 22), $"<b>{qi + 1} / {QzCount}</b> 문제   <color=#2e7d32>맞힌 수 {myNow}</color>", sSmall);   // 내 점수도 보이게
            if (!reveal)
            {
                float frac = (float)(1 - inq / QzAnswerMs);
                GUI.DrawTexture(new Rect(p.x + 16, p.y + 44, p.width - 32, 8), AcPalette.Tex(new Color(0.85f, 0.8f, 0.72f)));
                GUI.DrawTexture(new Rect(p.x + 16, p.y + 44, (p.width - 32) * Mathf.Clamp01(frac), 8), AcPalette.Tex(frac < 0.3f ? new Color(0.9f, 0.35f, 0.3f) : new Color(0.95f, 0.65f, 0.25f)));
            }
            GUI.Label(new Rect(p.x + 20, p.y + 58, p.width - 40, 66), qz.q, big);
            myQuizAnswer.TryGetValue(round, out int mine); bool answered = myQuizAnswer.ContainsKey(round);
            float cw = (p.width - 48) / 2, chh = 50;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(p.x + 16 + (i % 2) * (cw + 16), p.y + 130 + (i / 2) * (chh + 10), cw, chh);
                Color border = new Color(0.86f, 0.8f, 0.72f);
                if (answered && mine == i) border = new Color(0.25f, 0.5f, 0.9f);
                if (reveal && i == qz.correct) border = new Color(0.3f, 0.7f, 0.35f);
                else if (reveal && answered && mine == i) border = new Color(0.9f, 0.3f, 0.3f);
                AcUiTheme.Ring(r, border, 3);
                var cs = new GUIStyle(sBtn) { wordWrap = true }; if (qz.choices[i].Length > 14) cs.fontSize = Mathf.Max(11, sBtn.fontSize - 3);
                if (GUI.Button(r, "<b>" + (i + 1) + ". " + qz.choices[i] + "</b>", cs) && !reveal && !answered)
                { myQuizAnswer[round] = i; AddAct("q", round + ":" + i); Sfx("click", 0.6f); }
            }
            quizAnswers.TryGetValue(round, out var others);
            float ly = p.y + 130 + 2 * (chh + 10) + 8;
            if (!reveal)
            {
                int cnt = (others?.Count ?? 0) + (answered ? 1 : 0);
                GUI.Label(new Rect(p.x + 16, ly, p.width - 32, 24), (answered ? "답했어요! " : $"{Math.Ceiling((QzAnswerMs - inq) / 1000)}초 안에 골라요  ") + $"({cnt}명 답함)", center);
            }
            else
            {
                var right = new List<string>();
                if (answered && mine == qz.correct) right.Add(uiName.Trim());
                if (others != null) foreach (var o in others.Values) if (o.ch == qz.correct) right.Add(o.name);
                GUI.Label(new Rect(p.x + 16, ly, p.width - 32, 26), "<b>정답은 '" + qz.choices[qz.correct] + "'</b>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(p.x + 16, ly + 28, p.width - 32, 40), right.Count > 0 ? string.Join(", ", right.Take(8)) + (right.Count > 8 ? " 외" : "") + " 님이 맞혔어요!" : "아무도 못 맞혔어요", center);
            }
            return;
        }
        if (QzActive && el < 0)
        {
            GUI.Label(new Rect(p.x + 20, p.y + 80, p.width - 40, 40), "퀴즈가 곧 시작해요!", big);
            GUI.Label(new Rect(p.x + 20, p.y + 130, p.width - 40, 80), $"<size=48><b>{Math.Ceiling(-el / 1000)}</b></size>", big);
            GUI.Label(new Rect(p.x + 20, p.y + 220, p.width - 40, 24), "10문제, 문제마다 10초", center);
            return;
        }
        // 모이는 화면 (끝난 판이 있으면 결과도)
        float y = p.y + 48;
        bool justEnded = qzT0 > 0 && NetNow - (qzT0 + QzCount * QzPerMs) < 90000;
        if (justEnded)
        {
            var sc = QzScores();
            GUI.Label(new Rect(p.x + 16, y, p.width - 32, 24), "<b>지난 판 결과</b>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            y += 28;
            string line = string.Join("   ", sc.Take(5).Select((x, i) => $"{i + 1}등 {Short(x.name, 6)} {x.score}개"));
            GUI.Label(new Rect(p.x + 16, y, p.width - 32, 44), line, center);
            y += 50;
        }
        foreach (var k in quizLobby.Where(kv => Time.unscaledTime - kv.Value.at > 4f).Select(kv => kv.Key).ToList()) quizLobby.Remove(k);
        var names = new List<string> { uiName.Trim() }; names.AddRange(quizLobby.Values.Select(v => v.name));
        GUI.Label(new Rect(p.x + 16, y, p.width - 32, 24), $"<b>모인 사람 {names.Count}명</b>", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
        y += 28;
        GUI.Label(new Rect(p.x + 16, y, p.width - 32, 44), string.Join(", ", names.Take(14).Select(n => Short(n, 6))) + (names.Count > 14 ? " …" : ""), center);
        y += 50;
        GUI.Label(new Rect(p.x + 16, y, p.width - 32, 22), "10문제를 같이 풀어요, 문제마다 10초", center);
        y += 30;
        float bw = Mathf.Min(170, (p.width - 48) / 2);
        if (GUI.Button(new Rect(p.center.x - bw - 8, y, bw, 40), "모두에게 알리기", sBtn)) { AddAct("qa", ""); Toast("모두에게 퀴즈 소식을 보냈어요"); }
        if (GUI.Button(new Rect(p.center.x + 8, y, bw, 40), justEnded ? "<b>다시 시작!</b>" : "<b>시작!</b>", sBtn)) StartQuiz();
        y += 50;
        var s = net != null ? net.Get("quiz") : null;
        if (s != null && s.Count > 0 && y + 40 < p.yMax)
        {
            var top = s.Values.OfType<Dictionary<string, object>>().Select(d => (AcJson.S(d, "name"), (int)AcJson.D(d, "score"))).OrderByDescending(x => x.Item2).Take(5).ToList();
            GUI.Label(new Rect(p.x + 16, y, p.width - 32, 40), "누적 퀴즈 왕: " + string.Join("   ", top.Select((x, i) => $"{i + 1}. {Short(x.Item1, 6)} {x.Item2}")), center);
        }
    }

    // ========================================================
    // 눈싸움
    bool snowMode; int sbHits, sbTaken; float sbCool, sbNextWrite;
    class Snowball { public Vector3 a, b; public float t, T, h; public GameObject go; public string owner, ownerName; public bool big; }
    readonly List<Snowball> snowballs = new List<Snowball>();
    readonly List<(GameObject go, Vector3 v, float life)> sbBits = new List<(GameObject, Vector3, float)>();
    readonly List<(GameObject go, float until)> sbCaps = new List<(GameObject, float)>();   // 머리에 얹힌 눈 (큰 눈에 맞으면)
    bool sbBig = true;                 // 큰 눈(가까이, 세게) / 작은 눈(멀리, 빠르게)
    float sbHitFx; bool sbHitBig; Vector2 sbHitAt; Texture2D sbSplatTex;   // 내가 맞았을 때 화면 효과

    void ToggleSnowFight()
    {
        snowMode = !snowMode;
        if (snowMode && me.skating) { me.SetSkates(false); skateVel = Vector3.zero; }   // 스케이트와 눈싸움은 함께 못 해요
        if (snowMode) Toast(IsMobile ? "눈싸움! 친구나 땅을 톡 누르면 던져요 (큰 눈/작은 눈 고르기)" : "눈싸움! 클릭한 곳으로 던져요 (F: 앞으로, 1/2: 큰 눈/작은 눈)");
    }

    void ThrowSnowball(Vector3 target)
    {
        if (sbCool > 0) return;
        sbCool = sbBig ? 0.6f : 0.28f;
        float range = sbBig ? 16f : 30f;
        var from = me.transform.position;
        var d = target - from; d.y = 0;
        if (d.magnitude > range) target = from + d.normalized * range;
        if (d.sqrMagnitude > 0.01f) { yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; me.transform.rotation = Quaternion.Euler(0, yaw, 0); }
        var start = from + Vector3.up * 1.1f + me.transform.forward * 0.3f;
        Emote("throw"); Sfx("throw", sbBig ? 0.6f : 0.45f, sbBig ? 0.9f : 1.25f);
        AddAct("s", $"{start.x:F2},{start.z:F2},{target.x:F2},{target.z:F2},{(sbBig ? 1 : 0)}");
        SpawnSnowball(start, target, myId, uiName.Trim(), sbBig);
    }

    void SpawnSnowball(Vector3 a, Vector3 b, string owner, string ownerName, bool big)
    {
        a.y = 1.1f;
        float dist = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        var go = AcCatalog.Spawn("sm_snowball", world, null); go.transform.localScale = Vector3.one * (big ? 1.6f : 0.7f);
        snowballs.Add(new Snowball { a = a, b = new Vector3(b.x, 0.5f, b.z), big = big,
            T = big ? Mathf.Clamp(dist / 12f, 0.25f, 1.1f) : Mathf.Clamp(dist / 24f, 0.15f, 1.1f),
            h = big ? 0.6f + dist * 0.12f : 0.3f + dist * 0.05f, go = go, owner = owner, ownerName = ownerName });
    }

    // 눈에 맞은 캐릭터 효과: 큰 눈 = 머리에 눈이 얹히고 크게 휘청, 작은 눈 = 톡 튀는 눈가루
    void HitFx(Transform who, bool big)
    {
        if (!who) return;
        var c = who.GetComponent<AcCharacter>();
        if (big)
        {
            if (c) c.PlayEmote("hit");
            Splash(who.position + Vector3.up * 1.3f, true);
            var cap = AcCatalog.Spawn("sm_snowball", who, null);
            cap.transform.localPosition = new Vector3(0, 1.55f, 0); cap.transform.localScale = new Vector3(1.5f, 0.7f, 1.5f);
            sbCaps.Add((cap, Time.unscaledTime + 2.2f));
        }
        else
        {
            for (int k = 0; k < 5; k++)
            {
                var g = AcCatalog.Spawn("sm_snowball", world, null); g.transform.position = who.position + Vector3.up * 0.9f; g.transform.localScale = Vector3.one * 0.22f;
                sbBits.Add((g, new Vector3(UnityEngine.Random.Range(-1.4f, 1.4f), UnityEngine.Random.Range(0.8f, 2f), UnityEngine.Random.Range(-1.4f, 1.4f)), 0.35f));
            }
            if (c) c.PlayEmote("flinch");
        }
    }

    void SnowballUpdate(float dt)
    {
        sbCool -= dt;
        if (snowMode && !chatting && !askOpen)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) ThrowSnowball(me.transform.position + me.transform.forward * (sbBig ? 8f : 16f));
            if (kb != null && kb.digit1Key.wasPressedThisFrame) sbBig = true;
            if (kb != null && kb.digit2Key.wasPressedThisFrame) sbBig = false;
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
            Splash(s.b, s.big);
            if (s.go) Destroy(s.go);
            snowballs.RemoveAt(i);
            float hr = s.big ? 1.1f : 0.7f;
            // 맞았는지: 던진 사람이 나면 다른 사람을, 남이 던졌으면 나를 확인 (효과는 모두에게)
            foreach (var kv in remotes)
                if (kv.Key != s.owner && kv.Value.c && Flat(kv.Value.c.transform.position, s.b) < hr)
                {
                    HitFx(kv.Value.c.transform, s.big);
                    if (s.owner == myId)
                    {
                        sbHits++; Sfx("splat", s.big ? 0.85f : 0.6f, s.big ? 0.85f : 1.35f);
                        Toast($"{kv.Value.net?.name} 님을 맞혔어요! ({sbHits})");
                        if (Time.unscaledTime > sbNextWrite) { sbNextWrite = Time.unscaledTime + 1f; SaveSnowFight(); }
                    }
                    break;
                }
            if (s.owner == myId) foreach (var n in npcs) if (n && n.gameObject.activeSelf && Flat(n.transform.position, s.b) < hr) { HitFx(n.transform, s.big); Sfx("splat", 0.6f, s.big ? 0.85f : 1.35f); }
            if (s.owner != myId && Flat(me.transform.position, s.b) < hr)
            {
                sbTaken++; HitFx(me.transform, s.big); Sfx("splat", s.big ? 0.95f : 0.7f, s.big ? 0.8f : 1.3f);
                sbHitFx = s.big ? 1.6f : 0.7f; sbHitBig = s.big;
                sbHitAt = new Vector2(UnityEngine.Random.Range(0.2f, 0.8f), UnityEngine.Random.Range(0.25f, 0.65f));
                Toast($"{s.ownerName} 님의 {(s.big ? "큰 눈덩이" : "작은 눈덩이")}에 맞았어요!");
            }
        }
        for (int i = sbBits.Count - 1; i >= 0; i--)
        {
            var b = sbBits[i]; b.life -= dt; b.v.y -= 9f * dt;
            if (b.go) { b.go.transform.position += b.v * dt; b.go.transform.localScale *= 1 - dt * 1.5f; }
            sbBits[i] = b;
            if (b.life <= 0) { if (b.go) Destroy(b.go); sbBits.RemoveAt(i); }
        }
        for (int i = sbCaps.Count - 1; i >= 0; i--)
        {
            var (go, until) = sbCaps[i];
            float left = until - Time.unscaledTime;
            if (go && left < 0.5f) go.transform.localScale = new Vector3(1.5f, 0.7f, 1.5f) * Mathf.Max(0.01f, left / 0.5f);
            if (left <= 0 || !go) { if (go) Destroy(go); sbCaps.RemoveAt(i); }
        }
        if (sbHitFx > 0) sbHitFx -= dt;
    }

    void Splash(Vector3 at, bool big = true)
    {
        int n = big ? 10 : 4;
        for (int k = 0; k < n; k++)
        {
            var g = AcCatalog.Spawn("sm_snowball", world, null); g.transform.position = at; g.transform.localScale = Vector3.one * (big ? 0.6f : 0.3f);
            float sp = big ? 2.2f : 1.2f;
            var v = new Vector3(UnityEngine.Random.Range(-sp, sp), UnityEngine.Random.Range(1.5f, big ? 3.5f : 2.2f), UnityEngine.Random.Range(-sp, sp));
            sbBits.Add((g, v, big ? 0.7f : 0.4f));
        }
    }

    // 내가 맞았을 때 화면에 눈이 철썩 (큰 눈은 크게, 작은 눈은 귀퉁이에 작게)
    void SnowHitOverlay()
    {
        if (sbHitFx <= 0 || Event.current.type != EventType.Repaint) return;
        if (sbSplatTex == null)
        {
            sbSplatTex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var rnd = new System.Random(7);
            var blobs = new List<Vector3>();
            for (int k = 0; k < 14; k++) { float ang = k / 14f * Mathf.PI * 2; float rr = 30 + (float)rnd.NextDouble() * 18; blobs.Add(new Vector3(64 + Mathf.Cos(ang) * rr, 64 + Mathf.Sin(ang) * rr, 8 + (float)rnd.NextDouble() * 10)); }
            blobs.Add(new Vector3(64, 64, 40));
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    float a = 0;
                    foreach (var b in blobs) a = Mathf.Max(a, Mathf.Clamp01((b.z - new Vector2(x - b.x, y - b.y).magnitude) / 2.5f));
                    float sh = 0.9f + 0.1f * (y / 127f);
                    sbSplatTex.SetPixel(x, y, new Color(sh, sh, 1f, a));
                }
            sbSplatTex.Apply();
        }
        float life = sbHitBig ? 1.6f : 0.7f, k2 = Mathf.Clamp01(sbHitFx / life);
        float alpha = Mathf.Clamp01(k2 * 1.6f) * (sbHitBig ? 0.92f : 0.85f);
        float size = sbHitBig ? Mathf.Max(SW, SH) * 0.95f : Mathf.Min(SW, SH) * 0.32f;
        size *= 1f + (1 - k2) * 0.08f;
        var c = new Vector2(sbHitAt.x * SW, sbHitAt.y * SH + (1 - k2) * (sbHitBig ? 60 : 20));   // 천천히 흘러내림
        var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(c.x - size / 2, c.y - size / 2, size, size), sbSplatTex);
        if (sbHitBig) { GUI.color = new Color(1, 1, 1, alpha * 0.35f); GUI.DrawTexture(new Rect(0, 0, SW, SH), Texture2D.whiteTexture); }
        GUI.color = old;
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
        SnowHitOverlay();
        if (!snowMode) return;
        float w = 230, top = chatLog.Count > 0 ? 30 + 22 * chatLog.Count : SW < 1000 ? 112 : 60;
        var s = net != null ? net.Get("sbf") : null;
        var top3 = s == null ? new List<(string, int)>() : s.Values.OfType<Dictionary<string, object>>().Select(d => (AcJson.S(d, "name"), (int)AcJson.D(d, "hits"))).Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).Take(3).ToList();
        var r = new Rect(12, top, w, 36 + top3.Count * 20);
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 12, r.y + 6, w - 20, 24), $"<b>눈싸움</b>  맞힘 {sbHits}, 맞음 {sbTaken}", sSmall);
        // 큰 눈 / 작은 눈 고르기
        var br = new Rect(r.x, r.yMax + 6, w, 40);
        GUI.Box(R(br), "", sBox);
        for (int i = 0; i < 2; i++)
        {
            var b = new Rect(br.x + 8 + i * ((w - 22) / 2 + 6), br.y + 5, (w - 22) / 2, 30);
            bool on = (i == 0) == sbBig;
            if (on) AcUiTheme.Ring(b, AcUiTheme.Accent, 3);
            if (GUI.Button(b, i == 0 ? "큰 눈 (세게)" : "작은 눈 (멀리)", sBtn)) sbBig = i == 0;
        }
        {
            for (int i = 0; i < top3.Count; i++) GUI.Label(new Rect(r.x + 12, r.y + 30 + i * 20, w - 20, 20), $"{i + 1}. {Short(top3[i].Item1, 8)}  {top3[i].Item2}번 맞힘", sSmall);
        }
    }
}

// ===== 베개 싸움: 침실에 들어가면 자동으로 베개를 들고, 톡 누르면 휘둘러요 =====
public partial class AcRoom
{
    bool pillowMode, pillowWas; float pwCool, pwNextKnock; int pwHits, pwTaken; Vector3 pwKnock;
    readonly List<(string owner, string name, Vector3 from, float yaw, float at)> pwSwings = new List<(string, string, Vector3, float, float)>();
    readonly List<(GameObject go, Vector3 v, float life, float spin)> feathers = new List<(GameObject, Vector3, float, float)>();

    bool InBedroom(Vector3 p) => InHouse(p) && AreaAt(p) == "침실";

    void PillowUpdate(float dt)
    {
        pillowMode = phase == Phase.Play && !playingGame && !building && me != null && InBedroom(me.transform.position);
        if (pillowMode != pillowWas)
        {
            pillowWas = pillowMode;
            if (pillowMode) Toast(IsMobile ? "베개 싸움! '베개 휘두르기'를 누르거나 화면을 톡 눌러요" : "베개 싸움! 클릭이나 F, '베개 휘두르기'로 휘둘러요");
        }
        if (me) { me.holdPillow = pillowMode; me.pillowColor = Color.Lerp(me.look.sweater, Color.white, 0.55f); }
        foreach (var r in remotes.Values) if (r.c) { r.c.holdPillow = InBedroom(r.c.transform.position); r.c.pillowColor = Color.Lerp(r.c.look.sweater, Color.white, 0.55f); }
        pwCool -= dt;
#if ENABLE_INPUT_SYSTEM
        if (pillowMode && !chatting && !askOpen && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) SwingPillow(null);
#endif
        // 휘두른 뒤 0.2초에 맞았는지 확인 (휘두른 사람 앞쪽 1.8m, 좌우 75도 안)
        for (int i = pwSwings.Count - 1; i >= 0; i--)
        {
            var s = pwSwings[i];
            if (Time.unscaledTime - s.at < 0.2f) continue;
            pwSwings.RemoveAt(i);
            var fwd = Quaternion.Euler(0, s.yaw, 0) * Vector3.forward;
            bool Hit(Vector3 p) { var d = p - s.from; d.y = 0; return d.magnitude < 2.9f && d.magnitude > 0.05f && Vector3.Angle(fwd, d) < 85f; }   // 리치 길게 (내딛으며 휘두름)
            foreach (var kv in remotes)
                if (kv.Key != s.owner && kv.Value.c && Hit(kv.Value.c.transform.position))
                {
                    PillowHitFx(kv.Value.c, s.from);
                    if (s.owner == myId) { pwHits++; Toast($"{kv.Value.net?.name} 님을 퍽! ({pwHits})"); }
                }
            if (s.owner == myId) foreach (var n in npcs) if (n && n.gameObject.activeSelf && Hit(n.transform.position)) PillowHitFx(n, s.from);
            if (s.owner != myId && me && Hit(me.transform.position))
            {
                pwTaken++; PillowHitFx(me, s.from);
                var away = me.transform.position - s.from; away.y = 0; pwKnock = away.normalized * 3.2f;   // 뒤로 밀려남
                Toast($"{s.name} 님의 베개에 퍽!");
            }
        }
        // 밀려나기
        if (pwKnock.sqrMagnitude > 0.01f && me)
        {
            var prev = me.transform.position; var p = Collide(prev + pwKnock * dt, 0.36f);
            me.transform.position = p; pwKnock = Vector3.MoveTowards(pwKnock, Vector3.zero, dt * 9f);
        }
        // 깃털: 천천히 하늘하늘 떨어짐
        for (int i = feathers.Count - 1; i >= 0; i--)
        {
            var f = feathers[i]; f.life -= dt;
            f.v.y = Mathf.Max(f.v.y - 3.5f * dt, -0.45f); f.v.x *= 1 - dt * 1.5f; f.v.z *= 1 - dt * 1.5f;
            if (f.go)
            {
                f.go.transform.position += (f.v + new Vector3(Mathf.Sin(f.life * 6f + f.spin) * 0.5f, 0, Mathf.Cos(f.life * 5f + f.spin) * 0.4f)) * dt;
                f.go.transform.rotation = Quaternion.Euler(Mathf.Sin(f.life * 4f + f.spin) * 50f, f.spin * 60f + f.life * 90f, Mathf.Cos(f.life * 5f) * 40f);
                if (f.life < 0.5f) f.go.transform.localScale = new Vector3(0.11f, 0.02f, 0.2f) * Mathf.Max(0.01f, f.life / 0.5f);
            }
            feathers[i] = f;
            if (f.life <= 0) { if (f.go) Destroy(f.go); feathers.RemoveAt(i); }
        }
    }

    void SwingPillow(Vector3? target)
    {
        if (pwCool > 0 || !pillowMode) return;
        pwCool = 0.5f;
        if (target.HasValue) { var d = target.Value - me.transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) { yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; me.transform.rotation = Quaternion.Euler(0, yaw, 0); } }
        me.PlayEmote("swing"); Sfx("whoosh", 0.55f, UnityEngine.Random.Range(0.85f, 1.0f));
        pwKnock = Quaternion.Euler(0, yaw, 0) * Vector3.forward * 3.4f;   // 앞으로 한 발 내딛기
        AddAct("w", $"{me.transform.position.x:F2},{me.transform.position.z:F2},{yaw:F0}");
        pwSwings.Add((myId, uiName.Trim(), me.transform.position, yaw, Time.unscaledTime));
    }

    void OnRemoteSwing(string rid, Remote r, string data)
    {
        var v = data.Split(',');
        if (v.Length < 3 || !float.TryParse(v[0], out float x) || !float.TryParse(v[1], out float z) || !float.TryParse(v[2], out float yw)) return;
        if (r.c) r.c.PlayEmote("swing");
        if (me && Vector3.Distance(me.transform.position, new Vector3(x, 0, z)) < 14f) Sfx("whoosh", 0.35f, UnityEngine.Random.Range(0.92f, 1.1f));
        pwSwings.Add((rid, r.net.name, new Vector3(x, 0, z), yw, Time.unscaledTime));
    }

    // 맞은 캐릭터: 말랑 찌그러짐(bonk) + 깃털 펑 + 푹신한 '퍽'
    void PillowHitFx(AcCharacter c, Vector3 from)
    {
        if (!c) return;
        c.PlayEmote("bonk");
        var at = c.transform.position + Vector3.up * 0.9f;
        Sfx("pillow_hit", me && Vector3.Distance(me.transform.position, at) < 12f ? 0.85f : 0.4f, UnityEngine.Random.Range(0.9f, 1.12f));
        for (int k = 0; k < 12; k++)
        {
            var g = AcCatalog.Spawn("sm_snowball", world, null); if (!g) break;
            g.transform.position = at + UnityEngine.Random.insideUnitSphere * 0.25f;
            g.transform.localScale = new Vector3(0.11f, 0.02f, 0.2f);
            var away = (c.transform.position - from); away.y = 0; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            var v = away * UnityEngine.Random.Range(0.5f, 2f) + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(1.2f, 2.8f), UnityEngine.Random.Range(-1.5f, 1.5f));
            feathers.Add((g, v, UnityEngine.Random.Range(1.6f, 2.6f), UnityEngine.Random.Range(0f, 6f)));
        }
    }

    void PillowHUD()
    {
        if (!pillowMode) return;
        float w = 230, top = chatLog.Count > 0 ? 30 + 22 * chatLog.Count : SW < 1000 ? 112 : 60;
        var r = new Rect(12, top, w, 36);
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 12, r.y + 7, w - 20, 24), $"<b>베개 싸움</b>  퍽! {pwHits}, 맞음 {pwTaken}", sSmall);
    }
}
