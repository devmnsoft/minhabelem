(() => {
 const form = document.querySelector('#editorial-batch'); if (!form) return;
 const items = [...form.querySelectorAll('input[name="selected"]')];
 const pageToggle = document.querySelector('#batch-select-page');
 const count = () => {
   const selected = items.filter(x => x.checked).length;
   document.querySelector('#batch-count').textContent = selected;
   pageToggle.checked = items.length > 0 && selected === items.length;
   pageToggle.indeterminate = selected > 0 && selected < items.length;
 };
 document.querySelector('#batch-select-page').addEventListener('change', e => { items.forEach(x => x.checked = e.target.checked); count(); });
 items.forEach(x => x.addEventListener('change', count));
 // Alt+Shift+A selects the visible page; Escape clears the current selection.
 document.addEventListener('keydown', e => {
   if (e.target.matches('input:not([type=checkbox]),textarea,select,[contenteditable=true]')) return;
   if (e.altKey && e.shiftKey && e.code === 'KeyA') { e.preventDefault(); items.forEach(x => x.checked = true); count(); }
   if (e.key === 'Escape') { items.forEach(x => x.checked = false); count(); }
 });
 count();
})();
