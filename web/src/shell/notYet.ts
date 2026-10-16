import { useApp } from '../app/store';
import { useT } from '../i18n';

export type NotYetReason = 'later' | 'phase2';

/**
 * 명령이 없는 버튼(시작 화면의 Tool sets 등)을 눌렀을 때 명령창 기록에 안내를 남긴다.
 * 명령이 있는 버튼은 runCommand를 쓴다 (등록만 된 명령이면 runner가 같은 안내를 한다).
 */
export function useNotYet() {
  const log = useApp((s) => s.log);
  const t = useT();
  return (cmd: string, reason: NotYetReason = 'later') => {
    const key = reason === 'phase2' ? 'soon.phase2Msg' : 'soon.laterMsg';
    log({ text: `${t('cmdline.prompt')} ${cmd}`, kind: 'cmd' }, { text: t(key, { cmd }), kind: 'info' });
  };
}
