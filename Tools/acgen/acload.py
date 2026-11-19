import struct, numpy as np
def load_acm(path):
    b = open(path, "rb").read(); o = 4
    n, = struct.unpack_from("<i", b, o); o += 4; parts = []
    def s():
        nonlocal o
        l, = struct.unpack_from("<i", b, o); o += 4; v = b[o:o+l].decode(); o += l; return v
    for _ in range(n):
        name, group, parent = s(), s(), s()
        ht, tr, tg, tb, gl, px, py, pz = struct.unpack_from("<B3ff3f", b, o); o += struct.calcsize("<B3ff3f")
        nv, ni = struct.unpack_from("<ii", b, o); o += 8
        v = np.frombuffer(b, "<f4", nv*3, o).reshape(-1, 3); o += nv*12
        nn = np.frombuffer(b, "<f4", nv*3, o).reshape(-1, 3); o += nv*12
        o += nv*4 + nv; o += ni*4
        parts.append(dict(name=name, v=v, n=nn))
    return parts
