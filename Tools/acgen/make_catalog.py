"""models/catalog_*.json + char_info → models/catalog.json (Unity JsonUtility용 배열 형식)"""
import json, glob, os
items = []; seen = set()
for f in sorted(glob.glob("models/catalog_*.json")):
    for name, info in json.load(open(f)).items():
        if not os.path.exists(f"models/{name}.acm") or name in seen: continue
        seen.add(name)
        it = dict(name=name, cat=info.get("cat", ""), desc=info.get("desc", ""), groups=info.get("groups", []),
                  size=[round(info["hi"][i] - info["lo"][i], 3) for i in range(3)],
                  footprint=info.get("footprint", []), hang=info.get("hang", []), light=info.get("light", []),
                  wall=bool(info.get("wall", False)), tile=info.get("tile", 0.0),
                  emissiveParts=info.get("emissiveParts", []), transparentParts=info.get("transparentParts", []))
        items.append(it)
ci = json.load(open(sorted(glob.glob("models/char_info_*human*.json"))[-1]))
species = [dict(name=k, top=v["top"], fur=v["fur"]) for k, v in ci.items()]
from characters import FACES, HAT_TINTS
out = dict(items=items, species=species,
           faces=sorted(FACES), hairFemale=["long", "bob", "twin", "pony", "bun"], hairMale=["short", "spiky"],
           hats=sorted(HAT_TINTS), hatTop=1.04,
           palette=[[0.9,0.2,0.22],[0.98,0.78,0.32],[0.25,0.5,0.85],[0.6,0.35,0.8],[0.3,0.66,0.42],[0.95,0.5,0.65],[0.86,0.88,0.92],
                    [0.98,0.6,0.25],[0.4,0.8,0.85],[0.98,0.97,0.94],[0.2,0.2,0.24],[0.55,0.36,0.24],[0.98,0.9,0.4],[0.7,0.85,0.4],[0.45,0.3,0.6],[0.9,0.4,0.4]])
json.dump(out, open("models/catalog.json", "w"), ensure_ascii=False, indent=1)
print(len(items), "items", len(species), "species")
