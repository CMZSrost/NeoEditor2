'use strict';
/* D10 §二: JS 可视化组件库（统一模板）—— Section / Hero / ValueGrid / StatBar /
 * Badge（[data-nav] hover + ↗ 角标）/ LootTree / TopBar / RefPanel / Details。
 * 纯函数组件：输入快照 DTO（C# 预本地化），输出 HTMLElement。零宿主依赖。
 * 视觉规格：D04-D08 语义色 + D10 统一模板（§3.4 Section 单轨 / §3.7 可感知点击性）。
 * 组件包在 IIFE 内（不污染全局作用域 —— 否则 function el 等会与渲染器的
 * const { el } 解构冲突：Identifier 'el' has already been declared）。 */

(() => {
// ─────────────────────────── DOM 辅助 ───────────────────────────

/// 交互桥：由 app.js 挂到 window.VizActions（页面内 POST /viz/action，浏览器环境静默）
function postAction(kind, entityType, entityId) {
  if (window.VizActions) window.VizActions.postAction(kind, entityType, entityId);
}

function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v == null) continue;
    if (k === 'class') node.className = v;
    else if (k === 'text') node.textContent = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else if (k === 'dataset') Object.assign(node.dataset, v);
    else node.setAttribute(k, v);
  }
  for (const c of [].concat(children)) {
    if (c == null) continue;
    node.append(c.nodeType ? c : document.createTextNode(String(c)));
  }
  return node;
}

function fmtP(p) {
  const clamped = Math.max(0, Math.min(1, p));
  return (clamped * 100).toFixed(clamped * 100 % 1 === 0 ? 0 : 1).replace(/\.0$/, '') + '%';
}

/// 概率徽章渐变色（Avalonia BuildItemRow 同款：红(0%)→绿(100%)）
function probColor(p) {
  const t = Math.max(0, Math.min(1, p));
  let r, g, b;
  if (t < 0.5) { r = 198 + t * 2 * 57; g = t * 2 * 140; b = 40 + t * 2 * 10; }
  else { r = 255 - (t - 0.5) * 2 * 57; g = 140 + (t - 0.5) * 2 * 46; b = 50 - (t - 0.5) * 2 * 10; }
  return `#${Math.round(r).toString(16).padStart(2, '0')}${Math.round(g).toString(16).padStart(2, '0')}${Math.round(b).toString(16).padStart(2, '0')}`;
}

// ─────────────────────────── 徽章 / chip ───────────────────────────

function chip(c) {
  return el('span', { class: 'chip', style: `background:${c.bg};color:${c.fg}` }, c.label);
}

/// Badge（D10 §3.7：可跳转徽章统一 [data-nav] hover 态 + 静态 ↗ 角标）
/// text 与 label 兼容（BadgeDto.text / ConditionChipDto.label）
function badge(b) {
  const nav = !!(b.targetType && b.targetId);
  const text = b.text ?? b.label ?? '';
  const node = el('span', {
    class: 'badge' + (nav ? ' nav' : ''),
    style: `background:${b.bg};color:${b.fg}`,
  }, (b.icon ? b.icon + ' ' : '') + text);
  if (nav) {
    node.setAttribute('data-nav', '1');
    node.append(el('span', { class: 'nav-arrow', text: '↗' }));
    node.addEventListener('click', (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        postAction('navigate', b.targetType, b.targetId);
      }
    });
    node.addEventListener('contextmenu', (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        postAction('peek', b.targetType, b.targetId);
      }
    });
  }
  if (b.tooltip) bindTip(node, b.tooltip, text);
  return node;
}

function badgeRow(badges, prefix) {
  if (!badges || badges.length === 0) return null;
  const children = [];
  if (prefix) children.push(el('span', { class: 'chip', text: prefix }));
  children.push(...badges.map(badge));
  return el('div', { class: 'row' }, children);
}

/// hover tooltip（复杂信息住在这里，不在卡面 —— D06 §四）
const tipEl = el('div', { class: 'tip', style: 'display:none' });
document.addEventListener('DOMContentLoaded', () => document.body.append(tipEl));
let tipTimer = null;

function bindTip(target, text, title) {
  target.addEventListener('mouseenter', () => {
    tipTimer = setTimeout(() => {
      tipEl.innerHTML = '';
      if (title) tipEl.append(el('div', { class: 'tip-title', text: title }));
      tipEl.append(el('div', { class: 'tip-line', text }));
      const r = target.getBoundingClientRect();
      tipEl.style.left = Math.min(r.left, window.innerWidth - 300) + 'px';
      tipEl.style.top = (r.bottom + 6) + 'px';
      tipEl.style.display = 'block';
    }, 250);
  });
  target.addEventListener('mouseleave', () => { clearTimeout(tipTimer); tipEl.style.display = 'none'; });
}

function imageBlock(containerClass, placeholder, url) {
  const box = el('div', { class: containerClass });
  if (url) {
    const img = el('img', { src: url, alt: '' });
    img.addEventListener('error', () => { img.remove(); box.append(placeholder); });
    box.append(img);
  } else {
    box.append(placeholder);
  }
  return box;
}

// ─────────────────────────── Section（D10 §3.4 唯一区块头）───────────────────────────

/// 统一区块：图标 + 色条 + 标题 + 计数徽章 + 右侧操作区（collapsible 可选）。
/// opts: { icon, accent, count, right, collapsible, bodyClass }
function section(title, opts, body) {
  const { icon = null, accent = '#1565C0', count = null, right = null, collapsible = false } = opts || {};
  const head = el('div', { class: 'section-head' }, [
    el('span', { class: 'accent-bar', style: `background:${accent}` }),
    icon ? el('span', { class: 'section-icon', text: icon }) : null,
    el('span', { class: 'section-title', text: title }),
  ]);
  if (count != null) head.append(el('span', { class: 'section-count', text: String(count) }));
  if (right) head.append(right);

  let content = body;
  if (collapsible && body) {
    const details = el('details', { class: 'section-collapse' },
      [el('summary', { text: '' }), body]);
    details.open = true;
    content = details;
  }
  const sec = el('div', { class: 'section' }, [head, content]);
  return sec;
}

/// 两列并置（R40 AddRow 语义：某侧缺失时另一侧整行合并）
function twoCol(left, right) {
  if (left && right) return el('div', { class: 'two-col' }, [left, right]);
  return left ?? right;
}

// ─────────────────────────── Hero（D10 §二 统一身份区）───────────────────────────

/// s = 快照根；opts: { badges, subtitle, stats } —— badges 为预建 HTMLElement 行，
/// stats 为 FieldRowDto 关键数字行。默认徽章行 = ID + mod + 类型 chip（若 semantics 有）。
function hero(s, opts = {}) {
  const info = el('div', { class: 'hero-info' });

  const badges = opts.badges ?? [];
  const idRow = el('div', { class: 'row' });
  idRow.append(el('span', { class: 'chip', style: 'background:#e3f2fd;color:#1565c0;font-weight:700',
    text: `ID: ${s.id}` }));
  if (s.modId != null) idRow.append(el('span', { class: 'chip', style: 'background:#f3e5f5;color:#6a1b9a',
    text: `mod ${s.modId}` }));
  for (const b of badges) idRow.append(b);
  info.append(idRow);

  info.append(el('div', { class: 'hero-title', text: s.displayName }));
  if (opts.subtitle) info.append(el('div', { class: 'hero-subtitle', text: opts.subtitle }));

  if (opts.stats && opts.stats.length) {
    const row = el('div', { class: 'hero-chance' });
    for (const st of opts.stats) row.append(el('span', { style: st.color ? `color:${st.color}` : '', text: st.value }));
    info.append(row);
  }

  return el('div', { class: 'card hero' }, [
    imageBlock('hero-img', el('span', { class: 'placeholder', text: '📖' }), s.image),
    info,
  ]);
}

// ─────────────────────────── ValueGrid（薄类型模板主力 / 数值格）───────────────────────────

/// FieldRowDto[] → 两列键值格（label 9px 灰上，value 13px 半粗下；color 着色）
function valueGrid(rows, cols = 2) {
  if (!rows || rows.length === 0) return null;
  const grid = el('div', { class: 'value-grid', dataset: { cols: String(cols) } });
  for (const r of rows) {
    grid.append(el('div', { class: 'value-cell' }, [
      el('div', { class: 'value-label', text: r.label || '' }),
      el('div', { class: 'value-num', style: r.color ? `color:${r.color}` : '', text: r.value }),
    ]));
  }
  return grid;
}

// ─────────────────────────── StatBar（D04/D05：stacked / centered）───────────────────────────

/// StatBarDto：stacked = 段占比条；centered = 相对 Max 填充条 + 文本；
/// bipolar = 零中心双向条（正值向右 / 负值向左，负值色 NegativeColor ?? #C62828）。
function statBar(bar) {
  if (!bar) return null;
  const wrap = el('div', { class: 'statbar' });
  if (bar.text) wrap.append(el('span', { class: 'statbar-text', text: bar.text }));
  const segs = bar.segments || [];
  if (bar.mode === 'bipolar' && bar.max != null && segs.length) {
    const total = segs.reduce((a, s) => a + Math.abs(s.value), 0) || 1;
    const track = el('div', { class: 'statbar-track bipolar' });
    for (const s of segs) {
      const v = Number(s.value) || 0;
      if (v === 0) continue;
      const w = (Math.abs(v) / Math.max(Math.abs(bar.max), 0.0001)) * 50;
      const seg = el('div', {
        class: 'statbar-seg',
        style: `width:${Math.min(w, 50).toFixed(1)}%;background:${v > 0 ? s.color : (bar.negativeColor ?? '#C62828')}`,
      });
      if (v < 0) seg.style.marginLeft = 'auto';
      track.append(seg);
    }
    wrap.append(track);
  } else if (bar.mode === 'centered' && bar.max != null) {
    const ratio = Math.max(0, Math.min(1, (segs[0]?.value ?? 0) / Math.max(bar.max, 0.0001)));
    wrap.append(el('div', { class: 'statbar-track' },
      el('div', { class: 'statbar-fill', style: `width:${(ratio * 100).toFixed(1)}%;background:${bar.posColor ?? segs[0]?.color ?? '#4CAF50'}` })));
  } else {
    const total = segs.reduce((a, s) => a + Math.max(0, s.value), 0);
    if (total > 0) {
      const track = el('div', { class: 'statbar-track stacked' });
      for (const s of segs) {
        if (s.value <= 0) continue;
        track.append(el('div', {
          class: 'statbar-seg',
          style: `width:${((Math.max(0, s.value) / total) * 100).toFixed(1)}%;background:${s.color}`,
        }));
      }
      wrap.append(track);
    }
  }
  return wrap;
}

// ─────────────────────────── LightGrid（HexType 光照：6 时段热力格，Avalonia 同款）───────────────────────────

/// LightCellDto[] → 从早到晚横排：时段名在上、热力色块内数值（红→黄→绿插值色由 C# 预计算）。
function lightGrid(cells) {
  const grid = el('div', { class: 'light-grid' });
  for (const c of cells) {
    grid.append(el('div', { class: 'light-cell' }, [
      el('span', { class: 'light-label', text: c.label }),
      el('div', { class: 'light-value', style: `background:${c.bg};color:${c.fg}`, text: c.value }),
    ]));
  }
  return grid;
}

// ─────────────────────────── LootTree（P1 战利品嵌套树，D04 语义）───────────────────────────

/// LootTreeDto → 树：物品行 = 名称 | 概率徽章(权重+概率%) | 数量；嵌套 TT 行可折叠。
function lootTree(tree, depth = 0) {
  if (!tree || !tree.items || tree.items.length === 0) return null;
  const root = el('div', { class: 'loot-tree' });
  if (tree.title) {
    root.append(el('div', { class: 'loot-tree-title' }, [badge({
      text: tree.title, bg: '#E8EAF6', fg: '#283593',
      targetType: tree.targetType, targetId: tree.targetId,
    })]));
  }
  for (const node of tree.items) root.append(lootNode(node, depth));
  return root;
}

function lootNode(node, depth) {
  const row = el('div', { class: 'loot-row' + (node.kind === 'table' ? ' loot-table' : ''), dataset: { depth: String(depth) } });
  const color = probColor(node.prob);
  row.append(el('span', { class: 'loot-name' }, node.label));
  row.append(el('span', {
    class: 'loot-prob',
    style: `color:${color}`,
  }, `${Number(node.weight).toFixed(1)}(${fmtP(node.prob)})`));
  if (node.qty && node.qty !== '1') row.append(el('span', { class: 'loot-qty', text: `×${node.qty}` }));

  if (node.kind === 'table' && node.children && node.children.length) {
    const childrenBox = el('div', { class: 'loot-children' });
    for (const c of node.children) childrenBox.append(lootNode(c, depth + 1));
    row.classList.add('collapsible');
    row.append(el('span', { class: 'loot-arrow', text: '▼' }));
    // P2 §3.2: 展开状态记忆（bindExpand 统一协议，.open 控制）
    const wrap = el('div', { class: 'loot-wrap', dataset: { expandKey: `loot:${node.targetId ?? node.label}` } }, [row, childrenBox]);
    bindExpand(wrap, `loot:${node.targetId ?? node.label}`);
    const syncArrow = () => {
      row.querySelector('.loot-arrow').textContent = wrap.classList.contains('open') ? '▼' : '▶';
    };
    wrap.addEventListener('click', syncArrow);
    syncArrow();
    return wrap;
  }

  if (node.kind === 'item' && node.targetType && node.targetId) {
    row.classList.add('nav');
    row.setAttribute('data-nav', '1');
    row.append(el('span', { class: 'nav-arrow', text: '↗' }));
    row.addEventListener('click', (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        postAction('navigate', node.targetType, node.targetId);
      }
    });
    row.addEventListener('contextmenu', (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        postAction('peek', node.targetType, node.targetId);
      }
    });
  }
  return row;
}

// ─────────────────────────── TopBar（D10 §二 页面工具条）───────────────────────────

/// 类型名 + 审计统计（N 字段 · M 有值 · K 未解析）+ P2 ← 返回（组件内导航历史 §3.1）。
/// opts: { canBack, onBack }
function topBar(s, opts = {}) {
  const bar = el('div', { class: 'topbar' });
  if (opts.canBack) {
    const back = el('button', { class: 'back-btn topbar-back', text: '← 返回' });
    back.addEventListener('click', () => opts.onBack && opts.onBack());
    bar.append(back);
  }
  bar.append(el('span', { class: 'chip topbar-type', style: 'background:#ECEFF1;color:#546E7A', text: s.type }));
  if (s.audit) {
    bar.append(el('span', {
      class: 'topbar-audit', title: '字段统计（Raw Data 审计）', text: s.audit.text || '',
    }));
  }
  return bar;
}

// ─────────────────────────── RefPanel（D10 §3.6：聚合摘要 + 过滤 + 滚动加载）───────────────────────────

/// 每个类型组的滚动加载批大小
const REF_PAGE = 20;

function refPanel(summary) {
  if (!summary || !summary.groups || summary.groups.length === 0) return null;
  const content = el('div', {});

  const parts = summary.groups.map(g => `${g.count} ${g.typeName}`);
  content.append(el('div', { class: 'ref-summary', text: `被 ${parts.join(' · ')} 引用` }));

  // 过滤框：名称/id 前缀即时过滤（组内徽章 + 计数联动）
  const filter = el('input', {
    class: 'ref-filter', type: 'search', placeholder: '过滤引用…',
  });
  content.append(filter);

  const groupsBox = el('div', { class: 'ref-groups' });
  content.append(groupsBox);

  // 组数据池（全量徽章，来自快照；过滤/滚动加载只作用于渲染层）
  const pools = summary.groups.map(g => ({
    title: `${g.typeName}（${g.count}）`,
    items: g.items || [],
    more: g.more || 0,
  }));

  function matches(item, q) {
    const text = ((item.text ?? item.label) || '').toLowerCase();
    const target = ((item.targetId ?? '') || '').toLowerCase();
    return text.includes(q) || target.includes(q);
  }

  function renderGroups(q) {
    groupsBox.innerHTML = '';
    const query = (q || '').trim().toLowerCase();
    for (const pool of pools) {
      const visible = query ? pool.items.filter(it => matches(it, query)) : pool.items;
      const groupEl = el('div', { class: 'ref-group' });
      groupEl.append(el('span', {
        class: 'ref-group-title',
        text: query ? `▸ ${pool.title}（${visible.length}）` : `▸ ${pool.title}`,
      }));
      const list = el('div', { class: 'row ref-list' });
      groupEl.append(list);

      // 滚动加载：首批 REF_PAGE，哨兵出现时补一批（IntersectionObserver）
      let shown = 0;
      const appendBatch = () => {
        const next = visible.slice(shown, shown + REF_PAGE);
        for (const b of next) list.append(badge(b));
        shown += next.length;
        if (shown < visible.length) {
          const sentinel = el('div', { class: 'ref-sentinel', text: `+${visible.length - shown} more…` });
          list.append(sentinel);
          io.observe(sentinel);
        }
      };
      const io = new IntersectionObserver((entries) => {
        for (const en of entries) {
          if (en.isIntersecting) {
            io.unobserve(en.target);
            en.target.remove();
            appendBatch();
          }
        }
      }, { rootMargin: '200px' });
      appendBatch();
      groupsBox.append(groupEl);
    }
  }

  filter.addEventListener('input', () => renderGroups(filter.value));
  renderGroups('');
  return section('被引用', { icon: '🔗', accent: '#455A64' }, content);
}

// ─────────────────────────── 展开状态（P2 §3.2 状态记忆）───────────────────────────

/// 可展开元素统一协议：带 data-expand-key 的容器，.open 类控制显示；状态经
/// window.VizUiState（app.js 挂载：sessionStorage 持久化 + 滚动/展开跨刷新保留）。
function bindExpand(el, key) {
  el.classList.add('expandable');
  if (window.VizUiState?.expanded?.has(key)) el.classList.add('open');
  el.addEventListener('click', (e) => {
    if (e.ctrlKey || e.metaKey) return;
    const open = el.classList.toggle('open');
    window.VizUiState?.toggle?.(key, open);
  });
}

/// 渲染完成后恢复展开状态（[data-expand-key] 元素 → .open）。
function restoreExpands(root = document) {
  const state = window.VizUiState?.expanded;
  if (!state) return;
  root.querySelectorAll('[data-expand-key]').forEach(el2 => {
    const key = el2.getAttribute('data-expand-key');
    if (state.has(key)) el2.classList.add('open');
  });
}

// ─────────────────────────── Details（Raw XML 折叠，页面底部）───────────────────────────

function details(rawXml) {
  if (!rawXml) return null;
  const d = el('details', { class: 'raw-xml', dataset: { expandKey: 'rawxml' } },
    [el('summary', { text: 'Raw XML' }), el('pre', { text: rawXml })]);
  if (window.VizUiState?.expanded?.has('rawxml')) d.open = true;
  d.addEventListener('toggle', () => window.VizUiState?.toggle?.('rawxml', d.open));
  return d;
}

// 组件库导出（供 renderers.js / app.js 使用）
window.VizComponents = {
  el, fmtP, probColor, chip, badge, badgeRow, bindTip, imageBlock,
  section, twoCol, hero, valueGrid, statBar, lightGrid, lootTree, topBar, refPanel, details,
  bindExpand, restoreExpands,
};
})();
