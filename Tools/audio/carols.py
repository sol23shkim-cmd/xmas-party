# carols.py - 저작권이 끝난 크리스마스 캐럴 멜로디를 신나는 연주곡으로 편곡해서 WAV로 만듦
# 곡: 징글벨, 우리 모두 즐거운 성탄, 집을 꾸미자(Deck the Halls), 기쁘다 구주 오셨네, 천사 찬송하기를, 천사들의 노래가
import sys, numpy as np
from synth import *

QUAL = {'': [0, 4, 7], 'm': [0, 3, 7], '7': [0, 4, 7, 10], 'm7': [0, 3, 7, 10], 'maj7': [0, 4, 7, 11], 'dim': [0, 3, 6], 'sus': [0, 5, 7], '6': [0, 4, 7, 9]}
def chord(sym, tr=0):
    i = 1
    if len(sym) > 1 and sym[1] in '#b': i = 2
    root = NOTE[sym[0]] + (1 if i == 2 and sym[1] == '#' else -1 if i == 2 else 0)
    q = sym[i:]
    return 48 + ((root + tr) % 12), QUAL.get(q, QUAL[''])

def mel(s):
    out = []
    for tok in s.replace('|', ' ').split():
        n, d = tok.split(':')
        out.append((None if n == 'R' else midi(n), float(d)))
    return out

SONGS = {}
SONGS['jingle'] = dict(bpm=146, meter=4, swing=0.0, sections={
    'V': (mel("G4:1 E5:1 D5:1 C5:1 | G4:3 G4:.5 G4:.5 | G4:1 E5:1 D5:1 C5:1 | A4:4 | A4:1 F5:1 E5:1 D5:1 | B4:4 | G5:1 G5:1 F5:1 D5:1 | E5:4 |"
              "G4:1 E5:1 D5:1 C5:1 | G4:4 | G4:1 E5:1 D5:1 C5:1 | A4:3 A4:1 | A4:1 F5:1 E5:1 D5:1 | G5:1 G5:1 G5:1 G5:1 | A5:1 G5:1 F5:1 D5:1 | C5:2 R:2"),
          "C C C F Dm G G7 C C C C F Dm G G7 C".split()),
    'C': (mel("E5:1 E5:1 E5:2 | E5:1 E5:1 E5:2 | E5:1 G5:1 C5:1.5 D5:.5 | E5:4 | F5:1 F5:1 F5:1.5 F5:.5 | F5:1 E5:1 E5:1 E5:.5 E5:.5 | E5:1 D5:1 D5:1 E5:1 | D5:2 G5:2 |"
              "E5:1 E5:1 E5:2 | E5:1 E5:1 E5:2 | E5:1 G5:1 C5:1.5 D5:.5 | E5:4 | F5:1 F5:1 F5:1.5 F5:.5 | F5:1 E5:1 E5:1 E5:.5 E5:.5 | G5:1 G5:1 F5:1 D5:1 | C5:4"),
          "C C C C F C D7 G C C C C F C G7 C".split()),
}, form=[('C', 'bell', 0, 1), ('V', 'brass', 0, 1), ('C', 'brass', 0, 2), ('C', 'bell', 1, 2)], key=0)

SONGS['wish'] = dict(bpm=168, meter=3, swing=0.0, sections={
    'A': (mel("G4:1 G4:.5 A4:.5 G4:.5 F#4:.5 | E4:1 E4:1 E4:1 | A4:1 A4:.5 B4:.5 A4:.5 G4:.5 | F#4:1 D4:1 D4:1 | B4:1 B4:.5 C5:.5 B4:.5 A4:.5 | G4:1 E4:1 D4:.5 D4:.5 | E4:1 A4:1 F#4:1 | G4:2 D4:1 |"
              "G4:1 G4:1 G4:1 | F#4:2 F#4:1 | G4:1 F#4:1 E4:1 | D4:2 A4:1 | B4:1 A4:1 G4:1 | D5:1 D4:1 D4:.5 D4:.5 | E4:1 A4:1 F#4:1 | G4:2 D4:1"),
          "G C A7 D B7 C D G G D Em A7 G D7 D G".split()),
}, form=[('A', 'bell', 0, 1), ('A', 'brass', 0, 2), ('A', 'brass', 2, 2)], key=0)

SONGS['deck'] = dict(bpm=150, meter=4, swing=0.0, sections={
    'A': (mel("C5:1.5 Bb4:.5 A4:1 G4:1 | F4:1 G4:1 A4:1 F4:1 | G4:.5 A4:.5 Bb4:.5 G4:.5 A4:1.5 G4:.5 | F4:1 E4:1 F4:2 |"
              "C5:1.5 Bb4:.5 A4:1 G4:1 | F4:1 G4:1 A4:1 F4:1 | G4:.5 A4:.5 Bb4:.5 G4:.5 A4:1.5 G4:.5 | F4:1 E4:1 F4:2 |"
              "G4:1.5 A4:.5 Bb4:1 G4:1 | A4:1.5 Bb4:.5 C5:1 G4:1 | A4:.5 B4:.5 C5:1 D5:.5 E5:.5 F5:1 | E5:1 D5:1 C5:2 |"
              "C5:1.5 Bb4:.5 A4:1 G4:1 | F4:1 G4:1 A4:1 F4:1 | D5:.5 D5:.5 D5:.5 D5:.5 C5:1.5 Bb4:.5 | A4:1 G4:1 F4:2"),
          "F F C7 F F F C7 F C C G7 C F F Bb F".split()),
}, form=[('A', 'bell', 0, 1), ('A', 'brass', 0, 2), ('A', 'bell', 1, 2)], key=0)

SONGS['joy'] = dict(bpm=126, meter=4, swing=0.0, sections={
    'A': (mel("D5:1 C#5:.75 B4:.25 A4:1.5 G4:.5 | F#4:1 E4:1 D4:1.5 A4:.5 | B4:1.5 B4:.5 C#5:1.5 C#5:.5 | D5:3 D5:1 |"
              "C#5:.5 B4:.5 A4:1 A4:.75 G4:.25 F#4:1 | F#4:.5 F#4:.5 F#4:.5 F#4:.5 F#4:.25 G4:.25 A4:1.5 | G4:.25 F#4:.25 E4:.5 E4:.5 E4:.5 E4:.25 F#4:.25 G4:1.5 |"
              "F#4:.25 E4:.25 D4:.5 D5:1 B4:.5 A4:.75 G4:.25 F#4:.5 | G4:.5 F#4:.5 E4:1 D4:2"),
          "D D G D D D A7 D D".split()),
}, form=[('A', 'bell', 0, 1), ('A', 'brass', 0, 2), ('A', 'brass', 1, 2), ('A', 'bell', 2, 2)], key=0)

SONGS['hark'] = dict(bpm=138, meter=4, swing=0.0, sections={
    'A': (mel("C4:1 F4:1 F4:1.5 E4:.5 | F4:1 A4:1 A4:1 G4:1 | C5:1 C5:1 C5:1.5 Bb4:.5 | A4:1 G4:1 A4:2 |"
              "C4:1 F4:1 F4:1.5 E4:.5 | F4:1 A4:1 A4:1 G4:1 | C5:1 G4:1 G4:1.5 E4:.5 | E4:1 D4:1 C4:2 |"
              "C5:1 C5:1 C5:1 F4:1 | Bb4:1 A4:1 A4:1 G4:1 | C5:1 C5:1 C5:1 F4:1 | Bb4:1 A4:1 A4:1 G4:1 |"
              "D5:1.5 D5:.5 D5:1 C5:1 | Bb4:1 A4:1 Bb4:2 | G4:1 A4:.5 Bb4:.5 C5:1.5 F4:.5 | F4:1 G4:1 A4:2 |"
              "D5:1.5 D5:.5 D5:1 C5:1 | Bb4:1 A4:1 Bb4:2 | G4:1 A4:.5 Bb4:.5 C5:1.5 F4:.5 | F4:1 G4:1 F4:2"),
          "F F C7 F F F C C F C F C Bb F Gm7 F Bb F Gm7 C7".split()),
}, form=[('A', 'bell', 0, 1), ('A', 'brass', 0, 2), ('A', 'brass', 2, 2)], key=0)

SONGS['angels'] = dict(bpm=144, meter=4, swing=0.0, sections={
    'A': (mel("A4:1 A4:1 A4:1 C5:1 | C5:1.5 Bb4:.5 A4:2 | A4:1 G4:1 A4:1 C5:1 | A4:1.5 G4:.5 F4:2 |"
              "A4:1 A4:1 A4:1 C5:1 | C5:1.5 Bb4:.5 A4:2 | A4:1 G4:1 A4:1 C5:1 | A4:1.5 G4:.5 F4:2 |"
              "C5:1 D5:.5 C5:.5 Bb4:.5 A4:.5 Bb4:.5 C5:.5 | Bb4:.5 A4:.5 G4:.5 A4:.5 Bb4:1 A4:.5 G4:.5 | A4:1 G4:.5 F4:.5 G4:1.5 C4:.5 | C4:2 F4:1 G4:.5 A4:.5 |"
              "Bb4:1 A4:1 G4:2 | C5:1 D5:.5 C5:.5 Bb4:.5 A4:.5 Bb4:.5 C5:.5 | Bb4:.5 A4:.5 G4:.5 A4:.5 Bb4:1 A4:.5 G4:.5 | A4:1 G4:.5 F4:.5 G4:1.5 C4:.5 | C4:2 F4:1 G4:1 | A4:2 G4:2 | F4:4"),
          "F F C F F F C F F C F C F Bb F C F C F Bb C F".split()[:19]),
}, form=[('A', 'bell', 0, 1), ('A', 'brass', 0, 2), ('A', 'bell', 1, 2)], key=0)

LEADS = {'bell': bell, 'brass': brass, 'square': lead_square, 'celesta': celesta}

def arrange(name, sd, out):
    bpm = sd['bpm']; meter = sd['meter']; beat = 60.0 / bpm; bar = beat * meter
    pickup = sd.get('pickup', 0)
    # 길이 계산
    total_bars = 4
    for sec, lead, tr, en in sd['form']: total_bars += len(sd['sections'][sec][1])
    total_bars += 3
    secs = total_bars * bar + 4
    mx = Mix(secs)
    t0 = 0.0

    def drums(t, bars, energy, fill_last=True):
        for b in range(bars):
            tb = t + b * bar
            for k in range(meter * 4):   # 16분
                ts = tb + k * beat / 4
                if meter == 4:
                    if k in (0, 8) or (energy >= 2 and k == 10): mx.add(kick(1.0 if k == 0 else 0.85), ts, 0, 0.9, 0.05)
                    if k in (4, 12): mx.add(snare(0.95), ts, 0.05, 0.75, 0.25); 
                    if energy >= 2 and k in (4, 12): mx.add(clap(0.9), ts, -0.1, 0.6, 0.3)
                else:
                    if k == 0: mx.add(kick(1.0), ts, 0, 0.9, 0.05)
                    if k in (4, 8): mx.add(snare(0.6 if k == 4 else 0.75), ts, 0.05, 0.6, 0.25)
                if k % 2 == 0: mx.add(hat(0.7 if k % 4 == 0 else 1.0, open_=(energy >= 2 and k % 8 == 6)), ts, 0.35, 0.8, 0.1)
                mx.add(sleigh(0.55 + 0.45 * (k % 2 == 0)), ts + rng.uniform(0, 0.006), -0.45, 0.9 if energy >= 1 else 0.7, 0.2)
            if fill_last and b == bars - 1 and energy >= 1:
                for k in range(8):
                    ts = tb + bar - 2 * beat + k * beat / 4
                    mx.add(snare(0.35 + k * 0.08), ts, (k % 2) * 0.2 - 0.1, 0.6, 0.2)

    def comp(t, chords, tr, energy):
        for b, ch in enumerate(chords):
            tb = t + b * bar
            root, iv = chord(ch, tr)
            # 베이스: 근음-옥타브 통통 (4박) / 근음-5도 (3박)
            if meter == 4:
                pat = [(0, 0), (0.5, 12), (1, 7), (1.5, 12), (2, 0), (2.5, 12), (3, 7), (3.5, 10 if '7' in ch else 12)]
            else:
                pat = [(0, 0), (1, 7), (2, 12)]
            for off, iv2 in pat:
                mx.add(bass(hz(root - 12 + iv2), beat * (0.45 if meter == 4 else 0.9), 0.95 if off == 0 else 0.75), tb + off * beat, 0, 1.0, 0.03)
            # 피아노 엇박 스탭
            notes = [root + 12 + i for i in iv]
            offs = [0.5, 1.5, 2.5, 3.5] if meter == 4 else [1, 2]
            for off in offs:
                for m in notes: mx.add(pluck(hz(m), beat * 0.3, 0.9), tb + off * beat, 0.25, 1.0, 0.3)
            # 패드
            for m in notes: mx.add(pad(hz(m), bar, 1.0), tb, -0.2, 1.0, 0.5)
            # 반짝이는 첼레스타 아르페지오 (신날 때)
            if energy >= 2:
                arp = [root + 24 + i for i in iv] + [root + 36]
                for k in range(meter * 2):
                    mx.add(celesta(hz(arp[k % len(arp)]), beat * 0.4, 0.45), tb + k * beat / 2, 0.55, 0.6, 0.45)

    def melody(t, notes, inst, tr, energy, octave_double):
        tt = t - pickup * beat
        for m, d in notes:
            if m is not None:
                f = hz(m + tr)
                mx.add(LEADS[inst](f, d * beat * 0.92, 1.0), tt, -0.05, 1.0, 0.35)
                if octave_double: mx.add(bell(f * 2, d * beat, 0.5), tt, 0.3, 0.55, 0.45)
            tt += d * beat

    # 인트로: 썰매 방울 + 화음 4마디 + 종 소리 내림
    first = sd['sections'][sd['form'][0][0]][1]
    comp(t0, first[:4], 0, 1)
    drums(t0, 4, 0)
    for k in range(8): mx.add(bell(hz(84 - [0, 3, 5, 7, 9, 12, 15, 17][k]), 0.3, 0.6), t0 + 3 * bar + k * bar / 8, 0.3, 0.7, 0.5)
    t = t0 + 4 * bar
    for i, (sec, lead, tr, en) in enumerate(sd['form']):
        notes, chords = sd['sections'][sec]
        comp(t, chords, tr, en)
        drums(t, len(chords), en)
        melody(t, notes, lead, tr, en, octave_double=(lead == 'brass' and en >= 2))
        t += len(chords) * bar
    # 끝: 으뜸화음 길게 + 종 글리산도 + 심벌
    root, iv = chord(sd['sections'][sd['form'][-1][0]][1][-1], sd['form'][-1][2])
    for m in [root + 12 + i for i in iv] + [root + 24]: mx.add(piano(hz(m), bar * 2, 1.0), t, 0.2, 1.2, 0.4)
    mx.add(bass(hz(root - 12), bar * 1.5, 1.0), t)
    mx.add(kick(1.0), t)
    n = int(2.5 * SR); tt = np.arange(n) / SR
    crash = rng.standard_normal(n); crash = crash - np.concatenate([[0], crash[:-1]]); crash *= np.exp(-tt * 1.8) * 0.12
    mx.add(crash, t, 0.1, 1.0, 0.4)
    for k in range(12): mx.add(bell(hz(root + 24 + [0, 4, 7, 12, 16, 19, 24, 28, 31, 36, 40, 43][k] % 36), 0.3, 0.5), t + k * 0.05, 0.4, 0.6, 0.6)
    length = t + bar * 2.5
    mx.render(length, out=out, fade=0.8)
    print(name, round(length, 1), 's')

if __name__ == '__main__':
    names = sys.argv[1:] or list(SONGS)
    for n in names: arrange(n, SONGS[n], f'out/carol_{n}.wav')
