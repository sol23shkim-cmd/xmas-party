import sys, time
from characters import *
from acio import save_acm, stats
for s in sys.argv[1:]:
    t=time.time(); m=build_hair(s); save_acm(m, f"models/hair_{s}.acm"); print(s, stats(m), round(time.time()-t,1), flush=True)
