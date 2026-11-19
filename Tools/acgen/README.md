# acgen — 동물의 숲풍 3D 모델 생성기

SDF(부드러운 도형 합치기) → 마칭 큐브 → QEM 단순화(native/simplify) → `.acm` 파일 → `models.pack`.

## 다시 만들기 (Linux/WSL, Python 3 + numpy + scikit-image, g++)
```
g++ -O2 -o native/simplify native/simplify.cpp
python3 build_chars.py                 # 캐릭터 9종
python3 build_acc.py                   # 얼굴 5, 머리 7, 모자 6, 목도리
python3 build_assets.py xmas_tree furniture xmas_props nature room   # 소품 145종
python3 make_catalog.py && python3 make_pack.py
cp models/models.pack models/catalog.json ../../Assets/StreamingAssets/models/
```
- 하나만: `python3 build_assets.py furniture -k sofa`
- 미리보기: `sheet.py`, `sheet_chars.py`, `scene_tree.py` (headless Chrome 필요) → `previews/`
- 단순화 비율: 환경변수 `ACG_REDUCE` (기본 0.22)

## 모듈
| 파일 | 내용 |
|---|---|
| characters.py | 곰·고양이·토끼·강아지·여우·판다·사자·돼지·사람, 사람 얼굴/머리, 모자, 목도리 |
| xmas_tree.py | 거대 트리, 별, 받침천, 오너먼트 23종 |
| furniture.py | 거실·부엌·침실 가구 28종 |
| xmas_props.py | 진저브레드, 눈사람, 선물, 리스, 가랜드, 양말 등 29종 |
| nature.py | 나무 8종, 덤불, 바위, 눈더미, 울타리, 가로등 등 |
| room.py | 바닥·벽·문·창·계단, 바깥 땅, 오두막 |

Unity 쪽: `Assets/Scripts/AC/` (AcmLoader, AcCatalog, AcCharacter, AcRoom), `Assets/Resources/ACToon.shader`.
