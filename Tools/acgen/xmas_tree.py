"""거대 트리 + 오너먼트"""
from assets import *

PINE = (0.33, 0.62, 0.38)
PINE_TIP = (0.5, 0.76, 0.45)
TRUNK = (0.55, 0.36, 0.22)

# ---------------------------------------------------------------- 큰 트리 (높이 약 7m)
TIERS = [  # (아래 y, 높이, 아래 반지름)
    (0.85, 1.75, 2.7), (2.0, 1.6, 2.2), (3.05, 1.45, 1.72), (4.0, 1.3, 1.28), (4.85, 1.15, 0.88), (5.6, 1.35, 0.5)]


def pine_tiers(part, tiers, base_lobes=16, color=WHITE, mask=1.0, tip=PINE_TIP):
    """층층이 처진 치마 모양 가지. 층끼리는 거의 안 섞어서 층 그늘이 또렷하게."""
    for i, (y0, h, r) in enumerate(tiers):
        top = (0, y0 + h, 0)
        part.add(lambda p, y0=y0, h=h, r=r: sd_roundcone(p, (0, y0 + 0.25 * r / 2.7 + 0.1, 0), (0, y0 + h, 0), r * 0.62, 0.04),
                 color, mask, k=0.03)
        n = max(8, int(base_lobes * (r / tiers[0][2]) ** 0.7) + 2)
        for j in range(n):
            a = 2 * np.pi * (j + 0.5 * (i % 2)) / n
            d = np.array([np.cos(a), 0, np.sin(a)])
            inner = d * r * 0.42 + [0, y0 + h * 0.45, 0]
            outer = d * r * 0.93 + [0, y0 + 0.02, 0]
            rr = min(0.6, r * 1.1 * np.pi / n)
            part.add(lambda p, inner=inner, outer=outer, rr=rr: sd_roundcone(p, inner, outer, rr * 0.9, rr * 0.55), color, mask, k=0.09)
            part.paint(lambda p, outer=outer, rr=rr: sd_sphere(p, outer + [0, 0.05, 0], rr * 0.7), tip, 1.0, soft=rr * 0.8)


@asset("tree_giant", "tree", (-3.0, -0.05, -3.0), (3.0, 7.1, 3.0), 0.045, "장식 없는 거대 트리 (오너먼트 붙이는 곳)", attach="mesh")
def tree_giant(m):
    t = m.part("trunk", tint=None, k=0.05)
    t.add(lambda p: sd_cylinder(p, (0, 0.6, 0), 0.6, 0.32, 0.06), TRUNK, 0.0)
    t.add(lambda p: sd_cone(p, (0, 0.0, 0), 0.25, 0.5, 0.35, 0.03), TRUNK, 0.0)
    tr = m.part("leaves", tint=PINE, group="leaves", k=0.08)
    pine_tiers(tr, TIERS)
    return dict(height=6.95, tiers=TIERS)


@asset("tree_star_topper", "tree", (-0.6, -0.15, -0.3), (0.6, 1.1, 0.3), 0.012, "트리 꼭대기 별")
def star_topper(m):
    s = m.part("star", tint=GOLD, group="star", gloss=0.8, k=0.02)
    s.add(lambda p: sd_star(p, (0, 0.55, 0), 0.5, 5, 2.6, 0.1, 0.09), WHITE, 1.0)
    s.add(lambda p: sd_cylinder(p, (0, 0.0, 0), 0.12, 0.08, 0.03), WHITE, 1.0)
    return dict(mountY=0.0)


@asset("tree_skirt", "tree", (-2.3, -0.05, -2.3), (2.3, 0.4, 2.3), 0.04, "트리 밑 받침천")
def tree_skirt(m):
    s = m.part("skirt", tint=(0.86, 0.22, 0.22), group="skirt", k=0.05)
    def wavy(p):
        a = np.arctan2(p[:, 2], p[:, 0]); r = np.hypot(p[:, 0], p[:, 2])
        rr = 2.1 + 0.08 * np.sin(a * 9)
        return np.maximum(r - rr, np.abs(p[:, 1] - 0.05 - 0.04 * np.cos(a * 9)) - 0.035) - 0.02
    s.add(wavy, WHITE, 1.0)
    s.add(lambda p: sd_torus(p, (0, 0.06, 0), 2.1, 0.08), (0.98, 0.97, 0.94), 0.0)
    for i in range(18):   # 하얀 별 무늬
        a = np.radians(i * 20 + 10); rr = 1.35 + 0.25 * (i % 2)
        s.paint(lambda p, a=a, rr=rr: sd_sphere(p, (np.cos(a) * rr, 0.08, np.sin(a) * rr), 0.09), (0.98, 0.97, 0.94), 0.0, soft=0.01)


@asset("tree_planter", "tree", (-1.0, -0.05, -1.0), (1.0, 1.1, 1.0), 0.02, "나무 화분 받침")
def tree_planter(m):
    b = m.part("tub", tint=(0.72, 0.5, 0.32), group="wood", k=0.03)
    b.add(lambda p: sd_cone(p, (0, 0.05, 0), 0.85, 0.82, 0.95, 0.04), WHITE, 1.0)
    b.cut(lambda p: sd_cylinder(p, (0, 0.95, 0), 0.2, 0.86), 0.02)
    for y in (0.22, 0.72):
        b.add(lambda p, y=y: sd_torus(p, (0, y, 0), 0.86 + (y - 0.05) * 0.15, 0.04), (0.45, 0.42, 0.42), 0.0)
    b.paint(lambda p: np.abs(np.sin(np.arctan2(p[:, 2], p[:, 0]) * 14)) - 0.06, (0.8, 0.8, 0.8), 1.0, soft=0.04)
    soil = m.part("soil", k=0.02)
    soil.add(lambda p: sd_cylinder(p, (0, 0.85, 0), 0.04, 0.88, 0.02), (0.38, 0.27, 0.2), 0.0)


# ---------------------------------------------------------------- 오너먼트 (원점 = 걸이 고리, 아래로 매달림)
OB = (-0.16, -0.32, -0.16), (0.16, 0.03, 0.16)
OS = 0.0042


def hook(m, y_top=-0.012):
    c = m.part("cap", gloss=0.9, k=0.005)
    c.add(lambda p: sd_cylinder(p, (0, y_top - 0.01, 0), 0.012, 0.022, 0.004), GOLD, 0.0)
    c.add(lambda p: sd_torus(p, (0, y_top + 0.008, 0), 0.011, 0.0035, rot(x=90)), GOLD, 0.0)
    return c


def orn(name, desc, **kw):
    return asset(name, "ornament", OB[0], OB[1], OS, desc, ao=True, hang=[0, 0, 0], **kw)


@orn("orn_ball", "기본 공")
def o_ball(m):
    hook(m)
    m.part("body", tint=(0.9, 0.2, 0.22), group="orn", gloss=1.0).add(lambda p: sd_sphere(p, (0, -0.1, 0), 0.075), WHITE, 1.0)


@orn("orn_ball_stripe", "줄무늬 공")
def o_stripe(m):
    hook(m)
    b = m.part("body", tint=(0.25, 0.5, 0.85), group="orn", gloss=1.0)
    b.add(lambda p: sd_sphere(p, (0, -0.1, 0), 0.075), WHITE, 1.0)
    b.paint(lambda p: np.abs(p[:, 1] + 0.1) - 0.016, (0.98, 0.97, 0.94), 0.0, soft=0.003)
    b.paint(lambda p: np.abs(np.abs(p[:, 1] + 0.1) - 0.042) - 0.007, GOLD, 0.0, soft=0.003)


@orn("orn_ball_dot", "물방울 공")
def o_dot(m):
    hook(m)
    b = m.part("body", tint=(0.3, 0.66, 0.42), group="orn", gloss=1.0)
    b.add(lambda p: sd_sphere(p, (0, -0.1, 0), 0.075), WHITE, 1.0)
    pts = []
    for i in range(26):   # 골고루 퍼진 점 (피보나치 구)
        yy = 1 - 2 * (i + 0.5) / 26; rr = np.sqrt(1 - yy * yy); a = i * 2.39996
        pts.append(np.array([np.cos(a) * rr, yy, np.sin(a) * rr]) * 0.075 + [0, -0.1, 0])
    P = np.array(pts)
    b.paint(lambda p: np.min(np.linalg.norm(p[:, None, :] - P[None], axis=2), axis=1) - 0.013, (0.98, 0.97, 0.94), 0.0, soft=0.003)


@orn("orn_ball_glitter", "반짝이 띠 공")
def o_glit(m):
    hook(m)
    b = m.part("body", tint=(0.6, 0.35, 0.8), group="orn", gloss=1.0)
    b.add(lambda p: sd_sphere(p, (0, -0.1, 0), 0.075), WHITE, 1.0)
    b.paint(lambda p: np.abs(np.sin(np.arctan2(p[:, 2], p[:, 0]) * 3 + (p[:, 1] + 0.1) * 40)) - 0.25, GOLD, 0.0, soft=0.08)


@orn("orn_drop", "물방울 모양")
def o_drop(m):
    hook(m)
    b = m.part("body", tint=(0.95, 0.75, 0.3), group="orn", gloss=1.0, k=0.04)
    b.add(lambda p: sd_sphere(p, (0, -0.13, 0), 0.06), WHITE, 1.0)
    b.add(lambda p: sd_roundcone(p, (0, -0.13, 0), (0, -0.03, 0), 0.06, 0.012), WHITE, 1.0)
    b.add(lambda p: sd_roundcone(p, (0, -0.17, 0), (0, -0.25, 0), 0.04, 0.006), WHITE, 1.0)
    b.paint(lambda p: np.abs(np.sin(np.arctan2(p[:, 2], p[:, 0]) * 4)) - 0.2, (0.85, 0.85, 0.85), 1.0, soft=0.2)


@orn("orn_star", "별")
def o_star(m):
    hook(m)
    s = m.part("body", tint=GOLD, group="orn", gloss=0.9)
    s.add(lambda p: sd_star(p, (0, -0.1, 0), 0.085, 5, 2.6, 0.012, 0.012), WHITE, 1.0)


@orn("orn_heart", "하트")
def o_heart(m):
    hook(m)
    s = m.part("body", tint=(0.95, 0.4, 0.55), group="orn", gloss=0.9)
    s.add(lambda p: sd_heart(p, (0, -0.105, 0), 0.13, 0.016, 0.016), WHITE, 1.0)


@orn("orn_bell", "종")
def o_bell(m):
    hook(m)
    b = m.part("body", tint=GOLD, group="orn", gloss=1.0, k=0.012)
    # 종 옆면: 위는 둥근 돔, 아래로 갈수록 벌어지는 프로파일을 회전
    def bell(p):
        r = np.hypot(p[:, 0], p[:, 2]); y = -(p[:, 1] + 0.04)        # y: 0(위) ~ 0.12(아래)
        t = np.clip(y / 0.12, 0, 1)
        prof = 0.036 + 0.012 * t + 0.03 * t ** 3                       # 반지름
        side = r - prof
        return np.maximum(np.maximum(side, -y), y - 0.12) * 0.8
    b.add(lambda p: smin(bell(p), sd_sphere(p, (0, -0.05, 0), 0.036), 0.01), WHITE, 1.0)
    b.add(lambda p: sd_torus(p, (0, -0.16, 0), 0.075, 0.009), WHITE, 1.0)
    b.cut(lambda p: sd_cylinder(p, (0, -0.2, 0), 0.045, 0.066), 0.0)
    b.add(lambda p: sd_sphere(p, (0, -0.165, 0), 0.02), WHITE, 1.0)
    r = m.part("ribbon", tint=(0.86, 0.2, 0.22), group="orn2", k=0.01)
    r.add(lambda p: sd_torus(p, (0, -0.06, 0), 0.037, 0.008), WHITE, 1.0)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.035, -0.055, 0.03), (0.028, 0.016, 0.01), rot(z=s * 15, y=-s * 25)), WHITE, 1.0)
        r.add(lambda p, s=s: sd_roundbox(p, (s * 0.018, -0.085, 0.04), (0.008, 0.025, 0.004), 0.003, rot(z=s * 20)), WHITE, 1.0)


@orn("orn_candycane", "지팡이 사탕")
def o_cane(m):
    c = m.part("body", tint=(0.9, 0.18, 0.2), group="orn", gloss=0.6, k=0.004)
    pts = [(0.035 * np.cos(a) - 0.035, -0.04 + 0.035 * np.sin(a), 0) for a in np.radians(np.linspace(0, 180, 9))]
    pts = pts[::-1] + [(0.0, -0.06, 0), (0.0, -0.27, 0)]
    pts = [(-0.07, -0.06, 0)] + pts[1:]
    chain = [(-0.07, -0.07, 0)] + [(0.035 * np.cos(a) - 0.035, -0.035 + 0.035 * np.sin(a), 0) for a in np.radians(np.linspace(180, 0, 10))] + [(0.0, -0.27, 0)]
    for a, b in zip(chain[:-1], chain[1:]):
        c.add(lambda p, a=a, b=b: sd_capsule(p, a, b, 0.016), (0.98, 0.97, 0.95), 0.0)
    c.paint(lambda p: np.abs(np.sin((p[:, 1] + p[:, 0] * 0.8) * 70)) - 0.45, WHITE, 1.0, soft=0.15)
    return dict(hang=[-0.035, -0.0, 0])


@orn("orn_snowflake", "눈송이")
def o_flake(m):
    hook(m, -0.012)
    f = m.part("body", tint=(0.85, 0.93, 1.0), group="orn", gloss=0.7, k=0.006)
    c = np.array([0, -0.11, 0])
    for i in range(6):
        a = np.radians(i * 60); d = np.array([np.sin(a), np.cos(a), 0])
        f.add(lambda p, d=d: sd_capsule(p, c, c + d * 0.085, 0.009), WHITE, 1.0)
        for t in (0.045, 0.07):
            for s in (-1, 1):
                b = np.radians(i * 60 + s * 45); e = np.array([np.sin(b), np.cos(b), 0])
                f.add(lambda p, d=d, e=e, t=t: sd_capsule(p, c + d * t, c + d * t + e * 0.025, 0.006), WHITE, 1.0)
    f.add(lambda p: sd_cylinder(p, c, 0.012, 0.02, 0.006, rot(x=90)), WHITE, 1.0)


@orn("orn_gift", "작은 선물상자")
def o_gift(m):
    b = m.part("body", tint=(0.3, 0.62, 0.85), group="orn", k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, -0.1, 0), (0.06, 0.06, 0.06), 0.012), WHITE, 1.0)
    r = m.part("ribbon", tint=(0.98, 0.85, 0.35), group="orn2", gloss=0.5, k=0.008)
    r.add(lambda p: sd_roundbox(p, (0, -0.1, 0), (0.064, 0.064, 0.014), 0.005), WHITE, 1.0)
    r.add(lambda p: sd_roundbox(p, (0, -0.1, 0), (0.014, 0.064, 0.064), 0.005), WHITE, 1.0)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.025, -0.028, 0), (0.028, 0.018, 0.012), rot(z=s * 30)), WHITE, 1.0)
    r.add(lambda p: sd_torus(p, (0, -0.012, 0), 0.01, 0.0035, rot(x=90)), WHITE, 1.0)


@orn("orn_gingerbread", "진저브레드맨")
def o_ginger(m):
    g = m.part("body", tint=(0.76, 0.48, 0.26), group="orn", k=0.012)
    c = -0.12
    g.add(lambda p: sd_cylinder(p, (0, -0.06, 0), 0.016, 0.035, 0.012, rot(x=90)), WHITE, 1.0)
    g.add(lambda p: sd_roundbox(p, (0, c, 0), (0.035, 0.05, 0.014), 0.013), WHITE, 1.0)
    for s in (-1, 1):
        g.add(lambda p, s=s: sd_capsule(p, (s * 0.03, c + 0.03, 0), (s * 0.068, c + 0.01, 0), 0.016), WHITE, 1.0)
        g.add(lambda p, s=s: sd_capsule(p, (s * 0.018, c - 0.03, 0), (s * 0.03, c - 0.08, 0), 0.017), WHITE, 1.0)
    ic = m.part("icing", k=0.0)
    for s in (-1, 1):
        ic.add(lambda p, s=s: sd_sphere(p, (s * 0.012, -0.056, 0.014), 0.005), (0.2, 0.12, 0.1), 0.0)
        ic.add(lambda p, s=s: sd_capsule(p, (s * 0.055, c + 0.018, 0.013), (s * 0.068, c + 0.012, 0.013), 0.0035), (0.99, 0.98, 0.96), 0.0)
        ic.add(lambda p, s=s: sd_capsule(p, (s * 0.022, c - 0.07, 0.014), (s * 0.034, c - 0.076, 0.014), 0.0035), (0.99, 0.98, 0.96), 0.0)
    for y in (0.012, -0.012):
        ic.add(lambda p, y=y: sd_sphere(p, (0, c + y, 0.016), 0.007), (0.9, 0.25, 0.3), 0.0)
    pts = arc_pts(0, -0.072, 0.014, 0.008)
    for a, b in zip(pts[:-1], pts[1:]):
        ic.add(lambda p, a=a, b=b: sd_capsule(p, a, b, 0.0028), (0.99, 0.98, 0.96), 0.0)
    g.add(lambda p: sd_torus(p, (0, -0.02, 0), 0.01, 0.0035, rot(x=90)), GOLD, 0.0)


def arc_pts(cx, cy, w, h, z=0.017, n=6):
    return [(cx + w * np.cos(a), cy + h * np.sin(a), z) for a in np.radians(np.linspace(200, 340, n))]


@orn("orn_pinecone", "솔방울")
def o_pine(m):
    hook(m)
    b = m.part("body", tint=(0.62, 0.4, 0.26), group="orn", k=0.006)
    b.add(lambda p: sd_ellipsoid(p, (0, -0.11, 0), (0.042, 0.075, 0.042)), WHITE, 1.0)
    for i in range(40):
        t = i / 40; y = -0.045 - t * 0.13; a = i * 2.39996; r = 0.045 * np.sin(np.pi * (0.15 + 0.8 * t)) + 0.006
        c = (np.cos(a) * r, y, np.sin(a) * r)
        b.add(lambda p, c=c, a=a: sd_ellipsoid(p, c, (0.018, 0.011, 0.016), rot(y=-np.degrees(a), z=-25)), WHITE, 1.0)
    b.paint(lambda p: p[:, 1] + 0.06 + 0 * p[:, 0], (1.25, 1.25, 1.25), 1.0, soft=0.03)   # 눈 덮인 끝은 따로
    sn = m.part("snow", k=0.01)
    sn.add(lambda p: sd_ellipsoid(p, (0, -0.05, 0), (0.035, 0.014, 0.035)), (0.98, 0.98, 1.0), 0.0)


@orn("orn_icicle", "고드름")
def o_icicle(m):
    hook(m)
    b = m.part("body", tint=(0.78, 0.92, 1.0), group="orn", gloss=1.0, k=0.01)
    for i in range(5):
        y0 = -0.03 - i * 0.045; r0 = 0.022 - i * 0.0035
        b.add(lambda p, y0=y0, r0=r0: sd_roundcone(p, (0, y0, 0), (0, y0 - 0.05, 0), r0, r0 - 0.004), WHITE, 1.0)
        b.add(lambda p, y0=y0, r0=r0: sd_torus(p, (0, y0 - 0.015, 0), r0 + 0.001, 0.005), WHITE, 1.0)
    b.add(lambda p: sd_roundcone(p, (0, -0.25, 0), (0, -0.3, 0), 0.008, 0.002), WHITE, 1.0)


@orn("orn_bow", "리본")
def o_bow(m):
    r = m.part("body", tint=(0.88, 0.2, 0.25), group="orn", gloss=0.4, k=0.012)
    c = np.array([0, -0.06, 0])
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_torus(p, c + [s * 0.04, 0.0, 0], 0.03, 0.013, rot(x=90, z=s * 10)), WHITE, 1.0)
        r.add(lambda p, s=s: sd_roundbox(p, c + [s * 0.025, -0.06, 0], (0.014, 0.05, 0.006), 0.005, rot(z=s * 18)), WHITE, 1.0)
    r.add(lambda p: sd_sphere(p, c, 0.02), WHITE, 1.0)
    return dict(hang=[0, -0.04, 0])


@orn("orn_stocking", "작은 양말")
def o_stock(m):
    s = m.part("body", tint=(0.88, 0.2, 0.22), group="orn", k=0.02)
    s.add(lambda p: sd_capsule(p, (0, -0.06, 0), (0, -0.17, 0), 0.035), WHITE, 1.0)
    s.add(lambda p: sd_capsule(p, (0, -0.18, 0), (0.05, -0.2, 0), 0.036), WHITE, 1.0)
    s.paint(lambda p: sd_sphere(p, (0.065, -0.205, 0), 0.03), (0.98, 0.97, 0.94), 0.0, soft=0.01)
    s.paint(lambda p: sd_sphere(p, (-0.02, -0.2, 0), 0.03), (0.98, 0.97, 0.94), 0.0, soft=0.01)
    c = m.part("cuff", k=0.01)
    c.add(lambda p: sd_cylinder(p, (0, -0.045, 0), 0.022, 0.042, 0.016), (0.99, 0.98, 0.96), 0.0)
    c.add(lambda p: sd_torus(p, (-0.02, -0.015, 0), 0.012, 0.004, rot(x=90)), GOLD, 0.0)


@orn("orn_snowman", "작은 눈사람")
def o_snowman(m):
    hook(m, -0.008)
    b = m.part("body", k=0.015)
    b.add(lambda p: sd_sphere(p, (0, -0.2, 0), 0.055), (0.98, 0.98, 1.0), 0.0)
    b.add(lambda p: sd_sphere(p, (0, -0.115, 0), 0.042), (0.98, 0.98, 1.0), 0.0)
    h = m.part("hat", tint=(0.88, 0.2, 0.22), group="orn", k=0.01)
    h.add(lambda p: sd_roundcone(p, (0, -0.085, 0), (0.01, -0.03, -0.01), 0.035, 0.01), WHITE, 1.0)
    h.add(lambda p: sd_torus(p, (0, -0.135 + 0.055, 0), 0.032, 0.008, rot(x=-8)), (0.98, 0.97, 0.95), 0.0)
    h.add(lambda p: sd_torus(p, (0, -0.155, 0), 0.032, 0.01), WHITE, 1.0)
    d = m.part("face", k=0.0)
    for s in (-1, 1):
        d.add(lambda p, s=s: sd_sphere(p, (s * 0.015, -0.11, 0.038), 0.006), (0.15, 0.12, 0.12), 0.0)
    d.add(lambda p: sd_roundcone(p, (0, -0.122, 0.038), (0, -0.124, 0.065), 0.007, 0.002), (0.98, 0.55, 0.2), 0.0)
    for y in (-0.18, -0.21):
        d.add(lambda p, y=y: sd_sphere(p, (0, y, 0.053), 0.006), (0.15, 0.12, 0.12), 0.0)


@orn("orn_acorn", "도토리")
def o_acorn(m):
    hook(m)
    b = m.part("body", tint=(0.8, 0.52, 0.28), group="orn", gloss=0.8, k=0.01)
    b.add(lambda p: sd_ellipsoid(p, (0, -0.12, 0), (0.05, 0.06, 0.05)), WHITE, 1.0)
    b.add(lambda p: sd_roundcone(p, (0, -0.15, 0), (0, -0.19, 0), 0.035, 0.006), WHITE, 1.0)
    c = m.part("capB", k=0.01)
    c.add(lambda p: sd_ellipsoid(p, (0, -0.075, 0), (0.058, 0.035, 0.058)), (0.5, 0.34, 0.22), 0.0)
    c.paint(lambda p: np.abs(np.sin(p[:, 0] * 220) * np.sin(p[:, 2] * 220)) - 0.3, (0.42, 0.28, 0.18), 0.0, soft=0.3)


@orn("orn_mushroom", "버섯")
def o_mush(m):
    hook(m, -0.012)
    c = m.part("body", tint=(0.9, 0.22, 0.22), group="orn", gloss=0.6, k=0.01)
    c.add(lambda p: sd_ellipsoid(p, (0, -0.07, 0), (0.07, 0.045, 0.07)), WHITE, 1.0)
    c.cut(lambda p: p[:, 1] + 0.075, 0.01)
    for i, (a, rr) in enumerate([(0, 0.0), (60, 0.045), (180, 0.045), (300, 0.045), (120, 0.03), (240, 0.03)]):
        aa = np.radians(a)
        cc = np.array([np.cos(aa) * rr, -0.04 + (0.0 if rr == 0 else -0.012 if rr > 0.04 else -0.005), np.sin(aa) * rr])
        c.paint(lambda p, cc=cc: sd_sphere(p, cc, 0.014), (0.99, 0.98, 0.96), 0.0, soft=0.003)
    s = m.part("stem", k=0.015)
    s.add(lambda p: sd_roundcone(p, (0, -0.15, 0), (0, -0.075, 0), 0.03, 0.025), (0.98, 0.94, 0.86), 0.0)
    s.add(lambda p: sd_cylinder(p, (0, -0.078, 0), 0.006, 0.062, 0.004), (0.96, 0.9, 0.8), 0.0)


@orn("orn_bird", "작은 새")
def o_bird(m):
    hook(m, -0.01)
    b = m.part("body", tint=(0.9, 0.28, 0.25), group="orn", k=0.02)
    b.add(lambda p: sd_ellipsoid(p, (0, -0.09, 0), (0.045, 0.04, 0.06)), WHITE, 1.0)
    b.add(lambda p: sd_sphere(p, (0, -0.065, 0.04), 0.032), WHITE, 1.0)
    b.add(lambda p: sd_ellipsoid(p, (0, -0.075, -0.07), (0.022, 0.008, 0.035), rot(x=-20)), WHITE, 1.0)
    for s in (-1, 1):
        b.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.04, -0.085, -0.005), (0.01, 0.025, 0.045), rot(x=-10)), (0.85, 0.85, 0.85), 1.0)
    b.paint(lambda p: sd_ellipsoid(p, (0, -0.105, 0.03), (0.03, 0.025, 0.03)), (0.99, 0.97, 0.94), 0.0, soft=0.008)
    f = m.part("face", k=0.0)
    f.add(lambda p: sd_roundcone(p, (0, -0.065, 0.07), (0, -0.068, 0.09), 0.008, 0.002), (0.98, 0.75, 0.25), 0.0)
    for s in (-1, 1):
        f.add(lambda p, s=s: sd_sphere(p, (s * 0.022, -0.055, 0.058), 0.006), (0.12, 0.1, 0.1), 0.0)


@orn("orn_cookie", "별 쿠키")
def o_cookie(m):
    c = m.part("body", tint=(0.86, 0.62, 0.36), group="orn", k=0.01)
    c.add(lambda p: sd_star(p, (0, -0.1, 0), 0.08, 5, 2.3, 0.01, 0.009), WHITE, 1.0)
    ic = m.part("icing", tint=(0.98, 0.96, 0.98), group="orn2", k=0.0)
    ic.add(lambda p: sd_star(p, (0, -0.1, 0.012), 0.062, 5, 2.3, 0.004, 0.004), WHITE, 1.0)
    for i in range(5):
        a = np.radians(90 + i * 72)
        ic.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.032, -0.1 + np.sin(a) * 0.032, 0.018), 0.006), (0.9, 0.25, 0.35), 0.0)
    c.add(lambda p: sd_torus(p, (0, -0.012, 0), 0.01, 0.0035, rot(x=90)), (0.9, 0.2, 0.22), 0.0)


@orn("orn_light_bulb", "전구")
def o_bulb(m):
    hook(m)
    b = m.part("body", tint=(0.98, 0.85, 0.3), group="orn", gloss=1.0, k=0.03)
    b.add(lambda p: sd_sphere(p, (0, -0.1, 0), 0.04), WHITE, 1.0)
    b.add(lambda p: sd_roundcone(p, (0, -0.1, 0), (0, -0.04, 0), 0.04, 0.02), WHITE, 1.0)
    b.add(lambda p: sd_roundcone(p, (0, -0.12, 0), (0, -0.16, 0), 0.03, 0.006), WHITE, 1.0)
    return dict(emissive=True)


@orn("orn_moon", "초승달")
def o_moon(m):
    hook(m, -0.01)
    b = m.part("body", tint=(0.98, 0.88, 0.45), group="orn", gloss=0.8, k=0.01)
    b.add(lambda p: sd_cylinder(p, (0, -0.1, 0), 0.014, 0.075, 0.013, rot(x=90)), WHITE, 1.0)
    b.cut(lambda p: sd_cylinder(p, (0.04, -0.08, 0), 0.05, 0.065, 0.0, rot(x=90)), 0.012)
