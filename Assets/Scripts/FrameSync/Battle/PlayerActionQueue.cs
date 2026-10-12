using System.Collections.Generic;

namespace ZZZ
{
    // Raw local intent only. Logical combo eligibility is evaluated after server echo.
    public sealed class PlayerActionBuffer
    {
        readonly Queue<PlayerActionCommand> commands = new Queue<PlayerActionCommand>();
        public int Count => commands.Count;
        public PlayerActionCommand Dequeue() => commands.Dequeue();
        public void Clear() => commands.Clear();
        public void Enqueue(PlayerActionCommand command) => commands.Enqueue(command);
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
