from render import render
def it(model, x, rot=0, tints=None, pos_y=0):
    return dict(pos=[x, pos_y, 0], rotY=rot, model=model, tints=tints or {}, shadow=0.3)
items = []
for i, (hat, sw, sc, rot) in enumerate([("santa", (0.86,0.24,0.24), (0.98,0.96,0.92), 0), ("beanie", (0.3,0.55,0.95), (0.9,0.3,0.3), 35), (None, (0.3,0.66,0.42), (0.95,0.75,0.3), -35)]):
    x = (i - 1) * 0.9
    items.append(it("char_cookie", x, rot, {"sweater": sw}))
    items.append(it("scarf", x, rot, {"scarf": sc}))
    if hat: items.append(it("hat_" + hat, x, rot))
render(dict(width=1200, height=700, bg=[0.93,0.96,1.0], floorA=[0.86,0.92,0.8], floorB=[0.82,0.9,0.76], floorSize=30,
            camera=dict(pos=[0, 1.0, 3.0], target=[0, 0.55, 0], fov=34), items=items), "out/cookie.png")
