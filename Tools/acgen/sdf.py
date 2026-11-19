"""동물의 숲풍 모델을 만드는 작은 SDF 모델러.

- 둥근 기본 도형(구, 타원체, 캡슐, 둥근 상자, 원뿔, 토러스...)을 부드럽게 합쳐 모양을 만든다.
- 마칭 큐브로 메시를 뽑고, 정점마다 색·AO(구석 어둡게)·색바꿈 마스크를 계산한다.
- 좌표계는 Unity와 같다: x 오른쪽, y 위, z 앞(캐릭터 얼굴 방향).
"""
import numpy as np
from skimage.measure import marching_cubes


# ---------------------------------------------------------------- 회전
def rot(x=0.0, y=0.0, z=0.0):
    """도(degree) 단위 오일러 회전 행렬 (Unity 순서: Z, X, Y)"""
    x, y, z = np.radians([x, y, z])
    cx, sx, cy, sy, cz, sz = np.cos(x), np.sin(x), np.cos(y), np.sin(y), np.cos(z), np.sin(z)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Ry @ Rx @ Rz


def local(p, c, R):
    """월드 점 p를 (중심 c, 회전 R) 좌표계로"""
    q = p - np.asarray(c, float)
    return q @ R if R is not None else q


# ---------------------------------------------------------------- 기본 도형 (p: (N,3))
def sd_sphere(p, c, r):
    return np.linalg.norm(p - np.asarray(c, float), axis=1) - r


def sd_ellipsoid(p, c, radii, R=None):
    q = local(p, c, R)
    r = np.asarray(radii, float)
    k0 = np.linalg.norm(q / r, axis=1)
    k1 = np.linalg.norm(q / (r * r), axis=1)
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)


def sd_capsule(p, a, b, r):
    a = np.asarray(a, float); b = np.asarray(b, float)
    pa = p - a; ba = b - a
    h = np.clip((pa @ ba) / (ba @ ba), 0, 1)
    return np.linalg.norm(pa - h[:, None] * ba, axis=1) - r


def sd_roundcone(p, a, b, r1, r2):
    """a(반지름 r1)에서 b(반지름 r2)로 가는 둥근 원뿔"""
    a = np.asarray(a, float); b = np.asarray(b, float)
    ba = b - a; l2 = ba @ ba; rr = r1 - r2; a2 = l2 - rr * rr; il2 = 1.0 / l2
    pa = p - a
    y = pa @ ba; z = y - l2
    xv = pa * l2 - y[:, None] * ba
    x2 = np.sum(xv * xv, axis=1)
    y2 = y * y * l2; z2 = z * z * l2
    k = np.sign(rr) * rr * rr * x2
    res = np.where(np.sign(z) * a2 * z2 > k, np.sqrt(x2 + z2) * il2 - r2,
          np.where(np.sign(y) * a2 * y2 < k, np.sqrt(x2 + y2) * il2 - r1,
                   (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - r1))
    return res


def sd_roundbox(p, c, half, r, R=None):
    q = np.abs(local(p, c, R)) - (np.asarray(half, float) - r)
    return np.linalg.norm(np.maximum(q, 0), axis=1) + np.minimum(np.max(q, axis=1), 0) - r


def sd_torus(p, c, R_big, r, R=None):
    q = local(p, c, R)   # 고리는 xz 평면
    qx = np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) - R_big
    return np.sqrt(qx * qx + q[:, 1] ** 2) - r


def sd_cylinder(p, c, h, r, round_=0.0, R=None):
    """세로(y) 원기둥, h = 반높이"""
    q = local(p, c, R)
    d = np.stack([np.sqrt(q[:, 0] ** 2 + q[:, 2] ** 2) - r + round_, np.abs(q[:, 1]) - h + round_], axis=1)
    return np.minimum(np.maximum(d[:, 0], d[:, 1]), 0) + np.linalg.norm(np.maximum(d, 0), axis=1) - round_


def sd_cone(p, c, h, r_base, r_top=0.0, round_=0.0, R=None):
    """c가 바닥 중심인 세로 원뿔대 (높이 h)"""
    q = local(p, c, R)
    a = np.array([0.0, 0.0, 0.0]); b = np.array([0.0, h, 0.0])
    return sd_roundcone(q, a, b, r_base, max(r_top, 1e-3)) - round_


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


# ---------------------------------------------------------------- 모델 조립
class Part:
    """한 메시 조각 (색바꿈 단위). 고체(solid) 여러 개를 부드럽게 합치고, 페인트로 무늬를 칠한다."""

    def __init__(self, name, tint=None, gloss=0.0, k=0.03, group=None, pivot=(0, 0, 0), parent="", step=None, box=None, region=None):
        self.name = name
        self.group = group or name
        self.pivot = tuple(float(x) for x in pivot)
        self.parent = parent
        self.region = box or region      # (lo, hi) 이 조각만 따로 격자를 쓸 때
        self.step = step
        self.tint = tint          # 기본 색바꿈 색 (None이면 색바꿈 없음)
        self.gloss = gloss
        self.k = k                # 부드럽게 합치는 정도
        self.solids = []          # (fn, color, mask, k)
        self.cuts = []            # (fn, k)
        self.paints = []          # (fn, color, mask, soft)

    def add(self, fn, color, mask=1.0, k=None):
        self.solids.append((fn, np.asarray(color, float), float(mask), self.k if k is None else k))
        return self

    def cut(self, fn, k=0.0):
        self.cuts.append((fn, k))
        return self

    def paint(self, fn, color, mask=0.0, soft=0.004):
        self.paints.append((fn, np.asarray(color, float), float(mask), soft))
        return self

    # 전체 SDF
    def sdf(self, p):
        d = None
        for fn, _, _, k in self.solids:
            di = fn(p)
            d = di if d is None else smin(d, di, k)
        for fn, k in self.cuts:
            dc = fn(p)
            d = -smin(-d, dc, k) if k > 0 else np.maximum(d, -dc)
        return d

    def colors(self, p):
        # 고체별 거리로 부드럽게 색 섞기
        ds = np.stack([fn(p) for fn, _, _, _ in self.solids], axis=1)
        w = np.exp(-(ds - ds.min(axis=1, keepdims=True)) / 0.006)
        w /= w.sum(axis=1, keepdims=True)
        cols = np.stack([c for _, c, _, _ in self.solids])
        masks = np.array([m for _, _, m, _ in self.solids])
        col = w @ cols
        mask = w @ masks
        for fn, c, m, soft in self.paints:
            t = np.clip(0.5 - fn(p) / soft, 0, 1)[:, None]
            col = col * (1 - t) + c * t
            mask = mask * (1 - t[:, 0]) + m * t[:, 0]
        return col, mask

    def bounds(self, pad=0.05):
        return self._bounds

    def mesh(self, lo, hi, step):
        lo = np.asarray(lo, float); hi = np.asarray(hi, float)
        n = np.ceil((hi - lo) / step).astype(int) + 1
        xs = [lo[i] + np.arange(n[i]) * step for i in range(3)]
        vol = np.empty(n, np.float32)
        # x 방향으로 잘라서 평가 (메모리 절약)
        Y, Z = np.meshgrid(xs[1], xs[2], indexing="ij")
        yz = np.stack([Y.ravel(), Z.ravel()], axis=1)
        for i, x in enumerate(xs[0]):
            pts = np.column_stack([np.full(len(yz), x), yz])
            vol[i] = self.sdf(pts).reshape(n[1], n[2])
        if vol.min() > 0 or vol.max() < 0:
            raise ValueError(f"{self.name}: 표면이 격자 안에 없어요")
        verts, faces, _, _ = marching_cubes(vol, 0.0, spacing=(step, step, step))
        verts += lo
        # 법선: SDF 기울기
        e = step * 0.5
        nx = self.sdf(verts + [e, 0, 0]) - self.sdf(verts - [e, 0, 0])
        ny = self.sdf(verts + [0, e, 0]) - self.sdf(verts - [0, e, 0])
        nz = self.sdf(verts + [0, 0, e]) - self.sdf(verts - [0, 0, e])
        normals = np.stack([nx, ny, nz], axis=1)
        normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
        # 감는 방향: Unity는 cross(b-a, c-a)가 바깥쪽
        a, b, c = verts[faces[:, 0]], verts[faces[:, 1]], verts[faces[:, 2]]
        fn_ = np.cross(b - a, c - a)
        if np.sum(np.sum(fn_ * normals[faces[:, 0]], axis=1) > 0) < len(faces) / 2:
            faces = faces[:, [0, 2, 1]]
        col, mask = self.colors(verts)
        return verts.astype(np.float32), normals.astype(np.float32), faces.astype(np.uint32), col, mask


class Model:
    def __init__(self, name):
        self.name = name
        self.parts = []
        self.meshes = []

    def part(self, *a, **kw):
        p = Part(*a, **kw)
        self.parts.append(p)
        return p

    def build(self, lo, hi, step, ao_scene=True):
        """모든 조각 메시를 만들고 AO는 모델 전체 SDF로 계산"""
        out = []
        for p in self.parts:
            if not p.solids:
                continue
            plo, phi = p.region if p.region else (lo, hi)
            out.append((p, p.mesh(plo, phi, p.step or step)))

        def scene(q):
            d = None
            for p in self.parts:
                if p.solids:
                    di = p.sdf(q)
                    d = di if d is None else np.minimum(d, di)
            return d

        self.meshes = []
        for p, (v, n, f, col, mask) in out:
            ao = np.ones(len(v), np.float32)
            if ao_scene:
                occ = np.zeros(len(v))
                for i, h in enumerate([0.012, 0.03, 0.06, 0.1]):
                    occ += (h - scene(v + n * h)) / (2 ** i)
                ao = np.clip(1.0 - 1.6 * occ / 0.06, 0.35, 1.0)
            self.meshes.append(dict(name=p.name, v=v, n=n, f=f, col=col, mask=mask, ao=ao,
                                    tint=p.tint, gloss=p.gloss, group=p.group, pivot=p.pivot, parent=p.parent))
        return self
