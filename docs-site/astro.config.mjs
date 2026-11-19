import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { sections } from './navigation.mjs';

export default defineConfig({
  output: 'static',
  integrations: [
    starlight({
      title: 'Osteon Docs',
      description: 'Documentation for the Osteon anatomy explorer.',
      favicon: '/favicon.svg',
      customCss: ['./src/styles/docs.css'],
      components: { Footer: './src/components/Footer.astro' },
      sidebar: [
        { label: 'Overview', link: '/' },
        ...sections.map(({ label, pages }) => ({
          label,
          items: pages.map(([slug, label]) => ({ slug, label })),
        })),
      ],
    }),
  ],
});
