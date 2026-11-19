// AcMobile.cs - 휴대폰(터치)/웹용 조작: 눌러서/끌어서 이동, 화면 버튼, 길게 눌러 떼기, 브라우저 입력창, 가벼운 화질
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
        if (ptrDown) longUsed = false;
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
        if (tv.magnitude < 2.6f) { tv = tv.normalized * 2.6f; p.x = TreePos.x + tv.x; p.z = TreePos.z + tv.y; }
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

    // ===== 화면 버튼 (말하기/인사/하트/만세/꾸미기) =====
    void ActionButtons()
    {
        float gap = 6, bw = Mathf.Min(IsMobile ? 70 : 62, (SW - 30) / 5f - gap), bh = IsMobile ? 46 : 36;
        string[] labels = { "말하기", "인사", "하트", "만세", "꾸미기" };
        float x = SW - (bw + gap) * labels.Length - 10, y = SH - bh - 12;
        GUI.Box(R(new Rect(x - 8, y - 8, (bw + gap) * labels.Length + 10, bh + 16)), "", sBox);
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
