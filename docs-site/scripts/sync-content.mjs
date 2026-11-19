import { mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { sections } from '../navigation.mjs';

const site = fileURLToPath(new URL('../', import.meta.url));
const root = resolve(site, '..');
const output = resolve(site, 'src/content/docs');
const repository = 'https://github.com/harrywvu/Osteon/blob/HEAD/';
const pages = sections.flatMap(({ pages }) => pages);
const routes = new Map(pages.map(([slug, , source]) => [source, '/' + slug + '/']));
routes.set('README.md', '/');
routes.set('Docs/README.md', '/');

function escapeHtml(text) {
  return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

async function rewriteLinks(markdown, source) {
  // Leave code samples intact. Render Mermaid source with a readable fallback.
  const parts = markdown.split(/(^ {0,3}```[^\n]*\n[\s\S]*?^ {0,3}```[ \t]*$)/gm);
  for (let index = 0; index < parts.length; index++) {
    if (index % 2) {
      parts[index] = parts[index].replace(
        /^ {0,3}```mermaid[^\S\n]*\n([\s\S]*?)\n {0,3}```[ \t]*$/,
        (_, diagram) => '<pre class="diagram" data-mermaid><code>' + escapeHtml(diagram) + '</code></pre>',
      );
      continue;
    }

    // The repository's documentation uses inline Markdown links.
    const matches = [...parts[index].matchAll(/\]\(([^()\s]+)\)/g)];
    for (const [original, href] of matches) {
      if (/^(?:[a-z][a-z\d+.-]*:|[#/])/i.test(href)) continue;
      const [path, fragment] = href.split('#', 2);
      const target = new URL(path, new URL(source, 'https://repo.invalid/'));
      const relative = decodeURIComponent(target.pathname.slice(1));
      let destination = routes.get(relative);
      if (!destination) {
        const info = await stat(resolve(root, relative)).catch(() => null);
        if (!info) throw new Error(source + ' links to a missing file: ' + href);
        destination = repository.replace('/blob/', info.isDirectory() ? '/tree/' : '/blob/') +
          relative.split('/').map(encodeURIComponent).join('/');
      }
      parts[index] = parts[index].replaceAll(original, '](' + destination +
        (fragment ? '#' + fragment : '') + ')');
    }
  }
  return parts.join('');
}

async function writePage(path, content) {
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, content);
}

for (const [slug, , source] of pages) {
  const markdown = (await readFile(resolve(root, source), 'utf8')).replace(/\r\n/g, '\n');
  const heading = markdown.match(/^\uFEFF?# (.+)\n/);
  if (!heading) throw new Error('Expected a level-one title in ' + source);
  const body = await rewriteLinks(markdown.slice(heading[0].length).trimStart(), source);
  await writePage(resolve(output, slug + '.md'), '---\ntitle: ' +
    JSON.stringify(heading[1]) + '\n---\n\n' + body);
}

await writePage(resolve(output, 'index.md'), await readFile(resolve(site, 'src/home.md')));
console.log('Prepared ' + (pages.length + 1) + ' documentation pages.');
