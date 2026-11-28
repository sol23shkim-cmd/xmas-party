# make_sprites.py - '크리스마스 캔디팡' 그림 만들기 (SDF 모양 + 입체 조명, 3배로 그려 줄여서 매끈하게)
# 실행: python3 make_sprites.py  → Assets/Resources/Candy/*.png  (미리보기: Tools/candy/preview.png)
import os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'Candy')
os.makedirs(OUT, exist_ok=True)
N = 176          # 결과 크기
SS = 3           # 3배로 그리기
M = N * SS

def grid(n=M):
    a = (np.arange(n) + 0.5) / n * 2 - 1
    x, y = np.meshgrid(a, -a)          # y 위로 +
    return x, y

X, Y = grid()

# ---------- SDF ----------
def circle(x, y, cx, cy, r): return np.hypot(x - cx, y - cy) - r
def rbox(x, y, cx, cy, bx, by, r):
    qx = np.abs(x - cx) - bx + r; qy = np.abs(y - cy) - by + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
def capsule(x, y, ax, ay, bx, by, r):
    pax, pay = x - ax, y - ay; bax, bay = bx - ax, by - ay
    h = np.clip((pax * bax + pay * bay) / (bax * bax + bay * bay), 0, 1)
    return np.hypot(pax - bax * h, pay - bay * h) - r
def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)
def star5(x, y, r, rf):
    k1x, k1y = 0.809016994375, -0.587785252292
    k2x, k2y = -k1x, k1y
    px = np.abs(x); py = y.copy()
    d = np.maximum(k1x * px + k1y * py, 0); px = px - 2 * d * k1x; py = py - 2 * d * k1y
    d = np.maximum(k2x * px + k2y * py, 0); px = px - 2 * d * k2x; py = py - 2 * d * k2y
    px = np.abs(px); py = py - r
    bax, bay = rf * -k1y - 0, rf * k1x - 1
    h = np.clip((px * bax + py * bay) / (bax * bax + bay * bay), 0, r)
    return np.hypot(px - bax * h, py - bay * h) * np.sign(py * bax - px * bay)
def hexagon(x, y, r):
    kx, ky, kz = -0.866025404, 0.5, 0.577350269
    px = np.abs(y); py = np.abs(x)          # 꼭짓점이 위아래로
    d = np.minimum(kx * px + ky * py, 0); px = px - 2 * d * kx; py = py - 2 * d * ky
    px = px - np.clip(px, -kz * r, kz * r); py = py - r
    return np.hypot(px, py) * np.sign(py)
def tri(x, y, apex, half, height):
    # 위가 꼭짓점인 이등변 삼각형 (iq)
    px = np.abs(x); py = apex - y
    qx, qy = half, height
    ax = px - qx * np.clip((px * qx + py * qy) / (qx * qx + qy * qy), 0, 1); ay = py - qy * np.clip((px * qx + py * qy) / (qx * qx + qy * qy), 0, 1)
    bx = px - qx * np.clip(px / qx, 0, 1); by = py - qy
    s = -np.sign(qy)
    d1 = ax * ax + ay * ay; s1 = s * (px * qy - py * qx)
    d2 = bx * bx + by * by; s2 = s * (py - qy)
    d = np.where(d1 < d2, d1, d2); sg = np.where(d1 < d2, s1, s2)
    return -np.sqrt(d) * np.sign(sg)

def poly(x, y, pts):
    v = np.array(pts, dtype=np.float64); n = len(v)
    d = (x - v[0, 0]) ** 2 + (y - v[0, 1]) ** 2; sgn = np.ones_like(x)
    j = n - 1
    for i in range(n):
        ex, ey = v[j, 0] - v[i, 0], v[j, 1] - v[i, 1]
        wx, wy = x - v[i, 0], y - v[i, 1]
        h = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        bx, by = wx - ex * h, wy - ey * h
        d = np.minimum(d, bx * bx + by * by)
        c1 = y >= v[i, 1]; c2 = y < v[j, 1]; c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        sgn = np.where(flip, -sgn, sgn)
        j = i
    return sgn * np.sqrt(d)

def sm(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

# ---------- 입체 칠하기 ----------
L = np.array([-0.45, 0.6, 0.66]); L /= np.linalg.norm(L)
def shade(d, col, bevel=0.22, outline=0.035, ocol=None, gloss=True, height_extra=None, rough=1.0):
    """d: SDF(안쪽 음수), col: (M,M,3) 0..1 기본색 → RGBA float"""
    inside = -d
    h = np.sqrt(np.clip(inside / bevel, 0, 1)) * bevel                # 둥근 테두리 높이
    if height_extra is not None: h = h + height_extra
    gy, gx = np.gradient(h, 2.0 / M)
    nx, ny, nz = -gx, gy, np.ones_like(h) * 0.9
    nl = np.sqrt(nx * nx + ny * ny + nz * nz); nx /= nl; ny /= nl; nz /= nl
    diff = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    # 반사광
    rz = 2 * diff * nz - L[2]; spec = np.clip(rz, 0, 1) ** (40 * rough)
    c = col * (0.42 + 0.68 * diff[..., None]) + spec[..., None] * 0.55
    # 아래쪽은 살짝 반사된 빛 (통통하게)
    rim = sm(0.6, 1.0, -Y) * sm(0.0, 0.08, inside) * (1 - sm(0.08, 0.25, inside))
    c = c + rim[..., None] * col * 0.25
    if gloss:
        g = np.exp(-(((X + 0.32) / 0.32) ** 2 + ((Y - 0.42) / 0.17) ** 2)) * sm(0.04, 0.12, inside)
        c = c * (1 - g[..., None] * 0.7) + g[..., None] * 0.95
    if ocol is None: ocol = col * 0.45
    o = sm(outline * 0.4, outline, inside)          # 가장자리 진한 테두리
    c = c * o[..., None] + ocol * (1 - o[..., None])
    a = np.clip(0.5 + inside * M / 2 / 1.2, 0, 1)
    return np.concatenate([np.clip(c, 0, 1), a[..., None]], axis=-1)

def solid(rgb):
    return np.ones((M, M, 3)) * np.array(rgb)

def over(base, top):
    a = top[..., 3:4]; out = base.copy()
    out[..., :3] = base[..., :3] * (1 - a) + top[..., :3] * a
    out[..., 3:4] = base[..., 3:4] + a * (1 - base[..., 3:4])
    return out

def save(name, rgba, size=N):
    img = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA')
    if img.size[0] != size: img = img.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name + '.png'))
    return img

# ---------- 여섯 가지 사탕 ----------
COL = [(0.93, 0.16, 0.22), (0.18, 0.70, 0.30), (1.0, 0.80, 0.12), (0.22, 0.58, 1.0), (0.66, 0.33, 0.92), (0.86, 0.50, 0.22)]

def piece_sdf(t):
    if t == 0: return circle(X, Y, 0, 0, 0.80)
    if t == 1:
        tiers = [poly(X, Y, [(0, 0.86), (-0.38, 0.36), (0.38, 0.36)]),
                 poly(X, Y, [(0, 0.55), (-0.58, -0.05), (0.58, -0.05)]),
                 poly(X, Y, [(0, 0.2), (-0.76, -0.56), (0.76, -0.56)])]
        d = smin(smin(tiers[0], tiers[1], 0.05), tiers[2], 0.05) - 0.07
        d = np.minimum(d, rbox(X, Y, 0, -0.72, 0.13, 0.14, 0.04))
        return d
    if t == 2: return star5(X, Y * 1.0 + 0.04, 0.86, 0.48) - 0.07
    if t == 3: return hexagon(X, Y, 0.74) - 0.07
    if t == 4:
        d = rbox(X, Y, 0, -0.1, 0.72, 0.66, 0.16)
        bow = np.minimum(circle(X, Y, -0.2, 0.64, 0.17), circle(X, Y, 0.2, 0.64, 0.17))
        return smin(d, bow, 0.06)
    if t == 5:
        d = circle(X, Y, 0, 0.5, 0.33)
        d = smin(d, rbox(X, Y, 0, -0.16, 0.33, 0.42, 0.22), 0.08)
        for s in (-1, 1):
            d = smin(d, capsule(X, Y, s * 0.18, 0.04, s * 0.66, 0.24, 0.15), 0.08)
            d = smin(d, capsule(X, Y, s * 0.13, -0.42, s * 0.36, -0.84, 0.16), 0.08)
        return d

def piece_color(t, d):
    base = solid(COL[t])
    inside = -d
    if t == 0:   # 페퍼민트: 빙글빙글 하얀 줄
        r = np.hypot(X, Y); a = np.arctan2(Y, X) + r * 2.6
        w = sm(-0.15, 0.15, np.sin(a * 4))
        base = base * (1 - w[..., None]) + np.array([1, 0.97, 0.95]) * w[..., None]
        c = sm(0.17, 0.12, r)            # 가운데 하얀 점
        base = base * (1 - c[..., None]) + np.array([1, 1, 1]) * c[..., None]
    if t == 1:   # 트리: 줄 장식 + 방울 + 꼭대기 별 + 나무 기둥
        trunk = (Y < -0.6) & (np.abs(X) < 0.2)
        base[trunk] = np.array([0.55, 0.32, 0.18])
        for (cx, cy, cc) in [(-0.2, 0.05, (1, 0.25, 0.3)), (0.24, -0.25, (1, 0.85, 0.2)), (-0.33, -0.42, (0.3, 0.7, 1)), (0.06, 0.35, (1, 0.5, 0.9)), (0.4, -0.5, (1, 0.25, 0.3))]:
            m = sm(0.11, 0.08, np.hypot(X - cx, Y - cy))
            base = base * (1 - m[..., None]) + np.array(cc) * m[..., None]
        for k, yy in enumerate([0.2, -0.2]):   # 반짝이 줄
            m = sm(0.04, 0.018, np.abs(Y - (yy + X * 0.3 * (1 if k == 0 else -1))) ) * (Y > -0.55)
            base = base * (1 - m[..., None] * 0.8) + np.array([1, 0.95, 0.7]) * m[..., None] * 0.8
    if t == 2:   # 별: 가운데 밝은 별
        m = sm(0.02, -0.04, star5(X, Y + 0.04, 0.42, 0.48) - 0.04)
        base = base * (1 - m[..., None] * 0.6) + np.array([1, 0.97, 0.7]) * m[..., None] * 0.6
    if t == 3:   # 눈꽃 보석: 깎은 면 + 하얀 눈꽃
        ang = np.arctan2(Y, X); facet = (np.floor((ang + math.pi / 6) / (math.pi / 3)) % 2)
        inner = hexagon(X, Y, 0.38)
        base = base * np.where(inner[..., None] < 0, 1.15, np.where(facet[..., None] > 0, 1.0, 0.86))
        sf = np.ones_like(X) * 9
        for k in range(6):
            a = k * math.pi / 3
            sf = np.minimum(sf, capsule(X, Y, 0, 0, 0.42 * math.cos(a), 0.42 * math.sin(a), 0.035))
            for s in (-1, 1):
                bx, by = 0.26 * math.cos(a), 0.26 * math.sin(a)
                sf = np.minimum(sf, capsule(X, Y, bx, by, bx + 0.12 * math.cos(a + s * 0.8), by + 0.12 * math.sin(a + s * 0.8), 0.028))
        m = sm(0.015, -0.01, sf)
        base = base * (1 - m[..., None]) + np.array([0.95, 0.98, 1]) * m[..., None]
    if t == 4:   # 선물 상자: 금색 리본
        rib = (np.abs(X) < 0.13) | ((np.abs(Y + 0.1) < 0.13) & (Y < 0.56))
        bow = (Y > 0.45)
        base[rib | bow] = np.array([1, 0.84, 0.3])
        m = sm(0.07, 0.04, np.hypot(X, Y - 0.56))
        base = base * (1 - m[..., None]) + np.array([1, 0.72, 0.2]) * m[..., None]
    if t == 5:   # 진저브레드: 하얀 아이싱
        ic = np.ones_like(X) * 9
        for (cx, cy, r) in [(-0.12, 0.56, 0.05), (0.12, 0.56, 0.05), (0, -0.05, 0.06), (0, -0.28, 0.06)]:
            ic = np.minimum(ic, circle(X, Y, cx, cy, r))
        smile = np.abs(np.hypot(X, Y - 0.5) - 0.17) - 0.025
        smile = np.where((Y < 0.45), smile, 9)
        ic = np.minimum(ic, smile)
        for s in (-1, 1):
            ic = np.minimum(ic, np.where(np.abs(Y - (0.2 + (X - s * 0.52) * 0.4)) < 0.11, np.abs(X - s * 0.53) - 0.025, 9))
            ic = np.minimum(ic, np.where(np.abs(Y + 0.66) < 0.12, np.abs(X - s * 0.27) - 0.025, 9))
        m = sm(0.012, -0.008, ic)
        base = base * (1 - m[..., None]) + np.array([1, 0.98, 0.95]) * m[..., None]
    return base

def make_piece(t, variant=''):
    d = piece_sdf(t)
    col = piece_color(t, d)
    if variant in ('h', 'v'):   # 줄무늬: 하얀 띠 3줄
        q = Y if variant == 'h' else X
        w = sm(0.28, 0.22, np.abs(np.abs(q) - 0.0)) * 0 + sm(0.06, 0.0, np.abs((q * 2.6) % 1 - 0.5) - 0.18)
        col = col * (1 - w[..., None] * 0.85) + np.array([1, 1, 1]) * w[..., None] * 0.85
    rgba = shade(d, col, bevel=0.24)
    if variant == 'w':          # 셀로판 포장: 작아진 사탕 + 반짝이는 포장 + 양 옆 꼬인 끝
        small = Image.fromarray((np.nan_to_num(np.clip(rgba, 0, 1)) * 255).astype(np.uint8), 'RGBA').resize((int(M * 0.6), int(M * 0.6)), Image.LANCZOS)
        canvas = Image.new('RGBA', (M, M), (0, 0, 0, 0)); o = (M - small.size[0]) // 2
        canvas.alpha_composite(small, (o, o))
        base = np.asarray(canvas).astype(np.float64) / 255
        wd = rbox(X, Y, 0, 0, 0.52, 0.52, 0.2)
        for sg in (-1, 1):
            ends = poly(sg * X, Y, [(0.5, 0.08), (0.9, 0.3), (0.84, 0.0), (0.9, -0.3), (0.5, -0.08)]) - 0.02
            wd = smin(wd, ends, 0.03)
        wc = solid(np.array(COL[t]) * 0.6 + 0.4)
        wrap = shade(wd, wc, bevel=0.12, outline=0.03, ocol=np.array(COL[t]) * 0.6)
        wrap[..., 3] *= np.where(rbox(X, Y, 0, 0, 0.46, 0.46, 0.16) < 0, 0.34, 0.95)
        rgba = over(base, wrap)
        # 반짝이 별 몇 개
        for (cx, cy) in [(-0.34, 0.36), (0.36, -0.32), (0.32, 0.38)]:
            m = sm(0.02, -0.01, star5((X - cx) * 1, (Y - cy), 0.09, 0.4))
            rgba[..., :3] = rgba[..., :3] * (1 - m[..., None]) + m[..., None]
            rgba[..., 3] = np.maximum(rgba[..., 3], m)
    return rgba

def make_bomb():
    d = circle(X, Y, 0, -0.06, 0.74)
    cap = rbox(X, Y, 0, 0.72, 0.18, 0.1, 0.04)
    ring = np.abs(circle(X, Y, 0, 0.88, 0.09)) - 0.03
    col = solid((0.16, 0.12, 0.34))
    # 무지개 점
    rng = np.random.default_rng(7); cols = [c for c in COL]
    k = 0
    for gy in np.arange(-0.7, 0.71, 0.2):
        for gx in np.arange(-0.7, 0.71, 0.2):
            cx = gx + (0.1 if int(round((gy + 0.7) / 0.2)) % 2 else 0) + rng.uniform(-0.03, 0.03); cy = -0.06 + gy + rng.uniform(-0.03, 0.03)
            if math.hypot(cx, cy + 0.06) > 0.64: continue
            m = sm(0.065, 0.04, np.hypot(X - cx, Y - cy))
            col = col * (1 - m[..., None]) + np.array(cols[k % 6]) * 1.05 * m[..., None]; k += 1
    for i in range(0):
        col = col * (1 - m[..., None]) + np.array(cols[i % 6]) * 1.05 * m[..., None]
    body = shade(d, col, bevel=0.5, outline=0.03, ocol=np.array([0.08, 0.05, 0.2]))
    gold = solid((1, 0.82, 0.3))
    capr = shade(np.minimum(cap, ring), gold, bevel=0.08, outline=0.02, ocol=np.array([0.55, 0.38, 0.08]), gloss=False)
    return over(body, capr)

def make_snow(hp):
    d = rbox(X, Y, 0, 0, 0.8, 0.8, 0.3)
    bumps = np.zeros_like(X)
    rng = np.random.default_rng(3 + hp)
    for i in range(14):
        cx, cy = rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6)
        bumps += 0.05 * np.exp(-((X - cx) ** 2 + (Y - cy) ** 2) / 0.04)
    col = solid((1.0, 1.0, 1.0))
    col = col * (1 - 0.06 * sm(0.0, -0.8, Y)[..., None])
    if hp == 1:   # 금이 감
        cr = np.ones_like(X) * 9
        pts = [(-0.5, 0.55), (-0.1, 0.15), (0.05, -0.2), (0.45, -0.55)]
        for a, b in zip(pts[:-1], pts[1:]): cr = np.minimum(cr, capsule(X, Y, a[0], a[1], b[0], b[1], 0.018))
        cr = np.minimum(cr, capsule(X, Y, -0.1, 0.15, 0.35, 0.3, 0.015))
        m = sm(0.01, -0.005, cr)
        col = col * (1 - m[..., None] * 0.55)
        col = col * np.array([0.97, 0.98, 1.0])
    rgba = shade(d, col, bevel=0.3, outline=0.03, ocol=np.array([0.55, 0.68, 0.85]), height_extra=bumps, rough=0.6)
    rgba[..., :3] = np.clip(rgba[..., :3] * 1.12 + 0.04, 0, 1)
    for (cx, cy) in ([(-0.45, 0.45), (0.5, 0.2)] if hp == 2 else [(-0.4, 0.5)]):
        m = sm(0.02, -0.01, star5(X - cx, Y - cy, 0.1, 0.4))
        rgba[..., :3] = rgba[..., :3] * (1 - m[..., None]) + m[..., None]
    return rgba

def glow_dot(n=64):
    a = (np.arange(n) + 0.5) / n * 2 - 1; x, y = np.meshgrid(a, a); r = np.hypot(x, y)
    al = np.clip(1 - r, 0, 1) ** 2.2
    return np.dstack([np.ones_like(r), np.ones_like(r), np.ones_like(r), al])

def sparkle(n=96):
    a = (np.arange(n) + 0.5) / n * 2 - 1; x, y = np.meshgrid(a, a)
    r = np.hypot(x, y)
    rays = np.exp(-np.abs(x) * 18) * np.clip(1 - np.abs(y), 0, 1) ** 1.5 + np.exp(-np.abs(y) * 18) * np.clip(1 - np.abs(x), 0, 1) ** 1.5
    core = np.clip(1 - r * 2.2, 0, 1) ** 1.5
    al = np.clip(rays * 0.9 + core, 0, 1)
    return np.dstack([np.ones_like(r), np.ones_like(r), np.ones_like(r), al])

def ring(n=128):
    a = (np.arange(n) + 0.5) / n * 2 - 1; x, y = np.meshgrid(a, a); r = np.hypot(x, y)
    al = np.exp(-((r - 0.82) / 0.07) ** 2)
    return np.dstack([np.ones_like(r), np.ones_like(r), np.ones_like(r), al])

def beam(w=256, h=48):
    y = (np.arange(h) + 0.5) / h * 2 - 1; x = (np.arange(w) + 0.5) / w
    yy, xx = np.meshgrid(y, x, indexing='ij')
    al = np.exp(-(yy / 0.35) ** 2) * 0.8 + np.exp(-(yy / 0.1) ** 2) * 0.6
    al *= np.clip(np.minimum(xx, 1 - xx) * 12, 0, 1)
    return np.dstack([np.ones_like(al), np.ones_like(al), np.ones_like(al), np.clip(al, 0, 1)])

def shadow(n=96):
    a = (np.arange(n) + 0.5) / n * 2 - 1; x, y = np.meshgrid(a, a); r = np.hypot(x, y * 1.0)
    al = np.clip(1 - r, 0, 1) ** 1.6 * 0.55
    return np.dstack([np.zeros_like(r), np.zeros_like(r), np.zeros_like(r), al])

def rounded_rect(n, rad, border=0.0, inner=False):
    s = n * SS; a = (np.arange(s) + 0.5); x, y = np.meshgrid(a, a)
    c = s / 2; r = rad * SS
    qx = np.abs(x - c) - (c - r); qy = np.abs(y - c) - (c - r)
    d = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    al = np.clip(0.5 - d, 0, 1)
    shade_ = np.ones_like(al)
    if inner:   # 안쪽으로 살짝 파인 느낌 (위가 어둡고 아래가 밝음)
        shade_ = 0.88 + 0.12 * (y / s)
        edge = np.clip(-d / (6 * SS), 0, 1)
        shade_ = shade_ * (0.85 + 0.15 * edge)
    img = np.dstack([shade_, shade_, shade_, al])
    out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGBA').resize((n, n), Image.LANCZOS)
    return out

def background(w=720, h=1280):
    y = np.linspace(0, 1, h)[:, None] * np.ones((1, w)); x = np.linspace(0, 1, w)[None, :] * np.ones((h, 1))
    top = np.array([0.10, 0.12, 0.32]); mid = np.array([0.30, 0.22, 0.52]); bot = np.array([0.95, 0.62, 0.70])
    t = y
    col = np.where(t[..., None] < 0.55, top + (mid - top) * (t[..., None] / 0.55), mid + (bot - mid) * ((t[..., None] - 0.55) / 0.45))
    rng = np.random.default_rng(11)
    img = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), 'RGB').convert('RGBA')
    dr = ImageDraw.Draw(img, 'RGBA')
    for i in range(160):   # 별
        sx, sy = rng.uniform(0, w), rng.uniform(0, h * 0.6); s = rng.uniform(0.6, 2.2)
        a = int(rng.uniform(90, 230)); dr.ellipse([sx - s, sy - s, sx + s, sy + s], fill=(255, 250, 230, a))
    # 언덕 (눈)
    for k, (base, amp, colh) in enumerate([(0.78, 0.05, (200, 205, 235)), (0.86, 0.04, (232, 236, 252)), (0.93, 0.03, (250, 251, 255))]):
        pts = [(0, h)]
        for xi in range(0, w + 8, 8):
            yy = h * (base + amp * math.sin(xi / w * 6.3 + k * 1.7) + amp * 0.5 * math.sin(xi / w * 13 + k))
            pts.append((xi, yy))
        pts.append((w, h)); dr.polygon(pts, fill=colh + (255,))
    # 멀리 트리 실루엣
    for i in range(14):
        tx = rng.uniform(0, w); ty = h * rng.uniform(0.76, 0.84); s = rng.uniform(22, 50)
        dr.polygon([(tx, ty - s * 1.6), (tx - s * 0.6, ty), (tx + s * 0.6, ty)], fill=(60, 90, 110, 255))
        dr.polygon([(tx, ty - s * 1.6), (tx - s * 0.35, ty - s * 0.9), (tx + s * 0.35, ty - s * 0.9)], fill=(240, 245, 255, 255))
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    img.convert('RGB').save(os.path.join(OUT, 'bg.png'))

def title():
    font_path = '/usr/share/fonts/opentype/noto/NotoSansCJK-Black.ttc'
    W, H = 1100, 300
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    f1 = ImageFont.truetype(font_path, 92, index=1)   # KR
    f2 = ImageFont.truetype(font_path, 150, index=1)
    lines = [('크리스마스', f1, 18), ('캔디팡!', f2, 105)]
    for text, f, y in lines:
        tw = f.getbbox(text)[2]
        x = (W - tw) // 2
        layer = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
        d.text((x, y + 10), text, font=f, fill=(90, 20, 40, 255), stroke_width=14, stroke_fill=(90, 20, 40, 255))
        d.text((x, y), text, font=f, fill=(255, 255, 255, 255), stroke_width=12, stroke_fill=(120, 25, 50, 255))
        # 글자 안쪽 그라데이션
        mask = Image.new('L', (W, H), 0); ImageDraw.Draw(mask).text((x, y), text, font=f, fill=255)
        grad = Image.new('RGBA', (W, H)); gp = grad.load()
        bb = f.getbbox(text); top, bot = y + bb[1], y + bb[3]
        g = np.zeros((H, W, 4), np.uint8)
        for yy in range(H):
            t = np.clip((yy - top) / max(1, (bot - top)), 0, 1)
            c = np.array([255, 236, 120]) * (1 - t) + np.array([255, 120, 70]) * t if text == '캔디팡!' else np.array([255, 255, 255]) * (1 - t) + np.array([255, 200, 210]) * t
            g[yy, :, :3] = c; g[yy, :, 3] = 255
        grad = Image.fromarray(g, 'RGBA')
        layer.paste(grad, (0, 0), mask)
        img.alpha_composite(layer)
    img.save(os.path.join(OUT, 'title.png'))

if __name__ == '__main__':
    prev = Image.new('RGBA', (N * 8, N * 4), (60, 50, 90, 255))
    for t in range(6):
        for j, v in enumerate(['', 'h', 'v', 'w']):
            im = save(f'piece_{t}{("_" + v) if v else ""}', make_piece(t, v))
            prev.alpha_composite(im, (t * N, j * N))
    prev.alpha_composite(save('bomb', make_bomb()), (6 * N, 0))
    prev.alpha_composite(save('snow_2', make_snow(2)), (6 * N, N))
    prev.alpha_composite(save('snow_1', make_snow(1)), (6 * N, 2 * N))
    Image.fromarray((glow_dot() * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'dot.png'))
    Image.fromarray((sparkle() * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'spark.png'))
    Image.fromarray((ring() * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'ring.png'))
    Image.fromarray((beam() * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'beam.png'))
    Image.fromarray((shadow() * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'shadow.png'))
    rounded_rect(96, 26).save(os.path.join(OUT, 'panel.png'))
    rounded_rect(64, 14, inner=True).save(os.path.join(OUT, 'cell.png'))
    background(); title()
    prev.save(os.path.join(HERE, 'preview.png'))
    print('ok')
