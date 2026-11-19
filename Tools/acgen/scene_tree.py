import json, numpy as np, random
from acload import load_acm
from render import render
import sheet_chars as S
random.seed(3)
cat = json.load(open("models/catalog_xmas_tree.json"))
orns = sorted(n for n in cat if n.startswith("orn_"))
leaves = [p for p in load_acm("models/tree_giant.acm") if p["name"] == "leaves"][0]
v, n = leaves["v"], leaves["n"]
# 앞쪽(카메라 쪽), 바깥을 향하고 아래로 처진 가장자리 위주
r = np.hypot(v[:, 0], v[:, 2]); ang = np.arctan2(v[:, 2], v[:, 0])
key = (np.floor(v[:, 1] / 0.3) * 1000 + np.floor(np.degrees(ang) / 6)).astype(int)
best = {}
for i in np.where(((n[:, 0] * v[:, 0] + n[:, 2] * v[:, 2]) / np.maximum(r, 1e-6) > 0.25) & (n[:, 1] < 0.35))[0]:
    k = key[i]
    if k not in best or r[i] > r[best[k]]: best[k] = i
ok = [i for i in best.values() if v[i, 2] > 0.3 and v[i, 1] > 1.0]
PAL = [(0.9,0.2,0.22),(0.98,0.78,0.32),(0.25,0.5,0.85),(0.6,0.35,0.8),(0.3,0.66,0.42),(0.95,0.5,0.65),(0.86,0.88,0.92),(0.98,0.6,0.25),(0.4,0.8,0.85)]
items = [dict(model="tree_planter", pos=[0,0,0], shadow=1.2), dict(model="tree_giant", pos=[0,0.05,0], shadow=0),
         dict(model="tree_star_topper", pos=[0,6.85,0], shadow=0), dict(model="tree_skirt", pos=[0,0,0], shadow=0, scale=1.3)]
placed = []
for idx in random.sample(ok, len(ok)):
    p = v[idx]
    if any(np.linalg.norm(p - q) < 0.75 for q in placed): continue
    placed.append(p)
    if len(placed) >= 60: break
for i, p in enumerate(placed):
    o = orns[i % len(orns)]
    items.append(dict(model=o, pos=[float(p[0]), float(p[1]) - 0.03, float(p[2])], rotY=random.uniform(-40, 40), scale=3.0, shadow=0,
                      tints={"orn": PAL[i % len(PAL)]} if "ball" in o or o in ("orn_drop","orn_heart","orn_star","orn_bell") else {}))
for k, (sp, x, z, i, hat) in enumerate([("bear", -2.6, 2.4, 0, "santa"), ("cat", 2.3, 2.6, 1, "beanie"), ("rabbit", 0.4, 3.1, 2, None), ("fox", -1.0, 3.4, 3, "antlers")]):
    items += S.char(sp, x, z, i, hat=hat, rot=180 + (20 if x < 0 else -20) if False else (-160 if x>0 else 160))
scene = dict(width=1400, height=1600, bg=[0.95,0.93,0.88], floorA=[0.82,0.62,0.45], floorB=[0.78,0.58,0.42], floorSize=30,
             camera=dict(pos=[0, 3.4, 12.5], target=[0, 3.3, 0], fov=40), items=items)
render(scene, "out/tree_decorated.png")
