const CACHE = "rescue-shell-v2";
const SHELL = ["/offline.html", "/css/site.css", "/lib/bootstrap/bootstrap.min.css", "/js/site.js", "/js/offline.js", "/icons/icon.svg", "/icons/icon-192.png", "/icons/icon-512.png", "/manifest.webmanifest"];
self.addEventListener("install", event => { event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(SHELL))); self.skipWaiting(); });
self.addEventListener("activate", event => { event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))); self.clients.claim(); });
self.addEventListener("fetch", event => {
  const request = event.request;
  if (request.method !== "GET" || new URL(request.url).origin !== self.location.origin) return;
  if (request.mode === "navigate") { event.respondWith(fetch(request).catch(() => caches.match("/offline.html"))); return; }
  const path = new URL(request.url).pathname;
  if (SHELL.includes(path)) event.respondWith(caches.match(path).then(hit => hit || fetch(request)));
});
