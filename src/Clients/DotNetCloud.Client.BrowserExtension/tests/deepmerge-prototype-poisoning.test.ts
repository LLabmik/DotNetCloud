// ─── Vendored deepmerge hardening — Unit Tests ───────────────────────────────
// Guards CVE-2026-93753 / GHSA-gcqx-w48m-w2h4, which affects deepmerge <= 4.3.1.
// The vendored copy must ignore prototype-mutating keys instead of writing them.

import './setup';

const deepmerge: (
  target: Record<string, unknown>,
  source: Record<string, unknown>,
  options?: Record<string, unknown>,
) => Record<string, unknown> = jest.requireActual('../vendor/deepmerge');

describe('vendored deepmerge prototype-poisoning hardening', () => {
  afterEach(() => {
    delete (Object.prototype as Record<string, unknown>)['polluted'];
  });

  it('ignores a __proto__ key from the source', () => {
    const source = JSON.parse('{"__proto__":{"polluted":"yes"}}') as Record<string, unknown>;

    const merged = deepmerge({}, source);

    expect((Object.prototype as Record<string, unknown>)['polluted']).toBeUndefined();
    expect((merged as Record<string, unknown>)['polluted']).toBeUndefined();
  });

  it('ignores constructor and prototype keys from the source', () => {
    const source = JSON.parse(
      '{"constructor":{"prototype":{"polluted":"yes"}},"prototype":{"polluted":"yes"}}',
    ) as Record<string, unknown>;

    const merged = deepmerge({}, source);

    expect((Object.prototype as Record<string, unknown>)['polluted']).toBeUndefined();
    expect(Object.hasOwn(merged, 'constructor')).toBe(false);
    expect(Object.hasOwn(merged, 'prototype')).toBe(false);
  });

  it('ignores a __proto__ key present on the target', () => {
    const target = JSON.parse('{"__proto__":{"polluted":"yes"}}') as Record<string, unknown>;

    deepmerge(target, { safe: true });

    expect((Object.prototype as Record<string, unknown>)['polluted']).toBeUndefined();
  });

  it('still merges ordinary nested objects', () => {
    const merged = deepmerge(
      { server: { host: 'example.test', port: 443 } },
      { server: { port: 8443 } },
    );

    expect(merged).toEqual({ server: { host: 'example.test', port: 8443 } });
  });
});
