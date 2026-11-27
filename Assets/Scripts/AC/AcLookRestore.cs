// AcLookRestore.cs - 같은 이름으로 다시 들어오면 예전 모습을 불러올지 물어봄 (11-27)
// 입장할 때 그 방의 orn/x~member~<이름> 에 저장된 모습(look)이 지금 모습과 다르면 '예전 모습으로 들어갈까요?' 창을 띄움
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public partial class AcRoom
{
    readonly HashSet<string> lookChecked = new HashSet<string>();
    string lookAskLook; bool lookAskSolo, lookChecking;

    // Enter()에서 부름: 처음 보는 이름·방이면 서버에서 예전 모습을 찾아보고, 다 되면 Enter를 다시 부름. true면 Enter는 여기서 멈춤
    bool CheckOldLookFirst(string code, bool solo)
    {
        string name = uiName.Trim();
        if (name == SantaName || lookChecking) return lookChecking;
        string room = string.IsNullOrEmpty(code) ? "xmas" : code;
        string key = NameKey(name) + "@" + room;
        if (lookChecked.Contains(key)) return false;
        lookChecked.Add(key);
        StartCoroutine(CheckOldLook(room, NameKey(name), solo));
        return true;
    }

    IEnumerator CheckOldLook(string room, string nameKey, bool solo)
    {
        lookChecking = true; lobbyMsg = "예전 모습을 찾는 중...";
        string url = AcFirebase.DbUrl + "/rooms/" + UnityWebRequest.EscapeURL(room) + "/orn/" + UnityWebRequest.EscapeURL("x~member~" + nameKey) + "/look.json";
        string look = "";
        using (var r = UnityWebRequest.Get(url))
        {
            r.timeout = 5;
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
            {
                var v = AcJson.Parse(r.downloadHandler.text) as string;
                if (!string.IsNullOrEmpty(v)) look = v;
            }
        }
        lookChecking = false; lobbyMsg = "";
        if (look != "" && me != null && look != me.look.Encode()) { lookAskLook = look; lookAskSolo = solo; }
        else Enter(solo);
    }

    // 입장 화면 위에 뜨는 물음 창
    bool LookAskGUI()
    {
        if (lookAskLook == null) return false;
        float w = Mathf.Min(SW - 30, 380), h = 178;
        var r = new Rect(SW / 2 - w / 2, Screen.height > Screen.width ? SH - h - 30 : SH / 2 - h / 2, w, h);
        GUI.Box(R(r), "", sBox);
        var mid = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Label(new Rect(r.x + 14, r.y + 14, w - 28, 30), "<b>" + uiName.Trim() + " 님, 다시 오셨네요!</b>", mid);
        GUI.Label(new Rect(r.x + 14, r.y + 46, w - 28, 44), "<size=14>예전에 꾸민 모습이 있어요.\n그 모습 그대로 들어갈까요?</size>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
        float bw = (w - 28 - 10) / 2, by = r.yMax - 62;
        if (GUI.Button(new Rect(r.x + 14, by, bw, 46), "<b>예전 모습으로</b>", sBtnBig))
        {
            var l = AcLook.Decode(lookAskLook);
            me.Rebuild(l); PlayerPrefs.SetString("ac_look", l.Encode()); PlayerPrefs.Save();
            lookAskLook = null; Enter(lookAskSolo);
        }
        if (GUI.Button(new Rect(r.x + 14 + bw + 10, by + 4, bw, 42), "지금 모습으로", sBtn)) { lookAskLook = null; Enter(lookAskSolo); }
        return true;
    }
}
