using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared
{
    public class TwoPhaseCommit
    {
        public enum Phase
        {
            Prepare,
            Commit,
            Abort
        }

        public class Transaction
        {
            public string TransactionId { get; set; } = Guid.NewGuid().ToString();
            public Phase CurrentPhase { get; set; } = Phase.Prepare;
            public Dictionary<string, bool> ParticipantVotes { get; set; } = new();
            public DateTime StartTime { get; set; } = DateTime.Now;
        }

        private static readonly Dictionary<string, Transaction> _activeTransactions = new();
        private static readonly object _lock = new object();

        public static Transaction StartTransaction(List<string> participants)
        {
            lock (_lock)
            {
                var transaction = new Transaction();
                
                foreach (var participant in participants)
                {
                    transaction.ParticipantVotes[participant] = false;
                }

                _activeTransactions[transaction.TransactionId] = transaction;
                
                Console.WriteLine($" 2PC: Transaction {transaction.TransactionId} started");
                Console.WriteLine($" Participants: {string.Join(", ", participants)}");
                
                return transaction;
            }
        }

        public static async Task<bool> Prepare(string transactionId)
        {
            lock (_lock)
            {
                if (!_activeTransactions.ContainsKey(transactionId))
                {
                    Console.WriteLine($"❌ 2PC: Transaction {transactionId} not found");
                    return false;
                }

                var transaction = _activeTransactions[transactionId];
                transaction.CurrentPhase = Phase.Prepare;
                
                Console.WriteLine($"\n 2PC Phase 1: PREPARE");
                Console.WriteLine($"Transaction: {transactionId}");
            }

            await Task.Delay(500);
            
            lock (_lock)
            {
                var transaction = _activeTransactions[transactionId];
                bool allVotedYes = true;

                var keys = transaction.ParticipantVotes.Keys.ToList();
                foreach (var participant in keys)
                {
                    bool vote = !NetworkPartition.IsPartitioned(participant) && new Random().Next(10) > 1;
                    transaction.ParticipantVotes[participant] = vote;
                    
                    string voteStr = vote ? "✅ YES" : "❌ NO";
                    Console.WriteLine($"  {participant}: {voteStr}");
                    
                    if (!vote) allVotedYes = false;
                }

                Console.WriteLine($"\n{new string('═', 20)} PREPARE Result: {(allVotedYes ? "✅ ALL YES" : "❌ AT LEAST ONE NO")} {new string('═', 20)}");
                return allVotedYes;
            }
        }

        public static async Task Commit(string transactionId)
        {
            lock (_lock)
            {
                if (!_activeTransactions.ContainsKey(transactionId))
                {
                    Console.WriteLine($"❌ 2PC: Transaction {transactionId} not found");
                    return;
                }

                var transaction = _activeTransactions[transactionId];
                transaction.CurrentPhase = Phase.Commit;
                
                Console.WriteLine($"\n 2PC Phase 2: COMMIT");
                Console.WriteLine($"Transaction: {transactionId}");
            }

            await Task.Delay(500);

            lock (_lock)
            {
                var transaction = _activeTransactions[transactionId];
                
                foreach (var participant in transaction.ParticipantVotes.Keys)
                {
                    Console.WriteLine($"  {participant}: ✅ COMMITTED");
                }

                _activeTransactions.Remove(transactionId);
                Console.WriteLine($"\n{new string('═', 20)} ✅ TRANSACTION COMMITTED {new string('═', 20)}\n");
            }
        }

        public static async Task Abort(string transactionId)
        {
            lock (_lock)
            {
                if (!_activeTransactions.ContainsKey(transactionId))
                {
                    Console.WriteLine($"❌ 2PC: Transaction {transactionId} not found");
                    return;
                }

                var transaction = _activeTransactions[transactionId];
                transaction.CurrentPhase = Phase.Abort;
                
                Console.WriteLine($"\n 2PC Phase 2: ABORT");
                Console.WriteLine($"Transaction: {transactionId}");
            }

            await Task.Delay(500);

            lock (_lock)
            {
                var transaction = _activeTransactions[transactionId];
                
                foreach (var participant in transaction.ParticipantVotes.Keys)
                {
                    Console.WriteLine($"  {participant}:  ROLLED BACK");
                }

                _activeTransactions.Remove(transactionId);
                Console.WriteLine($"\n{new string('═', 20)}  TRANSACTION ABORTED {new string('═', 20)}\n");
            }
        }

        public static async Task<bool> ExecuteTransaction(List<string> participants, Func<Task> operation)
        {
            var transaction = StartTransaction(participants);

            try
            {
                bool prepareSuccess = await Prepare(transaction.TransactionId);

                if (prepareSuccess)
                {
                    await operation();
                    await Commit(transaction.TransactionId);
                    return true;
                }
                else
                {
                    await Abort(transaction.TransactionId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 2PC: Transaction failed: {ex.Message}");
                await Abort(transaction.TransactionId);
                return false;
            }
        }
    }
}