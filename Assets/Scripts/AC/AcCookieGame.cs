// AcCookieGame.cs - 쿠키 공방의 '쿠키 달리기' 게임 (쿠키런 비슷한 달리기) + 이름별 최고 점수 베스트 10 (금·은·동)
// 점수: rooms/<방>/scores/<이름> = {name, best, t}  (실제로는 orn/x~score~<이름>)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class AcRoom
{
    enum CgState { Menu, Play, Over }
    CgState cg = CgState.Menu;
    // 무대: 가로 800 x 세로 450 (화면에 맞게 늘림)
    const float StageW = 800, StageH = 450, GroundY = 372;
    float cgY, cgVy, cgSpeed, cgDist, cgHp, cgHurtUntil, cgTime, cgNextSpawn, cgBgX;
    int cgJumps, cgJelly, cgScore, cgLastScore;
    bool cgSlide, cgNewBest;
    class CgObj { public int kind; public float x, y, w, h; public bool done; public Color col; }   // kind: 0 낮은 장애물, 1 큰 장애물, 2 위 장애물, 10 젤리, 11 별, 12 하트
    readonly List<CgObj> cgObjs = new List<CgObj>();
    Texture2D cgSky, cgHill, cgGround;
    System.Random cgRnd = new System.Random();

    void OpenCookieGame()
    {
        playingGame = true; cg = CgState.Menu; walkTarget = null; followId = null;
        me.speed = 0;
    }

    void CloseCookieGame() { playingGame = false; }

    void StartRun()
    {
        cg = CgState.Play; cgObjs.Clear();
        cgY = GroundY; cgVy = 0; cgSpeed = 320; cgDist = 0; cgHp = 100; cgHurtUntil = 0; cgTime = 0; cgNextSpawn = 0.8f;
        cgJumps = 0; cgJelly = 0; cgScore = 0; cgSlide = false; cgNewBest = false;
    }

    bool KeyHeld(string k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; if (kb == null) return false;
        return k == "down" ? kb.downArrowKey.isPressed || kb.sKey.isPressed : false;
#else
        return k == "down" ? Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) : false;
#endif
    }
    bool JumpKey()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; if (kb == null) return false;
        return kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    float CookieH => cgSlide ? 44 : 78;

    void CookieGameUpdate(float dt)
    {
        if (!playingGame) return;
        if (Key("esc")) { if (cg == CgState.Play) cg = CgState.Over; else CloseCookieGame(); return; }
        if (cg != CgState.Play) { if (cg == CgState.Menu && JumpKey()) StartRun(); return; }
        dt = Mathf.Min(dt, 0.05f);
        cgTime += dt;
        cgSpeed = Mathf.Min(320 + cgTime * 9f, 720);
        cgDist += cgSpeed * dt;
        cgBgX += cgSpeed * dt;
        cgHp -= dt * (2.6f + cgTime * 0.02f);
        // 점프 (화면을 누르거나 스페이스) / 슬라이드 (아래 키 또는 슬라이드 버튼)
        var gp = new Vector2(ptrPos.x / UiK, (Screen.height - ptrPos.y) / UiK);
        bool tapJump = ptrDown && !cgButtons.Any(b => b.Contains(gp));
        if ((tapJump || JumpKey()) && cgJumps < 2) { cgVy = cgJumps == 0 ? -640 : -560; cgJumps++; }
        bool grounded = cgY >= GroundY - 0.5f && cgVy >= 0;
        cgSlide = (KeyHeld("down") || cgSlideBtn) && grounded;
        cgVy += 1900 * dt;
        cgY += cgVy * dt;
        if (cgY >= GroundY) { cgY = GroundY; cgVy = 0; cgJumps = 0; }
        // 만들기
        cgNextSpawn -= dt;
        if (cgNextSpawn <= 0) SpawnPattern();
        // 움직이기·부딪히기
        var box = new Rect(150 - 26, cgY - CookieH, 52, CookieH);
        foreach (var o in cgObjs)
        {
            o.x -= cgSpeed * dt;
            if (o.done) continue;
            var or = new Rect(o.x - o.w / 2, o.y - o.h, o.w, o.h);
            var shrink = new Rect(or.x + 6, or.y + 6, or.width - 12, or.height - 12);
            if (!box.Overlaps(o.kind >= 10 ? or : shrink)) continue;
            if (o.kind >= 10)
            {
                o.done = true;
                if (o.kind == 10) cgJelly += 10; else if (o.kind == 11) cgJelly += 50; else cgHp = Mathf.Min(100, cgHp + 25);
            }
            else if (Time.unscaledTime > cgHurtUntil)
            {
                o.done = true; cgHp -= 22; cgHurtUntil = Time.unscaledTime + 1.0f;
            }
        }
        cgObjs.RemoveAll(o => o.x < -80 || (o.done && o.kind >= 10));
        cgScore = cgJelly + (int)(cgDist / 10f);
        if (cgHp <= 0) GameOver();
    }

    void SpawnPattern()
    {
        float x = StageW + 60;
        int pat = cgRnd.Next(cgTime < 15 ? 5 : 8);
        switch (pat)
        {
            case 0: case 1:   // 낮은 장애물 + 위로 젤리 아치
                cgObjs.Add(new CgObj { kind = 0, x = x, y = GroundY, w = 46, h = 56 });
                for (int i = 0; i < 5; i++) cgObjs.Add(new CgObj { kind = 10, x = x - 80 + i * 40, y = GroundY - 90 - Mathf.Sin(i / 4f * Mathf.PI) * 70, w = 26, h = 26, col = JellyCol(i) });
                break;
            case 2:   // 젤리 줄
                for (int i = 0; i < 8; i++) cgObjs.Add(new CgObj { kind = 10, x = x + i * 38, y = GroundY - 20, w = 26, h = 26, col = JellyCol(i) });
                break;
            case 3:   // 위에 매달린 장애물 (슬라이드)
                cgObjs.Add(new CgObj { kind = 2, x = x, y = GroundY - 56, w = 70, h = 120 });
                for (int i = 0; i < 4; i++) cgObjs.Add(new CgObj { kind = 10, x = x - 60 + i * 40, y = GroundY - 12, w = 24, h = 24, col = JellyCol(i) });
                break;
            case 4:   // 별 보너스 (높이)
                cgObjs.Add(new CgObj { kind = 11, x = x, y = GroundY - 210, w = 40, h = 40 });
                for (int i = 0; i < 4; i++) cgObjs.Add(new CgObj { kind = 10, x = x - 120 + i * 30, y = GroundY - 100 - i * 25, w = 24, h = 24, col = JellyCol(i) });
                break;
            case 5:   // 큰 장애물 (2단 점프)
                cgObjs.Add(new CgObj { kind = 1, x = x, y = GroundY, w = 60, h = 130 });
                cgObjs.Add(new CgObj { kind = 11, x = x, y = GroundY - 210, w = 36, h = 36 });
                break;
            case 6:   // 낮은 장애물 두 개
                cgObjs.Add(new CgObj { kind = 0, x = x, y = GroundY, w = 46, h = 56 });
                cgObjs.Add(new CgObj { kind = 0, x = x + 260, y = GroundY, w = 46, h = 56 });
                break;
            default:  // 하트 (체력)
                cgObjs.Add(new CgObj { kind = 12, x = x, y = GroundY - 150, w = 34, h = 34 });
                cgObjs.Add(new CgObj { kind = 2, x = x + 220, y = GroundY - 56, w = 70, h = 120 });
                break;
        }
        cgNextSpawn = Mathf.Lerp(1.6f, 0.95f, Mathf.Clamp01(cgTime / 60f)) + (float)cgRnd.NextDouble() * 0.5f;
    }

    static Color JellyCol(int i) => new[] { new Color(0.95f, 0.35f, 0.5f), new Color(0.98f, 0.78f, 0.25f), new Color(0.35f, 0.75f, 0.95f), new Color(0.45f, 0.82f, 0.45f) }[i % 4];

    void GameOver()
    {
        cg = CgState.Over; cgLastScore = cgScore;
        int best = MyBest();
        if (cgScore > best && net != null)
        {
            cgNewBest = true;
            net.Put("scores/" + myKey, "{\"name\":" + AcJson.Esc(uiName.Trim()) + ",\"best\":" + cgScore + ",\"t\":" + AcSync.UnixMs.ToString("0") + "}");
        }
    }

    int MyBest() { var s = net != null ? net.Get("scores") : null; return s != null && s.TryGetValue(myKey, out var v) && v is Dictionary<string, object> d ? (int)AcJson.D(d, "best") : 0; }

    List<(string name, int best)> TopScores()
    {
        var res = new List<(string, int)>();
        var s = net != null ? net.Get("scores") : null;
        if (s != null) foreach (var kv in s) if (kv.Value is Dictionary<string, object> d) res.Add((AcJson.S(d, "name"), (int)AcJson.D(d, "best")));
        return res.OrderByDescending(x => x.Item2).Take(10).ToList();
    }

    // ===== 그리기 =====
    bool cgSlideBtn;
    readonly List<Rect> cgButtons = new List<Rect>();
    void CookieGameGUI()
    {
        if (!playingGame) return;
        R(new Rect(0, 0, SW, SH));
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0.1f, 0.07f, 0.05f, 0.6f)));
        float k = Mathf.Min((SW - 20) / StageW, (SH - 20) / StageH);
        var st = new Rect(SW / 2 - StageW * k / 2, SH / 2 - StageH * k / 2, StageW * k, StageH * k);
        Rect S(float x, float y, float w, float h) => new Rect(st.x + x * k, st.y + y * k, w * k, h * k);
        GUI.BeginGroup(st); var group = st; st = new Rect(0, 0, st.width, st.height);
        if (Event.current.type == EventType.Repaint) cgButtons.Clear();
        void Btn(Rect lr) { if (Event.current.type == EventType.Repaint) cgButtons.Add(new Rect(lr.x + group.x, lr.y + group.y, lr.width, lr.height)); }
        if (cgSky == null) MakeCgTextures();
        GUI.DrawTexture(S(0, 0, StageW, StageH), cgSky);
        // 언덕 (느리게) · 땅 (빠르게)
        float hx = -(cgBgX * 0.25f % 400);
        for (int i = 0; i < 4; i++) GUI.DrawTexture(S(hx + i * 400, 200, 402, 180), cgHill);
        float gx = -(cgBgX % 100);
        for (int i = 0; i < 10; i++) GUI.DrawTexture(S(gx + i * 100, GroundY, 101, StageH - GroundY), cgGround);
        // 물건
        foreach (var o in cgObjs)
        {
            if (o.done && o.kind < 10) GUI.color = new Color(1, 1, 1, 0.5f);
            var r = S(o.x - o.w / 2, o.y - o.h, o.w, o.h);
            switch (o.kind)
            {
                case 0: GUI.DrawTexture(r, SnowIcon("candy_cane_big", Color.white), ScaleMode.ScaleToFit); break;
                case 1: GUI.DrawTexture(r, SnowIcon("snowman_small", Color.white), ScaleMode.ScaleToFit); break;
                case 2:
                    GUI.DrawTexture(S(o.x - 3, 0, 6, o.y - o.h), AcPalette.Tex(new Color(0.85f, 0.25f, 0.3f)));
                    GUI.DrawTexture(r, SnowIcon("gift_box", new Color(0.3f, 0.6f, 0.9f)), ScaleMode.ScaleToFit); break;
                case 10: GUI.DrawTexture(r, SnowIcon("orn_ball", o.col), ScaleMode.ScaleToFit); break;
                case 11: GUI.DrawTexture(r, SnowIcon("orn_star", new Color(1f, 0.82f, 0.25f)), ScaleMode.ScaleToFit); break;
                case 12: GUI.DrawTexture(r, SnowIcon("orn_heart", new Color(0.95f, 0.25f, 0.35f)), ScaleMode.ScaleToFit); break;
            }
            GUI.color = Color.white;
        }
        // 쿠키 (달릴 때 통통, 점프할 때 빙글, 다치면 깜빡)
        bool hurt = Time.unscaledTime < cgHurtUntil && Mathf.Repeat(Time.unscaledTime * 10, 1) < 0.5f;
        if (!hurt || cg != CgState.Play)
        {
            float bob = cg == CgState.Play && cgJumps == 0 ? Mathf.Abs(Mathf.Sin(cgTime * 16)) * 5 : 0;
            var cr = S(150 - 45, cgY - (cgSlide ? 58 : 100) - bob, 90, cgSlide ? 58 : 100);
            var m = GUI.matrix;
            float ang = cgJumps == 2 ? (cgTime * 720) % 360 : cgSlide ? 0 : Mathf.Sin(cgTime * 16) * 6;
            GUIUtility.RotateAroundPivot(ang, cr.center);
            GUI.DrawTexture(cr, SnowIcon("gingerbread_man", Color.white), cgSlide ? ScaleMode.StretchToFill : ScaleMode.ScaleToFit);
            GUI.matrix = m;
        }
        // 위쪽 정보
        GUI.DrawTexture(S(16, 14, 220, 20), AcPalette.Tex(new Color(0, 0, 0, 0.3f)));
        GUI.DrawTexture(S(18, 16, 216 * Mathf.Clamp01(cgHp / 100f), 16), AcPalette.Tex(cgHp > 30 ? new Color(0.95f, 0.4f, 0.45f) : new Color(1f, 0.6f, 0.2f)));
        GUI.Label(S(20, 13, 200, 22), "<color=#ffffff><b>체력</b></color>", sSmall);
        var big = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(26 * k), alignment = TextAnchor.UpperRight };
        GUI.Label(S(StageW - 320, 8, 300, 40), "<color=#ffffff>" + cgScore.ToString("#,0") + "점</color>", big);
        Btn(S(StageW - 96, 52, 82, 30));
        if (GUI.Button(S(StageW - 96, 52, 82, 30), cg == CgState.Play ? "그만하기" : "나가기", sBtn))
        { if (cg == CgState.Play) GameOver(); else { GUI.EndGroup(); CloseCookieGame(); return; } }
        // 슬라이드 버튼 (누르고 있는 동안)
        cgSlideBtn = false;
        if (cg == CgState.Play)
        {
            var sb = S(14, StageH - 74, 120, 60); Btn(sb);
            GUI.Box(sb, "", sBox);
            GUI.Label(sb, "슬라이드", new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter });
            var abs = new Rect(sb.x + group.x, sb.y + group.y, sb.width, sb.height);
            R(abs);
            if (ptrPressed && abs.Contains(new Vector2(ptrPos.x / UiK, (Screen.height - ptrPos.y) / UiK))) cgSlideBtn = true;
            GUI.Label(S(150, StageH - 40, 500, 30), "<color=#ffffff><size=13>화면을 누르면 점프 (두 번 누르면 2단 점프) / 스페이스·위 키</size></color>", sSmall);
        }
        else CookieBoard(S(110, 60, StageW - 220, StageH - 90), k);
        GUI.EndGroup();
        // 점프 판정에서 버튼 영역 빼기: 전체를 UI로 막았으므로, 무대 위 누름은 직접 처리
    }

    void CookieBoard(Rect r, float k)
    {
        GUI.DrawTexture(r, AcPalette.Tex(new Color(1f, 0.97f, 0.9f, 0.96f)));
        var title = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(24 * k), alignment = TextAnchor.UpperCenter };
        if (cg == CgState.Menu) GUI.Label(new Rect(r.x, r.y + 10, r.width, 36), "쿠키 달리기", title);
        else GUI.Label(new Rect(r.x, r.y + 10, r.width, 36), cgNewBest ? $"새 최고 기록! {cgLastScore:#,0}점" : $"{cgLastScore:#,0}점", title);
        GUI.Label(new Rect(r.x, r.y + 44, r.width, 22), $"<size=13>{uiName.Trim()} 님의 최고 점수: {Mathf.Max(MyBest(), cgNewBest ? cgLastScore : 0):#,0}점</size>", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter });
        // 베스트 10
        var top = TopScores();
        float y = r.y + 74, rowH = Mathf.Min(26, (r.height - 130) / 10f);
        GUI.Label(new Rect(r.x + 20, y, 200, 22), "<b>베스트 10</b>", sSmall);
        y += 24;
        var medal = new[] { new Color(1f, 0.8f, 0.2f), new Color(0.78f, 0.8f, 0.84f), new Color(0.8f, 0.5f, 0.25f) };
        for (int i = 0; i < 10; i++)
        {
            float x = r.x + 20 + (i >= 5 ? r.width / 2 - 10 : 0), yy = y + (i % 5) * rowH;
            var circle = new Rect(x, yy + 2, rowH - 4, rowH - 4);
            GUI.DrawTexture(circle, AcPalette.Tex(i < 3 ? medal[i] : new Color(0.9f, 0.85f, 0.78f)));
            GUI.Label(circle, (i + 1).ToString(), new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            string nm = i < top.Count ? top[i].name : "-";
            string sc = i < top.Count ? top[i].best.ToString("#,0") + "점" : "";
            bool mine = i < top.Count && NameKey(top[i].name) == myKey;
            GUI.Label(new Rect(x + rowH + 4, yy + 2, r.width / 2 - rowH - 110, rowH), mine ? "<color=#c03030><b>" + nm + "</b></color>" : nm, sSmall);
            GUI.Label(new Rect(x + r.width / 2 - 120, yy + 2, 90, rowH), sc, new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
        }
        if (GUI.Button(new Rect(r.center.x - 70, r.yMax - 46, 140, 36), cg == CgState.Menu ? "<b>시작하기</b>" : "<b>다시 하기</b>", sBtn)) StartRun();
    }

    void MakeCgTextures()
    {
        cgSky = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) cgSky.SetPixel(0, y, Color.Lerp(new Color(0.98f, 0.86f, 0.75f), new Color(0.55f, 0.72f, 0.95f), y / 63f));
        cgSky.Apply();
        cgHill = new Texture2D(128, 64) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
            {
                float h = 30 + Mathf.Sin(x / 128f * Mathf.PI * 2) * 14 + Mathf.Sin(x / 128f * Mathf.PI * 4 + 1) * 6;
                cgHill.SetPixel(x, y, y < h ? Color.Lerp(new Color(0.85f, 0.9f, 0.98f), new Color(0.97f, 0.98f, 1f), y / h) : new Color(0, 0, 0, 0));
            }
        cgHill.Apply();
        cgGround = new Texture2D(32, 32) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                cgGround.SetPixel(x, y, y > 26 ? new Color(0.98f, 0.98f, 1f) : ((x / 8 + y / 8) % 2 == 0 ? new Color(0.78f, 0.52f, 0.32f) : new Color(0.7f, 0.45f, 0.28f)));
        cgGround.Apply();
    }
}
