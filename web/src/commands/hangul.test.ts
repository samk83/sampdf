import { describe, expect, it } from 'vitest';
import { hangulToLatin } from './hangul';

describe('한글 상태로 친 명령 (두벌식)', () => {
  it('낱자와 합쳐진 글자', () => {
    expect(hangulToLatin('ㅣㅑㅜㄷ').toUpperCase()).toBe('LINE');
    expect(hangulToLatin('ㅎ갸ㅇ').toUpperCase()).toBe('GRID');
    expect(hangulToLatin('ㅊㅡㅔ').toUpperCase()).toBe('CMP');
    expect(hangulToLatin('혀ㅑㅇ')).toBe('guid');
  });
  it('영어·숫자·기호는 그대로', () => {
    expect(hangulToLatin('ZOOM E')).toBe('ZOOM E');
    expect(hangulToLatin('@500<90')).toBe('@500<90');
  });
});
