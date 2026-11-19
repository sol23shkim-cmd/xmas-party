"""models/*.acm (ACM2) → 작은 ACM3 묶음 (위치 16비트, 법선 2바이트, 인덱스 16비트) — 휴대폰/웹용
python3 make_pack3.py [출력경로]"""
import glob, os, struct, sys
import numpy as np

def read_acm2(b):
    o = 4; n, = struct.unpack_from("<i", b, o); o += 4; parts = []
    def s():
        nonlocal o
        l, = struct.unpack_from("<i", b, o); o += 4; v = b[o:o + l]; o += l; return v
    for _ in range(n):
        name, group, parent = s(), s(), s()
        hdr = b[o:o + struct.calcsize("<B3ff3f")]; o += len(hdr)
        nv, ni = struct.unpack_from("<ii", b, o); o += 8
        v = np.frombuffer(b, "<f4", nv * 3, o).reshape(-1, 3); o += nv * 12
        nn = np.frombuffer(b, "<f4", nv * 3, o).reshape(-1, 3); o += nv * 12
        col = np.frombuffer(b, np.uint8, nv * 4, o); o += nv * 4
        ao = np.frombuffer(b, np.uint8, nv, o); o += nv
        idx = np.frombuffer(b, "<u4", ni, o); o += ni * 4
        parts.append((name, group, parent, hdr, v, nn, col, ao, idx))
    return parts

def oct_enc(n):
    n = n / np.maximum(np.abs(n).sum(1, keepdims=True), 1e-9)
    x, y, z = n[:, 0].copy(), n[:, 1].copy(), n[:, 2]
    neg = z < 0
    ox = np.where(neg, (1 - np.abs(y)) * np.sign(x + 1e-12), x)
    oy = np.where(neg, (1 - np.abs(x)) * np.sign(y + 1e-12), y)
    return np.clip(np.round(np.stack([ox, oy], 1) * 127), -127, 127).astype(np.int8)

def write_acm3(parts):
    out = bytearray(b"ACM3"); out += struct.pack("<i", len(parts))
    for name, group, parent, hdr, v, nn, col, ao, idx in parts:
        for sname in (name, group, parent): out += struct.pack("<i", len(sname)) + sname
        out += hdr
        nv, ni = len(v), len(idx)
        out += struct.pack("<ii", nv, ni)
        lo = v.min(0) if nv else np.zeros(3); size = np.maximum(v.max(0) - lo, 1e-6) if nv else np.ones(3)
        out += struct.pack("<3f3f", *lo, *size)
        q = np.clip(np.round((v - lo) / size * 65535), 0, 65535).astype("<u2")
        out += q.tobytes(); out += oct_enc(nn).tobytes(); out += col.tobytes(); out += ao.tobytes()
        small = nv <= 65535
        out += struct.pack("<B", 1 if small else 0)
        out += idx.astype("<u2" if small else "<u4").tobytes()
    return bytes(out)

if __name__ == "__main__":
    dst = sys.argv[1] if len(sys.argv) > 1 else "models/models3.pack"
    fs = sorted(glob.glob("models/*.acm"))
    with open(dst, "wb") as f:
        f.write(b"ACPK"); f.write(struct.pack("<i", len(fs)))
        for p in fs:
            data = write_acm3(read_acm2(open(p, "rb").read()))
            n = os.path.basename(p)[:-4].encode()
            f.write(struct.pack("<i", len(n))); f.write(n); f.write(struct.pack("<i", len(data))); f.write(data)
    print(len(fs), os.path.getsize(dst) / 1e6, "MB")
