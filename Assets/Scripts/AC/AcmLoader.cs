// AcmLoader.cs — Tools/acgen 이 만든 .acm 모델(StreamingAssets/models)을 실행 중에 읽어서 GameObject로 만들어요.
// 조각(part)마다 메시 하나. pivot/parent 정보로 팔·다리를 따로 돌릴 수 있게 계층을 만들어요.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public static class AcmLoader
{
    public class PartData
    {
        public string name, group, parent;
        public bool hasTint; public Vector4 tint; public float gloss; public Vector3 pivot;
        public Mesh mesh;
        public Bounds bounds;
    }

    public class ModelData
    {
        public string name;
        public List<PartData> parts = new List<PartData>();
        public Bounds bounds;
    }

    static readonly Dictionary<string, ModelData> cache = new Dictionary<string, ModelData>();
    static Material sharedMat;
    static MaterialPropertyBlock mpb;
    public static string ModelDir => Path.Combine(Application.streamingAssetsPath, "models");

    public static Material Mat
    {
        get
        {
            if (sharedMat == null)
            {
                var sh = Resources.Load<Shader>("ACToon");
                if (sh == null) sh = Shader.Find("XmasPlaza/ACToon");
                sharedMat = new Material(sh) { name = "ACToon (shared)", enableInstancing = false };
            }
            return sharedMat;
        }
    }

    static Dictionary<string, byte[]> pack;
    static void LoadPack()
    {
        if (pack != null) return;
        pack = new Dictionary<string, byte[]>();
        string pp = Path.Combine(ModelDir, "models.pack");
        if (!File.Exists(pp)) return;
        using (var br = new BinaryReader(File.OpenRead(pp)))
        {
            if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "ACPK") return;
            int n = br.ReadInt32();
            for (int i = 0; i < n; i++) { string nm = Str(br); int len = br.ReadInt32(); pack[nm] = br.ReadBytes(len); }
        }
    }

    public static bool Exists(string name) { LoadPack(); return pack.ContainsKey(name) || File.Exists(Path.Combine(ModelDir, name + ".acm")); }

    public static ModelData Load(string name)
    {
        if (cache.TryGetValue(name, out var md)) return md;
        LoadPack();
        string path = Path.Combine(ModelDir, name + ".acm");
        Stream st;
        if (File.Exists(path)) st = File.OpenRead(path);          // 낱개 파일이 있으면 그걸 우선 (작업 중 교체용)
        else if (pack.TryGetValue(name, out var bytes)) st = new MemoryStream(bytes);
        else { Debug.LogWarning("[AcmLoader] 모델 없음: " + name); return null; }
        md = new ModelData { name = name };
        using (var br = new BinaryReader(st))
        {
            var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (magic != "ACM2") { Debug.LogWarning("[AcmLoader] 형식이 달라요: " + path); return null; }
            int n = br.ReadInt32();
            bool first = true;
            for (int i = 0; i < n; i++)
            {
                var p = new PartData();
                p.name = Str(br); p.group = Str(br); p.parent = Str(br);
                p.hasTint = br.ReadByte() != 0;
                p.tint = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), 1);
                p.gloss = br.ReadSingle();
                p.pivot = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                int nv = br.ReadInt32(), ni = br.ReadInt32();
                var pos = new Vector3[nv]; var nor = new Vector3[nv]; var col = new Color32[nv]; var uv = new Vector2[nv];
                for (int k = 0; k < nv; k++) pos[k] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()) - p.pivot;
                for (int k = 0; k < nv; k++) nor[k] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var cb = br.ReadBytes(nv * 4);
                for (int k = 0; k < nv; k++) col[k] = new Color32(cb[k * 4], cb[k * 4 + 1], cb[k * 4 + 2], cb[k * 4 + 3]);
                var ab = br.ReadBytes(nv);
                for (int k = 0; k < nv; k++) uv[k] = new Vector2(ab[k] / 255f, 0);
                var idx = new int[ni];
                var ib = br.ReadBytes(ni * 4);
                Buffer.BlockCopy(ib, 0, idx, 0, ni * 4);
                var mesh = new Mesh { name = name + "/" + p.name };
                if (nv > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = pos; mesh.normals = nor; mesh.colors32 = col; mesh.uv = uv;
                mesh.triangles = idx;
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                p.mesh = mesh;
                var b = mesh.bounds; b.center += p.pivot; p.bounds = b;
                if (first) { md.bounds = b; first = false; } else md.bounds.Encapsulate(b);
                md.parts.Add(p);
            }
        }
        cache[name] = md;
        return md;
    }

    static string Str(BinaryReader br) { int n = br.ReadInt32(); return n > 0 ? Encoding.UTF8.GetString(br.ReadBytes(n)) : ""; }

    /// 모델을 만들어 붙여요. tints: 색묶음(group) 또는 조각 이름 → 감마 공간 색.
    public static GameObject Spawn(string name, Transform parent = null, Dictionary<string, Color> tints = null,
                                   ICollection<string> emissive = null, bool castShadows = true)
    {
        var md = Load(name);
        var root = new GameObject(name);
        if (parent) root.transform.SetParent(parent, false);
        if (md == null) return root;
        var map = new Dictionary<string, Transform>();
        foreach (var p in md.parts)
        {
            var go = new GameObject(p.name);
            map[p.name] = go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var info = go.AddComponent<AcPart>();
            info.data = p;
        }
        foreach (var p in md.parts)
        {
            var t = map[p.name];
            Transform par = (!string.IsNullOrEmpty(p.parent) && map.TryGetValue(p.parent, out var pt)) ? pt : root.transform;
            t.SetParent(par, false);
            Vector3 parentPivot = par == root.transform ? Vector3.zero : par.GetComponent<AcPart>().data.pivot;
            t.localPosition = p.pivot - parentPivot;
        }
        ApplyTints(root, tints, emissive);
        return root;
    }

    public static void ApplyTints(GameObject root, Dictionary<string, Color> tints, ICollection<string> emissive = null)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var part in root.GetComponentsInChildren<AcPart>(true))
        {
            var p = part.data;
            Vector4 t = p.tint;
            if (tints != null && (tints.TryGetValue(p.group, out var c) || tints.TryGetValue(p.name, out c))) t = new Vector4(c.r, c.g, c.b, 1);
            part.currentTint = t;
            mpb.Clear();
            mpb.SetVector("_Tint", t);
            mpb.SetFloat("_HasTint", p.hasTint ? 1 : 0);
            mpb.SetFloat("_Gloss", p.gloss);
            mpb.SetFloat("_Emit", emissive != null && emissive.Contains(p.name) ? 1 : 0);
            part.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
        }
    }

    public static Transform Find(GameObject root, string partName)
    {
        foreach (var p in root.GetComponentsInChildren<AcPart>(true)) if (p.name == partName) return p.transform;
        return null;
    }
}

/// 조각 하나에 붙는 정보 (색묶음, 기본 색, 회전 중심)
public class AcPart : MonoBehaviour
{
    public AcmLoader.PartData data;
    public Vector4 currentTint;
}
