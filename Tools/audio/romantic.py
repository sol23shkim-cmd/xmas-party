# romantic.py - 잔잔하지만 늘어지지 않는 로맨틱한 캐럴 3곡 (고요한 밤, 오 크리스마스 트리, 첫 번째 노엘)
# 오르골·피아노 멜로디 + 현악 패드 + 부드러운 베이스 + 브러시 드럼, 왈츠(3/4)
import sys, numpy as np
from synth import *
from carols import chord, mel

def brush(vel=1.0):
    n = int(0.28 * SR); t = np.arange(n) / SR
    x = rng.standard_normal(n); x = x - np.concatenate([[0], x[:-1]]) * 0.9
    return x * np.sin(np.pi * np.clip(t / 0.28, 0, 1)) ** 2 * vel * 0.035

def soft_kick(vel=1.0):
    n = int(0.3 * SR); t = np.arange(n) / SR
    f = 45 + 50 * np.exp(-t * 25)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 10) * vel * 0.45

def musicbox(f, dur, vel=1.0):
    n = int((dur + 1.6) * SR); t = np.arange(n) / SR
    x = np.sin(2*np.pi*f*t)*np.exp(-t*2.2) + 0.35*np.sin(2*np.pi*f*3.01*t)*np.exp(-t*5) + 0.12*np.sin(2*np.pi*f*5.4*t)*np.exp(-t*9)
    return x * np.minimum(1, t / 0.003) * vel * 0.42

def strings(f, dur, vel=1.0):
    n = int((dur + 0.8) * SR)
    amps = [1, 0.5, 0.33, 0.25, 0.18, 0.12, 0.08]
    x = sum(additive(f * d, n, amps, vib=0.004, vibf=5 + i * 0.4) for i, d in enumerate([0.996, 1.0, 1.004]))
    return x * env_adsr(n, 0.45, 0.3, 0.85, 0.7) * vel * 0.03

SONGS = {
 'silent': dict(bpm=104, meter=3, tr=0, lead2_up=1, notes=mel(
   "G4:1.5 A4:.5 G4:1 | E4:3 | G4:1.5 A4:.5 G4:1 | E4:3 | D5:2 D5:1 | B4:3 | C5:2 C5:1 | G4:3 |"
   "A4:2 A4:1 | C5:1.5 B4:.5 A4:1 | G4:1.5 A4:.5 G4:1 | E4:3 | A4:2 A4:1 | C5:1.5 B4:.5 A4:1 | G4:1.5 A4:.5 G4:1 | E4:3 |"
   "D5:2 D5:1 | F5:1.5 D5:.5 B4:1 | C5:3 | E5:3 | C5:1 G4:1 E4:1 | G4:1.5 F4:.5 D4:1 | C4:3 | R:3"),
   chords="C C C C G G C C F F C C F F C C G G7 C C C G7 C C".split()),
 'tree': dict(bpm=112, meter=3, tr=0, lead2_up=2, notes=mel(
   "R:2 C4:1 | F4:.75 F4:.25 F4:1.5 G4:.5 | A4:.75 A4:.25 A4:1.5 A4:.5 | G4:.5 A4:.5 Bb4:1 E4:1 | G4:1 F4:1 C4:1 |"
   "F4:.75 F4:.25 F4:1.5 G4:.5 | A4:.75 A4:.25 A4:1.5 A4:.5 | G4:.5 A4:.5 Bb4:1 E4:1 | G4:1 F4:1 R:1 |"
   "C5:.5 C5:.5 A4:.5 D5:1 C5:.5 | C5:.5 Bb4:.5 Bb4:1.5 Bb4:.5 | Bb4:.5 G4:.5 C5:1.5 Bb4:.5 | Bb4:.5 A4:.5 A4:1 C4:1 |"
   "F4:.75 F4:.25 F4:1.5 G4:.5 | A4:.75 A4:.25 A4:1.5 A4:.5 | G4:.5 A4:.5 Bb4:1 E4:1 | G4:1 F4:2"),
   chords="F F F C7 F F F C7 F F C7 C7 F F F C7 F".split()),
 'noel': dict(bpm=120, meter=3, tr=0, lead2_up=1, notes=mel(
   "R:2 F#4:.5 E4:.5 | D4:1.5 E4:.5 F#4:.5 G4:.5 | A4:2 B4:.5 C#5:.5 | D5:1 C#5:1 B4:1 | A4:2 B4:.5 C#5:.5 | D5:1 C#5:1 B4:1 | A4:1 B4:1 C#5:1 | D5:1 A4:1 G4:1 | F#4:2 F#4:.5 E4:.5 |"
   "D4:1.5 E4:.5 F#4:.5 G4:.5 | A4:2 B4:.5 C#5:.5 | D5:1 C#5:1 B4:1 | A4:2 B4:.5 C#5:.5 | D5:1 C#5:1 B4:1 | A4:1 B4:1 C#5:1 | D5:1 A4:1 G4:1 | F#4:2 F#4:.5 E4:.5 |"
   "D4:1.5 E4:.5 F#4:.5 G4:.5 | A4:2 D5:.5 C#5:.5 | B4:2 B4:1 | A4:3 | D5:1 C#5:1 B4:1 | A4:1 B4:1 C#5:1 | D5:1 A4:1 G4:1 | F#4:3"),
   chords="D D D G D G A A7 D D D G D G A A7 D D D G D G A A7 D".split()),
}

def arrange(name, sd, out):
    bpm, meter = sd['bpm'], sd['meter']; beat = 60 / bpm; bar = beat * meter
    notes, chords = sd['notes'], sd['chords']
    nb = len(chords)
    form = [('musicbox', 0, 0), ('piano', 0, 1), ('piano', sd['lead2_up'], 1)]
    total = (4 + nb * len(form) + 3) * bar + 4
    mx = Mix(total)
    def comp(t, tr, energy):
        for b, ch in enumerate(chords):
            tb = t + b * bar; root, iv = chord(ch, tr)
            mx.add(bass(hz(root - 12), beat * 1.6, 0.55), tb, 0, 0.8, 0.05)
            if energy: mx.add(bass(hz(root - 12 + 7), beat * 0.9, 0.4), tb + 2 * beat, 0, 0.8, 0.05)
            # 피아노 왈츠 아르페지오 (8분음)
            arp = [root + 12, root + 12 + iv[1], root + 12 + iv[2], root + 24, root + 12 + iv[2], root + 12 + iv[1]]
            for k in range(6): mx.add(piano(hz(arp[k]), beat * 0.6, 0.55 if k else 0.7), tb + k * beat / 2, 0.25, 0.75, 0.45)
            for i in iv: mx.add(strings(hz(root + 12 + i), bar, 1.0), tb, -0.3, 1.0, 0.55)
            if energy:
                mx.add(soft_kick(0.8), tb, 0, 0.8, 0.05)
                for k in (1, 2): mx.add(brush(1.0), tb + k * beat, 0.2, 0.9, 0.2)
                mx.add(sleigh(0.35), tb + 2.5 * beat, -0.4, 0.5, 0.3)
    def melody(t, inst, tr):
        tt = t
        for m, d in notes:
            if m is not None:
                f = hz(m + tr)
                if inst == 'musicbox': mx.add(musicbox(f * 2, d * beat, 1.0), tt, 0, 1.0, 0.5)
                else:
                    mx.add(piano(f * 2, d * beat * 0.95, 1.35), tt, -0.1, 1.0, 0.45)
                    mx.add(celesta(f * 4, d * beat, 0.25), tt, 0.3, 0.5, 0.5)
            tt += d * beat
    t = 0
    comp(t, 0, 0)
    for k in range(8): mx.add(musicbox(hz(84 + [0, 4, 7, 12, 7, 4, 0, -5][k]), 0.4, 0.5), t + k * bar / 2, 0.2, 0.6, 0.5)
    t += 4 * bar
    for inst, tr, en in form:
        comp(t, tr, en); melody(t, inst, tr); t += nb * bar
    root, iv = chord(chords[-1], form[-1][1])
    for m in [root + 12 + i for i in iv] + [root + 24]: mx.add(piano(hz(m), bar * 2.5, 0.9), t, 0.2, 1.1, 0.5); mx.add(strings(hz(m), bar * 2.5, 1.2), t, -0.2, 1, 0.5)
    mx.add(bass(hz(root - 12), bar * 2, 0.6), t)
    for k in range(6): mx.add(musicbox(hz(root + 24 + [0, 4, 7, 12, 16, 19][k] % 24), 0.5, 0.45), t + k * 0.12, 0.3, 0.6, 0.6)
    L = t + bar * 3
    mx.render(L, out=out, fade=1.5)
    print(name, round(L, 1))

if __name__ == '__main__':
    for n in (sys.argv[1:] or SONGS): arrange(n, SONGS[n], f'out/carol_{n}.wav')
