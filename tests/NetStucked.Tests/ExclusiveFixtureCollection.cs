using Xunit;

namespace NetStucked.Tests;

// Bulk XML/ZIP allocations and scheduler-sensitive network fixtures must not compete
// with one another or the parallel suite. Keep their production timing assertions intact.
[CollectionDefinition("Exclusive timing and bulk fixtures", DisableParallelization = true)]
public sealed class ExclusiveFixtureCollection;
