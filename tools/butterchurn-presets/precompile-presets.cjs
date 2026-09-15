#!/usr/bin/env node
/**
 * precompile-presets.cjs — Butterchurn preset equation precompiler.
 *
 * WHY THIS EXISTS
 * ---------------
 * Butterchurn compiles preset equations with the `Function` constructor:
 *
 *     preset.init_eqs = new Function("a", preset.init_eqs_str + " return a;");
 *
 * The DotNetCloud CSP is strict (see
 * `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/CspPolicy.cs`): `script-src` allows
 * `'wasm-unsafe-eval'` but deliberately NOT `'unsafe-eval'`. Under that policy every
 * `visualizer.loadPreset(...)` throws:
 *
 *     EvalError: Evaluating a string as JavaScript violates the following Content Security
 *     Policy directive because 'unsafe-eval' is not an allowed source of script ...
 *
 * `WebGLVisualizer.loadPreset()` only runs that compilation block when
 * `typeof preset.init_eqs !== "function"`. So if the equations are turned into *real*
 * functions ahead of time (same body text: `<eqs_str> return a;`), butterchurn never calls
 * `new Function` and the strict CSP can stay in place.
 *
 * WHAT IT DOES
 * ------------
 * Loads the vendor preset bundles plus the DotNetCloud custom presets (exposed by
 * `butterchurn-visualizer.js` as `window.dotnetcloudPresets`) in a sandboxed VM, then writes
 * `butterchurn-presets-compiled.js` containing one real function per equation, mirroring
 * butterchurn's compilation decisions exactly:
 *
 *   - `init_eqs` / `frame_eqs` (always) and `pixel_eqs` (function, or `""` when the preset has
 *     no pixel equation string — exactly what butterchurn assigns).
 *   - shape/wave `init_eqs` / `frame_eqs` and wave `point_eqs` ONLY for entries whose merged
 *     `baseVals.enabled !== 0` — the same condition butterchurn uses. Disabled shapes/waves are
 *     left untouched so their equations are never executed (matching upstream behaviour).
 *
 * The output file is intentionally NOT strict-mode: `new Function` bodies are sloppy-mode, and
 * the generated functions must inherit identical semantics (e.g. implicit globals).
 *
 * USAGE
 * -----
 *   node tools/butterchurn-presets/precompile-presets.cjs            # regenerate + validate
 *   node tools/butterchurn-presets/precompile-presets.cjs --check    # fail if out of date
 *   node tools/butterchurn-presets/precompile-presets.cjs --runtime  # also diff runtime results
 *
 * Run this after ANY change to a preset source (vendor preset bundle or a DotNetCloud custom
 * preset in `butterchurn-visualizer.js`). Node is required (Node 16+); it is not needed at
 * runtime or by the .NET build.
 */

'use strict';

const fs = require('fs');
const path = require('path');
const vm = require('vm');
const crypto = require('crypto');

const REPO_ROOT = path.resolve(__dirname, '..', '..');
const LIB_DIR = path.join(REPO_ROOT, 'src', 'UI', 'DotNetCloud.UI.Web', 'wwwroot', 'lib', 'butterchurn');
const JS_DIR = path.join(REPO_ROOT, 'src', 'UI', 'DotNetCloud.UI.Web', 'wwwroot', 'js');
const OUT_FILE = path.join(LIB_DIR, 'butterchurn-presets-compiled.js');

/** Preset sources, in load order (mirrors App.razor). */
const SOURCES = [
    { label: 'butterchurn-presets.min.js', file: path.join(LIB_DIR, 'butterchurn-presets.min.js'), global: 'butterchurnPresets' },
    { label: 'butterchurnPresetsExtra.min.js', file: path.join(LIB_DIR, 'butterchurnPresetsExtra.min.js'), global: 'butterchurnPresetsExtra' },
    { label: 'butterchurnPresetsExtra2.min.js', file: path.join(LIB_DIR, 'butterchurnPresetsExtra2.min.js'), global: 'butterchurnPresetsExtra2' },
    {
        label: 'butterchurn-visualizer.js (window.dotnetcloudPresets)',
        file: path.join(JS_DIR, 'butterchurn-visualizer.js'),
        global: 'dotnetcloudPresets',
        // Plain name → preset map (not a UMD library), so no resolvePresetLib() heuristics.
        plainMap: true,
    },
];

const args = new Set(process.argv.slice(2));
const CHECK_ONLY = args.has('--check');
const RUN_RUNTIME_DIFF = args.has('--runtime');
if (args.has('--help') || args.has('-h')) {
    console.log('Usage: node tools/butterchurn-presets/precompile-presets.cjs [--check] [--runtime]');
    process.exit(0);
}

// ────────────────────────────────────────────────────────────────────────────
// 1. Load every preset source in a browser-ish sandbox
// ────────────────────────────────────────────────────────────────────────────

function createSandbox() {
    const documentStub = {
        addEventListener() { },
        removeEventListener() { },
        getElementById() { return null; },
        createElement() { return { getContext() { return null; }, style: {} }; },
        documentElement: { style: {} },
        fullscreenElement: null,
        body: { classList: { add() { }, remove() { } } },
    };
    const sandbox = {
        console: { log() { }, warn() { }, error() { }, info() { }, debug() { } },
        document: documentStub,
        navigator: { userAgent: 'node' },
        location: { href: 'https://localhost/' },
        setTimeout, clearTimeout, setInterval, clearInterval,
        Math, JSON, Date, Object, Array, String, Number, Boolean, RegExp, Error, TypeError, Symbol,
        parseInt, parseFloat, isNaN, isFinite, Float32Array, Uint8Array, ArrayBuffer, Promise, Map, Set,
        performance: { now: () => Date.now() },
        requestAnimationFrame() { return 0; },
        cancelAnimationFrame() { },
        ResizeObserver: function ResizeObserver() { this.observe = () => { }; this.disconnect = () => { }; },
    };
    sandbox.window = sandbox;
    sandbox.self = sandbox;
    sandbox.globalThis = sandbox;
    vm.createContext(sandbox);
    return sandbox;
}

/** Mirrors `resolvePresetLib()` in butterchurn-visualizer.js. */
function resolvePresetLib(mod) {
    if (!mod) return null;
    let m = mod.default || mod;
    if (m && m.default) m = m.default;
    if (typeof m.getPresets === 'function') return m.getPresets();
    const keys = Object.keys(m).filter((k) => k !== 'default' && k !== '__esModule');
    if (keys.length > 5) return m;
    return null;
}

/** Resolves presets for a source: UMD library (heuristics) or plain preset map. */
function resolveSource(sandbox, source) {
    const value = sandbox[source.global];
    if (!value) return null;
    return source.plainMap ? value : resolvePresetLib(value);
}

function loadPresetSources() {
    const sandbox = createSandbox();
    const results = [];

    for (const source of SOURCES) {
        if (!fs.existsSync(source.file)) {
            throw new Error(`Preset source not found: ${source.file}`);
        }
        const code = fs.readFileSync(source.file, 'utf8');
        try {
            vm.runInContext(code, sandbox, { filename: source.file });
        } catch (err) {
            throw new Error(`Failed to evaluate ${source.label}: ${err.message}`);
        }
        const presets = resolveSource(sandbox, source);
        if (!presets || typeof presets !== 'object') {
            throw new Error(`No presets resolved from ${source.label} (global: window.${source.global})`);
        }
        results.push({ ...source, presets, names: Object.keys(presets).sort() });
    }

    return results;
}

// ────────────────────────────────────────────────────────────────────────────
// 2. Emit the compiled presets file
// ────────────────────────────────────────────────────────────────────────────

/** Butterchurn merges these defaults before testing `enabled`; default is 0 (disabled). */
function isEnabled(entry) {
    const baseVals = entry && entry.baseVals;
    return !!baseVals && 'enabled' in baseVals && baseVals.enabled !== 0;
}

/** `new Function("a", str + " return a;")` as a real, eval-free function. */
function emitEquation(target, property, source) {
    const body = `function (a) {${source} return a;}`;
    return `${target}.${property} = ${body};`;
}

function buildFile(sources) {
    const headerLines = [];
    const bodyLines = [];
    const stats = { presets: 0, equations: 0, shapes: 0, waves: 0, skippedDisabled: 0 };

    for (const source of sources) {
        const varName = `L_${source.global}`;
        // The custom preset map is a plain object; UMD libraries need resolving.
        const accessor = source.plainMap
            ? `window.${source.global}`
            : `resolvePresets(window.${source.global})`;
        bodyLines.push('');
        bodyLines.push(`    // ── ${source.label} — ${source.names.length} presets ──`);
        bodyLines.push(`    var ${varName} = ${accessor};`);
        bodyLines.push(`    if (!${varName}) console.warn("butterchurn-presets-compiled.js: window.${source.global} is missing — those presets are unavailable");`);
        bodyLines.push(`    if (${varName}) {`);
        for (const name of source.names) {
            const preset = source.presets[name];
            const parts = [];

            // Top-level equations — always present in butterchurn's compiled preset shape.
            parts.push(`p.init_eqs = function (a) {${preset.init_eqs_str} return a;};`);
            stats.equations++;
            parts.push(`p.frame_eqs = function (a) {${preset.frame_eqs_str} return a;};`);
            stats.equations++;
            if (preset.pixel_eqs_str && preset.pixel_eqs_str !== '') {
                parts.push(`p.pixel_eqs = function (a) {${preset.pixel_eqs_str} return a;};`);
                stats.equations++;
            } else {
                // butterchurn assigns "" for presets without pixel equations.
                parts.push('p.pixel_eqs = "";');
            }

            const shapes = Array.isArray(preset.shapes) ? preset.shapes : [];
            for (let i = 0; i < shapes.length; i++) {
                if (!isEnabled(shapes[i])) { stats.skippedDisabled++; continue; }
                parts.push(`p.shapes[${i}].init_eqs = function (a) {${shapes[i].init_eqs_str} return a;};`);
                parts.push(`p.shapes[${i}].frame_eqs = function (a) {${shapes[i].frame_eqs_str} return a;};`);
                stats.equations += 2;
                stats.shapes++;
            }

            const waves = Array.isArray(preset.waves) ? preset.waves : [];
            for (let i = 0; i < waves.length; i++) {
                if (!isEnabled(waves[i])) { stats.skippedDisabled++; continue; }
                parts.push(`p.waves[${i}].init_eqs = function (a) {${waves[i].init_eqs_str} return a;};`);
                parts.push(`p.waves[${i}].frame_eqs = function (a) {${waves[i].frame_eqs_str} return a;};`);
                stats.equations += 2;
                if (waves[i].point_eqs_str) {
                    parts.push(`p.waves[${i}].point_eqs = function (a) {${waves[i].point_eqs_str} return a;};`);
                    stats.equations++;
                } else {
                    parts.push(`p.waves[${i}].point_eqs = "";`);
                }
                stats.waves++;
            }

            bodyLines.push(`        if ((p = ${varName}[${JSON.stringify(name)}])) {${parts.join('')}}`);
            stats.presets++;
        }
        bodyLines.push('    }');
    }

    for (const source of sources) {
        const hash = crypto.createHash('sha256').update(fs.readFileSync(source.file)).digest('hex');
        headerLines.push(` *   ${source.label.padEnd(50)} sha256:${hash}`);
    }

    const header = `/*!
 * butterchurn-presets-compiled.js — GENERATED FILE, DO NOT EDIT BY HAND.
 *
 * Butterchurn's WebGLVisualizer.loadPreset() compiles preset equations with
 * \`new Function(...)\`, which the DotNetCloud CSP blocks (\`script-src 'self'
 * 'wasm-unsafe-eval'\` — no \`'unsafe-eval'\`; see DotNetCloud.Core.ServiceDefaults
 * Middleware/CspPolicy.cs). This file contains those equations already compiled into
 * real functions with byte-identical bodies (\`<eqs_str> return a;\`), so butterchurn's
 * \`typeof preset.init_eqs !== "function"\` guard is true and it never evaluates a string.
 *
 * Deliberately NOT strict-mode: \`new Function\` bodies are sloppy-mode and these
 * functions must keep identical semantics (e.g. implicit globals).
 *
 * Generated by : tools/butterchurn-presets/precompile-presets.cjs
 * Regenerate   : node tools/butterchurn-presets/precompile-presets.cjs
 * Check stale  : node tools/butterchurn-presets/precompile-presets.cjs --check
 *
 * Sources (regenerate whenever any of these change):
${headerLines.join('\n')}
 *
 * Stats: ${stats.presets} presets, ${stats.equations} precompiled equations
 *        (${stats.shapes} shapes, ${stats.waves} waves; ${stats.skippedDisabled} disabled
 *        shape/wave slots left uncompiled, matching butterchurn).
 *        Missing libraries/presets are skipped with a console warning (the page keeps working).
 */

/* eslint-disable */
(function () {
    function resolvePresets(mod) {
        if (!mod) return null;
        var m = mod.default || mod;
        if (m && m.default) m = m.default;
        if (typeof m.getPresets === "function") return m.getPresets();
        var keys = Object.keys(m).filter(function (k) { return k !== "default" && k !== "__esModule"; });
        if (keys.length > 5) return m;
        return null;
    }

    var p;
`;

    const footer = `
    // Sanity: every preset that participates in the UI must now carry compiled equations.
    // (butterchurn's loadPreset() would otherwise fall back to \`new Function\` → CSP error.)
    window.dotnetcloudPresetsCompiled = ${stats.presets};
})();
`;

    return header + bodyLines.join('\n') + '\n' + footer;
}

// ────────────────────────────────────────────────────────────────────────────
// 3. Validate the generated output
// ────────────────────────────────────────────────────────────────────────────

/** Sandbox with butterchurn (for its window.* EEL helpers) and every preset source loaded. */
function createRuntimeSandbox() {
    const sandbox = createSandbox();
    const butterchurn = path.join(LIB_DIR, 'butterchurn.min.js');
    vm.runInContext(fs.readFileSync(butterchurn, 'utf8'), sandbox, { filename: butterchurn });
    for (const source of SOURCES) {
        vm.runInContext(fs.readFileSync(source.file, 'utf8'), sandbox, { filename: source.file });
    }
    return sandbox;
}

/** Re-loads everything, applies the generated file, and asserts the compiled shape. */
function validateGenerated(generatedCode, sources) {
    const problems = [];
    const sandbox = createRuntimeSandbox();

    try {
        vm.runInContext(generatedCode, sandbox, { filename: OUT_FILE });
    } catch (err) {
        throw new Error(`Generated file failed to evaluate: ${err.message}`);
    }

    let checked = 0;
    for (const source of sources) {
        const presets = resolveSource(sandbox, source);
        if (!presets) { problems.push(`${source.label}: presets not resolvable after generation`); continue; }
        for (const name of source.names) {
            const preset = presets[name];
            checked++;
            if (typeof preset.init_eqs !== 'function') problems.push(`${source.label} / "${name}": init_eqs is not a function`);
            if (typeof preset.frame_eqs !== 'function') problems.push(`${source.label} / "${name}": frame_eqs is not a function`);
            if (typeof preset.pixel_eqs !== 'function' && preset.pixel_eqs !== '') problems.push(`${source.label} / "${name}": pixel_eqs must be a function or ""`);
            const shapes = Array.isArray(preset.shapes) ? preset.shapes : [];
            shapes.forEach((shape, i) => {
                const shouldCompile = isEnabled(shape);
                const compiled = typeof shape.init_eqs === 'function' && typeof shape.frame_eqs === 'function';
                if (shouldCompile && !compiled) problems.push(`${source.label} / "${name}": shape ${i} enabled but not compiled`);
                if (!shouldCompile && (shape.init_eqs !== undefined || shape.frame_eqs !== undefined)) {
                    problems.push(`${source.label} / "${name}": shape ${i} disabled but was compiled (behaviour differs from butterchurn)`);
                }
            });
            const waves = Array.isArray(preset.waves) ? preset.waves : [];
            waves.forEach((wave, i) => {
                const shouldCompile = isEnabled(wave);
                const compiled = typeof wave.init_eqs === 'function' && typeof wave.frame_eqs === 'function';
                if (shouldCompile && !compiled) problems.push(`${source.label} / "${name}": wave ${i} enabled but not compiled`);
                if (!shouldCompile && (wave.init_eqs !== undefined || wave.frame_eqs !== undefined)) {
                    problems.push(`${source.label} / "${name}": wave ${i} disabled but was compiled (behaviour differs from butterchurn)`);
                }
            });
        }
    }

    // Prove that nothing left in the generated file needs eval (comments excluded — the header
    // deliberately documents butterchurn's `new Function` behaviour).
    const codeOnly = generatedCode
        .replace(/^\/\*![\s\S]*?\*\//, '')
        .replace(/^[ \t]*\/\/.*$/gm, '');
    if (problems.length === 0 && /\bnew Function\s*\(|\beval\s*\(/.test(codeOnly)) {
        problems.push('Generated file contains new Function()/eval() — it must be eval-free');
    }

    return { problems, checked };
}

/** Executes precompiled vs. `new Function` equations and diffs the resulting params. */
function runtimeDiff(sources, generatedCode) {
    const sandbox = createRuntimeSandbox();
    vm.runInContext(generatedCode, sandbox, { filename: OUT_FILE });

    // Presets call rand()/rand_start which hit Math.random(). Seed it identically for both runs
    // (the sandbox shares this Math object), otherwise every preset looks different.
    const realRandom = Math.random;
    let randomState = 0;
    const seededRandom = () => {
        randomState = (randomState * 1664525 + 1013904223) >>> 0;
        return randomState / 4294967296;
    };
    const resetRandom = () => { randomState = 0x2f6e2b1; };

    const failures = [];
    let compared = 0;

    for (const source of sources) {
        const presets = resolveSource(sandbox, source);
        if (!presets) throw new Error(`Runtime diff: presets not resolvable for ${source.label}`);
        for (const name of source.names) {
            const preset = presets[name];
            for (const entry of [{ kind: 'top', obj: preset, label: name }].concat(
                (preset.shapes || []).map((s, i) => ({ kind: 'shape', obj: s, label: `${name}#shape${i}` })),
                (preset.waves || []).map((w, i) => ({ kind: 'wave', obj: w, label: `${name}#wave${i}` }))
            )) {
                const props = entry.kind === 'top' ? ['init_eqs', 'frame_eqs', 'pixel_eqs']
                    : entry.kind === 'shape' ? ['init_eqs', 'frame_eqs']
                        : ['init_eqs', 'frame_eqs', 'point_eqs'];
                for (const prop of props) {
                    const fn = entry.obj[prop];
                    if (typeof fn !== 'function') continue;
                    const str = entry.kind === 'top'
                        ? (prop === 'init_eqs' ? preset.init_eqs_str : prop === 'frame_eqs' ? preset.frame_eqs_str : preset.pixel_eqs_str)
                        : entry.obj[`${prop}_str`];
                    if (typeof str !== 'string') { failures.push(`${entry.label}.${prop}: no source string`); continue; }

                    // Reference implementation: exactly what butterchurn would have built.
                    let refFn;
                    try {
                        refFn = vm.runInContext(`(function (src) { return new Function("a", src + " return a;"); })(${JSON.stringify(str)})`, sandbox);
                    } catch (err) {
                        failures.push(`${entry.label}.${prop}: reference compile failed: ${err.message}`);
                        continue;
                    }

                    const a = seedParams(preset);
                    const b = seedParams(preset);
                    Math.random = seededRandom;
                    try {
                        resetRandom();
                        fn(a);
                        resetRandom();
                        refFn(b);
                    } catch (err) {
                        Math.random = realRandom;
                        failures.push(`${entry.label}.${prop}: ${err.name}: ${err.message}`);
                        continue;
                    } finally {
                        Math.random = realRandom;
                    }
                    const diff = diffObjects(a, b);
                    if (diff) failures.push(`${entry.label}.${prop}: output differs (${diff})`);
                    compared++;
                }
            }
        }
    }
    return { failures, compared };
}

function seedParams(preset) {
    const baseVals = (preset && preset.baseVals) || {};
    return Object.assign({}, baseVals, {
        frame: 100, time: 12.5, fps: 60,
        bass: 0.5, bass_att: 0.45, mid: 0.4, mid_att: 0.35, treb: 0.3, treb_att: 0.25,
        meshx: 32, meshy: 24, pixelsx: 640, pixelsy: 480, aspectx: 1.333, aspecty: 1,
        megabuf: [], gmegabuf: [], rand_start: [0.1, 0.2, 0.3, 0.4],
    });
}

function diffObjects(a, b) {
    const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
    for (const k of keys) {
        if (k === 'megabuf' || k === 'gmegabuf') continue;
        const x = a[k];
        const y = b[k];
        if (typeof x === 'function' || typeof y === 'function') continue;
        if (typeof x === 'number' && typeof y === 'number') {
            if (Number.isNaN(x) && Number.isNaN(y)) continue;
            if (Math.abs(x - y) > 1e-9) return `${k}: ${x} !== ${y}`;
            continue;
        }
        if (JSON.stringify(x) !== JSON.stringify(y)) return `${k}: ${JSON.stringify(x)} !== ${JSON.stringify(y)}`;
    }
    return null;
}

// ────────────────────────────────────────────────────────────────────────────
// 4. Main
// ────────────────────────────────────────────────────────────────────────────

function main() {
    const sources = loadPresetSources();
    const totalPresets = sources.reduce((n, s) => n + s.names.length, 0);
    console.log(`Loaded ${totalPresets} presets from ${sources.length} sources:`);
    sources.forEach((s) => console.log(`  - ${s.label}: ${s.names.length}`));

    const generated = buildFile(sources);
    const existing = fs.existsSync(OUT_FILE) ? fs.readFileSync(OUT_FILE, 'utf8') : null;

    if (CHECK_ONLY) {
        if (existing !== generated) {
            console.error('\n✗ butterchurn-presets-compiled.js is OUT OF DATE. Re-run:');
            console.error('    node tools/butterchurn-presets/precompile-presets.cjs');
            process.exitCode = 1;
            return;
        }
        console.log('\n✓ butterchurn-presets-compiled.js is up to date.');
    } else {
        fs.writeFileSync(OUT_FILE, generated, 'utf8');
        console.log(`\n✓ Wrote ${path.relative(REPO_ROOT, OUT_FILE)} (${(generated.length / 1024).toFixed(0)} KiB, ${existing === generated ? 'unchanged' : 'updated'})`);
    }

    const { problems, checked } = validateGenerated(existing === generated && CHECK_ONLY ? existing : generated, sources);
    if (problems.length) {
        console.error(`\n✗ Validation failed (${checked} presets checked):`);
        problems.slice(0, 25).forEach((p) => console.error(`   - ${p}`));
        if (problems.length > 25) console.error(`   ... and ${problems.length - 25} more`);
        process.exitCode = 1;
        return;
    }
    console.log(`✓ Validated ${checked} presets — every equation is a real function, no eval required.`);

    if (RUN_RUNTIME_DIFF) {
        const { failures, compared } = runtimeDiff(sources, generated);
        if (failures.length) {
            console.error(`\n✗ Runtime diff found ${failures.length} difference(s) vs new Function:`);
            failures.slice(0, 25).forEach((f) => console.error(`   - ${f}`));
            if (failures.length > 25) console.error(`   ... and ${failures.length - 25} more`);
            process.exitCode = 1;
            return;
        }
        console.log(`✓ Runtime diff: ${compared} equations produce identical results to new Function.`);
    }
}

main();
