"""마젠타(#FF00FF) 배경 그림을 오려 내서 투명 PNG로 저장한다.

사용: python3 cutout.py 입력.png 출력.png [최대변길이]
- 마젠타와 가까운 픽셀을 투명하게 (부드러운 경계)
- 경계에 남은 분홍 번짐 제거 (despill)
- 가장 큰 덩어리만 남기고 잡티 제거, 여백 잘라 내기
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage


def cutout(src, dst, max_side=1400):
    im = Image.open(src).convert("RGB")
    a = np.asarray(im).astype(np.float32) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]

    # 마젠타 정도: 빨강·파랑은 높고 초록은 낮을수록 큼
    mag = np.minimum(r, b) - g
    # 0.25 이하 = 물체, 0.55 이상 = 배경, 그 사이 부드럽게
    alpha = 1.0 - np.clip((mag - 0.25) / (0.55 - 0.25), 0, 1)
    # Gemini 표시(오른쪽 아래 반짝이) 지우기
    H, W = alpha.shape
    alpha[int(H * 0.9):, int(W * 0.9):] = 0

    # 큰 덩어리만 남기기
    solid = alpha > 0.5
    lab, n = ndimage.label(solid)
    if n > 1:
        sizes = ndimage.sum(solid, lab, range(1, n + 1))
        keep = np.zeros(n + 1, bool)
        biggest = sizes.max()
        for i, s in enumerate(sizes, start=1):
            if s > biggest * 0.05:      # 아주 작은 조각(잡티)만 버림
                keep[i] = True
        mask = keep[lab]
        mask = ndimage.binary_dilation(mask, iterations=3)
        alpha = alpha * mask

    # despill: 경계 픽셀의 빨강·파랑이 초록보다 과하게 높으면 줄이기
    edge = (alpha > 0.02) & (alpha < 0.98)
    over = np.minimum(r, b) - g
    spill = np.clip(over, 0, None) * np.where(edge, 1.0, 0.35)
    r2 = np.clip(r - spill * 0.8, 0, 1)
    b2 = np.clip(b - spill * 0.8, 0, 1)
    out = np.dstack([r2, g, b2, alpha])

    # 여백 자르기
    ys, xs = np.where(alpha > 0.05)
    y0, y1 = max(ys.min() - 4, 0), min(ys.max() + 5, alpha.shape[0])
    x0, x1 = max(xs.min() - 4, 0), min(xs.max() + 5, alpha.shape[1])
    out = out[y0:y1, x0:x1]

    img = Image.fromarray((out * 255).astype(np.uint8), "RGBA")
    s = max(img.size)
    if s > max_side:
        k = max_side / s
        img = img.resize((round(img.size[0] * k), round(img.size[1] * k)), Image.LANCZOS)
    img.save(dst, optimize=True)
    print(dst, img.size)


if __name__ == "__main__":
    cutout(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 1400)
