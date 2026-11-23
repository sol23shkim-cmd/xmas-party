// AcNav.cs - 따라가기용 길찾기: 0.5m 격자 위에서 가구·벽·트리·눈사람을 피해 A*로 길을 찾음
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    const float NavCell = 0.5f;
    int navW, navH; float navX0, navZ0;
    bool[] navStatic;          // 가구·벽·트리 (한 번만 계산)
    List<Vector3> navPath; int navIdx; float navNextAt; Vector3 navGoal;

    void BuildNav()
    {
        navX0 = WorldX0; navZ0 = WorldZ0;
        navW = Mathf.CeilToInt((WorldX1 - WorldX0) / NavCell); navH = Mathf.CeilToInt((WorldZ1 - WorldZ0) / NavCell);
        navStatic = new bool[navW * navH];
        for (int j = 0; j < navH; j++)
            for (int i = 0; i < navW; i++)
            {
                float x = navX0 + (i + 0.5f) * NavCell, z = navZ0 + (j + 0.5f) * NavCell;
                navStatic[j * navW + i] = StaticWalkable(x, z);
            }
    }

    bool StaticWalkable(float x, float z)
    {
        const float wall = 0.55f, body = 0.4f;
        if (new Vector2(x - TreePos.x, z - TreePos.z).magnitude < TreeR + 0.25f) return false;
        var p = new Vector2(x, z);
        foreach (var (a, b) in wallSegs)
        {
            if (x < Mathf.Min(a.x, b.x) - 1 || x > Mathf.Max(a.x, b.x) + 1 || z < Mathf.Min(a.y, b.y) - 1 || z > Mathf.Max(a.y, b.y) + 1) continue;
            if (SegDist(p, a, b) < wall) return false;
        }
        foreach (var o in obstacles)
        {
            var l = o.inv * (new Vector3(x, 0, z) - o.c);
            if (Mathf.Abs(l.x) < o.h.x + body && Mathf.Abs(l.z) < o.h.y + body) return false;
        }
        return true;
    }

    bool NavOk(int i, int j)
    {
        if (i < 0 || j < 0 || i >= navW || j >= navH || !navStatic[j * navW + i]) return false;
        float x = navX0 + (i + 0.5f) * NavCell, z = navZ0 + (j + 0.5f) * NavCell;
        foreach (var c in snowCircles) if (new Vector2(x - c.x, z - c.y).magnitude < c.z + 0.35f) return false;
        return true;
    }

    // A* (8방향). 실패하면 null
    List<Vector3> FindPath(Vector3 a, Vector3 b)
    {
        if (navStatic == null) return null;
        int si = Mathf.Clamp((int)((a.x - navX0) / NavCell), 0, navW - 1), sj = Mathf.Clamp((int)((a.z - navZ0) / NavCell), 0, navH - 1);
        int gi = Mathf.Clamp((int)((b.x - navX0) / NavCell), 0, navW - 1), gj = Mathf.Clamp((int)((b.z - navZ0) / NavCell), 0, navH - 1);
        // 목표 칸이 막혀 있으면 가까운 열린 칸으로
        if (!NavOk(gi, gj))
        {
            bool found = false;
            for (int r = 1; r <= 6 && !found; r++)
                for (int dj = -r; dj <= r && !found; dj++)
                    for (int di = -r; di <= r && !found; di++)
                        if (NavOk(gi + di, gj + dj)) { gi += di; gj += dj; found = true; }
            if (!found) return null;
        }
        int start = sj * navW + si, goal = gj * navW + gi;
        bool startBlocked = !NavOk(si, sj);
        var g = new Dictionary<int, float>(); var from = new Dictionary<int, int>(); var closed = new HashSet<int>();
        var open = new SortedSet<(float f, int id)>();
        g[start] = 0; open.Add((H(si, sj, gi, gj), start));
        int[] di8 = { 1, -1, 0, 0, 1, 1, -1, -1 }, dj8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        int iter = 0;
        while (open.Count > 0 && iter++ < 60000)
        {
            var cur = open.Min; open.Remove(cur);
            int id = cur.id;
            if (id == goal) break;
            if (!closed.Add(id)) continue;
            int ci = id % navW, cj = id / navW;
            for (int k = 0; k < 8; k++)
            {
                int ni = ci + di8[k], nj = cj + dj8[k];
                bool near = startBlocked && Mathf.Abs(ni - si) <= 2 && Mathf.Abs(nj - sj) <= 2;
                if (!NavOk(ni, nj) && !near) continue;
                if (k >= 4 && !near && (!NavOk(ci + di8[k], cj) || !NavOk(ci, cj + dj8[k]))) continue;   // 모서리 끼기 방지
                int nid = nj * navW + ni;
                float ng = g[id] + (k < 4 ? 1f : 1.4142f);
                if (g.TryGetValue(nid, out var og) && og <= ng) continue;
                g[nid] = ng; from[nid] = id;
                open.Add((ng + H(ni, nj, gi, gj), nid));
            }
        }
        if (!from.ContainsKey(goal) && goal != start) return null;
        var cells = new List<int>();
        for (int c = goal; c != start; c = from[c]) cells.Add(c);
        cells.Reverse();
        var pts = new List<Vector3>();
        foreach (var c in cells) pts.Add(new Vector3(navX0 + (c % navW + 0.5f) * NavCell, 0, navZ0 + (c / navW + 0.5f) * NavCell));
        // 곧게 갈 수 있는 점은 건너뛰기
        var smooth = new List<Vector3>();
        var anchor = a;
        int idx = 0;
        while (idx < pts.Count)
        {
            int far = idx;
            for (int t = pts.Count - 1; t > idx; t--) if (LineClear(anchor, pts[t])) { far = t; break; }
            smooth.Add(pts[far]); anchor = pts[far]; idx = far + 1;
        }
        return smooth;
    }

    static float H(int i, int j, int gi, int gj) { int dx = Mathf.Abs(i - gi), dz = Mathf.Abs(j - gj); return Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz); }

    bool LineClear(Vector3 a, Vector3 b)
    {
        float len = Flat(a, b); int steps = Mathf.CeilToInt(len / (NavCell * 0.5f));
        for (int s = 1; s <= steps; s++)
        {
            var p = Vector3.Lerp(a, b, s / (float)steps);
            if (!NavOk((int)((p.x - navX0) / NavCell), (int)((p.z - navZ0) / NavCell))) return false;
        }
        return true;
    }

    // 따라갈 다음 지점 (길을 0.7초마다 다시 찾음)
    Vector3 FollowStep(Vector3 a, Vector3 b)
    {
        if (navPath == null || Time.unscaledTime > navNextAt || Flat(navGoal, b) > 1.5f)
        {
            navPath = FindPath(a, b); navIdx = 0; navGoal = b; navNextAt = Time.unscaledTime + 0.7f;
        }
        if (navPath == null || navPath.Count == 0) return NextWaypoint(a, b);
        while (navIdx < navPath.Count - 1 && Flat(a, navPath[navIdx]) < 0.45f) navIdx++;
        return navPath[navIdx];
    }
}
