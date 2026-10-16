import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'default' | 'ghost'; icon?: ReactNode };

/** 버튼 (설계서 4-2): primary = 주요 동작 1개, default = 테두리, ghost = 글자만. 높이 --size-control. */
export function Button({ variant = 'default', icon, children, className = '', type = 'button', ...rest }: Props) {
  const look = {
    primary: 'bg-primary text-on-primary hover:brightness-110 disabled:bg-pressed disabled:text-fg-faint',
    default: 'border border-strong text-fg-secondary hover:bg-pressed hover:text-fg disabled:text-fg-faint',
    ghost: 'text-accent hover:underline disabled:text-fg-faint',
  }[variant];
  return (
    <button type={type} {...rest}
      className={`inline-flex h-control items-center justify-center gap-1.5 rounded-sm px-3 text-sm transition-colors duration-fast ease-standard disabled:cursor-not-allowed ${look} ${className}`}>
      {icon}
      {children}
    </button>
  );
}
