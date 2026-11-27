// 앱 설치용 서비스 워커: 저장(캐시)은 하지 않고 늘 네트워크에서 새로 받음 → 새 버전이 바로 반영됨
self.addEventListener("install", function () { self.skipWaiting(); });
self.addEventListener("activate", function (e) { e.waitUntil(self.clients.claim()); });
self.addEventListener("fetch", function (e) {
  if (e.request.method !== "GET") return;
  e.respondWith(fetch(e.request).catch(function () { return new Response("", { status: 503 }); }));
});
