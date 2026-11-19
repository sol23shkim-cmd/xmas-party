"""모델 저장 (Unity 런타임 로더용 .acm 바이너리) + 미리보기용 데이터.

.acm 형식 (리틀 엔디언)
  "ACM2", int32 조각 수
  조각마다:
    문자열 이름, 문자열 색묶음(group), 문자열 부모 조각 이름   (문자열 = int32 길이 + UTF-8)
    uint8 색바꿈 여부, float32*3 기본 색바꿈 색, float32 광택, float32*3 회전 중심(pivot)
    int32 정점 수, int32 인덱스 수
    float32*3*정점  위치
    float32*3*정점  법선
    uint8*4*정점    색 RGB + 색바꿈 마스크(A)
    uint8*정점      AO
    uint32*인덱스   삼각형 (Unity 감는 방향)
"""
import struct
import numpy as np


def save_acm(model, path):
    with open(path, "wb") as f:
        f.write(b"ACM2")
        f.write(struct.pack("<i", len(model.meshes)))
        for m in model.meshes:
            for sname in (m["name"], m.get("group", ""), m.get("parent", "")):
                b = sname.encode("utf-8")
                f.write(struct.pack("<i", len(b))); f.write(b)
            tint = m["tint"]
            f.write(struct.pack("<B3ff3f", 1 if tint is not None else 0, *(tint if tint is not None else (1, 1, 1)), m["gloss"],
                                *[float(x) for x in m.get("pivot", (0, 0, 0))]))
            v, n, fc = m["v"], m["n"], m["f"].ravel()
            f.write(struct.pack("<ii", len(v), len(fc)))
            f.write(v.astype("<f4").tobytes())
            f.write(n.astype("<f4").tobytes())
            rgba = np.concatenate([np.clip(m["col"] * 255, 0, 255), np.clip(m["mask"][:, None] * 255, 0, 255)], axis=1)
            f.write(rgba.astype(np.uint8).tobytes())
            f.write(np.clip(m["ao"] * 255, 0, 255).astype(np.uint8).tobytes())
            f.write(fc.astype("<u4").tobytes())


def stats(model):
    return sum(len(m["f"]) for m in model.meshes)
