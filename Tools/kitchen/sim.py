# sim.py - '산타의 크리스마스 주방' 난이도 시뮬레이션 (11-26)
# 스테이지 지도·메뉴·주문 규칙을 AcKitchen.cs 와 같게 두고, 봇(사람 실력 흉내)으로 여러 판을 돌려
# 스테이지마다 점수 분포를 보고 주문 간격·제한 시간·별 기준을 정함.
# 실행: python3 sim.py  → 표 출력 + stages.json (C#에 옮길 값)
import heapq, json, math, random, statistics
from collections import deque

TILE = 1.2
SPEED = 3.0          # m/s (캐릭터 걷기)
ICE_SLOW = 1.25      # 얼음 위는 미끄러워 조금 더 걸림
STROKES = 3          # 도마 문지르기 횟수

COOK = {'milk': 5.0, 'dough': 7.0, 'batter': 8.0, 'turkey': 10.0}
RECIPES = {
    0: ('핫초코', 20, 50.0),
    1: ('진저브레드 쿠키', 30, 65.0),
    2: ('딸기 케이크', 40, 80.0),
    3: ('칠면조 구이', 50, 85.0),
}

STAGES = [
    dict(name='첫 주방', menus=[0, 1], time=150, map=[
        '#MWuHH##',
        '#......#',
        'D......O',
        'X......O',
        '#......#',
        'p......X',
        '#......#',
        'R......V',
        '####V###']),
    dict(name='가운데 식탁', menus=[0, 1], time=150, map=[
        '#MWu#HH#',
        '#......#',
        'D..##..O',
        'X..##..O',
        '#......#',
        'p..##..X',
        '#..##..#',
        'R......#',
        '###VV###']),
    dict(name='딸기 케이크', menus=[0, 1, 2], time=180, map=[
        '#MWuHH##',
        '#......O',
        'D......O',
        'X..##..#',
        'B..##..X',
        'S......#',
        'p......#',
        'R......V',
        '##X##V##']),
    dict(name='미끄러운 얼음', menus=[0, 1, 2], time=180, map=[
        '#MWuHH##',
        'D.....iO',
        'X.iii.iO',
        'B.iii..#',
        'S......X',
        '#..ii..#',
        'p..ii..#',
        'R......V',
        '####V###']),
    dict(name='칸막이 주방', menus=[0, 1, 2], time=180, map=[
        '#MWu#HH#',
        'D...#..O',
        'X...#..O',
        'B......#',
        'S...#..X',
        '#...#..#',
        'p......#',
        'R...#..V',
        '###V####']),
    dict(name='칠면조 등장', menus=[0, 1, 2, 3], time=180, map=[
        '#MWuHHO#',
        'D......O',
        'X..##..O',
        'B..##..#',
        'S......X',
        'T..##..#',
        'P..##..X',
        'p......V',
        '#R#V####']),
    dict(name='컨베이어 벨트', menus=[0, 1, 2, 3], time=180, map=[
        'MWuDBSTP',
        '........',
        'X......X',
        '#......#',
        '#.>>>>>#',
        '#......O',
        'H......O',
        'H......O',
        '#Rp#VV##']),
    dict(name='산타 러시', menus=[0, 1, 2, 3], time=180, rush=True, map=[
        '#MWuHHO#',
        'D......O',
        'X.i.#..O',
        'B.i....#',
        'S...#.iX',
        'T...#.i#',
        'P......X',
        'p...#..V',
        '#R#V#V##']),
]

WALK = set('.i12345')


def parse(st):
    m = st['map']; H = len(m); W = len(m[0])
    assert all(len(r) == W for r in m), st['name']
    tiles = {}
    for z in range(H):
        for x in range(W):
            tiles[(x, z)] = m[z][x]
    return tiles, W, H


def bfs(tiles, W, H, start):
    dist = {start: 0.0}
    pq = [(0.0, start)]
    while pq:
        d, (x, z) = heapq.heappop(pq)
        if d > dist[(x, z)]: continue
        for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, z + dz)
            if n not in tiles or tiles[n] not in WALK: continue
            c = d + (ICE_SLOW if tiles[n] == 'i' else 1.0)
            if c < dist.get(n, 1e9):
                dist[n] = c; heapq.heappush(pq, (c, n))
    return dist


def stations(tiles, ch):
    return [p for p, c in tiles.items() if c == ch]


class World:
    def __init__(self, st):
        self.st = st
        self.tiles, self.W, self.H = parse(st)
        floors = [p for p, c in self.tiles.items() if c in WALK]
        self.dists = {p: bfs(self.tiles, self.W, self.H, p) for p in floors}
        # 각 기능칸 옆 바닥들
        self.adj = {}
        for p, c in self.tiles.items():
            if c in WALK: continue
            ns = [(p[0] + dx, p[1] + dz) for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            self.adj[p] = [n for n in ns if n in self.tiles and self.tiles[n] in WALK]
        # 컨베이어: 위에 놓으면 오른쪽 끝으로 (건너편 전달)
        self.conveyor = stations(self.tiles, '>')
        need = {'M', 'W', 'u', 'H'} if 0 in st['menus'] else set()
        for k, chs in {1: 'DXOp', 2: 'BSXOp', 3: 'TPXOp'}.items():
            if k in st['menus']: need |= set(chs)
        need |= {'V', 'R'}
        for ch in need:
            ss = stations(self.tiles, ch)
            assert ss, (st['name'], 'missing', ch)
            for s in ss:
                assert self.adj[s], (st['name'], 'unreachable', ch, s)
        # 바닥끼리 모두 이어져 있나
        f0 = floors[0]
        assert all(f in self.dists[f0] for f in floors), (st['name'], 'floor split')

    def travel(self, frm, station):
        """frm(바닥)에서 station 옆 바닥까지 (시간, 도착칸)"""
        best = None
        for a in self.adj[station]:
            d = self.dists[frm].get(a)
            if d is None: continue
            if best is None or d < best[0]: best = (d, a)
        return best[0] * TILE / SPEED, best[1]


class Bot:
    def __init__(self, w, pos, skill, rng):
        self.w = w; self.pos = pos; self.skill = skill; self.rng = rng
        self.t = 0.0

    def act_delay(self):
        # 버튼 누르기·방향 돌리기·망설임 (실력이 낮을수록 길고 들쭉날쭉)
        return 0.25 + (1 - self.skill) * self.rng.uniform(0.3, 1.6)

    def go(self, station):
        tt, a = self.w.travel(self.pos, station)
        self.t += tt / (0.75 + 0.25 * self.skill) + self.act_delay()
        self.pos = a

    def board(self):
        self.t += STROKES * (0.32 + (1 - self.skill) * 0.4)


def nearest(w, bot, ch, busy, now):
    best = None
    for s in stations(w.tiles, ch):
        tt, _ = w.travel(bot.pos, s)
        free = busy.get(s, 0.0)
        cost = max(tt, free - now) + tt * 0.2
        if best is None or cost < best[0]: best = (cost, s)
    return best[1]


def do_order(w, bot, r, busy):
    """봇 한 명이 주문 하나를 끝까지 (요리 기다리는 동안 다른 단계). 끝난 시각 반환"""
    def cook(item, oven_ch):
        s = nearest(w, bot, oven_ch, busy, bot.t)
        bot.go(s)
        start = max(bot.t, busy.get(s, 0.0))
        bot.t = start
        done = start + COOK[item]
        busy[s] = done + 0.5
        return s, done

    if r == 0:
        bot.go(nearest(w, bot, 'M', {}, bot.t))
        s, done = cook('milk', 'H')
        bot.go(nearest(w, bot, 'u', {}, bot.t))
        bot.go(nearest(w, bot, 'W', {}, bot.t))
        bot.go(s); bot.t = max(bot.t, done)
    elif r == 1:
        bot.go(nearest(w, bot, 'D', {}, bot.t))
        b = nearest(w, bot, 'X', busy, bot.t); bot.go(b); bot.t = max(bot.t, busy.get(b, 0)); bot.board(); busy[b] = bot.t
        s, done = cook('dough', 'O')
        bot.go(nearest(w, bot, 'p', {}, bot.t))           # 접시를 미리 놓아 두기
        bot.go(s); bot.t = max(bot.t, done)
        b = nearest(w, bot, 'X', busy, bot.t); bot.go(b); bot.t = max(bot.t, busy.get(b, 0)); bot.board(); busy[b] = bot.t
        bot.go(nearest(w, bot, 'p', {}, bot.t))
    elif r == 2:
        bot.go(nearest(w, bot, 'B', {}, bot.t))
        s, done = cook('batter', 'O')
        bot.go(nearest(w, bot, 'S', {}, bot.t))
        b = nearest(w, bot, 'X', busy, bot.t); bot.go(b); bot.t = max(bot.t, busy.get(b, 0)); bot.board(); busy[b] = bot.t
        bot.go(nearest(w, bot, 'p', {}, bot.t))
        bot.t += 0.6                                         # 접시를 옆 조리대에 내려놓기
        bot.go(s); bot.t = max(bot.t, done)
        b = nearest(w, bot, 'X', busy, bot.t); bot.go(b); bot.t = max(bot.t, busy.get(b, 0)); bot.board(); busy[b] = bot.t
        bot.t += 1.2                                         # 접시에 담고 다시 들기
    else:
        bot.go(nearest(w, bot, 'T', {}, bot.t))
        s, done = cook('turkey', 'O')
        bot.go(nearest(w, bot, 'P', {}, bot.t))
        b = nearest(w, bot, 'X', busy, bot.t); bot.go(b); bot.t = max(bot.t, busy.get(b, 0)); bot.board(); busy[b] = bot.t
        bot.go(nearest(w, bot, 'p', {}, bot.t))
        bot.go(s); bot.t = max(bot.t, done)
    bot.go(nearest(w, bot, 'V', {}, bot.t))
    # 실수 (가끔 태우거나 잘못 담아서 다시)
    if bot.rng.random() < 0.12 * (1 - bot.skill): bot.t += bot.rng.uniform(4, 12)
    # 칸막이·컨베이어 지도: 건너편 전달하느라 조금 더 (이미 길찾기에 반영, 손 바꿈 시간만)
    return bot.t


def play(st, params, nplayers, skill, seed):
    rng = random.Random(seed)
    w = WORLDS[st['name']]
    starts = [p for p, c in w.tiles.items() if c in WALK]
    rng.shuffle(starts)
    bots = [Bot(w, starts[i], skill + rng.uniform(-0.08, 0.08), rng) for i in range(nplayers)]
    T = st['time']
    k = PLAYER_RATE[nplayers]
    interval = params['interval'] / k
    life_k = params['life']
    maxa = params['max'] + (1 if nplayers >= 3 else 0)
    orders = []          # [spawn, recipe, life, taken, done]
    t_next = 1.0
    menus = st['menus']
    oid = 0
    order_rng = random.Random(seed * 7 + 1)
    state = dict(next=1.0)
    def spawn(now, visible):
        # AcKitchenCore.Step 과 같은 규칙
        if not [o for o in visible if not o[5]]: state['next'] = min(state['next'], now + 2.0)
        while state['next'] <= now and len([o for o in visible if not o[5]]) < maxa and state['next'] < T - 8:
            t0 = state['next']
            weights = [1.0 + (0.6 if m == menus[0] else 0) for m in menus]
            r = order_rng.choices(menus, weights)[0]
            if sum(1 for o in visible if not o[5] and o[1] == r) >= 2: r = order_rng.choice(menus)
            visible.append([t0, r, RECIPES[r][2] * life_k, False, None, False])
            gap = interval * order_rng.uniform(0.8, 1.2)
            if st.get('rush') and (60 <= t0 <= 75 or 130 <= t0 <= 145): gap *= 0.45
            if len([o for o in visible if not o[5]]) <= 1: gap = min(gap, 4.0)
            state['next'] = t0 + gap
        if state['next'] < now and len([o for o in visible if not o[5]]) >= maxa: state['next'] = now
    score = 0; served = 0; failed = 0
    busy = {}
    # 봇: 놀고 있으면 가장 오래된(남은 시간 적은) 주문을 맡음. 동시에 떠 있는 주문은 maxa 개까지 (넘치면 늦게 뜸)
    visible = []
    while True:
        now = min(b.t for b in bots)
        if now >= T: break
        spawn(now, visible)
        # 시간 지난 주문
        for o in visible:
            if not o[5] and not o[3] and now > o[0] + o[2]:
                o[5] = True; failed += 1; score -= 10
        bot = min(bots, key=lambda b: b.t)
        cand = [o for o in visible if not o[3] and not o[5]]
        if not cand:
            bot.t = max(bot.t + 0.3, min(state['next'], T))
            continue
        o = min(cand, key=lambda o: o[0] + o[2])
        o[3] = True
        end = do_order(w, bot, o[1], busy)
        if end > T: break
        if end <= o[0] + o[2]:
            base = RECIPES[o[1]][1]
            tip = round(base * 0.5 * (1 - (end - o[0]) / o[2]))
            score += base + max(0, tip); served += 1
        else:
            score -= 10; failed += 1
        o[5] = True
    for o in visible:
        if not o[5] and not o[3] and T > o[0] + o[2]: failed += 1; score -= 10
    return max(0, score), served, failed


PLAYER_RATE = {1: 1.0, 2: 1.55, 3: 1.95, 4: 2.25}
WORLDS = {}


def evaluate(st, params, nplayers, skill, n=60):
    res = [play(st, params, nplayers, skill, s) for s in range(n)]
    sc = [r[0] for r in res]
    return statistics.mean(sc), statistics.quantiles(sc, n=4) if len(sc) > 3 else (0, 0, 0), statistics.mean(r[1] for r in res), statistics.mean(r[2] for r in res)


if __name__ == '__main__':
    for st in STAGES:
        WORLDS[st['name']] = World(st)
    print('maps ok')


def table(params_by_stage, skills=(0.45, 0.65, 0.85), players=(1, 2, 3, 4), n=50):
    out = {}
    for st, pr in zip(STAGES, params_by_stage):
        row = {}
        for np_ in players:
            for sk in skills:
                m, q, sv, fl = evaluate(st, pr, np_, sk, n)
                row[(np_, sk)] = (m, q, sv, fl)
        out[st['name']] = row
    return out
