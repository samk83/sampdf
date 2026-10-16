import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { FileInfo } from '../api';
import { initialState, useApp } from '../app/store';
import { resetRunner, submit } from '../commands/runner';
import { cancelCompare, openFile, setIncluded, startCompare } from './actions';
import { compareRibbonState } from './commands';
import { useCompare } from './state';
import { change, pair, result } from './testFixtures';

// 서버 대신 (설계서 U3: 서버는 그대로, 화면 쪽만 확인)
const api = vi.hoisted(() => ({
  uploadFile: vi.fn(),
  runCompare: vi.fn(),
  closeFile: vi.fn(),
  exportPdf: vi.fn(),
  saveBlob: vi.fn(),
}));
vi.mock('../api', () => api);

const app = () => useApp.getState();
const cmp = (id: string) => useCompare.getState().tabs[id];
const lastLine = () => app().history[app().history.length - 1]?.text;
const info = (id: string, pages = 2): FileInfo => ({ id, name: `${id}.pdf`, pages: Array.from({ length: pages }, () => ({ width: 600, height: 400 })), isImage: false } as FileInfo);
const file = (name: string) => ({ name, size: 2048 }) as File;
const flush = () => new Promise((r) => setTimeout(r, 0));

const R = result([pair(0, [change('c1'), change('c2')]), pair(1, [change('c3')])]);

/** 비교 탭 하나를 열고 두 파일을 올려 비교까지 끝낸다. */
async function comparedTab(): Promise<string> {
  const id = app().openTab('compare');
  api.uploadFile.mockImplementation(async (f: File) => info(f.name.replace('.pdf', '')));
  api.runCompare.mockResolvedValue(R);
  await openFile(id, 'left', file('old.pdf'));
  await openFile(id, 'right', file('new.pdf'));
  await flush();
  return id;
}

beforeEach(() => {
  resetRunner();
  useApp.setState(initialState());
  useCompare.setState({ tabs: {} });
  vi.clearAllMocks();
});

describe('파일 열기·비교 (actions)', () => {
  it('두 파일을 열면 비교, 결과는 변경이 있는 첫 페이지', async () => {
    const id = await comparedTab();
    expect(api.runCompare).toHaveBeenCalledTimes(1);
    expect(cmp(id)).toMatchObject({ comparing: false, pairIndex: 0 });
    expect(cmp(id).result?.id).toBe('r1');
    expect(app().history.map((l) => l.text)).toContain('3 changes on 2 page(s).');
  });

  it('못 여는 파일 = 그 칸에 오류, 다른 칸은 그대로', async () => {
    const id = app().openTab('compare');
    api.uploadFile.mockRejectedValue(new Error('Not a PDF or image.'));
    await openFile(id, 'left', file('bad.pdf'));
    expect(cmp(id).left).toMatchObject({ file: null, error: 'Not a PDF or image.', failedName: 'bad.pdf', busy: false });
    expect(lastLine()).toContain("Couldn't open bad.pdf");
  });

  it('비교 실패 = compareError, 취소 = 결과 없이 파일은 남음', async () => {
    const id = app().openTab('compare');
    api.uploadFile.mockImplementation(async (f: File) => info(f.name));
    api.runCompare.mockRejectedValue(new Error('Engine stopped'));
    await openFile(id, 'left', file('a'));
    await openFile(id, 'right', file('b'));
    await flush();
    expect(cmp(id).compareError).toBe('Engine stopped');

    api.runCompare.mockReturnValue(new Promise(() => {}));
    startCompare(id);
    expect(cmp(id).comparing).toBe(true);
    expect(cancelCompare(id)).toBe(true);
    expect(cmp(id)).toMatchObject({ comparing: false, result: null });
    expect(cmp(id).left.file?.id).toBe('a');
    expect(cancelCompare(id)).toBe(false);
  });

  it('탭을 닫는 사이 끝난 업로드는 서버에서 닫는다', async () => {
    const id = app().openTab('compare');
    let done!: (f: FileInfo) => void;
    api.uploadFile.mockReturnValue(new Promise((r) => (done = r)));
    const p = openFile(id, 'left', file('x.pdf'));
    app().closeTab(id);
    done(info('x'));
    await p;
    expect(api.closeFile).toHaveBeenCalledWith('x');
  });
});

describe('비교 명령 (설계서 6-6)', () => {
  it('결과 전에는 NEXT·OVERLAY가 안내만', async () => {
    app().openTab('compare');
    await submit('NEXT');
    expect(lastLine()).toBe('Compare the drawings first.');
  });

  it('NEXT·PREV·REVIEWED·EXCLUDE', async () => {
    const id = await comparedTab();
    await submit('N');
    expect(cmp(id).selectedId).toBe('c1');
    await submit('RV');
    expect(cmp(id).marks.reviewed).toEqual(['c1']);
    expect(cmp(id).selectedId).toBe('c2');
    await submit('X');
    expect(cmp(id).marks.excluded).toEqual(['c2']);
    await submit('NEXT');
    expect(cmp(id)).toMatchObject({ selectedId: 'c3', pairIndex: 1 });
    await submit('NEXT');
    expect(lastLine()).toBe('That was the last change.');
    await submit('PV');
    expect(cmp(id)).toMatchObject({ selectedId: 'c2', pairIndex: 0 });
  });

  it('UNDO·REDO는 표시 수정·검토·빼기를 되돌린다', async () => {
    const id = await comparedTab();
    setIncluded(id, 'c1', false);
    await submit('UNDO');
    expect(cmp(id).marks.excluded).toEqual([]);
    await submit('REDO');
    expect(cmp(id).marks.excluded).toEqual(['c1']);
    await submit('U');
    await submit('U');
    expect(lastLine()).toBe('Nothing to undo.');
  });

  it('OVERLAY·SBS·SYNC·NUDGE, 리본 눌림 상태', async () => {
    const id = await comparedTab();
    expect(compareRibbonState().pressed.has('SBS')).toBe(true);
    await submit('NUDGE');
    expect(lastLine()).toContain('NUDGE works in Overlay');
    await submit('OV');
    expect(cmp(id).viewMode).toBe('overlay');
    expect(compareRibbonState().pressed.has('OV')).toBe(true);
    await submit('NUDGE');
    expect(cmp(id).overlay.nudging).toBe(true);
    await submit('SBS');
    expect(cmp(id).viewMode).toBe('side');
    await submit('SYNC');
    expect(cmp(id).sync).toBe(false);
    expect(lastLine()).toBe('<Sync pan off>');
  });

  it('PAGE: 번호·Next·범위 밖', async () => {
    const id = await comparedTab();
    await submit('PAGE 2');
    expect(cmp(id).pairIndex).toBe(1);
    await submit('PAGE Previous');
    expect(cmp(id).pairIndex).toBe(0);
    await submit('PG 5');
    expect(lastLine()).toBe('Type a page from 1 to 2.');
  });

  it('NUMBERING: 같은 번호 + 시작 A', async () => {
    const id = await comparedTab();
    const run = submit('NUM');
    await flush();
    expect(app().cmd.ask?.prompt).toBe('Numbering');
    await submit('S');
    await flush();
    await submit('A');
    await run;
    expect(cmp(id).settings).toMatchObject({ numbering: 'same', startLabel: 'A' });
  });

  it('ZOOM In = 화면 부품에 부탁, Window는 나중', async () => {
    const id = await comparedTab();
    await submit('Z I');
    expect(cmp(id).viewRequest).toMatchObject({ kind: 'in', n: 1 });
    await submit('Z W');
    expect(lastLine()).toContain('later update');
  });

  it('EXPORTCSV는 CSV를 내려받는다, 결과가 없으면 리본 버튼이 흐리다', async () => {
    app().openTab('compare');
    expect(compareRibbonState().disabled.has('EXPORTCSV')).toBe(true);
    await comparedTab();
    expect(compareRibbonState().disabled.has('EXPORTCSV')).toBe(false);
    await submit('EXC');
    expect(api.saveBlob).toHaveBeenCalledTimes(1);
    expect(api.saveBlob.mock.calls[0][1]).toBe('new changes.csv');
  });

  it('EXPORT는 뺀 변경을 빼고 번호를 보낸다', async () => {
    const id = await comparedTab();
    setIncluded(id, 'c2', false);
    api.exportPdf.mockResolvedValue(undefined);
    await submit('EXPORT');
    const [cid, options] = api.exportPdf.mock.calls[0];
    expect(cid).toBe('r1');
    expect(options.excluded).toEqual(['c2']);
    expect(Object.keys(options.edits)).toEqual(['c1', 'c3']);
    expect(options.edits.c3.label).toBe('2');
  });
});
