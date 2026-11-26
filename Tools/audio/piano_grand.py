# piano_grand.py - 우아한 그랜드 피아노 건반 소리: 건반마다 따로(MIDI 48~84) 만들어서 음정을 늘이지 않음 (11-26)
#  - 줄 3가닥(아주 조금씩 다른 음높이 → 부드러운 울림), 살짝 어긋난 배음(진짜 피아노 현), 해머 위치에 따른 배음 세기
#  - 빠르게 줄어드는 첫소리 + 길게 남는 여운(두 단계 감쇠), 부드러운 해머 소리, 작은 홀 잔향
#  - 건반마다 소리 크기를 고르게 맞춤
# 실행: python3 piano_grand.py → Assets/Resources/Audio/Sfx/piano_n48.wav ~ piano_n84.wav
import os, wave
import numpy as np
from scipy.signal import fftconvolve, butter, lfilter

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'Audio', 'Sfx')
rng = np.random.default_rng(23)


def hz(m): return 440.0 * 2 ** ((m - 69) / 12)


def save(name, x):
    w = wave.open(os.path.join(OUT, name), 'wb')
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes()); w.close()


# 작은 홀 잔향 (모든 건반 같은 것)
_n = int(1.1 * SR); _t = np.arange(_n) / SR
IR = rng.standard_normal(_n) * np.exp(-_t * 5.5)
IR[: int(0.018 * SR)] = 0
b, a = butter(1, 4200 / (SR / 2), 'low'); IR = lfilter(b, a, IR)
IR /= np.sqrt((IR ** 2).sum())


def note(m, dur=2.6):
    f = hz(m)
    n = int(dur * SR); t = np.arange(n) / SR
    out = np.zeros(n)
    B = 0.00007 * (f / 261.6) ** 0.6                      # 현의 뻣뻣함 (높을수록 조금 더)
    strings = [(-0.35, 1.0), (0.0, 0.92), (0.45, 0.85)]   # 센트, 세기
    x0 = 0.122                                            # 해머가 치는 자리 (현 길이의 약 1/8)
    hmax = int(min(24, 9000 / f))
    for h in range(1, hmax + 1):
        fh = f * h * np.sqrt(1 + B * h * h)
        amp = abs(np.sin(np.pi * h * x0)) / h ** 0.95 * np.exp(-fh / 4200)
        if amp < 1e-4: continue
        r1 = 1.6 + 0.0028 * fh                            # 첫소리
        r2 = 0.25 + 0.00055 * fh                          # 여운
        env = 0.62 * np.exp(-t * r1) + 0.38 * np.exp(-t * r2)
        for cents, sa in strings:
            ph = rng.uniform(0, 2 * np.pi)
            out += amp * sa * np.sin(2 * np.pi * fh * 2 ** (cents / 1200) * t + ph) * env
    # 해머 (부드러운 '톡')
    nz = rng.standard_normal(n)
    bb, aa = butter(2, [900 / (SR / 2), min(5000, f * 8) / (SR / 2)], 'band')
    out += lfilter(bb, aa, nz) * np.exp(-t * 90) * 0.06
    out *= np.minimum(1, t / 0.0018)                      # 아주 짧은 시작
    # 몸통의 따뜻함: 높은 소리를 살짝 누그러뜨림
    bl, al = butter(1, min(6500, 1800 + f * 4) / (SR / 2), 'low')
    out = lfilter(bl, al, out)
    # 잔향
    wet = fftconvolve(out, IR)[:n]
    out = out + wet * 0.16
    # 끝은 천천히 줄이기
    k = int(0.45 * SR); out[-k:] *= np.linspace(1, 0, k) ** 1.5
    # 크기 맞추기: 처음 0.4초의 RMS를 고르게 (높은 음은 조금 작게 — 귀에 고르게)
    rms = np.sqrt(np.mean(out[: int(0.4 * SR)] ** 2))
    target = 0.16 * (1 - 0.25 * (m - 48) / 36)
    out *= target / max(rms, 1e-6)
    return np.tanh(out * 1.1) / np.tanh(1.1)


if __name__ == '__main__':
    for m in range(48, 85):
        save('piano_n%d.wav' % m, note(m))
    print('ok', 85 - 48, 'notes')
