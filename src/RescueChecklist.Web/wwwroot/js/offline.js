(() => {
  window.addEventListener("online", () => { location.href = "/"; });
  const message = document.getElementById("offline-message");
  const app = document.getElementById("offline-app");
  const draftArea = document.getElementById("offline-drafts");
  const request = indexedDB.open("rescue-checklist-v1", 1);
  request.onupgradeneeded = () => { const db = request.result; if (!db.objectStoreNames.contains("drafts")) db.createObjectStore("drafts", { keyPath: "clientSubmissionId" }); if (!db.objectStoreNames.contains("tasks")) db.createObjectStore("tasks", { keyPath: "user" }); };
  request.onerror = () => message.textContent = "Data lokal tidak dapat dibuka.";
  request.onsuccess = () => {
    const db = request.result;
    const tx = db.transaction("tasks", "readonly");
    const get = tx.objectStore("tasks").getAll();
    get.onsuccess = () => {
      const tasks = get.result;
      if (!tasks.length) { message.textContent = "Belum ada data tugas. Masuk dan buka aplikasi sekali saat online."; return; }
      const task = tasks.at(-1);
      if (!task.template || !task.equipment.length) { message.textContent = "Tidak ada tugas yang dapat diisi."; return; }
      message.textContent = `Offline · data tugas untuk ${task.user}. Draf belum tersinkron.`;
      function showDrafts() {
        const list = db.transaction("drafts", "readonly").objectStore("drafts").getAll();
        list.onsuccess = () => {
          const own = list.result.filter(d => d.user === task.user);
          draftArea.replaceChildren();
          if (!own.length) return;
          const panel = document.createElement("section"); panel.className = "surface";
          const title = document.createElement("h2"); title.textContent = `${own.length} draf belum tersinkron`; panel.append(title);
          own.forEach(d => {
            const p = document.createElement("p");
            const equipment = task.equipment.find(e => e.id === d.equipmentId);
            p.textContent = `${equipment?.name || "Peralatan"} · ${d.answers.length} jawaban${d.lastError ? ` · ${d.lastError}` : ""}`;
            panel.append(p);
          });
          draftArea.append(panel);
        };
      }
      showDrafts();
      const select = document.createElement("select"); select.className = "form-select"; select.setAttribute("aria-label", "Pilih peralatan");
      task.equipment.forEach(e => { const option = new Option(`${e.code} · ${e.name} — ${e.buildingName}`, e.id); select.add(option); });
      const form = document.createElement("form"); form.className = "inspection-form";
      const box = document.createElement("div"); box.className = "surface";
      const heading = document.createElement("h2"); heading.textContent = "Pilih peralatan"; box.append(heading, select); form.append(box);
      const fields = [];
      task.template.items.forEach((item, index) => {
        const field = document.createElement("fieldset"); field.className = "checklist-item";
        const legend = document.createElement("legend"); legend.textContent = `${index + 1}. ${item.question}`; field.append(legend);
        const choices = document.createElement("div"); choices.className = "choice-row";
        ["Baik", "PerluTindakan", "TidakBerlaku"].forEach((value) => {
          const label = document.createElement("label"); const radio = document.createElement("input"); radio.type = "radio"; radio.name = `result-${index}`; radio.value = value;
          const span = document.createElement("span"); span.textContent = value === "PerluTindakan" ? "Perlu tindakan" : value === "TidakBerlaku" ? "Tidak berlaku" : value;
          label.append(radio, span); choices.append(label);
        }); field.append(choices);
        const noteLabel = document.createElement("label"); noteLabel.className = "form-label mt-3"; noteLabel.textContent = "Catatan";
        const note = document.createElement("textarea"); note.className = "form-control"; note.rows = 2; note.maxLength = 1000; noteLabel.append(note); field.append(noteLabel); fields.push({ item, field, note }); form.append(field);
      });
      const button = document.createElement("button"); button.type = "submit"; button.className = "btn btn-accent"; button.textContent = "Simpan draf lokal"; form.append(button);
      form.addEventListener("submit", event => {
        event.preventDefault();
        const answers = [];
        for (const { item, field, note } of fields) {
          const checked = field.querySelector("input:checked");
          if (!checked) { message.textContent = "Semua butir wajib dijawab."; field.querySelector("input").focus(); return; }
          if (checked.value === "PerluTindakan" && !note.value.trim()) { message.textContent = "Catatan wajib untuk temuan."; note.focus(); return; }
          answers.push({ checklistItemId: item.id, result: checked.value, note: note.value.trim() });
        }
        const draft = { user: task.user, clientSubmissionId: crypto.randomUUID(), equipmentId: Number(select.value), checklistTemplateId: task.template.id, templateVersion: task.template.version, startedAt: new Date().toISOString(), answers };
        const put = db.transaction("drafts", "readwrite").objectStore("drafts").put(draft);
        put.onsuccess = () => { message.textContent = "Draf tersimpan di perangkat. Buka aplikasi saat online untuk sinkronisasi."; form.reset(); showDrafts(); };
        put.onerror = () => message.textContent = "Gagal menyimpan draf lokal.";
      });
      app.append(form);
    };
  };
})();
