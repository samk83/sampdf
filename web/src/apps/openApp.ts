import { useApp } from '../app/store';
import { runCommand } from '../commands/runner';
import { appById, type AppDef } from './registry';

/** 쓸 수 있는 기능인가 (Ready·Experimental이고 여는 명령이 있음). */
export const isAppReady = (app: AppDef) => app.status !== 'soon' && !!app.command;

/** 기능 하나를 연다: 쓸 수 있으면 그 명령, 준비 중이면 기능 안내 탭 (시작 화면 카드·기능 주소가 같이 씀). */
export function openAppById(id: string): boolean {
  const app = appById(id);
  if (!app) return false;
  if (isAppReady(app)) void runCommand(app.command!);
  else useApp.getState().openApp(app.id);
  return true;
}
