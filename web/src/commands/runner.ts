import { activeTab, useApp } from '../app/store';
import { translate, type MessageKey } from '../i18n';
import { BUILTINS, TRANSPARENT } from './builtins';
import { hangulToLatin } from './hangul';
import { aliasesOf, findCommand, suggestCommand } from './lookup';
import { matchOption, parseNumber, parsePoint } from './parseInput';
import { COMMANDS, type Answer, type Ask, type CommandContext, type CommandDef } from './registry';

/** Esc로 명령을 취소하면 ask가 이 오류로 끝난다. 명령 대본은 잡지 않아도 된다. */
export class CommandCancelled extends Error {
  constructor() { super('cancelled'); }
}

const st = () => useApp.getState();
const tr = (key: MessageKey, params?: Record<string, string | number>) => translate(st().language, key, params);
const print = (text: string, kind: 'info' | 'cmd' | 'ok' | 'error' = 'info') => st().log({ text, kind });

/** 기다리는 질문 하나 (명령은 한 번에 하나만 돈다). */
let pending: { ask: Ask; resolve: (a: Answer) => void; reject: (e: Error) => void } | null = null;
/** `ZOOM E`처럼 명령 뒤에 미리 친 답. 질문이 오면 차례로 넣는다. */
let queued: string[] = [];
/** 지금 도는 명령의 번호 (취소된 명령이 끝나며 새 명령의 상태를 지우지 않게). */
let runId = 0;

const RECALL_LIMIT = 50;

/** 명령 이름 + 첫 별칭 (예: `LINE (L)`). */
export const nameWithAlias = (def: CommandDef) => {
  const alias = aliasesOf(def)[0];
  return alias ? `${def.name} (${alias})` : def.name;
};

/** 질문을 명령창에 보이는 글자로: `Close it anyway? [Yes/No] <No>:` */
export function promptText(ask: Ask): string {
  const options = 'options' in ask && ask.options?.length ? ` [${ask.options.join('/')}]` : '';
  const def = 'defaultValue' in ask && ask.defaultValue !== undefined ? ` <${ask.defaultValue}>` : '';
  return `${ask.prompt}${options}${def}:`;
}

/** 입력 글자를 질문의 답으로 바꾼다. 틀리면 고칠 방법을 담은 오류 키. */
export function interpret(ask: Ask, text: string): Answer | { error: MessageKey; params?: Record<string, string> } {
  const options = 'options' in ask ? ask.options ?? [] : [];
  if (ask.kind === 'text') return text === '' ? { done: true } : { text };
  if (text.trim() === '') {
    if ('defaultValue' in ask && ask.defaultValue !== undefined)
      return typeof ask.defaultValue === 'number' ? { number: ask.defaultValue } : { option: ask.defaultValue };
    return { done: true };
  }
  const option = matchOption(text, options);
  switch (ask.kind) {
    case 'option':
      return option ? { option } : { error: 'cmdline.invalidOption', params: { options: options.join(', ') } };
    case 'number': {
      const n = parseNumber(text);
      if (n !== undefined) return { number: n };
      return option ? { option } : { error: 'cmdline.invalidNumber' };
    }
    case 'point': {
      if (option) return { option };
      const p = parsePoint(text);
      return p ? { point: p } : { error: 'cmdline.invalidPoint' };
    }
    case 'select':
      return { text };
  }
}

function answer(raw: string) {
  if (!pending) return;
  const p = pending;
  const text = p.ask.kind === 'text' ? raw : hangulToLatin(raw.trim());
  const result = interpret(p.ask, text);
  if ('error' in result) {
    queued = []; // 미리 친 답이 틀렸으면 나머지는 버리고 다시 묻는다
    print(tr(result.error, result.params), 'error');
    return;
  }
  print(`${promptText(p.ask)} ${text}`, 'cmd');
  pending = null;
  st().setCmd({ ask: null });
  p.resolve(result);
}

function ask(a: Ask): Promise<Answer> {
  return new Promise((resolve, reject) => {
    pending = { ask: a, resolve, reject };
    st().setCmd({ ask: a });
    const next = queued.shift();
    if (next !== undefined) answer(next);
  });
}

/** 명령 하나를 시작한다. 끝날 때까지 기다리는 Promise (테스트용). */
async function start(word: string, args: string[], echo: string | null): Promise<void> {
  if (echo !== null) print(`${tr('cmdline.prompt')} ${echo}`, 'cmd');
  const def = findCommand(COMMANDS, word);
  if (!def) {
    const near = suggestCommand(COMMANDS, word);
    st().setCmd({ suggestion: near?.name ?? null });
    print(near ? tr('cmdline.didYouMean', { word: word.toUpperCase(), cmd: nameWithAlias(near) }) : tr('cmdline.unknown', { word: word.toUpperCase() }), 'error');
    return;
  }
  st().setCmd({ suggestion: null });
  const tab = activeTab(st());
  if (def.where && !def.where.includes(tab.kind)) {
    print(tr('cmdline.notHere', { cmd: def.name, tab: tr(`tabs.${tab.kind}` as MessageKey) }), 'error');
    return;
  }
  if (def.phase === 2) return print(tr('soon.phase2Msg', { cmd: def.name }));
  const run = BUILTINS[def.name] ?? def.run;
  if (!run) return print(tr('soon.laterMsg', { cmd: def.name }));

  const id = ++runId;
  queued = args;
  st().setCmd({ running: def.name, last: def.name });
  // 취소된 명령이 나중에 질문하면 새 명령의 질문을 빼앗지 않고 바로 취소로 끝난다
  const runAsk = (a: Ask) => (id === runId ? ask(a) : Promise.reject(new CommandCancelled()));
  const ctx: CommandContext = { tabId: tab.id, t: tr, print: (text, kind) => print(text, kind), ask: runAsk };
  try {
    await run(ctx);
  } catch (e) {
    if (!(e instanceof CommandCancelled)) {
      console.error(e);
      print(`${def.name}: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  } finally {
    if (id === runId) {
      pending = null;
      queued = [];
      st().setCmd({ running: null, ask: null });
    }
  }
}

/**
 * 명령창에서 Enter (또는 Space). 질문 중이면 답, 아니면 명령.
 * 빈 입력: 질문 중이면 기본값·단계 끝, 아니면 직전 명령 반복 (설계서 6-4).
 */
export function submit(raw: string): Promise<void> {
  // 한/영을 안 바꾸고 친 명령도 알아듣게 (글자를 묻는 질문은 한글 그대로)
  const text = pending?.ask.kind === 'text' ? raw.trim() : hangulToLatin(raw.trim());
  if (text) {
    const recall = st().cmd.recall;
    if (recall[recall.length - 1] !== text) st().setCmd({ recall: [...recall, text].slice(-RECALL_LIMIT) });
  }
  if (pending) {
    answer(pending.ask.kind === 'text' ? raw : text);
    return Promise.resolve();
  }
  const { running, last } = st().cmd;
  if (running) {
    print(tr('cmdline.busy', { cmd: running }), 'error');
    return Promise.resolve();
  }
  if (!text) return last ? start(last, [], last) : Promise.resolve();
  const [word, ...args] = text.split(/\s+/);
  return start(word, args, text);
}

/**
 * 버튼·단축키가 명령을 부른다 (= 명령창에 쳐 준 것과 같음, 6-1).
 * 다른 명령이 도는 중이면 그것을 취소하고 시작한다. 단 GRID 같은 투명 명령은 끼어들어 실행만 한다.
 * quiet: 단축키처럼 `Command: …` 줄을 남기지 않음.
 */
export function runCommand(text: string, options: { quiet?: boolean } = {}): Promise<void> {
  const [word, ...args] = text.trim().split(/\s+/);
  const def = findCommand(COMMANDS, word);
  if (def && TRANSPARENT.has(def.name) && st().cmd.running) {
    BUILTINS[def.name]({ tabId: activeTab(st()).id, t: tr, print: (t, k) => print(t, k), ask });
    return Promise.resolve();
  }
  cancel();
  st().setCmd({ input: '' });
  return start(word, args, options.quiet ? null : text.trim());
}

/** Esc: 지금 명령 취소. 취소한 것이 있으면 true. */
export function cancel(echo = true): boolean {
  const { running } = st().cmd;
  if (!running) return false;
  const p = pending;
  pending = null;
  queued = [];
  runId++;
  st().setCmd({ running: null, ask: null });
  if (echo) print(tr('cmdline.cancel'), 'cmd');
  p?.reject(new CommandCancelled());
  return true;
}

/** 테스트용: 모듈 안 상태를 비운다. */
export function resetRunner() {
  pending = null;
  queued = [];
  runId++;
}
