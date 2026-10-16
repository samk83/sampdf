import { useApp } from '../app/store';
import { useT } from '../i18n';

export type NotYetReason = 'command' | 'later' | 'phase2';

/**
 * 아직 동작하지 않는 버튼을 눌렀을 때 명령창 기록에 그 명령과 안내를 남긴다.
 * U2에서 명령 시스템이 생기면 리본 버튼은 이 대신 명령을 실행한다.
 */
export function useNotYet() {
  const log = useApp((s) => s.log);
  const t = useT();
  return (cmd: string, reason: NotYetReason = 'command') => {
    const key = reason === 'phase2' ? 'soon.phase2Msg' : reason === 'later' ? 'soon.laterMsg' : 'soon.commandMsg';
    log({ text: `${t('cmdline.prompt')} ${cmd}`, kind: 'cmd' }, { text: t(key, { cmd }), kind: 'info' });
  };
}
