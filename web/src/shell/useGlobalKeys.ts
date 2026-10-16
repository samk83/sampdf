import { useEffect } from 'react';
import { classifyTarget, routeKey } from '../app/keyboard';
import { useApp } from '../app/store';
import { toggleAid } from '../commands/builtins';
import { cancel, runCommand, submit } from '../commands/runner';
import { translate } from '../i18n';
import { focusCmdline } from './cmdlineFocus';

/** 창 전체의 키를 명령창·단축키로 나눠 보낸다 (설계서 6-7). */
export function useGlobalKeys() {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.defaultPrevented) return;
      const s = useApp.getState();
      const action = routeKey({
        key: e.key, code: e.code, ctrl: e.ctrlKey, shift: e.shiftKey, alt: e.altKey, meta: e.metaKey,
        target: classifyTarget(document.activeElement), modalOpen: s.helpOpen,
      });
      if (!action) return;
      e.preventDefault();
      switch (action.kind) {
        case 'run': void runCommand(action.command, { quiet: true }); break;
        case 'osnap': toggleAid('osnap'); break;
        case 'history': s.setUi({ historyExpanded: !s.historyExpanded }); break;
        case 'cancel': cancel(); break;
        case 'submit': {
          const text = s.cmd.input;
          s.setCmd({ input: '' });
          void submit(text);
          break;
        }
        case 'type':
          if (s.cmdlineHidden) {
            s.setUi({ cmdlineHidden: false });
            s.log({ text: translate(s.language, 'cmdline.shown'), kind: 'info' });
          }
          s.setCmd({ input: s.cmd.input + action.char, suggestion: null });
          focusCmdline();
          break;
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
}
