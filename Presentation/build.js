#!/usr/bin/env node
'use strict';

// Bundles index.html + styles.css + charts.js + presentation.js + assets/*.png into
// one self-contained Presentation/dist/presentation.html, then minifies the result
// with html-minifier-terser (which minifies the inlined <style>/<script> too via
// clean-css/terser). Run via `make presentation-bundle` (Docker + Node), or directly
// with Node installed: node Presentation/build.js
//
// CDN-hosted dependencies (fonts, Shoelace, Tailwind, Mermaid, ECharts) and the
// hardware slide's external VRAM-calculator iframe are left untouched — bundling
// those would mean vendoring entire third-party libraries, and an internet
// connection is already a documented assumption for this deck.

const fs = require('fs');
const path = require('path');

const ROOT = __dirname;
const OUT_DIR = path.join(ROOT, 'dist');
const OUT_FILE = path.join(OUT_DIR, 'presentation.html');

const MIME_TYPES = {
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.svg': 'image/svg+xml',
};

function inlineImages(html) {
  return html.replace(/(src=")(assets\/[^"]+)(")/g, (_match, pre, relPath, post) => {
    const imgPath = path.join(ROOT, relPath);
    const mime = MIME_TYPES[path.extname(imgPath).toLowerCase()] || 'application/octet-stream';
    const data = fs.readFileSync(imgPath).toString('base64');
    return `${pre}data:${mime};base64,${data}${post}`;
  });
}

function bundle() {
  let html = fs.readFileSync(path.join(ROOT, 'index.html'), 'utf8');
  const css = fs.readFileSync(path.join(ROOT, 'styles.css'), 'utf8');
  const chartsJs = fs.readFileSync(path.join(ROOT, 'charts.js'), 'utf8');
  const presentationJs = fs.readFileSync(path.join(ROOT, 'presentation.js'), 'utf8');

  const linkTag = '<link rel="stylesheet" href="styles.css" />';
  if (!html.includes(linkTag)) {
    throw new Error('build: could not find the styles.css <link> tag in index.html');
  }
  html = html.replace(linkTag, `<style>${css}</style>`);

  const scriptTags = '<script src="charts.js"></script>\n<script src="presentation.js"></script>';
  if (!html.includes(scriptTags)) {
    throw new Error('build: could not find the charts.js/presentation.js <script> tags in index.html');
  }
  html = html.replace(scriptTags, `<script>${chartsJs}\n${presentationJs}</script>`);

  return inlineImages(html);
}

async function minifyHtml(html) {
  let minify;
  try {
    ({ minify } = require('html-minifier-terser'));
  } catch (err) {
    console.warn('html-minifier-terser is not installed — writing the unminified bundle instead.');
    return html;
  }
  return minify(html, {
    collapseWhitespace: true,
    removeComments: true,
    minifyCSS: true,
    minifyJS: true,
  });
}

async function main() {
  const bundled = bundle();
  const minified = await minifyHtml(bundled);

  fs.mkdirSync(OUT_DIR, { recursive: true });
  fs.writeFileSync(OUT_FILE, minified, 'utf8');

  const { size } = fs.statSync(OUT_FILE);
  console.log(`Wrote ${OUT_FILE} (${(size / 1024).toFixed(1)} KB)`);
}

main().catch((err) => {
  console.error(err.message);
  process.exit(1);
});
