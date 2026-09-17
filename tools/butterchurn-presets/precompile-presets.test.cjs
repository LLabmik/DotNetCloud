#!/usr/bin/env node
/**
 * precompile-presets.test.cjs — regression tests for `precompile-presets.cjs`.
 *
 * WHY THIS FILE EXISTS
 * --------------------
 * Butterchurn compiles preset equations with `new Function("a", eqs_str + " return a;")`.
 * `--runtime` rebuilds each equation through that same compiler so the precompiled output can
 * be diffed against it. The equation text must reach the compiler as *data* (a plain argument),
 * because serialising it into evaluated code is `js/bad-code-sanitization` / CWE-94 — the
 * previous implementation interpolated `JSON.stringify(str)` into the `vm.runInContext` source,
 * which is CodeQL alert #489. Escaping a value does not make it safe to build code from it.
 *
 * These tests pin down (a) that the reference compiler still behaves exactly like butterchurn's
 * compilation — including for equation text full of quotes, newlines and `</script>` — and
 * (b) that the alert's pattern cannot come back unnoticed.
 *
 * The end-to-end equivalent is `node tools/butterchurn-presets/precompile-presets.cjs --runtime`,
 * which diffs every equation of every preset. This file needs no preset bundles, so it stays fast.
 *
 * USAGE
 * -----
 *   node --test tools/butterchurn-presets/
 */

'use strict';

const fs = require('fs');
const path = require('path');
const test = require('node:test');
const assert = require('node:assert/strict');

const {
    createSandbox,
    createReferenceCompiler,
} = require('./precompile-presets.cjs');

const SCRIPT_PATH = path.join(__dirname, 'precompile-presets.cjs');
const SCRIPT_SOURCE = fs.readFileSync(SCRIPT_PATH, 'utf8');

/** Source of a single top-level function from the precompiler, used for the structural guards. */
function functionSource(name) {
    const start = SCRIPT_SOURCE.indexOf(`function ${name}(`);
    assert.notEqual(start, -1, `${name}() not found in precompile-presets.cjs`);
    const next = SCRIPT_SOURCE.indexOf('\nfunction ', start);
    return SCRIPT_SOURCE.slice(start, next === -1 ? SCRIPT_SOURCE.length : next);
}

/** Compiles an equation the way butterchurn would, then runs it against fresh params. */
function compileAndRun(sandbox, equationSource) {
    const fn = createReferenceCompiler(sandbox)(equationSource);
    const params = {};
    const returned = fn(params);
    return { fn, params, returned };
}

test('CreateReferenceCompiler_PlainEquation_ReturnsTheParametersObject', () => {
    const sandbox = createSandbox();

    const { fn, params, returned } = compileAndRun(sandbox, 'a.bass = 1;');

    assert.equal(typeof fn, 'function');
    assert.equal(params.bass, 1);
    // Butterchurn appends ` return a;`, so the compiled function returns the params object.
    assert.equal(returned, params);
});

test('CreateReferenceCompiler_HostileEquationSource_IsHandledAsData', () => {
    const sandbox = createSandbox();
    const compile = createReferenceCompiler(sandbox);
    // Raw line separators that are legal inside a JS string literal but that naive escaping
    // (JSON.stringify) emits unescaped — a classic way to break a constructed code string.
    const separators = '\u2028\u2029';

    const cases = [
        {
            name: 'double/single quotes and backslashes',
            source: String.raw`a.q = "x\"y"; a.s = 'it\'s'; a.b = "a\\b";`,
            expect: { q: String.raw`x"y`, s: String.raw`it's`, b: String.raw`a\b` },
        },
        {
            name: 'newlines and trailing line comment',
            source: 'a.n1 = 1;\na.n2 = 2; // "quoted" comment',
            expect: { n1: 1, n2: 2 },
        },
        {
            name: 'script-terminator text',
            source: 'a.script = "</script>";',
            expect: { script: '</script>' },
        },
        {
            name: 'template literal and interpolation text',
            source: 'a.tpl = `t ${1 + 1}`;',
            expect: { tpl: 't 2' },
        },
        {
            name: 'raw unicode line separators',
            source: `a.sep = "${separators}";`,
            expect: { sep: separators },
        },
    ];

    for (const { name, source, expect } of cases) {
        const params = {};
        compile(source)(params);
        assert.deepEqual(params, expect, `case failed: ${name}`);
    }
});

test('CreateReferenceCompiler_TrailingLineComment_KeepsButterchurnAppendSemantics', () => {
    const sandbox = createSandbox();

    // `src + " return a;"` is appended on the same line; a trailing `//` comment swallows it.
    // Both the sandbox compiler and butterchurn's own `new Function` behave this way.
    const { params, returned } = compileAndRun(sandbox, 'a.x = 1; // trailing comment');

    assert.equal(params.x, 1);
    assert.equal(returned, undefined);
});

test('CreateReferenceCompiler_EquationReadingAGlobal_ResolvesItInTheSandboxRealm', () => {
    const sandbox = createSandbox();
    sandbox.presetSeed = 41;

    const params = {};
    createReferenceCompiler(sandbox)('a.marker = presetSeed + 1;')(params);

    // A compiler built in the Node realm could not see the sandbox global at all.
    assert.equal(params.marker, 42);
});

test('CreateReferenceCompiler_UndeclaredAssignment_TargetsTheSandboxGlobal', () => {
    const sandbox = createSandbox();

    createReferenceCompiler(sandbox)('implicitTestGlobal = 7;')({});

    // Preset equations are sloppy-mode: implicit globals must land in the sandbox, never in Node.
    assert.equal(sandbox.implicitTestGlobal, 7);
    assert.equal(Object.prototype.hasOwnProperty.call(globalThis, 'implicitTestGlobal'), false);
});

test('CreateReferenceCompiler_EvaluatedCodeText_IsAConstant', () => {
    const body = functionSource('createReferenceCompiler');

    assert.match(body, /vm\.runInContext\(/, 'the compiler must be created inside the sandbox realm');
    assert.ok(!body.includes('${'), 'the evaluated code text must not interpolate runtime data');
    assert.ok(!body.includes('JSON.stringify'), 'JSON.stringify is not a code sanitizer');
});

test('RuntimeDiff_ReferenceCompilation_ReceivesTheEquationAsAnArgument', () => {
    const body = functionSource('runtimeDiff');

    assert.match(body, /compileReference\(str\)/, 'the equation source must be passed as an argument');
    assert.match(body, /createReferenceCompiler\(sandbox\)/);
    assert.ok(!body.includes('function (src)'), 'the legacy interpolating compiler must not return');
    assert.ok(!body.includes('JSON.stringify(str)'), 'the equation source must not be serialised into code');
});
