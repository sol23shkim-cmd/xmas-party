"""방 껍데기 조각 (바닥·벽·문·계단) + 바깥 집. Unity에서 격자(2m)로 이어 붙임."""
from assets import *

FLOOR = (0.78, 0.56, 0.36)
WALL = (0.97, 0.9, 0.8)
WAIN = (0.55, 0.36, 0.26)
SNOW = (0.98, 0.98, 1.0)


def R(name, lo, hi, step, desc, **kw):
    return asset(name, "room", lo, hi, step, desc, ao=False, **kw)


@R("floor_planks", (-1.02, -0.06, -1.02), (1.02, 0.02, 1.02), 0.01, "나무 마루 바닥 2x2m", tile=2.0)
def floor(m):
    f = m.part("floor", tint=FLOOR, group="floor", k=0.0)
    f.add(lambda p: sd_box(p, (0, -0.03, 0), (1.0, 0.03, 1.0)), WHITE, 1.0)
    def seams(p):
        row = np.floor((p[:, 2] + 1) / 0.25)
        x = p[:, 0] + np.mod(row * 0.37, 1.0)
        dz = np.abs(np.mod(p[:, 2] + 1, 0.25) - 0.125) - 0.12
        dx = (np.abs(np.mod(x, 1.0) - 0.5) - 0.495)
        return np.maximum(np.maximum(-dz, -dx * 1.0) * -1, -1) * 0 + np.maximum(np.minimum(-dz, -dx), p[:, 1] - 0.0 + 0.006) * 0 + \
            np.maximum(np.maximum(dz, dx) * -1, -(p[:, 1] + 0.006) * -1) * 0 + np.maximum(-np.maximum(dz, dx), -0.006 - p[:, 1])
    f.cut(lambda p: np.maximum(-np.maximum(np.abs(np.mod(p[:, 2] + 1, 0.25) - 0.125) - 0.119,
                                        np.abs(np.mod(p[:, 0] + np.mod(np.floor((p[:, 2] + 1) / 0.25) * 0.37, 1.0), 1.0) - 0.5) - 0.494),
                              -0.007 - p[:, 1]) * -1 * -1, 0.0)
    for i in range(8):   # 판자마다 색 조금씩 다르게 (판자 하나 = 큰 면이라 단순화해도 깔끔)
        z0 = -1 + i * 0.25
        f.paint(lambda p, z0=z0: np.abs(p[:, 2] - z0 - 0.125) - 0.11 + (np.sin(np.floor((p[:, 0] + (i * 0.37) % 1.0)) * 3.1 + i) > 0.3) * 5,
                (0.9, 0.9, 0.9), 1.0, soft=0.002)
    f.reduce = 0.06


@R("floor_tiles", (-1.03125, -0.09375, -1.03125), (1.03125, 0.03125, 1.03125), 0.03125, "부엌 타일 바닥 2x2m (체크)", tile=2.0)
def floor_t(m):
    f = m.part("floor", tint=(0.95, 0.93, 0.88), group="floor", k=0.0)
    f.add(lambda p: sd_box(p, (0, -0.03, 0), (1.0, 0.03, 1.0)), WHITE, 1.0)
    f.paint(lambda p: (np.mod(np.floor(p[:, 0] / 0.25) + np.floor(p[:, 2] / 0.25), 2) - 0.5) * 0.02, (0.55, 0.72, 0.75), 0.0, soft=0.0005)
    f.reduce = 1.0


@R("wall_plain", (-1.02, -0.02, -0.12), (1.02, 3.52, 0.12), 0.014, "벽 2m (판자 징두리), 원점=바닥 중앙, 안쪽=+z", tile=2.0)
def wall(m):
    w = m.part("wall", tint=WALL, group="wall", k=0.0)
    w.add(lambda p: sd_box(p, (0, 1.75, -0.05), (1.0, 1.75, 0.05)), WHITE, 1.0)
    w.reduce = 0.03
    wn = m.part("wainscot", tint=WAIN, group="wainscot", k=0.008)
    wn.add(lambda p: sd_box(p, (0, 0.5, 0.0), (1.0, 0.5, 0.02)), WHITE, 1.0)
    for x in (-0.75, -0.25, 0.25, 0.75):   # 판넬 (도드라진 사각형)
        wn.add(lambda p, x=x: sd_roundbox(p, (x, 0.55, 0.025), (0.2, 0.33, 0.012), 0.01), WHITE, 1.0)
    wn.add(lambda p: sd_roundbox(p, (0, 1.02, 0.02), (1.0, 0.035, 0.035), 0.012), WHITE, 1.0)
    wn.add(lambda p: sd_roundbox(p, (0, 0.06, 0.02), (1.0, 0.06, 0.03), 0.01), WHITE, 1.0)
    wn.reduce = 0.08
    cr = m.part("crown", tint=(0.98, 0.96, 0.92), group="trim", k=0.01)
    cr.add(lambda p: sd_roundbox(p, (0, 3.45, -0.0), (1.0, 0.05, 0.05), 0.02), WHITE, 1.0)
    cr.reduce = 0.08


@R("wall_window", (-1.02, -0.02, -0.15), (1.02, 3.52, 0.2), 0.016, "창 달린 벽 2m (밖은 눈 풍경)", tile=2.0)
def wall_window(m):
    wall(m)
    for p_ in m.parts:
        if p_.name in ("wall",):
            p_.cut(lambda p: sd_roundbox(p, (0, 1.85, 0), (0.62, 0.72, 0.3), 0.02), 0.0)
        if p_.name == "wainscot":
            pass
    fr = m.part("frame", tint=(0.98, 0.96, 0.92), group="trim", k=0.01)
    fr.add(lambda p: sd_roundbox(p, (0, 1.85, -0.03), (0.68, 0.78, 0.06), 0.03), WHITE, 1.0)
    for x in (-0.31, 0.31):
        for y in (1.48, 2.22):
            fr.cut(lambda p, x=x, y=y: sd_roundbox(p, (x, y, 0), (0.27, 0.33, 0.2), 0.02), 0.0)
    fr.add(lambda p: sd_roundbox(p, (0, 1.06, 0.06), (0.75, 0.035, 0.1), 0.02), WHITE, 1.0)
    g = m.part("glass", gloss=1.0, k=0.0)
    g.add(lambda p: sd_box(p, (0, 1.85, -0.06), (0.62, 0.72, 0.008)), (0.75, 0.87, 0.97), 0.0)
    g.paint(lambda p: p[:, 1] - 1.4 - 0.08 * np.sin(p[:, 0] * 6), SNOW, 0.0, soft=0.01)
    sn = m.part("snow", k=0.02)
    for x in (-0.31, 0.31):
        for y in (1.15, 1.89):
            sn.add(lambda p, x=x, y=y: sd_ellipsoid(p, (x, y, -0.04), (0.26, 0.035, 0.03)), SNOW, 0.0)
    return dict(emissiveParts=["glass"])


@R("wall_door", (-1.02, -0.02, -0.2), (1.02, 3.52, 0.25), 0.016, "현관문 벽 2m (둥근 나무문, 리스 자리)", tile=2.0, attach_wreath=[0, 1.75, 0.12])
def wall_door(m):
    wall(m)
    for p_ in m.parts:
        if p_.name in ("wall", "wainscot"):
            p_.cut(lambda p: smin(sd_box(p, (0, 1.0, 0), (0.55, 1.0, 0.3)), sd_cylinder(p, (0, 2.0, 0), 0.3, 0.55, 0.0, rot(x=90)), 0.0), 0.0)
    fr = m.part("frame", tint=(0.98, 0.96, 0.92), group="trim", k=0.015)
    def arch(p, r_out, r_in, depth):
        q = p.copy()
        outer = smin(sd_box(q, (0, 1.0, 0), (r_out, 1.0, depth)), sd_cylinder(q, (0, 2.0, 0), depth, r_out, 0.0, rot(x=90)), 0.0)
        inner = smin(sd_box(q, (0, 1.0, 0), (r_in, 1.05, depth + 0.1)), sd_cylinder(q, (0, 2.0, 0), depth + 0.1, r_in, 0.0, rot(x=90)), 0.0)
        return np.maximum(outer, -inner)
    fr.add(lambda p: arch(p, 0.66, 0.55, 0.08), WHITE, 1.0)
    d = m.part("door", tint=(0.62, 0.38, 0.26), group="door", k=0.01, pivot=(-0.55, 0, -0.02))
    d.add(lambda p: smin(sd_box(p, (0, 1.0, -0.02), (0.55, 1.0, 0.04)), sd_cylinder(p, (0, 2.0, -0.02), 0.04, 0.55, 0.0, rot(x=90)), 0.0), WHITE, 1.0)
    d.paint(lambda p: np.abs(np.mod(p[:, 0] + 0.55, 0.22) - 0.11) - 0.1 + (p[:, 2] < 0.0) * 5, (0.82, 0.82, 0.82), 1.0, soft=0.005)
    d.reduce = 0.6
    d.add(lambda p: sd_sphere(p, (0.4, 1.05, 0.05), 0.04), (0.95, 0.78, 0.4), 0.0)
    d.add(lambda p: sd_roundbox(p, (0, 0.5, 0.025), (0.5, 0.04, 0.02), 0.01), (0.25, 0.22, 0.22), 0.0)
    d.add(lambda p: sd_roundbox(p, (0, 1.6, 0.025), (0.5, 0.04, 0.02), 0.01), (0.25, 0.22, 0.22), 0.0)
    w = m.part("doorwindow", k=0.0)
    w.add(lambda p: sd_cylinder(p, (0, 2.1, 0.03), 0.01, 0.15, 0.0, rot(x=90)), (1.0, 0.92, 0.65), 0.0)
    return dict(door=True)


@R("wall_corner", (-0.15, -0.02, -0.15), (0.15, 3.52, 0.15), 0.012, "벽 모서리 기둥")
def corner(m):
    c = m.part("post", tint=(0.98, 0.96, 0.92), group="trim", k=0.01)
    c.add(lambda p: sd_roundbox(p, (0, 1.75, 0), (0.1, 1.75, 0.1), 0.03), WHITE, 1.0)


@R("ceiling_beam", (-1.02, -0.2, -0.15), (1.02, 0.02, 0.15), 0.015, "천장 들보 2m (원점=천장 높이)", tile=2.0)
def beam(m):
    b = m.part("wood", tint=WAIN, group="wood", k=0.01)
    b.add(lambda p: sd_roundbox(p, (0, -0.09, 0), (1.0, 0.09, 0.12), 0.02), WHITE, 1.0)
    b.paint(lambda p: np.abs(np.sin(p[:, 0] * 12 + p[:, 1] * 30)) - 0.1, (0.88, 0.88, 0.88), 1.0, soft=0.2)


@R("stairs", (-0.65, -0.02, -1.6), (0.65, 3.6, 1.6), 0.02, "나무 계단 (2층 장식용)")
def stairs(m):
    s = m.part("wood", tint=(0.72, 0.5, 0.32), group="wood", k=0.01)
    for i in range(14):
        y = 0.25 * i; z = 1.4 - i * 0.2
        s.add(lambda p, y=y, z=z: sd_roundbox(p, (0, y + 0.12, z - 0.05), (0.6, 0.125, 0.12 + 0.0), 0.02), WHITE, 1.0)
        s.add(lambda p, y=y, z=z: sd_box(p, (0, y / 2 + 0.01, z - 0.05), (0.58, y / 2 + 0.01, 0.1)), WHITE, 1.0)
    rl = m.part("rail", tint=(0.98, 0.96, 0.92), group="trim", k=0.01)
    for i in range(0, 14, 2):
        y = 0.25 * i; z = 1.4 - i * 0.2
        rl.add(lambda p, y=y, z=z: sd_capsule(p, (0.55, y + 0.25, z - 0.05), (0.55, y + 1.05, z - 0.05), 0.025), WHITE, 1.0)
    rl.add(lambda p: sd_capsule(p, (0.55, 1.1, 1.4), (0.55, 4.35 - 0.8, -1.35), 0.04), WAIN, 0.0)
    rl.cut(lambda p: p[:, 1] - 3.55 + 10, 0.0) if False else None


@R("ground_snow", (-2.02, -0.12, -2.02), (2.02, 0.15, 2.02), 0.04, "눈 덮인 바깥 땅 4x4m (울퉁불퉁)", tile=4.0)
def ground_snow(m):
    g = m.part("snow", tint=SNOW, group="snow", k=0.0)
    g.add(lambda p: np.maximum(sd_box(p, (0, -0.05, 0), (2.0, 0.08, 2.0)),
                               p[:, 1] - 0.03 - 0.03 * np.sin(p[:, 0] * 2.1) * np.sin(p[:, 2] * 1.7 + 1)), WHITE, 1.0)
    g.paint(lambda p: 0.02 - p[:, 1], (0.9, 0.93, 1.0), 1.0, soft=0.05)


@R("path_stones", (-1.02, -0.05, -1.02), (1.02, 0.06, 1.02), 0.012, "징검다리 돌길 2m", tile=2.0)
def path_stones(m):
    s = m.part("stone", tint=(0.72, 0.7, 0.7), group="stone", k=0.0)
    for x, z, r in ((-0.4, -0.6, 0.28), (0.25, -0.2, 0.25), (-0.15, 0.35, 0.3), (0.45, 0.7, 0.22)):
        s.add(lambda p, x=x, z=z, r=r: sd_cylinder(p, (x, 0.0, z), 0.03, r, 0.025), WHITE, 1.0)


@asset("house_cottage", "outdoor", (-3.3, -0.05, -3.0), (3.3, 6.1, 3.2), 0.04, "눈 덮인 오두막 (바깥 배경, 창문 불빛)")
def cottage(m):
    w = m.part("walls", tint=(0.95, 0.88, 0.75), group="wall", k=0.03)
    w.add(lambda p: sd_roundbox(p, (0, 1.5, 0), (2.5, 1.5, 2.2), 0.08), WHITE, 1.0)
    w.paint(lambda p: np.abs(np.mod(p[:, 1], 0.35) - 0.175) - 0.16, (0.88, 0.88, 0.88), 1.0, soft=0.01)
    rf = m.part("roof", tint=(0.75, 0.3, 0.28), group="roof", k=0.04)
    def roof(p):
        x = np.abs(p[:, 0]); y = p[:, 1] - 3.0
        d = (x * 0.62 + y * 0.78) - 2.0
        slab = np.maximum(np.abs(d) - 0.12, np.maximum(np.abs(p[:, 2]) - 2.6, -y))
        return slab
    rf.add(roof, WHITE, 1.0)
    sn = m.part("snow", k=0.06)
    def snowroof(p):
        x = np.abs(p[:, 0]); y = p[:, 1] - 3.0
        d = (x * 0.62 + y * 0.78) - 2.22
        return np.maximum(np.abs(d) - 0.12 - 0.04 * np.sin(p[:, 2] * 3), np.maximum(np.abs(p[:, 2]) - 2.65, -y - 0.1)) - 0.03
    sn.add(snowroof, SNOW, 0.0)
    for x in np.linspace(-2.3, 2.3, 9):
        sn.add(lambda p, x=x: sd_roundcone(p, (x, 2.93, 2.65), (x, 2.75 - 0.08 * (abs(x) % 0.7), 2.66), 0.06, 0.02), (0.85, 0.93, 1.0), 0.0)
    ch = m.part("chimney", tint=(0.7, 0.38, 0.3), group="brick", k=0.02)
    ch.add(lambda p: sd_roundbox(p, (1.3, 4.6, -0.8), (0.32, 0.9, 0.32), 0.04), WHITE, 1.0)
    sn.add(lambda p: sd_ellipsoid(p, (1.3, 5.52, -0.8), (0.36, 0.08, 0.36)), SNOW, 0.0)
    dr = m.part("door", tint=(0.55, 0.33, 0.22), group="door", k=0.02)
    dr.add(lambda p: smin(sd_box(p, (0, 0.75, 2.2), (0.5, 0.75, 0.06)), sd_cylinder(p, (0, 1.5, 2.2), 0.06, 0.5, 0, rot(x=90)), 0.0), WHITE, 1.0)
    dr.add(lambda p: sd_sphere(p, (0.35, 0.9, 2.28), 0.05), (0.95, 0.78, 0.4), 0.0)
    gl = m.part("windows", k=0.02)
    for x in (-1.5, 1.5):
        gl.add(lambda p, x=x: sd_roundbox(p, (x, 1.6, 2.2), (0.48, 0.48, 0.05), 0.03), (1.0, 0.86, 0.5), 0.0)
        gl.add(lambda p, x=x: sd_roundbox(p, (x, 1.6, 2.25), (0.56, 0.56, 0.03), 0.03) if False else sd_roundbox(p, (x, 1.08, 2.3), (0.6, 0.04, 0.12), 0.02), (0.98, 0.96, 0.92), 0.0)
        for xx, yy, hx, hy in ((x, 1.6, 0.03, 0.5), (x, 1.6, 0.5, 0.03)):
            gl.add(lambda p, xx=xx, yy=yy, hx=hx, hy=hy: sd_box(p, (xx, yy, 2.24), (hx, hy, 0.03)), (0.98, 0.96, 0.92), 0.0)
    wr = m.part("wreath", k=0.02)
    for i in range(12):
        a = np.radians(i * 30)
        wr.add(lambda p, a=a: sd_sphere(p, (np.cos(a) * 0.22, 1.85 + np.sin(a) * 0.22, 2.3), 0.07), (0.3, 0.55, 0.38), 0.0)
    wr.add(lambda p: sd_sphere(p, (0, 1.65, 2.35), 0.06), (0.88, 0.22, 0.24), 0.0)
    return dict(emissiveParts=["windows"])
