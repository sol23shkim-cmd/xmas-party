// AcKitchenCore.cs - '산타의 크리스마스 주방' 규칙 (화면과 상관없는 부분, 11-26)
// 방장도 손님도 같은 규칙으로 돌림: 방장은 주문을 만들고 판정해서 상태를 서버에 올리고,
// 손님은 받은 상태 위에 아직 확인 안 된 자기 행동을 다시 얹어서 바로바로 보이게 함.
// 지도·주문 간격·별 기준은 Tools/kitchen/sim.py (봇 시뮬레이션)로 정함.
//
// 물건 표기: 재료 "milk.r"(이름.상태), 탄 것 "coal", 그릇 "mug" / "plate" / 담긴 것 "plate+cookie.i+..."
// 칸 기호: . 바닥  i 얼음 바닥  # 조리대  > 컨베이어(오른쪽으로)  X 도마  H 가스레인지(냄비)  O 오븐  R 쓰레기통  V 내는 곳
//          M 우유  W 마시멜로  D 쿠키 반죽  B 케이크 반죽  S 딸기  T 칠면조  P 감자  u 머그컵  p 접시
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public class CkTile { public int i; public string it = ""; public float p; }
[Serializable] public class CkHand { public string id = ""; public string it = ""; }
[Serializable] public class CkOrder { public int id, r; public float t0, life; }
[Serializable] public class CkAck { public string id = ""; public int seq; }
[Serializable] public class CkState
{
    public string sid = ""; public int seq, stage, seed, nPlayers = 1;
    public float t; public int score, served, failed, nextOrder; public float nextSpawn = 1f; public bool over;
    public List<CkTile> tiles = new List<CkTile>();
    public List<CkHand> hands = new List<CkHand>();
    public List<CkOrder> orders = new List<CkOrder>();
    public List<CkAck> acks = new List<CkAck>();
    public string host = "";                                // 방장이 도망가면 다음 방장 id (그 사람이 이어받음)
    public List<string> gone = new List<string>();         // 도망간 사람들
    public string lastEv = "";   // 방장이 마지막으로 판정한 일 (손님 화면 소리용) "번호:종류:내용"
    public int evN;
}

public static class Kc
{
    public const int W = 8, H = 9;
    public const float Tile = 1.2f;
    public const int Strokes = 2;   // 도마: 쓱 두 번 밀면 끝
    public const float ConveyorStep = 1.1f;

    public class Stage
    {
        public string name, desc; public int[] menus; public float time, interval, life; public int maxOrders; public bool rush;
        public string[] map; public int[,] thr;   // [인원-1, 별-1]
    }

    public static readonly Stage[] Stages = {
        new Stage { name = "첫 주방", desc = "핫초코와 진저브레드 쿠키부터 천천히", menus = new[] { 0, 1 }, time = 150, interval = 27.0f, life = 1.2f, maxOrders = 3,
            map = new[] { "#MWuHH##", "#......#", "D......O", "X......O", "#......#", "p......X", "#......#", "R......V", "####V###" },
            thr = new[,] { { 140, 190, 230 }, { 310, 390, 460 }, { 450, 570, 690 }, { 600, 760, 900 } } },
        new Stage { name = "가운데 식탁", desc = "식탁을 돌아서 다녀요", menus = new[] { 0, 1 }, time = 150, interval = 34.7f, life = 1.1f, maxOrders = 3,
            map = new[] { "#MWu#HH#", "#......#", "D..##..O", "X..##..O", "#......#", "p..##..X", "#..##..#", "R......#", "###VV###" },
            thr = new[,] { { 110, 160, 210 }, { 280, 360, 430 }, { 420, 530, 650 }, { 550, 700, 840 } } },
        new Stage { name = "딸기 케이크", desc = "새 메뉴: 딸기 케이크!", menus = new[] { 0, 1, 2 }, time = 180, interval = 52.4f, life = 1.1f, maxOrders = 4,
            map = new[] { "#MWuHH##", "#......O", "D......O", "X..##..#", "B..##..X", "S......#", "p......#", "R......V", "##X##V##" },
            thr = new[,] { { 140, 200, 260 }, { 340, 440, 530 }, { 520, 660, 790 }, { 690, 880, 1050 } } },
        new Stage { name = "미끄러운 얼음", desc = "얼음 바닥은 쭉 미끄러져요", menus = new[] { 0, 1, 2 }, time = 180, interval = 50.4f, life = 1.05f, maxOrders = 4,
            map = new[] { "#MWuHH##", "D.....iO", "X.iii.iO", "B.iii..#", "S......X", "#..ii..#", "p..ii..#", "R......V", "####V###" },
            thr = new[,] { { 130, 190, 260 }, { 340, 450, 530 }, { 520, 660, 790 }, { 680, 860, 1040 } } },
        new Stage { name = "칸막이 주방", desc = "가운데 칸막이: 조리대 위로 건네주면 빨라요", menus = new[] { 0, 1, 2 }, time = 180, interval = 48.6f, life = 1.0f, maxOrders = 4,
            map = new[] { "#MWu#HH#", "D...#..O", "X...#..O", "B......#", "S...#..X", "#...#..#", "p......#", "R...#..V", "###V####" },
            thr = new[,] { { 60, 120, 210 }, { 280, 380, 480 }, { 440, 570, 710 }, { 580, 760, 930 } } },
        new Stage { name = "칠면조 등장", desc = "새 메뉴: 칠면조 구이! 이제 4가지", menus = new[] { 0, 1, 2, 3 }, time = 180, interval = 49.5f, life = 1.0f, maxOrders = 4,
            map = new[] { "#MWuHHO#", "D......O", "X..##..O", "B..##..#", "S......X", "T..##..#", "P..##..X", "p......V", "#R#V####" },
            thr = new[,] { { 120, 180, 260 }, { 360, 470, 570 }, { 550, 710, 850 }, { 730, 940, 1130 } } },
        new Stage { name = "컨베이어 벨트", desc = "벨트에 올리면 오른쪽으로 날라 줘요", menus = new[] { 0, 1, 2, 3 }, time = 180, interval = 45.9f, life = 0.95f, maxOrders = 4,
            map = new[] { "MWuDBSTP", "........", "X......X", "#......#", "#.>>>>>#", "#......O", "H......O", "H......O", "#Rp#VV##" },
            thr = new[,] { { 50, 90, 140 }, { 220, 320, 440 }, { 390, 530, 670 }, { 550, 720, 870 } } },
        new Stage { name = "산타 러시", desc = "칸막이 + 얼음 + 주문이 몰려와요!", menus = new[] { 0, 1, 2, 3 }, time = 180, interval = 42.8f, life = 0.9f, maxOrders = 5, rush = true,
            map = new[] { "#MWuHHO#", "D......O", "X.i.#..O", "B.i....#", "S...#.iX", "T...#.i#", "P......X", "p...#..V", "#R#V#V##" },
            thr = new[,] { { 90, 160, 240 }, { 340, 470, 580 }, { 550, 710, 870 }, { 730, 950, 1140 } } },
    };

    public static readonly string[] RecipeKo = { "핫초코", "진저브레드 쿠키", "딸기 케이크", "칠면조 구이" };
    public static readonly int[] RecipePts = { 20, 30, 40, 50 };
    public static readonly float[] RecipeLife = { 50, 65, 80, 85 };
    public static readonly string[] RecipeCont = { "mug", "plate", "plate", "plate" };
    public static readonly string[][] RecipeParts = { new[] { "milk.h", "marsh.r" }, new[] { "cookie.i" }, new[] { "sponge.c", "straw.s" }, new[] { "turkey.b", "potato.s" } };
    public static readonly float[] PlayerRate = { 1f, 1.55f, 1.95f, 2.25f };

    public static readonly Dictionary<string, string> ItemKo = new Dictionary<string, string> {
        { "milk.r", "우유" }, { "milk.h", "따뜻한 우유" }, { "marsh.r", "마시멜로" }, { "dough.r", "쿠키 반죽" }, { "dough.c", "쿠키 모양 반죽" },
        { "cookie.b", "구운 쿠키" }, { "cookie.i", "아이싱 쿠키" }, { "batter.r", "케이크 반죽" }, { "sponge.b", "케이크 시트" }, { "sponge.c", "크림 바른 시트" },
        { "straw.r", "딸기" }, { "straw.s", "썬 딸기" }, { "turkey.r", "칠면조" }, { "turkey.b", "구운 칠면조" }, { "potato.r", "감자" }, { "potato.s", "썬 감자" },
        { "coal", "탄 음식" }, { "mug", "머그컵" }, { "plate", "접시" } };

    public static string Dispense(char c)
    {
        switch (c)
        {
            case 'M': return "milk.r"; case 'W': return "marsh.r"; case 'D': return "dough.r"; case 'B': return "batter.r";
            case 'S': return "straw.r"; case 'T': return "turkey.r"; case 'P': return "potato.r"; case 'u': return "mug"; case 'p': return "plate";
        }
        return null;
    }
    public static bool IsDispenser(char c) => Dispense(c) != null;
    public static bool Walkable(char c) => c == '.' || c == 'i';
    public static bool IsContainer(string it) => it != null && (it == "mug" || it == "plate" || it.StartsWith("mug+", StringComparison.Ordinal) || it.StartsWith("plate+", StringComparison.Ordinal));
    public static string ContType(string it) { int k = it.IndexOf('+'); return k < 0 ? it : it.Substring(0, k); }
    public static List<string> Parts(string it) { var p = it.Split('+').ToList(); p.RemoveAt(0); return p; }

    // 도마에서 바뀌는 것
    public static string Chopped(string it)
    {
        switch (it)
        {
            case "dough.r": return "dough.c"; case "straw.r": return "straw.s"; case "potato.r": return "potato.s";
            case "cookie.b": return "cookie.i"; case "sponge.b": return "sponge.c";
        }
        return null;
    }
    public static bool IsSpread(string it) => it == "cookie.b" || it == "sponge.b";   // 바르기 (아이싱·크림)

    // 불에서 바뀌는 것: (익는 데 걸리는 시간, 결과)
    public static bool Cooks(char st, string it, out float time, out string result)
    {
        time = 0; result = null;
        if (st == 'H' && it == "milk.r") { time = 5f; result = "milk.h"; return true; }
        if (st == 'O')
        {
            if (it == "dough.c") { time = 7f; result = "cookie.b"; return true; }
            if (it == "batter.r") { time = 8f; result = "sponge.b"; return true; }
            if (it == "turkey.r") { time = 10f; result = "turkey.b"; return true; }
        }
        return false;
    }
    public static float BurnAfter(char st) => st == 'H' ? 7f : 8f;
    public static bool CanBurn(char st, string it) => (st == 'H' && it == "milk.h") || (st == 'O' && (it == "cookie.b" || it == "sponge.b" || it == "turkey.b"));
    public static bool StationAccepts(char st, string it) => st == 'H' ? it == "milk.r" : st == 'O' ? (it == "dough.c" || it == "batter.r" || it == "turkey.r") : true;

    // 그릇에 담을 수 있나 (어떤 요리든 그 그릇·재료 조합이 되면)
    public static string Add(string cont, string part)
    {
        if (!IsContainer(cont) || part == null || IsContainer(part) || part == "coal") return null;
        string ct = ContType(cont); var have = Parts(cont);
        if (have.Contains(part)) return null;
        for (int r = 0; r < 4; r++)
        {
            if (RecipeCont[r] != ct) continue;
            var need = RecipeParts[r];
            if (!need.Contains(part)) continue;
            if (have.All(h => need.Contains(h))) { have.Add(part); have.Sort(string.CompareOrdinal); return ct + "+" + string.Join("+", have); }
        }
        return null;
    }
    public static int RecipeOf(string it)
    {
        if (!IsContainer(it)) return -1;
        string ct = ContType(it); var have = Parts(it);
        for (int r = 0; r < 4; r++) if (RecipeCont[r] == ct && have.Count == RecipeParts[r].Length && RecipeParts[r].All(have.Contains)) return r;
        return -1;
    }

    // ===== 상태 다루기 =====
    public static char At(Stage s, int i) => s.map[i / W][i % W];
    public static int Idx(int x, int z) => z * W + x;
    public static CkTile TileAt(CkState st, int i, bool make)
    {
        foreach (var t in st.tiles) if (t.i == i) return t;
        if (!make) return null;
        var n = new CkTile { i = i }; st.tiles.Add(n); return n;
    }
    public static string ItemAt(CkState st, int i) { var t = TileAt(st, i, false); return t != null ? t.it : ""; }
    public static void SetItem(CkState st, int i, string it, float p = 0)
    {
        var t = TileAt(st, i, true); t.it = it ?? ""; t.p = p;
        if (t.it == "") st.tiles.Remove(t);
    }
    public static CkHand Hand(CkState st, string id)
    {
        foreach (var h in st.hands) if (h.id == id) return h;
        var n = new CkHand { id = id }; st.hands.Add(n); return n;
    }

    // 잡기/놓기 (한 버튼). 돌려주는 값: 일어난 일 (소리·효과용) — "" 이면 아무 일 없음
    public static string Interact(CkState st, Stage s, string pid, int i)
    {
        if (st.over || i < 0 || i >= W * H) return "";
        char c = At(s, i);
        var hand = Hand(st, pid); string h = hand.it ?? ""; string on = ItemAt(st, i);
        if (Walkable(c))
        {   // 바닥에 떨어진 것 (던진 것): 줍기·그릇에 담기
            if (h == "" && on != "") { hand.it = on; SetItem(st, i, ""); return "pick"; }
            if (IsContainer(h) && on != "") { var a = Add(h, on); if (a != null) { hand.it = a; SetItem(st, i, ""); return "add"; } }
            return "";
        }
        if (IsDispenser(c))
        {
            string d = Dispense(c);
            if (h == "") { hand.it = d; return "pick"; }
            if (h == d) { hand.it = ""; return "putback"; }   // 같은 재료·빈 그릇은 상자에 도로 넣기
            if (IsContainer(h) && !IsContainer(d)) { var a = Add(h, d); if (a != null) { hand.it = a; return "add"; } return "bad"; }
            if (!IsContainer(h) && IsContainer(d)) { var a = Add(d, h); if (a != null) { hand.it = a; return "add"; } return "bad"; }
            return "bad";
        }
        switch (c)
        {
            case 'R':
                if (h == "") return "";
                hand.it = IsContainer(h) ? ContType(h) : "";
                return "trash";
            case 'V':
                {
                    if (h == "") return "";
                    int r = RecipeOf(h);
                    if (r < 0) return "bad";
                    CkOrder best = null;
                    foreach (var o in st.orders) if (o.r == r && (best == null || o.t0 + o.life < best.t0 + best.life)) best = o;
                    if (best == null) return "bad";
                    float left = Mathf.Clamp01(1f - (st.t - best.t0) / best.life);
                    int pts = RecipePts[r] + Mathf.RoundToInt(RecipePts[r] * 0.5f * left);
                    st.score += pts; st.served++; st.orders.Remove(best); hand.it = "";
                    return "serve:" + r + ":" + pts;
                }
            case 'H':
            case 'O':
                {
                    if (h == "" && on != "") { hand.it = on; SetItem(st, i, ""); return "pick"; }
                    if (h != "" && on == "") { if (!IsContainer(h) && StationAccepts(c, h)) { hand.it = ""; SetItem(st, i, h); return "cook"; } return "bad"; }
                    if (IsContainer(h) && on != "") { var a = Add(h, on); if (a != null) { hand.it = a; SetItem(st, i, ""); return "add"; } return "bad"; }
                    return "bad";
                }
            default:   // 조리대·도마·컨베이어
                {
                    if (h == "" && on != "") { hand.it = on; SetItem(st, i, ""); return "pick"; }
                    if (h != "" && on == "") { hand.it = ""; SetItem(st, i, h); return "place"; }
                    if (h != "" && on != "")
                    {
                        if (IsContainer(h)) { var a = Add(h, on); if (a != null) { hand.it = a; SetItem(st, i, ""); return "add"; } }
                        if (IsContainer(on)) { var a = Add(on, h); if (a != null) { hand.it = ""; SetItem(st, i, a); return "add"; } }
                    }
                    return "bad";
                }
        }
    }

    // 던지기: i = 떨어질 칸 (던진 사람 화면에서 앞으로 두 칸, 가는 길에 조리대가 있으면 그 칸)
    public static string Throw(CkState st, Stage s, string pid, int i)
    {
        if (st.over || i < 0 || i >= W * H) return "";
        var hand = Hand(st, pid); if (string.IsNullOrEmpty(hand.it)) return "";
        if (Walkable(At(s, i)))
        {
            if (ItemAt(st, i) != "") return "bad";
            SetItem(st, i, hand.it); hand.it = ""; return "throw";
        }
        var e = Interact(st, s, pid, i);
        return e == "place" ? "throw" : e;
    }
    // 행동 하나 (방장·손님 공통): c 썰기, t 던지기, 그 밖은 잡기/놓기
    public static string Act(CkState st, Stage s, string pid, char a, int i) => a == 'c' ? Chop(st, s, i) : a == 't' ? Throw(st, s, pid, i) : Interact(st, s, pid, i);

    // 도마 문지르기 한 번
    public static string Chop(CkState st, Stage s, int i)
    {
        if (st.over || i < 0 || i >= W * H || At(s, i) != 'X') return "";
        var t = TileAt(st, i, false); if (t == null) return "";
        var res = Chopped(t.it); if (res == null) return "";
        t.p += 1f;
        if (t.p >= Strokes - 0.01f) { t.it = res; t.p = 0; return "chopdone"; }
        return IsSpread(t.it) ? "spread" : "chop";
    }

    // 시간 흐름: 굽기·태우기·컨베이어 (모두), 주문 만들기·시간 초과·끝 (방장만)
    public static void Step(CkState st, Stage s, float dt, bool host, System.Random rng, List<string> evs)
    {
        if (st.over) return;
        st.t += dt;
        foreach (var t in st.tiles.ToList())
        {
            char c = At(s, t.i);
            if (c == 'H' || c == 'O')
            {
                if (Cooks(c, t.it, out float ct, out string res)) { t.p += dt; if (t.p >= ct) { t.it = res; t.p = 0; evs?.Add("done"); } }
                else if (CanBurn(c, t.it)) { t.p += dt; if (t.p >= BurnAfter(c)) { t.it = "coal"; t.p = 0; evs?.Add("burn"); } }
            }
            else if (c == '>')
            {
                t.p += dt;
                if (t.p >= ConveyorStep)
                {
                    int x = t.i % W, z = t.i / W;
                    if (x + 1 < W)
                    {
                        int ni = Idx(x + 1, z); char nc = At(s, ni);
                        if ((nc == '>' || nc == '#') && ItemAt(st, ni) == "") { var it = t.it; st.tiles.Remove(t); SetItem(st, ni, it); continue; }
                    }
                    t.p = ConveyorStep;   // 끝에서 기다림
                }
            }
        }
        if (!host) return;
        // 시간 지난 주문
        for (int k = st.orders.Count - 1; k >= 0; k--)
        {
            var o = st.orders[k];
            if (st.t > o.t0 + o.life) { st.orders.RemoveAt(k); st.failed++; st.score = Mathf.Max(0, st.score - 10); evs?.Add("fail:" + o.r); }
        }
        // 새 주문
        int n = Mathf.Clamp(st.nPlayers, 1, 4);
        int maxA = s.maxOrders + (n >= 3 ? 1 : 0);
        if (st.orders.Count == 0) st.nextSpawn = Mathf.Min(st.nextSpawn, st.t + 2f);   // 주문이 하나도 없으면 곧 다음 주문
        if (st.t >= st.nextSpawn && st.orders.Count < maxA && st.t < s.time - 8f)
        {
            var w = s.menus.Select(m => 1.0 + (m == s.menus[0] ? 0.6 : 0)).ToArray();
            double sum = w.Sum(), pick = rng.NextDouble() * sum; int r = s.menus[0];
            for (int k = 0; k < w.Length; k++) { pick -= w[k]; if (pick <= 0) { r = s.menus[k]; break; } }
            // 같은 메뉴 3개가 한꺼번에 몰리지 않게
            if (st.orders.Count(o => o.r == r) >= 2) r = s.menus[rng.Next(s.menus.Length)];
            st.orders.Add(new CkOrder { id = ++st.nextOrder, r = r, t0 = st.t, life = RecipeLife[r] * s.life });
            float gap = s.interval / PlayerRate[n - 1] * (0.8f + 0.4f * (float)rng.NextDouble());
            if (s.rush && ((st.t >= 60 && st.t <= 75) || (st.t >= 130 && st.t <= 145))) gap *= 0.45f;
            if (st.orders.Count <= 1) gap = Mathf.Min(gap, 4f);   // 너무 한가하면 금방 다음 주문
            st.nextSpawn = st.t + gap;
            evs?.Add("order");
        }
        if (st.t >= s.time) { st.over = true; evs?.Add("over"); }
    }

    public static int Stars(Stage s, int nPlayers, int score)
    {
        int n = Mathf.Clamp(nPlayers, 1, 4) - 1, k = 0;
        for (int j = 0; j < 3; j++) if (score >= s.thr[n, j]) k = j + 1;
        return k;
    }
}
