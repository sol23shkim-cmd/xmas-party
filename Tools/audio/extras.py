# extras.py - 피아노 건반 소리(C3·C4·C5) + 눈싸움·퀴즈 효과음
import numpy as np
from synth import *
from scipy.signal import butter, lfilter

def grand(f, dur=3.2):
    n = int(dur * SR); t = np.arange(n) / SR
    out = np.zeros(n)
    for h, a in enumerate([1, 0.55, 0.32, 0.2, 0.12, 0.08, 0.05, 0.03, 0.02], start=1):
        fh = f * h * (1 + 0.0004 * h * h)          # 살짝 어긋난 배음 (진짜 피아노처럼)
        if fh > SR * 0.45: break
        dec = 0.9 + 0.55 * h
        out += a * (np.sin(2*np.pi*fh*t) + 0.6*np.sin(2*np.pi*fh*1.0012*t)) * np.exp(-t * dec)
    hammer = rng.standard_normal(n) * np.exp(-t * 120) * 0.08
    x = (out + hammer) * np.minimum(1, t / 0.003)
    return x

def main():
    for name, m in (("piano_c3", 48), ("piano_c4", 60), ("piano_c5", 72)):
        save_mono(grand(hz(m)), f"out/sfx/{name}.wav", 0.8)
    n = int(0.35 * SR); t = np.arange(n) / SR
    nz = rng.standard_normal(n); b, a = butter(2, [600 / (SR / 2), 2500 / (SR / 2)], 'band')
    save_mono(lfilter(b, a, nz) * np.sin(np.pi * t / 0.35) ** 2, "out/sfx/throw.wav", 0.5)
    n = int(0.3 * SR); t = np.arange(n) / SR
    nz = rng.standard_normal(n); b, a = butter(2, 1800 / (SR / 2), 'low')
    x = lfilter(b, a, nz) * np.exp(-t * 18) + 0.4 * np.sin(2*np.pi*120*t) * np.exp(-t * 30)
    save_mono(x, "out/sfx/splat.wav", 0.8)
    def tone(f, d, v=1.0):
        n = int(d * SR); t = np.arange(n) / SR
        return (np.sin(2*np.pi*f*t) + 0.3*np.sin(4*np.pi*f*t)) * np.exp(-t * 6) * v
    x = np.concatenate([tone(hz(76), 0.12), tone(hz(83), 0.5)]); save_mono(x, "out/sfx/correct.wav", 0.7)
    x = np.concatenate([tone(hz(55), 0.15), tone(hz(52), 0.4)]); save_mono(x, "out/sfx/wrong.wav", 0.6)
    save_mono(tone(hz(88), 0.08), "out/sfx/tick.wav", 0.4)
main()
