"""눈사람 부품 미리보기: 부품 진열 + 조립 예시 3개"""
import numpy as np, json, sys
from render import render_many
from sheet import sheet

def basis(d, up=(0, 1, 0)):
    z = np.array(d, float); z /= np.linalg.norm(z)
    u = np.array(up, float); u = u - z * (u @ z)
    if np.linalg.norm(u) < 1e-3: u = np.array([0, 0, 1.0]) - z * z[2]
    y = u / np.linalg.norm(u); x = np.cross(y, z)
    return np.stack([x, y, z], 1)

def ball_item(c, r, model="sm_snowball", tints=None, R=None, sx=1, sy=1):
    it = dict(model=model, pos=list(map(float, c)), R=(R if R is not None else np.eye(3)).tolist(), sc=[2 * r * sx, 2 * r * sy, 2 * r * sx], shadow=0)
    if tints: it["tints"] = tints
    return it

def attach(items, c, r, d, model, tints=None, s=1.0, sy=1.0):
    d = np.array(d, float); d /= np.linalg.norm(d)
    # 타원 눈덩이(세로 sy) 표면
    p = np.array(c) + d * r * np.array([1, sy, 1])
    it = dict(model=model, pos=p.tolist(), R=basis(d).tolist(), sc=[s, s, s], shadow=0)
    if tints: it["tints"] = tints
    items.append(it)

def snowman(x, tiers, top_model=None, hat=None, deco=(), scarf=None, head_char=None):
    items = []; y = 0; centers = []
    for i, r in enumerate(tiers):
        y += r * (0.85 if i else 0.92)
        c = (x, y, 0)
        if i == len(tiers) - 1 and head_char:
            # 캐릭터 머리 눈덩이: 몸 숨기고 하얗게
            sc = r / 0.33
            items.append(dict(model="char_" + head_char, pos=[x, y - 0.74 * sc, 0], rotY=0, scale=sc, shadow=0,
                              only=["head", "ears", "face", "mane", "nose"], tints={"fur": [0.98, 0.985, 1.0], "mane": [0.95, 0.96, 1.0]}))
        else:
            items.append(ball_item(c, r))
        centers.append((c, r)); y += r * 0.85
    for (ti, d, model, tints, s) in deco:
        c, r = centers[ti]; attach(items, c, r, d, model, tints, s)
    c, r = centers[-1]
    if hat:
        sc = r / 0.32
        if hat == "sm_bucket":
            items.append(dict(model=hat, pos=[x, c[1] + r * 0.82, 0], R=basis((0.1, 0, 1), up=(0.15, 1, 0)).tolist(), sc=[r / 0.17 * 0.95] * 3, shadow=0))
        else:
            items.append(dict(model=hat, pos=[x, c[1] - 1.04 * sc + r * 1.0, 0], rotY=0, scale=sc, shadow=0))
    if scarf:
        c0, r0 = centers[-2]
        ny = c0[1] + r0 * 0.9
        items.append(dict(model="sm_scarf", pos=[x, ny, 0], rotY=0, scale=min(r, r0) * 0.75 / 0.2, shadow=0, tints={"scarf": scarf}))
    return items

F = (0, 0, 1)
def face(ti, r_eye=0.35, nose="sm_carrot", mouth="sm_smile", eye="sm_coal"):
    return [(ti, (-r_eye, 0.3, 1), eye, None, 1.0), (ti, (r_eye, 0.3, 1), eye, None, 1.0),
            (ti, (0, 0, 1), nose, None, 1.0), (ti, (0, -0.42, 1), mouth, None, 1.0)]

A = snowman(-2.4, [0.42, 0.31, 0.24], hat="sm_bucket", scarf=[0.25, 0.5, 0.85],
            deco=face(2) + [(1, (0, 0.2, 1), "sm_coal", None, 1.2), (1, (0, -0.2, 1), "sm_coal", None, 1.2), (0, (0, 0.15, 1), "sm_coal", None, 1.2),
                            (1, (-1, 0.15, 0.1), "sm_arm", None, 1.2), (1, (1, 0.15, 0.1), "sm_arm", None, 1.2)])
B = snowman(0.0, [0.45, 0.3], hat="hat_tophat", scarf=None,
            deco=face(1, nose="sm_nose_round") + [(1, (0, -0.85, 0.6), "sm_bowtie", None, 1.2), (1, (-0.35, 0.05, 1), "sm_glasses", None, 0.0),
                                                   (1, (0, 0.33, 1), "sm_glasses", None, 1.0), (1, (0, -0.15, 1), "sm_mustache", None, 1.0),
                                                   (0, (0, 0.2, 1), "sm_button", {"button": [0.3, 0.6, 0.9]}, 1.3), (0, (0, -0.15, 1), "sm_button", {"button": [0.3, 0.6, 0.9]}, 1.3),
                                                   (0, (-1, 0.5, 0.3), "sm_broom", None, 1.0), (1, (-0.5, -0.1, 1), "sm_blush", None, 1.0), (1, (0.5, -0.1, 1), "sm_blush", None, 1.0)])
C = snowman(2.4, [0.42, 0.3, 0.27], hat="hat_santa", scarf=[0.3, 0.62, 0.4], head_char="bear",
            deco=[(1, (0, 0.1, 1), "sm_heart", None, 1.3), (0, (0.3, 0.3, 1), "sm_flower", None, 1.4), (0, (-0.35, 0.0, 1), "sm_holly", None, 1.4),
                  (1, (-1, 0.1, 0.2), "sm_arm", None, 1.1), (1, (1, 0.25, 0.2), "sm_mitten", None, 1.4), (0, (0.1, -0.25, 1), "sm_star", None, 1.4)])
scene = dict(width=1600, height=900, bg=[0.9, 0.94, 1.0], floorA=[0.97, 0.98, 1.0], floorB=[0.9, 0.93, 0.98], floorSize=40,
             camera=dict(pos=[0, 2.0, 7.6], target=[0, 0.95, 0], fov=34), items=A + B + C)
# 부품 진열
names = ["sm_carrot", "sm_coal", "sm_button", "sm_arm", "sm_bucket", "sm_pipe", "sm_mitten", "sm_broom", "sm_bowtie", "sm_glasses",
         "sm_mustache", "sm_smile", "sm_nose_round", "sm_twig_hair", "sm_flower", "sm_blush", "sm_scarf", "sm_star", "sm_heart", "sm_holly"]
items = []
cols = 5
for i, n in enumerate(names):
    r, c = divmod(i, cols)
    x = (c - 2) * 0.55; y = 1.35 - r * 0.4
    sc = 0.6 if n in ("sm_broom", "sm_bucket", "sm_scarf", "sm_arm") else 1.7
    items.append(dict(model=n, pos=[x, y, 0], R=basis((0.35, 0.25, 1)).tolist(), sc=[sc] * 3, shadow=0))
scene2 = dict(width=1400, height=1000, bg=[0.93, 0.95, 1.0], floor=False, camera=dict(pos=[0, 0.75, 3.4], target=[0, 0.75, 0], fov=34), items=items)
render_many([(scene, "out/snowman_examples.png"), (scene2, "out/snowman_parts.png")])
