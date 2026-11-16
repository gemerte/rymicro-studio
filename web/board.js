/* RYMICRO Studio — экран клавиатуры, инспектор клавиши, бинды */
(() => {
  'use strict';
  const R = window.RY;
  const { A, $, esc, fmt, clamp, api, keyName, profileObj, rulesForLayer, ruleFor,
    actionSummary, badgeFor, stripStyle, toast, openModal, closeModal, ask, confirmBox,
    startCapture, render, renderLiveBar } = R;

  /* --------------------------- геометрия --------------------------- */

  const GEOM = [];
  const K = (id, x, y, w = 1, h = 1) => GEOM.push({ id, x, y, w, h });

  K('01', 0, 0);
  [['3B', 2], ['3C', 3], ['3D', 4], ['3E', 5], ['3F', 6.5], ['40', 7.5], ['41', 8.5], ['42', 9.5], ['43', 11], ['44', 12], ['57', 13], ['58', 14]]
    .forEach(([id, x]) => K(id, x, 0));
  K('E037', 15.75, 0); K('46', 16.75, 0); K('E11D', 17.75, 0);

  let x = 0;
  ['29', '02', '03', '04', '05', '06', '07', '08', '09', '0A', '0B', '0C', '0D'].forEach((id) => { K(id, x, 1.25); x++; });
  K('0E', x, 1.25, 2);
  K('E052', 15.75, 1.25); K('E047', 16.75, 1.25); K('E049', 17.75, 1.25);

  K('0F', 0, 2.25, 1.5);
  x = 1.5;
  ['10', '11', '12', '13', '14', '15', '16', '17', '18', '19', '1A', '1B'].forEach((id) => { K(id, x, 2.25); x++; });
  K('2B', 13.5, 2.25, 1.5);
  K('E053', 15.75, 2.25); K('E04F', 16.75, 2.25); K('E051', 17.75, 2.25);

  K('3A', 0, 3.25, 1.75);
  x = 1.75;
  ['1E', '1F', '20', '21', '22', '23', '24', '25', '26', '27', '28'].forEach((id) => { K(id, x, 3.25); x++; });
  K('1C', 12.75, 3.25, 2.25);

  K('2A', 0, 4.25, 2.25);
  x = 2.25;
  ['2C', '2D', '2E', '2F', '30', '31', '32', '33', '34', '35'].forEach((id) => { K(id, x, 4.25); x++; });
  K('36', 12.25, 4.25, 2.75);
  K('E048', 16.75, 4.25);

  K('1D', 0, 5.25, 1.25); K('E05B', 1.25, 5.25, 1.25); K('38', 2.5, 5.25, 1.25); K('39', 3.75, 5.25, 6.25);
  K('E038', 10, 5.25, 1.25); K('E05C', 11.25, 5.25, 1.25); K('E05D', 12.5, 5.25, 1.25); K('E01D', 13.75, 5.25, 1.25);
  K('E04B', 15.75, 5.25); K('E050', 16.75, 5.25); K('E04D', 17.75, 5.25);

  K('45', 19, 1.25); K('E035', 20, 1.25); K('37', 21, 1.25); K('4A', 22, 1.25);
  K('47', 19, 2.25); K('48', 20, 2.25); K('49', 21, 2.25); K('4E', 22, 2.25, 1, 2);
  K('4B', 19, 3.25); K('4C', 20, 3.25); K('4D', 21, 3.25);
  K('4F', 19, 4.25); K('50', 20, 4.25); K('51', 21, 4.25); K('E01C', 22, 4.25, 1, 2);
  K('52', 19, 5.25, 2); K('53', 21, 5.25);

  R.GEOM = GEOM;

  function section(name) {
    const app = $('#app');
    if (!app.dataset.init) { app.innerHTML = ''; app.dataset.init = '1'; }
    let p = document.querySelector('.page[data-page="' + name + '"]');
    if (!p) {
      p = document.createElement('section');
      p.className = 'page';
      p.dataset.page = name;
      app.appendChild(p);
    }
    return p;
  }
  R.section = section;

  /* ------------------------- отрисовка клавиатуры ------------------------- */

  function stripHtml(side, s) {
    s = s || {};
    const fx = s.Enabled ? (s.Effect || 'static') : 'off';
    return '<div class="side-strip ' + side + '" data-fx="' + esc(fx) + '" style="' + stripStyle(s) + '"></div>';
  }

  function renderKeyboard() {
    const kb = $('#kb');
    if (!kb || !A.cfg) return;
    const rules = rulesForLayer(profileObj(), A.layer);
    const stats = A.cfg.Stats || {};
    let max = 1;
    Object.keys(stats).forEach((k) => { if (stats[k] > max) max = stats[k]; });

    let html = stripHtml('left', A.cfg.Lighting.SideLeft) + stripHtml('right', A.cfg.Lighting.SideRight);

    GEOM.forEach((g) => {
      const def = A.kmap[g.id] || { name: g.id };
      const rule = rules.find((r) => r.From === g.id);
      const badge = rule ? badgeFor(rule) : null;
      const heat = A.heat ? clamp((stats[g.id] || 0) / max, 0, 1) : null;
      const cls = ['key'];
      if (rule) cls.push('remapped');
      if (rule && !rule.Enabled) cls.push('disabled');
      if (def.name && def.name.length > 5.5 * g.w) cls.push('xs');
      else if (def.name && def.name.length > 4 * g.w) cls.push('sl');
      if (A.live.has(g.id)) cls.push('down');
      if (A.sel === g.id) cls.push('selected');
      if (A.heat) cls.push('heat');
      const style = 'left:calc(' + g.x + ' * var(--u));top:calc(' + g.y + ' * var(--u));width:calc(' + g.w + ' * var(--u) - 3px);height:calc(' + g.h + ' * var(--u) - 3px);' +
        (heat != null ? '--heat:' + heat.toFixed(3) + ';' : '');
      html += '<div class="' + cls.join(' ') + '" data-key="' + g.id + '" draggable="true" style="' + style + '" title="' + esc(def.name) + ' · scan ' + g.id + '">' +
        (def.alt ? '<span class="k-alt">' + esc(def.alt) + '</span>' : '') +
        '<span class="k-lab">' + esc(def.name || g.id) + '</span>' +
        (badge ? '<span class="k-badge ' + badge.cls + '">' + esc(badge.label) + '</span>' : '') +
        '</div>';
    });
    kb.innerHTML = html;
  }
  R.renderKeyboard = renderKeyboard;

  function layerName(l) { return l ? l : 'Базовый'; }

  function renderLayerSeg() {
    const seg = $('#layerSeg');
    if (!seg || !A.cfg) return;
    const prof = profileObj();
    const layers = Object.keys((prof && prof.Layers) || {});
    let html = '<button class="' + (!A.layer ? 'on' : '') + '" data-layer="">Базовый</button>';
    layers.forEach((l) => {
      const n = (prof.Layers[l] || []).length;
      html += '<button class="' + (A.layer === l ? 'on' : '') + '" data-layer="' + esc(l) + '">' + esc(l) + (n ? ' · ' + n : '') + '</button>';
    });
    seg.innerHTML = html;
  }

  function renderBoard() {
    if (!A.cfg) return;
    const p = section('board');
    p.innerHTML = `
      <div class="board-layout">
        <div class="board-wrap">
          <div class="board-head">
            <div class="seg" id="layerSeg"></div>
            <div class="board-tools">
              <label class="switch"><input type="checkbox" id="heatToggle"${A.heat ? ' checked' : ''}><span class="track"></span>Тепловая карта</label>
              <button class="btn ghost sm" id="addLayerBtn">+ слой</button>
              <button class="btn ghost sm" id="delLayerBtn"${A.layer ? '' : ' style="display:none"'}>Удалить слой</button>
            </div>
          </div>
          <div class="kb-stage"><div class="kb" id="kb"></div></div>
          <div class="kb-legend">
            <span><i></i>ремап</span><span><i class="v"></i>макрос</span><span><i class="g"></i>текст</span>
            <span><i class="a"></i>запуск</span><span><i class="m"></i>слой / профиль</span>
            <span style="color:#6c7790">перетащите клавишу — ремап, Shift+перетаскивание — обмен местами</span>
          </div>
          <div class="livebar"><span class="lbl">Нажато</span><div id="liveKeys"></div><span class="grow"></span><span class="muted" id="liveInfo" style="font-size:11.5px"></span></div>
        </div>
        <aside class="inspector" id="inspector"></aside>
      </div>`;

    renderLayerSeg();
    renderKeyboard();
    renderInspector();
    renderLiveBar();

    $('#heatToggle').addEventListener('change', (e) => {
      A.heat = e.target.checked;
      renderKeyboard();
    });

    $('#addLayerBtn').addEventListener('click', addLayer);
    $('#delLayerBtn').addEventListener('click', deleteLayer);

    $('#layerSeg').addEventListener('click', (e) => {
      const b = e.target.closest('button[data-layer]');
      if (!b) return;
      A.layer = b.dataset.layer;
      if (A.sel) A.draft = draftFromRule(ruleFor(A.sel));
      renderLayerSeg();
      renderKeyboard();
      renderInspector();
      const del = $('#delLayerBtn');
      if (del) del.style.display = A.layer ? '' : 'none';
    });

    const kb = $('#kb');
    kb.addEventListener('click', (e) => {
      const key = e.target.closest('.key');
      if (!key) return;
      selectKey(key.dataset.key);
    });
    kb.addEventListener('dragstart', (e) => {
      const key = e.target.closest('.key');
      if (!key) return;
      A.drag = key.dataset.key;
      key.classList.add('dragging');
      try { e.dataTransfer.setData('text/plain', A.drag); e.dataTransfer.effectAllowed = 'link'; } catch (err) {}
    });
    kb.addEventListener('dragend', (e) => {
      const key = e.target.closest('.key');
      if (key) key.classList.remove('dragging');
      A.drag = null;
    });
    kb.addEventListener('dragover', (e) => {
      if (e.target.closest('.key') && A.drag) { e.preventDefault(); e.dataTransfer.dropEffect = 'link'; }
    });
    kb.addEventListener('drop', async (e) => {
      const to = e.target.closest('.key');
      if (!to || !A.drag) return;
      e.preventDefault();
      const from = A.drag;
      A.drag = null;
      const target = to.dataset.key;
      if (from === target) return;
      if (e.shiftKey) {
        try {
          await api('swap', { profile: A.cfg.ActiveProfile, layer: A.layer || null, a: from, b: target });
          A.draft = A.sel ? draftFromRule(ruleFor(A.sel)) : null;
          toast(keyName(from) + ' ⇄ ' + keyName(target), 'success');
        } catch (err) { toast('Ошибка обмена: ' + err.message, 'error'); }
        return;
      }
      await quickRemap(from, target);
    });
  }
  render.board = renderBoard;

  async function quickRemap(from, to) {
    const existing = ruleFor(from);
    if (existing && (existing.Action || {}).Type !== 'remap') {
      const ok = await confirmBox('Заменить действие?', 'На клавише «' + keyName(from) + '» уже настроено: ' + actionSummary(existing.Action) + '. Заменить на ремап → ' + keyName(to) + '?', 'Заменить');
      if (!ok) return;
    }
    const rule = {
      Id: existing ? existing.Id : undefined,
      From: from,
      Enabled: true,
      Note: existing ? existing.Note : '',
      Action: { Type: 'remap', Key: to },
    };
    try {
      await api('rule', { op: 'upsert', profile: A.cfg.ActiveProfile, layer: A.layer || null, rule });
      toast(keyName(from) + ' → ' + keyName(to), 'success');
    } catch (e) { toast('Не удалось сохранить ремап: ' + e.message, 'error'); }
  }

  async function addLayer() {
    const name = await ask('Новый слой', 'Слой — это дополнительный набор биндов. Активируется действием «Слой» (удержание или переключение). Например: nav, media, game.', 'nav');
    if (!name) return;
    const prof = profileObj();
    if (prof.Layers[name]) { toast('Слой уже существует', 'error'); return; }
    prof.Layers[name] = [];
    try {
      await api('layer-add', { profile: A.cfg.ActiveProfile, name });
      A.layer = name;
      toast('Слой «' + name + '» создан', 'success');
    } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
  }

  async function deleteLayer() {
    if (!A.layer) return;
    const ok = await confirmBox('Удалить слой?', 'Все бинды слоя «' + A.layer + '» будут удалены. Клавиши-переключатели останутся, но слой перестанет существовать.', 'Удалить слой');
    if (!ok) return;
    try {
      await api('layer-delete', { profile: A.cfg.ActiveProfile, name: A.layer });
      A.layer = '';
      toast('Слой удалён', 'success');
    } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
  }

  /* ---------------------------- инспектор ---------------------------- */

  function emptyDraft() {
    return {
      id: null, type: 'remap', key: '', keys: [], steps: [], text: '', path: '', args: '',
      command: '', layer: '', mode: 'hold', profile: '', note: '', enabled: true, exists: false,
    };
  }

  function draftFromRule(rule) {
    if (!rule) return emptyDraft();
    const a = rule.Action || {};
    return {
      id: rule.Id || null,
      type: (a.Type || 'remap').toLowerCase(),
      key: a.Key || '',
      keys: (a.Keys || []).slice(),
      steps: (a.Steps || []).map((s) => Object.assign({}, s)),
      text: a.Text || '',
      path: a.Path || '',
      args: a.Arguments || '',
      command: a.Command || '',
      layer: a.Layer || '',
      mode: a.Mode || 'hold',
      profile: a.Profile || '',
      note: rule.Note || '',
      enabled: rule.Enabled !== false,
      exists: true,
    };
  }

  function selectKey(id) {
    A.sel = id;
    A.draft = draftFromRule(ruleFor(id));
    document.querySelectorAll('.key.selected').forEach((k) => k.classList.remove('selected'));
    const el = document.querySelector('.key[data-key="' + id + '"]');
    if (el) { el.classList.add('selected'); el.scrollIntoView({ block: 'nearest', inline: 'nearest' }); }
    renderInspector();
  }
  R.selectKey = selectKey;

  const TYPES = [
    ['remap', 'Переназначить клавишу'],
    ['combo', 'Комбинация (Ctrl+C и т.п.)'],
    ['macro', 'Макрос (последовательность)'],
    ['text', 'Вставить текст'],
    ['app', 'Запустить программу'],
    ['run', 'Выполнить команду'],
    ['media', 'Мультимедиа / спецклавиша'],
    ['layer', 'Слой (Fn-подобный)'],
    ['profile', 'Переключить профиль'],
    ['disable', 'Отключить клавишу'],
  ];

  function keyOptions(sel) {
    return A.keys.map((k) => '<option value="' + esc(k.id) + '"' + (k.id === sel ? ' selected' : '') + '>' + esc(k.name) + '  (' + esc(k.id) + ')</option>').join('');
  }

  function field(label, inner) {
    return '<div class="field"><label>' + label + '</label>' + inner + '</div>';
  }

  function renderInspector() {
    const box = $('#inspector');
    if (!box || !A.cfg) return;
    if (!A.sel) {
      box.innerHTML = `<div class="card">
        <h3>Инспектор</h3>
        <p class="sub">Выберите клавишу на клавиатуре слева — здесь появится её действие.</p>
        <div class="empty">
          · клик по клавише — редактировать бинд<br>
          · перетащите клавишу на другую — быстрый ремап<br>
          · «Нажать» в поле клавиши — назначить физическую клавишу<br>
          · слой сверху — отдельные бинды под Fn-подобную клавишу
        </div>
        <hr class="sep">
        <div class="kbd-hint">Ремап работает на уровне системы: приложение перехватывает клавишу до того, как её получит игра или программа.</div>
      </div>`;
      return;
    }

    const d = A.draft || (A.draft = draftFromRule(ruleFor(A.sel)));
    const def = A.kmap[A.sel] || { name: A.sel };
    const stats = (A.cfg.Stats || {})[A.sel] || 0;
    const layerLabel = A.layer ? 'слой «' + A.layer + '»' : 'базовый слой';

    let body = '';

    if (d.type === 'remap' || d.type === 'media') {
      body += field('Целевая клавиша',
        '<div class="row"><input type="text" readonly id="dKeyLabel" value="' + esc(keyName(d.key)) + '">' +
        '<button class="btn ghost sm" data-act="cap-key">⌨ Нажать</button></div>' +
        '<select data-d="key" style="margin-top:8px">' +
        '<option value="">— выберите из списка —</option>' + keyOptions(d.key) + '</select>');
    } else if (d.type === 'combo') {
      const chips = (d.keys || []).map((k) => '<span class="chip">' + esc(keyName(k)) + '<button data-act="combo-del" data-id="' + esc(k) + '">×</button></span>').join('');
      body += field('Комбинация',
        '<div class="row" style="margin-bottom:8px">' + (chips || '<span class="muted" style="font-size:12px">пока пусто</span>') + '</div>' +
        '<div class="row"><button class="btn ghost sm" data-act="cap-combo">⌨ Записать комбинацию</button>' +
        '<button class="btn ghost sm" data-act="combo-clear">Очистить</button></div>' +
        '<div class="kbd-hint" style="margin-top:8px">Зажмите модификаторы и нужную клавишу — они добавятся по порядку. Затем «Готово».</div>');
    } else if (d.type === 'macro') {
      const lib = Object.keys(A.cfg.Macros || {});
      body += field('Макрос',
        '<div class="row" style="margin-bottom:8px"><span class="tag v">' + (d.steps || []).length + ' шагов</span>' +
        '<button class="btn ghost sm" data-act="rec-macro">● Записать</button>' +
        '<button class="btn ghost sm" data-act="clear-macro">Очистить</button></div>' +
        '<div class="row"><select id="macroLib"><option value="">— взять из библиотеки —</option>' +
        lib.map((n) => '<option value="' + esc(n) + '">' + esc(n) + ' (' + ((A.cfg.Macros[n].Steps || []).length) + ')</option>').join('') +
        '</select><button class="btn ghost sm" data-act="use-macro">Взять</button></div>');
    } else if (d.type === 'text') {
      body += field('Текст', '<textarea data-d="text" placeholder="Текст, который будет напечатан…">' + esc(d.text) + '</textarea>');
    } else if (d.type === 'app') {
      body += field('Путь или URL', '<input type="text" data-d="path" list="appHints" placeholder="calc.exe, steam://open/main, https://…" value="' + esc(d.path) + '">' +
        '<datalist id="appHints"><option value="calc.exe"><option value="notepad.exe"><option value="explorer.exe"><option value="steam://open/main"><option value="discord://"><option value="https://github.com"></datalist>') +
        field('Аргументы (необязательно)', '<input type="text" data-d="args" placeholder="--flag value" value="' + esc(d.args) + '">');
    } else if (d.type === 'run') {
      body += field('Команда CMD', '<input type="text" data-d="command" placeholder="shutdown /s /t 0" value="' + esc(d.command) + '">') +
        '<div class="kbd-hint">Выполняется скрыто через cmd.exe /c. Используйте осознанно.</div>';
    } else if (d.type === 'layer') {
      body += field('Имя слоя', '<input type="text" data-d="layer" placeholder="nav" value="' + esc(d.layer) + '">') +
        field('Режим', '<select data-d="mode"><option value="hold"' + (d.mode === 'hold' ? ' selected' : '') + '>Удержание (как Fn)</option>' +
        '<option value="toggle"' + (d.mode === 'toggle' ? ' selected' : '') + '>Переключение</option></select>') +
        '<div class="kbd-hint">Пока слой активен, его бинды имеют приоритет над базовыми.</div>';
    } else if (d.type === 'profile') {
      const names = Object.keys(A.cfg.Profiles || {});
      body += field('Профиль', '<select data-d="profile">' +
        names.map((n) => '<option value="' + esc(n) + '"' + (n === d.profile ? ' selected' : '') + '>' + esc(n) + '</option>').join('') + '</select>');
    } else if (d.type === 'disable') {
      body += '<div class="empty">Клавиша будет полностью проглочена системой: ни игра, ни Windows её не увидят.</div>';
    }

    body += '<div class="row" style="margin-bottom:12px"><button class="btn ghost sm" data-act="swap">⇄ Поменять местами с…</button></div>' +
      '<hr class="sep">' +
      field('Заметка', '<input type="text" data-d="note" placeholder="Например: Caps → Esc для vim" value="' + esc(d.note) + '">') +
      '<label class="switch" style="margin-bottom:14px"><input type="checkbox" data-d="enabled"' + (d.enabled ? ' checked' : '') + '><span class="track"></span>Бинд включён</label>' +
      '<div class="row end"><button class="btn" data-act="save">Сохранить</button>' +
      (d.exists ? '<button class="btn danger" data-act="delete">Удалить</button>' : '') + '</div>';

    box.innerHTML = `<div class="card">
      <div class="insp-head">
        <div>
          <div class="insp-title">${esc(def.name || A.sel)}</div>
          <div class="insp-sub">id ${esc(A.sel)} · ${layerLabel} · нажатий: ${fmt(stats)}</div>
        </div>
        <span class="tag ${d.exists ? (d.enabled ? 'g' : '') : 'v'}">${d.exists ? (d.enabled ? 'активен' : 'выключен') : 'свободна'}</span>
      </div>
      ${field('Действие', '<select data-d="type">' + TYPES.map(([v, t]) => '<option value="' + v + '"' + (v === d.type ? ' selected' : '') + '>' + t + '</option>').join('') + '</select>')}
      ${body}
    </div>`;

    const insp = box.querySelector('.card');
    insp.addEventListener('input', onInspectorInput);
    insp.addEventListener('change', onInspectorChange);
    insp.addEventListener('click', onInspectorClick);
  }
  render.inspector = renderInspector;

  function onInspectorInput(e) {
    const f = e.target.dataset.d;
    if (!f) return;
    if (f === 'enabled') A.draft.enabled = e.target.checked;
    else if (f === 'key') { A.draft.key = e.target.value; const lbl = $('#dKeyLabel'); if (lbl) lbl.value = keyName(e.target.value); }
    else A.draft[f] = e.target.value;
  }

  function onInspectorChange(e) {
    const f = e.target.dataset.d;
    if (!f) return;
    if (f === 'type') {
      A.draft.type = e.target.value;
      renderInspector();
      return;
    }
    if (f === 'enabled') { A.draft.enabled = e.target.checked; return; }
    if (f === 'key') { A.draft.key = e.target.value; renderInspector(); return; }
    A.draft[f] = e.target.value;
  }

  async function onInspectorClick(e) {
    const b = e.target.closest('[data-act]');
    if (!b) return;
    const act = b.dataset.act;
    const d = A.draft;

    if (act === 'cap-key') {
      startCapture({
        mode: 'single',
        title: 'Целевая клавиша',
        sub: 'Нажмите клавишу, на которую нужно переназначить «' + keyName(A.sel) + '». Esc — отмена.',
        onDone: (id) => { d.key = id; renderInspector(); },
      });
    } else if (act === 'cap-combo') {
      startCapture({
        mode: 'combo',
        title: 'Запись комбинации',
        sub: 'Нажимайте клавиши по порядку (например Ctrl, Shift, Esc). Затем «Готово».',
        onDone: (keys) => { d.keys = keys; renderInspector(); },
      });
    } else if (act === 'combo-del') {
      d.keys = (d.keys || []).filter((k) => k !== b.dataset.id);
      renderInspector();
    } else if (act === 'combo-clear') {
      d.keys = [];
      renderInspector();
    } else if (act === 'rec-macro') {
      startCapture({
        mode: 'macro',
        title: 'Запись макроса',
        sub: 'Нажимайте клавиши в нужном порядке. Между нажатиями сохраняются паузы. Затем «Готово».',
        onDone: (steps) => { d.steps = steps; renderInspector(); toast('Записано шагов: ' + steps.length, 'success'); },
      });
    } else if (act === 'clear-macro') {
      d.steps = [];
      renderInspector();
    } else if (act === 'use-macro') {
      const sel = $('#macroLib');
      const name = sel && sel.value;
      if (!name) { toast('Выберите макрос в списке', 'error'); return; }
      d.steps = ((A.cfg.Macros[name] || {}).Steps || []).map((s) => Object.assign({}, s));
      toast('Добавлено шагов: ' + d.steps.length, 'success');
      renderInspector();
    } else if (act === 'swap') {
      startCapture({
        mode: 'single',
        title: 'Обмен клавиш',
        sub: 'Нажмите вторую клавишу — «' + keyName(A.sel) + '» и она поменяются местами. Esc — отмена.',
        onDone: async (id) => {
          if (!id || id === A.sel) return;
          try {
            await api('swap', { profile: A.cfg.ActiveProfile, layer: A.layer || null, a: A.sel, b: id });
            A.draft = draftFromRule(ruleFor(A.sel));
            renderInspector();
            toast(keyName(A.sel) + ' ⇄ ' + keyName(id), 'success');
          } catch (err) { toast('Ошибка обмена: ' + err.message, 'error'); }
        },
      });
    } else if (act === 'save') {
      await saveRule();
    } else if (act === 'delete') {
      const ok = await confirmBox('Удалить бинд?', 'Действие клавиши «' + keyName(A.sel) + '» вернётся к стандартному.', 'Удалить');
      if (!ok) return;
      try {
        await api('rule', { op: 'delete', profile: A.cfg.ActiveProfile, layer: A.layer || null, id: d.id, from: A.sel });
        A.draft = emptyDraft();
        toast('Бинд удалён', 'success');
        renderInspector();
      } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
    }
  }

  async function saveRule() {
    const d = A.draft;
    if (!A.sel) return;
    if ((d.type === 'remap' || d.type === 'media') && !d.key) { toast('Выберите целевую клавишу', 'error'); return; }
    if (d.type === 'combo' && (!d.keys || d.keys.length === 0)) { toast('Запишите комбинацию', 'error'); return; }
    if (d.type === 'layer' && !d.layer) { toast('Укажите имя слоя', 'error'); return; }
    if (d.type === 'app' && !d.path) { toast('Укажите путь или URL', 'error'); return; }

    const action = {
      Type: d.type,
      Key: d.key,
      Keys: d.keys,
      Steps: d.steps,
      Text: d.text,
      Path: d.path,
      Arguments: d.args,
      Command: d.command,
      Layer: d.layer,
      Mode: d.mode,
      Profile: d.profile,
    };
    const rule = { Id: d.id || undefined, From: A.sel, Enabled: !!d.enabled, Note: d.note, Action: action };
    try {
      await api('rule', { op: 'upsert', profile: A.cfg.ActiveProfile, layer: A.layer || null, rule });
      toast('Сохранено: ' + keyName(A.sel) + ' — ' + actionSummary(action), 'success');
    } catch (e) {
      toast('Не удалось сохранить: ' + e.message, 'error');
    }
  }

  /* ------------------------------ бинды ------------------------------ */

  function renderRules() {
    if (!A.cfg) return;
    const p = section('rules');
    const prof = profileObj();
    const layers = Object.keys((prof && prof.Layers) || {});
    const rules = rulesForLayer(prof, A.layer);
    const stats = A.cfg.Stats || {};

    let rows = '';
    if (!rules.length) {
      rows = '<tr><td colspan="5"><div class="empty-table">Пока пусто. Нажмите «+ Новый бинд» или выберите клавишу на экране «Клавиатура».</div></td></tr>';
    } else {
      rules.slice().sort((a, b) => (stats[b.From] || 0) - (stats[a.From] || 0)).forEach((r) => {
        const statsN = stats[r.From] || 0;
        rows += '<tr>' +
          '<td><span class="keyname">' + esc(keyName(r.From)) + '</span> <span class="muted" style="font-size:11px;font-family:Consolas">' + esc(r.From) + '</span></td>' +
          '<td class="act">' + esc(actionSummary(r.Action)) + '</td>' +
          '<td class="note">' + esc(r.Note || '') + (statsN ? '<br><span style="font-size:11px;color:#5f6b82">нажатий: ' + fmt(statsN) + '</span>' : '') + '</td>' +
          '<td><label class="switch"><input type="checkbox" data-act="toggle-rule" data-id="' + esc(r.Id) + '"' + (r.Enabled ? ' checked' : '') + '><span class="track"></span></label></td>' +
          '<td><div class="ops"><button class="btn ghost sm" data-act="edit-rule" data-from="' + esc(r.From) + '">Изменить</button>' +
          '<button class="btn danger sm" data-act="del-rule" data-id="' + esc(r.Id) + '" data-from="' + esc(r.From) + '">×</button></div></td>' +
          '</tr>';
      });
    }

    p.innerHTML = `<div class="card" style="margin-bottom:14px">
      <div class="row between" style="margin-bottom:10px">
        <div><h3>Быстрые действия</h3>
        <p class="sub" style="margin:3px 0 0">Шаблон → клавиша. Steam, Discord и сайты открываются через свои ссылки, если установлены.</p></div>
      </div>
      <div class="row" id="quickRow">
        <button class="btn ghost sm" data-quick="steam">▶ Steam</button>
        <button class="btn ghost sm" data-quick="discord">▶ Discord</button>
        <button class="btn ghost sm" data-quick="browser">🌐 Сайт</button>
        <button class="btn ghost sm" data-quick="calc">🧮 Калькулятор</button>
        <button class="btn ghost sm" data-quick="explorer">📁 Проводник</button>
        <button class="btn ghost sm" data-quick="notepad">📝 Блокнот</button>
        <button class="btn ghost sm" data-quick="screenshot">✂ Скриншот</button>
        <button class="btn ghost sm" data-quick="text">⌨ Вставить текст</button>
        <button class="btn ghost sm" data-quick="command">⌘ Команда</button>
      </div>
      <div class="kbd-hint" style="margin-top:10px">Пример: «Steam» → выбрать клавишу <b>F8</b> → нажать F8, и Steam запускается. Всё это видно в таблице ниже и работает в любой программе.</div>
    </div>
    <div class="card">
      <div class="row between" style="margin-bottom:6px">
        <div><h3>Бинды · ${esc(A.cfg.ActiveProfile)}</h3>
        <p class="sub">Физическая клавиша → действие. Работает глобально, в любом приложении.</p></div>
        <div class="row">
          <select id="rulesLayer">
            <option value="">Базовый слой</option>
            ${layers.map((l) => '<option value="' + esc(l) + '"' + (A.layer === l ? ' selected' : '') + '>' + esc(l) + '</option>').join('')}
          </select>
          <button class="btn" id="addBindBtn">+ Новый бинд</button>
        </div>
      </div>
      <div style="overflow:auto">
        <table class="table">
          <thead><tr><th>Клавиша</th><th>Действие</th><th>Заметка</th><th>Вкл</th><th style="text-align:right">Операции</th></tr></thead>
          <tbody>${rows}</tbody>
        </table>
      </div>
      ${A.layer ? '<div class="kbd-hint" style="margin-top:12px">Слой «' + esc(A.layer) + '» активен только когда сработал переключатель слоя. Бинды базового слоя при этом продолжают работать, если клавиша не переопределена в слое.</div>' : ''}
    </div>`;

    $('#rulesLayer').addEventListener('change', (e) => {
      A.layer = e.target.value;
      renderRules();
    });
    $('#addBindBtn').addEventListener('click', () => pickKey((id) => {
      A.sel = id;
      A.draft = draftFromRule(ruleFor(id));
      R.switchTab('board');
    }));

    const quick = p.querySelector('#quickRow');
    if (quick) quick.addEventListener('click', (e) => {
      const b = e.target.closest('[data-quick]');
      if (b) quickBind(b.dataset.quick);
    });

    p.querySelector('tbody').addEventListener('change', async (e) => {
      const cb = e.target.closest('[data-act="toggle-rule"]');
      if (!cb) return;
      try { await api('rule', { op: 'toggle', profile: A.cfg.ActiveProfile, layer: A.layer || null, id: cb.dataset.id }); } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
    });

    p.querySelector('tbody').addEventListener('click', async (e) => {
      const edit = e.target.closest('[data-act="edit-rule"]');
      if (edit) {
        A.sel = edit.dataset.from;
        A.draft = draftFromRule(ruleFor(A.sel));
        R.switchTab('board');
        return;
      }
      const del = e.target.closest('[data-act="del-rule"]');
      if (del) {
        const ok = await confirmBox('Удалить бинд?', 'Клавиша «' + keyName(del.dataset.from) + '» вернётся к стандартному поведению.', 'Удалить');
        if (!ok) return;
        try {
          await api('rule', { op: 'delete', profile: A.cfg.ActiveProfile, layer: A.layer || null, id: del.dataset.id, from: del.dataset.from });
          toast('Бинд удалён', 'success');
        } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
      }
    });
  }
  render.rules = renderRules;

  /* ---------------------------- выбор клавиши ---------------------------- */

  function pickKey(cb) {
    openModal('<h3>Выберите клавишу</h3><p>Физическая клавиша, к которой привяжем действие.</p>' +
      '<input id="pkSearch" type="search" placeholder="Поиск: A, F5, Space, Num…" autocomplete="off">' +
      '<div id="pkList" style="display:flex;flex-wrap:wrap;gap:6px;max-height:330px;overflow:auto;margin-top:12px"></div>' +
      '<div class="row"><button class="btn ghost" id="pkCancel">Отмена</button></div>');
    const list = $('#pkList');
    const draw = (q) => {
      const query = (q || '').toLowerCase();
      list.innerHTML = A.keys.filter((k) => !query || k.name.toLowerCase().includes(query) || k.id.toLowerCase().includes(query))
        .map((k) => '<button class="btn ghost sm" data-k="' + esc(k.id) + '">' + esc(k.name) + '</button>').join('') ||
        '<span class="muted">Ничего не найдено</span>';
    };
    draw('');
    $('#pkSearch').addEventListener('input', (e) => draw(e.target.value));
    $('#pkSearch').focus();
    list.addEventListener('click', (e) => {
      const b = e.target.closest('[data-k]');
      if (!b) return;
      closeModal();
      cb(b.dataset.k);
    });
    $('#pkCancel').addEventListener('click', closeModal);
  }
  R.pickKey = pickKey;

  /* -------------------------- быстрые шаблоны -------------------------- */

  const QUICK = {
    steam: { path: 'steam://open/main', note: 'Steam' },
    discord: { path: 'discord://', note: 'Discord' },
    calc: { path: 'calc.exe', note: 'Калькулятор' },
    explorer: { path: 'explorer.exe', note: 'Проводник' },
    notepad: { path: 'notepad.exe', note: 'Блокнот' },
  };

  async function quickBind(kind) {
    if (kind === 'command') {
      const cmd = await ask('Команда', 'Что выполнить? Например: shutdown /s /t 0 или start steam://open/main', 'start steam://open/main');
      if (!cmd) return;
      pickKey((id) => createQuick(id, { Type: 'run', Command: cmd }, 'команда'));
      return;
    }
    if (kind === 'text') {
      const text = await ask('Текст', 'Какой текст печатать при нажатии клавиши? Например: ваша почта или приветствие', '');
      if (!text) return;
      pickKey((id) => createQuick(id, { Type: 'text', Text: text }, 'текст'));
      return;
    }
    if (kind === 'browser') {
      const url = await ask('Сайт', 'Адрес, который откроется при нажатии:', 'https://');
      if (!url || url === 'https://') return;
      pickKey((id) => createQuick(id, { Type: 'app', Path: url }, 'сайт'));
      return;
    }
    if (kind === 'screenshot') {
      const steps = [
        { Key: 'E05B', Down: true, DelayMs: 20 },
        { Key: '2A', Down: true, DelayMs: 20 },
        { Key: '1F', Down: true, DelayMs: 40 },
        { Key: '1F', Down: false, DelayMs: 20 },
        { Key: '2A', Down: false, DelayMs: 20 },
        { Key: 'E05B', Down: false, DelayMs: 20 },
      ];
      pickKey((id) => createQuick(id, { Type: 'macro', Steps: steps }, 'скриншот'));
      return;
    }
    const meta = QUICK[kind];
    if (!meta) return;
    pickKey((id) => createQuick(id, { Type: 'app', Path: meta.path }, meta.note));
  }

  async function createQuick(id, action, label) {
    try {
      await api('rule', {
        op: 'upsert', profile: A.cfg.ActiveProfile, layer: A.layer || null,
        rule: { From: id, Enabled: true, Note: label, Action: action },
      });
      toast(keyName(id) + ' → ' + label, 'success');
    } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
  }
  R.quickBind = quickBind;

  R.emptyDraft = emptyDraft;
  R.draftFromRule = draftFromRule;
})();
