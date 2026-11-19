# 🎄 xmas-plaza

사내 크리스마스 단체 게임 "크리스마스 광장"이에요. Unity 6에 URP를 쓰고, 2.5D 쿼터뷰 방식으로 만들어요.

- 참가자는 캐릭터를 꾸민 뒤 문으로 입장하고, 광장에서 서로의 움직임을 실시간으로 봐요.
- 광장 곳곳의 구역에서 미니게임(틀린 그림 찾기, 지뢰찾기, 선물 깨기)과 소원 트리, 포토존을 즐길 수 있어요.
- 회사가 폐쇄망이라 **네트워크 공유 폴더**로 동기화해요. 참가자마다 `playerNN.json` 슬롯 파일을 하나씩 갖고, 각자 자기 파일만 쓰고 남의 파일은 읽기만 해요.

## 처음 세팅 (집 데스크탑)

1. Unity Hub에서 **Unity 6 (LTS)**를 설치해요.
2. Unity Hub에서 새 프로젝트를 만들어요. 템플릿은 **Universal 3D**를 골라요.
3. 만든 프로젝트 폴더에서 아래 명령을 실행해 이 저장소와 연결해요.
   ```
   git init
   git remote add origin https://github.com/sol23shkim-cmd/xmas-plaza.git
   git fetch origin
   git checkout -b main --track origin/main
   git add .
   git commit -m "Unity 프로젝트 기본 파일"
   git push
   ```
4. Unity에서 Play를 누르면 `Assets/Scripts/XmasSyncTest.cs`가 테스트 광장을 자동으로 만들어요.

## 회사 반입

1. File → Build Profiles → Windows → Build로 빌드해요.
2. 빌드된 폴더를 통째로 zip으로 압축해서 반입해요.
3. 회사 PC의 로컬 폴더에 풀고 exe를 실행해요.
4. 입장 화면에 공유 폴더 경로를 넣어요. 예: `\\서버\공유\xmas` 또는 `Z:\xmas`

## 테스트에서 볼 것

- **FPS:** 왼쪽 위에 표시돼요.
- **갱신 간격:** 오른쪽 위에 표시돼요. 150ms 근처면 아주 좋고, 1초가 넘으면 공유 폴더 캐시 문제예요.
