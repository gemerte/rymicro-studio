/* RYMICRO Studio — ядро панели: состояние, API, события, роутер, тосты, захват клавиш */
(() => {
  'use strict';

  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (s) => String(s == null ? '' : s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const fmt = (n) => Number(n || 0).toLocaleString('ru-RU');
  const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
  const delay = (ms) => new Promise((r) => setTimeout(r, ms));

  const A = {
    token: new URLSearchParams(location.search).get('t') || '',
    cfg: null, keys: [], kmap: {}, kb: null, rt: {}, state: null, ver: '',
    tab: 'board', layer: '', sel: null, draft: null,
    live: new Set(), liveOrder: [], pressedTotal: 0, heat: false,
    drag: null, capture: null, audio: null, lastSound: 0,
    macroSel: null, macroDraft: null, rgbLog: [], hidDevices: [],
    pendingState: null, connected: false, sse: null, ready: false,
    saveTimer: null, lightingTimer: null, keyLog: [],
  };
  const R = {};

  /* ------------------------------- API ------------------------------- */

  async function api(name, body) {
    const res = await fetch('/api/' + name, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Auth': A.token },
      body: JSON.stringify(body || {}),
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    return res.json();
  }

  async function apiGet(name, params = {}) {
    const q = new URLSearchParams(Object.assign({ t: A.token }, params));
    const res = await fetch('/api/' + name + '?' + q.toString());
    if (!res.ok) throw new Error('HTTP ' + res.status);
    return res.json();
  }

  /* ----------------------------- helpers ----------------------------- */

  const keyName = (id) => (A.kmap[id] ? A.kmap[id].name : (id || '—'));
  const profileObj = () => (A.cfg && A.cfg.Profiles[A.cfg.ActiveProfile]) || null;

  function rulesForLayer(prof, layer) {
    if (!prof) return [];
    if (!layer) return prof.Rules || [];
    return (prof.Layers && prof.Layers[layer]) || [];
  }

  function ruleFor(id, layer = A.layer) {
    const prof = profileObj();
    return rulesForLayer(prof, layer).find((r) => r.From === id) || null;
  }

  function actionSummary(a) {
    a = a || {};
    switch ((a.Type || 'remap').toLowerCase()) {
      case 'remap': return '→ ' + keyName(a.Key) + (a.Keys && a.Keys.length ? ' (' + a.Keys.map(keyName).join('+') + ')' : '');
      case 'media': return 'медиа → ' + keyName(a.Key);
      case 'combo': return 'комбо ' + (a.Keys || []).map(keyName).join(' + ');
      case 'macro': return 'макрос · ' + ((a.Steps && a.Steps.length) || 0) + ' шагов';
      case 'text': return 'текст: «' + String(a.Text || '').slice(0, 46) + '»';
      case 'app': return 'запуск: ' + (a.Path || '—');
      case 'run': return 'команда: ' + (a.Command || '—');
      case 'disable': return 'отключено';
      case 'layer': return 'слой «' + (a.Layer || '') + '» · ' + ((a.Mode || 'hold') === 'hold' ? 'удержание' : 'переключение');
      case 'profile': return 'профиль: ' + (a.Profile || '—');
      default: return a.Type || '—';
    }
  }

  function badgeFor(rule) {
    const a = (rule && rule.Action) || {};
    if (!rule.Enabled) return null;
    switch ((a.Type || 'remap').toLowerCase()) {
      case 'remap': case 'media': return { label: '→' + keyName(a.Key), cls: '' };
      case 'combo': return { label: (a.Keys || []).map(keyName).join('+'), cls: 'text' };
      case 'macro': return { label: 'макрос', cls: 'macro' };
      case 'text': return { label: 'текст', cls: 'text' };
      case 'app': return { label: 'запуск', cls: 'app' };
      case 'run': return { label: 'cmd', cls: 'run' };
      case 'disable': return { label: 'off', cls: 'off' };
      case 'layer': return { label: 'слой', cls: 'layer' };
      case 'profile': return { label: 'профиль', cls: 'profile' };
      default: return { label: a.Type || '?', cls: '' };
    }
  }

  function hexToRgb(hex) {
    let h = String(hex || '').replace('#', '');
    if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
    const n = parseInt(h, 16);
    return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 };
  }

  function stripStyle(s) {
    s = s || {};
    const c = s.Colors || [];
    const c1 = c[0] || '#00e5ff';
    const c2 = c[1] || c[0] || '#ff2d95';
    const dur = (11.5 - clamp(s.Speed || 5, 1, 10)) + 's';
    const br = (0.45 + clamp(s.Brightness == null ? 80 : s.Brightness, 0, 100) / 100 * 0.8).toFixed(2);
    return '--c1:' + esc(c1) + ';--c2:' + esc(c2) + ';--dur:' + dur + ';--br:' + br + ';--glow:' + (clamp(s.Brightness == null ? 80 : s.Brightness, 0, 100) / 100);
  }

  /* ------------------------------ state ------------------------------ */

  function applyState(s) {
    if (!s || !s.config) return;
    A.state = s;
    A.cfg = s.config;
    A.keys = s.keys || A.keys;
    A.kmap = {};
    A.keys.forEach((k) => { A.kmap[k.id] = k; });
    A.kb = s.keyboard || A.kb || {};
    A.rt = s.runtime || {};
    A.ver = s.version || '';
    const profs = A.cfg.Profiles || {};
    if (!profs[A.cfg.ActiveProfile]) A.cfg.ActiveProfile = Object.keys(profs)[0] || 'default';
    if (A.layer && !((profileObj() || {}).Layers || {})[A.layer]) A.layer = '';
    A.ready = true;
    R.render.header();
    R.render.active();
  }

  function onState(s) {
    const ae = document.activeElement;
    if (ae && (ae.tagName === 'INPUT' || ae.tagName === 'TEXTAREA' || ae.tagName === 'SELECT')) {
      A.pendingState = s;
      return;
    }
    applyState(s);
  }

  document.addEventListener('focusout', () => {
    if (A.pendingState) {
      const s = A.pendingState;
      A.pendingState = null;
      setTimeout(() => applyState(s), 60);
    }
  });

  /* ------------------------------- SSE ------------------------------- */

  function connectSSE() {
    if (A.sse) A.sse.close();
    const es = new EventSource('/api/event?t=' + encodeURIComponent(A.token));
    A.sse = es;
    es.onopen = () => { A.connected = true; R.render.header(); };
    es.onerror = () => { A.connected = false; R.render.header(); };
    es.onmessage = (ev) => {
      let d;
      try { d = JSON.parse(ev.data); } catch (e) { return; }
      handleEvent(d);
    };
  }

  function handleEvent(d) {
    if (!d) return;
    if (d.Kind === 'key') onKey(d);
    else if (d.Kind === 'state') onState(d.state);
    else if (d.Kind === 'log') toast(d.Note || '', 'ok');
    else if (d.Kind === 'ping') { if (!A.connected) { A.connected = true; R.render.header(); } }
  }

  function onKey(d) {
    A.keyLog.unshift(d);
    if (A.keyLog.length > 14) A.keyLog.length = 14;
    renderKeyLog();
    if (d.Injected) return;
    if (d.Down) {
      if (!A.live.has(d.Id)) {
        A.live.add(d.Id);
        A.liveOrder.push(d.Id);
        A.pressedTotal++;
      }
    } else {
      A.live.delete(d.Id);
      A.liveOrder = A.liveOrder.filter((x) => x !== d.Id);
    }
    updateKeyLive(d.Id);
    renderLiveBar();
    playSound(d.Id);
    if (d.Down) pulseStrips();
    if (A.capture) captureEvent(d);
    if (A.cfg && A.cfg.Settings && A.cfg.Settings.StatsEnabled && d.Id) {
      if (!A.cfg.Stats) A.cfg.Stats = {};
      A.cfg.Stats[d.Id] = (A.cfg.Stats[d.Id] || 0) + (d.Down ? 1 : 0);
    }
  }

  function updateKeyLive(id) {
    const el = document.querySelector('.key[data-key="' + id + '"]');
    if (el) el.classList.toggle('down', A.live.has(id));
  }

  function renderLiveBar() {
    const box = $('#liveKeys');
    if (!box) return;
    const names = A.liveOrder.slice(-14).map((id) => '<span class="live-key">' + esc(keyName(id)) + '</span>');
    box.innerHTML = names.length ? names.join('') : '<span class="muted" style="font-size:12px">нажмите клавишу — появится здесь</span>';
    const info = $('#liveInfo');
    if (info) info.textContent = A.pressedTotal ? 'за сессию: ' + fmt(A.pressedTotal) : '';
  }

  const CANON = {
    '5B': 'LWIN', E05B: 'LWIN', '5C': 'RWIN', E05C: 'RWIN', '5D': 'MENU', E05D: 'MENU',
    '38': 'ALT', E038: 'ALT', '1D': 'CTRL', E01D: 'CTRL', '2A': 'SHIFT', '36': 'SHIFT',
  };
  const canonKey = (id) => CANON[String(id || '').toUpperCase()] || null;
  const sameKey = (a, b) => {
    const x = String(a || '').toUpperCase(), y = String(b || '').toUpperCase();
    if (x && x === y) return true;
    const ca = canonKey(x);
    return ca !== null && ca === canonKey(y);
  };
  const hex2 = (n) => Number(n || 0).toString(16).toUpperCase().padStart(2, '0');

  function renderKeyLog() {
    const box = $('#keyLogBox');
    if (!box) return;
    if (!A.keyLog.length) { box.innerHTML = '<div class="muted" style="font-size:12px">нажмите любую клавишу…</div>'; return; }
    const rules = rulesForLayer(profileObj(), A.layer) || [];
    box.innerHTML = A.keyLog.map((d) => {
      const rule = rules.find((r) => r.Enabled && sameKey(r.From, d.Id));
      const verdict = rule
        ? '<span class="ok">правило ' + esc(rule.From) + ' → ' + esc(actionSummary(rule.Action)) + '</span>'
        : '<span class="err">правила нет</span>';
      const alias = rule && String(rule.From).toUpperCase() !== String(d.Id || '').toUpperCase()
        ? ' <span class="tag">алиас</span>' : '';
      return '<div class="klog"><b>' + esc(d.Name || d.Id || '—') + '</b>' +
        '<span class="mono">id=' + esc(d.Id || '—') + ' · scan=' + hex2(d.Scan) + ' · vk=' + hex2(d.Vk) +
        (d.Ext ? ' · ext' : '') + (d.Down ? '' : ' · up') + '</span>' +
        (d.Injected ? '<span class="tag">inject</span>' : '') + verdict + alias + '</div>';
    }).join('');
  }

  function pulseStrips() {    const s = A.cfg && A.cfg.Lighting;
    if (!s) return;
    ['left', 'right'].forEach((side) => {
      const st = side === 'left' ? s.SideLeft : s.SideRight;
      if (!st || st.Effect !== 'reactive') return;
      const el = document.querySelector('.side-strip.' + side);
      if (!el) return;
      el.classList.remove('pulse');
      void el.offsetWidth;
      el.classList.add('pulse');
      clearTimeout(el._pulse);
      el._pulse = setTimeout(() => el.classList.remove('pulse'), 450);
    });
  }

  function playSound(id) {
    const st = A.cfg && A.cfg.Settings;
    if (!st || !st.Sound) return;
    const now = performance.now();
    if (now - A.lastSound < 18) return;
    A.lastSound = now;
    try {
      if (!A.audio) A.audio = new (window.AudioContext || window.webkitAudioContext)();
      const ctx = A.audio;
      if (ctx.state === 'suspended') ctx.resume();
      const o = ctx.createOscillator();
      const g = ctx.createGain();
      const base = 240 + (parseInt(id, 16) % 12) * 26;
      o.type = 'triangle';
      o.frequency.setValueAtTime(base, ctx.currentTime);
      o.frequency.exponentialRampToValueAtTime(base * 0.72, ctx.currentTime + 0.05);
      g.gain.setValueAtTime((st.SoundVolume == null ? 25 : st.SoundVolume) / 100 * 0.12, ctx.currentTime);
      g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.07);
      o.connect(g).connect(ctx.destination);
      o.start();
      o.stop(ctx.currentTime + 0.08);
    } catch (e) { /* без звука */ }
  }

  /* ------------------------------ capture ---------------------------- */

  function startCapture(opts) {
    A.capture = {
      mode: opts.mode || 'single',
      title: opts.title || 'Нажмите клавишу',
      sub: opts.sub || 'Нажмите клавишу на клавиатуре. Ремап временно приостановлен.',
      onDone: opts.onDone || null,
      result: opts.mode === 'macro' ? [] : [],
      last: performance.now(),
    };
    $('#capTitle').textContent = A.capture.title;
    $('#capSub').textContent = A.capture.sub;
    $('#capChips').innerHTML = '';
    $('#capDone').style.display = A.capture.mode === 'single' ? 'none' : 'inline-flex';
    $('#capture').classList.add('open');
    api('suspend', { on: true }).catch(() => {});
  }

  function finishCapture(save) {
    const c = A.capture;
    if (!c) return;
    A.capture = null;
    $('#capture').classList.remove('open');
    api('suspend', { on: false }).catch(() => {});
    if (save && c.onDone) {
      try { c.onDone(c.result); } catch (e) { console.error(e); }
    }
  }

  function captureEvent(d) {
    const c = A.capture;
    if (!c) return;
    if (c.mode === 'single') {
      if (d.Down) {
        if (d.Id === '01') { finishCapture(false); return; }
        c.result = d.Id;
        finishCapture(true);
      }
      return;
    }
    if (c.mode === 'combo') {
      if (d.Down && !c.result.includes(d.Id)) {
        c.result.push(d.Id);
        capChips();
      }
      return;
    }
    if (c.mode === 'macro') {
      const now = performance.now();
      const gap = now - c.last;
      c.last = now;
      c.result.push({ Key: d.Id, Down: !!d.Down, DelayMs: c.result.length === 0 ? Math.min(600, Math.round(gap)) : clamp(Math.round(gap), 5, 1500) });
      capChips(true);
    }
  }

  function capChips(isMacro) {
    const box = $('#capChips');
    if (!box || !A.capture) return;
    const c = A.capture;
    if (isMacro) {
      const n = c.result.length;
      const last = c.result.slice(-8).map((s) => '<span class="chip">' + esc(keyName(s.Key)) + (s.Down ? '↓' : '↑') + '</span>').join('');
      box.innerHTML = '<span class="chip">шагов: ' + n + '</span>' + last;
      return;
    }
    box.innerHTML = c.result.map((id) => '<span class="chip">' + esc(keyName(id)) + '</span>').join('') ||
      '<span class="muted">пока пусто</span>';
  }

  $('#capCancel').addEventListener('click', () => finishCapture(false));
  $('#capDone').addEventListener('click', () => finishCapture(true));

  /* ------------------------------ toasts ----------------------------- */

  function toast(msg, kind = 'info', ms = 3400) {
    if (!msg) return;
    const box = $('#toasts');
    const el = document.createElement('div');
    el.className = 'toast ' + (kind === 'error' ? 'err' : kind === 'success' ? 'ok' : '');
    el.innerHTML = '<span class="dot"></span><span>' + esc(msg) + '</span>';
    box.appendChild(el);
    setTimeout(() => { el.classList.add('leave'); setTimeout(() => el.remove(), 220); }, ms);
  }

  /* ------------------------------ modal ------------------------------ */

  function openModal(html) {
    const m = $('#modal');
    m.innerHTML = '<div class="modal-box">' + html + '</div>';
    m.classList.add('open');
    m.onclick = (e) => { if (e.target === m) closeModal(); };
    return m;
  }
  function closeModal() {
    const m = $('#modal');
    m.classList.remove('open');
    m.innerHTML = '';
  }

  function ask(title, desc, value = '') {
    return new Promise((resolve) => {
      openModal('<h3>' + esc(title) + '</h3><p>' + esc(desc || '') + '</p>' +
        '<input id="askInput" type="text" value="' + esc(value) + '" autocomplete="off">' +
        '<div class="row"><button class="btn ghost" data-x="cancel">Отмена</button><button class="btn" data-x="ok">ОК</button></div>');
      const input = $('#askInput');
      input.focus();
      input.select();
      const done = (val) => { closeModal(); resolve(val); };
      $('#modal').querySelector('[data-x="cancel"]').onclick = () => done(null);
      $('#modal').querySelector('[data-x="ok"]').onclick = () => done(input.value.trim() || null);
      input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') done(input.value.trim() || null);
        if (e.key === 'Escape') done(null);
      });
    });
  }

  function confirmBox(title, desc, okLabel = 'Удалить') {
    return new Promise((resolve) => {
      openModal('<h3>' + esc(title) + '</h3><p>' + esc(desc || '') + '</p>' +
        '<div class="row"><button class="btn ghost" data-x="cancel">Отмена</button><button class="btn danger" data-x="ok">' + esc(okLabel) + '</button></div>');
      $('#modal').querySelector('[data-x="cancel"]').onclick = () => { closeModal(); resolve(false); };
      $('#modal').querySelector('[data-x="ok"]').onclick = () => { closeModal(); resolve(true); };
    });
  }

  /* ------------------------------ header ----------------------------- */

  function renderHeader() {
    if (!A.cfg) return;
    const kb = A.kb || {};
    const dev = $('#devLine');
    if (dev) {
      dev.textContent = (kb.Name || 'RY Micro USB HID') + ' · VID ' + (kb.Vid || '352D') + ':PID ' + (kb.Pid || '2380') +
        (kb.Serial ? ' · S/N ' + kb.Serial : '');
    }
    const pill = $('#hookState');
    if (pill) {
      pill.className = 'pill ' + (A.connected ? (A.rt.paused ? 'off' : 'on') : 'err');
      pill.textContent = !A.connected ? '● нет связи' : A.rt.paused ? '● пауза' : '● хук активен';
    }
    const sel = $('#profileSel');
    if (sel) {
      const names = Object.keys(A.cfg.Profiles || {});
      sel.innerHTML = names.map((n) => '<option value="' + esc(n) + '"' + (n === A.cfg.ActiveProfile ? ' selected' : '') + '>' + esc(n) + '</option>').join('');
    }
    const pause = $('#pauseBtn');
    if (pause) {
      pause.textContent = A.rt.paused ? '▶ Продолжить' : '⏸ Пауза';
      pause.classList.toggle('magenta', !!A.rt.paused);
    }
    if (A.rt.suspended) {
      const c = $('#capTitle');
      if (c) c.textContent = A.capture ? A.capture.title : c.textContent;
    }
  }

  /* ------------------------------ router ----------------------------- */

  const render = { header: renderHeader, active: () => {} };

  function switchTab(tab) {
    if (!A.ready) return;
    A.tab = tab;
    try { localStorage.setItem('ry.tab', tab); } catch (e) {}
    $$('.tab').forEach((b) => b.classList.toggle('active', b.dataset.tab === tab));
    render.active();
    $$('.page').forEach((p) => p.classList.toggle('active', p.dataset.page === tab));
  }

  render.active = () => {
    const fn = render[A.tab];
    if (typeof fn === 'function') {
      try { fn(); } catch (e) { console.error(e); toast('Ошибка отрисовки: ' + e.message, 'error', 6000); }
    }
  };

  /* ------------------------------- boot ------------------------------ */

  async function start() {
    if (new URLSearchParams(location.search).get('noanim') === '1') document.body.classList.add('noanim');

    $('#tabs').addEventListener('click', (e) => {
      const b = e.target.closest('.tab');
      if (b) switchTab(b.dataset.tab);
    });

    $('#profileSel').addEventListener('change', async (e) => {
      try { await api('profile', { name: e.target.value }); } catch (err) { toast('Не удалось сменить профиль', 'error'); }
    });

    $('#pauseBtn').addEventListener('click', async () => {
      try {
        const r = await api('pause', { on: !A.rt.paused });
        A.rt.paused = r.paused;
        renderHeader();
        toast(r.paused ? 'Ремап на паузе' : 'Ремап включён', 'ok');
      } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });

    $('#aboutBtn').addEventListener('click', () => R.about());

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && A.capture) finishCapture(false);
      if (e.key === 'Escape' && $('#modal').classList.contains('open')) closeModal();
    });

    try {
      const s = await apiGet('state');
      applyState(s);
      const noSse = new URLSearchParams(location.search).get('nosse') === '1';
      if (noSse) { A.connected = true; renderHeader(); }
      else connectSSE();
      const saved = localStorage.getItem('ry.tab');
      const tabs = $$('.tab').map((b) => b.dataset.tab);
      const wanted = new URLSearchParams(location.search).get('tab');
      switchTab(wanted && tabs.includes(wanted) ? wanted : (saved && tabs.includes(saved) ? saved : 'board'));
      renderLiveBar();
    } catch (e) {
      $('#app').innerHTML = '<div class="card" style="max-width:640px;margin:60px auto"><h3>Нет связи с приложением</h3><p class="sub">' +
        esc(e.message) + '<br>Проверьте, что RYMICRO Studio запущена, и откройте панель через трей.</p>' +
        '<button class="btn" onclick="location.reload()">Обновить</button></div>';
    }
  }

  Object.assign(R, {
    A, $, $$, esc, fmt, clamp, delay, api, apiGet,
    keyName, profileObj, rulesForLayer, ruleFor, actionSummary, badgeFor, hexToRgb, stripStyle,
    applyState, onState, connectSSE, onKey, updateKeyLive, renderLiveBar, pulseStrips, playSound,
    sameKey, canonKey, renderKeyLog,
    startCapture, finishCapture, toast, openModal, closeModal, ask, confirmBox,
    render, switchTab, start, renderHeader,
  });
  window.RY = R;
})();
