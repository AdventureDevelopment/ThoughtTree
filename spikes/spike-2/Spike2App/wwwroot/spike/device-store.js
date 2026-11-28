// State of the on-device runner (spike 2, no computer): finished runs and the run queue, in IndexedDB
// "tt-spike2-device" of the site's origin (shared by the interpreted and the AOT build).
const DB = 'tt-spike2-device';

function open() {
  return new Promise((resolve, reject) => {
    const r = indexedDB.open(DB, 1);
    r.onupgradeneeded = () => { r.result.createObjectStore('runs', { keyPath: 'id' }); r.result.createObjectStore('state'); };
    r.onsuccess = () => resolve(r.result);
    r.onerror = () => reject(r.error);
  });
}

async function run(store, mode, fn) {
  const db = await open();
  try {
    return await new Promise((resolve, reject) => {
      const tx = db.transaction(store, mode);
      const req = fn(tx.objectStore(store));
      tx.oncomplete = () => resolve(req ? req.result : undefined);
      tx.onerror = () => reject(tx.error);
    });
  } finally { db.close(); }
}

export const saveRun = (id, result) => run('runs', 'readwrite', s => s.put({ id: String(id), result, savedAt: Date.now() }));
export const getRun = (id) => run('runs', 'readonly', s => s.get(String(id)));
export const allRuns = () => run('runs', 'readonly', s => s.getAll());
export const clearRuns = () => run('runs', 'readwrite', s => s.clear());
export const getState = (key) => run('state', 'readonly', s => s.get(key));
export const setState = (key, value) => run('state', 'readwrite', s => s.put(value, key));
