"""마당(집 바깥 사방) 조형물. 원점 = 바닥 중심, 앞 = +z"""
from assets import *

ICE = (0.78, 0.9, 1.0)
WARM = (1.0, 0.86, 0.5)
SNOW = (0.98, 0.985, 1.0)


def Y(name, lo, hi, step, desc, **kw):
    return asset(name, "yard", lo, hi, step, desc, **kw)


@Y("reindeer_light", (-0.75, -0.02, -0.3), (0.75, 1.75, 0.3), 0.012, "반짝이 철사 순록", footprint=[0.5, 0.2], emissiveParts=["lights"])
def reindeer(m):
    w = m.part("wire", k=0.0, gloss=0.5)
    G = (0.92, 0.88, 0.8)
    def seg(a, b, r=0.022): w.add(lambda p, a=a, b=b: sd_capsule(p, a, b, r), G, 0.0)
    # 몸 고리
    body = [(-0.42, 0.85), (-0.2, 0.98), (0.2, 0.98), (0.42, 0.88), (0.38, 0.65), (0.0, 0.6), (-0.4, 0.65), (-0.42, 0.85)]
    for (x0, y0), (x1, y1) in zip(body[:-1], body[1:]):
        seg((x0, y0, 0), (x1, y1, 0))
    for x in (-0.33, -0.2, 0.25, 0.36):   # 다리
        seg((x, 0.64, 0), (x + 0.02, 0.0, 0), 0.02)
    seg((0.38, 0.9, 0), (0.5, 1.25, 0)); seg((0.5, 1.25, 0), (0.7, 1.22, 0)); seg((0.7, 1.22, 0), (0.62, 1.1, 0)); seg((0.62, 1.1, 0), (0.42, 0.9, 0))
    for s in (-1, 1):   # 뿔
        seg((0.5, 1.25, s * 0.05), (0.45, 1.55, s * 0.12), 0.016); seg((0.47, 1.43, s * 0.08), (0.62, 1.55, s * 0.15), 0.014)
        seg((0.45, 1.55, s * 0.12), (0.36, 1.68, s * 0.16), 0.014)
    seg((-0.42, 0.88, 0), (-0.55, 1.0, 0), 0.016)   # 꼬리
    l = m.part("lights", k=0.0)
    rng = np.random.default_rng(3)
    for (x0, y0), (x1, y1) in zip(body[:-1], body[1:]):
        for t in np.linspace(0, 1, 4)[:-1]:
            l.add(lambda p, c=(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, 0.0): sd_sphere(p, c, 0.035), WARM, 0.0)
    for c in [(0.5, 1.25, 0), (0.45, 1.55, 0.12), (0.45, 1.55, -0.12), (0.62, 1.55, 0.15), (0.62, 1.55, -0.15)]:
        l.add(lambda p, c=c: sd_sphere(p, c, 0.035), WARM, 0.0)
    l.add(lambda p: sd_sphere(p, (0.72, 1.22, 0), 0.045), (1.0, 0.35, 0.3), 0.0)   # 빨간 코


@Y("ice_sculpture", (-0.6, -0.02, -0.6), (0.6, 1.9, 0.6), 0.012, "얼음 별 조각상", footprint=[0.5, 0.5])
def ice_star(m):
    b = m.part("base", k=0.03)
    b.add(lambda p: sd_roundbox(p, (0, 0.25, 0), (0.42, 0.25, 0.42), 0.06), SNOW, 0.0)
    b.add(lambda p: sd_roundbox(p, (0, 0.56, 0), (0.3, 0.07, 0.3), 0.04), SNOW, 0.0)
    i = m.part("ice", k=0.02, gloss=0.9)
    i.add(lambda p: sd_star(p, (0, 1.2, 0), 0.55, thick=0.1, rnd=0.05), ICE, 0.0)
    i.add(lambda p: sd_cylinder(p, (0, 0.7, 0), 0.12, 0.08, 0.03), ICE, 0.0)
    i.paint(lambda p: sd_star(p, (0, 1.2, 0.08), 0.32, thick=0.04, rnd=0.02), (0.92, 0.97, 1.0), 0.0, soft=0.04)


@Y("frozen_pond", (-2.6, -0.05, -1.9), (2.6, 0.25, 1.9), 0.02, "꽁꽁 언 연못", footprint=[0, 0])
def pond(m):
    i = m.part("ice", k=0.0, gloss=0.8)
    i.add(lambda p: sd_ellipsoid(p, (0, 0.0, 0), (2.3, 0.04, 1.6)), (0.72, 0.86, 0.97), 0.0)
    i.paint(lambda p: np.abs(np.sin(p[:, 0] * 3 + p[:, 2] * 5)) - 0.97, (0.95, 0.98, 1.0), 0.0, soft=0.02)
    s = m.part("rim", k=0.15)
    for k in range(22):
        a = np.radians(k * 360 / 22)
        c = (2.35 * np.cos(a), 0.02, 1.65 * np.sin(a)); r = 0.18 + 0.06 * np.sin(k * 2.3)
        s.add(lambda p, c=c, r=r: sd_sphere(p, c, r), SNOW, 0.0)
    s.cut(lambda p: p[:, 1] + 0.02)


@Y("light_arch", (-1.6, -0.02, -0.25), (1.6, 2.9, 0.25), 0.014, "전구 아치", footprint=[0, 0], emissiveParts=["lights"])
def arch(m):
    w = m.part("frame", tint=(0.3, 0.55, 0.35), group="frame", k=0.0)
    angs = np.radians(np.linspace(0, 180, 24))
    pts = [(1.35 * np.cos(a), 1.5 + 1.25 * np.sin(a), 0) for a in angs]
    for a, b in zip(pts[:-1], pts[1:]):
        w.add(lambda p, a=a, b=b: sd_capsule(p, a, b, 0.05), WHITE, 1.0)
    for s in (-1, 1):
        w.add(lambda p, s=s: sd_capsule(p, (s * 1.35, 0.0, 0), (s * 1.35, 1.5, 0), 0.06), WHITE, 1.0)
        w.add(lambda p, s=s: sd_cylinder(p, (s * 1.35, 0.06, 0), 0.06, 0.16, 0.03), (0.98, 0.98, 1.0), 0.0)
    l = m.part("lights", k=0.0)
    cols = [(1.0, 0.35, 0.3), (1.0, 0.85, 0.35), (0.4, 0.8, 0.45), (0.45, 0.65, 1.0)]
    allp = [(1.35 * np.cos(a), 1.5 + 1.25 * np.sin(a)) for a in np.radians(np.linspace(0, 180, 17))] + \
           [(s * 1.35, y) for s in (-1, 1) for y in np.linspace(0.3, 1.3, 5)]
    for j, (x, y) in enumerate(allp):
        l.add(lambda p, c=(x, y, 0.07): sd_sphere(p, c, 0.055), cols[j % 4], 0.0)
    b = m.part("bow", k=0.01)
    for s in (-1, 1):
        b.add(lambda p, s=s: sd_ellipsoid(p, (s * 0.16, 2.78, 0.08), (0.16, 0.1, 0.05), rot(z=s * 15)), (0.88, 0.22, 0.24), 0.0)
    b.add(lambda p: sd_sphere(p, (0, 2.78, 0.1), 0.07), (0.88, 0.22, 0.24), 0.0)


@Y("bird_feeder", (-0.4, -0.02, -0.4), (0.4, 1.7, 0.4), 0.01, "새 모이집", footprint=[0.15, 0.15])
def feeder(m):
    w = m.part("wood", tint=(0.62, 0.42, 0.26), group="wood", k=0.01)
    w.add(lambda p: sd_cylinder(p, (0, 0.65, 0), 0.65, 0.045, 0.01), WHITE, 1.0)
    w.add(lambda p: sd_roundbox(p, (0, 1.32, 0), (0.25, 0.02, 0.22), 0.01), WHITE, 1.0)
    for s in (-1, 1):
        w.add(lambda p, s=s: sd_roundbox(p, (s * 0.21, 1.42, 0), (0.02, 0.1, 0.02), 0.008), WHITE, 1.0)
    r = m.part("roof", tint=(0.8, 0.25, 0.25), group="roof", k=0.01)
    for s in (-1, 1):
        r.add(lambda p, s=s: sd_roundbox(p, (s * 0.15, 1.6, 0), (0.18, 0.015, 0.27), 0.01, rot(z=-s * 35)), WHITE, 1.0)
    sn = m.part("snow", k=0.04)
    for s in (-1, 1):
        sn.add(lambda p, s=s: sd_roundbox(p, (s * 0.15, 1.63, 0), (0.17, 0.02, 0.26), 0.02, rot(z=-s * 35)), SNOW, 0.0)
    sd = m.part("seeds", k=0.0)
    rng = np.random.default_rng(1)
    for _ in range(14):
        c = (rng.uniform(-0.18, 0.18), 1.35, rng.uniform(-0.16, 0.16))
        sd.add(lambda p, c=c: sd_sphere(p, c, 0.018), (0.85, 0.7, 0.4), 0.0)
    bd = m.part("bird", tint=(0.86, 0.25, 0.25), group="bird", k=0.02)
    bd.add(lambda p: sd_ellipsoid(p, (0.08, 1.4, 0.05), (0.06, 0.05, 0.05)), WHITE, 1.0)
    bd.add(lambda p: sd_sphere(p, (0.13, 1.46, 0.05), 0.035), WHITE, 1.0)
    bd.add(lambda p: sd_roundcone(p, (0.16, 1.46, 0.05), (0.19, 1.455, 0.05), 0.01, 0.003), (0.98, 0.7, 0.2), 0.0)
