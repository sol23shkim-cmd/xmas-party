# gift_garden.py - 온실·선물 포장실 효과음 (11-26)
#  water 물 붓는 졸졸, grow 쑥 자라는 반짝 올림음, tear 종이 찢는 소리, untie 리본 풀리는 스르륵,
#  box_open 상자 뚜껑 '뽁' + 반짝, boing 용수철 '띠용', ta_da 짜잔(짧은 팡파르)
# 실행: python3 gift_garden.py → Assets/Resources/Audio/Sfx/*.wav
import os, wave
import numpy as np

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'Audio', 'Sfx')
rng = np.random.default_rng(11)


def save(name, x):
    x = x / max(1e-6, np.max(np.abs(x))) * 0.9
    w = wave.open(os.path.join(OUT, name), 'wb')
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes()); w.close()


def lowpass(x, a):
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc); y[i] = acc
    return y


def bell(f, n, decay=6.0, amps=(1, 0.5, 0.25, 0.12)):
    t = np.arange(n) / SR
    out = np.zeros(n)
    for h, a in enumerate(amps, start=1):
        out += a * np.sin(2 * np.pi * f * h * (1 + 0.002 * h) * t) * np.exp(-t * decay * (1 + 0.4 * h))
    return out


def water():
    n = int(1.1 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    hiss = (lowpass(noise, 0.35) - lowpass(noise, 0.05)) * 0.5
    out = hiss * np.minimum(1, t / 0.08) * np.minimum(1, (1.1 - t) / 0.25)
    # 작은 물방울 '똑똑' (올라가는 사인)
    for k in range(26):
        st = rng.uniform(0.02, 0.95); dur = rng.uniform(0.025, 0.05); f0 = rng.uniform(700, 1500)
        i0 = int(st * SR); m = int(dur * SR); tt = np.arange(m) / SR
        f = f0 * (1 + tt / dur * 0.9)
        if i0 + m > n: continue
        out[i0:i0 + m] += 0.35 * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 70)
    save('water.wav', out)


def grow():
    n = int(0.75 * SR); out = np.zeros(n)
    for i, f in enumerate([523, 659, 784, 1047, 1319]):
        i0 = int(i * 0.07 * SR); m = n - i0
        out[i0:] += bell(f, m, 7.0) * (0.8 - i * 0.08)
    save('grow.wav', out)


def tear():
    n = int(0.42 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    crack = np.zeros(n)
    i = 0
    while i < n:   # 작은 '찍찍' 알갱이들
        g = int(rng.uniform(0.002, 0.009) * SR); a = rng.uniform(0.4, 1.0)
        crack[i:i + g] += a
        i += g + int(rng.uniform(0.001, 0.006) * SR)
    band = lowpass(noise, 0.6) - lowpass(noise, 0.12)
    env = np.minimum(1, t / 0.01) * np.exp(-t * 6) * (1 - np.clip((t - 0.3) / 0.12, 0, 1))
    save('tear.wav', band * crack[:n] * env * 1.4 + band * env * 0.3)


def untie():
    n = int(0.5 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    sweep = np.zeros(n); acc = 0.0
    for k in range(n):   # 점점 높아지는 대역
        a = 0.03 + 0.25 * (k / n)
        acc += a * (noise[k] - acc); sweep[k] = acc
    env = np.sin(np.pi * np.clip(t / 0.5, 0, 1)) ** 1.5
    tone = 0.25 * np.sin(2 * np.pi * np.cumsum(500 + 900 * t / 0.5) / SR) * env
    save('untie.wav', sweep * env * 1.2 + tone)


def box_open():
    n = int(1.0 * SR); t = np.arange(n) / SR
    pop_f = 380 * np.exp(-t * 30) + 160
    pop = np.sin(2 * np.pi * np.cumsum(pop_f) / SR) * np.exp(-t * 28) * 1.2
    out = pop.copy()
    for i, f in enumerate([1568, 1976, 2349, 2637, 3136]):
        i0 = int((0.06 + i * 0.05) * SR); m = n - i0
        out[i0:] += bell(f, m, 5.0, (1, 0.3, 0.1)) * 0.35
    save('box_open.wav', out)


def boing():
    n = int(0.7 * SR); t = np.arange(n) / SR
    f = 180 + 260 * np.exp(-t * 4) * (1 + 0.5 * np.sin(2 * np.pi * 9 * t))
    out = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 4.5)
    out += 0.3 * np.sin(2 * np.pi * 2 * np.cumsum(f) / SR) * np.exp(-t * 6)
    save('boing.wav', out)


def ta_da():
    n = int(1.3 * SR); out = np.zeros(n)
    notes = [(0.0, [523, 659]), (0.13, [587, 740]), (0.32, [784, 988, 1175])]
    for st, fs in notes:
        i0 = int(st * SR); m = n - i0; tt = np.arange(m) / SR
        for f in fs:
            tone = (np.sin(2 * np.pi * f * tt) + 0.45 * np.sin(2 * np.pi * 2 * f * tt) + 0.2 * np.sin(2 * np.pi * 3 * f * tt))
            env = np.minimum(1, tt / 0.01) * np.exp(-tt * (2.2 if st > 0.3 else 7))
            out[i0:] += tone * env * 0.4
    save('ta_da.wav', out)


if __name__ == '__main__':
    water(); grow(); tear(); untie(); box_open(); boing(); ta_da()
    print('ok')
