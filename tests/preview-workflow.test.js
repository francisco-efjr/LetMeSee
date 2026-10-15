'use strict';
// Domain tests for the browser DEMONSTRATION. These are not hardware tests.
const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs');
const vm = require('node:vm');
const W = require('../docs/design/workflow.js');
const NOW = 1000000;
function confirmAll(state) { W.checklist(state).manual.forEach(item => W.confirm(state, item.id, true)); return state; }
function prepared(options) { const state = W.create(Object.assign({ scenario: 'configured' }, options)); confirmAll(state); assert.equal(W.finish(state, NOW), true); return state; }

test('configured matrix requires explicit manual preparation in both experience modes', () => {
  for (const host of ['windows', 'macos']) for (const family of ['android', 'ios']) for (const method of ['usb', 'wifi']) for (const profile of ['guided', 'expert']) {
    const state = W.create({ host, family, method, profile, scenario: 'configured' });
    assert.equal(W.checklist(state).ready, false, `${host}/${family}/${method}/${profile}`);
    assert.equal(W.finish(state, NOW), false);
    confirmAll(state);
    assert.equal(W.checklist(state).ready, true);
    assert.equal(W.finish(state, NOW), true);
    assert.equal(W.start(state, NOW + 1), true);
  }
});

test('all scenario combinations expose internally consistent gates', () => {
  for (const host of ['windows', 'macos']) for (const family of ['android', 'ios']) for (const method of ['usb', 'wifi']) for (const scenario of W.SCENARIOS) {
    const state = W.create({ host, family, method, scenario });
    confirmAll(state);
    const list = W.checklist(state);
    assert.equal(list.completed <= list.total, true);
    assert.equal(list.ready, list.automatic.every(item => item.done));
    assert.equal(W.finish(state, NOW), list.ready);
  }
});

test('new systems cannot bypass absent motor or Android discovery with confirmations', () => {
  for (const host of ['windows', 'macos']) {
    const state = W.create({ host });
    confirmAll(state);
    assert.equal(W.checklist(state).automatic.find(i => i.id === 'engine').done, false);
    assert.equal(W.checklist(state).ready, false);
    W.repair(state, 'engine'); confirmAll(state);
    assert.equal(W.checklist(state).ready, false);
    W.repair(state, 'devices'); confirmAll(state);
    assert.equal(W.checklist(state).ready, true);
  }
});

test('manual confirmation never supersedes unauthorized Android state', () => {
  const state = W.create({ scenario: 'unauthorized' });
  confirmAll(state);
  assert.equal(W.checklist(state).ready, false);
  assert.equal(W.checklist(state).automatic.find(i => i.id === 'devices').done, false);
  assert.equal(W.start(state, NOW), false);
  assert.equal(state.view, 'setup');
});

test('tampered engines stay blocked until explicit simulated repair', () => {
  const state = W.create({ scenario: 'tampered' });
  confirmAll(state);
  assert.equal(W.checklist(state).ready, false);
  W.repair(state, 'engine');
  assert.equal(Object.values(state.confirmations).length, 0);
  confirmAll(state);
  assert.equal(W.checklist(state).ready, true);
});

test('multiple Androids require explicit selection, including after context change', () => {
  const state = W.create({ scenario: 'multiple' });
  assert.equal(state.selectedDevice, '');
  confirmAll(state);
  assert.equal(W.checklist(state).ready, false);
  const devices = W.probes(state).devices;
  W.selectDevice(state, devices[1].id);
  assert.equal(state.selectedDevice, devices[1].id);
  assert.deepEqual(state.confirmations, {});
  confirmAll(state);
  assert.equal(W.finish(state, NOW), true);
  W.selectDevice(state, devices[0].id);
  assert.equal(state.approvedAt, null);
  assert.equal(W.checklist(state).ready, false);
  W.selectDevice(state, 'not-in-the-list');
  assert.equal(state.selectedDevice, '');
});

test('USB and network discovery select separate fictitious Android serials', () => {
  const state = W.create({ scenario: 'configured' });
  assert.match(state.selectedDevice, /^USB-/);
  W.change(state, { method: 'wifi' });
  assert.match(state.selectedDevice, /^192\.0\.2\./);
  assert.equal(W.checklist(state).manual.some(item => item.id === 'network-trust'), true);
  assert.equal(W.checklist(state).manual.some(item => item.id === 'cable'), false);
});

test('no-network blocks network routes but does not invent a USB requirement', () => {
  for (const family of ['android', 'ios']) {
    const usb = W.create({ host: 'macos', family, method: 'usb', scenario: 'no-network' });
    confirmAll(usb);
    assert.equal(W.checklist(usb).ready, true);
    const wifi = W.create({ host: 'macos', family, method: 'wifi', scenario: 'no-network' });
    confirmAll(wifi);
    assert.equal(W.checklist(wifi).ready, false);
  }
});

test('Windows iOS always selects AirPlay, and Bonjour/ports remain separate gates', () => {
  const state = W.create({ family: 'ios', method: 'usb', scenario: 'new' });
  assert.equal(state.method, 'wifi');
  assert.equal(W.route(state), 'uxplay');
  confirmAll(state);
  W.repair(state, 'engine'); confirmAll(state);
  assert.equal(W.checklist(state).automatic.find(item => item.id === 'system').done, false);
  assert.equal(W.checklist(state).ready, false);
  const port = W.create({ family: 'ios', scenario: 'port' });
  confirmAll(port);
  assert.equal(W.checklist(port).automatic.find(item => item.id === 'port').done, false);
  assert.equal(W.finish(port, NOW), false);
});

test('Apple permission denial cannot be cleared by a checkbox', () => {
  const state = W.create({ host: 'macos', family: 'ios', method: 'usb', scenario: 'permission-denied' });
  confirmAll(state);
  assert.equal(W.checklist(state).ready, false);
  W.repair(state, 'permission'); confirmAll(state);
  assert.equal(W.checklist(state).ready, true);
});

test('profile selection changes explanation only, with no relaxed gates or erased consent', () => {
  const state = prepared();
  const before = W.checklist(state);
  const approvedAt = state.approvedAt;
  W.change(state, { profile: 'expert' });
  assert.deepEqual(W.checklist(state), before);
  assert.equal(state.approvedAt, approvedAt);
});

test('host, device family, method and scenario changes invalidate evidence and manual consent', () => {
  for (const patch of [{host:'macos'}, {family:'ios'}, {method:'wifi'}, {scenario:'no-network'}]) {
    const state = prepared();
    W.change(state, patch);
    assert.equal(state.view, 'setup');
    assert.equal(state.approvedAt, null);
    assert.deepEqual(state.confirmations, {});
    assert.equal(W.start(state, NOW + 1), false);
  }
});

test('unsupported computer blocks preparation', () => {
  const state = W.create({ host: 'linux', scenario: 'configured' });
  confirmAll(state);
  assert.equal(W.checklist(state).automatic.find(item => item.id === 'host').done, false);
  assert.equal(W.finish(state, NOW), false);
});

test('fresh start revalidates device removal and does not run a motor', () => {
  const state = prepared();
  const live = Object.assign({}, W.probes(state), { devices: [] });
  assert.equal(W.start(state, NOW + 1, live), false);
  assert.equal(state.view, 'setup');
  assert.equal(state.status, 'idle');
  assert.deepEqual(state.confirmations, {});
  assert.equal(W.checklist(state).automatic.find(item => item.id === 'devices').done, false);
});

test('fresh start rejects changed engine integrity or network observation', () => {
  for (const patch of [{engine:'tampered'}, {network:false}]) {
    const state = prepared({ method: 'wifi' });
    assert.equal(W.start(state, NOW + 1, Object.assign({}, W.probes(state), patch)), false);
    assert.equal(state.status, 'idle');
  }
});

test('five-minute evidence boundary and backward clock expire preparation', () => {
  assert.equal(W.start(prepared(), NOW + W.TTL - 1), true);
  assert.equal(W.start(prepared(), NOW + W.TTL), false);
  assert.equal(W.start(prepared(), NOW - 1), false);
});

test('duplicate start never produces a second active session', () => {
  const state = prepared();
  assert.equal(W.start(state, NOW + 1), true);
  assert.equal(W.start(state, NOW + 2), false);
  assert.equal(state.status, 'android-demo');
});

test('external Apple routes are guidance, not a claim of system-controlled mirroring', () => {
  const quicktime = prepared({ host: 'macos', family: 'ios', method: 'usb' });
  W.start(quicktime, NOW + 1);
  assert.equal(quicktime.status, 'continue-quicktime');
  W.stop(quicktime);
  assert.match(quicktime.notice, /não controla/);
  const airplay = prepared({ host: 'macos', family: 'ios', method: 'wifi' });
  W.start(airplay, NOW + 1);
  assert.equal(airplay.status, 'continue-iphone');
});

test('stop, interruption and suspension invalidate the session rather than silently restarting', () => {
  for (const reason of ['stop', 'error', 'network changed', 'sleep/resume']) {
    const state = prepared();
    W.start(state, NOW + 1);
    if (reason === 'stop') W.stop(state); else W.invalidate(state, reason);
    assert.equal(state.status, 'idle');
    assert.equal(state.approvedAt, null);
    assert.equal(W.start(state, NOW + 2), false);
  }
});

test('HTML main surface contains exactly two controls and no network dependencies', () => {
  const html = fs.readFileSync(require.resolve('../docs/design/preview.html'), 'utf8');
  const main = html.split('<section id="mirror"')[1].split('</section>')[0];
  assert.equal((main.match(/<button\b/g) || []).length, 2);
  assert.equal((main.match(/<(select|input|nav|a)\b/g) || []).length, 0);
  assert.match(html, /DEMONSTRAÇÃO INTERATIVA/);
  assert.doesNotMatch(html, /(?:src|href)="https?:/);
  const scripts = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
  scripts.forEach(script => new vm.Script(script[1]));
});
