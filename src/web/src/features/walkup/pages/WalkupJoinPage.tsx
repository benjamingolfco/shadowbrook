import { useState } from 'react';
import CodeEntry from '../components/CodeEntry';
import JoinForm from '../components/JoinForm';
import Confirmation from '../components/Confirmation';
import type { VerifyCodeResponse, JoinWaitlistResponse } from '@/types/waitlist';

type Phase = 'code' | 'join' | 'confirmation';

export default function WalkupJoinPage() {
  const [phase, setPhase] = useState<Phase>('code');
  const [verifyData, setVerifyData] = useState<VerifyCodeResponse | null>(null);
  const [joinResult, setJoinResult] = useState<JoinWaitlistResponse | null>(null);

  function handleVerified(data: VerifyCodeResponse) {
    setVerifyData(data);
    setPhase('join');
  }

  function handleJoined(result: JoinWaitlistResponse) {
    setJoinResult(result);
    setPhase('confirmation');
  }

  return (
    <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm">
        <h1 className="text-xl font-bold text-center mb-8">Shadowbrook</h1>

        {phase === 'code' && <CodeEntry onVerified={handleVerified} />}
        {phase === 'join' && verifyData && (
          <JoinForm verifyData={verifyData} onJoined={handleJoined} />
        )}
        {phase === 'confirmation' && joinResult && (
          <Confirmation result={joinResult} />
        )}
      </div>
    </div>
  );
}
