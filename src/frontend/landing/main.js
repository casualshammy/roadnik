function openMapModal() {
  const modal = document.getElementById('map-modal');
  modal.hidden = false;
  requestAnimationFrame(() => modal.classList.add('visible'));
  const input = document.getElementById('modal-input');
  input.value = '';
  clearModalError();
  setTimeout(() => input.focus(), 50);
  document.addEventListener('keydown', onModalKey);
}

function closeMapModal() {
  const modal = document.getElementById('map-modal');
  modal.classList.remove('visible');
  modal.addEventListener('transitionend', () => { modal.hidden = true; }, { once: true });
  document.removeEventListener('keydown', onModalKey);
}

function onModalKey(e) {
  if (e.key === 'Escape') closeMapModal();
}

function clearModalError() {
  document.getElementById('modal-error').hidden = true;
  document.getElementById('modal-input').classList.remove('modal-input--error');
}

function confirmMap() {
  const raw = document.getElementById('modal-input').value.trim();
  if (!raw) { showModalError(); return; }

  let roomId = null;
  try {
    const url = new URL(raw);
    roomId = url.searchParams.get('id');
  } catch (_) {
    // not a URL — treat as plain room key
    roomId = raw;
  }

  if (!roomId) { showModalError(); return; }
  window.location.href = 'https://roadnik.app/r/?id=' + encodeURIComponent(roomId);
}

function showModalError() {
  document.getElementById('modal-error').hidden = false;
  const input = document.getElementById('modal-input');
  input.classList.add('modal-input--error');
  input.focus();
}
