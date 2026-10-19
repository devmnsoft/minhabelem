document.getElementById('share')?.addEventListener('click', async () => {
  const status = document.getElementById('share-status');
  try {
    if (navigator.share) await navigator.share({ title: document.title, url: location.href });
    else if (navigator.clipboard) { await navigator.clipboard.writeText(location.href); status.textContent = 'Link copiado.'; }
    else status.textContent = location.href;
  } catch (error) { if (error.name !== 'AbortError') status.textContent = 'Copie o endereço desta página para compartilhar.'; }
});

// Shared navigation uses one state for pointer, touch and keyboard.
const mainNavigation = document.getElementById('main-navigation');
const menuButtons = [...document.querySelectorAll('.menu-toggle,.mobile-tab-more')];
let menuOpener;
function setNavigation(open, opener) {
  if (open) menuOpener = opener;
  mainNavigation?.classList.toggle('is-open', open);
  menuButtons.forEach(button => button.setAttribute('aria-expanded', String(open)));
  if (open) mainNavigation?.querySelector('a')?.focus();
}
menuButtons.forEach(button => button.addEventListener('click', () => {
  const open = !mainNavigation?.classList.contains('is-open');
  setNavigation(open, button);
  if (!open) button.focus();
}));
document.querySelectorAll('.nav-dropdown-btn').forEach(button => {
  button.addEventListener('click', () => {
    const open = button.getAttribute('aria-expanded') !== 'true';
    button.setAttribute('aria-expanded', String(open));
    button.closest('.nav-dropdown').classList.toggle('is-open', open);
  });
});
function closeDisclosures() {
  document.querySelectorAll('.nav-dropdown.is-open').forEach(menu => {
    menu.classList.remove('is-open');
    menu.querySelector('button')?.setAttribute('aria-expanded', 'false');
  });
}
document.addEventListener('click', event => {
  if (!event.target.closest('.nav-dropdown')) closeDisclosures();
  if (!event.target.closest('.header,.mobile-tab-bar')) setNavigation(false);
});
document.addEventListener('keydown', event => {
  if (event.key !== 'Escape') return;
  const disclosure = document.querySelector('.nav-dropdown.is-open');
  if (disclosure) { closeDisclosures(); disclosure.querySelector('button')?.focus(); }
  else if (mainNavigation?.classList.contains('is-open')) {
    setNavigation(false);
    menuOpener?.focus();
  }
});
mainNavigation?.addEventListener('click', event => {
  if (event.target.closest('a')) { setNavigation(false); closeDisclosures(); }
});
window.matchMedia('(min-width: 1200px)').addEventListener('change', () => {
  setNavigation(false); closeDisclosures();
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

/* ==========================================================================
   SISTEMA DE PREFERÊNCIAS DE ACESSIBILIDADE VISUAL (SPRINT 19)
   ========================================================================== */
(function() {
  const STORAGE_KEY = 'minhabelem_a11y_prefs';
  const defaultPrefs = {
    fontSize: 'default',
    spacing: 'default',
    contrast: false,
    focusHighlight: false,
    reducedMotion: false
  };

  function loadPrefs() {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      return saved ? Object.assign({}, defaultPrefs, JSON.parse(saved)) : Object.assign({}, defaultPrefs);
    } catch (e) {
      return Object.assign({}, defaultPrefs);
    }
  }

  function savePrefs(prefs) {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
    } catch (e) {}
  }

  function applyPrefs(prefs) {
    const root = document.documentElement;
    if (prefs.fontSize && prefs.fontSize !== 'default') {
      root.setAttribute('data-font-size', prefs.fontSize);
    } else {
      root.removeAttribute('data-font-size');
    }

    if (prefs.spacing && prefs.spacing !== 'default') {
      root.setAttribute('data-spacing', prefs.spacing);
    } else {
      root.removeAttribute('data-spacing');
    }

    if (prefs.contrast) {
      root.setAttribute('data-contrast', 'high');
    } else {
      root.removeAttribute('data-contrast');
    }

    if (prefs.focusHighlight) {
      root.setAttribute('data-focus-highlight', 'true');
    } else {
      root.removeAttribute('data-focus-highlight');
    }

    if (prefs.reducedMotion) {
      root.setAttribute('data-reduced-motion', 'true');
    } else {
      root.removeAttribute('data-reduced-motion');
    }

    updatePanelUI(prefs);
  }

  function updatePanelUI(prefs) {
    document.querySelectorAll('[data-a11y-font]').forEach(btn => {
      btn.setAttribute('aria-pressed', String(btn.dataset.a11yFont === prefs.fontSize));
    });
    document.querySelectorAll('[data-a11y-spacing]').forEach(btn => {
      btn.setAttribute('aria-pressed', String(btn.dataset.a11ySpacing === prefs.spacing));
    });
    const contrastChk = document.getElementById('a11y-toggle-contrast');
    if (contrastChk) contrastChk.checked = Boolean(prefs.contrast);
    const focusChk = document.getElementById('a11y-toggle-focus');
    if (focusChk) focusChk.checked = Boolean(prefs.focusHighlight);
    const motionChk = document.getElementById('a11y-toggle-motion');
    if (motionChk) motionChk.checked = Boolean(prefs.reducedMotion);
  }

  const currentPrefs = loadPrefs();
  applyPrefs(currentPrefs);

  document.addEventListener('DOMContentLoaded', () => {
    applyPrefs(currentPrefs);

    const backdrop = document.getElementById('a11y-modal-backdrop');
    const dialog = document.getElementById('a11y-dialog');
    const closeBtn = document.getElementById('close-a11y-panel');
    const applyCloseBtn = document.getElementById('a11y-apply-close-btn');
    const resetBtn = document.getElementById('a11y-reset-btn');
    let lastFocusedElement = null;

    function openPanel(triggerEl) {
      if (!backdrop) return;
      lastFocusedElement = triggerEl || document.activeElement;
      backdrop.classList.add('is-open');
      backdrop.setAttribute('aria-hidden', 'false');
      [...document.body.children].filter(el => el !== backdrop && el.tagName !== 'SCRIPT').forEach(el => {
        if (!el.inert) { el.inert = true; el.dataset.a11yInert = 'true'; }
      });
      if (triggerEl) triggerEl.setAttribute('aria-expanded', 'true');
      if (dialog) dialog.focus();
      document.body.style.overflow = 'hidden';
    }

    function closePanel() {
      if (!backdrop) return;
      backdrop.classList.remove('is-open');
      backdrop.setAttribute('aria-hidden', 'true');
      document.querySelectorAll('[data-a11y-inert]').forEach(el => { el.inert = false; delete el.dataset.a11yInert; });
      document.querySelectorAll('[data-open-a11y]').forEach(btn => btn.setAttribute('aria-expanded', 'false'));
      document.body.style.overflow = '';
      if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
        lastFocusedElement.focus();
      }
    }

    document.querySelectorAll('[data-open-a11y]').forEach(btn => {
      btn.addEventListener('click', e => {
        e.preventDefault();
        openPanel(btn);
      });
    });

    closeBtn?.addEventListener('click', closePanel);
    applyCloseBtn?.addEventListener('click', closePanel);

    backdrop?.addEventListener('click', e => {
      if (e.target === backdrop) closePanel();
    });

    document.addEventListener('keydown', e => {
      if (e.key === 'Tab' && backdrop?.classList.contains('is-open')) {
        const controls = [...dialog.querySelectorAll('button,input,select,a[href]')].filter(el => el.getClientRects().length);
        const first = controls[0], last = controls[controls.length - 1];
        if (e.shiftKey && (document.activeElement === first || document.activeElement === dialog)) { e.preventDefault(); last?.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first?.focus(); }
      }
      if (e.key === 'Escape' && backdrop?.classList.contains('is-open')) {
        closePanel();
      }
    });

    // Font buttons
    document.querySelectorAll('[data-a11y-font]').forEach(btn => {
      btn.addEventListener('click', () => {
        currentPrefs.fontSize = btn.dataset.a11yFont;
        savePrefs(currentPrefs);
        applyPrefs(currentPrefs);
      });
    });

    // Spacing buttons
    document.querySelectorAll('[data-a11y-spacing]').forEach(btn => {
      btn.addEventListener('click', () => {
        currentPrefs.spacing = btn.dataset.a11ySpacing;
        savePrefs(currentPrefs);
        applyPrefs(currentPrefs);
      });
    });

    // Contrast toggle
    document.getElementById('a11y-toggle-contrast')?.addEventListener('change', e => {
      currentPrefs.contrast = e.target.checked;
      savePrefs(currentPrefs);
      applyPrefs(currentPrefs);
    });

    // Focus highlight toggle
    document.getElementById('a11y-toggle-focus')?.addEventListener('change', e => {
      currentPrefs.focusHighlight = e.target.checked;
      savePrefs(currentPrefs);
      applyPrefs(currentPrefs);
    });

    // Reduced motion toggle
    document.getElementById('a11y-toggle-motion')?.addEventListener('change', e => {
      currentPrefs.reducedMotion = e.target.checked;
      savePrefs(currentPrefs);
      applyPrefs(currentPrefs);
    });

    // Reset button
    resetBtn?.addEventListener('click', () => {
      Object.assign(currentPrefs, defaultPrefs);
      savePrefs(currentPrefs);
      applyPrefs(currentPrefs);
    });
  });
})();

(() => {
  const storageKey = 'minhabelem.device';
  function deviceKey() {
    let key = localStorage.getItem(storageKey);
    if (!/^[a-f0-9]{64}$/.test(key || '')) {
      const bytes = new Uint8Array(32);
      crypto.getRandomValues(bytes);
      key = [...bytes].map(byte => byte.toString(16).padStart(2, '0')).join('');
      localStorage.setItem(storageKey, key);
    }
    return key;
  }
  function savedIds() {
    try {
      const data = JSON.parse(localStorage.getItem('minhabelem.agenda.saved') || '[]');
      return Array.isArray(data) ? data.filter(id => /^\d+$/.test(id)).slice(0, 200).map(Number) : [];
    } catch { return []; }
  }
  window.minhaBelemSyncSaves = function () {
    const ids = savedIds();
    window.minhaBelemChanges = fetch('/api/v1/salvos', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ device: deviceKey(), events: ids })
    }).then(response => response.ok ? response.json() : { changes: [] }).then(body => body.changes || []).catch(() => []);
    return window.minhaBelemChanges;
  };
  document.querySelector('[data-near-me]')?.addEventListener('click', () => {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(position => {
      const url = new URL(location.href);
      url.searchParams.set('lat', position.coords.latitude.toFixed(5));
      url.searchParams.set('lng', position.coords.longitude.toFixed(5));
      url.searchParams.set('raio', '3');
      location.href = url.pathname + url.search;
    });
  });
  if ('serviceWorker' in navigator && !location.pathname.startsWith('/admin') && !location.pathname.startsWith('/conta') && !location.pathname.startsWith('/editorial')) {
    navigator.serviceWorker.register('/sw.js').catch(() => {});
  }
  if (savedIds().length) window.minhaBelemSyncSaves();
})();
