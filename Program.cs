using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

string text = File.ReadAllText(args[0]);

JsonNode j = JsonNode.Parse(text)!.AsObject();

JsonObject targets = j["targets"]!.AsObject();

foreach ((string tf, JsonNode? packages) in targets)
{
	ConcurrentDictionary<Package, Refs> allKnownPackagesForTF = new();

	allKnownPackagesForTF.TryAdd(Package.ThisProject, new());

	foreach ((string name, JsonNode? details) in packages!.AsObject())
	{
		Package currentPackage = Package.FromSlashName(name);

		Refs refs = allKnownPackagesForTF.GetOrAdd(currentPackage, (_) => new Refs());

		refs.ReferencesMe.Add(Package.ThisProject);

		JsonObject? dependencies = details!.AsObject()["dependencies"]?.AsObject();

		if (dependencies is not null)
		{
			foreach ((string dependencyName, JsonNode? dependencyVersion) in dependencies)
			{
                Package dependency = new Package(dependencyName, dependencyVersion!.ToString());

				refs.DependsOn.Add(dependency);

				allKnownPackagesForTF.GetOrAdd(dependency, (_) => new Refs()).ReferencesMe.Add(currentPackage);
			}
		}
	}

	foreach ((Package r, Refs deps) in allKnownPackagesForTF)
	{
		if (r == new Package("Microsoft.Extensions.Logging.Abstractions", "2.2.0"))
		{
			Stack<StackEntry> stack = new();

			List<ImmutableList<Package>> routes = new();

			stack.Push(new(r, IndentLevel: 0, ImmutableList<Package>.Empty.Add(r)));

			while (stack.TryPop(out StackEntry? current))
			{
				//Console.WriteLine($"{new string(' ', current.IndentLevel)}{current.Package}");
				foreach (var item in allKnownPackagesForTF[current.Package].ReferencesMe)
				{
					if (item == Package.ThisProject)
					{
						routes.Add(current.DependencyChain.Reverse());
                    }
					else
					{
						stack.Push(new(item, current.IndentLevel + 1, current.DependencyChain.Add(item)));
					}
				}
            }

			foreach (var route in routes.OrderBy((l)=>l.Count))
			{
                Console.WriteLine($"Project->{string.Join("->", route.Select(p => p.ToString()))}");

            }

        }
    }
}

Console.WriteLine("done");

record Package(string Name, string Version)
{
	public static Package ThisProject = new("THE PROJECT", "CURRENT PROJECT VERSION");

	public static Package FromSlashName(string slashName)
	{
        string[] parts = slashName.Split('/');

		Debug.Assert(parts.Length == 2);

		return new Package(parts[0]!, parts[1]!);
	}

    public override string ToString()
    {
        return $"{Name}/{Version}";
    }
}

record class StackEntry(Package Package, int IndentLevel, ImmutableList<Package> DependencyChain);

class Refs
{
	public readonly List<Package> DependsOn = new List<Package>();
    public readonly HashSet<Package> ReferencesMe = new HashSet<Package>();
}