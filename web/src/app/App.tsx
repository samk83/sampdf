import { AppShell } from '../shell/AppShell';
import { KeyTest } from './KeyTest';

/** 주소 끝이 #keytest이면 브라우저 키 시험 화면 (위험 시험 R7), 아니면 앱. */
export function App() {
  return window.location.hash === '#keytest' ? <KeyTest /> : <AppShell />;
}
