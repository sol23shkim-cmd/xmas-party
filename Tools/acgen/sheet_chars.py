import json
from render import render
info = json.load(open("models/char_info_bear_cat_rabbit_dog_fox_panda_lion_pig_human.json"))
SW = [(0.86,0.24,0.24),(0.3,0.55,0.4),(0.55,0.7,0.92),(0.98,0.8,0.35),(0.7,0.5,0.85),(0.95,0.55,0.65),(0.4,0.65,0.8),(0.9,0.45,0.3),(0.85,0.85,0.82),(0.45,0.6,0.35)]
SC = [(0.98,0.96,0.92),(0.9,0.3,0.3),(0.3,0.5,0.85),(0.95,0.75,0.3),(0.9,0.6,0.7)]
HATS = ["santa","beanie","earmuffs","antlers","tophat","elf"]
def char(sp, x, z, i, hat=None, hair=None, face=None, hairtint=(0.45,0.28,0.18), rot=0):
    top = info[sp]["top"]
    base = dict(pos=[x,0,z], rotY=rot)
    items = [dict(base, model="char_"+sp, tints={"sweater":SW[i%len(SW)]}, shadow=0.32)]
    items.append(dict(base, model="scarf", tints={"scarf":SC[i%len(SC)]}, shadow=0))
    if face: items.append(dict(base, model="face_"+face, shadow=0))
    if hair: items.append(dict(base, model="hair_"+hair, tints={"hair":hairtint}, shadow=0))
    if hat:
        if sp == "human":   # 사람: 머리카락 위로 살짝 크게, 깊게 눌러 씌움
            sc, py, pz = (1.12, 0.80, -0.03) if hair else (1.0, 0, 0)   # 머리카락 덩어리 중심 기준으로 키움
            items.append(dict(base, pos=[x, py * (1 - sc) - (0 if hat == "earmuffs" else 0.075), z + pz * (1 - sc) - 0.01], scale=sc, model="hat_"+hat, shadow=0))
        else:
            items.append(dict(base, pos=[x, top - 1.04, z], model="hat_"+hat, shadow=0))
    return items
def scene(items, cam, w=1600, h=900):
    return dict(width=w, height=h, bg=[0.93,0.96,1.0], floorA=[0.86,0.92,0.8], floorB=[0.82,0.9,0.76], floorSize=30,
                camera=cam, items=items)
if __name__ == "__main__":
    sp = ["bear","cat","rabbit","dog","fox","panda","lion","pig"]
    items=[]
    for i,s in enumerate(sp):
        x = (i%4-1.5)*1.0; z = -(i//4)*1.1
        items += char(s, x, z, i, hat=HATS[i%6] if i%3!=2 else None)
    render(scene(items, dict(pos=[0,2.0,3.6], target=[0,0.55,-0.5], fov=34)), "out/sheet_animals.png")
    # 사람: 여자 5헤어 x 5얼굴 대표, 남자 2
    hairs = ["long","bob","twin","pony","bun"]; HT=[(0.45,0.28,0.18),(0.25,0.18,0.15),(0.95,0.75,0.4),(0.75,0.35,0.25),(0.35,0.3,0.45)]
    items=[]
    for i,h in enumerate(hairs):
        items += char("human", (i-2)*0.95, 0, i+2, hair=h, face=f"f{i+1}", hairtint=HT[i], hat=None)
    for j,h in enumerate(["short","spiky"]):
        items += char("human", (j-0.5)*1.0, -1.1, j+5, hair=h, face=["f1","f2"][j], hairtint=HT[j], hat=["beanie",None][j])
    render(scene(items, dict(pos=[0,1.9,3.6], target=[0,0.6,-0.4], fov=36)), "out/sheet_humans.png")
