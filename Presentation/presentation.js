(function () {
  const slides = Array.from(document.querySelectorAll('.slide'));
  const total = slides.length;
  const counterEl = document.getElementById('slide-counter');
  const progressBar = document.getElementById('progress-bar');
  const dotsWrap = document.getElementById('slide-dots');

  slides.forEach((_, i) => {
    const dot = document.createElement('div');
    dot.className = 'dot';
    dot.dataset.index = String(i);
    dot.addEventListener('click', () => goTo(i));
    dotsWrap.appendChild(dot);
  });
  const dots = Array.from(dotsWrap.children);

  function indexFromHash() {
    const match = /^#slide-(\d+)$/.exec(window.location.hash);
    if (!match) return 0;
    const i = parseInt(match[1], 10) - 1;
    return i >= 0 && i < total ? i : 0;
  }

  let current = indexFromHash();

  function render() {
    slides.forEach((slide, i) => slide.classList.toggle('active', i === current));
    dots.forEach((dot, i) => dot.classList.toggle('active', i === current));
    counterEl.textContent = `${String(current).padStart(2, '0')} / ${String(total - 1).padStart(2, '0')}`;
    progressBar.style.width = `${(current / (total - 1)) * 100}%`;
    history.replaceState(null, '', `#slide-${current + 1}`);
    window.dispatchEvent(new CustomEvent('slide:entered', { detail: { index: current, slide: slides[current] } }));
  }

  function goTo(index) {
    current = Math.max(0, Math.min(total - 1, index));
    render();
  }

  function next() { goTo(current + 1); }
  function prev() { goTo(current - 1); }

  document.getElementById('next-btn').addEventListener('click', next);
  document.getElementById('prev-btn').addEventListener('click', prev);

  window.addEventListener('keydown', (e) => {
    if (['Space', 'ArrowRight', 'PageDown'].includes(e.code)) {
      e.preventDefault();
      next();
    } else if (['ArrowLeft', 'PageUp'].includes(e.code)) {
      e.preventDefault();
      prev();
    }
  });

  let touchStartX = null;
  window.addEventListener('touchstart', (e) => { touchStartX = e.touches[0].clientX; });
  window.addEventListener('touchend', (e) => {
    if (touchStartX === null) return;
    const dx = e.changedTouches[0].clientX - touchStartX;
    if (Math.abs(dx) > 60) (dx < 0 ? next : prev)();
    touchStartX = null;
  });

  render();
})();
