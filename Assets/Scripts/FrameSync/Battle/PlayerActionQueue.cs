using System.Collections.Generic;

namespace ZZZ
{
    public enum ActionPreparation { Discard, Wait, Ready }
    public delegate ActionPreparation PreparePlayerAction(PlayerActionCommand command, out int payload);
    // Local, not-yet-sent inputs. Kept separate from the reliable outbound queue.
    public sealed class PlayerActionBuffer
    {
        private readonly Queue<PlayerActionCommand> commands = new Queue<PlayerActionCommand>();
        private PlayerActionCommand? preparedAttack;
        public int Count => commands.Count;
        public PlayerActionCommand Peek() => commands.Peek();
        public PlayerActionCommand Dequeue()
        {
            preparedAttack = null;
            return commands.Dequeue();
        }
        public void Clear()
        {
            preparedAttack = null;
            commands.Clear();
        }
        public bool TryPrepareHead(PreparePlayerAction prepare, bool canAttack, bool attackAllowed,
            out PlayerActionCommand command)
        {
            command = default;
            if (commands.Count == 0) return false;
            if (preparedAttack.HasValue)
            {
                if (!attackAllowed) { Dequeue(); return false; }
                if (!canAttack) return false;
                command = preparedAttack.Value;
                return true;
            }
            var head = commands.Peek();
            var result = prepare(head, out int payload);
            if (result == ActionPreparation.Discard) { Dequeue(); return false; }
            command = new PlayerActionCommand(head.Id, head.Type, head.Value1, payload);
            if (result == ActionPreparation.Wait)
            {
                // Wait means input was accepted; payload already contains the chosen step.
                preparedAttack = command;
                return false;
            }
            return true;
        }
        public void Enqueue(PlayerActionCommand command)
        {
            if (command.Type == 4) // rop4: dodge cancels only unsent attack pre-input.
            {
                preparedAttack = null;
                int count = commands.Count;
                for (int i = 0; i < count; i++)
                {
                    var pending = commands.Dequeue();
                    if (pending.Type != 1 && pending.Type != 6) commands.Enqueue(pending);
                }
            }
            commands.Enqueue(command);
        }
    }

    public readonly struct PlayerActionCommand
    {
        public readonly int Id;
        public readonly int Type;
        public readonly int Value1;
        public readonly int Value2;

        public PlayerActionCommand(int id, int type, int value1, int value2)
        {
            Id = id; Type = type; Value1 = value1; Value2 = value2;
        }
    }

    // One operation is retransmitted until its exact sequence ID is echoed.
    public sealed class PlayerActionQueue
    {
        private readonly Queue<PlayerActionCommand> commands = new Queue<PlayerActionCommand>();
        private int nextId = 1;
        public int Count => commands.Count;

        public void Enqueue(int type, int value1, int value2)
        {
            commands.Enqueue(new PlayerActionCommand(nextId++, type, value1, value2));
        }

        public bool TryPeek(out PlayerActionCommand command)
        {
            command = commands.Count > 0 ? commands.Peek() : default;
            return commands.Count > 0;
        }

        public bool Acknowledge(int id)
        {
            if (commands.Count == 0 || commands.Peek().Id != id) return false;
            commands.Dequeue();
            return true;
        }

        public void Clear()
        {
            commands.Clear();
            nextId = 1;
        }
    }
}
