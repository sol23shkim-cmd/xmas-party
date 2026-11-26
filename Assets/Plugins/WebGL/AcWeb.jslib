// AcWeb.jslib — 웹(휴대폰 브라우저)에서만 쓰는 기능: Firebase 실시간 스트림, 글 입력창, 기기 확인
mergeInto(LibraryManager.library, {
  AcWeb_Stream: function (urlPtr, goPtr) {
    var url = UTF8ToString(urlPtr), go = UTF8ToString(goPtr);
    if (window._acStream) { try { window._acStream.close(); } catch (e) {} }
    var es = new EventSource(url);
    window._acStream = es;
    var send = function (type, data) { try { SendMessage(go, 'OnFbEvent', type + '\n' + data); } catch (e) {} };
    es.addEventListener('put', function (e) { send('put', e.data); });
    es.addEventListener('patch', function (e) { send('patch', e.data); });
    es.addEventListener('cancel', function (e) { send('error', 'cancel'); });
    es.addEventListener('auth_revoked', function (e) { send('error', 'auth'); });
    es.onerror = function () { send('error', 'conn'); };
  },
  AcWeb_StopStream: function () { if (window._acStream) { try { window._acStream.close(); } catch (e) {} window._acStream = null; } },
  AcWeb_LeaveOnClose: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var bye = function () { try { fetch(url, { method: 'DELETE', keepalive: true }); } catch (e) {} };
    window._acBye = bye;
    window.addEventListener('pagehide', bye);
    window.addEventListener('beforeunload', bye);
  },
  AcWeb_Prompt: function (msgPtr, defPtr) {
    var r = window.prompt(UTF8ToString(msgPtr), UTF8ToString(defPtr));
    if (r === null) r = '\u0000';
    var n = lengthBytesUTF8(r) + 1, p = _malloc(n);
    stringToUTF8(r, p, n);
    return p;
  },
  AcWeb_Reload: function () {   // 게임 나가기: 내 자리 지우고 창 닫기 시도 → 브라우저가 막으면 작별 화면
    try { if (window._acStream) window._acStream.close(); } catch (e) {}
    try { if (window._acBye) window._acBye(); } catch (e) {}
    setTimeout(function () {
      try { window.open('', '_self'); window.close(); } catch (e) {}
      setTimeout(function () {
        if (window.closed) return;
        try { if (window.unityInstance && window.unityInstance.Quit) window.unityInstance.Quit(); } catch (e) {}
        document.body.innerHTML = '';
        var d = document.createElement('div');
        d.style.cssText = 'position:fixed;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:18px;background:linear-gradient(#fdf6ee,#f3e3e6);font-family:sans-serif;color:#5b3a2e;text-align:center;padding:24px';
        d.innerHTML = '<div style="font-size:54px">🎄</div><div style="font-size:22px;font-weight:700">파티에서 나왔어요</div>' +
          '<div style="font-size:15px;line-height:1.6">이제 이 탭(창)을 닫으면 돼요.<br>브라우저가 게임이 창을 직접 닫는 걸 막고 있어요.</div>' +
          '<button style="margin-top:8px;padding:12px 26px;font-size:16px;border:0;border-radius:14px;background:#c0392b;color:#fff;font-weight:700">다시 들어가기</button>';
        d.querySelector('button').onclick = function () { location.reload(); };
        document.body.appendChild(d);
      }, 350);
    }, 250);
  },
  AcWeb_IsMobile: function () {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent)) ? 1 : 0;
  },
});
