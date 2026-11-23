import time
from characters import *
import sys
from acio import save_acm, stats
jobs = [(f"face_{k}", lambda k=k: build_face(k)) for k in FACES]
jobs += [(f"hair_{s}", lambda s=s: build_hair(s)) for s in HAIRS]
jobs += [(f"hat_{k}", lambda k=k: build_hat(k)) for k in HAT_TINTS]
jobs += [("scarf", build_scarf)]
for n, fn in jobs:
    t=time.time()
    try: m=fn(); save_acm(m, f"models/{n}.acm"); print(n, stats(m), round(time.time()-t,1), flush=True)
    except Exception as e: print("FAIL", n, e, flush=True)
