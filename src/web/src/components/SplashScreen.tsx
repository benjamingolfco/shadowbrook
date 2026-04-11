export default function SplashScreen() {
  return (
    <div className="fixed inset-0 flex flex-col items-center justify-center bg-background">
      <h1 className="font-display text-4xl tracking-tight text-ink mb-8">Teeforce</h1>
      <div className="flex gap-1.5">
        <span className="size-2 rounded-full bg-green animate-[pulse-dot_1.4s_ease-in-out_infinite]" />
        <span className="size-2 rounded-full bg-green animate-[pulse-dot_1.4s_ease-in-out_0.2s_infinite]" />
        <span className="size-2 rounded-full bg-green animate-[pulse-dot_1.4s_ease-in-out_0.4s_infinite]" />
      </div>
    </div>
  );
}
