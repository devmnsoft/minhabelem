(() => {
  const button = document.querySelector('[data-share-itinerary]');
  if (!button) return;
  button.addEventListener('click', async () => {
    // Only public stop names, public portal links and rendered times enter the payload.
    const stops = [...document.querySelectorAll('.journey-timeline li')].map(stop => {
      const link = stop.querySelector('h3 a');
      if (!link) return '';
      const url = new URL(link.getAttribute('href'), location.origin);
      if (url.origin !== location.origin || !/^\/guia\/\d+$/.test(url.pathname)) return '';
      return `${stop.closest('section')?.querySelector('h2')?.textContent.trim() || ''}\n${link.textContent.trim()} · ${stop.querySelector('p')?.textContent.trim() || ''}\n${url.href}`;
    }).filter(Boolean);
    const text = `${document.querySelector('h1')?.textContent.trim() || 'MinhaBelém'}\n\n${stops.join('\n\n')}`;
    const status = document.querySelector('[data-itinerary-share-status]');
    try {
      if (navigator.share) await navigator.share({ text });
      else { await navigator.clipboard.writeText(text); status.textContent = button.dataset.copied; }
    } catch (error) { if (error.name !== 'AbortError') status.textContent = button.dataset.failed; }
  });
})();
