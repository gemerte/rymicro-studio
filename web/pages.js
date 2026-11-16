/* RYMICRO Studio — профили, макросы, статистика, настройки */
(() => {
  'use strict';
  const R = window.RY;
  const { A, $, esc, fmt, clamp, api, apiGet, keyName, profileObj, toast, openModal, closeModal,
    ask, confirmBox, startCapture, render, section } = R;

  /* ============================== ПРОФИЛИ ============================== */

  function renderProfiles() {
    if (!A.cfg) return;
    const p = section('profiles');
    const names = Object.keys(A.cfg.Profiles || {});

    const cards = names.map((name) => {
      const pr = A.cfg.Profiles[name] || {};
      const rules = (pr.Rules || []).length;
      const layers = Object.keys(pr.Layers || {}).length;
      const enabled = (pr.Rules || []).filter((r) => r.Enabled).length;
      const active = name === A.cfg.ActiveProfile;
      return `<div class="pcard${active ? ' active' : ''}">
        <div class="row between" style="align-items:flex-start">
          <div><h4>${esc(name)}</h4>
          <div class="meta">${esc(pr.MatchProcess || 'без автопереключения')}</div></div>
          ${active ? '<span class="tag g">активен</span>' : '<span class="tag v">профиль</span>'}
        </div>
        <div class="stat-line">
          <div><b>${rules}</b>биндов</div>
          <div><b>${enabled}</b>включено</div>
          <div><b>${layers}</b>слоёв</div>
        </div>
        <div class="field" style="margin-bottom:10px">
          <label>Автопереключение по процессу</label>
          <input type="text" data-match="${esc(name)}" value="${esc(pr.MatchProcess || '')}" placeholder="cs2.exe, chrome.exe (через запятую)">
        </div>
        <div class="ops">
          <button class="btn sm${active ? ' ghost' : ''}" data-p="${esc(name)}" data-act="activate"${active ? ' disabled' : ''}>Активировать</button>
          <button class="btn ghost sm" data-p="${esc(name)}" data-act="duplicate">Дублировать</button>
          <button class="btn ghost sm" data-p="${esc(name)}" data-act="rules">Бинды</button>
          <button class="btn danger sm" data-p="${esc(name)}" data-act="delete">Удалить</button>
        </div>
      </div>`;
    }).join('');

    p.innerHTML = `<div class="row between" style="margin-bottom:14px">
        <div><h3 style="margin:0;font-size:16px">Профили</h3>
        <p class="sub" style="margin:4px 0 0">Наборы биндов под игры и приложения. Переключаются вручную, из трея или автоматически по процессу.</p></div>
        <button class="btn" id="addProfileBtn">+ Новый профиль</button>
      </div>
      <div class="profile-grid">${cards}</div>
      <div class="card" style="margin-top:18px">
        <h3>Как работает автопереключение</h3>
        <p class="sub">Приложение раз в секунду смотрит на активное окно. Если имя процесса совпало с полем профиля — профиль включается сам. Укажите имя без .exe или с ним, можно несколько через запятую: <span class="mono">cs2, chrome</span>.</p>
        <div class="kbd-hint">Сейчас активно: <b>${esc(A.cfg.ActiveProfile)}</b> · всего профилей: ${names.length}</div>
      </div>`;

    p.querySelector('.profile-grid').addEventListener('click', async (e) => {
      const b = e.target.closest('button[data-act]');
      if (!b) return;
      const name = b.dataset.p;
      if (b.dataset.act === 'activate') {
        try { await api('profile', { name }); toast('Профиль: ' + name, 'success'); } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
      } else if (b.dataset.act === 'rules') {
        A.layer = '';
        R.switchTab('rules');
      } else if (b.dataset.act === 'duplicate') {
        const newName = await ask('Дублировать профиль', 'Имя копии профиля «' + name + '»:', name + ' copy');
        if (!newName) return;
        try {
          const copy = JSON.parse(JSON.stringify(A.cfg.Profiles[name]));
          copy.Name = newName;
          await api('profile-save', { name: newName, profile: copy });
          toast('Создан профиль ' + newName, 'success');
        } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
      } else if (b.dataset.act === 'delete') {
        const ok = await confirmBox('Удалить профиль?', 'Профиль «' + name + '» и все его бинды будут удалены.', 'Удалить');
        if (!ok) return;
        try { await api('profile-delete', { name }); toast('Профиль удалён', 'success'); } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
      }
    });

    p.querySelectorAll('input[data-match]').forEach((inp) => {
      inp.addEventListener('change', async () => {
        const name = inp.dataset.match;
        const pr = JSON.parse(JSON.stringify(A.cfg.Profiles[name]));
        pr.MatchProcess = inp.value.trim();
        try {
          await api('profile-save', { name, profile: pr });
          toast('Автопереключение обновлено', 'success');
        } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
      });
    });

    $('#addProfileBtn').addEventListener('click', async () => {
      const name = await ask('Новый профиль', 'Название профиля (например: Работа, Игры, OBS):', 'Новый профиль');
      if (!name) return;
      if (A.cfg.Profiles[name]) { toast('Такой профиль уже есть', 'error'); return; }
      try {
        await api('profile-save', { name, profile: { Name: name, MatchProcess: '', Rules: [], Layers: {} }, activate: true });
        toast('Профиль создан: ' + name, 'success');
      } catch (err) { toast('Ошибка: ' + err.message, 'error'); }
    });
  }
  render.profiles = renderProfiles;

  /* ============================== МАКРОСЫ ============================== */

  function ensureMacroDraft() {
    if (A.macroDraft) return;
    if (A.macroSel && A.cfg.Macros[A.macroSel]) {
      A.macroDraft = JSON.parse(JSON.stringify(A.cfg.Macros[A.macroSel]));
    } else {
      A.macroDraft = { Name: '', Steps: [] };
    }
  }

  function renderMacros() {
    if (!A.cfg) return;
    const p = section('macros');
    const names = Object.keys(A.cfg.Macros || {});
    if (!A.macroSel && names.length) A.macroSel = names[0];
    ensureMacroDraft();

    const items = names.map((n) => {
      const m = A.cfg.Macros[n] || {};
      return `<div class="macro-item${A.macroSel === n ? ' active' : ''}" data-m="${esc(n)}">
        <div><div class="n">${esc(n)}</div><div class="c">шагов: ${(m.Steps || []).length}</div></div>
        <span class="muted" style="font-size:16px">›</span>
      </div>`;
    }).join('') || '<div class="muted" style="font-size:12.5px">Библиотека пуста</div>';

    const d = A.macroDraft;
    const steps = (d.Steps || []).map((s, i) => {
      const label = s.Text ? ('текст: ' + s.Text) : (keyName(s.Key) + (s.Down ? ' ↓' : ' ↑'));
      return `<div class="step">
        <span class="idx">${i + 1}</span>
        <span class="k${s.Down ? '' : ' up'}">${esc(label)}</span>
        <input type="number" min="0" max="5000" step="5" data-delay="${i}" value="${Number(s.DelayMs || 0)}" title="Пауза после шага, мс">
        <button class="del" data-del="${i}" title="Удалить шаг">×</button>
      </div>`;
    }).join('') || '<div class="muted" style="font-size:12.5px;padding:10px 0">Шагов пока нет. Запишите макрос или добавьте клавишу.</div>';

    p.innerHTML = `<div class="macro-layout">
      <div class="card">
        <div class="row between" style="margin-bottom:12px"><h3>Библиотека</h3><button class="btn ghost sm" id="newMacroBtn">+</button></div>
        <div class="macro-list">${items}</div>
        <hr class="sep">
        <div class="kbd-hint">Макрос можно привязать к клавише в инспекторе: действие «Макрос» → «Взять из библиотеки».</div>
      </div>
      <div class="card">
        <div class="row between" style="margin-bottom:12px">
          <div><h3>Редактор макроса</h3><p class="sub" style="margin:3px 0 0">Запись клавиш с паузами, как в макропаде.</p></div>
          <div class="row">
            <button class="btn ghost sm" id="macroRecBtn">● Записать</button>
            <button class="btn ghost sm" id="macroAddKey">+ Клавиша</button>
          </div>
        </div>
        <div class="field"><label>Название</label><input type="text" id="mName" placeholder="Например: Быстрый ответ" value="${esc(d.Name || '')}"></div>
        <div class="steps" id="steps"></div>
        <div class="row between">
          <div class="row">
            <button class="btn ghost sm" id="macroPlay">▶ Воспроизвести</button>
            <button class="btn ghost sm" id="macroClear">Очистить</button>
          </div>
          <div class="row">
            <button class="btn danger sm" id="macroDelete">Удалить</button>
            <button class="btn" id="macroSave">Сохранить</button>
          </div>
        </div>
      </div>
    </div>`;

    $('#steps').innerHTML = steps;

    p.querySelector('.macro-list').addEventListener('click', (e) => {
      const item = e.target.closest('[data-m]');
      if (!item) return;
      A.macroSel = item.dataset.m;
      A.macroDraft = JSON.parse(JSON.stringify(A.cfg.Macros[A.macroSel]));
      renderMacros();
    });

    $('#mName').addEventListener('input', (e) => { A.macroDraft.Name = e.target.value; });

    $('#steps').addEventListener('change', (e) => {
      const i = e.target.dataset.delay;
      if (i == null) return;
      A.macroDraft.Steps[Number(i)].DelayMs = Math.max(0, Number(e.target.value) || 0);
    });
    $('#steps').addEventListener('click', (e) => {
      const del = e.target.closest('[data-del]');
      if (!del) return;
      A.macroDraft.Steps.splice(Number(del.dataset.del), 1);
      renderMacros();
    });

    $('#newMacroBtn').addEventListener('click', async () => {
      const name = await ask('Новый макрос', 'Название макроса:', 'Новый макрос');
      if (!name) return;
      A.macroSel = null;
      A.macroDraft = { Name: name, Steps: [] };
      renderMacros();
    });

    $('#macroRecBtn').addEventListener('click', () => {
      startCapture({
        mode: 'macro',
        title: 'Запись макроса',
        sub: 'Нажимайте клавиши в нужном порядке. Паузы сохраняются. Затем «Готово».',
        onDone: (steps) => {
          A.macroDraft.Steps = steps;
          renderMacros();
          toast('Записано шагов: ' + steps.length, 'success');
        },
      });
    });

    $('#macroAddKey').addEventListener('click', () => {
      startCapture({
        mode: 'single',
        title: 'Добавить клавишу',
        sub: 'Нажмите клавишу — в макрос добавятся нажатие и отпускание.',
        onDone: (id) => {
          A.macroDraft.Steps.push({ Key: id, Down: true, DelayMs: 20 });
          A.macroDraft.Steps.push({ Key: id, Down: false, DelayMs: 20 });
          renderMacros();
        },
      });
    });

    $('#macroClear').addEventListener('click', () => { A.macroDraft.Steps = []; renderMacros(); });

    $('#macroPlay').addEventListener('click', async () => {
      if (!A.macroDraft.Steps.length) { toast('Макрос пуст', 'error'); return; }
      try {
        await api('macro-test', { steps: A.macroDraft.Steps, delay: 0 });
        toast('Воспроизвожу…', 'success');
      } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });

    $('#macroSave').addEventListener('click', async () => {
      const name = ($('#mName').value || '').trim();
      if (!name) { toast('Введите название', 'error'); return; }
      try {
        A.macroDraft.Name = name;
        await api('macro-save', { name, macro: A.macroDraft });
        A.macroSel = name;
        toast('Макрос сохранён', 'success');
        renderMacros();
      } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });

    $('#macroDelete').addEventListener('click', async () => {
      if (!A.macroSel) { toast('Макрос не выбран', 'error'); return; }
      const ok = await confirmBox('Удалить макрос?', 'Макрос «' + A.macroSel + '» будет удалён. Бинды, использующие его, перестанут работать.', 'Удалить');
      if (!ok) return;
      try {
        await api('macro-delete', { name: A.macroSel });
        A.macroSel = null;
        A.macroDraft = null;
        toast('Макрос удалён', 'success');
        renderMacros();
      } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });
  }
  render.macros = renderMacros;

  /* ============================= СТАТИСТИКА ============================= */

  function renderStats() {
    if (!A.cfg) return;
    const p = section('stats');
    const stats = A.cfg.Stats || {};
    const entries = Object.keys(stats).map((k) => [k, stats[k]]).sort((a, b) => b[1] - a[1]);
    const total = entries.reduce((s, e) => s + e[1], 0);
    const top = entries[0];
    const max = entries.length ? entries[0][1] : 1;
    const used = entries.length;

    const topRows = entries.slice(0, 12).map(([id, n], i) => `
      <div class="set-row">
        <div style="display:flex;align-items:center;gap:10px;min-width:0">
          <span class="muted mono" style="width:20px;font-size:11px">${i + 1}</span>
          <span class="keyname">${esc(keyName(id))}</span>
          <span class="muted" style="font-size:11px">${esc(id)}</span>
        </div>
        <div style="flex:1;max-width:260px"><div class="bar"><i style="width:${Math.round(n / max * 100)}%"></i></div></div>
        <div class="mono" style="min-width:60px;text-align:right">${fmt(n)}</div>
      </div>`).join('') || '<div class="empty">Статистика пуста — нажмите несколько клавиш.</div>';

    p.innerHTML = `
      <div class="stat-cards">
        <div class="stat"><div class="n">${fmt(total)}</div><div class="l">всего нажатий</div></div>
        <div class="stat"><div class="n">${fmt(used)}</div><div class="l">клавиш задействовано</div></div>
        <div class="stat"><div class="n">${top ? esc(keyName(top[0])) : '—'}</div><div class="l">любимая клавиша</div></div>
        <div class="stat"><div class="n">${fmt(A.pressedTotal)}</div><div class="l">нажатий за сессию</div></div>
      </div>
      <div class="light-grid">
        <div class="card">
          <div class="row between" style="margin-bottom:10px"><h3>Топ клавиш</h3>
            <div class="row"><button class="btn ghost sm" id="heatBtn">Тепловая карта</button>
            <button class="btn danger sm" id="resetStats">Сбросить</button></div></div>
          <div>${topRows}</div>
        </div>
        <div class="card">
          <h3>Раскладка «горячими» цветами</h3>
          <p class="sub">Клавиши, которые чаще всего нажимаются, подсвечиваются ярче.</p>
          <div id="statsMini" style="display:grid;grid-template-columns:repeat(14,1fr);gap:5px;padding:12px;border-radius:12px;background:#080c14;border:1px solid rgba(255,255,255,.06)"></div>
          <div class="kbd-hint" style="margin-top:12px">Статистика хранится локально в config.json и не отправляется в интернет.</div>
        </div>
      </div>`;

    const mini = $('#statsMini');
    if (mini) {
      const sample = Object.keys(stats).slice(0, 70);
      mini.innerHTML = R.GEOM.slice(0, 70).map((g) => {
        const v = clamp((stats[g.id] || 0) / max, 0, 1);
        return '<i style="height:13px;border-radius:3px;background:rgba(0,229,255,' + (0.05 + v * 0.85) + ')" title="' + esc(keyName(g.id)) + '"></i>';
      }).join('');
    }

    $('#heatBtn').addEventListener('click', () => { A.heat = true; R.switchTab('board'); });
    $('#resetStats').addEventListener('click', async () => {
      const ok = await confirmBox('Сбросить статистику?', 'Счётчики нажатий будут обнулены. Бинды и настройки останутся.', 'Сбросить');
      if (!ok) return;
      try { await api('stats-reset'); toast('Статистика сброшена', 'success'); } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });
  }
  render.stats = renderStats;

  /* ============================== НАСТРОЙКИ ============================== */

  function renderSettings() {
    if (!A.cfg) return;
    const p = section('settings');
    const s = A.cfg.Settings || {};
    const base = (A.rt && A.rt.baseDir) || '';

    p.innerHTML = `<div class="settings-grid">
      <div class="card">
        <h3>Поведение</h3>
        <p class="sub">Как приложение реагирует на клавиши.</p>
        <div class="set-row">
          <div><div class="t">Звук нажатий</div><div class="d">Тихий щелчок для каждой клавиши. Работает только при открытой панели.</div></div>
          <label class="switch"><input type="checkbox" id="setSound"${s.Sound ? ' checked' : ''}><span class="track"></span></label>
        </div>
        <div class="set-row">
          <div><div class="t">Громкость</div><div class="d">0 — тихо, 100 — громко.</div></div>
          <div class="row" style="min-width:180px"><input type="range" id="setVol" min="0" max="100" value="${s.SoundVolume == null ? 25 : s.SoundVolume}"><span class="range-val" id="volVal">${s.SoundVolume == null ? 25 : s.SoundVolume}</span></div>
        </div>
        <div class="set-row">
          <div><div class="t">Автопереключение профилей</div><div class="d">Следить за активным процессом и включать подходящий профиль.</div></div>
          <label class="switch"><input type="checkbox" id="setAuto"${s.AutoSwitch ? ' checked' : ''}><span class="track"></span></label>
        </div>
        <div class="set-row">
          <div><div class="t">Считать статистику</div><div class="d">Счётчики нажатий по каждой клавише.</div></div>
          <label class="switch"><input type="checkbox" id="setStats"${s.StatsEnabled ? ' checked' : ''}><span class="track"></span></label>
        </div>
        <div class="set-row">
          <div><div class="t">Запуск с Windows</div><div class="d">Добавить в автозагрузку текущего пользователя.</div></div>
          <label class="switch"><input type="checkbox" id="setStart"${s.StartWithWindows || (A.rt && A.rt.autostart) ? ' checked' : ''}><span class="track"></span></label>
        </div>
      </div>

      <div class="card">
        <h3>Пульт для телефона / планшета</h3>
        <p class="sub">Откройте ссылку на телефоне в той же Wi-Fi сети — и запускайте Steam, Discord, программы и макросы на ПК одним касанием.</p>
        <div class="set-row">
          <div><div class="t">Включить пульт</div><div class="d">Локальный сервер на порту ${esc(String(s.RemotePort || 47832))} (без доступа из интернета).</div></div>
          <label class="switch"><input type="checkbox" id="setRemote"${s.RemoteEnabled ? ' checked' : ''}><span class="track"></span></label>
        </div>
        <div class="field" style="margin-top:10px"><label>Порт пульта</label>
          <div class="row"><input type="number" id="setRemotePort" min="1024" max="65535" value="${Number(s.RemotePort || 47832)}" style="width:120px">
          <button class="btn ghost sm" id="remoteApply">Применить</button></div>
        </div>
        <div class="field"><label>Ссылка для телефона</label>
          <div class="row"><input type="text" id="remUrl" readonly value="…" class="mono" style="flex:1;min-width:200px;font-size:12px">
          <button class="btn ghost sm" id="remCopy">Копировать</button></div>
        </div>
        <div class="row" style="margin-bottom:10px">
          <button class="btn ghost sm" id="remOpen">Открыть на ПК</button>
          <button class="btn ghost sm" id="remShow">Показать крупно</button>
          <button class="btn ghost sm" id="remFw">Разрешить в брандмауэре</button>
          <button class="btn ghost sm" id="remNewToken">Новый код доступа</button>
        </div>
        <div class="console" id="remLog" style="height:auto;max-height:120px">Проверяю…</div>
        <div class="kbd-hint" style="margin-top:10px">Если телефон не открывает страницу — нажмите «Разрешить в брандмауэре» и подтвердите запрос администратора Windows.</div>
      </div>

      <div class="card">
        <h3>Данные</h3>
        <p class="sub">Конфиг — обычный JSON, его удобно хранить в git.</p>
        <div class="row" style="margin-bottom:10px">
          <button class="btn ghost sm" id="exportCfg">Экспорт JSON</button>
          <button class="btn ghost sm" id="importCfg">Импорт JSON</button>
          <button class="btn ghost sm" id="openFolder">Открыть папку</button>
          <input type="file" id="importFile" accept="application/json" style="display:none">
        </div>
        <div class="console" style="height:auto;max-height:120px">${esc(base || 'папка приложения')}\\config\\config.json</div>
        <hr class="sep">
        <div class="row between">
          <div><div class="t">Сбросить всё</div><div class="d">Вернуть заводские бинды, профили и подсветку.</div></div>
          <button class="btn danger sm" id="resetAll">Сброс</button>
        </div>
      </div>

      <div class="card">
        <h3>Приложение</h3>
        <p class="sub">Служебная информация.</p>
        <div class="set-row"><div class="t">Версия</div><div class="mono">${esc(A.ver || '—')}</div></div>
        <div class="set-row"><div class="t">Порт панели</div><div class="mono">127.0.0.1:${esc(A.rt.port || '')}</div></div>
        <div class="set-row"><div class="t">Хук клавиатуры</div><div class="mono">${A.rt.hookInstalled ? 'установлен' : 'нет'}</div></div>
        <div class="set-row"><div class="t">Клавиатура</div><div class="mono" style="text-align:right;font-size:11.5px">${esc((A.kb && A.kb.Name) || 'RY Micro')}<br><span class="muted">${esc((A.kb && A.kb.Manufacturer) || '')} ${esc((A.kb && A.kb.Serial) || '')}</span></div></div>
        <hr class="sep">
        <div class="row"><button class="btn ghost sm" id="showLogs">Логи</button><button class="btn danger sm" id="shutdownApp">Выйти из приложения</button></div>
        <pre class="console" id="logBox" style="display:none;margin-top:12px">—</pre>
      </div>

      <div class="card">
        <h3>Горячие клавиши панели</h3>
        <p class="sub">Сама панель — обычный браузер, поэтому работают системные жесты.</p>
        <div class="set-row"><div class="t">Захват клавиши</div><div class="d">Кнопка «⌨ Нажать» в инспекторе ловит физическое нажатие через хук.</div><span class="tag">удобно</span></div>
        <div class="set-row"><div class="t">Слои</div><div class="d">Один слой может превратить IJKL в стрелки, не меняя базовые бинды.</div><span class="tag v">Fn</span></div>
        <div class="set-row"><div class="t">Пауза</div><div class="d">Кнопка в шапке или пункт в трее мгновенно отключает ремап.</div><span class="tag m">трей</span></div>
      </div>
    </div>`;

    const save = async (patch) => {
      const next = Object.assign({}, A.cfg.Settings, patch);
      A.cfg.Settings = next;
      try { await api('settings', { settings: next }); toast('Настройка сохранена', 'success', 1600); }
      catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    };

    $('#setSound').addEventListener('change', (e) => save({ Sound: e.target.checked }));
    $('#setAuto').addEventListener('change', (e) => save({ AutoSwitch: e.target.checked }));
    $('#setStats').addEventListener('change', (e) => save({ StatsEnabled: e.target.checked }));
    $('#setStart').addEventListener('change', (e) => save({ StartWithWindows: e.target.checked }));
    $('#setVol').addEventListener('input', (e) => { $('#volVal').textContent = e.target.value; });
    $('#setVol').addEventListener('change', (e) => save({ SoundVolume: Number(e.target.value) }));

    const remLog = (msg, ok) => {
      const box = $('#remLog');
      if (!box) return;
      box.textContent = msg;
      box.style.color = ok === false ? '#ff8fa3' : (ok ? '#8ff0c0' : '');
    };
    const loadRemote = async () => {
      try {
        const r = await apiGet('remote-status');
        $('#remUrl').value = r.url || '';
        remLog(r.running ? 'Пульт работает. Ссылка: ' + r.url : ('Пульт выключен' + (r.error ? '. Ошибка: ' + r.error : '')), !!r.running);
      } catch (e) { remLog('Статус недоступен: ' + e.message, false); }
    };
    loadRemote();

    if ($('#setRemote')) $('#setRemote').addEventListener('change', async (e) => {
      await save({ RemoteEnabled: e.target.checked });
      try { await api('remote-apply', {}); } catch (err) { /* noop */ }
      loadRemote();
    });
    if ($('#remoteApply')) $('#remoteApply').addEventListener('click', async () => {
      const port = Number($('#setRemotePort').value || 47832);
      await save({ RemotePort: port });
      try {
        const r = await api('remote-apply', {});
        remLog(r.ok ? 'Пульт перезапущен на порту ' + port : ('Ошибка: ' + (r.error || 'не удалось занять порт')), !!r.ok);
      } catch (err) { remLog('Ошибка: ' + err.message, false); }
      loadRemote();
    });
    if ($('#remCopy')) $('#remCopy').addEventListener('click', async () => {
      const url = $('#remUrl').value;
      try { await navigator.clipboard.writeText(url); toast('Ссылка скопирована', 'success'); }
      catch (e) { $('#remUrl').select(); toast('Нажмите Ctrl+C', 'info'); }
    });
    if ($('#remOpen')) $('#remOpen').addEventListener('click', async () => {
      try { await api('remote-open', {}); } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });
    if ($('#remShow')) $('#remShow').addEventListener('click', () => {
      openModal('Ссылка для телефона', '<p class="sub">Откройте этот адрес в браузере телефона в той же Wi-Fi сети:</p>' +
        '<div class="console" style="font-size:15px;word-break:break-all;user-select:all">' + esc($('#remUrl').value) + '</div>');
    });
    if ($('#remFw')) $('#remFw').addEventListener('click', async () => {
      try {
        const r = await api('firewall', {});
        remLog(r.message || r.error || 'Готово', r.ok);
      } catch (e) { remLog('Ошибка: ' + e.message, false); }
    });
    if ($('#remNewToken')) $('#remNewToken').addEventListener('click', async () => {
      const ok = await confirmBox('Новый код доступа?', 'Старая ссылка на телефоне перестанет работать — откройте новую.', 'Сменить код');
      if (!ok) return;
      try {
        const r = await api('remote-token', {});
        $('#remUrl').value = r.url || '';
        remLog('Новый код создан. Ссылка обновлена.', true);
      } catch (e) { remLog('Ошибка: ' + e.message, false); }
    });

    $('#exportCfg').addEventListener('click', () => {
      const blob = new Blob([JSON.stringify(A.cfg, null, 2)], { type: 'application/json' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = 'rymicro-config.json';
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1500);
    });

    $('#importCfg').addEventListener('click', () => $('#importFile').click());
    $('#importFile').addEventListener('change', (e) => {
      const file = e.target.files && e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = async () => {
        try {
          const cfg = JSON.parse(String(reader.result));
          await api('config', { config: cfg });
          toast('Конфиг импортирован', 'success');
        } catch (err) { toast('Некорректный JSON: ' + err.message, 'error'); }
      };
      reader.readAsText(file);
    });

    $('#openFolder').addEventListener('click', async () => {
      try { await api('open', { path: base || '' }); } catch (e) { toast('Не удалось открыть папку', 'error'); }
    });

    $('#resetAll').addEventListener('click', async () => {
      const ok = await confirmBox('Сбросить всё?', 'Все профили, бинды, макросы и настройки подсветки вернутся к заводским.', 'Сбросить всё');
      if (!ok) return;
      try { await api('reset'); toast('Настройки сброшены', 'success'); } catch (e) { toast('Ошибка: ' + e.message, 'error'); }
    });

    $('#showLogs').addEventListener('click', async () => {
      const box = $('#logBox');
      box.style.display = 'block';
      try {
        const r = await apiGet('logs', { tail: 200 });
        box.textContent = (r.lines || []).slice(-140).join('\n') || 'Лог пуст';
        box.scrollTop = box.scrollHeight;
      } catch (e) { box.textContent = 'Не удалось прочитать лог: ' + e.message; }
    });

    $('#shutdownApp').addEventListener('click', async () => {
      const ok = await confirmBox('Выйти из приложения?', 'Ремап и подсветка перестанут работать, пока вы снова не запустите RYMICRO Studio.', 'Выйти');
      if (!ok) return;
      try { await api('shutdown'); } catch (e) {}
      document.body.innerHTML = '<div class="loading"><div class="spinner"></div><div>Приложение закрыто. Можно закрыть вкладку.</div></div>';
    });
  }
  render.settings = renderSettings;

  /* =============================== О ПРОГРАММЕ =============================== */

  R.about = function () {
    const kb = A.kb || {};
    openModal(`<div class="row between" style="margin-bottom:10px">
        <h3 style="margin:0">RYMICRO Studio</h3><span class="tag">v${esc(A.ver || '1.0.0')}</span>
      </div>
      <p>Локальная студия для клавиатуры <b>${esc(kb.Name || 'RY Micro USB HID')}</b>: ремап клавиш без драйверов, слои, бинды, макросы, профили и дизайн боковой подсветки.</p>
      <div class="set-row"><div class="t">Устройство</div><div class="mono" style="text-align:right;font-size:11.5px">VID ${esc(kb.Vid || '352D')}:PID ${esc(kb.Pid || '2380')}<br>${esc(kb.Manufacturer || '')} · ${esc(kb.Serial || 'RYMICRO')}</div></div>
      <div class="set-row"><div class="t">Движок</div><div class="mono">WH_KEYBOARD_LL + SendInput</div></div>
      <div class="set-row"><div class="t">Панель</div><div class="mono">127.0.0.1:${esc(A.rt.port || '')} (только локально)</div></div>
      <div class="set-row"><div class="t">Подсветка</div><div class="mono">схемы + OpenRGB SDK + HID</div></div>
      <p class="sub" style="margin-top:14px">Всё хранится локально. Никаких аккаунтов и облаков. Ремап работает, пока приложение запущено.</p>
      <div class="row"><button class="btn" id="aboutClose">Понятно</button></div>`);
    $('#aboutClose').onclick = closeModal;
  };
})();
