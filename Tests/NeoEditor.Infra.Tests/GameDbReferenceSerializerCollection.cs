using Xunit;

namespace NeoEditor.Infra.Tests;

/// <summary>
/// Test classes that mutate the process-wide static <c>GameDbContext.ReferenceSerializer</c>
/// must not run in parallel with each other (or anything else in this assembly). Joining this
/// collection serializes them.
/// </summary>
[CollectionDefinition("GameDbReferenceSerializer", DisableParallelization = true)]
public class GameDbReferenceSerializerCollection;
