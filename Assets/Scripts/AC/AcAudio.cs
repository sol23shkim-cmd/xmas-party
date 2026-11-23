// AcAudio.cs - 배경 음악(크리스마스 캐럴 연주곡을 무작위로 이어서) + 쿠키 게임 전용 음악 + 효과음
using System.Collections.Generic;
using UnityEngine;

public partial class AcRoom
{
    static readonly string[] Carols = { "carol_silent", "carol_tree", "carol_noel" };   // 잔잔한 로맨틱 캐럴 3곡
    AudioSource musicA, musicB;      // 두 개로 겹쳐서 부드럽게 바꿈
    readonly List<AudioSource> sfxPool = new List<AudioSource>();
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    bool musicOn = true, inGameMusic;
    int lastCarol = -1; float musicGap;
    float musicVol = 0.34f, fade = 1f;

    void InitAudio()
    {
        musicOn = PlayerPrefs.GetInt("ac_music", 1) == 1;
        musicA = gameObject.AddComponent<AudioSource>(); musicB = gameObject.AddComponent<AudioSource>();
        foreach (var m in new[] { musicA, musicB }) { m.playOnAwake = false; m.spatialBlend = 0; m.volume = 0; }
        for (int i = 0; i < 10; i++) { var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0; sfxPool.Add(s); }
    }

    AudioClip Clip(string path)
    {
        if (clips.TryGetValue(path, out var c)) return c;
        c = Resources.Load<AudioClip>("Audio/" + path);
        clips[path] = c;
        return c;
    }

    public void Sfx(string name, float vol = 1f, float pitch = 1f)
    {
        var c = Clip("Sfx/" + name); if (c == null) return;
        AudioSource src = null;
        foreach (var s in sfxPool) if (!s.isPlaying) { src = s; break; }
        if (src == null) src = sfxPool[Random.Range(0, sfxPool.Count)];
        src.clip = c; src.pitch = pitch; src.volume = vol * (sfxOn ? 0.9f : 0f); src.Play();
    }
    bool sfxOn = true;

    // 매 프레임: 캐럴은 한 곡 끝나면 잠깐 쉬고 다른 곡 / 게임 중엔 전용 음악
    void UpdateAudio(float dt)
    {
        if (musicA == null || phase == Phase.Lobby) return;
        float target = musicOn ? musicVol : 0f;
        if (inGameMusic != (playingGame && cg != CgState.Board))
        {
            inGameMusic = playingGame && cg != CgState.Board;
            fade = 0; // 바꾸기
            musicB.clip = musicA.clip; musicB.time = musicA.isPlaying && musicA.clip ? Mathf.Min(musicA.time, musicA.clip.length - 0.05f) : 0; musicB.volume = musicA.volume; musicB.loop = musicA.loop; musicB.pitch = musicA.pitch;
            if (musicA.isPlaying) musicB.Play();
            musicA.Stop();
            if (inGameMusic) { musicA.clip = Clip("Music/cookie_theme"); musicA.loop = true; musicA.time = 0; musicA.Play(); }
            else { NextCarol(); }
        }
        fade = Mathf.MoveTowards(fade, 1, dt / 1.2f);
        musicA.volume = target * fade * (inGameMusic ? 0.85f : 1f);
        musicB.volume = target * (1 - fade) * (inGameMusic ? 1f : 0.85f);
        if (fade >= 1 && musicB.isPlaying) musicB.Stop();
        musicA.pitch = Mathf.MoveTowards(musicA.pitch, inGameMusic && cgFeverT > 0 ? 1.12f : 1f, dt * 0.5f);
        if (!inGameMusic && !musicA.isPlaying)
        {
            musicGap -= dt;
            if (musicGap <= 0) NextCarol();
        }
    }

    void NextCarol()
    {
        int k; do k = Random.Range(0, Carols.Length); while (Carols.Length > 1 && k == lastCarol);
        lastCarol = k;
        var c = Clip("Music/" + Carols[k]);
        musicA.clip = c; musicA.loop = false; musicA.pitch = 1; musicA.time = 0;
        if (c != null) musicA.Play();
        musicGap = 2.5f;
    }

    void MusicButton(Rect mapBtn)
    {
        if (phase != Phase.Play) return;
        var r = new Rect(mapBtn.x - 78, mapBtn.y, 70, mapBtn.height);
        R(r);
        if (GUI.Button(r, musicOn ? "음악 끄기" : "음악 켜기", sBtn))
        {
            musicOn = !musicOn; PlayerPrefs.SetInt("ac_music", musicOn ? 1 : 0); PlayerPrefs.Save();
        }
    }
}
