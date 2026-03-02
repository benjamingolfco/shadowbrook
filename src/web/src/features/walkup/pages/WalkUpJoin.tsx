import { useState } from 'react';
import CodeEntry from '../components/CodeEntry';
import JoinForm from '../components/JoinForm';
import Confirmation from '../components/Confirmation';

type Phase =
  | { step: 'code' }
  | { step: 'join'; courseWaitlistId: string; courseName: string }
  | { step: 'confirmed'; firstName: string; position: number; isExisting: boolean };

export default function WalkUpJoin() {
  const [phase, setPhase] = useState<Phase>({ step: 'code' });

  function handleVerified(data: { courseWaitlistId: string; courseName: string }) {
    setPhase({ step: 'join', courseWaitlistId: data.courseWaitlistId, courseName: data.courseName });
  }

  function handleJoined(data: { firstName: string; position: number; isExisting: boolean }) {
    setPhase({ step: 'confirmed', firstName: data.firstName, position: data.position, isExisting: data.isExisting });
  }

  return (
    <div className="min-h-screen bg-white flex flex-col items-center px-4 pt-12">
      <h1 className="text-xl font-bold mb-8">Shadowbrook</h1>

      {phase.step === 'code' && (
        <CodeEntry onVerified={handleVerified} />
      )}

      {phase.step === 'join' && (
        <JoinForm
          courseWaitlistId={phase.courseWaitlistId}
          courseName={phase.courseName}
          onJoined={handleJoined}
        />
      )}

      {phase.step === 'confirmed' && (
        <Confirmation
          firstName={phase.firstName}
          position={phase.position}
          isExisting={phase.isExisting}
        />
      )}
    </div>
  );
}
