#!/usr/bin/env node
import { chromium, devices } from '@playwright/test';
import { existsSync, mkdirSync, readFileSync, renameSync, unlinkSync } from 'node:fs';
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

async function assertGuestEnabled() {
  if (config.guestLogin === false) {
    return;
  }

  const response = await fetch(`${baseUrl}/api/server-info`);
  if (!response.ok) {
    throw new Error(`Failed to read ${baseUrl}/api/server-info (${response.status})`);
  }

  const info = await response.json();
  if (info?.guestEnabled !== true) {
    throw new Error(
      `Guest login is disabled on ${baseUrl} (guestEnabled=${info?.guestEnabled}). ` +
      'Activate the Guest user in Admin -> Users, then retry.',
    );
  }
}

async function loginAsGuestIfNeeded(targetPage) {
  await targetPage.goto(`${baseUrl}/welcome`, { waitUntil: 'networkidle', timeout: 120_000 });

  const guest = guestButton(targetPage);
  if (await guest.isVisible({ timeout: 8_000 }).catch(() => false)) {
    await guest.click();
    await waitForAppShell(targetPage);
    return;
  }

  if (targetPage.url().includes('/welcome') || targetPage.url().includes('/sign-in')) {
    throw new Error('Guest button not found on /welcome. Is guest mode enabled?');
  }

  await waitForAppShell(targetPage);
}

function mediaTypeOf(item) {
  return item?.$type ?? item?.type ?? null;
}

function pathForSearchHit(kind, item) {
  const type = mediaTypeOf(item);
  const id = item?.id ?? item?.Id;
  if (!id) {
    return null;
  }

  switch (kind) {
    case 'movie':
      return type === 'Movie' ? `/movies/${id}` : null;
    case 'series':
      if (type === 'Serie') {
        return `/series/${id}`;
      }
      if (type === 'SerieSeason' && (item.serieId || item.SerieId)) {
        return `/series/${item.serieId ?? item.SerieId}`;
      }
      return null;
    case 'album':
      if (type === 'MusicAlbum') {
        return `/music/albums/${id}`;
      }
      if (type === 'MusicTrack' && (item.albumId || item.AlbumId)) {
        return `/music/albums/${item.albumId ?? item.AlbumId}`;
      }
      return null;
    default:
      return null;
  }
}

function mediaTypesForKind(kind) {
  switch (kind) {
    case 'movie':
      return ['Movie'];
    case 'series':
      return ['Serie'];
    case 'album':
      return ['MusicAlbum'];
    default:
      return [];
  }
}

async function resolveViaSearch(page, { search, kind }) {
  // Guest cannot call /api/search (UserOrAbove). Browse /api/medias with SearchText instead.
  const params = new URLSearchParams({
    PageNumber: '1',
    PageSize: '25',
    SearchText: search,
  });
  for (const mediaType of mediaTypesForKind(kind)) {
    params.append('MediaTypes', mediaType);
  }

  const url = `${baseUrl}/api/medias?${params}`;
  const response = await page.request.get(url);
  if (!response.ok()) {
    throw new Error(`Medias API failed for "${search}" (${response.status()})`);
  }

  const payload = await response.json();
  const results = payload.items ?? payload.Items ?? [];
  const needle = search.trim().toLowerCase();
  const ranked = [...results].sort((a, b) => {
    const ta = (a.title ?? a.Title ?? '').toLowerCase();
    const tb = (b.title ?? b.Title ?? '').toLowerCase();
    const score = (t) => (t === needle ? 0 : t.includes(needle) ? 1 : 2);
    return score(ta) - score(tb);
  });

  for (const item of ranked) {
    const resolved = pathForSearchHit(kind, item);
    if (resolved) {
      const title = item.title ?? item.Title ?? '';
      console.log(`  resolve search "${search}" (${kind}) -> ${resolved} (${title})`);
      return resolved;
    }
  }

  throw new Error(`No ${kind} result for search "${search}" (${results.length} media hits)`);
}

async function resolveViaCategory(page, { category }) {
  await page.goto(`${baseUrl}/explore`, { waitUntil: 'domcontentloaded', timeout: 120_000 });
  await waitForAppShell(page);
  await page.getByRole('button', { name: new RegExp(category, 'i') }).first().click();
  await page.waitForTimeout(settleMs);
  const resolved = page.url().replace(baseUrl, '');
  console.log(`  resolve category "${category}" -> ${resolved}`);
  return resolved.startsWith('/') ? resolved : `/${resolved}`;
}

async function resolveEntryPath(page, entry, cache) {
  if (entry.path && !entry.resolve) {
    return entry.path;
  }

  if (!entry.resolve) {
    throw new Error(`Capture ${entry.file} has neither path nor resolve`);
  }

  const key = JSON.stringify(entry.resolve);
  if (cache.has(key)) {
    return cache.get(key);
  }

  let resolved;
  if (entry.resolve.category) {
    resolved = await resolveViaCategory(page, entry.resolve);
  } else if (entry.resolve.search && entry.resolve.kind) {
    resolved = await resolveViaSearch(page, entry.resolve);
  } else {
    throw new Error(`Invalid resolve for ${entry.file}: ${key}`);
  }

  cache.set(key, resolved);
  return resolved;
}

async function waitForEntry(targetPage, entry) {
  if (!entry.waitFor) {
    await waitForAppShell(targetPage);
    return;
  }

  try {
    if (entry.waitFor.startsWith('text=')) {
      await targetPage.getByText(entry.waitFor.slice('text='.length), { exact: false })
        .first()
        .waitFor({ state: 'visible', timeout: 60_000 });
      return;
    }

    // Support comma-separated CSS selectors (Playwright :has-text etc.)
    const selectors = entry.waitFor.split(',').map(s => s.trim()).filter(Boolean);
    await targetPage.locator(selectors.join(', ')).first()
      .waitFor({ state: 'visible', timeout: 60_000 });
  } catch {
    console.warn(`  waitFor timeout: ${entry.waitFor}`);
  }
}

async function captureEntry(targetPage, entry, resolveCache) {
  const resolvedPath = await resolveEntryPath(targetPage, entry, resolveCache);

  if (/__[^_]+__/.test(resolvedPath ?? '')) {
    if (entry.optional) {
      console.log(`SKIP optional (placeholder): ${entry.file}`);
      return;
    }

    console.warn(`WARN placeholder path not replaced: ${resolvedPath}`);
    return;
  }

  const url = `${baseUrl}${resolvedPath.startsWith('/') ? resolvedPath : `/${resolvedPath}`}`;
  console.log(`CAPTURE ${entry.file} <- ${url}`);

  await targetPage.goto(url, { waitUntil: 'domcontentloaded' });
  await waitForEntry(targetPage, entry);
  await targetPage.waitForTimeout(settleMs);

  const out = path.join(outputDir, entry.file);
  const tempOut = `${out}.${process.pid}.tmp.png`;
  await targetPage.screenshot({ path: tempOut, fullPage: false });
  try {
    if (existsSync(out)) {
      unlinkSync(out);
    }
    renameSync(tempOut, out);
  } catch (err) {
    await targetPage.screenshot({ path: out, fullPage: false }).catch(() => {
      throw err;
    });
    if (existsSync(tempOut)) {
      try {
        unlinkSync(tempOut);
      } catch {
        // ignore cleanup errors
      }
    }
  }
  console.log(`  -> ${out}`);
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

async function runProfile(browser, profile, captures) {
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
  const resolveCache = new Map();

  try {
    if (config.guestLogin !== false) {
      await loginAsGuestIfNeeded(page);
    }

    for (const entry of selectedCaptures) {
      try {
        await captureEntry(page, entry, resolveCache);
      } catch (err) {
        if (entry.optional) {
          console.warn(`SKIP optional ${entry.file}: ${err.message}`);
          continue;
        }
        throw err;
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

  await assertGuestEnabled();

  const browser = await chromium.launch({ headless: true });

  try {
    for (const [profileName, profileCaptures] of selectedGroups) {
      const profile = profiles.get(profileName);
      if (!profile) {
        throw new Error(`Unknown profile "${profileName}" referenced in captures`);
      }

      await runProfile(browser, profile, profileCaptures);
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
