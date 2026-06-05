// Open external links in a new tab.
//
// Covers both navigation entries (tabs/sidebar) and links inside page
// content. Content links to non-doc repo files (scripts, schemas, workflows,
// example manifests, ...) are rewritten by docs_hooks/prebuild.py into
// absolute github.com source URLs; those — like any other external link —
// should leave the docs site in a new tab rather than navigating away.
//
// Material doesn't apply target="_blank" by default. We catch links after
// page load by checking for an href that starts with http(s):// — internal
// docs links use relative paths so they aren't affected.
//
// Also covers Material's navigation.instant feature which re-runs link
// behaviour on every soft navigation; subscribing to document$ (a
// Material-exposed observable) re-applies after each instant page swap.

function markExternalLinks() {
  document
    .querySelectorAll('a.md-tabs__link, a.md-nav__link, .md-content a[href]')
    .forEach((a) => {
      const href = a.getAttribute('href') || '';
      if (href.startsWith('http://') || href.startsWith('https://')) {
        a.target = '_blank';
        a.rel = 'noopener noreferrer';
      }
    });
}

// First load.
document.addEventListener('DOMContentLoaded', markExternalLinks);

// Re-run after every Material instant-load navigation.
if (typeof document$ !== 'undefined') {
  document$.subscribe(markExternalLinks);
}

// Inject a Discord icon button into the header, immediately before the
// GitHub source button. Material exposes header buttons via the
// .md-header__button class; the styling matches the search/GitHub icons.
// Idempotent — skips if the button already exists.
function injectDiscordIcon() {
  if (document.querySelector('.md-header__discord')) return;
  const source = document.querySelector('.md-header__source');
  if (!source) return;

  const link = document.createElement('a');
  link.href = 'https://discord.pagonia.land';
  link.target = '_blank';
  link.rel = 'noopener noreferrer';
  link.className = 'md-header__button md-icon md-header__discord';
  link.title = 'Pagonia Land Discord';
  link.setAttribute('aria-label', 'Pagonia Land Discord');
  link.innerHTML =
    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" aria-hidden="true">' +
    '<path d="M19.27 5.33C17.94 4.71 16.5 4.26 15 4a.09.09 0 0 0-.07.03c-.18.33-.39.76-.53 1.09a16.09 16.09 0 0 0-4.8 0c-.14-.34-.35-.76-.54-1.09c-.01-.02-.04-.03-.07-.03c-1.5.26-2.93.71-4.27 1.33c-.01 0-.02.01-.03.02c-2.72 4.07-3.47 8.03-3.1 11.95c0 .02.01.04.03.05c1.8 1.32 3.53 2.12 5.24 2.65c.03.01.06 0 .07-.02c.4-.55.76-1.13 1.07-1.74c.02-.04 0-.08-.04-.09c-.57-.22-1.11-.48-1.64-.78c-.04-.02-.04-.08-.01-.11c.11-.08.22-.17.33-.25c.02-.02.05-.02.07-.01c3.44 1.57 7.15 1.57 10.55 0c.02-.01.05 0 .07.01c.11.09.22.17.33.26c.04.03.04.09-.01.11c-.52.31-1.07.56-1.64.78c-.04.01-.05.06-.04.09c.32.61.68 1.19 1.07 1.74c.03.01.06.02.09.01c1.72-.53 3.45-1.33 5.25-2.65c.02-.01.03-.03.03-.05c.44-4.53-.73-8.46-3.1-11.95c-.01-.01-.02-.02-.04-.02zM8.52 14.91c-1.03 0-1.89-.95-1.89-2.12s.84-2.12 1.89-2.12c1.06 0 1.9.96 1.89 2.12c0 1.17-.84 2.12-1.89 2.12zm6.97 0c-1.03 0-1.89-.95-1.89-2.12s.84-2.12 1.89-2.12c1.06 0 1.9.96 1.89 2.12c0 1.17-.83 2.12-1.89 2.12z"/>' +
    '</svg>';

  source.parentNode.insertBefore(link, source);
}

document.addEventListener('DOMContentLoaded', injectDiscordIcon);
if (typeof document$ !== 'undefined') {
  document$.subscribe(injectDiscordIcon);
}
