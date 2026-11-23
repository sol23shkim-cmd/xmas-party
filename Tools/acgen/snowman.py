"""눈사람 만들기용 부품.
붙이는 장식(attach): 원점 = 눈덩이 표면에 닿는 점, +z = 바깥(표면 법선), +y = 위쪽.
머리 위에 얹는 것(top): 원점 = 바닥 중심, +y 위.  목도리(neck): 원점 = 고리 중심, 고리 반지름 0.2.
눈덩이: 원점 = 중심, 반지름 0.5 (Unity에서 크기 조절)."""
from assets import *

SNOW = (0.98, 0.985, 1.0)
COAL = (0.13, 0.12, 0.13)
WOOD = (0.5, 0.33, 0.2)
ORANGE = (0.98, 0.52, 0.16)
RED = (0.88, 0.22, 0.24)
GREEN = (0.3, 0.6, 0.38)


def S(name, lo, hi, step, desc, place="attach", **kw):
    return asset(name, "snowman", lo, hi, step, desc, place=place, **kw)


@S("sm_snowball", (-0.56, -0.56, -0.56), (0.56, 0.56, 0.56), 0.011, "눈덩이", place="ball")
def snowball(m):
    b = m.part("snow", k=0.07)
    b.add(lambda p: sd_sphere(p, (0, 0, 0), 0.49), SNOW, 0.0)
    rng = np.random.default_rng(7)
    for _ in range(18):   # 손으로 굴린 듯 살짝 울퉁불퉁
        d = rng.normal(size=3); d /= np.linalg.norm(d)
        r = rng.uniform(0.09, 0.14); c = d * (0.49 - r * 0.75)
        b.add(lambda p, c=c, r=r: sd_sphere(p, c, r), SNOW, 0.0)
    b.paint(lambda p: np.abs(p[:, 1] + 0.05 + 0.04 * np.sin(p[:, 0] * 9)) - 0.01 + 0.3 * (np.linalg.norm(p, axis=1) < 0.4), (0.92, 0.94, 0.99), 0.0, soft=0.025)


@S("sm_carrot", (-0.06, -0.06, -0.04), (0.06, 0.06, 0.26), 0.004, "당근 코")
def carrot(m):
    c = m.part("carrot", k=0.01)
    c.add(lambda p: sd_roundcone(p, (0, 0, 0.0), (0, -0.008, 0.22), 0.036, 0.006), ORANGE, 0.0)
    for z in (0.05, 0.09, 0.13, 0.17):
        c.paint(lambda p, z=z: np.abs(p[:, 2] - z) - 0.003, (0.85, 0.4, 0.12), 0.0, soft=0.003)


@S("sm_coal", (-0.05, -0.05, -0.03), (0.05, 0.05, 0.05), 0.003, "숯 (눈·단추)")
def coal(m):
    c = m.part("coal", k=0.012)
    c.add(lambda p: sd_ellipsoid(p, (0, 0, 0.008), (0.03, 0.028, 0.022)), COAL, 0.0)
    c.add(lambda p: sd_sphere(p, (0.012, 0.01, 0.014), 0.016), COAL, 0.0)
    c.add(lambda p: sd_sphere(p, (-0.012, -0.008, 0.012), 0.015), COAL, 0.0)
    c.paint(lambda p: sd_sphere(p, (-0.008, 0.012, 0.03), 0.008), (0.45, 0.45, 0.5), 0.0, soft=0.006)


@S("sm_button", (-0.05, -0.05, -0.02), (0.05, 0.05, 0.03), 0.003, "단추")
def button(m):
    b = m.part("button", tint=RED, group="button", k=0.004)
    b.add(lambda p: sd_cylinder(p, (0, 0, 0.006), 0.009, 0.036, 0.006, rot(x=90)), WHITE, 1.0)
    b.cut(lambda p: sd_cylinder(p, (0, 0, 0.018), 0.004, 0.028, 0.002, rot(x=90)), 0.003)
    for x, y in ((-0.009, 0.009), (0.009, 0.009), (-0.009, -0.009), (0.009, -0.009)):
        b.cut(lambda p, x=x, y=y: sd_cylinder(p, (x, y, 0.006), 0.03, 0.004, 0.0, rot(x=90)), 0.0)


@S("sm_arm", (-0.12, -0.08, -0.04), (0.12, 0.2, 0.5), 0.005, "나뭇가지 팔")
def arm(m):
    w = m.part("twig", k=0.008)
    a0 = np.array([0, 0, -0.02]); a1 = np.array([0, 0.06, 0.3]); a2 = np.array([0.0, 0.1, 0.44])
    w.add(lambda p: sd_roundcone(p, a0, a1, 0.017, 0.012), WOOD, 0.0)
    w.add(lambda p: sd_roundcone(p, a1, a2, 0.012, 0.007), WOOD, 0.0)
    for b0, b1 in (((0, 0.06, 0.3), (0.07, 0.15, 0.4)), ((0, 0.04, 0.22), (-0.07, 0.1, 0.3)), ((0, 0.1, 0.44), (0.04, 0.17, 0.48))):
        w.add(lambda p, b0=b0, b1=b1: sd_roundcone(p, np.array(b0), np.array(b1), 0.009, 0.005), WOOD, 0.0)
    w.paint(lambda p: np.abs(np.sin(p[:, 2] * 90)) - 0.92, (0.4, 0.26, 0.16), 0.0, soft=0.05)


@S("sm_bucket", (-0.22, -0.02, -0.22), (0.26, 0.3, 0.22), 0.006, "양동이 모자", place="top")
def bucket(m):
    h = m.part("bucket", tint=(0.55, 0.62, 0.72), group="hat", k=0.01, gloss=0.4)
    h.add(lambda p: sd_cone(p, (0, 0.0, 0), 0.2, 0.17, 0.13, 0.012), WHITE, 1.0)
    h.cut(lambda p: sd_cone(p, (0, -0.03, 0), 0.2, 0.15, 0.115, 0.0), 0.004)
    h.add(lambda p: sd_torus(p, (0, 0.2, 0), 0.125, 0.012), WHITE, 1.0)
    h.add(lambda p: sd_torus(p, (0, 0.02, 0), 0.168, 0.012), WHITE, 1.0)
    h.paint(lambda p: np.abs(p[:, 1] - 0.1) - 0.006, (0.8, 0.8, 0.82), 1.0, soft=0.004)
    for sx in (-1, 1):
        h.add(lambda p, sx=sx: sd_sphere(p, (sx * 0.158, 0.13, 0), 0.016), WHITE, 1.0)


@S("sm_pipe", (-0.05, -0.06, -0.02), (0.05, 0.12, 0.22), 0.004, "파이프")
def pipe(m):
    w = m.part("pipe", k=0.01)
    w.add(lambda p: sd_capsule(p, (0, 0, 0.0), (0, -0.01, 0.15), 0.01), (0.32, 0.22, 0.16), 0.0)
    w.add(lambda p: sd_cylinder(p, (0, 0.025, 0.17), 0.04, 0.032, 0.008), (0.85, 0.7, 0.42), 0.0)
    w.cut(lambda p: sd_cylinder(p, (0, 0.06, 0.17), 0.03, 0.022), 0.003)
    w.paint(lambda p: np.abs(np.sin(p[:, 1] * 140)) - 0.75 + 5 * (np.abs(p[:, 2] - 0.17) > 0.05), (0.72, 0.55, 0.3), 0.0, soft=0.2)


@S("sm_mitten", (-0.08, -0.04, -0.02), (0.08, 0.16, 0.08), 0.004, "벙어리장갑")
def mitten(m):
    h = m.part("mitten", tint=(0.3, 0.55, 0.85), group="mitten", k=0.02)
    h.add(lambda p: sd_ellipsoid(p, (0, 0.07, 0.03), (0.045, 0.06, 0.028)), WHITE, 1.0)
    h.add(lambda p: sd_ellipsoid(p, (0.045, 0.065, 0.03), (0.02, 0.03, 0.02), rot(z=-30)), WHITE, 1.0)
    h.add(lambda p: sd_cylinder(p, (0, 0.0, 0.03), 0.018, 0.04, 0.012), (0.98, 0.97, 0.95), 0.0)
    h.paint(lambda p: np.abs(p[:, 1] - 0.08) - 0.008, (0.98, 0.97, 0.95), 0.0, soft=0.004)


@S("sm_broom", (-0.14, -0.6, -0.04), (0.14, 0.6, 0.16), 0.006, "빗자루")
def broom(m):
    w = m.part("broom", k=0.01)
    w.add(lambda p: sd_capsule(p, (0, -0.32, 0.06), (0, 0.58, 0.06), 0.015), WOOD, 0.0)
    w.add(lambda p: sd_cone(p, (0, -0.58, 0.06), 0.28, 0.12, 0.03, 0.02), (0.88, 0.74, 0.42), 0.0)
    w.paint(lambda p: np.abs(np.sin(np.arctan2(p[:, 0], p[:, 2] - 0.06) * 24)) - 0.5 + 5 * (p[:, 1] > -0.3), (0.76, 0.6, 0.32), 0.0, soft=0.3)
    w.add(lambda p: sd_torus(p, (0, -0.33, 0.06), 0.032, 0.008), RED, 0.0)


@S("sm_bowtie", (-0.1, -0.06, -0.02), (0.1, 0.06, 0.05), 0.003, "나비넥타이")
def bowtie(m):
    b = m.part("bow", tint=RED, group="bow", k=0.012)
    for s in (-1, 1):
        b.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.045, 0, 0.012), (0.045, 0.032, 0.014), rot(z=s * 8)), WHITE, 1.0)
    b.add(lambda p: sd_sphere(p, (0, 0, 0.018), 0.02), WHITE, 1.0)
    b.paint(lambda p: np.abs(np.sin(p[:, 0] * 60)) - 0.85, (0.98, 0.97, 0.95), 1.0, soft=0.05)


@S("sm_glasses", (-0.15, -0.06, -0.02), (0.15, 0.06, 0.06), 0.003, "동그란 안경")
def glasses(m):
    g = m.part("frame", tint=(0.85, 0.66, 0.3), group="frame", k=0.0, gloss=0.6)
    for s in (-1, 1):
        g.add(lambda p, s=s: sd_torus(p, (s * 0.06, 0, 0.03), 0.045, 0.006, rot(x=90)), WHITE, 1.0)
        g.add(lambda p, s=s: sd_capsule(p, (s * 0.105, 0.0, 0.03), (s * 0.13, 0.0, -0.01), 0.005), WHITE, 1.0)
    g.add(lambda p: sd_capsule(p, (-0.017, 0.012, 0.033), (0.017, 0.012, 0.033), 0.005), WHITE, 1.0)


@S("sm_mustache", (-0.12, -0.05, -0.02), (0.12, 0.05, 0.04), 0.003, "콧수염")
def mustache(m):
    h = m.part("must", tint=(0.32, 0.22, 0.16), group="hair", k=0.02)
    for s in (-1, 1):
        h.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.035, -0.005, 0.012), (0.04, 0.02, 0.014), rot(z=s * 12)), WHITE, 1.0)
        h.add(lambda p, s=s: sd_roundcone(p, (s * 0.065, -0.005, 0.01), (s * 0.095, 0.02, 0.006), 0.016, 0.006), WHITE, 1.0)


@S("sm_smile", (-0.1, -0.06, -0.04), (0.1, 0.03, 0.04), 0.003, "숯 웃는 입")
def smile(m):
    c = m.part("coal", k=0.0)
    for i in range(5):
        a = np.radians(210 + i * 30)
        x, y = 0.075 * np.cos(a), 0.04 * np.sin(a) + 0.02
        z = -(x * x) / (2 * 0.25) - (y * y) / (2 * 0.25)
        c.add(lambda p, x=x, y=y, z=z: sd_ellipsoid(p, (x, y, z + 0.008), (0.014, 0.013, 0.011)), COAL, 0.0)


@S("sm_nose_round", (-0.05, -0.05, -0.02), (0.05, 0.05, 0.08), 0.003, "동그란 코")
def nose_round(m):
    n = m.part("nose", tint=RED, group="nose", k=0.0, gloss=0.5)
    n.add(lambda p: sd_sphere(p, (0, 0, 0.03), 0.038), WHITE, 1.0)
    n.paint(lambda p: sd_sphere(p, (-0.012, 0.015, 0.06), 0.01), (1, 1, 1), 0.0, soft=0.008)


@S("sm_twig_hair", (-0.14, -0.02, -0.06), (0.14, 0.22, 0.12), 0.004, "나뭇가지 머리")
def twig_hair(m):
    w = m.part("twig", k=0.006)
    for a, h in ((-35, 0.17), (-10, 0.2), (15, 0.18), (40, 0.15)):
        r = np.radians(a)
        top = np.array([np.sin(r) * 0.12, h, 0.06])
        w.add(lambda p, top=top: sd_roundcone(p, np.array([0, 0, 0.0]), top, 0.01, 0.005), WOOD, 0.0)
        w.add(lambda p, top=top: sd_roundcone(p, top * 0.6, top * 0.6 + [0.04 * np.sign(top[0] + 1e-3), 0.05, 0], 0.006, 0.004), WOOD, 0.0)


@S("sm_flower", (-0.07, -0.07, -0.02), (0.07, 0.07, 0.04), 0.003, "꽃")
def flower(m):
    f = m.part("petal", tint=(0.95, 0.5, 0.65), group="flower", k=0.006)
    for i in range(5):
        a = np.radians(90 + i * 72)
        f.add(lambda p, a=a: sd_ellipsoid(p, (np.cos(a) * 0.028, np.sin(a) * 0.028, 0.01), (0.022, 0.016, 0.008), rot(z=np.degrees(a))), WHITE, 1.0)
    c = m.part("center", k=0.0)
    c.add(lambda p: sd_sphere(p, (0, 0, 0.016), 0.014), (0.99, 0.82, 0.3), 0.0)


@S("sm_blush", (-0.06, -0.04, -0.02), (0.06, 0.04, 0.02), 0.003, "볼 터치")
def blush(m):
    b = m.part("blush", tint=(0.99, 0.66, 0.72), group="blush", k=0.0)
    b.add(lambda p: sd_ellipsoid(p, (0, 0, 0.0), (0.045, 0.028, 0.006)), WHITE, 1.0)


@S("sm_scarf", (-0.3, -0.36, -0.3), (0.3, 0.1, 0.32), 0.006, "목도리", place="neck")
def scarf(m):
    sc = m.part("scarf", tint=RED, group="scarf", k=0.02)
    sc.add(lambda p: sd_torus(p, (0, 0, 0), 0.2, 0.05), WHITE, 1.0)
    sc.add(lambda p: sd_roundbox(p, (0.1, -0.14, 0.215), (0.055, 0.14, 0.022), 0.018, rot(z=8, x=-12)), WHITE, 1.0)
    sc.add(lambda p: sd_roundbox(p, (0.15, -0.12, 0.2), (0.05, 0.12, 0.022), 0.018, rot(z=25, x=-10)), WHITE, 1.0)
    sc.paint(lambda p: np.abs(np.sin(p[:, 1] * 45)) - 0.55 + 5 * (np.abs(p[:, 1]) < 0.06) * (p[:, 2] < 0.15), (0.98, 0.97, 0.95), 0.0, soft=0.2)


@S("sm_star", (-0.06, -0.06, -0.02), (0.06, 0.06, 0.04), 0.003, "별 배지")
def star(m):
    s = m.part("star", tint=GOLD, group="star", k=0.0, gloss=0.6)
    s.add(lambda p: sd_star(p, (0, 0, 0.012), 0.05, thick=0.01, rnd=0.006), WHITE, 1.0)


@S("sm_heart", (-0.06, -0.06, -0.02), (0.06, 0.06, 0.04), 0.003, "하트")
def heart(m):
    s = m.part("heart", tint=RED, group="heart", k=0.0, gloss=0.3)
    s.add(lambda p: sd_heart(p, (0, 0.005, 0.012), 0.08, thick=0.01, rnd=0.006), WHITE, 1.0)


@S("sm_holly", (-0.1, -0.06, -0.02), (0.1, 0.07, 0.05), 0.003, "호랑가시 잎")
def holly(m):
    l = m.part("leaf", k=0.0)
    for s in (-1, 1):
        l.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.045, 0.01, 0.01), (0.048, 0.02, 0.006), rot(z=s * 20)), (0.2, 0.5, 0.3), 0.0)
    b = m.part("berry", k=0.0)
    for x, y in ((0, 0.0), (0.016, 0.014), (-0.016, 0.014)):
        b.add(lambda p, x=x, y=y: sd_sphere(p, (x, y, 0.022), 0.013), RED, 0.0)
