# soft_pickup.py - 쿠키 달리기 젤리·코인 먹는 소리 (부드럽고 짧은 '퐁' 소리, 11-24 교체)
# cookie.py의 jelly/coin(높은 종소리)을 덮어씀. 실행: python3 soft_pickup.py  → Assets/Resources/Audio/Sfx/jelly.wav, coin.wav
import os, wave
import numpy as np

SR = 32000
OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'Audio', 'Sfx')


def save(name, x):
    x = np.clip(x, -1, 1)
    w = wave.open(os.path.join(OUT, name), 'wb')
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((x * 32767).astype('<i2').tobytes()); w.close()


def pop(f0, f1, dur, amp):
    t = np.arange(int(dur * SR)) / SR
    f = f0 + (f1 - f0) * np.clip(t / (dur * 0.6), 0, 1)
    ph = 2 * np.pi * np.cumsum(f) / SR
    env = np.minimum(1, t / 0.004) * np.exp(-t / (dur * 0.28))
    x = (np.sin(ph) + 0.15 * np.sin(2 * ph)) * env * amp
    y = np.zeros_like(x)
    for i in range(1, len(x)):
        y[i] = y[i - 1] + 0.35 * (x[i] - y[i - 1])   # 부드럽게 (저역 통과)
    return y


if __name__ == '__main__':
    save('jelly.wav', pop(420, 620, 0.11, 0.55))
    c, c2 = pop(520, 780, 0.12, 0.5), pop(700, 980, 0.12, 0.4)
    x = np.zeros(int(0.22 * SR)); x[:len(c)] += c; o = int(0.06 * SR); x[o:o + len(c2)] += c2
    save('coin.wav', x)
