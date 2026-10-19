/* ==========================================================================
   MINHABELÉM 360 — EVOLUÇÃO JS 2026
   Widget IA flutuante • Lightbox fotos/vídeo • Modo escuro • Mobile menu
   Upload de fotos • Lazy loading • Controles de vídeo
   ========================================================================== */

(function () {
  'use strict';

  /* ---------- Tema (Claro/Escuro) ---------- */
  const THEME_KEY = 'minhabelem_theme';
  const htmlEl = document.documentElement;
  const themeToggle = document.querySelector('.theme-toggle');

  function getSavedTheme() {
    try { return localStorage.getItem(THEME_KEY); } catch (e) { return null; }
  }
  function setTheme(theme) {
    if (theme === 'dark') {
      htmlEl.setAttribute('data-theme', 'dark');
      document.body.classList.add('dark-theme');
    } else {
      htmlEl.setAttribute('data-theme', 'light');
      document.body.classList.remove('dark-theme');
    }
    try { localStorage.setItem(THEME_KEY, theme); } catch (e) {}
  }
  function toggleTheme() {
    const isDark = htmlEl.getAttribute('data-theme') === 'dark' || document.body.classList.contains('dark-theme');
    setTheme(isDark ? 'light' : 'dark');
    if (themeToggle) {
      themeToggle.setAttribute('aria-pressed', String(!isDark));
      themeToggle.innerHTML = isDark ? '☀️' : '🌙';
    }
  }

  document.addEventListener('DOMContentLoaded', function () {
    const saved = getSavedTheme();
    if (saved === 'dark' || (!saved && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
      setTheme('dark');
    } else {
      setTheme(saved || 'light');
    }
    if (themeToggle) {
      const isDark = htmlEl.getAttribute('data-theme') === 'dark' || document.body.classList.contains('dark-theme');
      themeToggle.setAttribute('aria-pressed', String(isDark));
      themeToggle.innerHTML = isDark ? '☀️' : '🌙';
      themeToggle.addEventListener('click', toggleTheme);
      themeToggle.title = isDark ? 'Alternar para modo claro' : 'Alternar para modo escuro';
    }
  });

  /* ---------- Header scroll ---------- */
  const header = document.querySelector('.header');
  if (header) {
    let ticking = false;
    window.addEventListener('scroll', function () {
      if (!ticking) {
        window.requestAnimationFrame(function () {
          if (window.scrollY > 20) header.classList.add('scrolled');
          else header.classList.remove('scrolled');
          ticking = false;
        });
        ticking = true;
      }
    });
  }

  /* ---------- Progresso de leitura ---------- */
  const progress = document.querySelector('.page-progress');
  if (progress) {
    window.addEventListener('scroll', function () {
      const winScroll = document.body.scrollTop || document.documentElement.scrollTop;
      const height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
      const scrolled = height ? (winScroll / height) * 100 : 0;
      progress.style.width = scrolled + '%';
    });
  }

  /* ---------- Lightbox (fotos e vídeos) ---------- */
  const lightbox = document.getElementById('mb-lightbox');
  const lightboxContent = document.getElementById('mb-lightbox-content');
  const lightboxCaption = document.getElementById('mb-lightbox-caption');
  const lightboxClose = document.getElementById('mb-lightbox-close');
  const lightboxPrev = document.getElementById('mb-lightbox-prev');
  const lightboxNext = document.getElementById('mb-lightbox-next');

  let galleryItems = [];
  let currentIndex = 0;
  let lightboxOpener;

  function openLightbox(items, index) {
    if (!lightbox) return;
    lightboxOpener = document.activeElement;
    galleryItems = items;
    currentIndex = Math.max(0, Math.min(index, items.length - 1));
    renderLightboxItem();
    lightbox.classList.add('is-open');
    document.body.style.overflow = 'hidden';
    lightbox.setAttribute('aria-hidden', 'false');
    [...document.body.children].filter(el => el !== lightbox && el.tagName !== 'SCRIPT').forEach(el => {
      if (!el.inert) { el.inert = true; el.dataset.lightboxInert = 'true'; }
    });
    lightboxClose?.focus();
  }
  function closeLightbox() {
    if (!lightbox) return;
    lightbox.classList.remove('is-open');
    document.body.style.overflow = '';
    lightbox.setAttribute('aria-hidden', 'true');
    document.querySelectorAll('[data-lightbox-inert]').forEach(el => { el.inert = false; delete el.dataset.lightboxInert; });
    lightboxOpener?.focus();
    if (lightboxContent) lightboxContent.innerHTML = '';
    if (lightboxCaption) lightboxCaption.textContent = '';
  }
  function renderLightboxItem() {
    if (!lightboxContent) return;
    const item = galleryItems[currentIndex];
    if (!item) return;
    if (lightboxCaption) lightboxCaption.textContent = item.caption || '';
    let media = '';
    if (item.type === 'video') {
      media = '<video controls playsinline poster="' + (item.poster || '') + '" src="' + item.src + '" alt="' + (item.caption || '') + '"></video>';
    } else {
      media = '<img src="' + item.src + '" alt="' + (item.caption || '') + '" />';
    }
    lightboxContent.innerHTML =
      '<div class="mb-lightbox-content-inner" style="max-width:90vw;max-height:85vh;overflow:auto;border-radius:16px;box-shadow:0 24px 56px rgba(0,0,0,0.5);">' +
      media +
      '</div>';
    if (lightboxPrev) lightboxPrev.style.display = galleryItems.length > 1 ? 'flex' : 'none';
    if (lightboxNext) lightboxNext.style.display = galleryItems.length > 1 ? 'flex' : 'none';
  }
  function nextLightbox() { currentIndex = (currentIndex + 1) % galleryItems.length; renderLightboxItem(); }
  function prevLightbox() { currentIndex = (currentIndex - 1 + galleryItems.length) % galleryItems.length; renderLightboxItem(); }

  if (lightboxClose) lightboxClose.addEventListener('click', closeLightbox);
  if (lightboxPrev) lightboxPrev.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); prevLightbox(); });
  if (lightboxNext) lightboxNext.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); nextLightbox(); });
  if (lightbox) {
    lightbox.addEventListener('click', function (e) {
      if (e.target === lightbox) closeLightbox();
    });
    document.addEventListener('keydown', function (e) {
      if (!lightbox.classList.contains('is-open')) return;
      if (e.key === 'Escape') closeLightbox();
      if (e.key === 'ArrowRight') nextLightbox();
      if (e.key === 'ArrowLeft') prevLightbox();
      if (e.key === 'Tab') {
        const controls = [...lightbox.querySelectorAll('button,video')].filter(el => el.getClientRects().length);
        const first = controls[0], last = controls[controls.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last?.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first?.focus(); }
      }
    });
  }

  document.querySelectorAll('[data-lightbox]').forEach(function (el) {
    el.addEventListener('click', function (e) {
      e.preventDefault();
      const group = el.getAttribute('data-lightbox');
      const items = Array.from(document.querySelectorAll('[data-lightbox="' + group + '"]')).map(function (item, i) {
        return {
          src: item.getAttribute('data-src') || item.querySelector('img')?.src || item.href,
          type: item.getAttribute('data-type') || 'image',
          caption: item.getAttribute('data-caption') || item.querySelector('figcaption')?.textContent || '',
          poster: item.getAttribute('data-poster') || ''
        };
      });
      const index = items.findIndex(function (it) { return it.src === (el.getAttribute('data-src') || el.querySelector('img')?.src || el.href); });
      openLightbox(items, index >= 0 ? index : 0);
    });
  });

  /* ---------- Upload de fotos (drag & drop) ---------- */
  const dropZones = document.querySelectorAll('.photo-upload-zone');
  dropZones.forEach(function (zone) {
    const input = zone.querySelector('input[type="file"]');
    if (!input) return;

    zone.addEventListener('click', function (event) {
      // Não duplica a ação quando o clique veio do <label> ou do próprio input.
      const target = event.target;
      if (target && (target.tagName === 'INPUT' || target.tagName === 'LABEL' || target.closest('label'))) return;
      input.click();
    });

    zone.addEventListener('dragover', function (e) { e.preventDefault(); zone.classList.add('drag-over'); });
    zone.addEventListener('dragleave', function () { zone.classList.remove('drag-over'); });
    zone.addEventListener('drop', function (e) {
      e.preventDefault();
      zone.classList.remove('drag-over');
      const files = Array.from(e.dataTransfer.files).filter(function (f) { return f.type.startsWith('image/'); });
      if (files.length) input.files = combineFiles(input.files, files);
      renderPreviews(zone, input.files);
    });

    input.addEventListener('change', function () { renderPreviews(zone, input.files); });

    function renderPreviews(z, files) {
      let previewContainer = z.querySelector('.photo-previews');
      if (!previewContainer) {
        previewContainer = document.createElement('div');
        previewContainer.className = 'photo-previews';
        z.appendChild(previewContainer);
      }
      previewContainer.innerHTML = '';
      Array.from(files || []).forEach(function (file, i) {
        const reader = new FileReader();
        reader.onload = function (ev) {
          const wrap = document.createElement('div');
          wrap.className = 'photo-preview';
          wrap.innerHTML =
            '<img src="' + ev.target.result + '" alt="Pré-visualização ' + (i + 1) + '">' +
            '<button type="button" class="photo-preview-remove" data-index="' + i + '" aria-label="Remover foto">×</button>';
          previewContainer.appendChild(wrap);
          wrap.querySelector('.photo-preview-remove').addEventListener('click', function () {
            removeFile(zone, i);
          });
        };
        reader.readAsDataURL(file);
      });
    }
  });

  function combineFiles(existing, added) {
    const dt = new DataTransfer();
    Array.from(existing || []).forEach(f => dt.items.add(f));
    added.forEach(f => dt.items.add(f));
    return dt.files;
  }
  function removeFile(zone, index) {
    const input = zone.querySelector('input[type="file"]');
    if (!input) return;
    const dt = new DataTransfer();
    Array.from(input.files).forEach(function (f, i) { if (i !== index) dt.items.add(f); });
    input.files = dt.files;
    renderPreviews(zone, input.files);
  }

  /* ---------- Controles de vídeo ---------- */
  document.querySelectorAll('.play-overlay').forEach(function (overlay) {
    overlay.addEventListener('click', function () {
      const card = overlay.closest('.video-card');
      if (!card) return;
      const video = card.querySelector('video');
      if (video) {
        overlay.style.display = 'none';
        video.play().catch(function () {});
      }
    });
  });

  document.querySelectorAll('video').forEach(function (video) {
    video.addEventListener('pause', function () {
      const overlay = video.closest('.video-wrapper')?.querySelector('.play-overlay');
      if (overlay && !video.ended) overlay.style.display = 'flex';
    });
    video.addEventListener('ended', function () {
      const overlay = video.closest('.video-wrapper')?.querySelector('.play-overlay');
      if (overlay) overlay.style.display = 'flex';
    });
  });

  /* ---------- Lazy loading com IntersectionObserver ---------- */
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          const el = entry.target;
          if (el.dataset.src) {
            el.src = el.dataset.src;
            el.removeAttribute('data-src');
          }
          if (el.dataset.srcset) {
            el.srcset = el.dataset.srcset;
            el.removeAttribute('data-srcset');
          }
          if (el.dataset.embed) {
            el.innerHTML = el.dataset.embed;
            el.removeAttribute('data-embed');
          }
          el.classList.remove('lazy');
          observer.unobserve(el);
        }
      });
    }, { rootMargin: '200px 0px', threshold: 0.01 });
    document.querySelectorAll('img[data-src], source[data-srcset], iframe[data-src], video[data-src], [data-embed]').forEach(function (el) { observer.observe(el); });
  }

  /* ---------- Hero em vídeo: degradação silenciosa ---------- */
  document.querySelectorAll('.hero-video').forEach(function (video) {
    var drop = function () {
      // Sem arquivo de vídeo: mantém o poster e o fundo estático do herói.
      video.classList.add('is-unavailable');
      video.remove();
    };
    video.addEventListener('error', drop);
    // O erro de <source> não propaga para o <video> em todos os navegadores.
    video.querySelectorAll('source').forEach(function (source) {
      source.addEventListener('error', drop);
    });
    video.addEventListener('loadeddata', function () { video.classList.add('is-ready'); }, { once: true });
  });

  /* ---------- Botão flutuante de IA ---------- */
  const aiFloat = document.querySelector('.ai-float-btn');
  if (aiFloat) {
    aiFloat.addEventListener('click', function () {
      const target = aiFloat.getAttribute('data-href') || '/guia-ia';
      if (target) window.location.href = target;
    });
  }

  /* ---------- Copiar link compartilhamento ---------- */
  document.querySelectorAll('[data-copy-link]').forEach(function (btn) {
    btn.addEventListener('click', async function () {
      try {
        await navigator.clipboard.writeText(location.href);
        const original = btn.textContent;
        btn.textContent = btn.dataset.copied || 'Copiado!';
        setTimeout(function () { btn.textContent = original; }, 1800);
      } catch (e) { /* ignorar */ }
    });
  });

  /* ---------- Formulários: feedback de envio ---------- */
  document.querySelectorAll('form[data-ajax]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      const btn = form.querySelector('button[type="submit"]');
      if (btn) {
        btn.disabled = true;
        btn.setAttribute('aria-busy', 'true');
        const original = btn.innerHTML;
        btn.innerHTML = '<span aria-hidden="true">⟳</span> ' + (btn.dataset.processing || 'Processando...');
        setTimeout(function () {
          btn.disabled = false;
          btn.removeAttribute('aria-busy');
          btn.innerHTML = original;
        }, 3000);
      }
    });
  });

  /* ---------- AI: teclado Enter no textarea com Ctrl ---------- */
  const aiTextarea = document.getElementById('ai-question') || document.querySelector('textarea[name="question"]');
  if (aiTextarea) {
    aiTextarea.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        const form = aiTextarea.closest('form');
        if (form) form.requestSubmit();
      }
    });
  }

  /* ---------- Animação de revelação ao scroll ---------- */
  const revealEls = document.querySelectorAll('.reveal');
  if (revealEls.length && 'IntersectionObserver' in window) {
    const ro = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add('revealed');
          ro.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12 });
    revealEls.forEach(function (el) { ro.observe(el); });
  }

  /* ---------- Inicialização silenciosa ---------- */
  document.documentElement.classList.add('js-ready');
})();
