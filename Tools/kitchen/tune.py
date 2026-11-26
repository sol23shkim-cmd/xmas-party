import sim, json, statistics
for st in sim.STAGES: sim.WORLDS[st['name']] = sim.World(st)
def fails(st, I, life, np_, sk, n=40):
    m,q,sv,fl = sim.evaluate(st, dict(interval=I, life=life, max=4), np_, sk, n); return m, sv, fl
caps=[]
for st in sim.STAGES:
    lo, hi = 6.0, 40.0
    for _ in range(14):
        mid=(lo+hi)/2
        _,_,fl = fails(st, mid, 1.0, 1, 0.85)
        if fl > 0.6: lo = mid
        else: hi = mid
    caps.append(hi); print(st['name'], 'cap interval (good solo)', round(hi,1))
json.dump(caps, open('caps.json','w'))
