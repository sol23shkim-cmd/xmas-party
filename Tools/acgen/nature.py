"""식물·바깥 풍경 (나무 5종 이상, 덤불, 바위, 눈더미, 울타리, 가로등 ...). 원점 = 바닥 중심"""
from assets import *
from xmas_tree import pine_tiers

SNOW = (0.98, 0.98, 1.0)
BARK = (0.5, 0.34, 0.24)
LEAF = (0.4, 0.66, 0.38)
PINE2 = (0.27, 0.52, 0.36)


def N(name, lo, hi, step, desc, cat="nature", **kw):
    return asset(name, cat, lo, hi, step, desc, **kw)


def trunk(m, h=0.7, r=0.12, color=BARK):
    t = m.part("trunk", tint=color, group="bark", k=0.04)
    t.add(lambda p: sd_roundcone(p, (0, 0.02, 0), (0, h, 0), r * 1.3, r * 0.8), WHITE, 1.0)
    for a in (0, 120, 240):
        aa = np.radians(a)
        t.add(lambda p, aa=aa: sd_capsule(p, (0, 0.06, 0), (np.cos(aa) * r * 1.6, 0.02, np.sin(aa) * r * 1.6), r * 0.35), WHITE, 1.0)
    return t


def snow_caps(part, tiers, color=SNOW):
    for y0, h, r in tiers:
        part.add(lambda p, y0=y0, h=h, r=r: smin(sd_roundcone(p, (0, y0 + h * 0.55, 0), (0, y0 + h + 0.02, 0), r * 0.42, 0.05),
                                               10 * 0 + sd_sphere(p, (0, y0 + h + 0.03, 0), 0.08), 0.05), color, 0.0, k=0.0)


@N("pine_tall", (-1.3, -0.05, -1.3), (1.3, 4.2, 1.3), 0.025, "키 큰 전나무")
def pine_tall(m):
    trunk(m, 0.6, 0.13)
    lv = m.part("leaves", tint=PINE2, group="leaves", k=0.08)
    pine_tiers(lv, [(0.5, 1.2, 1.2), (1.35, 1.1, 0.95), (2.1, 1.0, 0.7), (2.8, 1.25, 0.42)], base_lobes=12)


@N("pine_snowy", (-1.3, -0.05, -1.3), (1.3, 4.3, 1.3), 0.03, "눈 덮인 전나무")
def pine_snowy(m):
    trunk(m, 0.6, 0.13)
    lv = m.part("leaves", tint=PINE2, group="leaves", k=0.08)
    T = [(0.5, 1.2, 1.2), (1.35, 1.1, 0.95), (2.1, 1.0, 0.7), (2.8, 1.25, 0.42)]
    pine_tiers(lv, T, base_lobes=12)
    sn0 = m.part("snowcaps", k=0.06)
    for i, (y0, h, r) in enumerate(T):   # 가지 윗면에 쌓인 눈 (가지 위에 얹힌 눈 덩어리)
        n = max(8, int(12 * (r / T[0][2]) ** 0.7) + 2); rr = min(0.6, r * 1.1 * np.pi / n)
        for j in range(n):
            ang = 2 * np.pi * (j + 0.5 * (i % 2)) / n
            d = np.array([np.cos(ang), 0, np.sin(ang)])
            inner = d * r * 0.45 + [0, y0 + h * 0.45 + rr * 0.62, 0]
            outer = d * r * 0.88 + [0, y0 + 0.02 + rr * 0.45, 0]
            sn0.add(lambda p, inner=inner, outer=outer, rr=rr: sd_roundcone(p, inner, outer, rr * 0.7, rr * 0.38), SNOW, 0.0)
    sn = m.part("snowtop", k=0.03)
    sn.add(lambda p: sd_sphere(p, (0, 4.0, 0), 0.12), SNOW, 0.0)


@N("pine_small", (-0.7, -0.05, -0.7), (0.7, 2.1, 0.7), 0.016, "작은 전나무")
def pine_small(m):
    trunk(m, 0.35, 0.08)
    lv = m.part("leaves", tint=(0.32, 0.58, 0.38), group="leaves", k=0.06)
    pine_tiers(lv, [(0.3, 0.7, 0.65), (0.8, 0.65, 0.48), (1.25, 0.75, 0.28)], base_lobes=10)


@N("round_tree", (-1.3, -0.05, -1.3), (1.3, 3.4, 1.3), 0.025, "동그란 활엽수 (구름 모양 잎)")
def round_tree(m):
    t = trunk(m, 1.6, 0.14)
    for s in (-1, 1):
        t.add(lambda p, s=s: sd_capsule(p, (0, 1.3, 0), (s * 0.45, 1.85, 0.1), 0.07), WHITE, 1.0)
    lv = m.part("leaves", tint=LEAF, group="leaves", k=0.18)
    rng = np.random.default_rng(1)
    lv.add(lambda p: sd_sphere(p, (0, 2.35, 0), 0.85), WHITE, 1.0)
    for i in range(9):
        a = rng.uniform(0, 2 * np.pi); e = rng.uniform(-0.3, 0.9)
        c = np.array([np.cos(a) * np.cos(e), np.sin(e), np.sin(a) * np.cos(e)]) * 0.75 + [0, 2.35, 0]
        lv.add(lambda p, c=c: sd_sphere(p, c, rng.uniform(0.35, 0.5) if False else 0.45), WHITE, 1.0)
    lv.paint(lambda p: -(p[:, 1] - 2.7), (1.15, 1.15, 1.0), 1.0, soft=-0.6)


@N("fruit_tree", (-1.2, -0.05, -1.2), (1.2, 3.0, 1.2), 0.022, "열매 나무 (사과)")
def fruit_tree(m):
    trunk(m, 1.3, 0.12)
    lv = m.part("leaves", tint=(0.38, 0.62, 0.36), group="leaves", k=0.2)
    lv.add(lambda p: sd_ellipsoid(p, (0, 2.05, 0), (0.95, 0.75, 0.95)), WHITE, 1.0)
    for a in range(0, 360, 60):
        aa = np.radians(a)
        lv.add(lambda p, aa=aa: sd_sphere(p, (np.cos(aa) * 0.7, 1.85, np.sin(aa) * 0.7), 0.42), WHITE, 1.0)
    fr = m.part("fruit", tint=(0.9, 0.22, 0.22), group="fruit", gloss=0.8, k=0.0)
    for i in range(9):
        a = np.radians(i * 40 + 15); y = 1.6 + (i % 3) * 0.3
        rr = 0.9 * np.sqrt(max(1 - ((y - 2.0) / 0.85) ** 2, 0.2)) + 0.03
        fr.add(lambda p, a=a, y=y, rr=rr: sd_sphere(p, (np.cos(a) * rr, y, np.sin(a) * rr), 0.09), WHITE, 1.0)


@N("bare_tree", (-1.2, -0.05, -1.2), (1.2, 3.2, 1.2), 0.016, "겨울 앙상한 나무 (가지에 눈)")
def bare_tree(m):
    t = m.part("trunk", tint=(0.48, 0.34, 0.27), group="bark", k=0.06)
    sn = m.part("snow", k=0.03)
    t.add(lambda p: sd_roundcone(p, (0, 0, 0), (0, 1.5, 0), 0.17, 0.1), WHITE, 1.0)
    rng = np.random.default_rng(7)
    def branch(a, b, r, depth):
        t.add(lambda p, a=a, b=b, r=r: sd_roundcone(p, a, b, r, r * 0.65), WHITE, 1.0)
        if r > 0.04:
            sn.add(lambda p, a=a, b=b, r=r: sd_capsule(p, a + [0, r * 0.75, 0], b + [0, r * 0.6, 0], r * 0.55), SNOW, 0.0)
        if depth == 0: return
        d = b - a
        for k in range(2):
            ang = rng.uniform(25, 45) * (1 if k == 0 else -1)
            yaw = rng.uniform(0, 360)
            R = rot(y=yaw) @ rot(z=ang)
            nd = (R @ (d / np.linalg.norm(d))) * np.linalg.norm(d) * 0.72
            nd[1] = abs(nd[1]) * 0.9 + 0.05
            branch(b, b + nd, r * 0.65, depth - 1)
    for yaw in (0, 130, 250):
        dd = rot(y=yaw) @ np.array([0.55, 0.65, 0])
        branch(np.array([0, 1.3, 0.0]), np.array([0, 1.3, 0]) + dd, 0.09, 2)
    branch(np.array([0, 1.4, 0.0]), np.array([0.05, 2.1, 0]), 0.1, 2)


@N("birch", (-0.9, -0.05, -0.9), (0.9, 3.4, 0.9), 0.018, "자작나무 (흰 줄기, 노란 잎)")
def birch(m):
    t = m.part("trunk", tint=(0.96, 0.95, 0.92), group="bark", k=0.04)
    t.add(lambda p: sd_roundcone(p, (0, 0, 0), (0.05, 2.3, 0), 0.12, 0.07), WHITE, 1.0)
    t.add(lambda p: sd_capsule(p, (0.02, 1.5, 0), (-0.35, 2.1, 0.1), 0.05), WHITE, 1.0)
    t.paint(lambda p: np.abs(np.sin(p[:, 1] * 9 + np.sin(np.arctan2(p[:, 2], p[:, 0]) * 2) * 2)) - 0.12, (0.25, 0.22, 0.2), 0.0, soft=0.05)
    lv = m.part("leaves", tint=(0.96, 0.8, 0.35), group="leaves", k=0.14)
    for c, r in (((0.05, 2.65, 0), 0.55), ((-0.35, 2.3, 0.1), 0.4), ((0.35, 2.4, -0.1), 0.42), ((0, 2.3, 0.35), 0.38), ((0.0, 3.0, 0), 0.38)):
        lv.add(lambda p, c=c, r=r: sd_sphere(p, c, r), WHITE, 1.0)


@N("palm_cedar", (-1.0, -0.05, -1.0), (1.0, 3.8, 1.0), 0.02, "삼나무 (둥근 원뿔형 사이프러스)")
def cypress(m):
    trunk(m, 0.4, 0.1)
    lv = m.part("leaves", tint=(0.3, 0.55, 0.42), group="leaves", k=0.12)
    lv.add(lambda p: sd_ellipsoid(p, (0, 1.95, 0), (0.6, 1.65, 0.6)), WHITE, 1.0)
    for i in range(14):
        a = i * 2.4; y = 0.7 + i * 0.18
        r = 0.6 * np.sqrt(max(1 - ((y - 1.95) / 1.65) ** 2, 0.05))
        lv.add(lambda p, a=a, y=y, r=r: sd_sphere(p, (np.cos(a) * r * 0.8, y, np.sin(a) * r * 0.8), 0.26), WHITE, 1.0)


@N("bush_round", (-0.7, -0.05, -0.7), (0.7, 0.9, 0.7), 0.014, "동그란 덤불")
def bush(m):
    b = m.part("leaves", tint=LEAF, group="leaves", k=0.12)
    for c, r in (((0, 0.4, 0), 0.4), ((0.3, 0.3, 0.1), 0.3), ((-0.3, 0.3, -0.05), 0.32), ((0.05, 0.3, 0.3), 0.28), ((-0.05, 0.32, -0.3), 0.28)):
        b.add(lambda p, c=c, r=r: sd_sphere(p, c, r), WHITE, 1.0)
    b.cut(lambda p: p[:, 1], 0.05)


@N("bush_holly", (-0.7, -0.05, -0.7), (0.7, 0.9, 0.7), 0.012, "호랑가시 덤불 (빨간 열매)")
def bush_holly(m):
    b = m.part("leaves", tint=(0.25, 0.5, 0.33), group="leaves", k=0.1)
    for c, r in (((0, 0.4, 0), 0.4), ((0.3, 0.3, 0.1), 0.3), ((-0.3, 0.3, -0.05), 0.32), ((0.05, 0.3, 0.3), 0.28)):
        b.add(lambda p, c=c, r=r: sd_sphere(p, c, r), WHITE, 1.0)
    b.cut(lambda p: p[:, 1], 0.05)
    br = m.part("berries", k=0.0)
    rng = np.random.default_rng(3)
    for i in range(18):
        a = rng.uniform(0, 2 * np.pi); e = rng.uniform(0.1, 1.2)
        c = np.array([np.cos(a) * np.cos(e), np.sin(e), np.sin(a) * np.cos(e)]) * 0.47 + [0, 0.33, 0]
        br.add(lambda p, c=c: sd_sphere(p, c, 0.04), (0.9, 0.18, 0.2), 0.0)


@N("bush_snowy", (-0.7, -0.05, -0.7), (0.7, 0.95, 0.7), 0.014, "눈 덮인 덤불")
def bush_snowy(m):
    b = m.part("leaves", tint=(0.35, 0.6, 0.4), group="leaves", k=0.12)
    for c, r in (((0, 0.4, 0), 0.4), ((0.3, 0.3, 0.1), 0.3), ((-0.3, 0.3, -0.05), 0.32)):
        b.add(lambda p, c=c, r=r: sd_sphere(p, c, r), WHITE, 1.0)
    b.cut(lambda p: p[:, 1], 0.05)
    b.paint(lambda p: 0.62 - p[:, 1] - 0.08 * np.sin(p[:, 0] * 14) * np.sin(p[:, 2] * 12), SNOW, 0.0, soft=0.03)


@N("rock_big", (-0.7, -0.05, -0.6), (0.7, 0.75, 0.6), 0.012, "큰 바위")
def rock(m):
    r = m.part("rock", tint=(0.66, 0.66, 0.68), group="rock", k=0.12)
    r.add(lambda p: sd_ellipsoid(p, (0, 0.25, 0), (0.55, 0.42, 0.45), rot(y=20)), WHITE, 1.0)
    r.add(lambda p: sd_ellipsoid(p, (0.3, 0.18, 0.15), (0.3, 0.25, 0.3)), WHITE, 1.0)
    r.cut(lambda p: p[:, 1], 0.05)
    r.cut(lambda p: 0.55 + 0.05 * p[:, 0] - p[:, 1], 0.08)
    r.paint(lambda p: 0.48 - p[:, 1], SNOW, 0.0, soft=0.04)


@N("rock_small", (-0.35, -0.05, -0.3), (0.35, 0.35, 0.3), 0.008, "작은 돌")
def rock_s(m):
    r = m.part("rock", tint=(0.68, 0.66, 0.66), group="rock", k=0.06)
    r.add(lambda p: sd_ellipsoid(p, (0, 0.1, 0), (0.25, 0.18, 0.2), rot(y=-20)), WHITE, 1.0)
    r.add(lambda p: sd_ellipsoid(p, (0.18, 0.06, 0.1), (0.12, 0.1, 0.12)), WHITE, 1.0)
    r.cut(lambda p: p[:, 1], 0.03)


@N("snow_pile", (-0.9, -0.05, -0.8), (0.9, 0.55, 0.8), 0.014, "눈 더미")
def snow_pile(m):
    s = m.part("snow", k=0.15)
    for c, r in (((0, 0.0, 0), 0.5), ((0.4, -0.05, 0.1), 0.38), ((-0.35, -0.05, -0.1), 0.4), ((0.1, -0.05, -0.35), 0.35)):
        s.add(lambda p, c=c, r=r: sd_ellipsoid(p, c, (r, r * 0.9, r)), SNOW, 0.0)
    s.cut(lambda p: p[:, 1], 0.02)
    s.paint(lambda p: p[:, 1] - 0.1, (0.9, 0.93, 1.0), 0.0, soft=0.15)


@N("snowball_pile", (-0.4, -0.05, -0.4), (0.4, 0.4, 0.4), 0.007, "눈뭉치 쌓인 것")
def snowballs(m):
    s = m.part("snow", k=0.01)
    pts = [(-0.12, 0.09, 0), (0.12, 0.09, 0), (0, 0.09, 0.18), (0, 0.09, -0.17), (-0.06, 0.25, 0.06), (0.07, 0.25, 0.05), (0, 0.25, -0.08), (0, 0.38, 0)]
    for c in pts:
        s.add(lambda p, c=c: sd_sphere(p, c, 0.1), SNOW, 0.0)


@N("fence", (-1.05, -0.05, -0.12), (1.05, 0.9, 0.12), 0.01, "나무 울타리 (2m, 이어붙임)", tile=2.0)
def fence(m):
    w = m.part("wood", tint=(0.95, 0.93, 0.88), group="wood", k=0.01)
    for x in np.linspace(-0.9, 0.9, 7):
        w.add(lambda p, x=x: sd_roundbox(p, (x, 0.36, 0), (0.07, 0.36, 0.03), 0.02), WHITE, 1.0)
        w.add(lambda p, x=x: sd_roundcone(p, (x, 0.7, 0), (x, 0.8, 0), 0.06, 0.02), WHITE, 1.0)
    for y in (0.25, 0.55):
        w.add(lambda p, y=y: sd_roundbox(p, (0, y, -0.05), (1.0, 0.04, 0.02), 0.015), WHITE, 1.0)
    sn = m.part("snow", k=0.02)
    for x in np.linspace(-0.9, 0.9, 7):
        sn.add(lambda p, x=x: sd_ellipsoid(p, (x, 0.81, 0), (0.075, 0.035, 0.05)), SNOW, 0.0)


@N("lamp_post", (-0.4, -0.05, -0.4), (0.4, 2.7, 0.4), 0.01, "가로등 (리본 장식)", light=[0, 2.25, 0])
def lamp_post(m):
    b = m.part("metal", tint=(0.22, 0.3, 0.28), group="metal", gloss=0.5, k=0.03)
    b.add(lambda p: sd_cone(p, (0, 0, 0), 0.3, 0.2, 0.08, 0.03), WHITE, 1.0)
    b.add(lambda p: sd_capsule(p, (0, 0.2, 0), (0, 2.0, 0), 0.045), WHITE, 1.0)
    b.add(lambda p: sd_cone(p, (0, 2.0, 0), 0.08, 0.15, 0.12, 0.02), WHITE, 1.0)
    b.add(lambda p: sd_cone(p, (0, 2.42, 0), 0.12, 0.22, 0.03, 0.03), WHITE, 1.0)
    b.add(lambda p: sd_sphere(p, (0, 2.6, 0), 0.04), WHITE, 1.0)
    for a in (45, 135, 225, 315):
        aa = np.radians(a)
        b.add(lambda p, aa=aa: sd_capsule(p, (np.cos(aa) * 0.13, 2.08, np.sin(aa) * 0.13), (np.cos(aa) * 0.18, 2.42, np.sin(aa) * 0.18), 0.012), WHITE, 1.0)
    g = m.part("glass", k=0.0)
    g.add(lambda p: sd_cone(p, (0, 2.08, 0), 0.34, 0.13, 0.17, 0.0), (1.0, 0.93, 0.65), 0.0)
    r = m.part("ribbon", tint=(0.88, 0.22, 0.24), group="ribbon", k=0.02)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.1, 1.85, 0.06), (0.1, 0.05, 0.03), rot(z=s * 20)), WHITE, 1.0)
        r.add(lambda p, s=s: sd_roundbox(p, (s * 0.05, 1.7, 0.06), (0.025, 0.12, 0.008), 0.008, rot(z=s * 15)), WHITE, 1.0)
    r.add(lambda p: sd_sphere(p, (0, 1.85, 0.07), 0.04), WHITE, 1.0)
    w = m.part("wreathlet", k=0.02)
    for i in range(10):
        a = np.radians(i * 36)
        w.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.08, 1.85 + np.sin(a) * 0.08, 0.05), 0.035), (0.3, 0.55, 0.38), 0.0)
    return dict(emissiveParts=["glass"])


@N("mailbox", (-0.3, -0.05, -0.3), (0.3, 1.3, 0.35), 0.008, "우체통")
def mailbox(m):
    p_ = m.part("post", tint=(0.72, 0.5, 0.32), group="wood", k=0.02)
    p_.add(lambda p: sd_roundbox(p, (0, 0.45, 0), (0.05, 0.45, 0.05), 0.02), WHITE, 1.0)
    b = m.part("box", tint=(0.88, 0.25, 0.25), group="box", gloss=0.5, k=0.02)
    b.add(lambda p: smin(sd_roundbox(p, (0, 0.98, 0), (0.16, 0.1, 0.25), 0.02), sd_cylinder(p, (0, 1.08, 0), 0.25, 0.16, 0.02, rot(x=90)), 0.01), WHITE, 1.0)
    b.add(lambda p: sd_roundbox(p, (0.18, 1.12, -0.05), (0.008, 0.08, 0.02), 0.005), (0.98, 0.8, 0.3), 0.0)
    sn = m.part("snow", k=0.02)
    sn.add(lambda p: sd_ellipsoid(p, (0, 1.24, 0), (0.15, 0.05, 0.24)), SNOW, 0.0)


@N("bench", (-0.9, -0.05, -0.4), (0.9, 0.95, 0.4), 0.01, "공원 벤치 (눈)")
def bench(m):
    w = m.part("wood", tint=(0.72, 0.48, 0.3), group="wood", k=0.01)
    for z in (-0.12, 0.0, 0.12):
        w.add(lambda p, z=z: sd_roundbox(p, (0, 0.42, z + 0.02), (0.8, 0.025, 0.05), 0.015), WHITE, 1.0)
    for y in (0.62, 0.78):
        w.add(lambda p, y=y: sd_roundbox(p, (0, y, -0.2), (0.8, 0.05, 0.02), 0.015, rot(x=-10)), WHITE, 1.0)
    i = m.part("iron", tint=(0.25, 0.25, 0.28), group="metal", gloss=0.5, k=0.02)
    for x in (-0.7, 0.7):
        i.add(lambda p, x=x: sd_capsule(p, (x, 0.02, 0.15), (x, 0.4, 0.1), 0.03), WHITE, 1.0)
        i.add(lambda p, x=x: sd_capsule(p, (x, 0.02, -0.18), (x, 0.88, -0.24), 0.03), WHITE, 1.0)
        i.add(lambda p, x=x: sd_capsule(p, (x, 0.55, -0.2), (x, 0.55, 0.18), 0.025), WHITE, 1.0)
    sn = m.part("snow", k=0.03)
    sn.add(lambda p: sd_ellipsoid(p, (0.15, 0.46, 0.0), (0.55, 0.03, 0.15)), SNOW, 0.0)


@N("signpost", (-0.5, -0.05, -0.2), (0.5, 1.6, 0.2), 0.008, "표지판")
def signpost(m):
    w = m.part("wood", tint=(0.72, 0.5, 0.32), group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.65, 0), (0.05, 0.65, 0.05), 0.02), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0.1, 1.25, 0.06), (0.38, 0.13, 0.03), 0.03, rot(z=4)), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (-0.08, 0.95, 0.06), (0.32, 0.11, 0.03), 0.03, rot(z=-5)), WHITE, 1.0)
    sn = m.part("snow", k=0.02)
    sn.add(lambda p: sd_ellipsoid(p, (0.1, 1.39, 0.06), (0.36, 0.035, 0.04)), SNOW, 0.0)
    sn.add(lambda p: sd_ellipsoid(p, (0, 1.32, 0), (0.06, 0.03, 0.06)), SNOW, 0.0)


@N("igloo", (-1.3, -0.05, -1.3), (1.3, 1.4, 1.7), 0.02, "이글루")
def igloo(m):
    s = m.part("snow", tint=(0.92, 0.95, 1.0), group="ice", k=0.05)
    s.add(lambda p: sd_sphere(p, (0, 0, 0), 1.2), WHITE, 1.0)
    s.add(lambda p: sd_cylinder(p, (0, 0.35, 1.15), 0.4, 0.45, 0.1, rot(x=90)), WHITE, 1.0)
    s.cut(lambda p: sd_sphere(p, (0, 0, 0), 1.05), 0.0)
    s.cut(lambda p: sd_cylinder(p, (0, 0.3, 1.25), 0.5, 0.32, 0.05, rot(x=90)), 0.03)
    s.cut(lambda p: p[:, 1], 0.0)
    def bricks(p):
        ang = np.arctan2(p[:, 2], p[:, 0]); row = np.floor(p[:, 1] / 0.25)
        a = np.mod(ang * 1.2 / (0.2) + (row % 2) * 0.5, 1.0)
        return np.minimum(np.abs(np.mod(p[:, 1], 0.25) - 0.125) - 0.112, 0) * -1 + np.minimum(np.abs(a - 0.5) - 0.46, 0) * -1 - 0.0001
    s.paint(lambda p: -(np.maximum(np.abs(np.mod(p[:, 1], 0.25) - 0.125) - 0.113, 0)) + 0.0005 - 0.001 * 0, (0.82, 0.86, 0.95), 1.0, soft=0.005)


@N("ice_skates", (-0.25, -0.05, -0.2), (0.25, 0.35, 0.25), 0.004, "스케이트 한 켤레")
def skates(m):
    b = m.part("boot", tint=(0.98, 0.97, 0.95), group="boot", k=0.02)
    for x in (-0.09, 0.09):
        b.add(lambda p, x=x: sd_roundbox(p, (x, 0.17, -0.03), (0.05, 0.1, 0.06), 0.04), WHITE, 1.0)
        b.add(lambda p, x=x: sd_roundbox(p, (x, 0.1, 0.04), (0.05, 0.04, 0.09), 0.035), WHITE, 1.0)
        b.add(lambda p, x=x: sd_torus(p, (x, 0.27, -0.03), 0.055, 0.015), (0.85, 0.3, 0.35), 0.0)
    bl = m.part("blade", gloss=1.0, k=0.005)
    for x in (-0.09, 0.09):
        bl.add(lambda p, x=x: sd_roundbox(p, (x, 0.03, 0.01), (0.006, 0.025, 0.14), 0.005), (0.85, 0.87, 0.9), 0.0)
        bl.add(lambda p, x=x: sd_torus(p, (x, 0.05, 0.14), 0.03, 0.006, rot(z=90)), (0.85, 0.87, 0.9), 0.0)


@N("lantern_ground", (-0.2, -0.05, -0.2), (0.2, 0.55, 0.2), 0.006, "바닥 랜턴 (촛불)", light=[0, 0.25, 0])
def lantern(m):
    b = m.part("metal", tint=(0.25, 0.25, 0.28), group="metal", gloss=0.6, k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, 0.03, 0), (0.13, 0.03, 0.13), 0.01), WHITE, 1.0)
    b.add(lambda p: sd_cone(p, (0, 0.38, 0), 0.1, 0.15, 0.03, 0.01), WHITE, 1.0)
    b.add(lambda p: sd_torus(p, (0, 0.5, 0), 0.05, 0.01, rot(x=90)), WHITE, 1.0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.add(lambda p, sx=sx, sz=sz: sd_capsule(p, (sx * 0.11, 0.05, sz * 0.11), (sx * 0.11, 0.38, sz * 0.11), 0.012), WHITE, 1.0)
    g = m.part("glass", k=0.01)
    g.add(lambda p: sd_box(p, (0, 0.21, 0), (0.1, 0.16, 0.1)), (1.0, 0.9, 0.6), 0.0)
    return dict(emissiveParts=["glass"])
