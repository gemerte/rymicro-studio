/* RYMICRO Studio — подсветка: боковые панели, превью, OpenRGB, HID-консоль */
(() => {
  'use strict';
  const R = window.RY;
  const { A, $, esc, clamp, api, apiGet, toast, stripStyle, openModal, closeModal, ask, confirmBox, render, section } = R;

  const EFFECTS = [
    ['off', 'Выключено'],
    ['static', 'Статичный цвет'],
    ['gradient', 'Градиент (переливается)'],
    ['breathing', 'Дыхание'],
    ['rainbow', 'Радуга'],
    ['wave', 'Волна'],
    ['reactive', 'Реакция на нажатия'],
  ];

  const PRESETS = {
    neon: { L: ['#00e5ff', '#ff2d95'], R: ['#00e5ff', '#ff2d95'], fx: 'gradient', kb: '#00e5ff' },
    cyber: { L: ['#7c4dff', '#00e5ff'], R: ['#00e5ff', '#7c4dff'], fx: 'wave', kb: '#7c4dff' },
    sunset: { L: ['#ff8a00', '#ff2d95'], R: ['#ff2d95', '#ff8a00'], fx: 'gradient', kb: '#ff8a00' },
    ice: { L: ['#aef6ff', '#aef6ff'], R: ['#aef6ff', '#aef6ff'], fx: 'static', kb: '#aef6ff' },
    toxic: { L: ['#37e6a4', '#c6ff00'], R: ['#c6ff00', '#37e6a4'], fx: 'breathing', kb: '#37e6a4' },
    vapor: { L: ['#ff77ff', '#00e5ff'], R: ['#ff77ff', '#00e5ff'], fx: 'rainbow', kb: '#ff77ff' },
  };

  function logRgb(msg, ok) {
    A.rgbLog.push({ t: new Date().toLocaleTimeString('ru-RU'), msg, ok });
    if (A.rgbLog.length > 80) A.rgbLog.shift();
    const box = $('#rgbLog');
    if (box) {
      box.innerHTML = A.rgbLog.map((l) => '<div class="' + (l.ok ? 'ok' : 'err') + '">[' + esc(l.t) + '] ' + esc(l.msg) + '</div>').join('');
      box.scrollTop = box.scrollHeight;
    }
  }

  function sideBlock(side, s) {
    const title = side === 'SideLeft' ? 'Левая панель' : 'Правая панель';
    const c = s.Colors || [];
    return `<div class="card">
      <div class="row between" style="margin-bottom:10px">
        <div><h3>${title}</h3><p class="sub" style="margin:2px 0 0">Светящаяся полоса на торце клавиатуры</p></div>
        <label class="switch"><input type="checkbox" data-side="${side}" data-prop="Enabled"${s.Enabled ? ' checked' : ''}><span class="track"></span></label>
      </div>
      <div class="field"><label>Эффект</label>
        <select data-side="${side}" data-prop="Effect">${EFFECTS.map(([v, t]) => '<option value="' + v + '"' + (v === s.Effect ? ' selected' : '') + '>' + t + '</option>').join('')}</select>
      </div>
      <div class="field"><label>Цвета</label>
        <div class="color-row">
          <input type="color" data-color="${side}" data-idx="0" value="${esc(c[0] || '#00e5ff')}">
          <input type="color" data-color="${side}" data-idx="1" value="${esc(c[1] || c[0] || '#ff2d95')}">
          <button class="btn ghost sm" data-act="swap-colors" data-side="${side}">⇄ Поменять</button>
        </div>
      </div>
      <div class="field"><label>Скорость</label>
        <div class="row"><input type="range" data-side="${side}" data-prop="Speed" min="1" max="10" value="${s.Speed || 5}"><span class="range-val" data-val="${side}-Speed">${s.Speed || 5}</span></div>
      </div>
      <div class="field"><label>Яркость</label>
        <div class="row"><input type="range" data-side="${side}" data-prop="Brightness" min="0" max="100" value="${s.Brightness == null ? 80 : s.Brightness}"><span class="range-val" data-val="${side}-Brightness">${s.Brightness == null ? 80 : s.Brightness}</span></div>
      </div>
      ${side === 'SideRight' ? '<label class="switch"><input type="checkbox" data-side="SideRight" data-prop="Mirror"' + (s.Mirror ? ' checked' : '') + '><span class="track"></span>Зеркалить левую</label>' : ''}
    </div>`;
  }

  function renderLighting() {
    if (!A.cfg) return;
    const p = section('lighting');
    const L = A.cfg.Lighting;
    const ls = L.SideLeft || {}, rs = L.SideRight || {};

    const cells = [];
    for (let i = 0; i < 60; i++) {
      const wide = i % 14 === 0 ? ' sp' : (i === 59 ? ' wide' : '');
      cells.push('<i class="lit' + wide + '"></i>');
    }

    p.innerHTML = `
      <div class="light-grid">
        <div class="card wide">
          <div class="row between" style="margin-bottom:10px">
            <div><h3>Боковые панели — превью</h3><p class="sub" style="margin:2px 0 0">Так будет выглядеть подсветка торцов. Схему можно сохранить как PNG или записать WebM.</p></div>
            <div class="row"><span class="tag" style="background:rgba(255,200,87,.1);border-color:rgba(255,200,87,.3);color:#ffd894">схема</span></div>
          </div>
          <div class="preview-stage" id="pvStage">
            <div class="side-preview" id="pvLeft" data-fx="${esc(ls.Enabled ? ls.Effect : 'off')}" style="${stripStyle(ls)}"></div>
            <div class="kb-mini" id="pvKb" style="--kb:${esc(L.KeyboardColor || '#00e5ff')}">${cells.join('')}</div>
            <div class="side-preview" id="pvRight" data-fx="${esc(rs.Enabled ? rs.Effect : 'off')}" style="${stripStyle(rs)}"></div>
          </div>
          <div class="preview-actions">
            <button class="btn ghost sm" data-act="png">Скачать PNG</button>
            <button class="btn ghost sm" data-act="webm">Записать WebM (5 сек)</button>
            <span class="grow"></span>
            ${Object.keys(PRESETS).map((k) => '<button class="btn ghost sm" data-preset="' + k + '">' + k + '</button>').join('')}
          </div>
        </div>

        ${sideBlock('SideLeft', ls)}
        ${sideBlock('SideRight', rs)}

        <div class="card">
          <h3>Подсветка клавиш</h3>
          <p class="sub">Цвет и эффект в превью. На железо применяется через OpenRGB, если клавиатура поддерживается.</p>
          <div class="field"><label>Эффект</label>
            <select data-kb="KeyboardEffect">${EFFECTS.map(([v, t]) => '<option value="' + v + '"' + (v === L.KeyboardEffect ? ' selected' : '') + '>' + t + '</option>').join('')}</select>
          </div>
          <div class="field"><label>Цвет</label>
            <div class="color-row"><input type="color" data-kb="KeyboardColor" value="${esc(L.KeyboardColor || '#00e5ff')}">
            <span class="muted" style="font-size:12px">основной цвет клавиш</span></div>
          </div>
          <div class="kbd-hint">Клавиатура RY Micro — бюджетная модель: подсветка клавиш обычно управляется прошивкой через FN-комбинации.</div>
        </div>

        <div class="card">
          <h3>OpenRGB</h3>
          <p class="sub">Если клавиатуру поддерживает OpenRGB, цвета можно заливать напрямую. Запустите OpenRGB с SDK-сервером (по умолчанию порт 6742).</p>
          <div class="row" style="margin-bottom:8px">
            <input type="text" data-rgb="Host" value="${esc(L.OpenRgb.Host || '127.0.0.1')}" style="width:130px" title="Хост OpenRGB SDK">
            <input type="number" data-rgb="Port" value="${Number(L.OpenRgb.Port || 6742)}" style="width:82px" title="Порт SDK">
            <input type="number" data-rgb="Device" value="${Number(L.OpenRgb.Device || 0)}" style="width:74px" title="Индекс устройства">
            <input type="number" data-rgb="LedCount" value="${Number(L.OpenRgb.LedCount || 0)}" style="width:96px" title="Количество LED">
          </div>
          <div class="row" style="margin-bottom:10px">
            <button class="btn ghost sm" data-act="rgb-probe">Проверить</button>
            <button class="btn sm" data-act="rgb-apply">Залить цвет</button>
            <button class="btn ghost sm" data-act="rgb-rainbow">Радуга</button>
            <button class="btn ghost sm" data-act="rgb-off">Выключить</button>
          </div>
          <div class="console" id="rgbLog" style="height:120px">${A.rgbLog.map((l) => '<div class="' + (l.ok ? 'ok' : 'err') + '">[' + esc(l.t) + '] ' + esc(l.msg) + '</div>').join('')}</div>
          <div class="kbd-hint" style="margin-top:10px">Порядок полей: хост, порт, индекс устройства, количество LED. Если OpenRGB не запущен — «Проверить» честно покажет ошибку.</div>
        </div>

        <div class="card">
          <h3>HID-консоль (экспериментально)</h3>
          <p class="sub">Отправка feature/output-репортов напрямую в устройство. Для энтузиастов: можно попробовать найти заводской протокол подсветки.</p>
          <div class="row" style="margin-bottom:8px">
            <input type="text" id="hidVid" value="352D" style="width:80px" title="VID (hex)">
            <input type="text" id="hidPid" value="2380" style="width:80px" title="PID (hex)">
            <input type="number" id="hidReport" value="0" min="0" max="255" style="width:76px" title="Report ID">
            <select id="hidType" style="width:110px"><option value="feature">feature</option><option value="output">output</option></select>
            <button class="btn ghost sm" data-act="hid-refresh">Найти устройства</button>
          </div>
          <div class="row" style="margin-bottom:10px">
            <input type="text" id="hidHex" class="mono" placeholder="05 00 01 FF 00 …" style="flex:1;min-width:180px">
            <button class="btn sm" data-act="hid-send">Отправить</button>
          </div>
          <div class="hid-list" id="hidList"></div>
          <div class="console" id="hidLog" style="height:96px;margin-top:10px"></div>
          <hr class="sep">
          <div class="row between" style="margin-bottom:8px"><span class="muted" style="font-size:12px">Сохранённые пакеты</span><button class="btn ghost sm" data-act="hid-save">+ Сохранить</button></div>
          <div id="snippets" style="display:flex;flex-direction:column;gap:6px">${snippetRows()}</div>
        </div>

        <div class="card wide">
          <h3>Как менять подсветку именно на этой клавиатуре</h3>
          <p class="sub">Честно: у бюджетных моделей вроде RY Micro боковые полосы чаще всего подключены к тому же контроллеру, что и клавиши, и управляются прошивкой. Программного HID-протокола у них обычно нет.</p>
          <div class="row" style="align-items:flex-start;gap:24px;flex-wrap:wrap">
            <div style="flex:1;min-width:280px">
              <div class="kbd-hint" style="margin-bottom:10px"><b>1.</b> Попробуйте заводские FN-комбинации — они меняют режим мгновенно и работают без софта.</div>
              <div class="kbd-hint" style="margin-bottom:10px"><b>2.</b> Если OpenRGB видит устройство — включите SDK-сервер и заливайте цвет из блока выше.</div>
              <div class="kbd-hint"><b>3.</b> Если хочется копать глубже — HID-консоль и сохранённые пакеты помогут записать удачную последовательность.</div>
            </div>
            <div style="flex:1;min-width:280px">
              <table class="table">
                <thead><tr><th>Комбинация</th><th>Обычно делает</th></tr></thead>
                <tbody>
                  <tr><td><span class="keyname">FN + Ins</span></td><td class="note">смена режима подсветки</td></tr>
                  <tr><td><span class="keyname">FN + Del</span></td><td class="note">следующий эффект</td></tr>
                  <tr><td><span class="keyname">FN + ↑ / ↓</span></td><td class="note">яркость</td></tr>
                  <tr><td><span class="keyname">FN + ← / →</span></td><td class="note">скорость эффекта</td></tr>
                  <tr><td><span class="keyname">FN + 1…5</span></td><td class="note">цветовые пресеты (зависит от прошивки)</td></tr>
                  <tr><td><span class="keyname">FN + Esc</span></td><td class="note">сброс подсветки</td></tr>
                  <tr><td><span class="keyname">FN + Win</span></td><td class="note">блокировка клавиши Win</td></tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>`;

    /* -------- обработчики (навешиваются один раз на секцию) -------- */

    if (!p.dataset.wired) {
    p.dataset.wired = '1';

    p.addEventListener('change', (e) => {
      const L = A.cfg.Lighting;
      const t = e.target;
      if (t.dataset.side) {
        const s = L[t.dataset.side];
        const prop = t.dataset.prop;
        if (t.type === 'checkbox') s[prop] = t.checked;
        else if (t.type === 'range') s[prop] = Number(t.value);
        else s[prop] = t.value;
        if (t.dataset.side === 'SideRight' && prop === 'Mirror' && t.checked) {
          const copy = JSON.parse(JSON.stringify(L.SideLeft));
          copy.Mirror = true;
          L.SideRight = copy;
        }
        saveLighting();
        renderLighting();
        return;
      }
      if (t.dataset.kb) {
        L[t.dataset.kb] = t.value;
        saveLighting();
        renderLighting();
        return;
      }
      if (t.dataset.rgb) {
        const k = t.dataset.rgb;
        L.OpenRgb[k] = t.type === 'number' ? Number(t.value) : t.value;
        saveLighting();
        return;
      }
      if (t.dataset.color) {
        const s = L[t.dataset.color];
        s.Colors[Number(t.dataset.idx)] = t.value;
        saveLighting();
        updatePreview(t.dataset.color);
      }
    });

    p.addEventListener('input', (e) => {
      const L = A.cfg.Lighting;
      const t = e.target;
      if (t.dataset.side && t.type === 'range') {
        const s = L[t.dataset.side];
        s[t.dataset.prop] = Number(t.value);
        const val = p.querySelector('[data-val="' + t.dataset.side + '-' + t.dataset.prop + '"]');
        if (val) val.textContent = t.value;
        updatePreview(t.dataset.side);
      }
      if (t.dataset.color) {
        const s = L[t.dataset.color];
        s.Colors[Number(t.dataset.idx)] = t.value;
        updatePreview(t.dataset.color);
      }
    });

    p.addEventListener('click', async (e) => {
      const L = A.cfg.Lighting;
      const b = e.target.closest('[data-act],[data-preset]');
      if (!b) return;

      if (b.dataset.preset) {
        const pr = PRESETS[b.dataset.preset];
        L.SideLeft.Effect = pr.fx; L.SideLeft.Enabled = true; L.SideLeft.Colors = pr.L.slice();
        L.SideRight.Effect = pr.fx; L.SideRight.Enabled = true; L.SideRight.Colors = pr.R.slice();
        L.KeyboardColor = pr.kb;
        saveLighting();
        renderLighting();
        toast('Пресет: ' + b.dataset.preset, 'success');
        return;
      }

      const act = b.dataset.act;

      if (act === 'swap-colors') {
        const s = L[b.dataset.side];
        s.Colors = [s.Colors[1] || s.Colors[0], s.Colors[0] || s.Colors[1]];
        saveLighting();
        renderLighting();
      } else if (act === 'png') {
        exportPng();
      } else if (act === 'webm') {
        recordWebm();
      } else if (act === 'rgb-probe') {
        logRgb('Проверяю OpenRGB на ' + L.OpenRgb.Host + ':' + L.OpenRgb.Port + '…', true);
        try {
          const r = await api('openrgb', { op: 'probe', host: L.OpenRgb.Host, port: L.OpenRgb.Port, device: L.OpenRgb.Device, ledCount: L.OpenRgb.LedCount });
          if (r.ok) logRgb('Устройств найдено: ' + r.count + '. Индексы от 0 до ' + Math.max(0, r.count - 1) + '.', true);
          else logRgb('Ошибка: ' + (r.error || 'нет ответа'), false);
        } catch (err) { logRgb('Ошибка запроса: ' + err.message, false); }
      } else if (act === 'rgb-apply') {
        try {
          const r = await api('openrgb', { op: 'apply', host: L.OpenRgb.Host, port: L.OpenRgb.Port, device: L.OpenRgb.Device, ledCount: L.OpenRgb.LedCount, color: L.KeyboardColor });
          logRgb(r.message || 'Готово', !!(r.message || '').startsWith('OK'));
        } catch (err) { logRgb('Ошибка: ' + err.message, false); }
      } else if (act === 'rgb-rainbow') {
        try {
          const r = await api('openrgb', { op: 'rainbow', host: L.OpenRgb.Host, port: L.OpenRgb.Port, device: L.OpenRgb.Device, ledCount: L.OpenRgb.LedCount });
          logRgb(r.message || 'Готово', !!(r.message || '').startsWith('OK'));
        } catch (err) { logRgb('Ошибка: ' + err.message, false); }
      } else if (act === 'rgb-off') {
        try {
          const r = await api('openrgb', { op: 'off', host: L.OpenRgb.Host, port: L.OpenRgb.Port, device: L.OpenRgb.Device, ledCount: L.OpenRgb.LedCount });
          logRgb(r.message || 'Готово', !!(r.message || '').startsWith('OK'));
        } catch (err) { logRgb('Ошибка: ' + err.message, false); }
      } else if (act === 'hid-refresh') {
        await loadHid();
      } else if (act === 'hid-send') {
        await sendHid();
      } else if (act === 'hid-save') {
        const name = await ask('Сохранить пакет', 'Название для сохранения HEX-пакета:', 'Пакет ' + (L.HidSnippets.length + 1));
        if (!name) return;
        L.HidSnippets.push({ Name: name, Hex: ($('#hidHex') || {}).value || '', ReportId: Number(($('#hidReport') || {}).value || 0) });
        saveLighting();
        renderLighting();
        toast('Пакет сохранён', 'success');
      } else if (act === 'hid-apply') {
        const sn = L.HidSnippets[Number(b.dataset.idx)];
        if (!sn) return;
        $('#hidHex').value = sn.Hex;
        $('#hidReport').value = sn.ReportId || 0;
        await sendHid();
      } else if (act === 'hid-del') {
        L.HidSnippets.splice(Number(b.dataset.idx), 1);
        saveLighting();
        renderLighting();
      }
    });
    }

    loadHid(true);
  }
  render.lighting = renderLighting;

  function snippetRows() {
    const list = (A.cfg.Lighting.HidSnippets || []);
    if (!list.length) return '<span class="muted" style="font-size:12px">Пока нет сохранённых пакетов</span>';
    return list.map((s, i) => '<div class="snippet"><span class="mono">' + esc(s.Hex || '—') + '</span>' +
      '<button class="btn ghost sm" data-act="hid-apply" data-idx="' + i + '">Отправить</button>' +
      '<button class="btn danger sm" data-act="hid-del" data-idx="' + i + '">×</button></div>').join('');
  }

  function saveLighting() {
    clearTimeout(A.lightingTimer);
    A.lightingTimer = setTimeout(async () => {
      try { await api('lighting', { lighting: A.cfg.Lighting }); }
      catch (e) { toast('Не удалось сохранить подсветку: ' + e.message, 'error'); }
    }, 300);
  }

  function updatePreview(side) {
    if (!A.cfg) return;
    const L = A.cfg.Lighting;
    const s = L[side];
    const el = $('#' + (side === 'SideLeft' ? 'pvLeft' : 'pvRight'));
    if (el && s) {
      el.dataset.fx = s.Enabled ? s.Effect : 'off';
      el.setAttribute('style', stripStyle(s));
    }
    const kb = $('#pvKb');
    if (kb) kb.style.setProperty('--kb', L.KeyboardColor || '#00e5ff');
  }

  async function loadHid(quiet) {
    try {
      const r = await apiGet('hid');
      A.hidDevices = r.devices || [];
      const box = $('#hidList');
      if (box) {
        const mine = A.hidDevices.filter((d) => d.vid === 0x352D && d.pid === 0x2380);
        const show = mine.length ? mine : A.hidDevices.slice(0, 14);
        box.innerHTML = show.map((d) => '<div class="hid-item"><span><b>' + d.vid.toString(16).toUpperCase().padStart(4, '0') + ':' + d.pid.toString(16).toUpperCase().padStart(4, '0') + '</b> ' + esc(d.product || d.manufacturer || 'HID') + '</span></div>').join('') ||
          '<div class="muted" style="font-size:12px">HID-устройства не найдены</div>';
        if (mine.length && !quiet) logHid('Найдена клавиатура: ' + (mine[0].product || 'RY Micro') + ' (VID 352D:PID 2380)', true);
      }
    } catch (e) {
      if (!quiet) logHid('Не удалось получить список HID: ' + e.message, false);
    }
  }

  function logHid(msg, ok) {
    const box = $('#hidLog');
    if (!box) return;
    const line = document.createElement('div');
    line.className = ok ? 'ok' : 'err';
    line.textContent = '[' + new Date().toLocaleTimeString('ru-RU') + '] ' + msg;
    box.appendChild(line);
    box.scrollTop = box.scrollHeight;
  }

  async function sendHid() {
    const hex = ($('#hidHex') || {}).value || '';
    if (!hex.trim()) { logHid('Введите HEX-пакет', false); return; }
    const body = {
      vid: ($('#hidVid') || {}).value || '352D',
      pid: ($('#hidPid') || {}).value || '2380',
      hex,
      reportId: Number(($('#hidReport') || {}).value || 0),
      output: (($('#hidType') || {}).value || 'feature') === 'output',
    };
    try {
      const r = await api('hid-send', body);
      logHid(r.message || (r.ok ? 'OK' : 'Ошибка'), !!r.ok);
    } catch (e) { logHid('Ошибка запроса: ' + e.message, false); }
  }

  /* ---------------------------- экспорт превью ---------------------------- */

  function rounded(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  function mix(a, b, t) {
    return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
  }

  function drawPreview(canvas, t) {
    const L = A.cfg.Lighting;
    const ctx = canvas.getContext('2d');
    const W = canvas.width, H = canvas.height;
    ctx.clearRect(0, 0, W, H);
    const bg = ctx.createLinearGradient(0, 0, 0, H);
    bg.addColorStop(0, '#070a11');
    bg.addColorStop(1, '#0b1019');
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, W, H);

    const glow = ctx.createRadialGradient(W / 2, H * 0.75, 20, W / 2, H * 0.75, W * 0.7);
    glow.addColorStop(0, 'rgba(0,229,255,0.10)');
    glow.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = glow;
    ctx.fillRect(0, 0, W, H);

    const drawStrip = (x, w, s) => {
      if (!s || !s.Enabled || s.Effect === 'off') {
        ctx.fillStyle = '#0d1118';
        rounded(ctx, x, H * 0.18, w, H * 0.64, w / 2);
        ctx.fill();
        return;
      }
      const c1 = R.hexToRgb((s.Colors || [])[0] || '#00e5ff');
      const c2 = R.hexToRgb((s.Colors || [])[1] || (s.Colors || [])[0] || '#ff2d95');
      const phase = (Math.sin(t * (0.6 + (s.Speed || 5) / 10) * Math.PI) + 1) / 2;
      const breathe = s.Effect === 'breathing' ? 0.55 + 0.45 * Math.sin(t * 2) : 1;
      const bright = (s.Brightness == null ? 80 : s.Brightness) / 100;
      const a = s.Effect === 'rainbow' ? [255, 40, 160] : c1;
      const b = s.Effect === 'rainbow' ? [0, 229, 255] : c2;
      const grad = ctx.createLinearGradient(0, H * 0.18, 0, H * 0.82);
      grad.addColorStop(0, 'rgb(' + mix(a, b, phase).map(Math.round).join(',') + ')');
      grad.addColorStop(1, 'rgb(' + mix(b, a, phase).map(Math.round).join(',') + ')');
      ctx.save();
      ctx.globalAlpha = clamp(bright * breathe, 0.05, 1);
      ctx.shadowColor = 'rgb(' + a.map(Math.round).join(',') + ')';
      ctx.shadowBlur = 42 * bright;
      ctx.fillStyle = grad;
      rounded(ctx, x, H * 0.18, w, H * 0.64, w / 2);
      ctx.fill();
      ctx.restore();
    };

    const sw = Math.max(18, W * 0.032);
    drawStrip(W * 0.075, sw, L.SideLeft);
    drawStrip(W - W * 0.075 - sw, sw, L.SideRight);

    const kx = W * 0.19, kw = W * 0.62, ky = H * 0.30, kh = H * 0.42;
    ctx.fillStyle = '#111825';
    rounded(ctx, kx, ky, kw, kh, 16);
    ctx.fill();
    ctx.strokeStyle = 'rgba(255,255,255,0.09)';
    ctx.lineWidth = 1.5;
    ctx.stroke();

    const kb = R.hexToRgb(L.KeyboardColor || '#00e5ff');
    const pulse = L.KeyboardEffect === 'breathing' ? 0.55 + 0.45 * Math.sin(t * 2.2) : 1;
    const hueShift = L.KeyboardEffect === 'rainbow' ? (t * 60) % 360 : 0;
    const cols = 14, rows = 5;
    const pad = kw * 0.035;
    const cellW = (kw - pad * 2) / cols;
    const cellH = (kh - pad * 2) / rows;
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        let col = kb;
        if (hueShift) {
          const hh = (hueShift + c * 12 + r * 20) % 360;
          const rgb = R.hexToRgb(hslHex(hh));
          col = rgb;
        }
        ctx.fillStyle = 'rgba(' + col.r + ',' + col.g + ',' + col.b + ',' + (0.36 + 0.55 * pulse) + ')';
        const wKey = cellW * (r === 4 && c === 3 ? 5 : 0.86);
        rounded(ctx, kx + pad + c * cellW, ky + pad + r * cellH, wKey, cellH * 0.78, 4);
        ctx.fill();
        if (r === 4 && c === 3) c += 4;
      }
    }

    ctx.fillStyle = 'rgba(232,237,247,0.9)';
    ctx.font = '600 ' + Math.round(W * 0.018) + 'px Segoe UI, sans-serif';
    ctx.fillText('RYMICRO  STUDIO', kx + 4, ky - 26);
    ctx.fillStyle = 'rgba(139,150,173,0.9)';
    ctx.font = Math.round(W * 0.013) + 'px Consolas, monospace';
    ctx.fillText('side panels · ' + (L.SideLeft.Effect || 'static') + ' / ' + (L.SideRight.Effect || 'static'), kx + 4, ky - 8);
  }

  function hslHex(h) {
    const c = 1, x = 1 - Math.abs((h / 60) % 2 - 1);
    let r = 0, g = 0, b = 0;
    if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
    else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
    const f = (v) => Math.round(v * 255).toString(16).padStart(2, '0');
    return '#' + f(r) + f(g) + f(b);
  }

  function download(blob, name) {
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 2000);
  }

  function exportPng() {
    const canvas = document.createElement('canvas');
    canvas.width = 1400;
    canvas.height = 620;
    drawPreview(canvas, 0.35);
    canvas.toBlob((b) => { download(b, 'rymicro-side-panels.png'); toast('PNG сохранён', 'success'); }, 'image/png');
  }

  async function recordWebm() {
    if (typeof MediaRecorder === 'undefined') { toast('Браузер не поддерживает запись видео', 'error'); return; }
    const canvas = document.createElement('canvas');
    canvas.width = 960;
    canvas.height = 540;
    drawPreview(canvas, 0);
    const stream = canvas.captureStream(30);
    let mime = 'video/webm;codecs=vp9';
    if (!MediaRecorder.isTypeSupported(mime)) mime = 'video/webm';
    const rec = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 4000000 });
    const chunks = [];
    rec.ondataavailable = (e) => { if (e.data && e.data.size) chunks.push(e.data); };
    const stopped = new Promise((r) => { rec.onstop = r; });
    rec.start();
    toast('Записываю 5 секунд…', 'info');
    const t0 = performance.now();
    const frame = () => {
      const t = (performance.now() - t0) / 1000;
      drawPreview(canvas, t);
      if (t < 5) requestAnimationFrame(frame);
      else if (rec.state !== 'inactive') rec.stop();
    };
    frame();
    await stopped;
    download(new Blob(chunks, { type: 'video/webm' }), 'rymicro-side-panels.webm');
    toast('WebM сохранён', 'success');
  }
})();
