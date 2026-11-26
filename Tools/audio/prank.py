# prank.py - 깜짝 놀래기 선물 효과음: squeak(고무 닭), pfft(방귀 쿠션), chatter(딱딱 이빨), squirt(물총 꽃), pop(뱀 캔), punch(펀치 상자)
import numpy as np, wave, os, sys
SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '../../Assets/Resources/Audio/Sfx')
rng = np.random.default_rng(7)
def save(name, x):
    x = np.asarray(x, dtype=np.float64)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.85
    fade = int(SR * 0.006); x[:fade] *= np.linspace(0, 1, fade); x[-fade:] *= np.linspace(1, 0, fade)
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype('<i2').tobytes())
def t_(d): return np.arange(int(SR * d)) / SR
def lp(x, a):
    y = np.zeros_like(x); s = 0.0
    for i, v in enumerate(x): s += a * (v - s); y[i] = s
    return y
def osc(freq):  # freq array → phase
    return np.cumsum(2 * np.pi * freq / SR)

# 고무 닭: 위로 꽥 올라갔다 떨리며 내려오는 고무 소리 (배음 많은 펄스 + 콧소리)
t = t_(0.55)
f = 900 + 700 * np.sin(np.pi * np.clip(t / 0.5, 0, 1)) ** 0.6 + 60 * np.sin(2 * np.pi * 28 * t)
ph = osc(f); x = np.sign(np.sin(ph)) * 0.5 + np.sin(2 * ph) * 0.4 + np.sin(3 * ph) * 0.2
x = lp(x, 0.35) * np.minimum(1, t / 0.02) * np.exp(-((t - 0.25) / 0.32) ** 4)
x += rng.normal(0, 0.05, len(t)) * np.exp(-t * 6)
save('squeak', x)

# 방귀 쿠션: 떨리는 낮은 부르르 + 바람 (귀엽게 짧게)
t = t_(0.75)
f = 95 + 40 * np.exp(-t * 3) + 18 * np.sin(2 * np.pi * 7 * t) + rng.normal(0, 6, len(t))
ph = osc(f); buzz = np.sign(np.sin(ph)) * (0.6 + 0.4 * np.sin(2 * np.pi * 11 * t))
air = lp(rng.normal(0, 1, len(t)), 0.08)
env = np.minimum(1, t / 0.03) * np.clip(1 - (t / 0.72) ** 2, 0, 1)
x = (lp(buzz, 0.12) * 0.9 + air * 0.5) * env
save('pfft', x)

# 딱딱 이빨: 빠른 딸깍 연타
t = t_(0.9); x = np.zeros(len(t))
for k in range(14):
    s = int(SR * (0.02 + k * 0.062)); n = int(SR * 0.025)
    if s + n > len(x): break
    tt = np.arange(n) / SR
    click = (np.sin(2 * np.pi * (2400 + 300 * (k % 2)) * tt) + rng.normal(0, 0.6, n)) * np.exp(-tt * 260)
    x[s:s + n] += click * (0.8 if k % 2 == 0 else 0.55)
save('chatter', x)

# 물총 꽃: 쉬익 물줄기
t = t_(0.5)
n = rng.normal(0, 1, len(t)); x = n - lp(n, 0.25)   # 높은 쉬익
x = lp(x, 0.6) * np.minimum(1, t / 0.015) * np.exp(-t * 5)
x += np.sin(osc(1800 - 900 * t)) * 0.15 * np.exp(-t * 9)
save('squirt', x)

# 뱀 캔: 뽕! + 용수철 띠요옹
t = t_(0.7)
pop = rng.normal(0, 1, len(t)) * np.exp(-t * 90) + np.sin(osc(220 * np.exp(-t * 20) + 80)) * np.exp(-t * 30)
f = 330 + 160 * np.sin(2 * np.pi * 9 * t) * np.exp(-t * 3.5)
boing = np.sin(osc(f)) * np.exp(-t * 4) * np.minimum(1, np.maximum(0, t - 0.03) / 0.02)
save('pop', pop * 0.8 + boing * 0.6)

# 펀치 상자: 휙 + 퍽(뾰로롱 별)
t = t_(0.6)
wh = lp(rng.normal(0, 1, len(t)), 0.2) * np.exp(-((t - 0.08) / 0.05) ** 2)
thud = np.sin(osc(140 * np.exp(-np.maximum(0, t - 0.13) * 12) + 50)) * np.exp(-np.maximum(0, t - 0.13) * 18) * (t > 0.13)
tw = sum(np.sin(osc(np.full(len(t), fr))) for fr in (1568, 2093, 2637)) / 3 * np.exp(-np.maximum(0, t - 0.2) * 7) * (t > 0.2) * 0.35
save('punch', wh * 0.6 + thud + tw)
print('ok')
