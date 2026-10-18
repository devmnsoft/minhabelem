document.getElementById('share')?.addEventListener('click', async () => {
  const status = document.getElementById('share-status');
  try {
    if (navigator.share) await navigator.share({ title: document.title, url: location.href });
    else if (navigator.clipboard) { await navigator.clipboard.writeText(location.href); status.textContent = 'Link copiado.'; }
    else status.textContent = location.href;
  } catch (error) { if (error.name !== 'AbortError') status.textContent = 'Copie o endereço desta página para compartilhar.'; }
});

document.querySelector('.menu-toggle')?.addEventListener('click', event => {
  const button = event.currentTarget;
  const open = button.getAttribute('aria-expanded') !== 'true';
  button.setAttribute('aria-expanded', String(open));
  document.getElementById('main-navigation')?.classList.toggle('is-open', open);
});

document.addEventListener('keydown', event => {
  if (event.key === 'Escape') {
    document.querySelector('.main-nav.is-open')?.classList.remove('is-open');
    document.querySelector('.menu-toggle')?.setAttribute('aria-expanded', 'false');
  }
});

const connectionNotice = document.querySelector('[data-connection-status]');
function showConnection(offline) {
  if (!connectionNotice) return;
  connectionNotice.textContent = offline ? connectionNotice.dataset.offline : connectionNotice.dataset.online;
  connectionNotice.hidden = false;
}
window.addEventListener('offline', () => showConnection(true));
window.addEventListener('online', () => showConnection(false));
if (!navigator.onLine) showConnection(true);
document.querySelectorAll('form[method="post"]').forEach(form => {
  form.addEventListener('submit', event => {
    if (event.defaultPrevented) return;
    if (!navigator.onLine) { event.preventDefault(); showConnection(true); return; }
    if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
    // Do not disable controls: submitter names/values and antiforgery must reach the server.
    form.dataset.submitting = 'true';
    form.setAttribute('aria-busy', 'true');
    document.documentElement.dataset.navigating = 'true';
  });
});
window.addEventListener('pageshow', () => {
  delete document.documentElement.dataset.navigating;
  document.querySelectorAll('form[data-submitting]').forEach(form => {
    delete form.dataset.submitting;
    form.removeAttribute('aria-busy');
  });
});

document.querySelectorAll('table').forEach(table => {
  if (table.closest('.table-scroll')) return;
  const region = document.createElement('div');
  region.className = 'table-scroll';
  region.tabIndex = 0;
  region.setAttribute('role', 'region');
  region.setAttribute('aria-label', table.querySelector('caption')?.textContent || document.querySelector('h1')?.textContent || document.title);
  table.before(region);
  region.append(table);
});
