// Starts Blazor (index.html loads blazor.webassembly.js with autostart="false"). On-device cold runs (cold=1): every
// boot resource is fetched with cache "no-store", so no HTTP cache and no compiled-code cache entry is used; the
// browser process and the operating system's file cache stay warm (no adb, no forced restart). In device mode the
// screen is kept on while the run lasts.
const params = new URLSearchParams(location.search);
const cold = params.get('cold') === '1';
if (params.get('device') === '1' && navigator.wakeLock) navigator.wakeLock.request('screen').catch(() => {});
Blazor.start(cold ? {
  loadBootResource(type, name, defaultUri, integrity) {
    if (type === 'dotnetjs') return defaultUri;
    return fetch(defaultUri, { cache: 'no-store', integrity, credentials: 'omit' });
  },
} : {});
