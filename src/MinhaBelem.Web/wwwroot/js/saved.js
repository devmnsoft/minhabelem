(() => {
  const list = document.getElementById('saved-list');
  if (!list) return;
  const empty = document.getElementById('saved-empty');
  const alerts = document.getElementById('saved-alerts');
  let ids = [];
  try {
    const data = JSON.parse(localStorage.getItem('minhabelem.agenda.saved') || '[]');
    if (Array.isArray(data)) ids = data.filter(id => /^\d+$/.test(id));
  } catch { ids = []; }
  if (ids.length === 0) { empty.hidden = false; return; }
  fetch('/api/v1/agenda?dias=365').then(response => response.json()).then(rows => {
    const byId = new Map(rows.map(row => [String(row.id), row]));
    for (const id of ids) {
      const row = byId.get(id);
      const card = document.createElement('article');
      card.className = 'card';
      const title = document.createElement('h2');
      const link = document.createElement('a');
      link.href = '/Home/Detalhe/' + id;
      link.textContent = row ? row.title : 'Evento ' + id;
      title.append(link);
      card.append(title);
      if (row && row.status && row.status !== 'Published') {
        const note = document.createElement('p');
        note.className = 'notice';
        note.textContent = 'Situação atual: ' + row.status;
        card.append(note);
      }
      list.append(card);
    }
  }).catch(() => { empty.hidden = false; empty.textContent = 'Não foi possível carregar a agenda agora. Seus planos continuam neste aparelho.'; });
  if (alerts && window.minhaBelemChanges) {
    window.minhaBelemChanges.then(changes => {
      if (!changes.length) return;
      alerts.hidden = false;
      alerts.textContent = changes.map(change => change.title + ' (' + change.status + ')').join(' · ');
    });
  }
})();
