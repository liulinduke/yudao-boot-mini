(function () {
  const contactConfig = { phone: '15914489294', address: '佛山市禅城区兴业路绿地慧谷广场 1 座 810' };
  const modal = document.querySelector('#modal');
  const openModal = () => { if (!modal) return; modal.classList.add('open'); modal.setAttribute('aria-hidden', 'false'); document.body.style.overflow = 'hidden'; };
  const closeModal = () => { if (!modal) return; modal.classList.remove('open'); modal.setAttribute('aria-hidden', 'true'); document.body.style.overflow = ''; };
  document.querySelectorAll('[data-modal]').forEach((button) => button.addEventListener('click', openModal));
  document.querySelectorAll('[data-close-modal]').forEach((button) => button.addEventListener('click', closeModal));
  if (modal) modal.addEventListener('click', (event) => { if (event.target === modal) closeModal(); });
  document.addEventListener('keydown', (event) => { if (event.key === 'Escape') closeModal(); });

  const navToggle = document.querySelector('.nav-toggle');
  const nav = document.querySelector('.site-nav');
  if (navToggle && nav) navToggle.addEventListener('click', () => nav.classList.toggle('open'));
  document.querySelectorAll('.site-nav a').forEach((link) => link.addEventListener('click', () => nav && nav.classList.remove('open')));

  const revealObserver = new IntersectionObserver((entries) => entries.forEach((entry) => { if (entry.isIntersecting) { entry.target.classList.add('visible'); revealObserver.unobserve(entry.target); } }), { threshold: 0.12 });
  document.querySelectorAll('.reveal').forEach((element) => revealObserver.observe(element));

  const animateNumber = (element, target, duration = 900, decimals = 0) => {
    if (!element) return;
    const start = performance.now();
    const tick = (now) => {
      const progress = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      const value = target * eased;
      element.textContent = decimals ? value.toFixed(decimals) : Math.floor(value).toLocaleString('en-US');
      if (progress < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  };

  const count = document.querySelector('#lead-count');
  if (count) animateNumber(count, 3280, 1100);

  const metricRow = document.querySelector('.metric-row');
  if (metricRow) {
    metricRow.innerHTML = '<div class="metric-demo"><small>需求识别</small><strong><span data-count-target="98.6" data-count-decimals="1">0</span><span class="percent">%</span></strong><span class="up">↑ 12.4%</span></div><div class="metric-demo"><small>高意向买家</small><strong data-count-target="468">0</strong><span class="up">↑ 8.2%</span></div><div class="metric-demo"><small>已触达</small><strong data-count-target="326">0</strong><span class="neutral">实时</span></div>';
    metricRow.querySelectorAll('[data-count-target]').forEach((element, index) => {
      animateNumber(element, Number(element.dataset.countTarget), 850 + index * 130, Number(element.dataset.countDecimals || 0));
    });
  }

  const prices = { month: ['¥399', ' / 月', '按月灵活付费'], quarter: ['¥1137', ' / 季度', '季度付费 · 已享 95 折'], halfyear: ['¥2107', ' / 半年', '半年付费 · 已享 88 折'], year: ['¥3830', ' / 年', '年付 · 已享 8 折'] };
  document.querySelectorAll('[data-cycle]').forEach((button) => button.addEventListener('click', () => {
    document.querySelectorAll('[data-cycle]').forEach((item) => item.classList.remove('active')); button.classList.add('active');
    const data = prices[button.dataset.cycle]; const value = document.querySelector('#price-value'); const unit = document.querySelector('#price-unit'); const saving = document.querySelector('#saving-text');
    if (value && unit && saving) { value.textContent = data[0]; unit.textContent = data[1]; saving.textContent = data[2]; }
  }));

  const copyButton = document.querySelector('#copy-request');
  if (copyButton) copyButton.addEventListener('click', async () => {
    const request = `易洋出海免费试用咨询\n手机：${contactConfig.phone}\n地址：${contactConfig.address}`;
    try { await navigator.clipboard.writeText(request); } catch (_) { const helper = document.createElement('textarea'); helper.value = request; document.body.appendChild(helper); helper.select(); document.execCommand('copy'); helper.remove(); }
    const note = document.querySelector('#copy-note'); if (note) note.textContent = '手机号码已复制，请添加微信或拨打电话咨询。';
  });
})();
