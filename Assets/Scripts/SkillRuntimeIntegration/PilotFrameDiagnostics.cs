using System;
using System.Collections.Generic;
using GameProtocol;

namespace SkillRuntime.Integration
{
    // Optional observer owned by tooling. Never supplies values to simulation.
    // Implementations must bound storage. Session isolates observer exceptions from combat.
    public interface IPilotFrameDiagnostics
    {
        void Begin(long frame);
        void Committed(long frame, AllPlayerOperation input, PilotBattleSession session);
        void End(long frame);
    }
}
