// AcMobile.cs - 휴대폰(터치)/웹용 조작: 눌러서/끌어서 이동, 화면 버튼, 길게 눌러 떼기, 브라우저 입력창, 가벼운 화질
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class AcRoom
{
    public static bool IsMobile;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int AcWeb_IsMobile();
    [DllImport("__Internal")] static extern string AcWeb_Prompt(string msg, string def);
    static bool Web => true;
#else
    static int AcWeb_IsMobile() => 0;
    static string AcWeb_Prompt(string msg, string def) => null;
    static bool Web => false;
#endif

    static bool DetectMobile() => Application.isMobilePlatform || AcWeb_IsMobile() == 1;

    void QualityForMobile()
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null) { urp.renderScale = 0.8f; urp.shadowDistance = 22f; urp.msaaSampleCount = 1; }
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data) data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        Application.targetFrameRate = 30;
    }

    // ===== 화면 UI 영역 (그 위를 누르면 이동하지 않음) =====
    readonly List<Rect> uiRects = new List<Rect>();
    Rect R(Rect r) { uiRects.Add(r); return r; }
    bool OverUI(Vector2 screen)
    {
        var p = new Vector2(screen.x / UiK, (Screen.height - screen.y) / UiK);
        foreach (var r in uiRects) if (r.Contains(p)) return true;
        return false;
    }

    // ===== 포인터 (마우스/손가락 공통) =====
    bool gPressed, gDown, gUp, gSeen; Vector2 gPos;   // 화면 이벤트(IMGUI)로 받은 누르기
    bool ptrDown, ptrPressed, ptrUp; Vector2 ptrPos, ptrStart; float ptrDownAt; bool ptrOnUI, ptrMoved;
    void UpdatePointer()
    {
        bool pressed; Vector2 pos;
#if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        pressed = p != null && p.press.isPressed; pos = p != null ? p.position.ReadValue() : Vector2.zero;
        // 아주 짧게 톡 누르면 한 프레임 안에 눌림+뗌이 다 지나가요 → 이번 프레임 기록으로도 잡기
        bool wDown = p != null && p.press.wasPressedThisFrame, wUp = p != null && p.press.wasReleasedThisFrame;
#else
        pressed = Input.GetMouseButton(0); pos = Input.mousePosition;
        bool wDown = Input.GetMouseButtonDown(0), wUp = Input.GetMouseButtonUp(0);
#endif
        if (gSeen)
        {   // 화면 이벤트(IMGUI)를 한 번이라도 받았으면 그쪽 누름 상태를 믿음 (휴대폰 흉내·일부 브라우저에서 입력 장치 쪽 떼기가 안 들어옴)
            wDown = gDown; wUp = gUp;
            pressed = gPressed;
            pos = gPos;
            gDown = false; gUp = false;
        }
        ptrDown = wDown || (pressed && !ptrPressed); ptrUp = wUp || (!pressed && ptrPressed); ptrPressed = pressed; ptrPos = pos;
        if (ptrDown) { ptrStart = pos; ptrDownAt = Time.unscaledTime; ptrOnUI = OverUI(pos); ptrMoved = false; }
        if (pressed && (pos - ptrStart).magnitude > 18f) ptrMoved = true;
    }
    bool Tap => ptrUp && !ptrOnUI && !ptrMoved && Time.unscaledTime - ptrDownAt < 0.45f;
    bool LongPress => ptrPressed && !ptrOnUI && !ptrMoved && Time.unscaledTime - ptrDownAt > 0.55f;

    // 바닥을 누르거나 끌면 그쪽으로 걸어감
    Vector3? walkTarget; bool longUsed;
    bool GroundAt(Vector2 screen, out Vector3 p)
    {
        var ray = cam.ScreenPointToRay(screen);
        if (Mathf.Abs(ray.direction.y) < 1e-4f) { p = default; return false; }
        float t = -ray.origin.y / ray.direction.y;
        p = ray.origin + ray.direction * t;
        return t > 0;
    }

    void TapMove(float dt)
    {
        if (me.skating)   // 스케이트: 누른 곳 방향으로 미끄러짐 (움직임은 MovePlayer가 함)
        {
            if (ptrPressed && !ptrOnUI && (ptrMoved || Time.unscaledTime - ptrDownAt > 0.15f) && GroundAt(ptrPos, out var gs)) walkTarget = gs;
            else if (Tap && GroundAt(ptrPos, out var gs2)) walkTarget = gs2;
            return;
        }
        if (ptrDown) longUsed = false;
        // 눈싸움: 톡 누르면 그곳(또는 누른 친구)으로 눈덩이를 던짐 (끌면 걸어감)
        if (snowMode && Tap)
        {
            var ray = cam.ScreenPointToRay(ptrPos); Vector3? tgt = null;
            foreach (var kv in remotes)
            {
                if (!kv.Value.c) continue;
                var c = kv.Value.c.transform.position + Vector3.up * 0.6f;
                if (Vector3.Cross(ray.direction, c - ray.origin).magnitude < 0.7f) { tgt = kv.Value.c.transform.position; break; }
            }
            if (!tgt.HasValue && GroundAt(ptrPos, out var gp)) tgt = gp;
            if (tgt.HasValue) { ThrowSnowball(tgt.Value); walkTarget = null; return; }
        }
        // 베개 싸움: 톡 누르면 그쪽(또는 누른 친구)으로 베개를 휘두름 (끌면 걸어감)
        if (pillowMode && Tap)
        {
            var ray = cam.ScreenPointToRay(ptrPos); Vector3? tgt = null;
            foreach (var kv in remotes) { if (!kv.Value.c) continue; var c = kv.Value.c.transform.position + Vector3.up * 0.6f; if (Vector3.Cross(ray.direction, c - ray.origin).magnitude < 0.7f) { tgt = kv.Value.c.transform.position; break; } }
            if (!tgt.HasValue && GroundAt(ptrPos, out var gp2)) tgt = gp2;
            SwingPillow(tgt); walkTarget = null; return;
        }
        // 마당의 눈사람을 톡 누르면 그 눈사람 보기
        if (Tap && Outside && PickSnowman(ptrPos, out var sid)) { FocusSnowman(sid); walkTarget = null; return; }
        // 꾸미는 중에 트리를 누른 것이면 이동하지 않음
        bool onTree = decorating && treeCol != null && treeCol.Raycast(cam.ScreenPointToRay(ptrPos), out _, 100f);
        if (ptrDown) onTreeAtDown = onTree;
        bool keyMove = MoveAxis().sqrMagnitude > 0.01f;
        if (keyMove) { walkTarget = null; return; }
        if (ptrPressed && !ptrOnUI && !onTreeAtDown && (ptrMoved || Time.unscaledTime - ptrDownAt > 0.15f) && !showCustom)
        {
            if (GroundAt(ptrPos, out var g)) walkTarget = g;   // 끌면 계속 따라감
        }
        else if (Tap && !onTree && !onTreeAtDown && GroundAt(ptrPos, out var g2)) walkTarget = g2;
        if (!walkTarget.HasValue) return;
        var d = walkTarget.Value - me.transform.position; d.y = 0;
        if (d.magnitude < 0.15f) { walkTarget = null; return; }
        var dir = d.normalized;
        float spd = Mathf.Min(3.6f, d.magnitude * 4f + 1f);
        var prev = me.transform.position;
        var p = prev + dir * spd * dt;
        p = Collide(p, 0.36f);
        p = ClampRegion(prev, p);
        var tv = new Vector2(p.x - TreePos.x, p.z - TreePos.z);
        if (tv.magnitude < TreeR) { tv = tv.normalized * TreeR; p.x = TreePos.x + tv.x; p.z = TreePos.z + tv.y; }
        var moved = p - prev; moved.y = 0;
        if (moved.magnitude < spd * dt * 0.15f && !ptrPressed) walkTarget = null;   // 막혀서 못 가면 멈춤
        me.speed = dt > 0 ? moved.magnitude / dt : 0;
        if (moved.sqrMagnitude > 1e-6f) yaw = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
        me.transform.position = p;
        me.transform.rotation = Quaternion.Slerp(me.transform.rotation, Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-dt * 14f));
    }
    bool onTreeAtDown;

    // ===== 글 입력 (웹은 브라우저 입력창, PC는 화면 글 칸) =====
    string TextBox(string prompt, string value, int max)
    {
        if (Web)
        {
            if (GUILayout.Button(string.IsNullOrEmpty(value) ? "<color=#a08a78>눌러서 입력</color>" : value, sBtn, GUILayout.Height(28)))
            {
                var r = AcWeb_Prompt(prompt, value ?? "");
                if (r != null && r != "\u0000") value = r.Length > max ? r.Substring(0, max) : r;
            }
            return value;
        }
        return GUILayout.TextField(value ?? "", max, GUILayout.Height(26));
    }

    void OpenChat()
    {
        if (Web)
        {
            var r = AcWeb_Prompt("하고 싶은 말", "");
            if (r != null && r != "\u0000") Say(r);
            return;
        }
        chatting = true; chatText = ""; chatOpenedAt = Time.unscaledTime;
    }

    // ===== 끌어서 스크롤 (휴대폰처럼 손가락으로 위아래로 밀기) =====
    readonly Dictionary<string, Rect> scrollRects = new Dictionary<string, Rect>();
    string dragKey; Vector2 dragLast, dragStart; bool dragMoved;

    Vector2 BeginDragScroll(string key, Vector2 scroll, params GUILayoutOption[] opts)
    {
        DragScrollEvents(key, ref scroll);
        return GUILayout.BeginScrollView(scroll, false, false, opts);
    }
    void EndDragScroll(string key)
    {
        GUILayout.EndScrollView();
        if (Event.current.type == EventType.Repaint) scrollRects[key] = GUILayoutUtility.GetLastRect();
    }
    // GUILayout 밖(직접 그리는 목록)에서 쓰는 버전: 보이는 영역 r을 직접 알려줌
    void DragScrollArea(string key, Rect r, ref Vector2 scroll) { scrollRects[key] = r; DragScrollEvents(key, ref scroll); }

    void DragScrollEvents(string key, ref Vector2 scroll)
    {
        var e = Event.current;
        if (!scrollRects.TryGetValue(key, out var r)) return;
        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) { dragKey = key; dragLast = dragStart = e.mousePosition; dragMoved = false; }
        else if (e.type == EventType.MouseDrag && dragKey == key)
        {
            var d = e.mousePosition - dragLast; dragLast = e.mousePosition;
            if (!dragMoved && (e.mousePosition - dragStart).magnitude > 7f) { dragMoved = true; GUIUtility.hotControl = 0; }
            if (dragMoved) { scroll.y = Mathf.Max(0, scroll.y - d.y); e.Use(); }
        }
        else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && dragKey == key)
        {
            dragKey = null;
            if (dragMoved) { GUIUtility.hotControl = 0; if (e.type == EventType.MouseUp) e.Use(); }
        }
    }
    bool Dragging => dragKey != null && dragMoved;

    // ===== 글 입력 창 (웹: 브라우저 입력창 / PC: 화면 창) =====
    string askTitle, askText = ""; bool askMulti; Action<string> askDone; bool askOpen;
    void Ask(string title, string def, bool multiline, Action<string> done)
    {
        if (Web)
        {
            var r = AcWeb_Prompt(title, def ?? "");
            if (r != null && r != "\u0000" && r.Trim() != "") done(r);
            return;
        }
        askTitle = title; askText = def ?? ""; askMulti = multiline; askDone = done; askOpen = true;
    }

    void AskWindow()
    {
        if (!askOpen) return;
        float w = Mathf.Min(520, SW - 30), h = askMulti ? 300 : 140;
        var r = new Rect(SW / 2 - w / 2, SH / 2 - h / 2 - 40, w, h);
        GUI.DrawTexture(new Rect(0, 0, SW, SH), AcPalette.Tex(new Color(0, 0, 0, 0.25f)));
        R(new Rect(0, 0, SW, SH));
        GUI.Box(r, "", sBox);
        GUI.Label(new Rect(r.x + 16, r.y + 10, w - 32, 24), askTitle, sLabel);
        GUI.SetNextControlName("ask");
        if (askMulti) askText = GUI.TextArea(new Rect(r.x + 16, r.y + 40, w - 32, h - 96), askText, 600, sHandArea ?? GUI.skin.textArea);
        else askText = GUI.TextField(new Rect(r.x + 16, r.y + 42, w - 32, 30), askText, 60);
        GUI.FocusControl("ask");
        var e = Event.current;
        bool enter = !askMulti && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        if (GUI.Button(new Rect(r.xMax - 196, r.yMax - 44, 86, 32), "취소", sBtn) || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)) { askOpen = false; GUIUtility.keyboardControl = 0; }
        if (GUI.Button(new Rect(r.xMax - 102, r.yMax - 44, 86, 32), "확인", sBtn) || enter)
        {
            askOpen = false; GUIUtility.keyboardControl = 0;
            if (askText.Trim() != "") askDone?.Invoke(askText);
            if (enter) e.Use();
        }
    }

    // ===== 화면 버튼 (말하기/인사/하트/만세/꾸미기) =====
    void ActionButtons()
    {
        float gap = 6, bw = Mathf.Min(IsMobile ? 70 : 62, (SW - 30) / 5f - gap), bh = IsMobile ? 46 : 36;
        string[] labels = { "말하기", "인사", "하트", "만세", "꾸미기" };
        float x = SW - (bw + gap) * labels.Length - 10, y = SH - bh - 12;
        R(new Rect(x - 4, y - 4, (bw + gap) * labels.Length + 2, bh + 8));   // 뒤 판 없이 버튼만 (터치 영역은 그대로 등록)
        for (int i = 0; i < labels.Length; i++)
        {
            if (GUI.Button(new Rect(x + i * (bw + gap), y, bw, bh), labels[i], sBtn))
            {
                switch (i)
                {
                    case 0: OpenChat(); break;
                    case 1: Emote("wave"); break;
                    case 2: Emote("heart"); break;
                    case 3: Emote("cheer"); break;
                    case 4: showCustom = !showCustom; break;
                }
            }
        }
    }
}
