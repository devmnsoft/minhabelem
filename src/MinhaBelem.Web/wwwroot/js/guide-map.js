// Loads the public browser map only on request. The server Routes key never enters this page.
const mapButton = document.querySelector('[data-guide-map-load]');
if (mapButton) {
  const status = document.querySelector('[data-guide-map-status]');
  const canvas = document.querySelector('[data-guide-map]');
  let timer;
  function failed() {
    clearTimeout(timer);
    canvas.hidden = true;
    status.textContent = mapButton.dataset.failed;
    mapButton.disabled = false;
  }
  window.initMinhaBelemGuideMap = () => {
    clearTimeout(timer);
    try {
      const center = { lat: Number(mapButton.dataset.lat), lng: Number(mapButton.dataset.lng) };
      canvas.hidden = false;
      const map = new google.maps.Map(canvas, { center, zoom: 16, mapTypeControl: false, streetViewControl: false });
      new google.maps.Marker({ position: center, map });
      status.textContent = '';
      mapButton.hidden = true;
    } catch { failed(); }
  };
  window.gm_authFailure = failed;
  mapButton.addEventListener('click', () => {
    mapButton.disabled = true;
    status.textContent = mapButton.dataset.loading;
    timer = setTimeout(failed, 15000);
    if (window.google?.maps) { window.initMinhaBelemGuideMap(); return; }
    document.querySelector('[data-guide-map-script]')?.remove();
    const script = document.createElement('script');
    script.dataset.guideMapScript = 'true';
    const params = new URLSearchParams({ key: mapButton.dataset.mapKey, callback: 'initMinhaBelemGuideMap', loading: 'async' });
    script.src = `https://maps.googleapis.com/maps/api/js?${params}`;
    script.async = true;
    script.onerror = failed;
    document.head.append(script);
  });
}
