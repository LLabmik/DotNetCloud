import { defineConfig, type UserConfig } from 'vite';
import { resolve } from 'path';
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'fs';

export default defineConfig(({ mode }) => {
  const browser = mode === 'firefox' ? 'firefox' : 'chrome';

  return {
    build: {
      outDir: `dist/${browser}`,
      emptyOutDir: true,
      rollupOptions: {
        input: {
          popup: resolve(__dirname, 'src/popup/popup.html'),
          background: resolve(__dirname, 'src/background/service-worker.ts'),
        },
        output: {
          entryFileNames: '[name]/[name].js',
          chunkFileNames: 'chunks/[name].js',
          assetFileNames: 'assets/[name].[ext]',
        },
      },
    },
    plugins: [
      // Copy the appropriate manifest file into the output directory
      {
        name: 'copy-manifest',
        closeBundle() {
          const manifestSrc = resolve(__dirname, `manifest.${browser}.json`);
          const outDir = resolve(__dirname, `dist/${browser}`);
          const manifestDest = resolve(outDir, 'manifest.json');

          if (!existsSync(outDir)) {
            mkdirSync(outDir, { recursive: true });
          }

          const manifest = JSON.parse(readFileSync(manifestSrc, 'utf-8'));

          // Inject version from package.json
          const pkg = JSON.parse(readFileSync(resolve(__dirname, 'package.json'), 'utf-8'));
          manifest.version = pkg.version;

          writeFileSync(manifestDest, JSON.stringify(manifest, null, 2));
          console.log(`Copied manifest.${browser}.json → ${manifestDest}`);
        },
      },
      // Copy icons into the output directory
      {
        name: 'copy-icons',
        closeBundle() {
          const srcDir = resolve(__dirname, 'icons');
          const outDir = resolve(__dirname, `dist/${browser}/icons`);

          if (existsSync(srcDir)) {
            if (!existsSync(outDir)) {
              mkdirSync(outDir, { recursive: true });
            }
            for (const size of ['16', '48', '128']) {
              const src = resolve(srcDir, `icon-${size}.png`);
              if (existsSync(src)) {
                copyFileSync(src, resolve(outDir, `icon-${size}.png`));
              }
            }
            console.log(`Copied icons → ${outDir}`);
          }
        },
      },
    ],
  } satisfies UserConfig;
});
