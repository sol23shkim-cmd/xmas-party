# cookie.py - 쿠키 달리기 전용 음악(끊김 없이 반복) + 효과음
import numpy as np
from synth import *
from carols import chord, mel

def theme(out):
    bpm = 152; beat = 60 / bpm; bar = 4 * beat
    A = mel("E5:.5 G5:.5 C6:.5 G5:.5 E5:.5 G5:.5 A5:.5 G5:.5 | E5:.5 A5:.5 C6:.5 A5:.5 B5:.5 C6:.5 D6:1 | C6:.75 A5:.25 F5:.5 A5:.5 C6:.5 D6:.5 C6:.5 A5:.5 | B5:.5 G5:.5 D5:.5 G5:.5 B5:1 R:1 |"
            "E5:.5 G5:.5 C6:.5 E6:.5 D6:.5 C6:.5 G5:1 | A5:.5 C6:.5 E6:.5 C6:.5 B5:.5 A5:.5 G5:1 | F5:.5 A5:.5 C6:.5 A5:.5 G5:.5 B5:.5 D6:.5 F6:.5 | E6:1.5 D6:.25 E6:.25 C6:2 |"
            "A5:1 C6:.5 A5:.5 F5:1 A5:1 | B5:1 D6:.5 B5:.5 G5:1 B5:1 | G5:.5 B5:.5 E6:.5 B5:.5 G5:.5 B5:.5 E6:1 | C6:.5 E6:.5 A6:.5 E6:.5 C6:.5 A5:.5 E5:1 |"
            "F5:.5 A5:.5 D6:.5 F6:.5 E6:.5 D6:.5 C6:.5 A5:.5 | B5:.5 D6:.5 G6:.5 F6:.5 E6:.5 D6:.5 C6:.5 B5:.5 | C6:.5 E6:.5 G6:1 E6:.5 C6:.5 G5:1 | G5:.5 A5:.5 B5:.5 C6:.5 D6:.5 E6:.5 F6:.5 G6:.5")
    ch = "C Am F G C Am F C F G Em Am Dm G C G7".split()
    loops = 3; L = len(ch) * bar
    mx = Mix(L * loops + 2)
    for lp in range(loops):
        t0 = lp * L
        tt = t0
        for m, d in A:
            if m is not None:
                mx.add(lead_square(hz(m - 12), d * beat * 0.85, 1.0), tt, -0.1, 1.0, 0.2)
                mx.add(celesta(hz(m), d * beat, 0.5), tt, 0.35, 0.45, 0.3)
            tt += d * beat
        for b, c in enumerate(ch):
            tb = t0 + b * bar
            root, iv = chord(c)
            for k in range(8):
                mx.add(bass(hz(root - 12 + (12 if k % 2 else 0)), beat * 0.4, 0.95 if k % 2 == 0 else 0.7), tb + k * beat / 2, 0, 1.0, 0.02)
            for off in (0.5, 1.5, 2.5, 3.5):
                for i in iv: mx.add(pluck(hz(root + 12 + i), beat * 0.25, 0.8), tb + off * beat, 0.3, 0.9, 0.25)
            for i in iv: mx.add(pad(hz(root + 12 + i), bar, 0.8), tb, -0.3, 1.0, 0.4)
            for k in range(16):
                ts = tb + k * beat / 4
                if k in (0, 6, 8) : mx.add(kick(1.0), ts, 0, 0.95, 0.03)
                if k in (4, 12): mx.add(snare(1.0), ts, 0.05, 0.7, 0.2); mx.add(clap(1.0), ts, -0.1, 0.55, 0.25)
                mx.add(hat(0.6 + 0.4 * (k % 2 == 0), open_=(k == 14)), ts, 0.35, 0.75, 0.05)
                if k % 2 == 0: mx.add(sleigh(0.6), ts, -0.4, 0.7, 0.15)
    st = mx.render(L * loops, fade=0)
    a = int(L * SR); seg = st[a:2 * a]   # 가운데 한 바퀴 (앞뒤 꼬리까지 이어져서 끊김 없이 반복)
    wavfile.write(out, SR, (seg / np.abs(seg).max() * 0.9 * 32767).astype(np.int16))
    print('theme', round(L, 2), 's')

def tone_sweep(f0, f1, dur, kind='sq', vol=1.0, curve=1.0):
    n = int(dur * SR); t = np.arange(n) / SR
    f = f0 + (f1 - f0) * (t / dur) ** curve
    ph = 2 * np.pi * np.cumsum(f) / SR
    if kind == 'sq': x = sum(np.sin(h * ph) / h for h in (1, 3, 5, 7, 9))
    elif kind == 'tri': x = sum(((-1) ** ((h - 1) // 2)) * np.sin(h * ph) / (h * h) for h in (1, 3, 5, 7))
    else: x = np.sin(ph)
    return x * vol

def env(n, a=0.005, r=0.05):
    e = np.ones(n); a = int(a * SR); r = int(r * SR)
    e[:a] = np.linspace(0, 1, a); e[-r:] *= np.linspace(1, 0, r)
    return e

def cat(*xs):
    n = max(int(s * SR) + len(x) for s, x in xs)
    out = np.zeros(n)
    for s, x in xs: i = int(s * SR); out[i:i + len(x)] += x
    return out

def sfx(d):
    def S(name, x, peak=0.85): save_mono(x, f'{d}/{name}.wav', peak)
    x = tone_sweep(320, 820, 0.13, 'sq', 0.5, 0.6); S('jump', x * env(len(x), 0.003, 0.05))
    x = tone_sweep(520, 1300, 0.16, 'sq', 0.5, 0.5); x = cat((0, x * env(len(x))), (0.05, bell(hz(96), 0.2, 0.6)), (0.09, bell(hz(100), 0.2, 0.5))); S('jump2', x)
    n = int(0.32 * SR); t = np.arange(n) / SR; nz = rng.standard_normal(n)
    from scipy.signal import butter, lfilter
    b, a = butter(2, [900 / (SR / 2), 3500 / (SR / 2)], 'band'); x = lfilter(b, a, nz) * np.sin(np.pi * t / 0.32) ** 1.5; S('slide', x, 0.6)
    x = cat((0, bell(hz(88), 0.12, 1.0)), (0.0, bell(hz(95), 0.12, 0.4))); S('jelly', x[: int(0.5 * SR)], 0.7)
    x = cat(*[(k * 0.045, bell(hz(84 + [0, 4, 7, 12, 16][k]), 0.2, 0.9)) for k in range(5)]); S('star', x[: int(0.9 * SR)])
    x = cat(*[(k * 0.06, lead_square(hz(72 + [0, 4, 7, 12, 16, 19, 24][k]), 0.07, 1.0)) for k in range(7)], (0.2, bell(hz(96), 0.4, 0.6))); S('power', x[: int(1.0 * SR)])
    x = cat((0, bell(hz(79), 0.3, 1.0)), (0.12, bell(hz(84), 0.4, 1.0)), (0.12, celesta(hz(91), 0.4, 0.6))); S('heart', x[: int(1.0 * SR)])
    n = int(0.35 * SR); t = np.arange(n) / SR
    x = cat((0, kick(1.2)), (0, tone_sweep(420, 110, 0.32, 'sq', 0.45, 0.7) * env(int(0.32 * SR)))); S('hit', x)
    nz = rng.standard_normal(int(0.4 * SR)); tt = np.arange(len(nz)) / SR
    x = cat((0, kick(1.3)), (0, nz * np.exp(-tt * 9) * 0.6), (0.02, snare(1.0))); S('smash', x)
    x = tone_sweep(900, 180, 0.7, 'tri', 0.6, 1.4); S('fall', x * env(len(x), 0.01, 0.1))
    x = tone_sweep(880, 880, 0.12, 'sq', 0.4) * env(int(0.12 * SR)); S('count', x, 0.6)
    x = cat((0, tone_sweep(1320, 1320, 0.35, 'sq', 0.3) * env(int(0.35 * SR), 0.005, 0.2)), (0, bell(hz(96), 0.4, 0.8))); S('go', x, 0.75)
    x = cat(*[(k * 0.18, brass(hz([76, 72, 67, 60][k]), 0.16 if k < 3 else 0.6, 1.0)) for k in range(4)]); S('gameover', x)
    seq = [(0, 67, .12), (.13, 72, .12), (.26, 76, .12), (.39, 79, .3), (.72, 76, .12), (.85, 79, .7)]
    x = cat(*[(s, brass(hz(m), d, 1.0)) for s, m, d in seq], *[(s, bell(hz(m + 12), d + .2, .5)) for s, m, d in seq], (0.85, celesta(hz(96), 0.8, 0.5)))
    S('record', x)
    x = cat((0, bell(hz(100), 0.05, 0.6))); S('click', x[: int(0.25 * SR)], 0.5)
    nz = rng.standard_normal(int(0.8 * SR)); tt = np.arange(len(nz)) / SR
    b, a = butter(2, 2000 / (SR / 2), 'high'); wh = lfilter(b, a, nz) * np.sin(np.pi * tt / 0.8) * 0.4
    x = cat((0, wh), *[(0.1 + k * 0.05, bell(hz(84 + [0, 4, 7, 12, 16, 19, 24, 28][k]), 0.2, 0.7)) for k in range(8)]); S('fever', x)
    x = cat((0, bell(hz(91), 0.1, 1.0)), (0.07, bell(hz(98), 0.15, 0.8))); S('coin', x[: int(0.6 * SR)])

if __name__ == '__main__':
    import os; os.makedirs('out/sfx', exist_ok=True)
    theme('out/cookie_theme.wav')
    sfx('out/sfx')
