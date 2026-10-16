/** 명령창 입력칸에 커서를 보내는 창구 (키보드 분배·옛 화면이 쓴다). 입력칸이 그려질 때 등록된다. */
let input: HTMLInputElement | null = null;
let wanted = false;

export function registerCmdline(el: HTMLInputElement | null) {
  input = el;
  if (el && wanted) focusCmdline();
}

/** 입력칸으로 커서를 옮기고 끝에 둔다. 아직 숨겨져 있으면 보이는 순간 옮긴다. */
export function focusCmdline() {
  if (!input || input.offsetParent === null) {
    wanted = true;
    return;
  }
  wanted = false;
  input.focus();
  const end = input.value.length;
  input.setSelectionRange(end, end);
}
