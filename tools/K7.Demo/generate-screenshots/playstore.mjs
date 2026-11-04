#!/usr/bin/env node
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { deflateSync } from 'node:zlib';
import { chromium } from '@playwright/test';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../..');
const outputDir = path.resolve(repoRoot, 'screenshots/play');
const configPath = path.join(__dirname, 'playstore.config.json');
const iconSource = path.resolve(repoRoot, 'branding/icon.png');
const logoSource = path.resolve(repoRoot, 'branding/logo.svg');

const featureWidth = 1024;
const featureHeight = 500;
const iconSize = 512;

function runCapture() {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [path.join(__dirname, 'capture.mjs')], {
      cwd: __dirname,
      stdio: 'inherit',
      env: {
        ...process.env,
        K7_SCREENSHOTS_CONFIG: configPath,
      },
    });
    child.on('error', reject);
    child.on('exit', (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`capture exited with code ${code}`));
    });
  });
}

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) {
      const mask = -(crc & 1);
      crc = (crc >>> 1) ^ (0xedb88320 & mask);
    }
  }

  return (crc ^ 0xffffffff) >>> 0;
}

function pngChunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const typeBuf = Buffer.from(type);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([typeBuf, data])));
  return Buffer.concat([length, typeBuf, data, crc]);
}

function encodePng(width, height, pixels, colorType) {
  const channels = colorType === 6 ? 4 : 3;
  const stride = width * channels;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    const dest = y * (stride + 1);
    raw[dest] = 0;
    pixels.copy(raw, dest + 1, y * stride, y * stride + stride);
  }

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = colorType;

  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  return Buffer.concat([
    signature,
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', deflateSync(raw)),
    pngChunk('IEND', Buffer.alloc(0)),
  ]);
}

function pngSize(filePath) {
  const buffer = readFileSync(filePath);
  return {
    width: buffer.readUInt32BE(16),
    height: buffer.readUInt32BE(20),
    colorType: buffer[25],
  };
}

function toDataUrl(filePath, mime) {
  const buffer = readFileSync(filePath);
  return `data:${mime};base64,${buffer.toString('base64')}`;
}

async function readImagePixels(page, dataUrl, width, height) {
  await page.setContent(`<!DOCTYPE html>
<html>
<head>
  <style>
    html, body { margin: 0; width: ${width}px; height: ${height}px; overflow: hidden; background: #0d0907; }
    img { width: ${width}px; height: ${height}px; display: block; }
  </style>
</head>
<body><img id="source" src="${dataUrl}" /></body>
</html>`, { waitUntil: 'load' });

  const bytes = await page.evaluate(async ({ width: targetWidth, height: targetHeight }) => {
    const image = document.querySelector('#source');
    await image.decode();
    const canvas = document.createElement('canvas');
    canvas.width = targetWidth;
    canvas.height = targetHeight;
    const context = canvas.getContext('2d', { willReadFrequently: true });
    context.drawImage(image, 0, 0, targetWidth, targetHeight);
    return Array.from(context.getImageData(0, 0, targetWidth, targetHeight).data);
  }, { width, height });

  return Buffer.from(bytes);
}

function rgbaToRgb(rgba) {
  const rgb = Buffer.alloc((rgba.length / 4) * 3);
  let dest = 0;
  for (let i = 0; i < rgba.length; i += 4) {
    rgb[dest++] = rgba[i];
    rgb[dest++] = rgba[i + 1];
    rgb[dest++] = rgba[i + 2];
  }

  return rgb;
}

async function writeIcon(page) {
  const rgba = await readImagePixels(page, toDataUrl(iconSource, 'image/png'), iconSize, iconSize);
  const out = path.join(outputDir, 'icon.png');
  writeFileSync(out, encodePng(iconSize, iconSize, rgba, 6));
  console.log(`ICON ${out}`);
}

async function writeFeatureGraphic(browser) {
  const phonePath = path.join(outputDir, 'phone-home.png');
  if (!existsSync(phonePath)) {
    throw new Error(`Missing ${phonePath}. The home capture did not complete.`);
  }

  const page = await browser.newPage({
    viewport: { width: featureWidth, height: featureHeight },
    deviceScaleFactor: 1,
  });

  const logo = toDataUrl(logoSource, 'image/svg+xml');
  const phone = toDataUrl(phonePath, 'image/png');
  await page.setContent(`<!DOCTYPE html>
<html>
<head>
  <style>
    html, body {
      margin: 0;
      width: ${featureWidth}px;
      height: ${featureHeight}px;
      overflow: hidden;
      background: #0d0907;
    }
    .scene {
      width: 1024px;
      height: 500px;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 72px;
    }
    .logo {
      width: 329px;
      height: 180px;
    }
    .phone {
      width: 248px;
      height: 440px;
      border-radius: 28px;
      overflow: hidden;
      border: 1px solid rgba(243, 236, 222, 0.14);
      box-shadow: 0 18px 40px rgba(0, 0, 0, 0.45);
    }
    .phone img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
  </style>
</head>
<body>
  <div class="scene">
    <img class="logo" src="${logo}" alt="" />
    <div class="phone"><img src="${phone}" alt="" /></div>
  </div>
</body>
</html>`, { waitUntil: 'load' });

  await page.locator('.logo').evaluate((node) => node.decode());
  await page.locator('.phone img').evaluate((node) => node.decode());
  const rgba = await page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = 1024;
    canvas.height = 500;
    const context = canvas.getContext('2d', { willReadFrequently: true });
    context.fillStyle = '#0d0907';
    context.fillRect(0, 0, 1024, 500);

    const logo = document.querySelector('.logo');
    const phone = document.querySelector('.phone img');
    const logoWidth = 329;
    const logoHeight = 180;
    const phoneWidth = 248;
    const phoneHeight = 440;
    const gap = 72;
    const groupWidth = logoWidth + gap + phoneWidth;
    const originX = Math.round((1024 - groupWidth) / 2);
    context.drawImage(logo, originX, Math.round((500 - logoHeight) / 2), logoWidth, logoHeight);

    const phoneX = originX + logoWidth + gap;
    const phoneY = Math.round((500 - phoneHeight) / 2);
    const radius = 28;
    context.save();
    context.beginPath();
    context.moveTo(phoneX + radius, phoneY);
    context.arcTo(phoneX + phoneWidth, phoneY, phoneX + phoneWidth, phoneY + phoneHeight, radius);
    context.arcTo(phoneX + phoneWidth, phoneY + phoneHeight, phoneX, phoneY + phoneHeight, radius);
    context.arcTo(phoneX, phoneY + phoneHeight, phoneX, phoneY, radius);
    context.arcTo(phoneX, phoneY, phoneX + phoneWidth, phoneY, radius);
    context.closePath();
    context.clip();
    context.drawImage(phone, phoneX, phoneY, phoneWidth, phoneHeight);
    context.restore();

    return Array.from(context.getImageData(0, 0, 1024, 500).data);
  });
  await page.close();

  const out = path.join(outputDir, 'feature-graphic.png');
  writeFileSync(out, encodePng(featureWidth, featureHeight, rgbaToRgb(Buffer.from(rgba)), 2));
  console.log(`FEATURE ${out}`);
}

function assertShots(label, files, accept) {
  for (const file of files) {
    const filePath = path.join(outputDir, file);
    if (!existsSync(filePath)) {
      console.warn(`MISSING ${file}`);
      continue;
    }

    const size = pngSize(filePath);
    if (!accept(size)) {
      throw new Error(`${file} is ${size.width}x${size.height} colorType=${size.colorType}`);
    }

    console.log(`${label} ${file} ${size.width}x${size.height} colorType=${size.colorType}`);
  }
}

function assertPlayShots() {
  assertShots('PHONE', [
    'phone-home.png',
    'phone-explore.png',
    'phone-movies.png',
    'phone-movie.png',
    'phone-music.png',
    'phone-series.png',
  ], (size) => size.width >= 320 && size.height <= 3840 && size.height / size.width <= 2);

  assertShots('TV', [
    'tv-home.png',
    'tv-explore.png',
    'tv-movies.png',
    'tv-movie.png',
    'tv-music.png',
    'tv-series.png',
  ], (size) => size.width === 1920 && size.height === 1080 && size.colorType === 2);
}

async function main() {
  mkdirSync(outputDir, { recursive: true });
  if (!existsSync(iconSource)) {
    throw new Error(`Icon not found: ${iconSource}`);
  }

  await runCapture();
  assertPlayShots();

  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({
      viewport: { width: iconSize, height: iconSize },
      deviceScaleFactor: 1,
    });
    await writeIcon(page);
    await page.close();
    await writeFeatureGraphic(browser);
  } finally {
    await browser.close();
  }

  const icon = pngSize(path.join(outputDir, 'icon.png'));
  const feature = pngSize(path.join(outputDir, 'feature-graphic.png'));
  if (icon.width !== 512 || icon.height !== 512 || icon.colorType !== 6) {
    throw new Error(`icon.png is ${icon.width}x${icon.height} colorType=${icon.colorType}`);
  }

  if (feature.width !== 1024 || feature.height !== 500 || feature.colorType !== 2) {
    throw new Error(`feature-graphic.png is ${feature.width}x${feature.height} colorType=${feature.colorType}`);
  }

  console.log('PLAY DONE');
  console.log(`  ${outputDir}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
