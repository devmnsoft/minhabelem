(() => {
 const form = document.querySelector('#editorial-batch'); if (!form) return;
 const items = [...form.querySelectorAll('input[name="selected"]')];
 const count = () => { document.querySelector('#batch-count').textContent = form.querySelector('input[name="allFiltered"]').checked ? form.dataset.filteredTotal : items.filter(x => x.checked).length; };
 document.querySelector('#batch-select-page').addEventListener('change', e => { items.forEach(x => x.checked = e.target.checked); count(); });
 items.forEach(x => x.addEventListener('change', count));
 form.querySelector('input[name="allFiltered"]').addEventListener('change',e=>{if(e.target.checked)form.querySelector('input[name="confirmedCount"]').value=form.dataset.filteredTotal;count();});
})();
