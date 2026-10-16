document.getElementById('share')?.addEventListener('click', async () => {
  const status = document.getElementById('share-status');
  try {
    if (navigator.share) await navigator.share({ title: document.title, url: location.href });
    else if (navigator.clipboard) { await navigator.clipboard.writeText(location.href); status.textContent = 'Link copiado.'; }
    else status.textContent = location.href;
  } catch (error) { if (error.name !== 'AbortError') status.textContent = 'Copie o endereço desta página para compartilhar.'; }
});
