# Osteon documentation site

From the repository root, use Node.js 22.12 or later.

```sh
cd docs-site
npm install
npm run dev
```

Astro prints the local URL. To make and preview a static build, run:

```sh
npm run build
npm run preview
```

The build writes `dist/` for a host that serves the site at its domain root.
Keep maintainer documentation behind access control. The static output contains
the pages and their search index, so the host must protect both.

Edit the source Markdown in `../Docs/`. The sync step imports the pages
listed in `navigation.mjs`, rewrites cross-links, and keeps references to
other repository files as GitHub links. Local-only documentation and build
artifacts are outside the import list. Re-run `npm run sync` after document
edits while the dev server is running. The homepage lives in `src/home.md`.
