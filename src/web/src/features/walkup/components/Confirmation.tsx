interface ConfirmationProps {
  firstName: string;
  position: number;
  isExisting: boolean;
}

function CheckCircleIcon() {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="size-16 text-green-500"
      aria-hidden="true"
    >
      <circle cx={12} cy={12} r={10} />
      <path d="m9 12 2 2 4-4" />
    </svg>
  );
}

export default function Confirmation({ firstName, position, isExisting }: ConfirmationProps) {
  return (
    <div className="w-full max-w-xs text-center">
      <div className="flex justify-center mb-4">
        <CheckCircleIcon />
      </div>

      <h2 className="text-2xl font-bold text-gray-900 mb-2">
        You&apos;re on the list, {firstName}!
      </h2>

      {isExisting && (
        <p className="text-sm text-gray-500 mb-3">You were already on the list.</p>
      )}

      <p className="text-lg font-semibold text-gray-800 mb-4">
        Your position: #{position}
      </p>

      <p className="text-sm text-gray-500">
        Keep your phone handy -- we&apos;ll text you when a tee time opens up.
      </p>
    </div>
  );
}
