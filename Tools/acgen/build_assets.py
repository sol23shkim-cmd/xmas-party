"""python3 build_assets.py [모듈...] [-k 이름필터]  → models/*.acm + models/catalog_<모듈>.json"""
import sys, json, time, importlib, os
from multiprocessing import Pool
mods = [a for a in sys.argv[1:] if not a.startswith("-")]
flt = sys.argv[sys.argv.index("-k") + 1] if "-k" in sys.argv else ""
mods = [m for m in mods if m != flt]

def job(args):
    mod, name = args
    importlib.import_module(mod)
    from assets import make
    from acio import save_acm, stats
    t = time.time()
    try:
        m, info = make(name); save_acm(m, f"models/{name}.acm")
        return name, info, f"{stats(m)} tris {time.time()-t:.1f}s"
    except Exception as e:
        import traceback; return name, None, "FAIL " + repr(e) + traceback.format_exc()[-400:]

if __name__ == "__main__":
    for mod in mods:
        importlib.import_module(mod)
        from assets import REG
        names = [n for n in REG if flt in n]
        cat_path = f"models/catalog_{mod}.json"
        cat = json.load(open(cat_path)) if os.path.exists(cat_path) else {}
        with Pool(min(8, os.cpu_count())) as pool:
            for name, info, msg in pool.imap_unordered(job, [(mod, n) for n in names]):
                print(name, msg, flush=True)
                if info: cat[name] = info
        json.dump(cat, open(cat_path, "w"), ensure_ascii=False, indent=1)
        REG.clear()
