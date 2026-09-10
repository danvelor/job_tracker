'use client';

import { useCallback, useRef } from 'react';

export function SignaturePad({
  testId,
  value,
  onChange,
}: {
  readonly testId: string;
  readonly value: string;
  readonly onChange: (dataUrl: string) => void;
}) {
  const canvas = useRef<HTMLCanvasElement>(null);

  const capture = useCallback(() => {
    const element = canvas.current;
    if (element === null) return;
    onChange(element.toDataURL('image/png'));
  }, [onChange]);

  return (
    <div>
      <canvas
        ref={canvas}
        width={320}
        height={96}
        onPointerUp={capture}
        aria-hidden="true"
        className="rounded border border-slate-300"
      />
      <input
        data-testid={testId}
        aria-label="Customer signature"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Signature"
        className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5 text-sm"
      />
    </div>
  );
}
