# synth.py - 작은 신시사이저: 악기, 드럼, 리버브, 믹스 (크리스마스 캐럴 연주곡과 효과음 만들기)
import numpy as np
from scipy.signal import fftconvolve
from scipy.io import wavfile

SR = 32000
rng = np.random.default_rng(7)

NOTE = {'C':0,'D':2,'E':4,'F':5,'G':7,'A':9,'B':11}
def midi(n):
    n = n.strip()
    k = NOTE[n[0]]; i = 1
    while i < len(n) and n[i] in '#b':
        k += 1 if n[i] == '#' else -1; i += 1
    return 12 * (int(n[i:]) + 1) + k
def hz(m): return 440.0 * 2 ** ((m - 69) / 12)

def env_adsr(n, a, d, s, r, sus_len=None):
    a = max(1, int(a * SR)); d = max(1, int(d * SR)); r = max(1, int(r * SR))
    e = np.ones(n) * s
    e[:min(a, n)] = np.linspace(0, 1, a)[:min(a, n)]
    if a < n:
        k = min(d, n - a); e[a:a + k] = np.linspace(1, s, d)[:k]
    rs = max(0, n - r)
    e[rs:] *= np.linspace(1, 0, n - rs)
    return e

def additive(f, n, amps, decay=None, detune=0.0, vib=0.0, vibf=5.5, phase_rand=True):
    t = np.arange(n) / SR
    out = np.zeros(n)
    fm = f * (1 + vib * np.sin(2 * np.pi * vibf * t) * np.clip(t * 3 - 0.3, 0, 1))
    ph = 2 * np.pi * np.cumsum(fm) / SR
    for h, a in enumerate(amps, start=1):
        if f * h > SR * 0.45 or a == 0: continue
        p0 = rng.uniform(0, 6.28) if phase_rand else 0
        comp = a * np.sin(h * ph + p0)
        if decay is not None: comp *= np.exp(-t * decay * (1 + 0.35 * (h - 1)))
        out += comp
    return out

# ===== 악기 =====
def bell(f, dur, vel=1.0):          # 글로켄슈필 / 썰매 종 같은 맑은 종
    n = int((dur + 1.4) * SR); t = np.arange(n) / SR
    parts = [(1, 1.0, 3.0), (2.0, 0.32, 5.0), (3.0, 0.12, 7), (4.17, 0.18, 9), (5.43, 0.10, 12), (8.93, 0.04, 18)]
    out = np.zeros(n)
    for r, a, dc in parts:
        if f * r > SR * 0.45: continue
        out += a * np.sin(2 * np.pi * f * r * t + rng.uniform(0, 6)) * np.exp(-t * dc)
    att = np.minimum(1, t / 0.002)
    return out * att * vel * 0.55

def celesta(f, dur, vel=1.0):
    n = int((dur + 0.9) * SR); t = np.arange(n) / SR
    out = np.sin(2*np.pi*f*t)*np.exp(-t*4) + 0.25*np.sin(2*np.pi*f*2*t)*np.exp(-t*7) + 0.08*np.sin(2*np.pi*f*4*t)*np.exp(-t*12)
    return out * np.minimum(1, t/0.002) * vel * 0.5

def brass(f, dur, vel=1.0):         # 신나는 브라스 리드 (톱니파 계열, 밝기가 시간에 따라 열림)
    n = int((dur + 0.15) * SR)
    amps = [1/h for h in range(1, 16)]
    x = additive(f, n, amps, vib=0.006, vibf=5.8) * 0.9
    x += additive(f * 1.004, n, amps, vib=0.006, vibf=6.1) * 0.5
    e = env_adsr(n, 0.03, 0.12, 0.8, 0.12)
    # 간단한 밝기 조절: 고조파를 약하게 섞은 사인과 섞음
    t = np.arange(n) / SR
    soft = np.sin(2 * np.pi * np.cumsum(np.full(n, f)) / SR)
    bright = np.clip(t / 0.08, 0, 1)
    return (soft * (1 - bright) + x * bright * 0.55) * e * vel * 0.32

def lead_square(f, dur, vel=1.0):   # 칩튠 느낌 사각파 (쿠키 게임)
    n = int((dur + 0.08) * SR)
    amps = [(1/h if h % 2 else 0) for h in range(1, 22)]
    x = additive(f, n, amps, vib=0.004, vibf=6.5)
    e = env_adsr(n, 0.005, 0.08, 0.6, 0.06)
    return x * e * vel * 0.22

def pluck(f, dur, vel=1.0):         # 피아노/기타 같은 짧은 화음 스탭
    n = int((dur + 0.35) * SR)
    amps = [1, 0.55, 0.3, 0.18, 0.1, 0.06, 0.03]
    x = additive(f, n, amps, decay=5.5)
    t = np.arange(n) / SR
    return x * np.minimum(1, t / 0.003) * vel * 0.18

def piano(f, dur, vel=1.0):
    n = int((dur + 0.8) * SR)
    amps = [1, 0.6, 0.35, 0.22, 0.12, 0.08, 0.05, 0.03]
    x = additive(f, n, amps, decay=2.2) + additive(f * 1.0015, n, amps, decay=2.4) * 0.5
    t = np.arange(n) / SR
    rel = np.ones(n); k = int(dur * SR)
    if k < n: rel[k:] = np.exp(-(t[k:] - t[k]) * 9)
    return x * np.minimum(1, t / 0.002) * rel * vel * 0.16

def bass(f, dur, vel=1.0):
    n = int((dur + 0.1) * SR)
    x = additive(f, n, [1, 0.5, 0.25, 0.12, 0.06], decay=1.5)
    e = env_adsr(n, 0.006, 0.1, 0.75, 0.06)
    return x * e * vel * 0.42

def pad(f, dur, vel=1.0):
    n = int((dur + 0.6) * SR)
    amps = [1/h for h in range(1, 9)]
    x = sum(additive(f * d, n, amps, vib=0.003, vibf=4 + i) for i, d in enumerate([0.997, 1.0, 1.003]))
    e = env_adsr(n, 0.35, 0.3, 0.8, 0.5)
    return x * e * vel * 0.035

# ===== 드럼 =====
def kick(vel=1.0):
    n = int(0.35 * SR); t = np.arange(n) / SR
    f = 50 + 110 * np.exp(-t * 32)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)
    x += 0.3 * rng.standard_normal(n) * np.exp(-t * 300)
    return x * vel * 0.8

def snare(vel=1.0):
    n = int(0.25 * SR); t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    noise = noise - np.concatenate([[0], noise[:-1]]) * 0.6   # 고역 강조
    x = noise * np.exp(-t * 22) * 0.5 + np.sin(2 * np.pi * 190 * t) * np.exp(-t * 30) * 0.6
    return x * vel * 0.45

def hat(vel=1.0, open_=False):
    n = int((0.3 if open_ else 0.06) * SR); t = np.arange(n) / SR
    x = rng.standard_normal(n)
    x = x - np.concatenate([[0], x[:-1]])
    x = x - np.concatenate([[0], x[:-1]])
    return x * np.exp(-t * (12 if open_ else 70)) * vel * 0.09

def sleigh(vel=1.0):               # 썰매 방울: 짧은 금속성 짤랑
    n = int(0.22 * SR); t = np.arange(n) / SR
    out = np.zeros(n)
    for k in range(5):
        st = int(rng.uniform(0, 0.03) * SR)
        f = rng.uniform(4800, 9000)
        seg = np.sin(2 * np.pi * f * t[: n - st]) * np.exp(-t[: n - st] * rng.uniform(25, 45))
        out[st:] += seg * rng.uniform(0.4, 1)
    hp = rng.standard_normal(n); hp = hp - np.concatenate([[0], hp[:-1]])
    out += hp * np.exp(-t * 40) * 0.25
    return out * vel * 0.06

def clap(vel=1.0):
    n = int(0.2 * SR); t = np.arange(n) / SR
    x = np.zeros(n)
    for k in range(3):
        st = int(k * 0.011 * SR)
        nz = rng.standard_normal(n - st); nz = nz - np.concatenate([[0], nz[:-1]]) * 0.8
        x[st:] += nz * np.exp(-t[: n - st] * 45)
    return x * vel * 0.18

# ===== 믹스 =====
class Mix:
    def __init__(self, seconds):
        self.L = np.zeros(int(seconds * SR) + SR * 4); self.R = np.zeros_like(self.L)
        self.wetL = np.zeros_like(self.L); self.wetR = np.zeros_like(self.L)
    def add(self, x, t, pan=0.0, gain=1.0, wet=0.25):
        i = int(t * SR)
        if i >= len(self.L): return
        x = x[: len(self.L) - i] * gain
        l = np.cos((pan + 1) * np.pi / 4) * 1.414; r = np.sin((pan + 1) * np.pi / 4) * 1.414
        self.L[i:i + len(x)] += x * l; self.R[i:i + len(x)] += x * r
        if wet > 0:
            self.wetL[i:i + len(x)] += x * l * wet; self.wetR[i:i + len(x)] += x * r * wet
    def render(self, length, rev=1.6, out=None, fade=1.5):
        n = int(rev * SR); t = np.arange(n) / SR
        irL = rng.standard_normal(n) * np.exp(-t * 4.2); irR = rng.standard_normal(n) * np.exp(-t * 4.2)
        irL[: int(0.012 * SR)] = 0; irR[: int(0.017 * SR)] = 0
        irL /= np.sqrt((irL ** 2).sum()); irR /= np.sqrt((irR ** 2).sum())
        L = self.L + fftconvolve(self.wetL, irL)[: len(self.L)] * 0.9
        R = self.R + fftconvolve(self.wetR, irR)[: len(self.R)] * 0.9
        k = int(length * SR); L = L[:k]; R = R[:k]
        m = max(np.abs(L).max(), np.abs(R).max(), 1e-6)
        L /= m; R /= m
        L = np.tanh(L * 1.35) / np.tanh(1.35); R = np.tanh(R * 1.35) / np.tanh(1.35)
        f = int(fade * SR)
        if f > 0: L[-f:] *= np.linspace(1, 0, f); R[-f:] *= np.linspace(1, 0, f)
        st = np.stack([L, R], 1) * 0.92
        if out: wavfile.write(out, SR, (st * 32767).astype(np.int16))
        return st

def save_mono(x, path, peak=0.9):
    x = x / max(np.abs(x).max(), 1e-6) * peak
    wavfile.write(path, SR, (x * 32767).astype(np.int16))
