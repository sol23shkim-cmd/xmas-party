import json, sys, time
from characters import *
from acio import save_acm, stats
out = "models/"
cat = {}
which = sys.argv[1:] or list(SPECIES)
for sp in which:
    t=time.time(); m, info = build_species(sp); save_acm(m, out+f"char_{sp}.acm")
    cat[sp]=info; print(sp, stats(m), round(time.time()-t,1), flush=True)
json.dump(cat, open(out+"char_info_"+"_".join(which)+".json","w"))
