(() => {
  "use strict";
  const DB = "rescue-checklist-v1";
  const user = document.body.dataset.user || "";
  let dbPromise;
  function openDb() {
    if (!dbPromise) dbPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB, 1);
      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains("drafts")) db.createObjectStore("drafts", { keyPath: "clientSubmissionId" });
        if (!db.objectStoreNames.contains("tasks")) db.createObjectStore("tasks", { keyPath: "user" });
      };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    return dbPromise;
  }
  async function store(name, mode, operation) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(name, mode);
      const request = operation(tx.objectStore(name));
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }
  const allDrafts = () => store("drafts", "readonly", s => s.getAll());
  const putDraft = value => store("drafts", "readwrite", s => s.put(value));
  const deleteDraft = id => store("drafts", "readwrite", s => s.delete(id));
  async function updateStatus(message) {
    const status = document.getElementById("connection-status");
    if (status) status.textContent = message || (navigator.onLine ? "Online" : "Offline — draf tersimpan di perangkat");
    const count = document.getElementById("draft-count");
    if (count && user) count.textContent = `${(await allDrafts()).filter(d => d.user === user).length} draf tertunda`;
  }
  function token() { return document.querySelector('#csrf-form input[name="__RequestVerificationToken"], #inspection-form input[name="__RequestVerificationToken"]')?.value || ""; }
  async function syncDrafts() {
    if (!user || !navigator.onLine) return updateStatus();
    const drafts = (await allDrafts()).filter(d => d.user === user);
    let failed = 0;
    let firstError = "";
    for (const draft of drafts) {
      try {
        const { user: owner, lastError, ...payload } = draft;
        const response = await fetch("/api/inspections/sync", { method: "POST", headers: { "Content-Type": "application/json", "RequestVerificationToken": token() }, credentials: "same-origin", body: JSON.stringify(payload) });
        if (!response.ok) {
          const error = await response.json().catch(() => ({}));
          draft.lastError = error.error || (response.status === 401 ? "Sesi berakhir. Masuk kembali." : response.status === 403 ? "Akun tidak diizinkan mengirim inspeksi. Hubungi admin." : `Gagal (HTTP ${response.status}).`);
          await putDraft(draft); failed++; firstError ||= draft.lastError;
        } else await deleteDraft(draft.clientSubmissionId);
      } catch (e) { draft.lastError = "Koneksi terputus. Coba sinkronkan kembali."; await putDraft(draft); failed++; firstError ||= draft.lastError; }
    }
    await updateStatus(failed ? `${failed} draf gagal: ${firstError}` : drafts.length ? "Semua draf tersinkron" : undefined);
    return { sent: drafts.length - failed, failed };
  }
  function dataFromForm(form) {
    const get = name => form.elements.namedItem(name)?.value || "";
    const answers = [];
    for (let i = 0; form.elements.namedItem(`Answers[${i}].ChecklistItemId`); i++) {
      const checked = form.querySelector(`input[name="Answers[${i}].Result"]:checked`);
      if (checked) answers.push({ checklistItemId: Number(get(`Answers[${i}].ChecklistItemId`)), result: checked.value, note: get(`Answers[${i}].Note`).trim() });
    }
    return { user, clientSubmissionId: get("ClientSubmissionId"), equipmentId: Number(get("EquipmentId")), checklistTemplateId: Number(get("ChecklistTemplateId")), templateVersion: Number(get("TemplateVersion")), startedAt: get("StartedAt"), answers };
  }
  async function setupForm() {
    const form = document.getElementById("inspection-form");
    if (!form) return;
    const equipment = Number(form.dataset.equipment);
    const draft = (await allDrafts()).filter(x => x.user === user && x.equipmentId === equipment).at(-1);
    if (draft) {
      form.elements.namedItem("ClientSubmissionId").value = draft.clientSubmissionId;
      draft.answers.forEach((a, i) => {
        const radio = form.querySelector(`input[name="Answers[${i}].Result"][value="${a.result}"]`);
        if (radio) radio.checked = true;
        const note = form.elements.namedItem(`Answers[${i}].Note`);
        if (note) note.value = a.note || "";
      });
      document.getElementById("draft-message").textContent = draft.lastError || "Draf lokal dipulihkan.";
    }
    const validate = () => {
      for (const field of form.querySelectorAll("fieldset")) {
        const checked = field.querySelector('input[type="radio"]:checked');
        const note = field.querySelector("textarea");
        if (!checked) { field.querySelector('input[type="radio"]').focus(); return "Semua butir wajib dijawab."; }
        if (checked.value === "PerluTindakan" && !note.value.trim()) { note.focus(); return "Catatan wajib untuk kondisi Perlu tindakan."; }
      }
      return "";
    };
    document.getElementById("save-draft").addEventListener("click", async () => {
      await putDraft(dataFromForm(form));
      document.getElementById("draft-message").textContent = "Draf tersimpan di perangkat. Belum tersinkron.";
      await updateStatus();
    });
    form.addEventListener("submit", async event => {
      event.preventDefault();
      const error = validate();
      if (error) { document.getElementById("draft-message").textContent = error; return; }
      const draft = dataFromForm(form);
      await putDraft(draft);
      await updateStatus("Mengirim inspeksi…");
      if (!navigator.onLine) { document.getElementById("draft-message").textContent = "Offline. Draf akan dikirim saat online."; return; }
      const result = await syncDrafts();
      const current = (await allDrafts()).find(x => x.clientSubmissionId === draft.clientSubmissionId);
      if (current) document.getElementById("draft-message").textContent = current.lastError || "Draf belum terkirim.";
      else if (result?.sent) location.href = "/Inspections";
    });
  }
  async function downloadTasks() {
    if (!user || !navigator.onLine) return;
    try {
      const response = await fetch("/api/offline/tasks", { credentials: "same-origin", cache: "no-store" });
      if (response.ok) {
        const task = await response.json();
        await store("tasks", "readwrite", s => s.put(task));
      }
    } catch (_) { /* A previous task copy is still available. */ }
  }
  async function clearLocal() {
    await store("drafts", "readwrite", s => s.clear());
    await store("tasks", "readwrite", s => s.clear());
  }
  document.addEventListener("DOMContentLoaded", async () => {
    if ("serviceWorker" in navigator) navigator.serviceWorker.register("/sw.js").catch(() => {});
    const logout = document.getElementById("logout-button");
    if (logout) logout.addEventListener("click", async () => {
      if (!confirm("Keluar dan hapus semua draf lokal di perangkat ini?")) return;
      await clearLocal(); document.getElementById("logout-form").submit();
    });
    document.getElementById("sync-now")?.addEventListener("click", syncDrafts);
    window.addEventListener("online", () => { updateStatus(); downloadTasks(); syncDrafts(); });
    window.addEventListener("offline", () => updateStatus());
    await updateStatus();
    await setupForm();
    await downloadTasks();
    if (user && navigator.onLine) await syncDrafts();
  });
})();
