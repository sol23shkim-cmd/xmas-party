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
  AcWeb_Reload: function () {   // 게임 나가기: 스트림 닫고 처음 화면으로 (떠날 때 내 자리는 pagehide에서 지움)
    try { if (window._acStream) window._acStream.close(); } catch (e) {}
    setTimeout(function () { location.reload(); }, 200);
  },
  AcWeb_IsMobile: function () {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent)) ? 1 : 0;
  },
});
