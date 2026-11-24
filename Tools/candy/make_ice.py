# make_ice.py - 캔디 팡 '얼음 덮개'(ice.png)와 별 반짝이(star.png) 그림
# 실행: python3 make_ice.py → Assets/Resources/Candy/ice.png, star.png
import os, math
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'Candy')
N = 176; SS = 3; M = N * SS
a = (np.arange(M) + 0.5) / M * 2 - 1
X, Y = np.meshgrid(a, -a)

def rbox(x, y, bx, by, r):
    qx = np.abs(x) - bx + r; qy = np.abs(y) - by + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r

def save(rgba, name, n=N):
    img = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA').resize((n, n), Image.LANCZOS)
    img.save(os.path.join(OUT, name)); return img

# ---- 얼음 덮개: 반투명 하늘색 얼음 상자 + 위쪽 하이라이트 + 금 + 눈 결정 테두리
d = rbox(X, Y, 0.9, 0.9, 0.28)
inside = np.clip(-d * M / 2 / 1.5, 0, 1)
edge = np.exp(-((d + 0.07) / 0.05) ** 2)                         # 두꺼운 얼음 테두리
base = np.stack([0.72 + 0 * X, 0.9 + 0 * X, 1.0 + 0 * X], -1)
alpha = 0.42 + 0.2 * (1 - (Y + 1) / 2)                          # 아래쪽이 조금 더 진함
alpha = alpha + edge * 0.7
# 위쪽 반짝이 띠
hl = np.exp(-((X + Y * 0.6 + 0.55) / 0.12) ** 2) * (Y > 0.0) * 0.6
hl += np.exp(-((X + Y * 0.6 + 0.2) / 0.05) ** 2) * (Y > 0.15) * 0.45
# 금(가는 선 몇 개)
cr = np.zeros_like(X)
for (x0, y0, x1, y1) in [(-0.6, -0.2, -0.1, -0.55), (-0.1, -0.55, 0.35, -0.35), (-0.1, -0.55, 0.05, -0.85), (0.4, 0.5, 0.7, 0.2)]:
    px, py = X - x0, Y - y0; bx, by = x1 - x0, y1 - y0
    h = np.clip((px * bx + py * by) / (bx * bx + by * by), 0, 1)
    cr = np.maximum(cr, np.exp(-((np.hypot(px - bx * h, py - by * h)) / 0.018) ** 2))
col = base * (1 - hl[..., None]) + hl[..., None]
col = col * (1 - cr[..., None] * 0.5) + cr[..., None] * np.array([1, 1, 1]) * 0.5
alpha = np.clip(alpha + hl * 0.5 + cr * 0.5, 0, 0.95) * inside
save(np.dstack([col, alpha]), 'ice.png')

# ---- 별 반짝이(4갈래 빛)
r = np.hypot(X, Y)
rays = np.exp(-(np.abs(X) / 0.05)) * np.exp(-np.abs(Y) * 2.2) + np.exp(-(np.abs(Y) / 0.05)) * np.exp(-np.abs(X) * 2.2)
rays += 0.5 * (np.exp(-(np.abs(X - Y) / 0.05)) + np.exp(-(np.abs(X + Y) / 0.05))) * np.exp(-r * 3.5)
core = np.exp(-(r / 0.16) ** 2)
al = np.clip(rays + core, 0, 1)
save(np.dstack([np.ones_like(X), np.ones_like(X), np.ones_like(X), al]), 'star.png', 96)

# 미리보기
pv = Image.new('RGBA', (400, 200), (60, 40, 90, 255))
piece = Image.open(os.path.join(OUT, 'piece_3.png')).resize((176, 176))
pv.alpha_composite(piece, (10, 12)); pv.alpha_composite(Image.open(os.path.join(OUT, 'ice.png')), (10, 12))
pv.alpha_composite(Image.open(os.path.join(OUT, 'star.png')).resize((150, 150)), (230, 25))
pv.save(os.path.join(HERE, 'preview_ice.png'))
print('ok')
