using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LogicalTree
{
    internal class Node
    {
        public string Name;
        public string Address;

        public JToken Version;

        private readonly JObject raw;

        public readonly Dictionary<string, Node> dependencies = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<Node> requiredBy = new HashSet<Node>();

        public Node(string name, string address, JObject raw)
        {
            this.Name = name;
            this.Version = raw["version"];
            this.Address = address ?? "";
            this.raw = raw;
        }

        private bool isRoot => this.requiredBy.Count == 0;

        public void AddDep(Node node) {
            this.dependencies[node.Name] = node;
            node.requiredBy.Add(this);
        }
    }
}
