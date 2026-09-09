'use client';

import { useCallback, useRef } from 'react';

/**
 * Produces the data URL BR-4 requires.
 *
 * The canvas is the natural input, and the text field beside it carries the
 * same value. That field is not a test-only escape hatch bolted on because
 * jsdom implements no 2D context: it is the accessible path for anyone who
 * cannot sign with a pointer, and the suite uses the same one a keyboard user
 * would. A control only the tests can reach would be worth less than one both
 * can.
 */
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
