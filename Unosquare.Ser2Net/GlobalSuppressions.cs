// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.


[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via dependency injection.", Scope = "namespaceanddescendants", Target = "~N:Unosquare.Ser2Net.Workers")]
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via dependency injection.", Scope = "namespaceanddescendants", Target = "~N:Unosquare.Ser2Net.Services")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Generic exception is intentionally expected.", Scope = "module")]
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The MemoryQueue class is a findamental type that correctly expresses its intent.", Scope = "type", Target = "~T:Unosquare.Ser2Net.Memory.MemoryQueue`1")]
[assembly: SuppressMessage("Performance", "CA1836:Prefer IsEmpty over Count", Justification = "In this particular instance, the constant is build-time configurable.", Scope = "member", Target = "~M:Unosquare.Ser2Net.Workers.NetServer.ExecuteAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Platform compatibility is in fact, checked.", Scope = "member", Target = "~M:Unosquare.Ser2Net.BuilderExtensions.ConfigureLifetimeAndLogging``1(``0)~``0")]
