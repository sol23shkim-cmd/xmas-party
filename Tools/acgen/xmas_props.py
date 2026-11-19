"""크리스마스 소품 (실내·실외 공용). 원점 = 바닥 중심, 앞 = +z"""
from assets import *

RED = (0.88, 0.22, 0.24)
GREEN = (0.3, 0.6, 0.38)
SNOW = (0.98, 0.98, 1.0)
GINGER = (0.78, 0.5, 0.28)
ICING = (0.99, 0.98, 0.96)
DARK = (0.15, 0.12, 0.12)


def P(name, lo, hi, step, desc, **kw):
    return asset(name, "xmas", lo, hi, step, desc, **kw)


def ginger_face(part, c, s, z):
    for x in (-1, 1):
        part.add(lambda p, x=x: sd_sphere(p, (c[0] + x * 0.06 * s, c[1] + 0.02 * s, z), 0.018 * s), DARK, 0.0, k=0)
    pts = [(c[0] + 0.06 * s * np.cos(a), c[1] - 0.03 * s + 0.035 * s * np.sin(a), z) for a in np.radians(np.linspace(200, 340, 7))]
    for a, b in zip(pts[:-1], pts[1:]):
        part.add(lambda p, a=a, b=b: sd_capsule(p, a, b, 0.009 * s), ICING, 0.0, k=0)


def gingerbread(m, female=False, s=1.0):
    g = m.part("cookie", tint=GINGER, group="cookie", k=0.04 * s)
    hc = (0, 0.78 * s, 0)
    g.add(lambda p: sd_ellipsoid(p, hc, (0.2 * s, 0.19 * s, 0.09 * s)), WHITE, 1.0)
    g.add(lambda p: sd_ellipsoid(p, (0, 0.42 * s, 0), (0.17 * s, 0.22 * s, 0.09 * s)), WHITE, 1.0)
    for x in (-1, 1):
        g.add(lambda p, x=x: sd_capsule(p, (x * 0.12 * s, 0.5 * s, 0), (x * 0.3 * s, 0.42 * s, 0), 0.075 * s), WHITE, 1.0)
        g.add(lambda p, x=x: sd_capsule(p, (x * 0.08 * s, 0.3 * s, 0), (x * 0.13 * s, 0.07 * s, 0), 0.08 * s), WHITE, 1.0)
    if female:
        g.add(lambda p: sd_cone(p, (0, 0.13 * s, 0), 0.25 * s, 0.25 * s, 0.12 * s, 0.05 * s), WHITE, 1.0)
    ic = m.part("icing", k=0.0)
    ginger_face(ic, hc, s, 0.09 * s)
    for x in (-1, 1):
        ic.add(lambda p, x=x: sd_capsule(p, (x * 0.24 * s, 0.47 * s, 0.07 * s), (x * 0.3 * s, 0.4 * s, 0.07 * s), 0.012 * s), ICING, 0.0)
        ic.add(lambda p, x=x: sd_capsule(p, (x * 0.09 * s, 0.11 * s, 0.07 * s), (x * 0.15 * s, 0.1 * s, 0.07 * s), 0.012 * s), ICING, 0.0)
    for y in (0.5, 0.38):
        ic.add(lambda p, y=y: sd_sphere(p, (0, y * s, 0.085 * s), 0.028 * s), RED if y > 0.4 else GREEN, 0.0)
    if female:
        for x in (-1, 1):
            ic.add(lambda p, x=x: sd_ellipsoid(p, (x * 0.18 * s, 0.93 * s, 0.02 * s), (0.06 * s, 0.04 * s, 0.03 * s), rot(z=x * 30)), (0.95, 0.45, 0.6), 0.0)
        ic.add(lambda p: sd_sphere(p, (0, 0.93 * s, 0.05 * s), 0.03 * s), (0.95, 0.45, 0.6), 0.0)
        ic.add(lambda p: sd_torus(p, (0, 0.2 * s, 0), 0.19 * s, 0.012 * s), ICING, 0.0)
    else:
        ic.add(lambda p: sd_capsule(p, (-0.07 * s, 0.6 * s, 0.075 * s), (0.07 * s, 0.6 * s, 0.075 * s), 0.02 * s), RED, 0.0)


@P("gingerbread_man", (-0.42, -0.02, -0.15), (0.42, 1.02, 0.18), 0.009, "진저브레드맨 (큰 쿠키 인형)")
def gman(m): gingerbread(m)


@P("gingerbread_woman", (-0.42, -0.02, -0.3), (0.42, 1.02, 0.3), 0.009, "진저브레드 아가씨")
def gwoman(m): gingerbread(m, female=True)


def snowman(m, s=1.0, hat="top", scarf_t=RED, arms=True):
    b = m.part("snow", k=0.03 * s)
    b.add(lambda p: sd_sphere(p, (0, 0.36 * s, 0), 0.38 * s), SNOW, 0.0)
    b.add(lambda p: sd_sphere(p, (0, 0.92 * s, 0), 0.28 * s), SNOW, 0.0)
    b.add(lambda p: sd_sphere(p, (0, 1.33 * s, 0), 0.22 * s), SNOW, 0.0)
    b.paint(lambda p: sd_ellipsoid(p, (0, 1.27 * s, 0.17 * s), (0.08 * s, 0.05 * s, 0.05 * s)), (0.99, 0.82, 0.85), 0.0, soft=0.05 * s)
    f = m.part("face", k=0.0)
    for x in (-1, 1):
        f.add(lambda p, x=x: sd_sphere(p, (x * 0.075 * s, 1.38 * s, 0.195 * s), 0.025 * s), DARK, 0.0)
        f.add(lambda p, x=x: sd_ellipsoid(p, (x * 0.13 * s, 1.28 * s, 0.16 * s), (0.04 * s, 0.025 * s, 0.03 * s)), (0.99, 0.7, 0.72), 0.0)
    f.add(lambda p: sd_roundcone(p, (0, 1.32 * s, 0.2 * s), (0, 1.3 * s, 0.38 * s), 0.035 * s, 0.008 * s), (0.98, 0.55, 0.2), 0.0)
    for i in range(5):
        a = np.radians(220 + i * 25)
        f.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.1 * s, 1.27 * s + np.sin(a) * 0.06 * s + 0.06 * s, 0.19 * s), 0.014 * s), DARK, 0.0)
    for y in (0.98, 0.84, 0.6):
        z = np.sqrt(max((0.28 if y > 0.7 else 0.38) ** 2 - (y - (0.92 if y > 0.7 else 0.36)) ** 2, 0)) * s
        f.add(lambda p, y=y, z=z: sd_sphere(p, (0, y * s, z), 0.03 * s), DARK, 0.0)
    sc = m.part("scarf", tint=scarf_t, group="scarf", k=0.02 * s)
    sc.add(lambda p: sd_torus(p, (0, 1.14 * s, 0), 0.2 * s, 0.055 * s), WHITE, 1.0)
    sc.add(lambda p: sd_roundbox(p, (0.12 * s, 1.0 * s, 0.2 * s), (0.055 * s, 0.13 * s, 0.025 * s), 0.02 * s, rot(z=10, x=-15)), WHITE, 1.0)
    sc.paint(lambda p: np.abs(np.sin(p[:, 0] * 50 / s + p[:, 1] * 50 / s)) - 0.4, (0.98, 0.97, 0.95), 0.0, soft=0.3)
    h = m.part("hat", tint=(0.22, 0.2, 0.22) if hat == "top" else RED, group="hat", k=0.02 * s)
    if hat == "top":
        h.add(lambda p: sd_cylinder(p, (0, 1.5 * s, 0), 0.015 * s, 0.24 * s, 0.01 * s, rot(z=-8)), WHITE, 1.0)
        h.add(lambda p: sd_cylinder(p, (0.015 * s, 1.63 * s, 0), 0.13 * s, 0.15 * s, 0.03 * s, rot(z=-8)), WHITE, 1.0)
        h.add(lambda p: sd_cylinder(p, (0.006 * s, 1.54 * s, 0), 0.03 * s, 0.155 * s, 0.01 * s, rot(z=-8)), RED, 0.0)
    elif hat == "bucket":
        h.add(lambda p: sd_cone(p, (0, 1.47 * s, 0), 0.22 * s, 0.17 * s, 0.13 * s, 0.02 * s, rot(z=-12)), WHITE, 1.0)
    else:   # 털모자
        h.add(lambda p: sd_ellipsoid(p, (0, 1.42 * s, 0), (0.22 * s, 0.17 * s, 0.22 * s)), WHITE, 1.0)
        h.cut(lambda p: p[:, 1] - 1.44 * s, 0.0)
        h.add(lambda p: sd_torus(p, (0, 1.45 * s, 0), 0.215 * s, 0.04 * s), (0.98, 0.97, 0.95), 0.0)
        h.add(lambda p: sd_sphere(p, (0, 1.64 * s, 0), 0.06 * s), (0.98, 0.97, 0.95), 0.0)
    if arms:
        w = m.part("arms", k=0.01 * s)
        for x in (-1, 1):
            a0 = np.array([x * 0.22 * s, 0.98 * s, 0]); a1 = a0 + [x * 0.3 * s, 0.18 * s, 0]
            w.add(lambda p, a0=a0, a1=a1: sd_capsule(p, a0, a1, 0.018 * s), (0.5, 0.33, 0.2), 0.0)
            w.add(lambda p, a1=a1, x=x: sd_capsule(p, a1 - [x * 0.1 * s, 0.03 * s, 0], a1 - [x * 0.03 * s, -0.1 * s, 0], 0.014 * s), (0.5, 0.33, 0.2), 0.0)


@P("snowman_big", (-0.85, -0.02, -0.45), (0.85, 1.85, 0.5), 0.014, "큰 눈사람 (실크햇)")
def sm1(m): snowman(m)


@P("snowman_bucket", (-0.85, -0.02, -0.45), (0.85, 1.85, 0.5), 0.014, "눈사람 (양동이 모자)")
def sm2(m): snowman(m, hat="bucket", scarf_t=(0.3, 0.55, 0.85))


@P("snowman_small", (-0.45, -0.02, -0.25), (0.45, 1.0, 0.3), 0.008, "작은 눈사람 (털모자)")
def sm3(m): snowman(m, s=0.55, hat="beanie", scarf_t=GREEN)


@P("gift_box", (-0.3, -0.02, -0.3), (0.3, 0.62, 0.3), 0.008, "선물 상자 (정육면체)")
def gift1(m):
    b = m.part("box", tint=RED, group="box", k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, 0.2, 0), (0.25, 0.2, 0.25), 0.03), WHITE, 1.0)
    b.add(lambda p: sd_roundbox(p, (0, 0.42, 0), (0.27, 0.04, 0.27), 0.02), WHITE, 1.0)
    r = m.part("ribbon", tint=(0.98, 0.84, 0.35), group="ribbon", gloss=0.5, k=0.01)
    r.add(lambda p: sd_roundbox(p, (0, 0.235, 0), (0.06, 0.235, 0.275), 0.01), WHITE, 1.0)
    r.add(lambda p: sd_roundbox(p, (0, 0.235, 0), (0.275, 0.235, 0.06), 0.01), WHITE, 1.0)
    for a in (45, 135):
        r.add(lambda p, a=a: sd_torus(p, (0, 0.52, 0), 0.07, 0.025, rot(y=a, x=90)), WHITE, 1.0)
    r.add(lambda p: sd_sphere(p, (0, 0.48, 0), 0.04), WHITE, 1.0)


@P("gift_tall", (-0.22, -0.02, -0.22), (0.22, 0.75, 0.22), 0.007, "길쭉한 선물 상자 (물방울)")
def gift2(m):
    b = m.part("box", tint=(0.3, 0.6, 0.85), group="box", k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, 0.28, 0), (0.17, 0.28, 0.17), 0.025), WHITE, 1.0)
    pts = np.array([[x, y, z] for x in (-0.17, 0.17) for y in (0.1, 0.25, 0.4) for z in (-0.08, 0.08)] +
                   [[x, y, z] for z in (-0.17, 0.17) for y in (0.17, 0.33, 0.48) for x in (-0.08, 0.08)])
    b.paint(lambda p: np.min(np.linalg.norm(p[:, None] - pts[None], axis=2), axis=1) - 0.03, (0.98, 0.97, 0.95), 0.0, soft=0.004)
    r = m.part("ribbon", tint=(0.98, 0.97, 0.95), group="ribbon", k=0.01)
    r.add(lambda p: sd_roundbox(p, (0, 0.3, 0), (0.04, 0.3, 0.18), 0.008), WHITE, 1.0)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.07, 0.6, 0), (0.08, 0.04, 0.03), rot(z=s * 30)), WHITE, 1.0)


@P("gift_flat", (-0.35, -0.02, -0.25), (0.35, 0.35, 0.25), 0.008, "납작한 선물 상자 (줄무늬)")
def gift3(m):
    b = m.part("box", tint=GREEN, group="box", k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, 0.11, 0), (0.3, 0.11, 0.2), 0.025), WHITE, 1.0)
    b.paint(lambda p: np.abs(np.sin((p[:, 0] + p[:, 2]) * 30)) - 0.5, (0.98, 0.97, 0.95), 0.0, soft=0.2)
    r = m.part("ribbon", tint=RED, group="ribbon", k=0.01)
    r.add(lambda p: sd_roundbox(p, (0.1, 0.12, 0), (0.035, 0.125, 0.205), 0.008), WHITE, 1.0)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_ellipsoid(p, (0.1 + s * 0.06, 0.26, 0), (0.07, 0.035, 0.03), rot(z=s * 25)), WHITE, 1.0)
    r.add(lambda p: sd_sphere(p, (0.1, 0.24, 0), 0.03), WHITE, 1.0)


@P("gift_pile", (-0.7, -0.02, -0.55), (0.7, 0.75, 0.6), 0.012, "선물 더미")
def gift_pile(m):
    specs = [((-0.3, 0, -0.1), (0.25, 0.2, 0.22), 15, RED, (0.98, 0.84, 0.35)), ((0.28, 0, -0.15), (0.2, 0.28, 0.2), -20, (0.3, 0.6, 0.85), (0.98, 0.97, 0.95)),
             ((0.0, 0, 0.3), (0.3, 0.12, 0.2), 5, GREEN, RED), ((-0.25, 0.4, -0.1), (0.15, 0.12, 0.15), 40, (0.95, 0.8, 0.35), RED), ((0.45, 0, 0.3), (0.12, 0.12, 0.12), 30, (0.7, 0.45, 0.8), (0.98, 0.97, 0.95))]
    for i, (c, h, ry, col, rc) in enumerate(specs):
        cc = (c[0], c[1] + h[1], c[2]); R = rot(y=ry)
        b = m.part(f"box{i}", k=0.01)
        b.add(lambda p, cc=cc, h=h, R=R: sd_roundbox(p, cc, h, 0.025, R), col, 0.0)
        b.add(lambda p, cc=cc, h=h, R=R: sd_roundbox(p, cc, (0.04, h[1] + 0.006, h[2] + 0.006), 0.008, R), rc, 0.0)
        b.add(lambda p, cc=cc, h=h, R=R: sd_roundbox(p, cc, (h[0] + 0.006, h[1] + 0.006, 0.04), 0.008, R), rc, 0.0)
        top = np.array(cc) + [0, h[1] + 0.03, 0]
        b.add(lambda p, top=top: sd_sphere(p, top, 0.035), rc, 0.0)


@P("candy_cane_big", (-0.3, -0.02, -0.1), (0.3, 1.25, 0.1), 0.008, "큰 지팡이 사탕 (바닥 장식)")
def cane_big(m):
    c = m.part("cane", tint=RED, group="stripe", gloss=0.6, k=0.01)
    pts = [(0.1, 0.0, 0)] + [(0.1 * np.cos(a) - 0.0, 1.0 + 0.1 * np.sin(a), 0) for a in np.radians(np.linspace(0, 180, 12))] + [(-0.1, 0.85, 0)]
    pts[0] = (0.1, 0.04, 0)
    for a, b in zip(pts[:-1], pts[1:]):
        c.add(lambda p, a=a, b=b: sd_capsule(p, a, b, 0.045), ICING, 0.0)
    c.paint(lambda p: np.abs(np.sin((p[:, 1] - p[:, 0] * 0.7) * 22)) - 0.5, WHITE, 1.0, soft=0.25)


@P("stocking", (-0.25, -0.62, -0.1), (0.3, 0.05, 0.12), 0.007, "벽난로 양말 (원점=거는 곳)", hang=[0, 0, 0])
def stocking(m):
    s = m.part("sock", tint=RED, group="sock", k=0.05)
    s.add(lambda p: sd_capsule(p, (0, -0.14, 0), (0, -0.42, 0), 0.1), WHITE, 1.0)
    s.add(lambda p: sd_capsule(p, (0, -0.44, 0), (0.14, -0.48, 0), 0.1), WHITE, 1.0)
    for c in ((0.17, -0.5, 0), (-0.04, -0.48, 0)):
        s.paint(lambda p, c=c: sd_sphere(p, c, 0.08), ICING, 0.0, soft=0.02)
    s.paint(lambda p: np.abs(p[:, 1] + 0.3) - 0.025, (0.3, 0.6, 0.38), 0.0, soft=0.005)
    c = m.part("cuff", k=0.02)
    c.add(lambda p: sd_cylinder(p, (0, -0.08, 0), 0.06, 0.12, 0.05), ICING, 0.0)
    c.add(lambda p: sd_torus(p, (-0.06, 0.0, 0), 0.03, 0.01, rot(x=90)), (0.9, 0.75, 0.4), 0.0)


@P("wreath", (-0.42, -0.42, -0.1), (0.42, 0.42, 0.14), 0.008, "리스 (원점=중심, 벽걸이)", wall=True)
def wreath(m):
    w = m.part("leaves", tint=GREEN, group="leaves", k=0.03)
    for i in range(22):
        a = np.radians(i * 360 / 22)
        c = (np.cos(a) * 0.27, np.sin(a) * 0.27, 0)
        w.add(lambda p, c=c, a=a: sd_ellipsoid(p, c, (0.09, 0.05, 0.06), rot(z=np.degrees(a) + 60 + (i % 2) * 40)), WHITE, 1.0)
    w.add(lambda p: sd_torus(p, (0, 0, -0.01), 0.27, 0.08, rot(x=90)), WHITE, 1.0)
    b = m.part("berries", k=0.0)
    for i in range(9):
        a = np.radians(i * 40 + 10)
        b.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.3, np.sin(a) * 0.3, 0.07), 0.028), RED, 0.0)
    for i in range(6):
        a = np.radians(i * 60 + 35)
        b.add(lambda p, a=a: sd_ellipsoid(p, (np.cos(a) * 0.24, np.sin(a) * 0.24, 0.06), (0.035, 0.045, 0.03)), (0.6, 0.4, 0.25), 0.0)
    r = m.part("bow", tint=RED, group="bow", gloss=0.4, k=0.015)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_torus(p, (s * 0.08, -0.25, 0.08), 0.06, 0.025, rot(x=90, z=s * 12)), WHITE, 1.0)
        r.add(lambda p, s=s: sd_roundbox(p, (s * 0.05, -0.36, 0.08), (0.03, 0.09, 0.01), 0.01, rot(z=s * 20)), WHITE, 1.0)
    r.add(lambda p: sd_sphere(p, (0, -0.25, 0.09), 0.04), WHITE, 1.0)


@P("garland", (-1.25, -0.4, -0.1), (1.25, 0.12, 0.16), 0.012, "늘어진 가랜드 (벽/벽난로, 원점=양끝 높이 중심)", wall=True)
def garland(m):
    g = m.part("leaves", tint=GREEN, group="leaves", k=0.05)
    xs = np.linspace(-1.15, 1.15, 26)
    for i, x in enumerate(xs):
        y = -0.25 * (1 - (x / 1.15) ** 2)
        g.add(lambda p, x=x, y=y, i=i: sd_ellipsoid(p, (x, y, 0), (0.08, 0.06, 0.07), rot(z=(i % 3) * 40, x=20)), WHITE, 1.0)
    b = m.part("lights", k=0.0)
    for i, x in enumerate(np.linspace(-1.05, 1.05, 12)):
        y = -0.25 * (1 - (x / 1.15) ** 2) - 0.03
        col = [RED, (0.98, 0.84, 0.35), (0.3, 0.6, 0.9), (0.4, 0.8, 0.45)][i % 4]
        b.add(lambda p, x=x, y=y: sd_ellipsoid(p, (x, y - 0.03, 0.08), (0.025, 0.04, 0.025)), col, 0.0)
    for x in (-1.15, 1.15):
        b.add(lambda p, x=x: sd_sphere(p, (x, 0.02, 0.02), 0.06), RED, 0.0)
    return dict(emissiveParts=["lights"])


@P("string_lights", (-1.3, -0.25, -0.05), (1.3, 0.05, 0.08), 0.008, "전구 줄 (원점=양끝 높이)", wall=True)
def string_lights(m):
    w = m.part("wire", k=0.0)
    xs = np.linspace(-1.2, 1.2, 40)
    ys = -0.15 * (1 - (xs / 1.2) ** 2)
    for i in range(len(xs) - 1):
        w.add(lambda p, i=i: sd_capsule(p, (xs[i], ys[i], 0), (xs[i + 1], ys[i + 1], 0), 0.006), (0.2, 0.35, 0.25), 0.0)
    b = m.part("bulbs", gloss=1.0, k=0.0)
    for i, x in enumerate(np.linspace(-1.1, 1.1, 14)):
        y = -0.15 * (1 - (x / 1.2) ** 2)
        col = [RED, (0.98, 0.84, 0.35), (0.3, 0.6, 0.9), (0.4, 0.8, 0.45), (0.95, 0.5, 0.75)][i % 5]
        b.add(lambda p, x=x, y=y: sd_roundcone(p, (x, y - 0.03, 0.01), (x, y - 0.075, 0.01), 0.022, 0.012), col, 0.0)
    return dict(emissiveParts=["bulbs"])


@P("bell_pair", (-0.3, -0.45, -0.12), (0.3, 0.1, 0.16), 0.006, "쌍방울 종 (리본, 원점=거는 곳)", hang=[0, 0, 0])
def bell_pair(m):
    b = m.part("bells", tint=(0.98, 0.78, 0.32), group="metal", gloss=1.0, k=0.01)
    for s in (-1, 1):
        c = np.array([s * 0.1, -0.25, 0])
        R = rot(z=s * 15)
        b.add(lambda p, c=c, R=R: sd_cone(p, c, 0.16, 0.1, 0.045, 0.02, R), WHITE, 1.0)
        b.add(lambda p, c=c, R=R: sd_sphere(p, c + R @ [0, 0.15, 0], 0.05), WHITE, 1.0)
        b.add(lambda p, c=c: sd_sphere(p, c - [0, 0.02, 0], 0.03), WHITE, 1.0)
    r = m.part("ribbon", tint=RED, group="ribbon", k=0.015)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_torus(p, (s * 0.06, -0.06, 0.04), 0.05, 0.02, rot(x=90, z=s * 15)), WHITE, 1.0)
    r.add(lambda p: sd_sphere(p, (0, -0.06, 0.05), 0.035), WHITE, 1.0)
    g = m.part("holly", k=0.01)
    for s in (-1, 1):
        g.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.07, -0.0, 0.03), (0.06, 0.025, 0.01), rot(z=s * 20)), GREEN, 0.0)


@P("nutcracker", (-0.3, -0.02, -0.25), (0.3, 1.35, 0.25), 0.008, "호두까기 인형 병정")
def nutcracker(m):
    b = m.part("coat", tint=RED, group="coat", k=0.02)
    b.add(lambda p: sd_roundbox(p, (0, 0.62, 0), (0.15, 0.18, 0.12), 0.06), WHITE, 1.0)
    for s in (-1, 1):
        b.add(lambda p, s=s: sd_capsule(p, (s * 0.17, 0.74, 0), (s * 0.2, 0.5, 0.03), 0.05), WHITE, 1.0)
    lg = m.part("legs", tint=(0.2, 0.25, 0.45), group="pants", k=0.02)
    for s in (-1, 1):
        lg.add(lambda p, s=s: sd_capsule(p, (s * 0.07, 0.1, 0), (s * 0.07, 0.45, 0), 0.06), WHITE, 1.0)
        lg.add(lambda p, s=s: sd_roundbox(p, (s * 0.07, 0.05, 0.03), (0.06, 0.05, 0.09), 0.03), (0.15, 0.12, 0.12), 0.0)
    h = m.part("head", k=0.02)
    h.add(lambda p: sd_cylinder(p, (0, 0.92, 0), 0.12, 0.13, 0.05), (1.0, 0.88, 0.78), 0.0)
    h.paint(lambda p: (p[:, 1] - 0.865) + (p[:, 2] < 0.02) * 5 + (p[:, 1] > 1.0) * 5, (0.98, 0.98, 0.98), 0.0, soft=0.01)
    h.add(lambda p: sd_cylinder(p, (0, 1.14, 0), 0.12, 0.15, 0.05), (0.18, 0.15, 0.17), 0.0)
    h.add(lambda p: sd_sphere(p, (0, 1.29, 0.0), 0.04), (0.98, 0.8, 0.3), 0.0)
    h.add(lambda p: sd_cylinder(p, (0, 1.02, 0.0), 0.02, 0.15, 0.01), (0.98, 0.8, 0.3), 0.0)
    f = m.part("face", k=0.0)
    for s in (-1, 1):
        f.add(lambda p, s=s: sd_sphere(p, (s * 0.045, 0.96, 0.125), 0.016), DARK, 0.0)
        f.add(lambda p, s=s: sd_sphere(p, (s * 0.07, 0.9, 0.12), 0.022), (0.99, 0.65, 0.65), 0.0)
    f.add(lambda p: sd_sphere(p, (0, 0.92, 0.14), 0.02), (1.0, 0.8, 0.7), 0.0)
    f.add(lambda p: sd_capsule(p, (-0.05, 0.875, 0.13), (0.05, 0.875, 0.13), 0.012), DARK, 0.0)
    d = m.part("deco", k=0.01)
    d.add(lambda p: sd_roundbox(p, (0, 0.47, 0.0), (0.155, 0.025, 0.125), 0.02), (0.15, 0.12, 0.12), 0.0)
    d.add(lambda p: sd_roundbox(p, (0, 0.47, 0.13), (0.035, 0.03, 0.01), 0.008), (0.98, 0.8, 0.3), 0.0)
    for y in (0.58, 0.66, 0.74):
        for s in (-1, 1):
            d.add(lambda p, y=y, s=s: sd_sphere(p, (s * 0.06, y, 0.165), 0.014), (0.98, 0.8, 0.3), 0.0)


@P("toy_train", (-0.6, -0.02, -0.25), (0.6, 0.55, 0.25), 0.008, "장난감 기차")
def toy_train(m):
    e = m.part("engine", tint=RED, group="body", gloss=0.5, k=0.015)
    e.add(lambda p: sd_cylinder(p, (0.15, 0.25, 0), 0.22, 0.12, 0.03, rot(z=90)), WHITE, 1.0)
    e.add(lambda p: sd_roundbox(p, (-0.15, 0.3, 0), (0.12, 0.17, 0.14), 0.03), WHITE, 1.0)
    e.add(lambda p: sd_roundbox(p, (-0.15, 0.49, 0), (0.15, 0.025, 0.17), 0.02), (0.2, 0.2, 0.22), 0.0)
    e.add(lambda p: sd_cone(p, (0.25, 0.35, 0), 0.15, 0.04, 0.06, 0.01), (0.2, 0.2, 0.22), 0.0)
    e.add(lambda p: sd_roundbox(p, (0.0, 0.11, 0), (0.33, 0.03, 0.13), 0.02), (0.2, 0.2, 0.22), 0.0)
    wl = m.part("wheels", tint=(0.98, 0.8, 0.3), group="trim", gloss=0.8, k=0.0)
    for x in (-0.2, 0.0, 0.22):
        for z in (-0.13, 0.13):
            wl.add(lambda p, x=x, z=z: sd_cylinder(p, (x, 0.08, z), 0.02, 0.075 if x < 0 else 0.06, 0.01, rot(x=90)), WHITE, 1.0)
    wl.add(lambda p: sd_cylinder(p, (0.375, 0.25, 0), 0.01, 0.08, 0.008, rot(z=90)), WHITE, 1.0)
    win = m.part("window", k=0.0)
    for z in (-0.145, 0.145):
        win.add(lambda p, z=z: sd_roundbox(p, (-0.15, 0.36, z), (0.06, 0.06, 0.006), 0.01), (0.7, 0.85, 0.95), 0.0)


@P("teddy_bear", (-0.3, -0.02, -0.3), (0.3, 0.6, 0.3), 0.007, "곰 인형 (앉은 자세)")
def teddy(m):
    t = m.part("fur", tint=(0.75, 0.52, 0.32), group="fur", k=0.03)
    t.add(lambda p: sd_ellipsoid(p, (0, 0.17, 0), (0.15, 0.16, 0.13)), WHITE, 1.0)
    t.add(lambda p: sd_sphere(p, (0, 0.42, 0.01), 0.14), WHITE, 1.0)
    for s in (-1, 1):
        t.add(lambda p, s=s: sd_sphere(p, (s * 0.1, 0.53, -0.01), 0.05), WHITE, 1.0)
        t.add(lambda p, s=s: sd_capsule(p, (s * 0.12, 0.24, 0.02), (s * 0.18, 0.12, 0.08), 0.05), WHITE, 1.0)
        t.add(lambda p, s=s: sd_capsule(p, (s * 0.08, 0.06, 0.05), (s * 0.1, 0.05, 0.2), 0.06), WHITE, 1.0)
        t.paint(lambda p, s=s: sd_sphere(p, (s * 0.1, 0.05, 0.25), 0.04), (0.98, 0.85, 0.7), 0.0, soft=0.01)
    t.add(lambda p: sd_ellipsoid(p, (0, 0.39, 0.12), (0.06, 0.045, 0.04)), (0.98, 0.88, 0.75), 0.0)
    f = m.part("face", k=0.0)
    for s in (-1, 1):
        f.add(lambda p, s=s: sd_sphere(p, (s * 0.055, 0.45, 0.125), 0.016), DARK, 0.0)
    f.add(lambda p: sd_ellipsoid(p, (0, 0.405, 0.158), (0.018, 0.013, 0.01)), DARK, 0.0)
    bw = m.part("bow", tint=RED, group="bow", k=0.01)
    for s in (-1, 1):
        bw.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.05, 0.3, 0.12), (0.05, 0.03, 0.02), rot(z=s * 15)), WHITE, 1.0)
    bw.add(lambda p: sd_sphere(p, (0, 0.3, 0.13), 0.02), WHITE, 1.0)


@P("sled", (-0.45, -0.02, -0.8), (0.45, 0.6, 0.8), 0.01, "나무 썰매")
def sled(m):
    w = m.part("wood", tint=(0.72, 0.42, 0.26), group="wood", k=0.015)
    w.add(lambda p: sd_roundbox(p, (0, 0.28, 0), (0.32, 0.025, 0.6), 0.02), WHITE, 1.0)
    w.paint(lambda p: np.abs(np.mod(p[:, 0] + 0.32, 0.16) - 0.08) - 0.072 + (p[:, 1] < 0.29) * 5, (0.8, 0.8, 0.8), 1.0, soft=0.004)
    for x in (-0.28, 0.28):
        for z in (-0.4, 0.0, 0.4):
            w.add(lambda p, x=x, z=z: sd_capsule(p, (x, 0.05, z), (x, 0.27, z), 0.025), WHITE, 1.0)
    r = m.part("runner", tint=RED, group="runner", gloss=0.6, k=0.01)
    for x in (-0.3, 0.3):
        pts = [(x, 0.04, -0.6), (x, 0.04, 0.55)] + [(x, 0.2 + 0.16 * np.cos(a), 0.6 + 0.16 * np.sin(a)) for a in np.radians(np.linspace(-90, 120, 9))]
        pts[2] = (x, 0.04, 0.6)
        for a, b in zip(pts[:-1], pts[1:]):
            r.add(lambda p, a=a, b=b: sd_capsule(p, a, b, 0.03), WHITE, 1.0)


@P("cookie_plate", (-0.25, -0.02, -0.25), (0.25, 0.16, 0.25), 0.005, "쿠키 접시 + 우유")
def cookie_plate(m):
    pl = m.part("plate", k=0.01)
    pl.add(lambda p: sd_cone(p, (0, 0.0, 0), 0.025, 0.14, 0.18, 0.008), (0.98, 0.97, 0.94), 0.0)
    pl.paint(lambda p: np.abs(np.hypot(p[:, 0], p[:, 2]) - 0.165) - 0.008, (0.4, 0.6, 0.85), 0.0, soft=0.003)
    ck = m.part("cookies", tint=(0.85, 0.6, 0.35), group="cookie", k=0.005)
    for i, (x, z, a) in enumerate([(-0.05, 0.03, 0), (0.04, -0.04, 30), (0.02, 0.06, 60), (-0.02, -0.01, 15)]):
        y = 0.035 + i * 0.015
        ck.add(lambda p, x=x, z=z, y=y: sd_cylinder(p, (x, y, z), 0.008, 0.055, 0.006), WHITE, 1.0)
        for j in range(3):
            b = np.radians(a + j * 120)
            ck.add(lambda p, x=x, z=z, y=y, b=b: sd_sphere(p, (x + np.cos(b) * 0.025, y + 0.008, z + np.sin(b) * 0.025), 0.008), (0.35, 0.2, 0.15), 0.0, k=0)
    g = m.part("milk", gloss=0.8, k=0.005)
    g.add(lambda p: sd_cylinder(p, (0.13, 0.08, -0.1), 0.075, 0.035, 0.008), (0.98, 0.98, 0.98), 0.0)


@P("cocoa_mug", (-0.1, -0.02, -0.1), (0.13, 0.2, 0.1), 0.0035, "코코아 머그 (마시멜로)")
def cocoa(m):
    c = m.part("mug", tint=RED, group="mug", gloss=0.7, k=0.008)
    c.add(lambda p: sd_cylinder(p, (0, 0.06, 0), 0.06, 0.06, 0.012), WHITE, 1.0)
    c.cut(lambda p: sd_cylinder(p, (0, 0.11, 0), 0.04, 0.05), 0.005)
    c.add(lambda p: sd_torus(p, (0.065, 0.065, 0), 0.03, 0.01, rot(x=90)), WHITE, 1.0)
    c.paint(lambda p: np.abs(p[:, 1] - 0.06) - 0.01, (0.98, 0.97, 0.94), 0.0, soft=0.003)
    k = m.part("cocoa", k=0.008)
    k.add(lambda p: sd_cylinder(p, (0, 0.095, 0), 0.006, 0.052), (0.45, 0.28, 0.2), 0.0)
    for x, z in ((-0.015, 0.01), (0.015, -0.01), (0.005, 0.02)):
        k.add(lambda p, x=x, z=z: sd_roundbox(p, (x, 0.11, z), (0.012, 0.012, 0.012), 0.006), (0.99, 0.98, 0.97), 0.0)


@P("candle_set", (-0.25, -0.02, -0.15), (0.25, 0.48, 0.15), 0.005, "양초 3개 + 호랑가시", light=[0, 0.4, 0])
def candles(m):
    w = m.part("wax", tint=(0.98, 0.95, 0.88), group="wax", k=0.005)
    fl = m.part("flame", k=0.01)
    for x, h in ((-0.12, 0.2), (0.0, 0.3), (0.12, 0.15)):
        w.add(lambda p, x=x, h=h: sd_cylinder(p, (x, h / 2 + 0.02, 0), h / 2, 0.045, 0.01), WHITE, 1.0)
        w.add(lambda p, x=x, h=h: sd_ellipsoid(p, (x + 0.03, h - 0.02, 0.035), (0.012, 0.03, 0.012)), WHITE, 1.0)
        fl.add(lambda p, x=x, h=h: sd_roundcone(p, (x, h + 0.05, 0), (x, h + 0.11, 0), 0.02, 0.004), (1.0, 0.75, 0.3), 0.0)
        fl.add(lambda p, x=x, h=h: sd_capsule(p, (x, h + 0.02, 0), (x, h + 0.045, 0), 0.004), (0.2, 0.15, 0.1), 0.0)
    b = m.part("base", k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, 0.01, 0), (0.22, 0.015, 0.1), 0.01), (0.92, 0.75, 0.4), 0.0)
    for i in range(6):
        a = np.radians(i * 60 + 20)
        b.add(lambda p, a=a: sd_ellipsoid(p, (np.cos(a) * 0.16, 0.035, np.sin(a) * 0.08), (0.05, 0.012, 0.025), rot(y=-np.degrees(a))), GREEN, 0.0)
    for x in (-0.06, 0.06, 0.18):
        b.add(lambda p, x=x: sd_sphere(p, (x, 0.04, 0.08), 0.015), RED, 0.0)
    return dict(emissiveParts=["flame"])


@P("poinsettia", (-0.35, -0.02, -0.35), (0.35, 0.65, 0.35), 0.007, "포인세티아 화분")
def poinsettia(m):
    pot = m.part("pot", tint=(0.95, 0.8, 0.35), group="pot", gloss=0.6, k=0.01)
    pot.add(lambda p: sd_cone(p, (0, 0, 0), 0.28, 0.13, 0.17, 0.02), WHITE, 1.0)
    pot.add(lambda p: sd_torus(p, (0, 0.27, 0), 0.17, 0.025), WHITE, 1.0)
    pot.add(lambda p: sd_cylinder(p, (0, 0.27, 0), 0.01, 0.16), (0.4, 0.28, 0.2), 0.0)
    lv = m.part("bracts", tint=RED, group="petal", k=0.02)
    gr = m.part("leaves", k=0.02)
    rng = np.random.default_rng(2)
    for layer, (rr, y, n, col) in enumerate([(0.2, 0.36, 7, None), (0.14, 0.44, 6, None), (0.07, 0.5, 5, None)]):
        for i in range(n):
            a = 2 * np.pi * i / n + layer * 0.4
            c = (np.cos(a) * rr, y + rng.uniform(-0.02, 0.02), np.sin(a) * rr)
            lv.add(lambda p, c=c, a=a, layer=layer: sd_ellipsoid(p, c, (0.1 - layer * 0.02, 0.015, 0.045), rot(y=-np.degrees(a), z=12 + layer * 10)), WHITE, 1.0)
    for i in range(8):
        a = 2 * np.pi * i / 8 + 0.2
        gr.add(lambda p, a=a: sd_ellipsoid(p, (np.cos(a) * 0.24, 0.32, np.sin(a) * 0.24), (0.09, 0.012, 0.04), rot(y=-np.degrees(a), z=-10)), GREEN, 0.0)
    for i in range(5):
        a = 2 * np.pi * i / 5
        gr.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.02, 0.53, np.sin(a) * 0.02), 0.012), (0.98, 0.85, 0.3), 0.0)


@P("mistletoe", (-0.2, -0.5, -0.2), (0.2, 0.05, 0.2), 0.005, "겨우살이 (천장에 매다는 것, 원점=거는 곳)", hang=[0, 0, 0])
def mistletoe(m):
    r = m.part("string", k=0.0)
    r.add(lambda p: sd_capsule(p, (0, 0.0, 0), (0, -0.2, 0), 0.006), RED, 0.0)
    l = m.part("leaves", tint=(0.45, 0.68, 0.4), group="leaves", k=0.01)
    for i in range(8):
        a = np.radians(i * 45)
        l.add(lambda p, a=a: sd_ellipsoid(p, (np.cos(a) * 0.08, -0.3 - (i % 2) * 0.03, np.sin(a) * 0.08), (0.09, 0.02, 0.035), rot(y=-np.degrees(a), z=30)), WHITE, 1.0)
    b = m.part("berries", k=0.0)
    for i in range(7):
        a = np.radians(i * 51)
        b.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.03, -0.27 + (i % 3) * 0.01, np.sin(a) * 0.03), 0.017), (0.98, 0.98, 0.94), 0.0)
    w = m.part("bow", tint=RED, group="bow", k=0.01)
    for s in (-1, 1):
        w.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.04, -0.2, 0), (0.04, 0.025, 0.015), rot(z=s * 15)), WHITE, 1.0)


@P("holly_sprig", (-0.15, -0.02, -0.1), (0.15, 0.08, 0.1), 0.003, "호랑가시 잎 장식")
def holly(m):
    l = m.part("leaves", tint=GREEN, group="leaves", gloss=0.6, k=0.005)
    for s in (-1, 1):
        def leaf(p, s=s):
            q = local(p, (s * 0.06, 0.025, 0), rot(y=s * 20, z=-s * 15))
            x = q[:, 0]; ww = 0.03 * np.sqrt(np.maximum(1 - (x / 0.07) ** 2, 0)) * (1 + 0.25 * np.cos(x * 130))
            return np.maximum(np.maximum(np.abs(q[:, 2]) - ww, np.abs(x) - 0.07), np.abs(q[:, 1]) - 0.005) - 0.003
        l.add(leaf, WHITE, 1.0)
    b = m.part("berries", gloss=0.8, k=0.0)
    for x, z in ((0, 0.01), (-0.017, -0.012), (0.017, -0.012)):
        b.add(lambda p, x=x, z=z: sd_sphere(p, (x, 0.04, z), 0.016), RED, 0.0)


@P("star_lantern", (-0.25, -0.02, -0.15), (0.25, 0.55, 0.15), 0.006, "별 램프 (선반 장식)", light=[0, 0.3, 0])
def star_lantern(m):
    s = m.part("star", tint=(0.98, 0.85, 0.4), group="star", gloss=0.6, k=0.01)
    s.add(lambda p: sd_star(p, (0, 0.3, 0), 0.22, 5, 2.6, 0.04, 0.035), WHITE, 1.0)
    b = m.part("stand", k=0.01)
    b.add(lambda p: sd_capsule(p, (0, 0.03, 0), (0, 0.12, 0), 0.015), (0.55, 0.37, 0.24), 0.0)
    b.add(lambda p: sd_cylinder(p, (0, 0.02, 0), 0.02, 0.08, 0.015), (0.55, 0.37, 0.24), 0.0)
    return dict(emissiveParts=["star"])


@P("snow_globe", (-0.16, -0.02, -0.16), (0.16, 0.34, 0.16), 0.004, "스노우볼")
def snow_globe(m):
    b = m.part("base", tint=(0.55, 0.37, 0.24), group="wood", k=0.01)
    b.add(lambda p: sd_cone(p, (0, 0, 0), 0.08, 0.13, 0.1, 0.01), WHITE, 1.0)
    g = m.part("glass", gloss=1.0, k=0.0)
    g.add(lambda p: sd_sphere(p, (0, 0.19, 0), 0.12), (0.82, 0.92, 0.98), 0.0)
    t = m.part("inside", k=0.01)
    t.add(lambda p: sd_cone(p, (0, 0.13, 0), 0.15, 0.06, 0.005, 0.005), GREEN, 0.0)
    t.add(lambda p: sd_ellipsoid(p, (0, 0.1, 0), (0.1, 0.025, 0.1)), SNOW, 0.0)
    return dict(transparentParts=["glass"])


@P("present_sack", (-0.45, -0.02, -0.4), (0.45, 0.95, 0.45), 0.01, "산타 선물 자루")
def sack(m):
    s = m.part("sack", tint=(0.72, 0.5, 0.35), group="sack", k=0.06)
    s.add(lambda p: sd_ellipsoid(p, (0, 0.33, 0), (0.36, 0.33, 0.32)), WHITE, 1.0)
    s.add(lambda p: sd_roundcone(p, (0, 0.5, 0), (0.05, 0.75, -0.02), 0.2, 0.08), WHITE, 1.0)
    s.add(lambda p: sd_ellipsoid(p, (0.08, 0.82, -0.02), (0.14, 0.08, 0.12), rot(z=-20)), WHITE, 1.0)
    s.paint(lambda p: np.abs(np.sin(p[:, 0] * 40) * np.sin(p[:, 1] * 40)) - 0.2, (0.85, 0.85, 0.85), 1.0, soft=0.3)
    r = m.part("rope", tint=RED, group="rope", k=0.01)
    r.add(lambda p: sd_torus(p, (0.04, 0.72, -0.015), 0.085, 0.02, rot(z=-8)), WHITE, 1.0)
    gp = m.part("gifts", k=0.01)
    gp.add(lambda p: sd_roundbox(p, (0.25, 0.65, 0.05), (0.08, 0.08, 0.08), 0.015, rot(z=-30)), (0.3, 0.6, 0.85), 0.0)
    gp.add(lambda p: sd_roundbox(p, (0.25, 0.65, 0.05), (0.083, 0.02, 0.083), 0.008, rot(z=-30)), (0.98, 0.85, 0.35), 0.0)


@P("reindeer_plush", (-0.4, -0.02, -0.35), (0.4, 1.0, 0.4), 0.009, "루돌프 인형")
def reindeer(m):
    t = m.part("fur", tint=(0.72, 0.5, 0.32), group="fur", k=0.04)
    t.add(lambda p: sd_ellipsoid(p, (0, 0.33, -0.05), (0.18, 0.17, 0.25)), WHITE, 1.0)
    t.add(lambda p: sd_sphere(p, (0, 0.6, 0.12), 0.16), WHITE, 1.0)
    t.add(lambda p: sd_ellipsoid(p, (0, 0.55, 0.26), (0.09, 0.07, 0.08)), (0.98, 0.9, 0.8), 0.0)
    for x in (-0.1, 0.1):
        for z in (-0.18, 0.12):
            t.add(lambda p, x=x, z=z: sd_capsule(p, (x, 0.06, z), (x, 0.25, z), 0.055), WHITE, 1.0)
    for s in (-1, 1):
        t.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.15, 0.7, 0.08), (0.07, 0.035, 0.04), rot(z=-s * 30)), WHITE, 1.0)
    a = m.part("antlers", k=0.01)
    for s in (-1, 1):
        a.add(lambda p, s=s: sd_capsule(p, (s * 0.07, 0.72, 0.08), (s * 0.15, 0.95, 0.03), 0.022), (0.55, 0.38, 0.25), 0.0)
        a.add(lambda p, s=s: sd_capsule(p, (s * 0.11, 0.84, 0.06), (s * 0.22, 0.88, 0.05), 0.017), (0.55, 0.38, 0.25), 0.0)
    f = m.part("face", k=0.0)
    f.add(lambda p: sd_sphere(p, (0, 0.57, 0.34), 0.04), (0.92, 0.18, 0.2), 0.0)
    for s in (-1, 1):
        f.add(lambda p, s=s: sd_sphere(p, (s * 0.06, 0.66, 0.26), 0.018), DARK, 0.0)
    f.add(lambda p: sd_torus(p, (0, 0.47, 0.13), 0.1, 0.022, rot(x=-30)), RED, 0.0)
    f.add(lambda p: sd_sphere(p, (0, 0.4, 0.22), 0.04), (0.98, 0.78, 0.32), 0.0)
