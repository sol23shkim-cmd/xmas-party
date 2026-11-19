// AcCatalog.cs — StreamingAssets/models/catalog.json (에셋 목록, 캐릭터 종류, 색 팔레트)
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable] public class AcItem
{
    public string name, cat, desc;
    public string[] groups;
    public float[] size, footprint, hang, light;
    public bool wall;
    public float tile;
    public string[] emissiveParts, transparentParts;
}
[Serializable] public class AcSpecies { public string name; public float top; public float[] fur; }
[Serializable] public class AcCatalogData
{
    public AcItem[] items; public AcSpecies[] species;
    public string[] faces, hairFemale, hairMale, hats;
    public float hatTop;
    public float[] palette;   // JsonUtility는 2차원 배열을 못 읽어서 아래에서 따로 처리
}

public static class AcCatalog
{
    static AcCatalogData data;
    static Dictionary<string, AcItem> byName;
    public static List<Color> Palette = new List<Color>();

    public static AcCatalogData Data { get { if (data == null) Load(); return data; } }

    public static void Load()
    {
        string path = Path.Combine(AcmLoader.ModelDir, "catalog.json");
        if (!File.Exists(path)) { data = new AcCatalogData { items = new AcItem[0], species = new AcSpecies[0] }; byName = new Dictionary<string, AcItem>(); return; }
        string json = File.ReadAllText(path);
        // "palette": [[r,g,b],...] → 따로 파싱
        int pi = json.IndexOf("\"palette\"", StringComparison.Ordinal);
        Palette.Clear();
        if (pi >= 0)
        {
            int s = json.IndexOf('[', pi), depth = 0, e = s;
            for (; e < json.Length; e++) { if (json[e] == '[') depth++; else if (json[e] == ']') { depth--; if (depth == 0) break; } }
            string arr = json.Substring(s + 1, e - s - 1);
            foreach (var chunk in arr.Split(new[] { ']' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var nums = chunk.Replace("[", "").Replace(",", " ").Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (nums.Length >= 3)
                    Palette.Add(new Color(float.Parse(nums[0], System.Globalization.CultureInfo.InvariantCulture),
                                          float.Parse(nums[1], System.Globalization.CultureInfo.InvariantCulture),
                                          float.Parse(nums[2], System.Globalization.CultureInfo.InvariantCulture)));
            }
            json = json.Substring(0, pi) + "\"palette\": []" + json.Substring(e + 1);
        }
        data = JsonUtility.FromJson<AcCatalogData>(json);
        byName = new Dictionary<string, AcItem>();
        foreach (var it in data.items) byName[it.name] = it;
    }

    public static AcItem Get(string name) { if (data == null) Load(); return byName.TryGetValue(name, out var it) ? it : null; }

    public static List<AcItem> ByCat(string cat)
    {
        var l = new List<AcItem>();
        foreach (var it in Data.items) if (it.cat == cat) l.Add(it);
        return l;
    }

    public static AcSpecies Species(string name)
    {
        foreach (var s in Data.species) if (s.name == name) return s;
        return null;
    }

    public static GameObject Spawn(string name, Transform parent = null, Dictionary<string, Color> tints = null)
    {
        var it = Get(name);
        return AcmLoader.Spawn(name, parent, tints, it != null ? it.emissiveParts : null);
    }
}
