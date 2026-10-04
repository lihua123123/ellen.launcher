/**
 * gen.mjs — 把 ellen-wiki 的 content/formulas/genshin.md 转成启动器可用的结构化数据。
 *
 * 产物：
 *   1) Resource/Formulas/GenshinFormulas.json       —— 章节 / 段落 / 公式 / 表格 / 引用（单一数据源）
 *   2) Resource/Formulas/Genshin/{dark,light}/fNNNN.png —— 每个【块公式】按主题预渲染的位图
 *
 * 为什么是 PNG 而不是 SVG：WinUI 3 的 SvgImageSource 对 MathJax 输出（大量 <path>/<use>）
 * 支持情况不确定，且 currentColor 在不同主题下无法自动切换。PNG 由 sharp/librsvg 光栅化，
 * 3× 密度，亮/暗各一套，取用零风险。
 *
 * 行内公式不进图：genshin.md 里的行内公式都很简单（`90\%` / `(6+5\times元素量)`），
 * 由 sanitizeInlineMath() 转成普通文本，交给现有的 GuideRichText 渲染。
 *
 * 用法：node gen.mjs [--wiki <ellen-wiki 根目录>] [--out <Ellen.Launcher 项目目录>]
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { mathjax } from 'mathjax-full/js/mathjax.js';
import { TeX } from 'mathjax-full/js/input/tex.js';
import { SVG } from 'mathjax-full/js/output/svg.js';
import { liteAdaptor } from 'mathjax-full/js/adaptors/liteAdaptor.js';
import { RegisterHTMLHandler } from 'mathjax-full/js/handlers/html.js';
import { AllPackages } from 'mathjax-full/js/input/tex/AllPackages.js';
import sharp from 'sharp';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function argValue(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : fallback;
}

const WIKI_ROOT = argValue('--wiki', 'D:\\艾莲的数据库\\ellen-wiki');
const OUT_PROJECT = argValue('--out', path.resolve(__dirname, '..', '..', 'src', 'Ellen.Launcher', 'Ellen.Launcher'));

const RESOURCE_DIR = path.join(OUT_PROJECT, 'Resource', 'Formulas');
const GFX_DIR = path.join(RESOURCE_DIR, 'Genshin');
const JSON_PATH = path.join(RESOURCE_DIR, 'GenshinFormulas.json');

const EM = 16;
const EX = 8;
const SCALE = 3;

/* ---------------- MathJax 环境 ---------------- */

const adaptor = liteAdaptor();
RegisterHTMLHandler(adaptor);
const texInput = new TeX({ packages: AllPackages });
const svgOutput = new SVG({ fontCache: 'none' });
const mjDocument = mathjax.document('', { InputJax: texInput, OutputJax: svgOutput });

function renderSvg(tex, display) {
  const node = mjDocument.convert(tex, {
    display,
    em: EM,
    ex: EX,
    containerWidth: 80 * EM,
  });
  const html = adaptor.outerHTML(node);
  const match = html.match(/<svg[\s\S]*<\/svg>/);
  if (!match) {
    throw new Error('MathJax 未产出 <svg>');
  }
  return match[0];
}

/** 返回 { svg, widthDip, heightDip } */
function measure(tex, display) {
  const svg = renderSvg(tex, display);
  const w = svg.match(/width="([\d.]+)ex"/);
  const h = svg.match(/height="([\d.]+)ex"/);
  if (!w || !h) {
    throw new Error('无法从 MathJax SVG 读取尺寸');
  }
  return {
    svg,
    widthDip: Math.round(Number(w[1]) * EX),
    heightDip: Math.round(Number(h[1]) * EX),
  };
}

/* ---------------- 行内文本清洗 ---------------- */

const INLINE_MATH_RE = /(?<!\$)\$(?!\$)([^$\n]+?)(?<!\$)\$(?!\$)/g;

const TEX_TEXT_MAP = [
  [/\\times/g, '×'],
  [/\\cdot/g, '·'],
  [/\\to/g, '→'],
  [/\\leqslant/g, '≤'],
  [/\\geqslant/g, '≥'],
  [/\\le\b/g, '≤'],
  [/\\ge\b/g, '≥'],
  [/\\pm/g, '±'],
  [/\\%/g, '%'],
  [/\\ /g, ' '],
  [/\\[,;:!]/g, ''],
  [/\\left\.?|\\right\.?/g, ''],
  [/[{}]/g, ''],
];

function sanitizeInlineMath(src) {
  let s = src.trim();
  for (const [re, to] of TEX_TEXT_MAP) {
    s = s.replace(re, to);
  }
  return s.replace(/\s+/g, ' ').trim();
}

/**
 * 把一段 wiki 行内文本转成 GuideRichText 能吃的标记文本。
 * - 行内公式 `$...$` → 普通文本
 * - `==高亮==` → `**加粗**`（GuideRichText 没有 mark 概念）
 * - 单个 `*斜体*` → 去掉星号（多余强调标记会原样显示）
 * - 其余 `**加粗**` / `` `代码` `` / `<span class="x">` 原样保留
 */
function sanitizeText(src) {
  let s = src.replace(INLINE_MATH_RE, (_m, math) => sanitizeInlineMath(math));
  s = s.replace(/==([^=]+?)==/g, '**$1**');
  s = s.replace(/(?<!\*)\*(?!\*)([^*\n]+?)\*(?!\*)/g, '$1');
  return s;
}

/* ---------------- Markdown 结构解析 ---------------- */

function isTableLine(line) {
  return /^\s*\|.*\|\s*$/.test(line);
}

function isTableSeparator(line) {
  return /^\s*\|[\s:|-]+\|\s*$/.test(line) && line.includes('-');
}

function splitTableRow(line) {
  let s = line.trim();
  s = s.replace(/^\|/, '').replace(/\|$/, '');
  return s.split('|').map((c) => c.trim());
}

let formulaCounter = 0;
const formulas = [];
/** tex -> { image, width, height } */
const formulaCache = new Map();

function addFormula(tex) {
  const key = tex.trim();
  if (formulaCache.has(key)) {
    return formulaCache.get(key);
  }
  const body = `\\def\\arraystretch{1.6} ${key}`;
  const { svg, widthDip, heightDip } = measure(body, true);
  const id = `f${String(++formulaCounter).padStart(4, '0')}`;
  formulas.push({ id, svg });
  const info = { image: id, width: widthDip, height: heightDip };
  formulaCache.set(key, info);
  return info;
}

/**
 * 解析一个“块容器”的若干行（章节正文 / 引用块内部）。
 * @returns {Array<{kind:string,...}>}
 */
function parseBlocks(lines) {
  const blocks = [];
  const para = [];

  const flushPara = () => {
    if (para.length) {
      blocks.push({ kind: 'p', text: sanitizeText(para.join('\n')) });
      para.length = 0;
    }
  };

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i];
    const line = raw.replace(/\s+$/, '');

    if (line.trim() === '') {
      flushPara();
      continue;
    }

    // 分隔线
    if (/^-{3,}$/.test(line.trim())) {
      flushPara();
      continue;
    }

    // 子标题
    const h = line.match(/^(#{3,6})\s+(.*)$/);
    if (h) {
      flushPara();
      blocks.push({ kind: 'h', level: h[1].length, text: sanitizeText(h[2].trim()) });
      continue;
    }

    // 块公式（可跨行）
    if (line.trim().startsWith('$$')) {
      flushPara();
      let body = line.trim().slice(2);
      const closedOnSameLine = body.includes('$$');
      if (closedOnSameLine) {
        body = body.slice(0, body.indexOf('$$'));
      } else {
        const buf = [body];
        let j = i + 1;
        for (; j < lines.length; j++) {
          const l = lines[j];
          const end = l.indexOf('$$');
          if (end >= 0) {
            buf.push(l.slice(0, end));
            break;
          }
          buf.push(l);
        }
        i = j;
        body = buf.join('\n');
      }
      const info = addFormula(body);
      blocks.push({ kind: 'formula', ...info, tex: body.trim() });
      continue;
    }

    // 表格
    if (isTableLine(line)) {
      flushPara();
      const tableLines = [];
      let j = i;
      for (; j < lines.length && isTableLine(lines[j]); j++) {
        tableLines.push(lines[j]);
      }
      i = j - 1;
      const rows = tableLines
        .filter((l) => !isTableSeparator(l))
        .map(splitTableRow);
      if (rows.length) {
        blocks.push({
          kind: 'table',
          head: rows[0].map(sanitizeText),
          rows: rows.slice(1).map((r) => r.map(sanitizeText)),
        });
      }
      continue;
    }

    // 引用块
    if (/^\s*>/.test(line)) {
      flushPara();
      const quoteLines = [];
      let j = i;
      for (; j < lines.length; j++) {
        const l = lines[j];
        if (/^\s*>/.test(l)) {
          quoteLines.push(l.replace(/^\s*>\s?/, ''));
        } else if (l.trim() === '') {
          // 引用块内的空行只在下一行仍是引用时保留
          if (j + 1 < lines.length && /^\s*>/.test(lines[j + 1])) {
            quoteLines.push('');
          } else {
            break;
          }
        } else {
          break;
        }
      }
      i = j - 1;
      const inner = parseBlocks(quoteLines);
      if (inner.length) {
        blocks.push({ kind: 'quote', blocks: inner });
      }
      continue;
    }

    para.push(line);
  }

  flushPara();
  return blocks;
}

/* ---------------- 主流程 ---------------- */

const mdPath = path.join(WIKI_ROOT, 'content', 'formulas', 'genshin.md');
if (!fs.existsSync(mdPath)) {
  console.error(`找不到源文件：${mdPath}`);
  process.exit(1);
}

const markdown = fs.readFileSync(mdPath, 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const lines = markdown.split('\n');

const sections = [];
let current = null;

for (const line of lines) {
  const h2 = line.match(/^##\s+(.+?)\s*$/);
  if (h2) {
    current = { title: h2[1].trim(), lines: [] };
    sections.push(current);
    continue;
  }
  if (current) {
    current.lines.push(line);
  }
}

const model = {
  game: 'genshin',
  title: '原神 · 伤害计算公式',
  sections: sections.map((s) => ({
    title: s.title,
    blocks: parseBlocks(s.lines),
  })),
};

// 重排公式编号（按出现顺序已保证），写出图片
fs.mkdirSync(path.join(GFX_DIR, 'dark'), { recursive: true });
fs.mkdirSync(path.join(GFX_DIR, 'light'), { recursive: true });

const themeColors = { dark: '#FFFFFF', light: '#000000' };

for (const { id, svg } of formulas) {
  for (const [theme, color] of Object.entries(themeColors)) {
    const colored = svg.replace(/currentColor/g, color);
    const png = await sharp(Buffer.from(colored), { density: 96 * SCALE })
      .png({ compressionLevel: 9 })
      .toBuffer();
    fs.writeFileSync(path.join(GFX_DIR, theme, `${id}.png`), png);
  }
}

// 清理旧的公式图片（同一 id 集合之外的文件）
const known = new Set(formulas.map((f) => `${f.id}.png`));
for (const theme of Object.keys(themeColors)) {
  for (const f of fs.readdirSync(path.join(GFX_DIR, theme))) {
    if (!known.has(f)) {
      fs.unlinkSync(path.join(GFX_DIR, theme, f));
    }
  }
}

fs.mkdirSync(RESOURCE_DIR, { recursive: true });
fs.writeFileSync(JSON_PATH, JSON.stringify(model, null, 2) + '\n', 'utf8');

const blockCount = model.sections.reduce((n, s) => n + s.blocks.length, 0);
console.log(`章节 ${model.sections.length} / 区块 ${blockCount} / 块公式 ${formulas.length}（每公式 2 张 PNG）`);
console.log(`JSON  -> ${JSON_PATH}`);
console.log(`PNG   -> ${GFX_DIR}\\{dark,light}\\*.png`);
