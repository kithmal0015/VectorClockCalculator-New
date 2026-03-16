using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    /// <summary>
    /// Matrix Clock tracks not just what this node knows (vector clock),
    /// but also what this node knows about what OTHER nodes know.
    /// Matrix[i][j] = what node i knows about node j's clock
    /// </summary>
    public class MatrixClock
    {
        private readonly string _nodeId;
        private readonly Dictionary<string, Dictionary<string, int>> _matrix;
        private readonly List<string> _allNodes;
        private readonly object _lock = new object();

        public MatrixClock(string nodeId, List<string> allNodes)
        {
            _nodeId = nodeId;
            _allNodes = new List<string>(allNodes);
            _matrix = new Dictionary<string, Dictionary<string, int>>();

            // Initialize matrix
            foreach (var node in _allNodes)
            {
                _matrix[node] = new Dictionary<string, int>();
                foreach (var otherNode in _allNodes)
                {
                    _matrix[node][otherNode] = 0;
                }
            }
        }

        /// <summary>
        /// Increment this node's knowledge about itself
        /// </summary>
        public void Increment()
        {
            lock (_lock)
            {
                _matrix[_nodeId][_nodeId]++;
                Console.WriteLine($"[{_nodeId}] Matrix clock incremented");
                Console.WriteLine($"[{_nodeId}] My row: {GetMyRow()}");
            }
        }

        /// <summary>
        /// Merge incoming matrix from another node
        /// </summary>
        public void Merge(Dictionary<string, Dictionary<string, int>> incomingMatrix)
        {
            lock (_lock)
            {
                Console.WriteLine($"[{_nodeId}] Merging matrix clock...");
                
                foreach (var row in incomingMatrix)
                {
                    string nodeI = row.Key;
                    
                    if (!_matrix.ContainsKey(nodeI))
                        _matrix[nodeI] = new Dictionary<string, int>();

                    foreach (var col in row.Value)
                    {
                        string nodeJ = col.Key;
                        int incomingValue = col.Value;

                        if (!_matrix[nodeI].ContainsKey(nodeJ))
                        {
                            _matrix[nodeI][nodeJ] = incomingValue;
                        }
                        else
                        {
                            _matrix[nodeI][nodeJ] = Math.Max(_matrix[nodeI][nodeJ], incomingValue);
                        }
                    }
                }

                Console.WriteLine($"[{_nodeId}] Matrix merged. My row: {GetMyRow()}");
            }
        }

        /// <summary>
        /// Get the full matrix
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> GetMatrix()
        {
            lock (_lock)
            {
                var copy = new Dictionary<string, Dictionary<string, int>>();
                foreach (var row in _matrix)
                {
                    copy[row.Key] = new Dictionary<string, int>(row.Value);
                }
                return copy;
            }
        }

        /// <summary>
        /// Get this node's row (what this node knows about all nodes)
        /// </summary>
        public Dictionary<string, int> GetMyRow()
        {
            lock (_lock)
            {
                return new Dictionary<string, int>(_matrix[_nodeId]);
            }
        }

        /// <summary>
        /// Check if this node knows that node A has seen all of node B's events
        /// </summary>
        public bool KnowsThatNodeASawNodeB(string nodeA, string nodeB)
        {
            lock (_lock)
            {
                if (!_matrix.ContainsKey(nodeA) || !_matrix.ContainsKey(nodeB))
                    return false;

                if (!_matrix[nodeA].ContainsKey(nodeB) || !_matrix[nodeB].ContainsKey(nodeB))
                    return false;

                return _matrix[nodeA][nodeB] >= _matrix[nodeB][nodeB];
            }
        }

        /// <summary>
        /// Get a visual representation of the matrix
        /// </summary>
        public string ToMatrixString()
        {
            lock (_lock)
            {
                var result = $"\n[{_nodeId}] Matrix Clock:\n";
                result += "     ";
                
                // Header row
                foreach (var node in _allNodes)
                {
                    result += $"{node,8} ";
                }
                result += "\n";

                // Matrix rows
                foreach (var nodeI in _allNodes)
                {
                    result += $"{nodeI,4} ";
                    foreach (var nodeJ in _allNodes)
                    {
                        int value = _matrix.ContainsKey(nodeI) && _matrix[nodeI].ContainsKey(nodeJ) 
                            ? _matrix[nodeI][nodeJ] 
                            : 0;
                        result += $"{value,8} ";
                    }
                    result += "\n";
                }

                return result;
            }
        }

        /// <summary>
        /// Detect if this node can determine global causality
        /// </summary>
        public bool CanDetermineGlobalCausality()
        {
            lock (_lock)
            {
                // Check if this node knows about all other nodes
                foreach (var node in _allNodes)
                {
                    if (node == _nodeId) continue;
                    
                    if (!_matrix[_nodeId].ContainsKey(node) || _matrix[_nodeId][node] == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override string ToString()
        {
            var myRow = GetMyRow();
            var entries = myRow.OrderBy(kvp => kvp.Key)
                              .Select(kvp => $"{kvp.Key}:{kvp.Value}");
            return $"{{{string.Join(", ", entries)}}}";
        }
    }
}