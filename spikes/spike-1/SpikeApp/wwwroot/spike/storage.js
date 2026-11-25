// Engine-neutral storage interface of the spike (plan section 2), IndexedDB implementation.
// Five operations: append a batch of envelopes atomically; read envelopes after a sequence number in chunks;
// put/get a snapshot; put/get index chunks; put/get a draft. Durability hint per transaction ('relaxed' | 'strict' | 'default').

const STORES = ['events', 'snapshots', 'index', 'drafts', 'meta'];

function req(r) {
  return new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
}

function done(tx) {
  return new Promise((resolve, reject) => {
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error || new Error('aborted'));
  });
}

export class IdbStore {
  constructor(db, durability) { this.db = db; this.durability = durability || 'default'; }

  static open(name, durability) {
    return new Promise((resolve, reject) => {
      const r = indexedDB.open(name, 1);
      r.onupgradeneeded = () => {
        const db = r.result;
        db.createObjectStore('events', { keyPath: 'seq', autoIncrement: true });
        for (const s of STORES.slice(1)) db.createObjectStore(s);
      };
      r.onsuccess = () => resolve(new IdbStore(r.result, durability));
      r.onerror = () => reject(r.error);
      r.onblocked = () => reject(new Error('open blocked'));
    });
  }

  static delete(name) {
    return new Promise((resolve, reject) => {
      const r = indexedDB.deleteDatabase(name);
      r.onsuccess = () => resolve(); r.onerror = () => reject(r.error); r.onblocked = () => resolve();
    });
  }

  close() { this.db.close(); }

  tx(stores, mode) {
    return mode === 'readwrite' && this.durability !== 'default'
      ? this.db.transaction(stores, mode, { durability: this.durability })
      : this.db.transaction(stores, mode);
  }

  /** Appends all records in one transaction; resolves on 'complete' (the acknowledgement), else nothing is stored. */
  appendBatch(records) {
    const tx = this.tx(['events'], 'readwrite');
    const os = tx.objectStore('events');
    const finished = done(tx);
    try { for (const r of records) os.add(r); } catch (e) { tx.abort(); finished.catch(() => {}); return Promise.reject(e); }
    return finished;
  }

  /** Up to `count` envelopes with sequence number greater than `afterSeq`, in sequence order. */
  readAfter(afterSeq, count) {
    const tx = this.tx(['events'], 'readonly');
    return req(tx.objectStore('events').getAll(IDBKeyRange.lowerBound(afterSeq, true), count));
  }

  countEvents() { return req(this.tx(['events'], 'readonly').objectStore('events').count()); }

  async put(store, key, value) {
    const tx = this.tx([store], 'readwrite');
    const finished = done(tx);
    tx.objectStore(store).put(value, key);
    return finished;
  }

  get(store, key) { return req(this.tx([store], 'readonly').objectStore(store).get(key)); }

  /** Replaces all index chunks in one transaction. */
  async putIndexChunks(chunks) {
    const tx = this.tx(['index'], 'readwrite');
    const finished = done(tx);
    const os = tx.objectStore('index');
    os.clear();
    chunks.forEach((c, i) => os.put(c, i));
    return finished;
  }

  getIndexChunks() { return req(this.tx(['index'], 'readonly').objectStore('index').getAll()); }

  putSnapshot(value) { return this.put('snapshots', 'latest', value); }
  getSnapshot() { return this.get('snapshots', 'latest'); }
  putDraft(itemId, value) { return this.put('drafts', itemId, value); }
  getDraft(itemId) { return this.get('drafts', itemId); }
}
