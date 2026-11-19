"""에셋 등록부. @asset(이름, 분류, ...)으로 만든 함수가 Model을 돌려준다.
catalog.json: 이름 → {cat, size, attach, tint 기본색, desc}
"""
import numpy as np
from sdf import *

REG = {}
WHITE = (1, 1, 1)
GOLD = (0.98, 0.78, 0.32)
SILVER = (0.86, 0.88, 0.92)


def asset(name, cat, lo, hi, step, desc="", ao=True, **meta):
    def deco(fn):
        REG[name] = dict(fn=fn, cat=cat, lo=lo, hi=hi, step=step, desc=desc, ao=ao, meta=meta)
        return fn
    return deco


def make(name):
    a = REG[name]
    m = Model(name)
    extra = a["fn"](m) or {}
    import os; m.build(a["lo"], a["hi"], a["step"] * float(os.environ.get("ACG_STEP_MUL", "1")), ao_scene=a["ao"])
    info = dict(cat=a["cat"], desc=a["desc"], lo=list(a["lo"]), hi=list(a["hi"])); info.update(a["meta"]); info.update(extra)
    info["groups"] = sorted({mm["group"] for mm in m.meshes if mm["tint"] is not None})
    return m, info
