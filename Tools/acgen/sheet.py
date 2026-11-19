"""카탈로그 미리보기 시트: python3 sheet.py out.png 간격 열수 이름... (y=hang이면 매달린 것)"""
import sys, json, glob
from render import render

def sheet(names, out, gap=1.0, cols=6, y=0.0, rot=20, tints=None, cam_h=0.9, dist=None, target_y=None, scale=1.0, w=1600, h=1000, floor=True, extra=None):
    items = []
    rows = (len(names) + cols - 1) // cols
    for i, n in enumerate(names):
        r, c = divmod(i, cols)
        x = -(c - (cols - 1) / 2) * gap; z = -(r - (rows - 1) / 2) * gap * 1.0
        it = dict(model=n, pos=[x, y, z], rotY=rot, scale=scale, shadow=0.25 * gap if floor else 0)
        if tints and n in tints: it["tints"] = tints[n]
        items.append(it)
    items += extra or []
    span = max(cols, rows * 1.3) * gap
    d = dist or span * 1.05
    ty = target_y if target_y is not None else y
    cam = dict(pos=[0, ty + d * cam_h * 0.75, d * 1.0 + rows * gap * 0.3], target=[0, ty, 0], fov=34)
    render(dict(width=w, height=h, bg=[0.95, 0.96, 1.0], floorA=[0.93, 0.9, 0.84], floorB=[0.9, 0.86, 0.8], floorSize=60, camera=cam, items=items), out)
