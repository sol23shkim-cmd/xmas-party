"""models/*.acm → models.pack (Unity가 파일 하나로 읽음). 형식: "ACPK", int32 개수, [int32 이름길이, 이름, int32 크기, 바이트]..."""
import glob, os, struct, sys
out = sys.argv[1] if len(sys.argv) > 1 else "models/models.pack"
fs = sorted(glob.glob("models/*.acm"))
with open(out, "wb") as f:
    f.write(b"ACPK"); f.write(struct.pack("<i", len(fs)))
    for p in fs:
        n = os.path.basename(p)[:-4].encode(); b = open(p, "rb").read()
        f.write(struct.pack("<i", len(n))); f.write(n); f.write(struct.pack("<i", len(b))); f.write(b)
print(len(fs), os.path.getsize(out) / 1e6, "MB")
