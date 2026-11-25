// AcStrip.cs - 옆으로 넘기는 그림 줄 (트리 장식·눈사람 장식 공통)
// 줄을 옆으로 끌거나 휠로 넘기고, 그림을 누르면 고르기, 그림을 위로 끌어 내놓으면 그 자리에 붙이기(끌어 놓기는 각 모드가 처리)
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    class IconStripState { public float scroll, x0, y0, s0; public bool drag, moved; public int selSeen = -1; }
    readonly IconStripState ornStrip = new IconStripState(), snowStrip = new IconStripState();

    // area: 줄이 들어갈 자리(너비 기준). 누른 그림 번호를 돌려줌 (없으면 -1). 그림 사각형은 decoIconRects에 (끌기 시작 판단용)
    int IconStrip(IconStripState st, Rect area, int count, float size, Func<int, Texture> icon, int sel, bool picked)
    {
        var e = Event.current;
        if (e.type == EventType.Repaint) decoIconRects.Clear();
        float step = size + 6;
        int fit = Mathf.Max(1, (int)((area.width - 6) / step));
        float viewW = Mathf.Min(fit, count) * step, contentW = count * step;
        var view = new Rect(area.x, area.y, viewW + 6, size + 6);
        float maxScroll = Mathf.Max(0, contentW - viewW);
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) { st.scroll += (Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y) ? e.delta.x : e.delta.y) * 22f; e.Use(); }
        if (e.type == EventType.MouseDown && view.Contains(e.mousePosition)) { st.drag = true; st.moved = false; st.x0 = e.mousePosition.x; st.y0 = e.mousePosition.y; st.s0 = st.scroll; }
        if (e.type == EventType.MouseDrag && st.drag && !dragOrn)
        {
            float dx = e.mousePosition.x - st.x0, dy = e.mousePosition.y - st.y0;
            if (!st.moved && Mathf.Abs(dx) > 10 && Mathf.Abs(dx) > Mathf.Abs(dy)) { st.moved = true; dragCand = -1; }
            if (st.moved) st.scroll = st.s0 - dx;
        }
        if (e.type == EventType.MouseUp) st.drag = false;
        if (sel != st.selSeen) { st.selSeen = sel; if (sel * step < st.scroll) st.scroll = sel * step; else if ((sel + 1) * step > st.scroll + viewW) st.scroll = (sel + 1) * step - viewW; }
        st.scroll = Mathf.Clamp(st.scroll, 0, maxScroll);
        if (st.scroll > 1) GUI.Label(new Rect(view.x - 14, view.y + size / 2 - 12, 14, 24), "<", sLabel);
        if (st.scroll < maxScroll - 1) GUI.Label(new Rect(view.xMax + 1, view.y + size / 2 - 12, 14, 24), ">", sLabel);
        int clicked = -1;
        GUI.BeginGroup(view);
        int first = Mathf.Max(0, (int)(st.scroll / step)), last = Mathf.Min(count - 1, (int)((st.scroll + viewW) / step) + 1);
        for (int i = first; i <= last; i++)
        {
            var r = new Rect(3 + i * step - st.scroll, 3, size, size);
            bool on = i == sel && picked;
            AcUiTheme.Fill(r, on ? new Color(1f, 0.93f, 0.8f) : new Color(1f, 1f, 1f, 0.85f), 3); if (on) AcUiTheme.Ring(r, AcUiTheme.Accent, 3);
            var tex = icon(i); if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, false);
            if (e.type == EventType.Repaint)
            {
                var ar = new Rect(r.x + view.x, r.y + view.y, r.width, r.height);
                ar = Rect.MinMaxRect(Mathf.Max(ar.xMin, view.xMin), ar.yMin, Mathf.Min(ar.xMax, view.xMax), ar.yMax);
                if (ar.width > 4) decoIconRects.Add((ar, i));
            }
            if (GUI.Button(r, "", GUIStyle.none) && !dragOrn && !st.moved) clicked = i;
        }
        GUI.EndGroup();
        R(view);
        return clicked;
    }
}
