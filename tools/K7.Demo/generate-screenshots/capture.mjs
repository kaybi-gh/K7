#!/usr/bin/env node
import { chromium, devices } from '@playwright/test';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const configPath = process.env.K7_SCREENSHOTS_CONFIG
  ?? path.join(__dirname, 'screenshots.config.json');

if (!existsSync(configPath)) {
  console.error(`Config not found: ${configPath}`);
  process.exit(1);
}

const config = JSON.parse(readFileSync(configPath, 'utf8'));
const baseUrl = (process.env.K7_DEMO_URL ?? config.baseUrl).replace(/\/$/, '');
const outputDir = path.resolve(__dirname, config.outputDir ?? '../../../screenshots');
const colorScheme = config.colorScheme ?? 'dark';
const settleMs = Number(config.settleMs ?? 2500);
const defaultProfile = config.defaultProfile ?? 'desktop';

mkdirSync(outputDir, { recursive: true });

const guestButton = (targetPage) =>
  targetPage.getByRole('button', { name: /continue as guest|continuer en tant qu'invité/i });

function buildProfiles() {
  const rootViewport = config.viewport ?? { width: 1440, height: 900 };
  const configured = config.profiles ?? {
    desktop: { viewport: rootViewport },
  };

  const profiles = new Map();

  for (const [name, profile] of Object.entries(configured)) {
    const contextOptions = {
      colorScheme,
      locale: profile.locale ?? 'en-US',
    };

    if (profile.device && devices[profile.device]) {
      Object.assign(contextOptions, devices[profile.device]);
    } else if (profile.viewport) {
      contextOptions.viewport = profile.viewport;
      if (profile.deviceScaleFactor) {
        contextOptions.deviceScaleFactor = profile.deviceScaleFactor;
      }
      if (profile.isMobile) {
        contextOptions.isMobile = true;
        contextOptions.hasTouch = true;
      }
    }

    if (profile.userAgent) {
      contextOptions.userAgent = profile.userAgent;
    }

    profiles.set(name, {
      name,
      platform: profile.platform ?? null,
      contextOptions,
    });
  }

  if (!profiles.has(defaultProfile)) {
    profiles.set(defaultProfile, {
      name: defaultProfile,
      platform: null,
      contextOptions: { viewport: rootViewport, colorScheme, locale: 'en-US' },
    });
  }

  return profiles;
}

function platformInitScript(platform) {
  if (!platform) {
    return null;
  }

  const platformType = platform === 'tv' ? 'tv' : platform;

  return `(() => {
    const platformType = ${JSON.stringify(platformType)};
    const install = () => {
      if (platformType === 'tv') {
        document.documentElement.classList.add('platform-tv');
      }

      const original = window.getParsedUserAgent;
      if (typeof original !== 'function' || original.__k7Screenshot) {
        return false;
      }

      const wrapped = function () {
        return { ...original(), PlatformType: platformType };
      };
      wrapped.__k7Screenshot = true;
      window.getParsedUserAgent = wrapped;
      return true;
    };

    document.addEventListener('DOMContentLoaded', install);
    window.addEventListener('load', install);

    let attempts = 0;
    const timer = setInterval(() => {
      install();
      if (window.getParsedUserAgent?.__k7Screenshot || ++attempts > 400) {
        clearInterval(timer);
      }
    }, 50);
  })();`;
}

async function waitForAppShell(targetPage) {
  await targetPage.locator("a[href='/explore']").first().waitFor({
    state: 'visible',
    timeout: 120_000,
  });
  await targetPage.waitForTimeout(settleMs);
}

async function loginAsGuestIfNeeded(targetPage) {
  await targetPage.goto(`${baseUrl}/welcome`, { waitUntil: 'networkidle', timeout: 120_000 });

  const guest = guestButton(targetPage);
  if (await guest.isVisible({ timeout: 8_000 }).catch(() => false)) {
    await guest.click();
    await waitForAppShell(targetPage);
    return;
  }

  if (targetPage.url().includes('/welcome')) {
    throw new Error('Guest button not found on /welcome. Is guest mode enabled?');
  }

  await waitForAppShell(targetPage);
}

async function captureEntry(targetPage, entry) {
  if (/__[^_]+__/.test(entry.path ?? '')) {
    if (entry.optional) {
      console.log(`SKIP optional (placeholder): ${entry.file}`);
      return;
    }

    console.warn(`WARN placeholder path not replaced: ${entry.path}`);
    return;
  }

  const url = `${baseUrl}${entry.path.startsWith('/') ? entry.path : `/${entry.path}`}`;
  console.log(`CAPTURE ${entry.file} <- ${url}`);

  await targetPage.goto(url, { waitUntil: 'domcontentloaded' });

  if (entry.waitFor) {
    await targetPage.waitForSelector(entry.waitFor, { timeout: 60_000 }).catch(() => {
      console.warn(`  waitFor timeout: ${entry.waitFor}`);
    });
  } else {
    await waitForAppShell(targetPage);
  }

  await targetPage.waitForTimeout(settleMs);

  const out = path.join(outputDir, entry.file);
  await targetPage.screenshot({ path: out, fullPage: false });
  console.log(`  -> ${out}`);
}

async function discoverMediaLinks(targetPage) {
  if (!config.discover?.enabled) {
    return [];
  }

  const patterns = [
    { type: 'music-album', re: /\/music\/albums\/[0-9a-f-]{36}/i },
    { type: 'movie', re: /\/movies\/[0-9a-f-]{36}/i },
    { type: 'series', re: /\/series\/[0-9a-f-]{36}/i },
    { type: 'library-group', re: /\/library-groups\/[0-9a-f-]{36}/i },
  ];

  const pathsToScan = ['/explore', '/'];
  const found = new Map();

  for (const scanPath of pathsToScan) {
    await targetPage.goto(`${baseUrl}${scanPath}`, { waitUntil: 'domcontentloaded' });
    await waitForAppShell(targetPage);

    const hrefs = await targetPage.locator('a[href]').evaluateAll(anchors =>
      anchors
        .map(a => a.getAttribute('href'))
        .filter(Boolean),
    );

    for (const href of hrefs) {
      for (const { type, re } of patterns) {
        if (!re.test(href) || found.has(type)) {
          continue;
        }

        found.set(type, href.startsWith('http') ? new URL(href).pathname : href);
      }
    }
  }

  const max = config.discover.maxPerType ?? 1;
  const discoveries = [];

  if (found.has('library-group')) {
    const groupPath = found.get('library-group');
    await targetPage.goto(`${baseUrl}${groupPath}`, { waitUntil: 'domcontentloaded' });
    await waitForAppShell(targetPage);

    const inner = await targetPage.locator('a[href]').evaluateAll(anchors =>
      anchors.map(a => a.getAttribute('href')).filter(Boolean),
    );

    for (const href of inner) {
      for (const { type, re } of patterns.filter(p => p.type !== 'library-group')) {
        if (discoveries.filter(d => d.type === type).length >= max) {
          continue;
        }

        if (re.test(href)) {
          discoveries.push({
            type,
            path: href.startsWith('http') ? new URL(href).pathname : href,
          });
        }
      }
    }
  }

  for (const [type, p] of found) {
    if (type === 'library-group') {
      continue;
    }

    if (discoveries.filter(d => d.type === type).length < max) {
      discoveries.push({ type, path: p });
    }
  }

  return discoveries;
}

function discoveryToFile(type) {
  switch (type) {
    case 'music-album':
      return 'music-album-desktop.png';
    case 'movie':
      return 'movie-detail-desktop.png';
    case 'series':
      return 'series-desktop.png';
    case 'library-group':
      return 'library-group-desktop.png';
    default:
      return `${type}-desktop.png`;
  }
}

function groupCapturesByProfile(captures) {
  const groups = new Map();

  for (const entry of captures) {
    const profileName = entry.profile ?? defaultProfile;
    if (!groups.has(profileName)) {
      groups.set(profileName, []);
    }

    groups.get(profileName).push(entry);
  }

  return groups;
}

async function runProfile(browser, profile, captures, { discover = false } = {}) {
  console.log(`PROFILE ${profile.name}`);

  const fileFilter = process.env.K7_SCREENSHOTS_FILES
    ?.split(',')
    .map(value => value.trim())
    .filter(Boolean);

  const selectedCaptures = fileFilter
    ? captures.filter(entry => fileFilter.includes(entry.file))
    : captures;

  if (selectedCaptures.length === 0) {
    console.log(`SKIP ${profile.name}: no captures matched K7_SCREENSHOTS_FILES`);
    return;
  }

  const context = await browser.newContext(profile.contextOptions);
  const initScript = platformInitScript(profile.platform);
  if (initScript) {
    await context.addInitScript({ content: initScript });
  }

  const page = await context.newPage();

  try {
    if (config.guestLogin !== false) {
      await loginAsGuestIfNeeded(page);
    }

    for (const entry of selectedCaptures) {
      await captureEntry(page, entry);
    }

    if (discover) {
      const discoveries = await discoverMediaLinks(page);
      for (const item of discoveries) {
        const file = discoveryToFile(item.type);
        const alreadyCaptured = (config.captures ?? []).some(c => c.file === file);
        if (alreadyCaptured) {
          console.log(`SKIP discover ${item.type}: ${file} already in config`);
          continue;
        }

        await captureEntry(page, {
          file,
          path: item.path,
          waitFor: 'h1, .media-card, table',
          optional: true,
        });
      }
    }
  } finally {
    await context.close();
  }
}

async function main() {
  const profiles = buildProfiles();
  const captures = config.captures ?? [];
  const groups = groupCapturesByProfile(captures);
  const profileFilter = process.env.K7_SCREENSHOTS_PROFILES
    ?.split(',')
    .map(value => value.trim())
    .filter(Boolean);

  const selectedGroups = profileFilter
    ? [...groups.entries()].filter(([profileName]) => profileFilter.includes(profileName))
    : [...groups.entries()];

  if (selectedGroups.length === 0) {
    throw new Error(`No captures matched K7_SCREENSHOTS_PROFILES=${process.env.K7_SCREENSHOTS_PROFILES}`);
  }

  console.log('K7 screenshot capture');
  console.log(`  baseUrl:   ${baseUrl}`);
  console.log(`  outputDir: ${outputDir}`);
  console.log(`  profiles:  ${selectedGroups.map(([name]) => name).join(', ')}`);

  const browser = await chromium.launch({ headless: true });

  try {
    for (const [profileName, profileCaptures] of selectedGroups) {
      const profile = profiles.get(profileName);
      if (!profile) {
        throw new Error(`Unknown profile "${profileName}" referenced in captures`);
      }

      const discover = profileName === defaultProfile && config.discover?.enabled;
      await runProfile(browser, profile, profileCaptures, { discover });
    }

    console.log('DONE');
  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
