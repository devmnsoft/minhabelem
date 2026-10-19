const cacheName = 'minhabelem-shell-24';
const shell = ['/css/site.css', '/css/amazonico.css', '/css/components.css', '/css/agenda.css', '/css/evolution.css', '/js/site.js', '/js/evolution.js', '/js/saved.js', '/manifest.webmanifest', '/images/favicon.svg'];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(cacheName).then(cache => cache.addAll(shell)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key !== cacheName).map(key => caches.delete(key)))).then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return;
  if (url.pathname.startsWith('/admin') || url.pathname.startsWith('/conta') || url.pathname.startsWith('/editorial')) return;
  if (url.pathname.startsWith('/api/v1/agenda')) {
    event.respondWith(fetch(event.request).then(response => {
      const copy = response.clone();
      caches.open(cacheName).then(cache => cache.put(event.request, copy));
      return response;
    }).catch(() => caches.match(event.request)));
    return;
  }
  if (shell.includes(url.pathname)) {
    event.respondWith(caches.match(event.request).then(hit => hit || fetch(event.request)));
  }
});
