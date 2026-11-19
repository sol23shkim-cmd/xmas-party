"""동물의 숲풍 캐릭터 (머리 크고 팔다리 짧고 통통하게).

파일 구성 (Unity에서 조합)
  char_<종>.acm  : head, ears, face, torso, armL/armR, handL/handR, legL/legR, tail, (mane, nose)
  face_<n>.acm   : 사람 얼굴 5종 (눈·눈썹·입)
  hair_<스타일>.acm : 사람 머리카락 (여자 5종, 남자 2종)
  hat_<종류>.acm, scarf.acm : 모자, 목도리
색 묶음(group): fur(털/피부), sweater(옷), hair, hat, scarf, mane
"""
import numpy as np
from sdf import *

WHITE = (1, 1, 1)
DARK = (0.17, 0.12, 0.11)
PINK = (0.99, 0.64, 0.68)
CREAM = (1.0, 0.95, 0.88)

HC = np.array([0.0, 0.74, 0.0])          # 머리 중심
HR = np.array([0.34, 0.31, 0.31])        # 머리 반지름
LO, HI = (-0.5, -0.02, -0.55), (0.5, 1.55, 0.48)
STEP = 0.011
FACE_BOX = ((-0.32, 0.45, 0.05), (0.32, 0.95, 0.48))


# ---------------------------------------------------------------- 표면 위치 계산
def on_ellipsoid(c, r, x, y, inset=0.0):
    """타원체 앞면(z+) 위 (x,y) 지점의 위치와, 그 면을 바라보는 회전"""
    c = np.asarray(c, float); r = np.asarray(r, float)
    u = (x - c[0]) / r[0]; v = (y - c[1]) / r[1]
    w = np.sqrt(max(1 - u * u - v * v, 0.02))
    pos = np.array([x, y, c[2] + r[2] * w])
    n = np.array([u / r[0], v / r[1], w / r[2]]); n /= np.linalg.norm(n)
    z = n; xa = np.cross([0, 1, 0], z); xa /= np.linalg.norm(xa); ya = np.cross(z, xa)
    R = np.stack([xa, ya, z], axis=1)
    return pos - n * inset, R


def stamp(part, c, r, x, y, size, color, mask=0.0, depth=0.012, out=0.004):
    """곡면에 붙은 납작한 타원 (눈, 코, 무늬)"""
    pos, R = on_ellipsoid(c, r, x, y)
    cen = pos + R[:, 2] * (out - depth * 0.5)
    part.add(lambda p, cen=cen, R=R: sd_ellipsoid(p, cen, (size[0], size[1], depth), R), color, mask, k=0.0)


def curve(part, c, r, pts_xy, rad, color, mask=0.0, out=0.003):
    """곡면을 따라가는 가는 선 (입, 눈썹, 웃는 눈)"""
    pts = []
    for x, y in pts_xy:
        pos, R = on_ellipsoid(c, r, x, y)
        pts.append(pos + R[:, 2] * out)
    for a, b in zip(pts[:-1], pts[1:]):
        part.add(lambda p, a=a, b=b: sd_capsule(p, a, b, rad), color, mask, k=0.0)


def arc_xy(cx, cy, w, h, n=7, a0=200, a1=340):
    t = np.radians(np.linspace(a0, a1, n))
    return [(cx + w * np.cos(a), cy + h * np.sin(a)) for a in t]


# ---------------------------------------------------------------- 몸
def build_body(m, fur_tint, sweater_tint, leg_color=WHITE, leg_mask=1.0, hand_color=WHITE, hand_mask=1.0):
    torso = m.part("torso", tint=sweater_tint, group="sweater", k=0.06)
    torso.add(lambda p: sd_ellipsoid(p, (0, 0.27, 0), (0.195, 0.185, 0.165)), WHITE, 1.0)
    torso.add(lambda p: sd_ellipsoid(p, (0, 0.39, 0), (0.15, 0.1, 0.13)), WHITE, 1.0)
    torso.paint(lambda p: np.abs(p[:, 1] - 0.135) - 0.022, (0.92, 0.92, 0.92), 0.3, soft=0.006)
    torso.paint(lambda p: np.abs(p[:, 1] - 0.19) - 0.009, (0.92, 0.92, 0.92), 0.3, soft=0.005)
    for s, nm in ((-1, "L"), (1, "R")):
        sh = (s * 0.14, 0.385, 0.0)
        arm = m.part("arm" + nm, tint=sweater_tint, group="sweater", pivot=sh, k=0.04)
        arm.add(lambda p, s=s, sh=sh: sd_roundcone(p, sh, (s * 0.235, 0.29, 0.04), 0.078, 0.07), WHITE, 1.0)
        arm.paint(lambda p, s=s: sd_sphere(p, (s * 0.245, 0.275, 0.045), 0.07), (0.92, 0.92, 0.92), 0.3, soft=0.008)
        hand = m.part("hand" + nm, tint=fur_tint, group="fur", parent="arm" + nm, pivot=sh)
        hand.add(lambda p, s=s: sd_sphere(p, (s * 0.262, 0.245, 0.055), 0.066), hand_color, hand_mask)
        hip = (s * 0.088, 0.17, 0.0)
        leg = m.part("leg" + nm, tint=fur_tint, group="fur", pivot=hip, k=0.04)
        leg.add(lambda p, s=s: sd_capsule(p, (s * 0.088, 0.075, 0.01), (s * 0.088, 0.15, 0.0), 0.078), leg_color, leg_mask)
        leg.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.09, 0.045, 0.04), (0.08, 0.052, 0.095)), leg_color, leg_mask)


def head_part(m, fur_tint, r=HR):
    h = m.part("head", tint=fur_tint, group="fur", k=0.035)
    h.add(lambda p, r=r: sd_ellipsoid(p, HC, r), WHITE, 1.0)
    return h


def face_part(m):
    return m.part("face", k=0.0, step=0.0055, box=FACE_BOX)


def ears_part(m, fur_tint):
    return m.part("ears", tint=fur_tint, group="fur", k=0.03)


def tail_part(m, fur_tint, pivot):
    return m.part("tail", tint=fur_tint, group="fur", pivot=pivot, k=0.03)


def eyes(face, x=0.13, y=0.75, size=(0.037, 0.054), hr=HR, style="dot", hl=True):
    for s in (-1, 1):
        ex = s * x
        if style == "dot":
            stamp(face, HC, hr, ex, y, size, DARK)
            if hl:
                stamp(face, HC, hr, ex - s * 0.012, y + 0.02, (0.012, 0.012), WHITE, out=0.007, depth=0.006)
        elif style == "sparkle":
            stamp(face, HC, hr, ex, y, (size[0] * 1.15, size[1] * 1.15), DARK)
            stamp(face, HC, hr, ex - s * 0.013, y + 0.022, (0.015, 0.015), WHITE, out=0.008, depth=0.006)
            stamp(face, HC, hr, ex + s * 0.012, y - 0.02, (0.008, 0.008), WHITE, out=0.008, depth=0.006)
        elif style == "happy":
            curve(face, HC, hr, arc_xy(ex, y - 0.012, 0.032, 0.03, a0=20, a1=160), 0.0075, DARK)
        elif style == "sleepy":
            curve(face, HC, hr, arc_xy(ex, y + 0.01, 0.032, 0.022, a0=200, a1=340), 0.0075, DARK)
        elif style == "lash":
            stamp(face, HC, hr, ex, y, size, DARK)
            stamp(face, HC, hr, ex - s * 0.012, y + 0.02, (0.012, 0.012), WHITE, out=0.007, depth=0.006)
            for k in (0, 1):
                curve(face, HC, hr, [(ex + s * (0.03 + 0.01 * k), y + 0.03 + 0.012 * k),
                                     (ex + s * (0.048 + 0.012 * k), y + 0.045 + 0.016 * k)], 0.005, DARK)


def blush(head, y=0.665, x=0.205):
    for s in (-1, 1):
        head.paint(lambda p, s=s: sd_ellipsoid(p, (s * x, y, 0.22), (0.055, 0.032, 0.09)), PINK, 0.0, soft=0.022)


def mouth_w(face, mc, mr, y, w=0.02, h=0.016, rad=0.006):
    curve(face, mc, mr, arc_xy(-w, y, w, h, a0=200, a1=360), rad, DARK)
    curve(face, mc, mr, arc_xy(w, y, w, h, a0=180, a1=340), rad, DARK)


# ---------------------------------------------------------------- 종별 머리
def sp_bear(m, t):
    head = head_part(m, t); face = face_part(m); ears = ears_part(m, t)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.235, 1.0, -0.04), (0.095, 0.095, 0.07)), WHITE, 1.0)
        ears.paint(lambda p, s=s: sd_sphere(p, (s * 0.24, 1.0, 0.03), 0.055), (0.86, 0.7, 0.62), 0.45, soft=0.012)
    mc, mr = (0, 0.655, 0.25), (0.135, 0.095, 0.09)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), CREAM, 0.25)
    stamp(face, mc, mr, 0, 0.7, (0.045, 0.03), DARK, depth=0.03, out=0.012)
    curve(face, mc, mr, [(0, 0.69), (0, 0.66)], 0.006, DARK)
    mouth_w(face, mc, mr, 0.655, 0.022, 0.018)
    eyes(face, x=0.135, y=0.77)
    blush(head)
    tail = tail_part(m, t, (0, 0.2, -0.16))
    tail.add(lambda p: sd_sphere(p, (0, 0.2, -0.18), 0.05), WHITE, 1.0)
    return dict(top=1.04, tallEars=False)


def sp_cat(m, t):
    hr = np.array([0.35, 0.3, 0.3])
    head = head_part(m, t, r=hr); face = face_part(m); ears = ears_part(m, t)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_roundcone(p, (s * 0.2, 0.9, -0.03), (s * 0.27, 1.1, -0.05), 0.09, 0.02), WHITE, 1.0)
        ears.paint(lambda p, s=s: sd_roundcone(p, (s * 0.205, 0.92, 0.03), (s * 0.255, 1.06, 0.0), 0.045, 0.01), PINK, 0.0, soft=0.012)
        for dy, dd in ((-0.015, -0.01), (0.012, 0.012)):
            curve(face, HC, hr, [(s * 0.17, 0.66 + dy), (s * 0.27, 0.665 + dy + dd)], 0.004, DARK)
    mc, mr = (0, 0.655, 0.245), (0.09, 0.055, 0.065)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), (1, 0.98, 0.95), 0.12)
    stamp(face, mc, mr, 0, 0.685, (0.024, 0.016), (0.96, 0.52, 0.56), depth=0.02, out=0.008)
    mouth_w(face, mc, mr, 0.66, 0.018, 0.016, 0.0055)
    eyes(face, x=0.135, y=0.77, size=(0.04, 0.058), hr=hr)
    blush(head)
    tail = tail_part(m, t, (0, 0.2, -0.15))
    tail.add(lambda p: sd_roundcone(p, (0, 0.2, -0.15), (0.06, 0.38, -0.3), 0.04, 0.035), WHITE, 1.0)
    tail.add(lambda p: sd_roundcone(p, (0.06, 0.38, -0.3), (0.0, 0.5, -0.28), 0.035, 0.032), WHITE, 1.0)
    return dict(top=1.02, tallEars=True)


def sp_rabbit(m, t):
    hr = np.array([0.33, 0.31, 0.3])
    head = head_part(m, t, r=hr); face = face_part(m); ears = ears_part(m, t)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.11, 1.2, -0.04), (0.07, 0.22, 0.045), rot(z=-s * 8)), WHITE, 1.0)
        ears.paint(lambda p, s=s: sd_ellipsoid(p, (s * 0.11, 1.2, 0.0), (0.04, 0.17, 0.03), rot(z=-s * 8)), PINK, 0.0, soft=0.012)
    mc, mr = (0, 0.645, 0.25), (0.08, 0.055, 0.06)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), (1, 0.98, 0.96), 0.1)
    stamp(face, mc, mr, 0, 0.675, (0.022, 0.016), (0.95, 0.48, 0.55), depth=0.02, out=0.007)
    curve(face, mc, mr, [(0, 0.665), (0, 0.64)], 0.005, DARK)
    mouth_w(face, mc, mr, 0.64, 0.014, 0.012, 0.005)
    eyes(face, x=0.13, y=0.76, size=(0.036, 0.055), hr=hr)
    blush(head)
    tail = tail_part(m, t, (0, 0.2, -0.16))
    tail.add(lambda p: sd_sphere(p, (0, 0.21, -0.185), 0.065), WHITE, 0.3)
    return dict(top=1.04, tallEars=True)


def sp_dog(m, t):
    head = head_part(m, t); face = face_part(m); ears = ears_part(m, t)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.33, 0.8, -0.02), (0.07, 0.15, 0.1), rot(z=s * 18)), (0.8, 0.8, 0.8), 1.0)
    mc, mr = (0, 0.645, 0.26), (0.14, 0.1, 0.1)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), CREAM, 0.3)
    stamp(face, mc, mr, 0, 0.7, (0.05, 0.035), DARK, depth=0.035, out=0.014)
    mouth_w(face, mc, mr, 0.645, 0.025, 0.022, 0.0065)
    stamp(face, mc, mr, 0, 0.6, (0.022, 0.025), (0.95, 0.5, 0.55), depth=0.01, out=0.005)
    eyes(face, x=0.135, y=0.775)
    blush(head)
    tail = tail_part(m, t, (0, 0.22, -0.15))
    tail.add(lambda p: sd_roundcone(p, (0, 0.22, -0.15), (0, 0.36, -0.25), 0.035, 0.045), WHITE, 1.0)
    return dict(top=1.04, tallEars=False)


def sp_fox(m, t):
    hr = np.array([0.35, 0.3, 0.3])
    head = head_part(m, t, r=hr); face = face_part(m); ears = ears_part(m, t)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_roundcone(p, (s * 0.19, 0.9, -0.03), (s * 0.27, 1.14, -0.06), 0.095, 0.022), WHITE, 1.0)
        ears.paint(lambda p: p[:, 1] - 1.07, (0.25, 0.17, 0.14), 0.0, soft=0.02)
        ears.paint(lambda p, s=s: sd_roundcone(p, (s * 0.2, 0.92, 0.03), (s * 0.255, 1.06, 0.0), 0.045, 0.012), (0.99, 0.94, 0.88), 0.0, soft=0.012)
        head.paint(lambda p, s=s: sd_ellipsoid(p, (s * 0.15, 0.64, 0.2), (0.15, 0.1, 0.15)), (1.0, 0.97, 0.92), 0.0, soft=0.03)
    mc, mr = (0, 0.64, 0.27), (0.1, 0.075, 0.11)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), (1.0, 0.97, 0.92), 0.0)
    stamp(face, mc, mr, 0, 0.675, (0.03, 0.022), DARK, depth=0.025, out=0.01)
    mouth_w(face, mc, mr, 0.645, 0.017, 0.015, 0.0055)
    eyes(face, x=0.14, y=0.77, hr=hr, style="happy")
    blush(head, x=0.22)
    tail = tail_part(m, t, (0, 0.2, -0.15))
    tail.add(lambda p: sd_ellipsoid(p, (0.05, 0.32, -0.3), (0.1, 0.18, 0.1), rot(x=30, z=-15)), WHITE, 1.0)
    tail.paint(lambda p: sd_sphere(p, (0.11, 0.46, -0.37), 0.08), (1, 0.97, 0.93), 0.0, soft=0.02)
    return dict(top=1.02, tallEars=True)


def sp_panda(m, t):
    head = head_part(m, t); face = face_part(m); ears = ears_part(m, t)
    blk = (0.2, 0.18, 0.2)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.235, 1.0, -0.04), (0.095, 0.095, 0.07)), blk, 0.0)
        head.paint(lambda p, s=s: sd_ellipsoid(p, (s * 0.135, 0.755, 0.27), (0.075, 0.09, 0.08), rot(z=s * 25)), blk, 0.0, soft=0.012)
    mc, mr = (0, 0.65, 0.25), (0.12, 0.085, 0.085)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), WHITE, 1.0)
    stamp(face, mc, mr, 0, 0.69, (0.04, 0.026), DARK, depth=0.028, out=0.011)
    mouth_w(face, mc, mr, 0.65)
    for s in (-1, 1):
        stamp(face, HC, HR, s * 0.135, 0.765, (0.03, 0.04), (0.1, 0.08, 0.1), out=0.006)
        stamp(face, HC, HR, s * 0.135 - s * 0.01, 0.782, (0.012, 0.012), WHITE, out=0.009, depth=0.006)
    blush(head)
    tail = tail_part(m, t, (0, 0.2, -0.16))
    tail.add(lambda p: sd_sphere(p, (0, 0.2, -0.18), 0.045), WHITE, 1.0)
    return dict(top=1.04, tallEars=False, legColor=blk, handColor=blk)


def sp_lion(m, t):
    head = head_part(m, t); face = face_part(m); ears = ears_part(m, t)
    mane = m.part("mane", tint=(0.72, 0.36, 0.16), group="mane", k=0.04)
    for i in range(14):
        a = np.radians(i * 360 / 14)
        cx, cy = 0.37 * np.cos(a), 0.74 + 0.35 * np.sin(a)
        mane.add(lambda p, cx=cx, cy=cy: sd_sphere(p, (cx, cy, -0.06), 0.12), WHITE, 1.0)
    mane.add(lambda p: sd_ellipsoid(p, (0, 0.74, -0.12), (0.38, 0.36, 0.2)), WHITE, 1.0)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.22, 1.0, 0.02), (0.07, 0.07, 0.05)), WHITE, 1.0)
    mc, mr = (0, 0.65, 0.255), (0.13, 0.09, 0.09)
    head.add(lambda p: sd_ellipsoid(p, mc, mr), CREAM, 0.25)
    stamp(face, mc, mr, 0, 0.695, (0.045, 0.03), (0.45, 0.25, 0.2), depth=0.03, out=0.012)
    mouth_w(face, mc, mr, 0.655, 0.022, 0.018)
    eyes(face, x=0.135, y=0.77)
    for s in (-1, 1):
        curve(face, HC, HR, [(s * 0.1, 0.84), (s * 0.17, 0.85)], 0.008, (0.55, 0.3, 0.15))
    blush(head)
    tail = tail_part(m, t, (0, 0.2, -0.15))
    tail.add(lambda p: sd_capsule(p, (0, 0.2, -0.15), (0.05, 0.3, -0.33), 0.025), WHITE, 1.0)
    tail.add(lambda p: sd_sphere(p, (0.06, 0.32, -0.37), 0.055), (0.72, 0.36, 0.16), 0.0)
    return dict(top=1.1, tallEars=False)


def sp_pig(m, t):
    hr = np.array([0.35, 0.3, 0.3])
    head = head_part(m, t, r=hr); face = face_part(m); ears = ears_part(m, t)
    for s in (-1, 1):
        ears.add(lambda p, s=s: sd_roundcone(p, (s * 0.2, 0.93, 0.0), (s * 0.29, 1.0, 0.1), 0.07, 0.02), WHITE, 1.0)
    sc = (0, 0.655, 0.29)
    head.add(lambda p: sd_cylinder(p, sc, 0.04, 0.09, 0.025, rot(x=90)), (1.0, 0.88, 0.88), 0.6)
    for s in (-1, 1):
        face.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.03, 0.655, 0.33), (0.014, 0.022, 0.01)), (0.55, 0.28, 0.3), 0.0, k=0)
    curve(face, HC, hr, arc_xy(0, 0.585, 0.04, 0.02, a0=200, a1=340), 0.006, DARK)
    eyes(face, x=0.15, y=0.77, hr=hr)
    blush(head, x=0.22)
    tail = tail_part(m, t, (0, 0.2, -0.16))
    for i in range(10):
        a0, a1 = np.radians(i * 60), np.radians((i + 1) * 60)
        p0 = (0.04 * np.cos(a0), 0.24 + 0.04 * np.sin(a0) + i * 0.004, -0.19 - i * 0.008)
        p1 = (0.04 * np.cos(a1), 0.24 + 0.04 * np.sin(a1) + (i + 1) * 0.004, -0.19 - (i + 1) * 0.008)
        tail.add(lambda p, p0=p0, p1=p1: sd_capsule(p, p0, p1, 0.016), WHITE, 1.0, k=0.01)
    return dict(top=1.02, tallEars=False)


HUMAN_HR = np.array([0.32, 0.31, 0.29])


def sp_human(m, t):
    head = head_part(m, t, r=HUMAN_HR)
    nose = m.part("nose", tint=t, group="fur", k=0.0)
    nose.add(lambda p: sd_ellipsoid(p, (0, 0.685, 0.285), (0.025, 0.02, 0.02)), (0.98, 0.92, 0.9), 1.0)
    for s in (-1, 1):
        head.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.31, 0.71, 0.0), (0.045, 0.065, 0.045)), WHITE, 1.0)
    blush(head, y=0.66, x=0.19)
    return dict(top=1.07, tallEars=False)


SPECIES = {
    "bear": (sp_bear, (0.72, 0.5, 0.34)), "cat": (sp_cat, (0.98, 0.76, 0.48)), "rabbit": (sp_rabbit, (0.98, 0.97, 0.96)),
    "dog": (sp_dog, (0.92, 0.78, 0.58)), "fox": (sp_fox, (0.95, 0.55, 0.28)), "panda": (sp_panda, (0.98, 0.98, 0.97)),
    "lion": (sp_lion, (0.97, 0.8, 0.45)), "pig": (sp_pig, (0.99, 0.75, 0.75)), "human": (sp_human, (1.0, 0.86, 0.76)),
}


def build_species(name):
    fn, fur = SPECIES[name]
    m = Model("char_" + name)
    info = fn(m, fur)
    build_body(m, fur, (0.86, 0.24, 0.24),
               leg_color=info.get("legColor", WHITE), leg_mask=0.0 if "legColor" in info else 1.0,
               hand_color=info.get("handColor", WHITE), hand_mask=0.0 if "handColor" in info else 1.0)
    m.build(LO, HI, STEP)
    info["fur"] = fur
    info.pop("legColor", None); info.pop("handColor", None)
    return m, info


# ---------------------------------------------------------------- 사람 얼굴 5종
FACES = {"f1": ("dot", "smile"), "f2": ("happy", "open"), "f3": ("sparkle", "small"), "f4": ("sleepy", "o"), "f5": ("lash", "smile")}


def build_face(key):
    style, mouth = FACES[key]
    m = Model("face_" + key)
    face = face_part(m)
    eyes(face, x=0.12, y=0.725, size=(0.032, 0.048), hr=HUMAN_HR, style=style)
    for s in (-1, 1):
        curve(face, HC, HUMAN_HR, arc_xy(s * 0.12, 0.8, 0.032, 0.012, n=6, a0=30, a1=150), 0.0055, (0.42, 0.28, 0.2))
    if mouth == "smile":
        curve(face, HC, HUMAN_HR, arc_xy(0, 0.635, 0.035, 0.02, a0=200, a1=340), 0.0065, (0.5, 0.22, 0.22))
    elif mouth == "open":
        stamp(face, HC, HUMAN_HR, 0, 0.625, (0.03, 0.02), (0.65, 0.25, 0.28), depth=0.008)
    elif mouth == "small":
        curve(face, HC, HUMAN_HR, arc_xy(0, 0.635, 0.02, 0.012, a0=210, a1=330), 0.006, (0.5, 0.22, 0.22))
    elif mouth == "o":
        stamp(face, HC, HUMAN_HR, 0, 0.625, (0.014, 0.016), (0.6, 0.25, 0.28), depth=0.008)
    m.build(LO, HI, STEP, ao_scene=False)
    return m


# ---------------------------------------------------------------- 머리카락
def build_hair(style):
    m = Model("hair_" + style)
    h = m.part("hair", tint=(0.45, 0.28, 0.18), group="hair", k=0.035)
    face_hole = lambda p: sd_ellipsoid(p, (0, 0.6, 0.22), (0.31, 0.24, 0.3))
    h.add(lambda p: sd_ellipsoid(p, (0, 0.775, -0.03), (0.345, 0.31, 0.32)), WHITE, 1.0)
    if style in ("long", "bob", "twin", "pony", "bun"):
        h.add(lambda p: sd_ellipsoid(p, (-0.1, 0.87, 0.19), (0.16, 0.075, 0.11), rot(z=10)), WHITE, 1.0)
        h.add(lambda p: sd_ellipsoid(p, (0.12, 0.865, 0.18), (0.14, 0.07, 0.11), rot(z=-14)), WHITE, 1.0)
    if style == "long":
        h.add(lambda p: sd_ellipsoid(p, (0, 0.56, -0.12), (0.33, 0.33, 0.2)), WHITE, 1.0)
        for s in (-1, 1):
            h.add(lambda p, s=s: sd_capsule(p, (s * 0.29, 0.72, 0.06), (s * 0.29, 0.42, 0.03), 0.075), WHITE, 1.0)
    elif style == "bob":
        h.add(lambda p: sd_ellipsoid(p, (0, 0.64, -0.06), (0.37, 0.24, 0.29)), WHITE, 1.0)
    elif style == "twin":
        for s in (-1, 1):
            h.add(lambda p, s=s: sd_sphere(p, (s * 0.33, 0.78, -0.08), 0.06), (0.92, 0.32, 0.38), 0.0)
            h.add(lambda p, s=s: sd_roundcone(p, (s * 0.37, 0.76, -0.1), (s * 0.43, 0.48, -0.12), 0.1, 0.06), WHITE, 1.0)
    elif style == "pony":
        h.add(lambda p: sd_sphere(p, (0, 0.92, -0.3), 0.05), (0.92, 0.32, 0.38), 0.0)
        h.add(lambda p: sd_roundcone(p, (0, 0.9, -0.34), (0, 0.55, -0.42), 0.11, 0.06), WHITE, 1.0)
    elif style == "bun":
        h.add(lambda p: sd_sphere(p, (0, 1.1, -0.1), 0.12), WHITE, 1.0)
        h.add(lambda p: sd_torus(p, (0, 1.0, -0.08), 0.08, 0.025, rot(x=-15)), (0.92, 0.32, 0.38), 0.0)
    elif style == "short":
        h.add(lambda p: sd_ellipsoid(p, (0.04, 0.88, 0.17), (0.22, 0.07, 0.12), rot(z=-10)), WHITE, 1.0)
    elif style == "spiky":   # 옆으로 넘긴 덥수룩한 앞머리
        for x, y, z, rz in [(-0.16, 0.9, 0.16, 25), (-0.02, 0.93, 0.2, 10), (0.13, 0.91, 0.18, -5), (0.25, 0.85, 0.1, -20)]:
            h.add(lambda p, x=x, y=y, z=z, rz=rz: sd_ellipsoid(p, (x, y, z), (0.1, 0.06, 0.09), rot(z=rz, x=-10)), WHITE, 1.0)
        h.add(lambda p: sd_roundcone(p, (0.05, 1.03, 0.0), (0.12, 1.1, 0.05), 0.05, 0.02), WHITE, 1.0)   # 삐친 머리
    h.cut(face_hole, 0.04)
    h.paint(lambda p: sd_ellipsoid(p, (0.06, 0.99, 0.06), (0.14, 0.03, 0.22), rot(z=-10)), (1, 1, 1), 0.5, soft=0.025)
    m.build(LO, HI, STEP, ao_scene=False)
    return m


# ---------------------------------------------------------------- 모자·목도리
def arc(part, c, R, r, color, mask, a0=0, a1=180, n=16):
    angs = np.radians(np.linspace(a0, a1, n))
    pts = [(c[0] + R * np.cos(a), c[1] + R * np.sin(a), c[2]) for a in angs]
    for a, b in zip(pts[:-1], pts[1:]):
        part.add(lambda p, a=a, b=b: sd_capsule(p, a, b, r), color, mask, k=0.01)


TOP = 1.04   # 모자 기준 정수리 높이 (종마다 Unity에서 y 오프셋으로 맞춤)
HAT_TINTS = {"santa": (0.86, 0.2, 0.2), "beanie": (0.95, 0.82, 0.35), "earmuffs": (0.97, 0.6, 0.7),
             "antlers": (0.62, 0.42, 0.26), "tophat": (0.85, 0.2, 0.22), "elf": (0.3, 0.62, 0.38)}


def build_hat(kind):
    m = Model("hat_" + kind)
    top = TOP
    h = m.part("hat", tint=HAT_TINTS[kind], group="hat", k=0.025)
    if kind == "santa":
        h.add(lambda p: sd_roundcone(p, (0, top - 0.03, -0.02), (0.07, top + 0.24, -0.17), 0.22, 0.04), WHITE, 1.0)
        h.add(lambda p: sd_sphere(p, (0.085, top + 0.23, -0.21), 0.065), (0.98, 0.98, 0.96), 0.0)
        h.add(lambda p: sd_torus(p, (0, top - 0.05, -0.02), 0.225, 0.055, rot(x=-10)), (0.98, 0.98, 0.96), 0.0)
    elif kind == "beanie":
        h.add(lambda p: sd_ellipsoid(p, (0, top - 0.06, -0.01), (0.33, 0.22, 0.31)), WHITE, 1.0)
        h.cut(lambda p: p[:, 1] - (top - 0.12))
        h.add(lambda p: sd_torus(p, (0, top - 0.12, -0.01), 0.31, 0.05), (0.88, 0.88, 0.88), 1.0)
        h.add(lambda p: sd_sphere(p, (0, top + 0.17, -0.02), 0.075), (0.98, 0.98, 0.96), 0.0)
        h.paint(lambda p: np.abs(np.sin(np.arctan2(p[:, 0], p[:, 2]) * 10)) - 0.22 + (p[:, 1] < top - 0.08) * 5 + (p[:, 1] > top + 0.1) * 5, (0.82, 0.82, 0.82), 1.0, soft=0.08)
    elif kind == "earmuffs":
        arc(h, (0, 0.74, -0.02), 0.355, 0.024, (0.85, 0.82, 0.8), 0.0, a0=8, a1=172)
        for s in (-1, 1):
            h.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.345, 0.74, -0.01), (0.06, 0.095, 0.095)), WHITE, 1.0)
    elif kind == "antlers":
        arc(h, (0, top - 0.3, -0.03), 0.31, 0.02, (0.55, 0.32, 0.2), 0.0, a0=25, a1=155)
        for s in (-1, 1):
            h.add(lambda p, s=s: sd_roundcone(p, (s * 0.15, top - 0.03, -0.04), (s * 0.24, top + 0.22, -0.06), 0.034, 0.025), WHITE, 1.0)
            h.add(lambda p, s=s: sd_roundcone(p, (s * 0.2, top + 0.09, -0.05), (s * 0.34, top + 0.16, -0.04), 0.024, 0.019), WHITE, 1.0)
            h.add(lambda p, s=s: sd_roundcone(p, (s * 0.22, top + 0.15, -0.05), (s * 0.18, top + 0.29, -0.08), 0.022, 0.017), WHITE, 1.0)
            h.add(lambda p, s=s: sd_sphere(p, (s * 0.13, top + 0.0, 0.0), 0.035), (0.9, 0.25, 0.25), 0.0)
    elif kind == "tophat":
        h.add(lambda p: sd_cylinder(p, (0, top - 0.01, -0.02), 0.02, 0.31, 0.014, rot(x=-8)), (0.2, 0.17, 0.18), 0.0)
        h.add(lambda p: sd_cylinder(p, (0, top + 0.13, -0.035), 0.14, 0.18, 0.035, rot(x=-8)), (0.2, 0.17, 0.18), 0.0)
        h.add(lambda p: sd_cylinder(p, (0, top + 0.035, -0.027), 0.035, 0.185, 0.012, rot(x=-8)), WHITE, 1.0)
        h.add(lambda p: sd_sphere(p, (0.15, top + 0.06, 0.06), 0.03), (0.3, 0.6, 0.35), 0.0)
        h.add(lambda p: sd_sphere(p, (0.17, top + 0.04, 0.09), 0.018), (0.9, 0.2, 0.2), 0.0)
    elif kind == "elf":
        h.add(lambda p: sd_roundcone(p, (0, top - 0.03, -0.02), (0.0, top + 0.3, -0.25), 0.21, 0.035), WHITE, 1.0)
        h.add(lambda p: sd_roundcone(p, (0.0, top + 0.3, -0.25), (0.08, top + 0.28, -0.4), 0.035, 0.02), WHITE, 1.0)
        h.add(lambda p: sd_sphere(p, (0.09, top + 0.27, -0.42), 0.04), (0.98, 0.8, 0.3), 0.0)
        h.add(lambda p: sd_torus(p, (0, top - 0.05, -0.02), 0.215, 0.05, rot(x=-10)), (0.9, 0.25, 0.25), 0.0)
    m.build(LO, HI, STEP, ao_scene=False)
    return m


def build_scarf():
    m = Model("scarf")
    sc = m.part("scarf", tint=(0.98, 0.96, 0.92), group="scarf", k=0.025)
    sc.add(lambda p: sd_torus(p, (0, 0.465, 0.0), 0.14, 0.05, rot(x=-6)), WHITE, 1.0)
    sc.add(lambda p: sd_roundbox(p, (0.08, 0.37, 0.145), (0.05, 0.095, 0.02), 0.018, rot(x=-12, z=8)), WHITE, 1.0)
    sc.add(lambda p: sd_roundbox(p, (0.12, 0.39, 0.13), (0.045, 0.075, 0.02), 0.018, rot(x=-10, z=25)), WHITE, 1.0)
    sc.paint(lambda p: (np.abs(p[:, 1] - 0.315) - 0.012) + (p[:, 2] < 0.08) * 5, (0.85, 0.85, 0.85), 1.0, soft=0.004)
    m.build(LO, HI, STEP, ao_scene=False)
    return m
