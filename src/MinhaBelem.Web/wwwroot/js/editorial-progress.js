(() => {
  const progress = document.querySelector('[data-progress-url]');
  if (!progress || !['queued', 'running'].includes(progress.dataset.batchStatus)) return;
  const poll = async () => {
    if (document.hidden) { setTimeout(poll, 5000); return; }
    try {
      const response = await fetch(progress.dataset.progressUrl, { headers: { Accept: 'application/json' }, redirect: 'error', cache: 'no-store' });
      if (!response.ok) return;
      const batch = await response.json();
      progress.max = Math.max(1, batch.total);
      progress.value = batch.finished;
      progress.setAttribute('aria-valuetext', `${batch.finished}/${batch.total}`);
      const labels = JSON.parse(progress.dataset.statusLabels);
      const overall = document.querySelector('[data-overall-status]');
      if (overall) overall.textContent = labels[batch.status] || batch.status;
      const completed = document.querySelector('[data-completed-count]');
      const failed = document.querySelector('[data-failed-count]');
      if (completed) completed.textContent = batch.items.filter(item => item.status === 'completed').length;
      if (failed) failed.textContent = batch.items.filter(item => ['blocked', 'failed'].includes(item.status)).length;
      for (const item of batch.items) {
        const key = `${item.targetKind}:${item.targetId}`;
        const row = [...document.querySelectorAll('[data-batch-item]')].find(el => el.dataset.batchItem === key);
        if (row) {
          row.querySelector('[data-item-status]').textContent = labels[item.status] || item.status;
          row.querySelector('[data-item-result]').textContent = item.result || '';
        }
      }
      if (['queued', 'running'].includes(batch.status)) setTimeout(poll, 5000);
    } catch { /* Keep the last persisted result and the existing manual refresh link. */ }
  };
  setTimeout(poll, 3000);
})();
