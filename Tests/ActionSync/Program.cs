using ZZZ;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    passed++;
}

var queue = new PlayerActionQueue();
Check(!queue.TryPeek(out _), "A new battle has no pending action");
queue.Enqueue(1, 42, 9);
queue.Enqueue(4, 121, 0);
queue.Enqueue(1, 90, 2);
Check(queue.Count == 3, "Rapid attack/dodge/attack inputs must not overwrite one another");
Check(queue.TryPeek(out var first) && first.Type == 1 && first.Value1 == 42 && first.Value2 == 9,
    "First action retains its captured arguments");
Check(queue.TryPeek(out var retry) && retry.Id == first.Id, "Retries retain the same operation ID");
Check(!queue.Acknowledge(first.Id + 1) && queue.Count == 3, "A future acknowledgement cannot discard unsent input");
Check(queue.Acknowledge(first.Id), "An exact echo consumes the head");
Check(queue.TryPeek(out var second) && second.Type == 4 && second.Id > first.Id,
    "FIFO preserves action ordering and monotonic IDs");
Check(!queue.Acknowledge(first.Id) && queue.Count == 2, "Duplicate echoes cannot consume the next action");
queue.Acknowledge(second.Id);
Check(queue.TryPeek(out var third) && third.Type == 1 && third.Value2 == 2, "Repeated same-type actions stay distinct");
queue.Clear();
Check(queue.Count == 0 && !queue.TryPeek(out _), "Battle reset removes all pending input");
queue.Enqueue(2, 0, 0);
Check(queue.TryPeek(out var reset) && reset.Id == 1, "New battle restarts sequence IDs");
var remoteQueue = new PlayerActionQueue();
Check(remoteQueue.Count == 0, "Independent players do not share pending actions");
var buffer = new PlayerActionBuffer();
buffer.Enqueue(new PlayerActionCommand(0, 1, 90, 0));
buffer.Enqueue(new PlayerActionCommand(0, 4, 60, 1));
Check(buffer.Count == 1 && buffer.Peek().Type == 4,
    "Dodge cancels an unsent buffered attack instead of waiting for its combo window");
buffer.Enqueue(new PlayerActionCommand(0, 1, 30, 0));
Check(buffer.Count == 2 && buffer.Dequeue().Type == 4 && buffer.Dequeue().Type == 1,
    "An attack entered after dodge is retained for a dodge combo");
buffer.Enqueue(new PlayerActionCommand(0, 5, 0, 0));
buffer.Enqueue(new PlayerActionCommand(0, 1, 0, 0));
buffer.Enqueue(new PlayerActionCommand(0, 4, 0, 0));
Check(buffer.Count == 2 && buffer.Dequeue().Type == 5 && buffer.Dequeue().Type == 4,
    "Dodge cancellation preserves other unsent actions in order");
var preInput = new PlayerActionBuffer();
int selectedPayload = 5; // Moving attack, second combo step.
ActionPreparation PrepareAttack(PlayerActionCommand command, out int payload)
{
    payload = selectedPayload;
    return ActionPreparation.Wait;
}
preInput.Enqueue(new PlayerActionCommand(0, 1, 42, 1));
Check(!preInput.TryPrepareHead(PrepareAttack, false, true, out _), "Pre-input waits for attack window");
selectedPayload = 1; // Animation's disconnect event resets selection to first step.
Check(!preInput.TryPrepareHead(PrepareAttack, false, true, out _),
    "Repeated polling does not release an attack before its window");
Check(preInput.TryPrepareHead(PrepareAttack, true, true, out var prepared)
    && prepared.Value2 == 5 && prepared.Value1 == 42,
    "Accepted second attack retains its step and facing after combo timeout");
preInput.Dequeue();
preInput.Enqueue(new PlayerActionCommand(0, 1, 60, 0));
Check(!preInput.TryPrepareHead(PrepareAttack, false, true, out _), "Next attack can wait independently");
Check(!preInput.TryPrepareHead(PrepareAttack, true, false, out _) && preInput.Count == 0,
    "Hit or skill interruption discards accepted unsent attack");
selectedPayload = 8;
preInput.Enqueue(new PlayerActionCommand(0, 1, 60, 0));
preInput.TryPrepareHead(PrepareAttack, false, true, out _);
preInput.Enqueue(new PlayerActionCommand(0, 4, 30, 0));
ActionPreparation PrepareReady(PlayerActionCommand command, out int payload)
{
    payload = command.Value2;
    return ActionPreparation.Ready;
}
Check(preInput.TryPrepareHead(PrepareReady, true, true, out var dodgeCommand)
    && dodgeCommand.Type == 4 && dodgeCommand.Value1 == 30 && dodgeCommand.Value2 == 0,
    "Dodge cancels the locked attack payload as well as its queued input");
preInput.Dequeue();
preInput.Enqueue(new PlayerActionCommand(0, 1, 60, 0));
preInput.TryPrepareHead(PrepareAttack, false, true, out _);
preInput.Clear();
preInput.Enqueue(new PlayerActionCommand(0, 1, 90, 0));
Check(preInput.TryPrepareHead(PrepareReady, true, true, out var newAttack) && newAttack.Value2 == 0,
    "Clearing input removes any locked combo selection");
var machine = new TestMachine();
var idle = new TestState();
machine.ChangeState(idle);
machine.ChangeState(idle);
Check(idle.Entered == 1 && idle.Exited == 0, "Repeated frames must not restart the current animation state");
var run = new TestState();
machine.ChangeState(run);
Check(idle.Exited == 1 && run.Entered == 1, "Real state changes exit and enter exactly once");
machine.currentState.Value = null;
Check(machine.currentState.Value == null, "Disabling an actor can clear its state without throwing");
var comboMachine = new PlayerComboStateMachine();
var completion = new ComboCompletionProbe(comboMachine);
var animator = comboMachine.Player.characterAnimator;
completion.Enter();
Check(!completion.Finished("ATK"), "An attack is not complete before its animation is observed");
animator.Current = new UnityEngine.AnimatorStateInfo { Tag = "ATK", Name = "Unagi_Normal_4" };
Check(!completion.Finished("ATK"), "A playing attack remains active");
animator.Transition = true;
animator.Next = new UnityEngine.AnimatorStateInfo { Name = "Attack_End" };
Check(!completion.Finished("ATK"),
    "Normal4 fading out must retain the combo state for its late pre-input and cooldown events");
animator.Current = animator.Next;
animator.Transition = false;
Check(completion.Finished("ATK"), "Combo completes once the outgoing attack actually exits");
completion.Enter();
animator.Transition = true;
animator.Next = new UnityEngine.AnimatorStateInfo { Tag = "ATK", Name = "Unagi_Normal_5" };
Check(!completion.Finished("ATK"), "An incoming next attack also keeps the combo alive");
animator.Current = animator.Next;
animator.Transition = false;
Check(!completion.Finished("ATK"), "Next combo step does not finish immediately after blending");
Console.WriteLine($"PASS: {passed} action sync assertions");

sealed class TestMachine : StateMachine { }
sealed class TestState : IState
{
    public int Entered, Exited;
    public void Enter() { Entered++; }
    public void Exit() { Exited++; }
    public void UpdateAnimationParameters() { }
    public void Update() { }
    public void OnAnimationTranslateEvent(IState state) { }
    public void OnAnimationExitEvent() { }
}
