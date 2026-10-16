import { useEffect, useState } from 'react';
import { useT, type MessageKey } from '../i18n';

/** 시험할 키 (설계서 2-5·10장 R7). 앱 명령에 쓰려는 키 중 브라우저가 가져갈 수 있는 것. */
export const TEST_KEYS = ['F1', 'F3', 'F7', 'F11', 'Ctrl+0', 'F2', 'F8', 'F9', 'Ctrl+9', 'Ctrl+Shift+F'] as const;

/** 키 이벤트를 "Ctrl+Shift+F" 같은 이름으로. */
export function keyName(e: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey'>): string {
  const key = e.key.length === 1 ? e.key.toUpperCase() : e.key;
  return [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Meta', key].filter(Boolean).join('+');
}

/**
 * 브라우저 키 시험 화면. 키를 받으면 기본 동작을 막아 본다(preventDefault).
 * 받았는지는 이 화면이 알 수 있지만, 브라우저가 기본 동작도 했는지는 사람이 보고 적어야 한다.
 */
export function KeyTest() {
  const t = useT();
  const [received, setReceived] = useState<Set<string>>(new Set());

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const name = keyName(e);
      if ((TEST_KEYS as readonly string[]).includes(name)) {
        e.preventDefault();
        setReceived((s) => new Set(s).add(name));
      }
    };
    window.addEventListener('keydown', onKey, { capture: true });
    return () => window.removeEventListener('keydown', onKey, { capture: true });
  }, []);

  return (
    <main className="h-full overflow-auto bg-background p-8 text-fg">
      <div className="mx-auto flex max-w-[760px] flex-col gap-4">
        <h1 className="m-0 text-xl font-bold">{t('keytest.title')}</h1>
        <p className="m-0 text-fg-muted">{t('keytest.intro')}</p>
        <table className="w-full border-collapse text-sm">
          <thead className="bg-sunken text-left text-fg-muted">
            <tr><th className="p-2">{t('keytest.key')}</th><th className="p-2">{t('keytest.browser')}</th><th className="p-2">{t('keytest.received')}</th></tr>
          </thead>
          <tbody>
            {TEST_KEYS.map((k) => (
              <tr key={k} className="border-b border-subtle">
                <td className="p-2 font-mono">{k}</td>
                <td className="p-2 text-fg-secondary">{t(`keytest.action.${k}` as MessageKey)}</td>
                <td className={`p-2 font-semibold ${received.has(k) ? 'text-success' : 'text-fg-faint'}`}>
                  {received.has(k) ? t('keytest.yes') : t('keytest.no')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="m-0 font-mono text-xs text-fg-faint">{t('keytest.browserInfo')}: {navigator.userAgent}</p>
        <a href="./" className="text-accent">{t('keytest.back')}</a>
      </div>
    </main>
  );
}
