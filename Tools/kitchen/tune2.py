import sim, json, statistics
for st in sim.STAGES: sim.WORLDS[st['name']] = sim.World(st)
K = [1.35, 1.25, 1.2, 1.12, 1.08, 1.1, 1.02, 0.95]       # 주문 간격 = 보통 실력 혼자 한계 × K
LIFE = [1.20, 1.10, 1.10, 1.05, 1.0, 1.0, 0.95, 0.9]
MAXA = [3, 3, 4, 4, 4, 4, 4, 5]
out = []
for i, st in enumerate(sim.STAGES):
    lo, hi = 6.0, 45.0
    for _ in range(13):
        mid = (lo + hi) / 2
        m, q, sv, fl = sim.evaluate(st, dict(interval=mid, life=LIFE[i], max=MAXA[i]), 1, 0.45, 40)
        if fl > 0.6: lo = mid
        else: hi = mid
    interval = round(hi * K[i], 1)
    pr = dict(interval=interval, life=LIFE[i], max=MAXA[i])
    thr = {}
    for np_ in (1, 2, 3, 4):
        mc = sim.evaluate(st, pr, np_, 0.30, 60)[0]
        mn = sim.evaluate(st, pr, np_, 0.50, 60)[0]
        mg = sim.evaluate(st, pr, np_, 0.75, 60)[0]
        thr[np_] = (round(mc * 0.85 / 10) * 10, round(mn * 0.95 / 10) * 10, round(mg * 0.98 / 10) * 10, round(mc), round(mn), round(mg))
    print(f"{i+1} {st['name']:8s} interval={interval:5.1f} life={LIFE[i]} max={MAXA[i]}  " + '  '.join(f"P{k}:{v[0]}/{v[1]}/{v[2]}" for k, v in thr.items()))
    out.append(dict(name=st['name'], interval=interval, life=LIFE[i], max=MAXA[i], thr={k: v[:3] for k, v in thr.items()}))
json.dump(out, open('stages.json', 'w'), ensure_ascii=False, indent=1)
