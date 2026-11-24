# pillow.py - 베개 싸움 효과음: 푹신한 '퍽'(pillow_hit), 휘두르는 '휙'(whoosh)
# 실행: python3 pillow.py → Assets/Resources/Audio/Sfx/pillow_hit.wav, whoosh.wav
import os, wave
import numpy as np

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'Audio', 'Sfx')
rng = np.random.default_rng(3)


def save(name, x):
    x = x / max(1e-6, np.max(np.abs(x))) * 0.9
    w = wave.open(os.path.join(OUT, name), 'wb')
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes()); w.close()


def lowpass(x, a):
    y = np.zeros_like(x)
    for i in range(1, len(x)): y[i] = y[i - 1] + a * (x[i] - y[i - 1])
    return y


def pillow_hit():
    n = int(0.42 * SR); t = np.arange(n) / SR
    f = 95 * np.exp(-t * 9) + 42                      # 낮게 떨어지는 '둥'
    thump = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 16)
    noise = rng.standard_normal(n)
    puff = lowpass(lowpass(noise, 0.09), 0.12) * np.minimum(1, t / 0.006) * np.exp(-t * 22) * 3.2   # 천에 바람 빠지는 '퍽'
    rustle = lowpass(noise, 0.25) * np.exp(-((t - 0.09) / 0.07) ** 2) * 0.35                          # 깃털 사각
    return thump * 0.9 + puff + rustle


def whoosh():
    n = int(0.32 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    env = np.sin(np.clip(t / 0.3, 0, 1) * np.pi) ** 2
    a = 0.04 + 0.22 * env                              # 빨라질수록 밝게
    y = np.zeros(n)
    for i in range(1, n): y[i] = y[i - 1] + a[i] * (noise[i] - y[i - 1])
    return y * env


if __name__ == '__main__':
    save('pillow_hit.wav', pillow_hit())
    save('whoosh.wav', whoosh() * 0.6)
