"""집 안 가구 (거실·부엌·침실). 크기는 m, 캐릭터 키 약 1.3m 기준. 앞면 = +z"""
from assets import *

WOOD = (0.76, 0.54, 0.34)
WOOD_D = (0.55, 0.37, 0.24)
FAB_R = (0.85, 0.3, 0.3)
CREAMF = (0.97, 0.93, 0.85)
BRASS = (0.92, 0.75, 0.4)
STONE = (0.78, 0.74, 0.7)
BRICK = (0.78, 0.42, 0.32)


def legs(part, xs, zs, y0, y1, r, color=WHITE, mask=1.0, taper=0.75):
    for x in xs:
        for z in zs:
            part.add(lambda p, x=x, z=z: sd_roundcone(p, (x, y0 + r * taper, z), (x, y1, z), r * taper, r), color, mask, k=0.01)


def F(name, lo, hi, step, desc, **kw):
    return asset(name, "furniture", lo, hi, step, desc, **kw)


# ---------------------------------------------------------------- 거실
@F("sofa", (-1.05, -0.02, -0.5), (1.05, 0.95, 0.5), 0.016, "3인용 푹신 소파", footprint=[2.0, 0.9])
def sofa(m):
    f = m.part("fabric", tint=FAB_R, group="fabric", k=0.05)
    f.add(lambda p: sd_roundbox(p, (0, 0.28, 0), (0.95, 0.15, 0.42), 0.1), WHITE, 1.0)
    f.add(lambda p: sd_roundbox(p, (0, 0.6, -0.3), (0.95, 0.3, 0.13), 0.12, rot(x=-8)), WHITE, 1.0)
    for s in (-1, 1):
        f.add(lambda p, s=s: sd_roundbox(p, (s * 0.92, 0.45, 0.0), (0.12, 0.22, 0.42), 0.11), WHITE, 1.0)
    c = m.part("cushion", tint=(0.98, 0.9, 0.8), group="fabric2", k=0.04)
    for x in (-0.48, 0.0, 0.48):
        c.add(lambda p, x=x: sd_roundbox(p, (x, 0.47, 0.04), (0.22, 0.06, 0.32), 0.06), WHITE, 1.0, k=0.0)
    w = m.part("legs", tint=WOOD_D, group="wood", k=0.01)
    legs(w, (-0.88, 0.88), (-0.34, 0.34), 0.0, 0.14, 0.045)


@F("armchair", (-0.6, -0.02, -0.5), (0.6, 1.05, 0.5), 0.014, "1인 안락의자 (등받이 둥근)", footprint=[1.0, 0.9])
def armchair(m):
    f = m.part("fabric", tint=(0.4, 0.6, 0.45), group="fabric", k=0.06)
    f.add(lambda p: sd_roundbox(p, (0, 0.3, 0.02), (0.45, 0.15, 0.4), 0.11), WHITE, 1.0)
    f.add(lambda p: sd_ellipsoid(p, (0, 0.72, -0.3), (0.5, 0.36, 0.14), rot(x=-8)), WHITE, 1.0)
    for s in (-1, 1):
        f.add(lambda p, s=s: sd_roundbox(p, (s * 0.44, 0.47, 0.0), (0.1, 0.2, 0.4), 0.1), WHITE, 1.0)
    f.paint(lambda p: sd_ellipsoid(p, (0, 0.72, -0.17), (0.08, 0.08, 0.05)), (0.85, 0.85, 0.85), 1.0, soft=0.02)   # 단추
    c = m.part("cushion", tint=(0.98, 0.92, 0.8), group="fabric2", k=0.0)
    c.add(lambda p: sd_roundbox(p, (0, 0.48, 0.06), (0.33, 0.06, 0.3), 0.06), WHITE, 1.0)
    w = m.part("legs", tint=WOOD_D, group="wood")
    legs(w, (-0.4, 0.4), (-0.32, 0.34), 0.0, 0.16, 0.045)


@F("rug_round", (-1.65, -0.02, -1.65), (1.65, 0.06, 1.65), 0.03, "둥근 러그", footprint=[3.2, 3.2], ao=False)
def rug_round(m):
    r = m.part("rug", tint=(0.86, 0.32, 0.3), group="fabric", k=0.0)
    r.add(lambda p: sd_cylinder(p, (0, 0.012, 0), 0.012, 1.55, 0.01), WHITE, 1.0)
    for rr, col in ((1.4, (0.98, 0.94, 0.86)), (0.9, (0.98, 0.94, 0.86)), (0.45, (0.85, 0.85, 0.85))):
        r.paint(lambda p, rr=rr: np.abs(np.hypot(p[:, 0], p[:, 2]) - rr) - 0.05, col, 0.0 if col[0] > 0.9 else 1.0, soft=0.02)


@F("rug_rect", (-1.6, -0.02, -1.1), (1.6, 0.06, 1.1), 0.03, "네모 러그 (체크무늬)", footprint=[3.0, 2.0], ao=False)
def rug_rect(m):
    r = m.part("rug", tint=(0.35, 0.55, 0.42), group="fabric", k=0.0)
    r.add(lambda p: sd_roundbox(p, (0, 0.012, 0), (1.5, 0.012, 1.0), 0.01), WHITE, 1.0)
    r.paint(lambda p: np.minimum(np.abs(np.mod(p[:, 0], 0.5) - 0.25), np.abs(np.mod(p[:, 2], 0.5) - 0.25)) - 0.05, (0.8, 0.8, 0.8), 1.0, soft=0.02)
    r.paint(lambda p: np.minimum(1.5 - np.abs(p[:, 0]), 1.0 - np.abs(p[:, 2])) - 0.1, (0.98, 0.94, 0.86), 0.0, soft=0.02)


@F("coffee_table", (-0.75, -0.02, -0.5), (0.75, 0.5, 0.5), 0.012, "둥근 모서리 낮은 탁자", footprint=[1.4, 0.9])
def coffee_table(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.38, 0), (0.7, 0.045, 0.45), 0.04), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.14, 0), (0.6, 0.02, 0.36), 0.02), WHITE, 1.0)
    legs(w, (-0.6, 0.6), (-0.36, 0.36), 0.0, 0.36, 0.045)
    w.paint(lambda p: np.abs(np.sin(p[:, 0] * 30 + np.sin(p[:, 2] * 8) * 1.5)) - 0.1 + (p[:, 1] < 0.4) * 5, (0.88, 0.88, 0.88), 1.0, soft=0.2)


@F("side_table", (-0.32, -0.02, -0.32), (0.32, 0.62, 0.32), 0.01, "둥근 협탁", footprint=[0.6, 0.6])
def side_table(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.02)
    w.add(lambda p: sd_cylinder(p, (0, 0.55, 0), 0.035, 0.3, 0.025), WHITE, 1.0)
    w.add(lambda p: sd_roundcone(p, (0, 0.05, 0), (0, 0.52, 0), 0.06, 0.04), WHITE, 1.0)
    w.add(lambda p: sd_cylinder(p, (0, 0.03, 0), 0.03, 0.2, 0.025), WHITE, 1.0)


@F("floor_lamp", (-0.35, -0.02, -0.35), (0.35, 1.75, 0.35), 0.012, "스탠드 조명", footprint=[0.5, 0.5], light=[0, 1.45, 0])
def floor_lamp(m):
    b = m.part("metal", tint=BRASS, group="metal", gloss=0.8, k=0.02)
    b.add(lambda p: sd_cylinder(p, (0, 0.025, 0), 0.025, 0.2, 0.02), WHITE, 1.0)
    b.add(lambda p: sd_capsule(p, (0, 0.05, 0), (0, 1.4, 0), 0.022), WHITE, 1.0)
    s = m.part("shade", tint=CREAMF, group="shade", k=0.02)
    s.add(lambda p: sd_cone(p, (0, 1.3, 0), 0.34, 0.3, 0.18, 0.02), WHITE, 1.0)
    s.cut(lambda p: sd_cone(p, (0, 1.26, 0), 0.4, 0.27, 0.15, 0.0), 0.0)
    s.paint(lambda p: np.abs(p[:, 1] - 1.33) - 0.012, (0.85, 0.85, 0.85), 1.0, soft=0.004)
    bulb = m.part("bulb", k=0.0)
    bulb.add(lambda p: sd_sphere(p, (0, 1.38, 0), 0.07), (1.0, 0.95, 0.75), 0.0)
    return dict(emissiveParts=["bulb", "shade"])


@F("table_lamp", (-0.22, -0.02, -0.22), (0.22, 0.55, 0.22), 0.007, "탁상 조명", footprint=[0.3, 0.3])
def table_lamp(m):
    b = m.part("base", tint=(0.55, 0.7, 0.85), group="base", gloss=0.8, k=0.02)
    b.add(lambda p: sd_sphere(p, (0, 0.13, 0), 0.12), WHITE, 1.0)
    b.add(lambda p: sd_cylinder(p, (0, 0.02, 0), 0.02, 0.08, 0.015), WHITE, 1.0)
    b.add(lambda p: sd_capsule(p, (0, 0.2, 0), (0, 0.32, 0), 0.015), BRASS, 0.0)
    s = m.part("shade", tint=CREAMF, group="shade", k=0.01)
    s.add(lambda p: sd_cone(p, (0, 0.28, 0), 0.2, 0.19, 0.12, 0.015), WHITE, 1.0)
    s.cut(lambda p: sd_cone(p, (0, 0.25, 0), 0.25, 0.17, 0.1, 0.0), 0.0)


@F("fireplace", (-1.05, -0.02, -0.55), (1.05, 2.4, 0.5), 0.016, "벽난로 (벽돌, 장작불)", footprint=[2.0, 0.8], light=[0, 0.35, 0.15])
def fireplace(m):
    b = m.part("brick", tint=BRICK, group="brick", k=0.02)
    b.add(lambda p: sd_roundbox(p, (0, 0.6, -0.15), (0.95, 0.6, 0.3), 0.05), WHITE, 1.0)
    b.add(lambda p: sd_roundbox(p, (0, 1.7, -0.3), (0.6, 0.65, 0.18), 0.05), WHITE, 1.0)
    b.cut(lambda p: smin(sd_box(p, (0, 0.35, 0.1), (0.5, 0.35, 0.4)), sd_cylinder(p, (0, 0.7, 0.1), 0.4, 0.5, 0.0, rot(x=90)), 0.0), 0.04)
    # 벽돌 줄눈
    def mortar(p):
        row = np.floor(p[:, 1] / 0.13)
        x = p[:, 0] + (row % 2) * 0.13
        return np.minimum(np.abs(np.mod(p[:, 1], 0.13) - 0.065), np.abs(np.mod(x, 0.26) - 0.13)) - 0.055
    b.paint(lambda p: -mortar(p) - 0.003, (0.9, 0.86, 0.82), 0.0, soft=0.005)
    st = m.part("mantel", tint=WOOD_D, group="wood", k=0.02)
    st.add(lambda p: sd_roundbox(p, (0, 1.24, 0.0), (1.02, 0.06, 0.4), 0.03), WHITE, 1.0)
    st.add(lambda p: sd_roundbox(p, (0, 0.04, 0.05), (1.0, 0.04, 0.45), 0.03), STONE, 0.0)
    ins = m.part("inside", k=0.01)
    ins.add(lambda p: sd_box(p, (0, 0.4, -0.25), (0.5, 0.4, 0.04)), (0.25, 0.17, 0.15), 0.0)
    for x, rz in ((-0.15, 20), (0.15, -20), (0.0, 0)):
        ins.add(lambda p, x=x, rz=rz: sd_capsule(p, (x - 0.18, 0.13 + (0.06 if rz == 0 else 0), 0.05), (x + 0.18, 0.13 + (0.06 if rz == 0 else 0), -0.05 if rz else 0.05), 0.06),
                (0.55, 0.36, 0.24), 0.0, k=0.01)
    fire = m.part("fire", k=0.03)
    for x, h, r in ((0, 0.5, 0.15), (-0.15, 0.36, 0.1), (0.15, 0.4, 0.11)):
        fire.add(lambda p, x=x, h=h, r=r: sd_roundcone(p, (x, 0.22, 0.02), (x, 0.22 + h, 0.02), r, 0.01), (1.0, 0.6, 0.2), 0.0)
    fire.paint(lambda p: p[:, 1] - 0.36, (1.0, 0.85, 0.35), 0.0, soft=-0.08)
    return dict(emissiveParts=["fire"])


@F("bookshelf", (-0.7, -0.02, -0.25), (0.7, 1.95, 0.25), 0.016, "책장 (책 꽂힘)", footprint=[1.3, 0.4])
def bookshelf(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.015)
    w.add(lambda p: sd_roundbox(p, (0, 0.95, 0), (0.62, 0.92, 0.2), 0.03), WHITE, 1.0)
    for y in (0.12, 0.58, 1.04, 1.5):
        w.cut(lambda p, y=y: sd_box(p, (0, y + 0.2, 0.06), (0.56, 0.19, 0.2)), 0.015)
    bk = m.part("books", k=0.0)
    rng = np.random.default_rng(4)
    cols = [(0.85, 0.3, 0.3), (0.3, 0.55, 0.8), (0.95, 0.8, 0.35), (0.4, 0.65, 0.4), (0.6, 0.4, 0.7), (0.95, 0.6, 0.5), (0.9, 0.9, 0.85)]
    for y in (0.12, 0.58, 1.04, 1.5):
        x = -0.53
        while x < 0.5:
            wdt = rng.uniform(0.035, 0.06); h = rng.uniform(0.24, 0.35); tilt = 0 if rng.random() > 0.12 else 12
            c = cols[rng.integers(len(cols))]
            if rng.random() > 0.12:
                bk.add(lambda p, x=x, y=y, wdt=wdt, h=h, tilt=tilt: sd_roundbox(p, (x + wdt, y + 0.02 + h / 2, 0.04), (wdt, h / 2, 0.12), 0.012, rot(z=-tilt)), c, 0.0)
            x += wdt * 2 + 0.006 + (0.06 if tilt else 0)


@F("piano", (-0.85, -0.02, -0.45), (0.85, 1.4, 0.55), 0.012, "업라이트 피아노", footprint=[1.6, 0.7])
def piano(m):
    w = m.part("body", tint=(0.32, 0.2, 0.16), group="wood", gloss=0.6, k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.7, -0.2), (0.8, 0.65, 0.22), 0.04), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.72, 0.1), (0.8, 0.05, 0.16), 0.03), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.35, 0.05), (0.78, 0.3, 0.1), 0.03), WHITE, 1.0)
    w.cut(lambda p: sd_box(p, (0, 0.85, 0.2), (0.74, 0.08, 0.14)), 0.01)
    k = m.part("keys", k=0.0)
    k.add(lambda p: sd_roundbox(p, (0, 0.8, 0.14), (0.72, 0.025, 0.1), 0.01), (0.98, 0.97, 0.94), 0.0)
    for i in range(36):
        if i % 7 in (2, 6): continue
        x = -0.7 + i * 0.04 + 0.03
        k.add(lambda p, x=x: sd_roundbox(p, (x, 0.83, 0.1), (0.011, 0.018, 0.06), 0.006), (0.12, 0.1, 0.1), 0.0)
    for i in range(37):
        k.paint(lambda p, i=i: np.abs(p[:, 0] - (-0.72 + i * 0.039)) - 0.002 + (p[:, 1] < 0.81) * 5, (0.7, 0.7, 0.7), 0.0, soft=0.002)
    bench = m.part("bench", tint=(0.32, 0.2, 0.16), group="wood", k=0.02)
    bench.add(lambda p: sd_roundbox(p, (0, 0.45, 0.48), (0.4, 0.05, 0.06), 0.03), WHITE, 1.0)


@F("wall_clock", (-0.45, -0.02, -0.15), (0.45, 1.95, 0.25), 0.01, "괘종시계", footprint=[0.6, 0.4])
def grandfather_clock(m):
    w = m.part("wood", tint=WOOD_D, group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.85, 0), (0.25, 0.85, 0.15), 0.04), WHITE, 1.0)
    w.add(lambda p: sd_cylinder(p, (0, 1.6, 0.02), 0.13, 0.3, 0.04, rot(x=90)), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.06, 0), (0.3, 0.06, 0.19), 0.03), WHITE, 1.0)
    w.cut(lambda p: sd_box(p, (0, 0.75, 0.15), (0.15, 0.4, 0.05)), 0.02)
    f = m.part("face", k=0.0)
    f.add(lambda p: sd_cylinder(p, (0, 1.6, 0.13), 0.02, 0.22, 0.015, rot(x=90)), (0.98, 0.96, 0.9), 0.0)
    f.add(lambda p: sd_torus(p, (0, 1.6, 0.14), 0.23, 0.02, rot(x=90)), BRASS, 0.0)
    for i in range(12):
        a = np.radians(i * 30)
        f.add(lambda p, a=a: sd_sphere(p, (np.sin(a) * 0.17, 1.6 + np.cos(a) * 0.17, 0.15), 0.012), (0.2, 0.15, 0.15), 0.0)
    f.add(lambda p: sd_capsule(p, (0, 1.6, 0.16), (0.0, 1.73, 0.16), 0.01), (0.2, 0.15, 0.15), 0.0)
    f.add(lambda p: sd_capsule(p, (0, 1.6, 0.16), (0.09, 1.56, 0.16), 0.01), (0.2, 0.15, 0.15), 0.0)
    pend = m.part("pendulum", gloss=0.9, k=0.01, pivot=(0, 1.15, 0.1))
    pend.add(lambda p: sd_capsule(p, (0, 1.15, 0.1), (0, 0.55, 0.1), 0.01), BRASS, 0.0)
    pend.add(lambda p: sd_cylinder(p, (0, 0.52, 0.1), 0.015, 0.08, 0.01, rot(x=90)), BRASS, 0.0)


@F("tv_retro", (-0.55, -0.02, -0.4), (0.55, 1.05, 0.45), 0.01, "레트로 TV", footprint=[1.0, 0.7])
def tv(m):
    w = m.part("body", tint=(0.86, 0.6, 0.4), group="body", k=0.04)
    w.add(lambda p: sd_roundbox(p, (0, 0.62, -0.05), (0.48, 0.36, 0.3), 0.12), WHITE, 1.0)
    w.cut(lambda p: sd_roundbox(p, (-0.08, 0.62, 0.3), (0.32, 0.26, 0.1), 0.08), 0.02)
    legs(w, (-0.35, 0.35), (-0.15,), 0.0, 0.28, 0.03)
    s = m.part("screen", gloss=1.0, k=0.0)
    s.add(lambda p: sd_ellipsoid(p, (-0.08, 0.62, 0.17), (0.31, 0.25, 0.06)), (0.3, 0.38, 0.42), 0.0)
    for y in (0.75, 0.6):
        s.add(lambda p, y=y: sd_cylinder(p, (0.36, y, 0.24), 0.02, 0.04, 0.01, rot(x=90)), (0.95, 0.9, 0.8), 0.0)
    s.add(lambda p: sd_capsule(p, (-0.1, 0.98, -0.1), (-0.3, 1.2, -0.15), 0.01), (0.6, 0.6, 0.62), 0.0)
    s.add(lambda p: sd_capsule(p, (0.1, 0.98, -0.1), (0.3, 1.2, -0.15), 0.01), (0.6, 0.6, 0.62), 0.0)
    return dict()


@F("window", (-0.75, -0.02, -0.12), (0.75, 1.65, 0.16), 0.01, "격자 창문 (벽에 붙임, 원점=창 아래 중심)", footprint=[1.4, 0.2], wall=True)
def window(m):
    fr = m.part("frame", tint=(0.98, 0.96, 0.92), group="frame", k=0.01)
    fr.add(lambda p: sd_roundbox(p, (0, 0.8, 0), (0.65, 0.8, 0.07), 0.03), WHITE, 1.0)
    for x in (-0.31, 0.31):
        for y in (0.42, 1.18):
            fr.cut(lambda p, x=x, y=y: sd_roundbox(p, (x, y, 0), (0.27, 0.34, 0.2), 0.02), 0.0)
    fr.add(lambda p: sd_roundbox(p, (0, 0.0, 0.06), (0.72, 0.035, 0.1), 0.02), WHITE, 1.0)
    g = m.part("glass", gloss=1.0, k=0.0)
    g.add(lambda p: sd_box(p, (0, 0.8, -0.01), (0.62, 0.77, 0.01)), (0.72, 0.85, 0.95), 0.0)
    g.paint(lambda p: np.abs(p[:, 0] + p[:, 1] * 0.6 - 0.5 - np.round((p[:, 0] + p[:, 1] * 0.6) / 0.8) * 0.8 * 0) - 0.05, (0.92, 0.97, 1.0), 0.0, soft=0.03)
    sn = m.part("snow", k=0.02)   # 창틀 눈
    for x in (-0.31, 0.31):
        for y in (0.08, 0.84):
            sn.add(lambda p, x=x, y=y: sd_ellipsoid(p, (x, y, -0.02), (0.26, 0.035, 0.03)), (0.99, 0.99, 1.0), 0.0)
    return dict(emissiveParts=["glass"])


@F("curtain", (-0.95, -0.02, -0.12), (0.95, 2.1, 0.2), 0.012, "커튼 한 쌍 + 봉", footprint=[1.8, 0.2], wall=True)
def curtain(m):
    c = m.part("cloth", tint=(0.85, 0.3, 0.32), group="fabric", k=0.03)
    for s in (-1, 1):
        def drape(p, s=s):
            x = p[:, 0] * s - 0.7; y = p[:, 1]
            wave = 0.04 * np.sin(x * 40) + 0.05
            width = 0.16 + 0.08 * (y < 1.0) * (1.0 - y) ** 1 - 0.07 * np.exp(-((y - 1.0) / 0.12) ** 2)
            d_x = np.abs(x) - width
            d_z = np.abs(p[:, 2] - wave * 0.5) - 0.035
            d_y = np.maximum(y - 1.98, 0.02 - y)
            return np.maximum(np.maximum(d_x, d_z), d_y)
        c.add(drape, WHITE, 1.0)
        c.add(lambda p, s=s: sd_torus(p, (s * 0.7, 1.0, 0.03), 0.16, 0.03, rot(z=90, y=90)), (0.98, 0.85, 0.4), 0.0)
    r = m.part("rod", tint=WOOD_D, group="wood", k=0.01)
    r.add(lambda p: sd_capsule(p, (-0.92, 2.02, 0.02), (0.92, 2.02, 0.02), 0.025), WHITE, 1.0)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_sphere(p, (s * 0.92, 2.02, 0.02), 0.045), WHITE, 1.0)


@F("picture_frame", (-0.38, -0.02, -0.05), (0.38, 0.6, 0.08), 0.006, "액자 (벽걸이, 눈 풍경)", footprint=[0.7, 0.1], wall=True)
def frame(m):
    f = m.part("frame", tint=(0.92, 0.75, 0.4), group="frame", gloss=0.6, k=0.01)
    f.add(lambda p: sd_roundbox(p, (0, 0.28, 0), (0.34, 0.26, 0.03), 0.02), WHITE, 1.0)
    f.cut(lambda p: sd_box(p, (0, 0.28, 0.03), (0.28, 0.2, 0.03)), 0.005)
    a = m.part("art", k=0.0)
    a.add(lambda p: sd_box(p, (0, 0.28, 0.005), (0.285, 0.205, 0.006)), (0.62, 0.8, 0.95), 0.0)
    a.paint(lambda p: p[:, 1] - 0.2 - 0.03 * np.sin(p[:, 0] * 12), (0.98, 0.98, 1.0), 0.0, soft=0.004)
    a.paint(lambda p: np.maximum(np.abs(p[:, 0] - 0.1) - (0.38 - p[:, 1]) * 0.5, np.abs(p[:, 1] - 0.3) - 0.12), (0.3, 0.6, 0.4), 0.0, soft=0.003)
    a.paint(lambda p: np.hypot(p[:, 0] + 0.15, p[:, 1] - 0.4) - 0.04, (1.0, 0.95, 0.7), 0.0, soft=0.003)


@F("cushion", (-0.3, -0.02, -0.3), (0.3, 0.3, 0.3), 0.008, "방석 쿠션", footprint=[0.5, 0.5])
def cushion(m):
    c = m.part("fabric", tint=(0.95, 0.7, 0.4), group="fabric", k=0.05)
    def puffy(p):
        q = np.abs(p - [0, 0.12, 0])
        return sd_roundbox(p, (0, 0.12, 0), (0.25, 0.08, 0.25), 0.08) + 0.02 * (1 - np.exp(-((q[:, 0] - q[:, 2]) ** 2) / 0.005)) * 0
    c.add(puffy, WHITE, 1.0)
    c.add(lambda p: sd_sphere(p, (0, 0.2, 0), 0.025), (0.85, 0.85, 0.85), 1.0)
    for s in ((-1, -1), (-1, 1), (1, -1), (1, 1)):
        c.add(lambda p, s=s: sd_sphere(p, (s[0] * 0.24, 0.12, s[1] * 0.24), 0.04), WHITE, 1.0)


@F("ottoman", (-0.35, -0.02, -0.35), (0.35, 0.5, 0.35), 0.01, "둥근 발받침 스툴", footprint=[0.6, 0.6])
def ottoman(m):
    c = m.part("fabric", tint=(0.55, 0.42, 0.7), group="fabric", k=0.04)
    c.add(lambda p: sd_cylinder(p, (0, 0.24, 0), 0.16, 0.3, 0.08), WHITE, 1.0)
    for i in range(8):
        a = np.radians(i * 45)
        c.paint(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.15, 0.4, np.sin(a) * 0.15), 0.02), (0.8, 0.8, 0.8), 1.0, soft=0.01)
    w = m.part("legs", tint=WOOD_D, group="wood")
    legs(w, (-0.18, 0.18), (-0.18, 0.18), 0.0, 0.1, 0.03)


# ---------------------------------------------------------------- 부엌·식당
@F("dining_table", (-0.95, -0.02, -0.6), (0.95, 0.85, 0.6), 0.014, "식탁 (체크 테이블보)", footprint=[1.8, 1.1])
def dining_table(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.74, 0), (0.9, 0.04, 0.55), 0.03), WHITE, 1.0)
    legs(w, (-0.78, 0.78), (-0.44, 0.44), 0.0, 0.72, 0.055, taper=0.7)
    c = m.part("cloth", tint=(0.88, 0.3, 0.3), group="fabric", k=0.02)
    c.add(lambda p: sd_roundbox(p, (0, 0.795, 0), (0.95, 0.015, 0.6), 0.01), WHITE, 1.0)
    for s in (-1, 1):
        c.add(lambda p, s=s: sd_roundbox(p, (0, 0.72, s * 0.6), (0.95, 0.08, 0.012), 0.01), WHITE, 1.0)
        c.add(lambda p, s=s: sd_roundbox(p, (s * 0.95, 0.72, 0), (0.012, 0.08, 0.6), 0.01), WHITE, 1.0)
    c.paint(lambda p: -np.maximum(np.abs(np.mod(p[:, 0], 0.16) - 0.08), np.abs(np.mod(p[:, 2], 0.16) - 0.08)) + 0.04, (0.98, 0.96, 0.92), 0.0, soft=0.005)


@F("chair", (-0.3, -0.02, -0.3), (0.3, 1.1, 0.3), 0.009, "식탁 의자 (하트 등받이)", footprint=[0.5, 0.5])
def chair(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.015)
    w.add(lambda p: sd_roundbox(p, (0, 0.46, 0), (0.24, 0.035, 0.24), 0.03), WHITE, 1.0)
    legs(w, (-0.19, 0.19), (-0.19, 0.19), 0.0, 0.45, 0.03)
    for s in (-1, 1):
        w.add(lambda p, s=s: sd_capsule(p, (s * 0.19, 0.46, -0.21), (s * 0.19, 1.0, -0.23), 0.03), WHITE, 1.0)
    w.add(lambda p: sd_capsule(p, (-0.19, 1.0, -0.23), (0.19, 1.0, -0.23), 0.035), WHITE, 1.0)
    w.add(lambda p: sd_heart(p, (0, 0.78, -0.225), 0.2, 0.02, 0.015), WHITE, 1.0)
    c = m.part("seat", tint=(0.88, 0.3, 0.3), group="fabric", k=0.03)
    c.add(lambda p: sd_roundbox(p, (0, 0.51, 0.01), (0.2, 0.03, 0.2), 0.03), WHITE, 1.0)


@F("kitchen_counter", (-0.85, -0.02, -0.4), (0.85, 1.0, 0.38), 0.012, "조리대 (서랍장 + 싱크)", footprint=[1.6, 0.7])
def counter(m):
    b = m.part("cabinet", tint=(0.98, 0.95, 0.88), group="cabinet", k=0.015)
    b.add(lambda p: sd_roundbox(p, (0, 0.44, 0), (0.8, 0.44, 0.33), 0.03), WHITE, 1.0)
    for x in (-0.4, 0.4):
        b.cut(lambda p, x=x: sd_roundbox(p, (x, 0.42, 0.36), (0.34, 0.33, 0.04), 0.02), 0.01)
        b.add(lambda p, x=x: sd_roundbox(p, (x, 0.42, 0.315), (0.32, 0.31, 0.02), 0.02), WHITE, 1.0)
        b.add(lambda p, x=x: sd_capsule(p, (x - 0.08, 0.66, 0.35), (x + 0.08, 0.66, 0.35), 0.015), BRASS, 0.0)
    t = m.part("top", tint=(0.55, 0.75, 0.78), group="top", gloss=0.5, k=0.01)
    t.add(lambda p: sd_roundbox(p, (0, 0.91, 0), (0.84, 0.035, 0.36), 0.02), WHITE, 1.0)
    t.cut(lambda p: sd_roundbox(p, (0.35, 0.95, -0.02), (0.25, 0.08, 0.2), 0.04), 0.01)
    f = m.part("faucet", gloss=1.0, k=0.01)
    f.add(lambda p: sd_capsule(p, (0.35, 0.94, -0.28), (0.35, 1.12, -0.28), 0.018), (0.85, 0.87, 0.9), 0.0)
    f.add(lambda p: sd_capsule(p, (0.35, 1.12, -0.28), (0.35, 1.1, -0.12), 0.018), (0.85, 0.87, 0.9), 0.0)


@F("oven", (-0.45, -0.02, -0.4), (0.45, 1.0, 0.4), 0.01, "오븐 레인지", footprint=[0.8, 0.7], light=[0, 0.4, 0.3])
def oven(m):
    b = m.part("body", tint=(0.9, 0.35, 0.33), group="body", gloss=0.5, k=0.02)
    b.add(lambda p: sd_roundbox(p, (0, 0.45, 0), (0.4, 0.45, 0.34), 0.05), WHITE, 1.0)
    b.add(lambda p: sd_roundbox(p, (0, 0.98, -0.3), (0.4, 0.08, 0.04), 0.03), WHITE, 1.0)
    d = m.part("door", k=0.01)
    d.add(lambda p: sd_roundbox(p, (0, 0.4, 0.33), (0.32, 0.25, 0.02), 0.02), (0.25, 0.2, 0.22), 0.0)
    d.add(lambda p: sd_roundbox(p, (0, 0.4, 0.35), (0.22, 0.14, 0.01), 0.02), (0.95, 0.6, 0.3), 0.0)
    d.add(lambda p: sd_capsule(p, (-0.25, 0.71, 0.38), (0.25, 0.71, 0.38), 0.018), (0.85, 0.87, 0.9), 0.0)
    for x in (-0.2, 0.2):
        for z in (-0.12, 0.12):
            d.add(lambda p, x=x, z=z: sd_torus(p, (x, 0.905, z), 0.08, 0.012), (0.2, 0.18, 0.18), 0.0)
    for x in (-0.25, -0.08, 0.08, 0.25):
        d.add(lambda p, x=x: sd_cylinder(p, (x, 0.8, 0.35), 0.02, 0.03, 0.01, rot(x=90)), (0.95, 0.92, 0.85), 0.0)


@F("fridge", (-0.42, -0.02, -0.38), (0.42, 1.9, 0.4), 0.012, "둥근 레트로 냉장고", footprint=[0.8, 0.7])
def fridge(m):
    b = m.part("body", tint=(0.6, 0.82, 0.8), group="body", gloss=0.6, k=0.03)
    b.add(lambda p: sd_roundbox(p, (0, 0.92, 0), (0.38, 0.9, 0.33), 0.14), WHITE, 1.0)
    b.paint(lambda p: np.abs(p[:, 1] - 1.2) - 0.012 + (p[:, 2] < 0.2) * 5, (0.7, 0.7, 0.7), 1.0, soft=0.004)
    h = m.part("handle", gloss=1.0, k=0.01)
    for y0, y1 in ((1.35, 1.6), (0.75, 1.05)):
        h.add(lambda p, y0=y0, y1=y1: sd_capsule(p, (0.28, y0, 0.36), (0.28, y1, 0.36), 0.02), (0.85, 0.87, 0.9), 0.0)


@F("kitchen_shelf", (-0.6, -0.02, -0.15), (0.6, 0.7, 0.2), 0.008, "벽 선반 (그릇, 병)", footprint=[1.1, 0.25], wall=True)
def kshelf(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.01)
    w.add(lambda p: sd_roundbox(p, (0, 0.03, 0), (0.55, 0.025, 0.12), 0.012), WHITE, 1.0)
    for s in (-1, 1):
        w.add(lambda p, s=s: sd_roundbox(p, (s * 0.45, -0.0, -0.08), (0.02, 0.06, 0.04), 0.01), WHITE, 1.0)
    it = m.part("items", k=0.01)
    for x, c in ((-0.4, (0.95, 0.5, 0.45)), (-0.32, (0.95, 0.85, 0.4)), (-0.24, (0.5, 0.75, 0.9))):
        it.add(lambda p, x=x: sd_roundcone(p, (x, 0.12, 0), (x, 0.24, 0), 0.04, 0.02), c, 0.0)
        it.add(lambda p, x=x: sd_cylinder(p, (x, 0.27, 0), 0.02, 0.015, 0.008), (0.75, 0.55, 0.4), 0.0)
    for i in range(4):
        it.add(lambda p, i=i: sd_cylinder(p, (0.05, 0.25, -0.05 + i * 0.0), 0.16, 0.012, 0.008, rot(x=80)) if False else
               sd_cylinder(p, (-0.02 + i * 0.035, 0.18, -0.03), 0.13, 0.012, 0.01, rot(z=90 - 8, y=90)), (0.98, 0.97, 0.94), 0.0)
    it.add(lambda p: sd_roundcone(p, (0.35, 0.06, 0), (0.35, 0.18, 0), 0.08, 0.09), (0.98, 0.97, 0.94), 0.0)
    it.add(lambda p: sd_torus(p, (0.45, 0.13, 0), 0.04, 0.012, rot(z=90)), (0.98, 0.97, 0.94), 0.0)


# ---------------------------------------------------------------- 침실
@F("bed", (-0.75, -0.02, -1.15), (0.75, 1.15, 1.15), 0.015, "나무 침대 (퀼트 이불)", footprint=[1.4, 2.2])
def bed(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.6, -1.03), (0.7, 0.5, 0.06), 0.05), WHITE, 1.0)
    w.add(lambda p: sd_cylinder(p, (0, 1.05, -1.03), 0.06, 0.4, 0.05, rot(x=90)), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.4, 1.05), (0.7, 0.3, 0.05), 0.05), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.2, 0), (0.68, 0.08, 1.05), 0.04), WHITE, 1.0)
    w.add(lambda p: sd_heart(p, (0, 0.85, -0.97), 0.25, 0.02, 0.015), (0.9, 0.35, 0.4), 0.0)
    mt = m.part("mattress", k=0.04)
    mt.add(lambda p: sd_roundbox(p, (0, 0.35, -0.05), (0.62, 0.1, 0.95), 0.08), (0.98, 0.97, 0.94), 0.0)
    mt.add(lambda p: sd_roundbox(p, (0, 0.5, -0.8), (0.42, 0.07, 0.16), 0.07), (0.98, 0.97, 0.94), 0.0, k=0.0)
    q = m.part("quilt", tint=(0.4, 0.6, 0.85), group="fabric", k=0.03)
    q.add(lambda p: sd_roundbox(p, (0, 0.46, 0.25), (0.66, 0.05, 0.72), 0.05), WHITE, 1.0)
    q.add(lambda p: sd_roundbox(p, (0, 0.5, -0.43), (0.66, 0.045, 0.06), 0.04), (0.98, 0.97, 0.94), 0.0)
    q.paint(lambda p: -np.maximum(np.abs(np.mod(p[:, 0] + 0.1, 0.4) - 0.2), np.abs(np.mod(p[:, 2], 0.4) - 0.2)) + 0.1 + (p[:, 1] < 0.48) * 5, (0.82, 0.82, 0.82), 1.0, soft=0.01)


@F("wardrobe", (-0.62, -0.02, -0.35), (0.62, 2.0, 0.35), 0.013, "옷장", footprint=[1.2, 0.6])
def wardrobe(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.98, 0), (0.56, 0.9, 0.3), 0.05), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 1.9, 0.0), (0.6, 0.06, 0.33), 0.04), WHITE, 1.0)
    legs(w, (-0.48, 0.48), (-0.22, 0.22), 0.0, 0.1, 0.04)
    for x in (-0.275, 0.275):
        w.cut(lambda p, x=x: sd_roundbox(p, (x, 1.0, 0.32), (0.25, 0.8, 0.03), 0.03), 0.005)
        w.add(lambda p, x=x: sd_roundbox(p, (x, 1.0, 0.29), (0.24, 0.79, 0.015), 0.02), WHITE, 1.0)
        w.add(lambda p, x=x: sd_sphere(p, (np.sign(x) * 0.05, 1.0, 0.33), 0.025), BRASS, 0.0)
        w.paint(lambda p, x=x: np.abs(np.hypot(p[:, 0] - x, (p[:, 1] - 1.0) * 0.5) - 0.16) - 0.012 + (p[:, 2] < 0.29) * 5, (0.82, 0.82, 0.82), 1.0, soft=0.005)


@F("dresser", (-0.55, -0.02, -0.3), (0.55, 0.95, 0.3), 0.01, "서랍장", footprint=[1.0, 0.5])
def dresser(m):
    w = m.part("wood", tint=(0.98, 0.88, 0.75), group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.47, 0), (0.5, 0.4, 0.25), 0.04), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 0.88, 0), (0.53, 0.03, 0.27), 0.02), WHITE, 1.0)
    legs(w, (-0.42, 0.42), (-0.17, 0.17), 0.0, 0.1, 0.035)
    for y in (0.27, 0.53, 0.77):
        w.paint(lambda p, y=y: np.abs(p[:, 1] - y + 0.12) - 0.008 + (p[:, 2] < 0.24) * 5, (0.8, 0.8, 0.8), 1.0, soft=0.004)
        for x in (-0.25, 0.25):
            w.add(lambda p, y=y, x=x: sd_sphere(p, (x, y - 0.02, 0.27), 0.025), BRASS, 0.0)


@F("nightstand_lamp", (-0.28, -0.02, -0.25), (0.28, 0.95, 0.25), 0.008, "협탁 + 버섯 램프", footprint=[0.5, 0.45])
def nightstand(m):
    w = m.part("wood", tint=WOOD, group="wood", k=0.02)
    w.add(lambda p: sd_roundbox(p, (0, 0.28, 0), (0.24, 0.25, 0.2), 0.04), WHITE, 1.0)
    w.add(lambda p: sd_sphere(p, (0, 0.3, 0.21), 0.02), BRASS, 0.0)
    l = m.part("lamp", k=0.02)
    l.add(lambda p: sd_roundcone(p, (0, 0.55, 0), (0, 0.75, 0), 0.05, 0.035), (0.98, 0.95, 0.88), 0.0)
    l.add(lambda p: sd_ellipsoid(p, (0, 0.78, 0), (0.15, 0.1, 0.15)), (0.95, 0.4, 0.38), 0.0)
    l.cut(lambda p: p[:, 1] - 0.74, 0.02)
    return dict(emissiveParts=["lamp"])
