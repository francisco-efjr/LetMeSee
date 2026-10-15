/* Browser demo only. This module never probes hardware or starts a process. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.LetMeSeeWorkflow = api;
})(typeof globalThis === 'object' ? globalThis : this, function () {
  'use strict';
  const TTL = 5 * 60 * 1000;
  const SCENARIOS = ['new', 'configured', 'unauthorized', 'no-network', 'multiple', 'tampered', 'port', 'permission-denied'];
  function route(state) {
    if (state.family === 'android') return 'scrcpy-' + state.method;
    if (state.host === 'windows') return 'uxplay';
    return state.method === 'usb' ? 'quicktime' : 'airplay-mac';
  }
  function normalize(state) {
    if (state.family === 'ios' && state.host === 'windows') state.method = 'wifi';
    return state;
  }
  function create(options) {
    const state = Object.assign({ host: 'windows', family: 'android', method: 'usb', profile: 'guided', scenario: 'new', selectedDevice: '', confirmations: {}, repairs: {}, approvedAt: null, view: 'setup', status: 'idle', notice: '' }, options);
    normalize(state);
    chooseSingle(state);
    return state;
  }
  function baseProbes(state) {
    const method = route(state), managed = method.startsWith('scrcpy') || method === 'uxplay';
    const prefix = state.method === 'usb' ? 'USB-DEMO-' : '192.0.2.10:5555-DEMO-';
    const probes = { host: ['windows', 'macos'].includes(state.host), engine: managed && state.scenario === 'new' ? 'missing' : 'verified', system: true, network: state.scenario !== 'no-network', port: true, permission: true, devices: [] };
    if (managed && state.scenario === 'tampered') probes.engine = 'tampered';
    if (state.family === 'android') {
      if (state.scenario !== 'new') probes.devices = [{ id: prefix + 'A', name: 'Android de demonstração A', status: ['unauthorized', 'permission-denied'].includes(state.scenario) ? 'unauthorized' : 'device' }];
      if (state.scenario === 'multiple') probes.devices.push({ id: prefix + 'B', name: 'Android de demonstração B', status: 'device' });
    }
    if (method === 'uxplay') {
      probes.system = state.scenario !== 'new';
      probes.port = state.scenario !== 'port';
    }
    if (state.family === 'ios' && state.scenario === 'permission-denied') probes.permission = false;
    return Object.assign(probes, state.repairs);
  }
  function chooseSingle(state) {
    const devices = baseProbes(state).devices;
    if (state.family === 'android' && devices.length === 1) state.selectedDevice = devices[0].id;
  }
  function invalidate(state, notice) {
    state.confirmations = {};
    state.approvedAt = null;
    state.view = 'setup';
    state.status = 'idle';
    state.notice = notice || 'O contexto mudou. Confira a preparação novamente.';
    return state;
  }
  function change(state, patch) {
    const changesContext = ['host', 'family', 'method', 'scenario'].some(key => patch[key] !== undefined && patch[key] !== state[key]);
    if (changesContext) {
      state.repairs = {};
      state.selectedDevice = '';
      invalidate(state);
    }
    Object.assign(state, patch);
    normalize(state);
    if (changesContext) chooseSingle(state);
    return state;
  }
  function selectDevice(state, id) {
    if (state.selectedDevice === id) return state;
    invalidate(state, 'Aparelho alterado. Confirme os cuidados para este Android.');
    state.selectedDevice = baseProbes(state).devices.some(d => d.id === id) ? id : '';
    return state;
  }
  function manualItems(state) {
    const items = [];
    if (state.method === 'usb') items.push({ id: 'cable', title: 'Cabo que transfere dados', detail: 'Conectei um cabo de dados e deixei o aparelho desbloqueado. Um cabo que só carrega pode não servir.' });
    if (state.method === 'wifi') items.push({ id: 'network-trust', title: 'Rede local de confiança', detail: 'Os dois aparelhos estão em uma rede que permite comunicação local. Evitei rede pública ou de convidados; internet não é necessária depois da preparação.' });
    if (state.family === 'android') items.push({ id: 'android-authorization', title: 'Autorização no Android', detail: state.method === 'usb' ? 'Revisei a depuração USB e autorizei este computador na mensagem do telefone. A consulta ADB também precisa passar.' : 'Este Android já foi pareado com o ADB. O app não procura IPs nem habilita depuração de rede sozinho.' });
    if (route(state) === 'quicktime') items.push({ id: 'apple-trust', title: 'Confiança no computador', detail: 'Desbloqueei o iPhone e confirmei “Confiar” no próprio aparelho, se essa mensagem apareceu. Nenhum código é informado ao LetMeSee.' });
    if (route(state) === 'airplay-mac') items.push({ id: 'native-receiver', title: 'Receptor AirPlay do Mac', detail: 'Conferi a compatibilidade do Mac e habilitei Receptor AirPlay nos Ajustes. Esta é uma confirmação minha, não uma inspeção do app.' });
    items.push({ id: 'privacy', title: 'Privacidade antes da tela aparecer', detail: 'Revisei notificações e conteúdo pessoal visível. Controle remoto, gravação e microfone não são ativados neste fluxo.' });
    return items.map(item => Object.assign(item, { done: state.confirmations[item.id] === true }));
  }
  function automaticItems(state, observed) {
    const probes = observed || baseProbes(state), method = route(state), items = [];
    items.push({ id: 'host', title: 'Ambiente do computador', done: probes.host, detail: probes.host ? (state.host === 'windows' ? 'Windows' : 'macOS') + ' selecionado nesta simulação.' : 'Este sistema não tem uma rota suportada. Escolha Windows ou macOS.' });
    if (method.startsWith('scrcpy') || method === 'uxplay') {
      items.push({ id: 'engine', title: method === 'uxplay' ? 'Motor AirPlay local' : 'Motor Android local', done: probes.engine === 'verified', detail: probes.engine === 'verified' ? 'Arquivos e integridade aprovados na simulação.' : probes.engine === 'tampered' ? 'Integridade reprovada. Um arquivo alterado nunca deve ser executado.' : 'Motor ausente. A preparação é uma ação explícita; não há instalação automática.', repair: 'engine' });
    }
    if (method === 'uxplay') {
      items.push({ id: 'system', title: 'Descoberta Bonjour', done: probes.system, detail: probes.system ? 'Serviço disponível na simulação. Isso não prova que o iPhone encontra o receptor.' : 'Bonjour indisponível. No app, uma instalação exige ação explícita e instalador Apple verificado.', repair: 'system' });
      items.push({ id: 'port', title: 'Portas do receptor', done: probes.port, detail: probes.port ? 'Portas disponíveis na simulação.' : 'Uma porta está ocupada. Não encerramos outro programa nem desativamos o firewall.', repair: 'port' });
    }
    if (state.method === 'wifi') items.push({ id: 'network', title: 'Conexão com a rede local', done: probes.network, detail: probes.network ? 'Interface disponível na simulação. A pessoa ainda confirma a rede de confiança.' : 'Não há rede local disponível neste cenário. Reconecte ou use USB, se a rota oferecer.', repair: 'network' });
    if (state.family === 'android') {
      const selected = probes.devices.find(device => device.id === state.selectedDevice);
      let detail = 'Nenhum Android encontrado. Confira cabo de dados, desbloqueio, depuração e, no Windows, o driver oficial.';
      if (probes.devices.length && !selected) detail = 'Há mais de um Android. Escolha explicitamente o aparelho que você quer usar.';
      else if (selected && selected.status === 'unauthorized') detail = 'Android não autorizado. Desbloqueie e aceite a autorização RSA no próprio telefone; marcar uma confirmação não resolve este bloqueio.';
      else if (selected && selected.status === 'offline') detail = 'Android offline. Reconecte o aparelho e consulte novamente.';
      else if (selected && selected.status === 'device') detail = selected.name + ' autorizado na simulação. Isso ainda não comprova imagem recebida.';
      items.push({ id: 'devices', title: 'Android correto e autorizado', done: !!selected && selected.status === 'device', detail, repair: probes.devices.length > 1 && !selected ? null : 'devices' });
    } else items.push({ id: 'permission', title: 'Disponibilidade da rota Apple', done: probes.permission, detail: probes.permission ? 'Nenhuma restrição simulada. Os passos no iPhone e no Mac continuam manuais.' : 'Permissão negada neste cenário. Revise os ajustes no aparelho; uma política da organização exige ajuda do administrador.', repair: 'permission' });
    return items;
  }
  function checklist(state, observed) {
    const automatic = automaticItems(state, observed), manual = manualItems(state);
    return { automatic, manual, ready: automatic.every(item => item.done) && manual.every(item => item.done), total: automatic.length + manual.length, completed: automatic.filter(item => item.done).length + manual.filter(item => item.done).length };
  }
  function confirm(state, id, checked) {
    if (manualItems(state).some(item => item.id === id)) state.confirmations[id] = checked === true;
    state.approvedAt = null;
    return state;
  }
  function repair(state, field) {
    const patches = { engine: 'verified', system: true, network: true, port: true, permission: true };
    invalidate(state, 'Correção simulada aplicada. Revise e confirme os cuidados novamente.');
    if (field === 'devices') {
      state.repairs.devices = [{ id: (state.method === 'usb' ? 'USB' : 'WIFI') + '-DEMO-RECONNECTED', name: 'Android de demonstração reconectado', status: 'device' }];
      state.selectedDevice = state.repairs.devices[0].id;
    } else if (Object.prototype.hasOwnProperty.call(patches, field)) state.repairs[field] = patches[field];
    return state;
  }
  function finish(state, now) {
    if (!checklist(state).ready) return false;
    state.approvedAt = now;
    state.approvedProbes = JSON.stringify(baseProbes(state));
    state.view = 'mirror';
    state.status = 'ready';
    state.notice = '';
    return true;
  }
  function start(state, now, observed) {
    const live = observed || baseProbes(state);
    if (state.approvedAt === null || now < state.approvedAt || now - state.approvedAt >= TTL || state.approvedProbes !== JSON.stringify(live) || !checklist(state, live).ready) {
      invalidate(state, 'A preparação mudou ou venceu. Confira as condições atuais antes de começar.');
      if (observed) state.repairs = Object.assign({}, state.repairs, observed);
      return false;
    }
    if (state.status !== 'ready') return false;
    const method = route(state);
    state.status = method === 'quicktime' ? 'continue-quicktime' : method === 'airplay-mac' ? 'continue-iphone' : method === 'uxplay' ? 'receiver-demo' : 'android-demo';
    return true;
  }
  function stop(state) {
    return invalidate(state, ['continue-quicktime', 'continue-iphone'].includes(state.status) ? 'Orientação concluída. Encerre o espelhamento no QuickTime ou no iPhone; o LetMeSee não controla essa sessão do sistema.' : 'Demonstração encerrada. Nenhum motor real foi iniciado. Confira a preparação para uma nova sessão.');
  }
  return { TTL, SCENARIOS, create, route, change, selectDevice, checklist, confirm, repair, finish, start, stop, invalidate, probes: baseProbes };
});
