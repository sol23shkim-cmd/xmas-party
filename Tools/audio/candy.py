# candy.py - '크리스마스 캔디팡' 효과음
# 실행: python3 candy.py → Assets/Resources/Audio/Sfx/candy_*.wav
import os, wave
import numpy as np
SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'Audio', 'Sfx')
rng = np.random.default_rng(5)

def save(name, x, peak=0.85):
    x = x / max(1e-6, np.max(np.abs(x))) * peak
    w = wave.open(os.path.join(OUT, name + '.wav'), 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes()); w.close()

def t_(d): return np.arange(int(d * SR)) / SR
def lp(x, a):
    y = np.zeros_like(x)
    for i in range(1, len(x)): y[i] = y[i - 1] + a * (x[i] - y[i - 1])
    return y
def bell(f, d, dec=6.0, harm=(1, 2.01, 3.03), amps=(1, 0.35, 0.15)):
    t = t_(d); x = sum(a * np.sin(2 * np.pi * f * h * t) * np.exp(-t * dec * (1 + i * 0.6)) for i, (h, a) in enumerate(zip(harm, amps)))
    return x * np.minimum(1, t / 0.003)

def pop():          # 사탕 터짐: 짧고 밝은 '뽁' + 반짝
    t = t_(0.22)
    f = 900 * np.exp(-t * 18) + 500
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 26)
    x += bell(1760, 0.22, 14) * 0.35
    return x
def swap():         # 바꾸기: 부드러운 '슉'
    t = t_(0.16); n = rng.standard_normal(len(t)); env = np.sin(np.pi * t / 0.16) ** 2
    return lp(n, 0.12) * env + np.sin(2 * np.pi * (500 + 900 * t / 0.16) * t) * env * 0.15
def bad():          # 안 되는 바꾸기: 통통 '뽀잉'
    t = t_(0.25); f = 300 + 80 * np.sin(2 * np.pi * 12 * t)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
def special():      # 특수 사탕 생김: 반짝 아르페지오
    x = np.zeros(int(0.5 * SR))
    for i, f in enumerate([1046, 1318, 1568, 2093]):
        b = bell(f, 0.3, 8); o = int(i * 0.06 * SR); x[o:o + len(b)] += b * (0.8 - i * 0.1)
    return x
def line():         # 줄 사탕: '쥬웅' 레이저
    t = t_(0.45); f = 2400 * np.exp(-t * 6) + 300
    x = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.3 + np.sin(2 * np.pi * np.cumsum(f * 0.5) / SR)
    return lp(x, 0.25) * np.exp(-t * 5) * np.minimum(1, t / 0.005)
def boom():         # 포장 사탕: 폭신한 '펑'
    t = t_(0.6); n = rng.standard_normal(len(t))
    x = lp(lp(n, 0.06), 0.1) * np.exp(-t * 7) * 3 + np.sin(2 * np.pi * (70 + 60 * np.exp(-t * 10)) * t) * np.exp(-t * 6)
    return x * np.minimum(1, t / 0.004)
def magic():        # 방울 폭탄: 마법 반짝이
    x = np.zeros(int(0.9 * SR))
    for i in range(14):
        f = 1200 + i * 130 + rng.uniform(-40, 40); b = bell(f, 0.35, 9); o = int(i * 0.04 * SR); x[o:o + len(b)] += b * 0.6
    t = t_(0.9); x += lp(rng.standard_normal(len(t)), 0.4) * np.exp(-t * 4) * 0.15
    return x
def snow():         # 눈덩이 깨짐: 사각
    t = t_(0.25); n = rng.standard_normal(len(t))
    return (lp(n, 0.5) - lp(n, 0.08)) * np.exp(-t * 18) * (1 + 0.5 * np.sin(2 * np.pi * 60 * t))
def win():          # 성공 팡파르
    notes = [(784, 0), (988, 0.12), (1175, 0.24), (1568, 0.36), (1175, 0.55), (1568, 0.67)]
    x = np.zeros(int(1.6 * SR))
    for f, s in notes:
        b = bell(f, 0.9, 3.5) + bell(f / 2, 0.9, 4) * 0.4; o = int(s * SR); x[o:o + len(b)] += b
    return x
def lose():
    notes = [(523, 0), (466, 0.22), (415, 0.44), (349, 0.7)]
    x = np.zeros(int(1.6 * SR))
    for f, s in notes:
        b = bell(f, 0.8, 3); o = int(s * SR); x[o:o + len(b)] += b
    return x
def collect():      # 목표 칸에 쏙
    return bell(2093, 0.18, 16, (1, 2.7), (1, 0.2)) + bell(2637, 0.18, 18) * 0.5

if __name__ == '__main__':
    for nm, fn in [('pop', pop), ('swap', swap), ('bad', bad), ('special', special), ('line', line), ('boom', boom), ('magic', magic), ('snow', snow), ('win', win), ('lose', lose), ('collect', collect)]:
        save('candy_' + nm, fn())
    print('ok')
