"""장면 JSON → PNG 렌더 (헤드리스 Chrome + viewer.html)"""
import json, threading, urllib.parse, functools, os, sys
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from playwright.sync_api import sync_playwright

ROOT = os.path.dirname(os.path.abspath(__file__))
_server = None


def _serve():
    global _server
    if _server:
        return _server.server_address[1]
    h = functools.partial(SimpleHTTPRequestHandler, directory=ROOT)
    h.log_message = lambda *a, **k: None
    _server = ThreadingHTTPServer(("127.0.0.1", 0), h)
    threading.Thread(target=_server.serve_forever, daemon=True).start()
    return _server.server_address[1]


def render_many(jobs):
    """jobs: [(scene_dict, out_png)]"""
    port = _serve()
    with sync_playwright() as p:
        b = p.chromium.launch(executable_path="/usr/bin/google-chrome",
                              args=["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
        for scene, out in jobs:
            w, h = scene.get("width", 1200), scene.get("height", 900)
            pg = b.new_page(viewport={"width": w, "height": h}, device_scale_factor=1)
            url = f"http://127.0.0.1:{port}/viewer.html?scene=" + urllib.parse.quote(json.dumps(scene))
            pg.goto(url)
            pg.wait_for_function("document.title === 'done' || document.title.startsWith('err')", timeout=180000)
            if pg.title() != "done":
                print("RENDER ERROR", out, pg.title())
            pg.locator("canvas").screenshot(path=out)
            pg.close()
            print("rendered", out)
        b.close()


def render(scene, out):
    render_many([(scene, out)])
