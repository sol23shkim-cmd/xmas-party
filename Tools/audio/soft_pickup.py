# soft_pickup.py - 쿠키 달리기 젤리·코인 먹는 소리 (부드럽고 짧은 '퐁' 소리, 11-24 교체)
# 11-28: 너무 작아 안 들렸음 → 마림바처럼 통통한 소리로 바꾸고 게임 쪽 음량을 올림 (고음 성분은 4kHz 위로 없게)
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


def marimba(f, dur, amp):
    # 나무 건반 느낌: 기본음 + 빨리 사라지는 4배음, 부드러운 시작, 저역 통과
    t = np.arange(int(dur * SR)) / SR
    env = np.minimum(1, t / 0.003) * np.exp(-t / (dur * 0.3))
    x = np.sin(2 * np.pi * f * t) * env
    x += 0.35 * np.sin(2 * np.pi * f * 4 * t) * np.minimum(1, t / 0.002) * np.exp(-t / 0.012)
    x += 0.25 * np.sin(2 * np.pi * f * 0.5 * t) * env * np.exp(-t / 0.03)   # 살짝 통통한 몸통
    y = np.zeros_like(x); a = 0.45
    for i in range(1, len(x)):
        y[i] = y[i - 1] + a * (x[i] - y[i - 1])
    return y / np.max(np.abs(y)) * amp


if __name__ == '__main__':
    save('jelly.wav', marimba(659.3, 0.16, 0.7))                  # 미(E5) '통'
    a, b = marimba(784, 0.14, 0.62), marimba(1046.5, 0.2, 0.55)    # 솔→도 '통통'
    x = np.zeros(int(0.28 * SR)); x[:len(a)] += a; o = int(0.055 * SR); x[o:o + len(b)] += b
    save('coin.wav', x / np.max(np.abs(x)) * 0.7)
    raise SystemExit

    save('jelly.wav', pop(420, 620, 0.11, 0.55))
    c, c2 = pop(520, 780, 0.12, 0.5), pop(700, 980, 0.12, 0.4)
    x = np.zeros(int(0.22 * SR)); x[:len(c)] += c; o = int(0.06 * SR); x[o:o + len(c2)] += c2
    save('coin.wav', x)
