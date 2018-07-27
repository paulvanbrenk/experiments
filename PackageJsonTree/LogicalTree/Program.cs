using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LogicalTree
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Write("Expected path to folder containing package.json and package-lock.json");
                return;
            }

            var packageJson = ReadJsonFile(Path.Combine(args[0], "package.json"));
            var packageLock = ReadJsonFile(Path.Combine(args[0], "package-lock.json"));

            var tree = MakeTree(packageJson, packageLock);
        }

        public static Node MakeTree(JObject pkg, JObject pkgLock)
        {
            var tree = new Node(pkg["name"].ToString(), null, pkg);

            var dependencies = pkg["dependencies"] ?? new JObject();
            var optional = pkg["optionalDependencies"] ?? new JObject();
            var dev = pkg["devDependencies"] ?? new JObject();

            var names = new HashSet<string>(dependencies.Children().Select(c => ((JProperty)c).Name));
            names.UnionWith(optional.Children().Select(c => ((JProperty)c).Name));
            names.UnionWith(dev.Children().Select(c => ((JProperty)c).Name));

            var allDeps = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (!allDeps.TryGetValue(name, out var dep))
                {
                    var depNode = (JObject)(pkgLock["dependencies"] ?? new JObject())[name];
                    dep = new Node(name, name, depNode);
                }
                AddChild(dep, tree, allDeps, pkgLock);
            }

            return tree;
        }

        private static void AddChild(Node dep, Node tree, Dictionary<string, Node> allDeps, JObject pkgLock)
        {
            tree.AddDep(dep);
            if (allDeps.ContainsKey(dep.Address))
            {
                Debug.Assert(allDeps[dep.Address] == dep);
            }

            allDeps[dep.Address] = dep;

            var addr = dep.Address;
            var lockNode = atAddr(pkgLock, addr);
            var names = (lockNode["requires"] ?? new JObject()).Children().Select(c => ((JProperty)c).Name);
            foreach (var name in names)
            {
                var tdepAddr = reqAddr(pkgLock, name, addr);
                if (!allDeps.TryGetValue(tdepAddr, out var tdep))
                {
                    tdep = new Node(name, tdepAddr, atAddr(pkgLock, tdepAddr));
                    AddChild(tdep, dep, allDeps, pkgLock);
                }
                else
                {
                    dep.AddDep(tdep);
                }
            }
        }

        private static string reqAddr(JObject pkgLock, string name, string fromAddr)
        {
            var lockNode = atAddr(pkgLock, fromAddr);
            var child = (lockNode["dependencies"] ?? new JObject())[name];
            if (child != null)
            {
                return $"{fromAddr}:{name}";
            }
            else
            {
                var parts = fromAddr;
                while (parts.Length > 0)
                {
                    var last = parts.LastIndexOf(':');
                    parts = last > 0 ? parts.Substring(0, last) : "";
                    var parent = atAddr(pkgLock, parts);
                    if (parent != null)
                    {
                        child = (parent["dependencies"] ?? new JObject())[name];
                        if (child != null)
                        {
                            return $"{parts}{((parts.Length > 0) ? ":" : "")}{name}";
                        }
                    }

                }
                throw new InvalidOperationException($"{name} is not reachable from ${fromAddr}");
            }
        }

        private static JObject atAddr(JObject pkgLock, string addr)
        {
            if (string.IsNullOrEmpty(addr)) { return pkgLock; }

            var parts = addr.Split(':');
            var current = pkgLock;
            foreach (var part in parts)
            {
                var cdd = (JObject)current?["dependencies"]?[part];
                if (cdd != null)
                {
                    current = cdd;
                }
            }

            return current;
        }

        private static JObject ReadJsonFile(string fullPathToFile)
        {
            using (var fin = new FileStream(fullPathToFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fin))
            {
                var text = reader.ReadToEnd();
                return JObject.Parse(text);
            }
        }
    }
}
