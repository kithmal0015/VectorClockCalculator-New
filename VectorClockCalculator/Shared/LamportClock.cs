using System;

namespace Shared
{
    public class LamportClock
    {
        private int _time;
        private readonly string _nodeId;
        private readonly object _lock = new object();

        public LamportClock(string nodeId)
        {
            _nodeId = nodeId;
            _time = 0;
        }

        public int Increment()
        {
            lock (_lock)
            {
                _time++;
                Console.WriteLine($"[{_nodeId}] Lamport clock: {_time}");
                return _time;
            }
        }

        public int Update(int receivedTime)
        {
            lock (_lock)
            {
                int oldTime = _time;
                _time = Math.Max(_time, receivedTime) + 1;
                Console.WriteLine($"[{_nodeId}] Lamport clock updated: {oldTime} → {_time} (received: {receivedTime})");
                return _time;
            }
        }

        public int GetTime()
        {
            lock (_lock)
            {
                return _time;
            }
        }

        public static bool HappenedBefore(int time1, int time2)
        {
            return time1 < time2;
        }

        public override string ToString()
        {
            return $"[T={_time}]";
        }
    }
}