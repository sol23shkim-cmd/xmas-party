// AcQuest.cs - 오늘의 퀘스트 + 퀘스트 포인트 보상 (11-27)
// 매일(한국 시간 자정 기준) 사람마다 가벼운 퀘스트 3개. 하나 끝낼 때마다 퀘스트 포인트 +1.
// 보상: 3포인트 = 반짝이는 이름표 / 7포인트 = 펫 시스템 열림 + 루돌프 / 그 뒤 3포인트마다 새 펫 하나(남은 펫 중 랜덤)
// 저장: orn/x~quest~<이름키> = {qp, pets:"rudolph,fox", day, prog:"piano:2|skate:1", done:"piano", sc:"눈사람id,..."} + 이 기기 PlayerPrefs (둘 중 큰 쪽)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AcRoom
{
    class QuestDef { public string id, text; public int target; public bool max; public QuestDef(string id, string text, int target, bool max = false) { this.id = id; this.text = text; this.target = target; this.max = max; } }
    static readonly QuestDef[] QuestPool = {
        new QuestDef("piano", "피아노로 노래 {0}곡 끝까지 연주하기", 3),
        new QuestDef("skate", "스케이트 타고 집 {0}바퀴 돌기", 2, true),
        new QuestDef("snowc", "눈사람 {0}개에 감상평 남기기", 2),
        new QuestDef("cookie", "쿠키 달리기 {0}점 넘기기", 3000, true),
        new QuestDef("candy", "캔디 팡 {0}점 넘기기", 8000, true),
        new QuestDef("kitchen", "주방 게임에서 별 1개 이상 받기", 1),
        new QuestDef("quiz", "크리스마스 퀴즈 {0}문제 맞히기", 3, true),
        new QuestDef("orn", "트리에 장식 {0}개 달기", 3),
        new QuestDef("water", "화분에 물 {0}번 주기", 2),
        new QuestDef("gift", "선물 포장해서 친구에게 주기", 1),
        new QuestDef("letter", "친구에게 편지 쓰기", 1),
    };
    const string AllPetsName = "상환";   // 모든 펫을 처음부터
    // 산타: 이 기기에서 원래 '산타'로 놀던 사람만 쓸 수 있음 (다른 기기는 주소에 ?santakey=rudolph 를 붙여 한 번 열면 됨)
    public const string SantaName = "산타";
    bool IsSantaMe => uiName != null && uiName.Trim() == SantaName && SantaAllowed;
    bool SantaAllowed
    {
        get
        {
            if (PlayerPrefs.GetInt("santa_ok", 0) == 1) return true;
            if (Application.absoluteURL.Contains("santakey=rudolph") || PlayerPrefs.GetString("xmas_name", "") == SantaName) { PlayerPrefs.SetInt("santa_ok", 1); PlayerPrefs.Save(); return true; }
            return false;
        }
    }

    int questDay = -1, questPoints; readonly List<string> questToday = new List<string>();
    readonly Dictionary<string, int> questProg = new Dictionary<string, int>();
    readonly HashSet<string> questDone = new HashSet<string>(), questSnowc = new HashSet<string>();
    readonly List<string> petsOwned = new List<string>();
    bool questLoaded, questDirty; float questSaveAt, questMergeAt; bool questOpen;
    float skateAcc, skateLastAng = float.NaN;

    static int TodayKst => (int)Math.Floor((AcSync.UnixMs + 9 * 3600000.0) / 86400000.0);
    bool AllPets => myKey == AllPetsName;
    bool PetUnlocked => AllPets || IsSantaMe || questPoints >= 7 || petsOwned.Count > 0;
    bool FancyName => questPoints >= 3;
    List<string> MyPets => AllPets ? PetIds.ToList() : petsOwned;
    string CurPet
    {
        get { if (IsSantaMe) return "rudolph"; var p = PlayerPrefs.GetString("pet_cur", ""); return p == "-" ? "" : (MyPets.Contains(p) ? p : (MyPets.Count > 0 ? MyPets[0] : "")); }
        set { PlayerPrefs.SetString("pet_cur", string.IsNullOrEmpty(value) ? "-" : value); PlayerPrefs.Save(); }
    }

    QuestDef QDef(string id) => QuestPool.FirstOrDefault(q => q.id == id);
    static string QText(QuestDef q) => string.Format(q.text, q.target);

    // 오늘의 3개: 날짜 + 이름으로 정해짐 (같은 날 같은 사람은 어느 기기에서나 같음)
    void PickToday(int day)
    {
        questToday.Clear();
        int seed = day * 7919; foreach (var ch in myKey) seed = seed * 31 + ch;
        var rng = new System.Random(seed);
        var ids = QuestPool.Select(q => q.id).OrderBy(_ => rng.Next()).ToList();
        questToday.AddRange(ids.Take(3));
    }

    void QuestLoadLocal()
    {
        int day = TodayKst;
        questPoints = PlayerPrefs.GetInt("quest_qp", 0);
        petsOwned.Clear(); petsOwned.AddRange(PlayerPrefs.GetString("quest_pets", "").Split(',').Where(p => Array.IndexOf(PetIds, p) >= 0).Distinct());
        questProg.Clear(); questDone.Clear(); questSnowc.Clear();
        if (PlayerPrefs.GetInt("quest_day", -1) == day)
        {
            ParseProg(PlayerPrefs.GetString("quest_prog", ""), questProg);
            foreach (var d in PlayerPrefs.GetString("quest_done", "").Split(',')) if (d != "") questDone.Add(d);
            foreach (var d in PlayerPrefs.GetString("quest_sc", "").Split(',')) if (d != "") questSnowc.Add(d);
        }
        questDay = day; PickToday(day);
    }
    static void ParseProg(string s, Dictionary<string, int> into)
    {
        foreach (var part in (s ?? "").Split('|')) { var kv = part.Split(':'); if (kv.Length == 2 && int.TryParse(kv[1], out int v)) into[kv[0]] = Math.Max(into.TryGetValue(kv[0], out int o) ? o : 0, v); }
    }
    string ProgStr => string.Join("|", questProg.Select(kv => kv.Key + ":" + kv.Value));

    void QuestSaveLocal()
    {
        PlayerPrefs.SetInt("quest_qp", questPoints); PlayerPrefs.SetString("quest_pets", string.Join(",", petsOwned));
        PlayerPrefs.SetInt("quest_day", questDay); PlayerPrefs.SetString("quest_prog", ProgStr);
        PlayerPrefs.SetString("quest_done", string.Join(",", questDone)); PlayerPrefs.SetString("quest_sc", string.Join(",", questSnowc));
        PlayerPrefs.Save();
    }

    void QuestUpdate(float dt)
    {
        if (me == null || string.IsNullOrWhiteSpace(uiName)) return;
        if (!questLoaded) { questLoaded = true; QuestLoadLocal(); questMergeAt = Time.unscaledTime + 2f; }
        if (TodayKst != questDay) { questDay = TodayKst; questProg.Clear(); questDone.Clear(); questSnowc.Clear(); PickToday(questDay); questDirty = true; }
        // 서버 기록과 합치기 (처음 한 번 + 가끔): 다른 기기에서 한 것도 이어짐
        if (questMergeAt > 0 && Time.unscaledTime > questMergeAt && net != null && !net.Offline)
        {
            questMergeAt = -1;
            var all = net.Get("quest");
            if (all != null && all.TryGetValue(myKey, out var v) && v is Dictionary<string, object> d)
            {
                int qp = (int)AcJson.D(d, "qp");
                if (qp > questPoints) { questPoints = qp; questDirty = true; }
                foreach (var p in AcJson.S(d, "pets").Split(',')) if (Array.IndexOf(PetIds, p) >= 0 && !petsOwned.Contains(p)) { petsOwned.Add(p); questDirty = true; }
                if ((int)AcJson.D(d, "day") == questDay)
                {
                    int before = questProg.Values.Sum(); ParseProg(AcJson.S(d, "prog"), questProg); if (questProg.Values.Sum() != before) questDirty = true;
                    foreach (var x in AcJson.S(d, "done").Split(',')) if (x != "" && questDone.Add(x)) questDirty = true;
                    foreach (var x in AcJson.S(d, "sc").Split(',')) if (x != "" && questSnowc.Add(x)) questDirty = true;
                }
            }
            GrantPets(false);
            questDirty = true;
        }
        // 스케이트로 집 둘레 돌기: 집 가운데를 기준으로 돈 각도를 모음
        if (me.skating && Outside)
        {
            var p = me.transform.position; float ang = Mathf.Atan2(p.z, p.x);
            if (!float.IsNaN(skateLastAng)) skateAcc += Mathf.DeltaAngle(skateLastAng * Mathf.Rad2Deg, ang * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            skateLastAng = ang;
            int laps = Mathf.FloorToInt(Mathf.Abs(skateAcc) / (Mathf.PI * 2f));
            if (laps > 0) QuestProgress("skate", laps, true);
        }
        else skateLastAng = float.NaN;
        if (questDirty && Time.unscaledTime > questSaveAt) { questDirty = false; questSaveAt = Time.unscaledTime + 3f; QuestSave(); }
    }

    void QuestSave()
    {
        QuestSaveLocal();
        if (net == null || net.Offline || string.IsNullOrWhiteSpace(uiName)) return;
        net.Put("quest/" + myKey, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"qp\":" + questPoints + ",\"pets\":" + AcJson.Esc(string.Join(",", petsOwned)) +
                ",\"day\":" + questDay + ",\"prog\":" + AcJson.Esc(ProgStr) + ",\"done\":" + AcJson.Esc(string.Join(",", questDone)) + ",\"sc\":" + AcJson.Esc(string.Join(",", questSnowc)) +
                ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
    }

    // 퀘스트 진행: max=true면 '최고 기록', 아니면 더하기
    void QuestProgress(string id, int amount, bool asMax = false)
    {
        if (!questLoaded || !questToday.Contains(id) || questDone.Contains(id)) return;
        var q = QDef(id); if (q == null) return;
        int cur = questProg.TryGetValue(id, out int c) ? c : 0;
        int nv = asMax ? Math.Max(cur, amount) : cur + amount;
        if (nv == cur) return;
        questProg[id] = Math.Min(nv, q.target); questDirty = true;
        if (nv >= q.target)
        {
            questDone.Add(id); questPoints++;
            Sfx("candy_win", 0.5f); FxBurst("star", me.transform.position + Vector3.up * 1.6f, 18, 2f, 0.06f, FxGold, 1.3f, 1f);
            string extra = questPoints == 3 ? "  이름표가 반짝반짝해졌어요!" : "";
            Toast($"퀘스트 완료! '{QText(q)}'  포인트 +1 (모두 {questPoints}){extra}");
            GrantPets(true);
            questSaveAt = 0;
        }
        else if (!asMax || id == "skate") Toast($"퀘스트: {QText(q)}  ({questProg[id]}/{q.target})");
    }
    void QuestSnowman(string snowId) { if (questToday.Contains("snowc") && !questDone.Contains("snowc") && questSnowc.Add(snowId)) QuestProgress("snowc", 1); }

    // 포인트만큼 펫 주기: 7포인트에 루돌프, 그 뒤 3포인트마다 하나씩(랜덤)
    void GrantPets(bool announce)
    {
        int want = questPoints >= 7 ? Mathf.Min(PetIds.Length, 1 + (questPoints - 7) / 3) : 0;
        bool first = petsOwned.Count == 0;
        while (petsOwned.Count < want)
        {
            string p;
            if (!petsOwned.Contains("rudolph")) p = "rudolph";
            else { var left = PetIds.Where(x => !petsOwned.Contains(x)).ToList(); if (left.Count == 0) break; p = left[UnityEngine.Random.Range(0, left.Count)]; }
            petsOwned.Add(p); questDirty = true;
            if (announce) Toast(first ? $"펫이 열렸어요! '{PetName(p)}'가 따라다녀요. 꾸미기에서 바꿀 수 있어요" : $"새 펫을 얻었어요: {PetName(p)}! 꾸미기에서 골라 보세요");
            if (first) CurPet = p;
            first = false;
        }
    }

    // ===== 화면 =====
    void QuestHUD()
    {
        if (phase != Phase.Play || !questLoaded || questToday.Count == 0) return;
        int done = questToday.Count(id => questDone.Contains(id));
        float w = IsMobile ? 132 : 124, h = IsMobile ? 38 : 34;
        var r = new Rect(12, IsMobile ? 104 : 96, w, h);
        bool all = done >= questToday.Count;
        if (GUI.Button(R(r), $"<b>오늘의 퀘스트 {done}/{questToday.Count}</b>", all ? sBtn : sBtnDeco ?? sBtn)) questOpen = !questOpen;
        if (!all) AcUiTheme.Star(new Rect(r.xMax - 14, r.y - 6, 16, 16), AcUiTheme.Gold);
    }

    void QuestWindow()
    {
        if (!questOpen || phase != Phase.Play) return;
        float w = Mathf.Min(420, SW - 24), h = 330;
        var r = new Rect(SW / 2 - w / 2, Mathf.Max(60, SH / 2 - h / 2 - 40), w, h);
        GUI.Box(R(r), "", sBox);
        GUI.Label(new Rect(r.x + 18, r.y + 12, w - 120, 30), "<size=19><b>오늘의 퀘스트</b></size>", sLabel);
        if (GUI.Button(new Rect(r.xMax - 82, r.y + 12, 66, 30), "닫기", sBtn)) questOpen = false;
        float y = r.y + 52;
        foreach (var id in questToday)
        {
            var q = QDef(id); if (q == null) continue;
            bool ok = questDone.Contains(id); int pv = questProg.TryGetValue(id, out int v) ? v : 0;
            var row = new Rect(r.x + 14, y, w - 28, 54);
            AcUiTheme.Fill(row, ok ? new Color(0.9f, 0.97f, 0.88f) : new Color(1f, 0.98f, 0.94f), 2);
            var cb = new Rect(row.x + 10, row.y + 14, 26, 26);
            AcUiTheme.Circle(cb, ok ? new Color(0.35f, 0.7f, 0.4f) : new Color(0.88f, 0.85f, 0.8f));
            if (ok) GUI.Label(cb, "<b><color=#ffffff>V</color></b>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(row.x + 46, row.y + 6, row.width - 56, 24), "<size=14>" + QText(q) + "</size>", sSmall);
            var bar = new Rect(row.x + 46, row.y + 34, row.width - 120, 8);
            AcUiTheme.Fill(bar, new Color(0.86f, 0.83f, 0.79f), 0);
            AcUiTheme.Fill(new Rect(bar.x, bar.y, Mathf.Max(4, bar.width * Mathf.Clamp01(ok ? 1f : pv / (float)q.target)), bar.height), ok ? new Color(0.35f, 0.7f, 0.4f) : new Color(1f, 0.7f, 0.2f), 0);
            GUI.Label(new Rect(bar.xMax + 8, row.y + 27, 70, 20), "<size=12>" + (ok ? "완료" : (q.target >= 100 ? pv + "점" : pv + "/" + q.target)) + "</size>", sSmall);
            y += 60;
        }
        // 포인트와 다음 보상
        GUI.Label(new Rect(r.x + 18, y + 4, w - 36, 24), $"<size=15><b>퀘스트 포인트 {questPoints}</b></size>", sLabel);
        string next = questPoints < 3 ? $"{3 - questPoints}포인트 더 모으면 반짝이는 이름표"
                    : questPoints < 7 ? $"{7 - questPoints}포인트 더 모으면 펫 '루돌프'가 따라다녀요"
                    : petsOwned.Count < PetIds.Length ? $"{3 - (questPoints - 7) % 3}포인트 더 모으면 새 펫 (지금 {MyPets.Count}마리)"
                    : "펫을 모두 모았어요!";
        if (AllPets) next = $"모든 펫이 열려 있어요 ({PetIds.Length}마리). 꾸미기에서 골라요";
        GUI.Label(new Rect(r.x + 18, y + 30, w - 36, 22), "<size=13>" + next + "</size>", sSmall);
    }

    // 꾸미기 창 아래: 펫 고르기
    void PetCustomControls()
    {
        if (!PetUnlocked) { if (questLoaded) { GUILayout.Space(6); GUILayout.Label($"<size=12>펫: 퀘스트 포인트 7이 되면 열려요 (지금 {questPoints})</size>", sSmall); } return; }
        GUILayout.Space(8);
        GUILayout.Label("펫", sSmall);
        var list = new List<string> { "" }; list.AddRange(MyPets);
        var names = list.Select(p => p == "" ? "없음" : PetName(p)).ToArray();
        int cur = Mathf.Max(0, list.IndexOf(CurPet));
        int sel = GUILayout.SelectionGrid(cur, names, 3, sBtn);
        if (sel != cur) CurPet = list[sel];
    }
}
