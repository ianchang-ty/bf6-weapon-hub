(function () {
  'use strict';

  const PAYLOAD = window.BF6_DATA || { meta: {}, weapons: [] };
  const WEAPONS = PAYLOAD.weapons || [];
  const WMAP = {};
  WEAPONS.forEach((w) => { WMAP[w.key] = w; });

  const FAV_STORAGE_KEY = 'bf6_hub_favs_v1';
  function loadFavs() {
    try {
      const raw = localStorage.getItem(FAV_STORAGE_KEY);
      const arr = raw ? JSON.parse(raw) : [];
      return Array.isArray(arr) ? arr : [];
    } catch (_) {
      return [];
    }
  }
  function saveFavs(list) {
    try { localStorage.setItem(FAV_STORAGE_KEY, JSON.stringify(list)); } catch (_) {}
  }

  const BANDS = [
    { id: 'all', label: '全部距离' },
    { id: '近', label: '近战 0–15m' },
    { id: '中近', label: '中近 15–35m' },
    { id: '中', label: '中距 35–60m' },
    { id: '远', label: '远距 60m+' }
  ];
  const CLASSES = [
    { id: 'all', label: '全部' },
    { id: 'ar', label: '突击步枪' },
    { id: 'carbine', label: '卡宾枪' },
    { id: 'smg', label: '冲锋枪' },
    { id: 'lmg', label: '机枪' },
    { id: 'dmr', label: '射手步枪' },
    { id: 'sniper', label: '狙击步枪' },
    { id: 'shotgun', label: '霰弹枪' },
    { id: 'sidearm', label: '副武器' }
  ];

  const state = {
    nav: 'hall',
    band: 'all',
    cls: 'all',
    tier: 'all',
    q: '',
    ttkBand: '中近',
    openKey: null,
    openRole: null,
    favs: loadFavs()
  };

  function esc(str) {
    return String(str == null ? '' : str)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function getTier(w, band) {
    const t = (w.meta && w.meta.tier) || {};
    return band === 'all' ? (t[w.meta.best] || 'T2') : (t[band] || null);
  }

  function sortScore(w, band) {
    const order = { T0: 0, T1: 1, T2: 2, T3: 3 };
    const t = getTier(w, band) || 'T9';
    const rk = (w.meta && w.meta.rank && (band === 'all' ? w.meta.rank[w.meta.best] : w.meta.rank[band])) || 99;
    return (order[t] !== undefined ? order[t] : 9) * 100 + rk;
  }

  function filterList(source) {
    const q = state.q.trim().toLowerCase();
    return source.filter((w) => {
      if (state.cls !== 'all' && w.clsId !== state.cls) return false;
      if (state.band !== 'all' && !getTier(w, state.band)) return false;
      const t = getTier(w, state.band) || 'T3';
      if (state.tier === 'T0' && t !== 'T0') return false;
      if (state.tier === 'T0T1' && t !== 'T0' && t !== 'T1') return false;
      if (q) {
        const hay = (w.name + ' ' + w.en + ' ' + w.key + ' ' + w.cls + ' ' + ((w.meta && w.meta.arch) || '')).toLowerCase();
        if (hay.indexOf(q) === -1) return false;
      }
      return true;
    }).sort((a, b) => sortScore(a, state.band) - sortScore(b, state.band));
  }

  function cardHtml(w, bandForTier) {
    const t = getTier(w, bandForTier) || 'T2';
    const bp0 = w.bp && w.bp[0];
    const rightInfo = w.clsId === 'sniper'
      ? ('甜区 ' + (w.sweet_spot || '无') + ' · ' + Math.round(w.base.v0) + 'm/s')
      : (Math.round(w.base.rpm) + 'RPM · ' + (bp0 ? bp0.btk + '枪/' + bp0.ttk + 'ms' : ''));
    const favMark = state.favs.indexOf(w.key) !== -1 ? ' ★' : '';
    return '<div class="w-card" data-wkey="' + esc(w.key) + '">' +
      '<div class="w-card-head">' +
        '<div class="w-title-group">' +
          '<span class="tier-pill tier-' + esc(t) + '">' + esc(t) + '</span>' +
          '<span class="w-name">' + esc(w.name + favMark) + '</span>' +
          '<span class="w-cls">' + esc(w.cls) + '</span>' +
        '</div>' +
        '<span class="dist-tag">主: ' + esc((w.meta && w.meta.best) || '近') + '距</span>' +
      '</div>' +
      '<div class="w-card-sub">' +
        '<span>' + esc((w.meta && w.meta.arch) || '') + '</span>' +
        '<span class="mono">' + esc(rightInfo) + '</span>' +
      '</div>' +
    '</div>';
  }

  function bindCards(container) {
    container.querySelectorAll('[data-wkey]').forEach((el) => {
      el.addEventListener('click', () => {
        state.openKey = el.getAttribute('data-wkey');
        state.openRole = null;
        renderSheet();
      });
    });
  }

  function renderChips() {
    const bEl = document.getElementById('band-chips');
    bEl.innerHTML = BANDS.map((b) =>
      '<button type="button" class="chip' + (state.band === b.id ? ' active' : '') + '" data-band="' + b.id + '">' + esc(b.label) + '</button>'
    ).join('');
    bEl.querySelectorAll('button').forEach((btn) => {
      btn.addEventListener('click', () => { state.band = btn.getAttribute('data-band'); renderAll(); });
    });

    const cEl = document.getElementById('cls-chips');
    cEl.innerHTML = CLASSES.map((c) =>
      '<button type="button" class="chip' + (state.cls === c.id ? ' active' : '') + '" data-cls="' + c.id + '">' + esc(c.label) + '</button>'
    ).join('');
    cEl.querySelectorAll('button').forEach((btn) => {
      btn.addEventListener('click', () => { state.cls = btn.getAttribute('data-cls'); renderAll(); });
    });
  }

  function renderHall() {
    const list = filterList(WEAPONS);
    document.getElementById('count-label').textContent = '共匹配 ' + list.length + ' 把武器';
    const wrap = document.getElementById('weapon-cards');
    wrap.innerHTML = list.length ? list.map((w) => cardHtml(w, state.band)).join('') : '<div class="mini-box">无匹配武器，请调整上方筛选条件</div>';
    bindCards(wrap);
  }

  function renderTtk() {
    const tChips = document.getElementById('ttk-chips');
    const tbands = [
      { id: '近', label: '近战 0–15m' },
      { id: '中近', label: '中近 15–35m' },
      { id: '中', label: '中距 35–60m' },
      { id: '远', label: '远距 60m+' }
    ];
    tChips.innerHTML = tbands.map((b) =>
      '<button type="button" class="chip' + (state.ttkBand === b.id ? ' active' : '') + '" data-tb="' + b.id + '">' + esc(b.label) + '</button>'
    ).join('');
    tChips.querySelectorAll('button').forEach((btn) => {
      btn.addEventListener('click', () => { state.ttkBand = btn.getAttribute('data-tb'); renderTtk(); });
    });

    const autos = WEAPONS.filter((w) => ['ar', 'carbine', 'smg', 'lmg'].indexOf(w.clsId) !== -1)
      .sort((a, b) => a.bands[state.ttkBand] - b.bands[state.ttkBand]);
    const minV = autos[0].bands[state.ttkBand];
    const maxV = autos[autos.length - 1].bands[state.ttkBand];
    const listEl = document.getElementById('ttk-list');
    listEl.innerHTML = autos.map((w, idx) => {
      const val = w.bands[state.ttkBand];
      const wPct = Math.round(25 + ((val - minV) / Math.max(1, maxV - minV)) * 75);
      const t = w.meta.tier[state.ttkBand];
      return '<div class="w-card" data-wkey="' + esc(w.key) + '">' +
        '<div class="w-card-head">' +
          '<div class="w-title-group">' +
            '<span class="mono w-cls">#' + (idx + 1) + '</span>' +
            '<span class="tier-pill tier-' + esc(t) + '">' + esc(t) + '</span>' +
            '<span class="w-name">' + esc(w.name) + '</span>' +
            '<span class="w-cls">' + esc(w.cls) + '</span>' +
          '</div>' +
          '<span class="mono"><strong>' + Math.round(val) + ' ms</strong></span>' +
        '</div>' +
        '<svg class="bar-svg" viewBox="0 0 100 6" preserveAspectRatio="none"><rect class="bar-mod" x="0" y="0" width="' + wPct + '" height="6"/></svg>' +
      '</div>';
    }).join('');
    bindCards(listEl);
  }

  function renderFavs() {
    const favWeapons = state.favs.map((k) => WMAP[k]).filter(Boolean);
    document.getElementById('fav-count').textContent = '已收藏 ' + favWeapons.length + ' 把武器（仅存本机）';
    const wrap = document.getElementById('fav-cards');
    wrap.innerHTML = favWeapons.length
      ? favWeapons.map((w) => cardHtml(w, 'all')).join('')
      : '<div class="mini-box">暂无收藏武器。点击任意武器卡片右上角「☆ 收藏」即可加入此处，方便对局中秒查。</div>';
    bindCards(wrap);
  }

  function statBoxHtml(label, bVal, mVal) {
    if (bVal == null) return '';
    const b = Math.round(bVal);
    const m = mVal != null ? Math.round(mVal) : b;
    const d = m - b;
    const dHtml = d > 0 ? ' <span class="pos">+' + d + '</span>' : (d < 0 ? ' <span class="neg">' + d + '</span>' : '');
    const bPct = Math.min(100, Math.max(2, b));
    const mPct = Math.min(100, Math.max(2, m));
    return '<div class="mini-box">' +
      '<div class="mini-box-row"><span class="w-cls">' + esc(label) + '</span><span class="mono">' + b + '→<strong>' + m + '</strong>' + dHtml + '</span></div>' +
      '<svg class="bar-svg" viewBox="0 0 100 6" preserveAspectRatio="none">' +
        '<rect class="bar-base" x="0" y="0" width="' + bPct + '" height="6"/>' +
        '<rect class="bar-mod" x="0" y="0" width="' + mPct + '" height="6"/>' +
      '</svg>' +
    '</div>';
  }

  function renderSheet() {
    const backdrop = document.getElementById('detail-backdrop');
    const w = state.openKey ? WMAP[state.openKey] : null;
    if (!w) {
      backdrop.classList.add('hidden');
      return;
    }
    backdrop.classList.remove('hidden');
    document.getElementById('d-name').textContent = w.name;
    document.getElementById('d-cls').textContent = w.cls + ' · ' + w.fire;
    document.getElementById('d-arch').textContent = ((w.meta && w.meta.arch) || '') + (w.sweet_spot ? ' · 甜区 ' + w.sweet_spot : '');

    const favBtn = document.getElementById('d-fav-btn');
    const isFav = state.favs.indexOf(w.key) !== -1;
    favBtn.textContent = isFav ? '★ 已收藏' : '☆ 收藏';
    favBtn.classList.toggle('fav-on', isFav);

    const roleMap = { '近': 'cqb', '中近': 'mid', '中': 'mid', '远': 'long' };
    const defaultRole = (state.band !== 'all' && w.builds[roleMap[state.band]]) ? roleMap[state.band] : ((w.meta && w.meta.primary_role) || Object.keys(w.builds)[0]);
    const curRole = (state.openRole && w.builds[state.openRole]) ? state.openRole : defaultRole;
    const bd = w.builds[curRole];
    const b = w.base;
    const st = bd.st;

    const roleChips = Object.keys(w.builds).map((rk) =>
      '<button type="button" class="chip' + (rk === curRole ? ' active' : '') + '" data-srole="' + esc(rk) + '">' +
      esc(w.builds[rk].rc) + ' (' + w.builds[rk].c + '点)</button>'
    ).join('');

    const bpBoxes = (w.bp || []).slice(0, 4).map((seg) => {
      const rng = (seg.from === 0 && seg.to == null) ? '全距离' : (seg.to == null ? seg.from + 'm+' : seg.from + '–' + seg.to + 'm');
      return '<div class="mini-box mini-box-row"><span class="w-cls mono">' + esc(rng) + '</span><strong class="mono">' + seg.btk + '枪 / ' + seg.ttk + 'ms</strong></div>';
    }).join('');

    const itemsBoxes = (bd.it || []).map((it) =>
      '<div class="mini-box mini-box-row"><span><span class="w-cls">' + esc(it.slot_cn) + '：</span><strong>' + esc(it.cn) + '</strong>' +
      (it.default ? ' <span class="badge-meta">原厂</span>' : '') + '</span><span class="badge-meta">' + it.cost + '点</span></div>'
    ).join('');

    let prioHtml = '';
    if (bd.fk && bd.fk.length) {
      prioHtml += '<div class="notice-box"><strong>Lv.1 原厂保留别拆：</strong>' +
        bd.fk.map((x) => esc(x.slot_cn + '·' + x.cn + '(' + x.cost + '点)')).join('、') + '</div>';
    }
    if (bd.pr && bd.pr.length) {
      prioHtml += bd.pr.map((p, idx) => {
        const steps = (p.steps && p.steps.length)
          ? '<div class="prio-steps">低阶过渡：' + p.steps.map((s) => esc(s.cn + '(' + s.cost + '点, ~' + s.kept + '%)')).join(' → ') + '</div>'
          : '';
        return '<div class="prio-item"><div class="mini-box-row"><span><strong>#' + (idx + 1) + ' ' + esc(p.slot_cn) + '</strong> → ' + esc(p.cn) + ' (' + p.cost + '点)</span><span class="pos mono">+' + p.loss + '</span></div>' + steps + '</div>';
      }).join('');
    } else {
      prioHtml += '<div class="mini-box">原厂默认配件已高度成型，优先更换顺手瞄具即可。</div>';
    }

    const notesHtml = (w.notes && w.notes.length)
      ? '<div class="notice-box"><strong>特殊机制配件：</strong>' + esc(w.notes.join('；')) + '</div>'
      : '';

    const body = document.getElementById('detail-body');
    body.innerHTML =
      '<div><div class="section-title">切换配装流派</div><div class="chip-strip">' + roleChips + '</div></div>' +
      '<div><div class="section-title">素体伤害衰减 · 初速 ' + Math.round(b.v0) + '→' + Math.round(st.v0) + 'm/s · 弹匣 ' + b.mag + '→' + st.mag + ' · 开镜 ' + (b.adsTime || 0).toFixed(2) + '→' + (st.adsTime || 0).toFixed(2) + 's</div><div class="grid-2">' + bpBoxes + '</div></div>' +
      '<div><div class="section-title">素体 vs 满配四维对比</div><div class="grid-2">' +
        statBoxHtml('后坐控制', b.control, st.control) +
        statBoxHtml('连射精准', b.precision_est, st.precision_est) +
        statBoxHtml('腰射散布', b.hipfire, st.hipfire) +
        statBoxHtml('机动性能', b.mobility, st.mobility) +
      '</div></div>' +
      '<div><div class="section-title">100点满级毕业配件（' + bd.c + '/' + w.budget + '点）</div><div class="grid-2">' + itemsBoxes + '</div></div>' +
      '<div><div class="section-title">未满级前升级替换顺序（从 #1 开始换）</div><div class="card-list">' + prioHtml + '</div></div>' +
      notesHtml +
      '<button id="copy-loadout-btn" type="button" class="copy-btn">复制当前配装与过渡顺序</button>';

    body.querySelectorAll('[data-srole]').forEach((btn) => {
      btn.addEventListener('click', () => { state.openRole = btn.getAttribute('data-srole'); renderSheet(); });
    });

    document.getElementById('copy-loadout-btn').addEventListener('click', (e) => {
      const text = '【战地6配装】' + w.name + ' · ' + bd.rc + ' (' + bd.c + '/' + w.budget + '点)\n' +
        '配件：' + (bd.it || []).map((x) => x.slot_cn + '-' + x.cn + '(' + x.cost + ')').join(' / ') + '\n' +
        '未满级优先级：' + ((bd.pr || []).map((p, i) => '#' + (i + 1) + ' ' + p.slot_cn + '→' + p.cn).join('，') || '保持原厂');
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(() => { e.target.textContent = '✓ 已复制到剪贴板'; });
      }
    });
  }

  function renderNav() {
    document.querySelectorAll('.nav-btn').forEach((btn) => {
      btn.classList.toggle('active', btn.getAttribute('data-nav') === state.nav);
    });
    document.getElementById('filter-area').classList.toggle('hidden', state.nav !== 'hall');
    document.getElementById('tab-hall').classList.toggle('hidden', state.nav !== 'hall');
    document.getElementById('tab-ttk').classList.toggle('hidden', state.nav !== 'ttk');
    document.getElementById('tab-fav').classList.toggle('hidden', state.nav !== 'fav');
    document.getElementById('tab-rules').classList.toggle('hidden', state.nav !== 'rules');
  }

  function renderAll() {
    renderNav();
    renderChips();
    if (state.nav === 'hall') renderHall();
    if (state.nav === 'ttk') renderTtk();
    if (state.nav === 'fav') renderFavs();
  }

  if (PAYLOAD.meta && PAYLOAD.meta.date) {
    document.getElementById('meta-tag').textContent = 'v' + PAYLOAD.meta.date + ' · 离线零权限';
  }

  document.getElementById('search-box').addEventListener('input', (e) => { state.q = e.target.value; renderHall(); });
  document.getElementById('tier-sel').addEventListener('change', (e) => { state.tier = e.target.value; renderHall(); });
  document.querySelectorAll('.nav-btn').forEach((btn) => {
    btn.addEventListener('click', () => { state.nav = btn.getAttribute('data-nav'); renderAll(); });
  });
  document.getElementById('d-close-btn').addEventListener('click', () => { state.openKey = null; renderSheet(); });
  document.getElementById('detail-backdrop').addEventListener('click', (e) => {
    if (e.target.id === 'detail-backdrop') { state.openKey = null; renderSheet(); }
  });
  document.getElementById('d-fav-btn').addEventListener('click', () => {
    if (!state.openKey) return;
    const i = state.favs.indexOf(state.openKey);
    if (i === -1) state.favs.push(state.openKey);
    else state.favs.splice(i, 1);
    saveFavs(state.favs);
    renderSheet();
    renderAll();
  });

  if ('serviceWorker' in navigator && (location.protocol === 'https:' || location.hostname === 'localhost')) {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  }

  renderAll();
})();
