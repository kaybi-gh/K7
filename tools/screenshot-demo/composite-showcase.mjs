#!/usr/bin/env node
import { chromium } from '@playwright/test';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');
const screenshotsDir = path.resolve(repoRoot, 'screenshots');
const outputFile = path.join(screenshotsDir, 'movie-showcase-devices.png');

const sources = {
  tv: 'home-tv.png',
  desktop: 'movie-detail-sintel-desktop.png',
  mobile: 'movie-detail-sintel-mobile.png',
};

function toDataUrl(filePath) {
  const buffer = readFileSync(filePath);
  return `data:image/png;base64,${buffer.toString('base64')}`;
}

function readPngSize(filePath) {
  const buffer = readFileSync(filePath);
  return { width: buffer.readUInt32BE(16), height: buffer.readUInt32BE(20) };
}

function buildHtml(images, sizes) {
  const mobileAspect = `${sizes.mobile.width} / ${sizes.mobile.height}`;
  const desktopAspect = `${sizes.desktop.width} / ${sizes.desktop.height}`;
  const tvAspect = `${sizes.tv.width} / ${sizes.tv.height}`;

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }

    body {
      width: 2400px;
      height: 1350px;
      overflow: hidden;
    }

    .scene {
      position: relative;
      width: 100%;
      height: 100%;
    }

    .device {
      position: absolute;
      filter: drop-shadow(0 22px 44px rgba(0, 0, 0, 0.42));
    }

    .device img {
      display: block;
      width: 100%;
      height: auto;
      vertical-align: top;
      background: #000;
    }

    /* TV - back center */
    .device-tv {
      bottom: 20px;
      left: 20px;
      width: 1020px;
      z-index: 1;
    }

    .tv-shell {
      background: #111316;
      border: 2px solid #2a2e36;
      border-radius: 10px;
      padding: 12px 12px 14px;
    }

    .tv-screen {
      overflow: hidden;
      border-radius: 2px;
      background: #000;
      aspect-ratio: ${tvAspect};
    }

    .tv-stand {
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .tv-neck {
      width: 110px;
      height: 38px;
      background: linear-gradient(180deg, #1d2128 0%, #111316 100%);
      clip-path: polygon(12% 0, 88% 0, 100% 100%, 0 100%);
    }

    .tv-foot {
      width: 260px;
      height: 10px;
      border-radius: 0 0 6px 6px;
      background: #1d2128;
    }

    /* Laptop - front left, overlaps TV */
    .device-laptop {
      left: 800px;
      bottom: 20px;
      width: 780px;
      z-index: 2;
    }

    .laptop-lid {
      background: #111316;
      border: 2px solid #2a2e36;
      border-radius: 12px 12px 0 0;
      padding: 12px 12px 10px;
    }

    .laptop-camera {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      margin: 0 auto 8px;
      background: #2a3038;
    }

    .laptop-screen {
      overflow: hidden;
      border-radius: 2px;
      background: #000;
      aspect-ratio: ${desktopAspect};
    }

    .laptop-hinge {
      height: 4px;
      margin: 0 24px;
      background: linear-gradient(180deg, #3a404a, #1d2128);
      border-radius: 0 0 2px 2px;
    }

    .laptop-base {
      height: 28px;
      margin: 0 4px;
      border-radius: 0 0 16px 16px;
      background: linear-gradient(180deg, #343a44 0%, #1d2128 45%, #111316 100%);
      border: 2px solid #2a2e36;
      border-top: none;
    }

    .laptop-trackpad {
      width: 140px;
      height: 10px;
      margin: 7px auto 0;
      border-radius: 5px;
      background: #1a1e25;
    }

    /* Phone - front right, overlaps laptop */
    .device-phone {
      left: 1500px;
      bottom: 60px;
      width: 268px;
      z-index: 3;
    }

    .phone-shell {
      background: #111316;
      border: 2px solid #2a2e36;
      border-radius: 30px;
      padding: 8px;
    }

    .phone-screen-wrap {
      border-radius: 22px;
      overflow: hidden;
      background: #000;
      aspect-ratio: ${mobileAspect};
    }
  </style>
</head>
<body>
  <div class="scene">
    <div class="device device-tv">
      <div class="tv-shell">
        <div class="tv-screen"><img src="${images.tv}" alt="K7 home on TV" /></div>
      </div>
      <div class="tv-stand">
        <div class="tv-neck"></div>
        <div class="tv-foot"></div>
      </div>
    </div>

    <div class="device device-laptop">
      <div class="laptop-lid">
        <div class="laptop-camera"></div>
        <div class="laptop-screen"><img src="${images.desktop}" alt="Sintel on laptop" /></div>
      </div>
      <div class="laptop-hinge"></div>
      <div class="laptop-base">
        <div class="laptop-trackpad"></div>
      </div>
    </div>

    <div class="device device-phone">
      <div class="phone-shell">
        <div class="phone-screen-wrap">
          <img src="${images.mobile}" alt="Sintel on phone" />
        </div>
      </div>
    </div>
  </div>
</body>
</html>`;
}

async function main() {
  mkdirSync(screenshotsDir, { recursive: true });

  const missing = Object.entries(sources)
    .filter(([, file]) => !existsSync(path.join(screenshotsDir, file)))
    .map(([key, file]) => `${key}: ${file}`);

  if (missing.length > 0) {
    console.error('Missing screenshots:');
    for (const item of missing) {
      console.error(`  - ${item}`);
    }

    console.error('Run capture first, e.g.:');
    console.error('  K7_SCREENSHOTS_FILES=movie-detail-sintel-desktop.png,movie-detail-sintel-mobile.png npm run capture');
    process.exit(1);
  }

  const sizes = Object.fromEntries(
    Object.entries(sources).map(([key, file]) => [key, readPngSize(path.join(screenshotsDir, file))]),
  );

  const images = Object.fromEntries(
    Object.entries(sources).map(([key, file]) => [key, toDataUrl(path.join(screenshotsDir, file))]),
  );

  const html = buildHtml(images, sizes);
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({
    viewport: { width: 2400, height: 1350 },
    deviceScaleFactor: 1,
  });

  try {
    await page.setContent(html, { waitUntil: 'load' });
    await page.waitForTimeout(300);
    await page.screenshot({ path: outputFile, fullPage: false });
    console.log(`SHOWCASE -> ${outputFile}`);
  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
