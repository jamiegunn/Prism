using Prism.Features.Rag.Domain;

namespace Prism.Tests.Unit.Rag;

public class RagTraceTests
{
    [Fact]
    public void RagTrace_DefaultValues_AreCorrect()
    {
        var trace = new RagTrace();

        trace.Query.Should().BeEmpty();
        trace.SearchType.Should().Be("Vector");
        trace.RetrievedChunkCount.Should().Be(0);
        trace.RetrievedChunksJson.Should().Be("[]");
        trace.AssembledContext.Should().BeEmpty();
        trace.GeneratedResponse.Should().BeNull();
        trace.IsSuccess.Should().BeFalse();
        trace.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void RagTrace_CanSetAllFields()
    {
        Guid collectionId = Guid.NewGuid();
        var trace = new RagTrace
        {
            CollectionId = collectionId,
            Query = "What is attention?",
            SearchType = "Hybrid",
            RetrievedChunkCount = 3,
            RetrievedChunksJson = "[{\"id\":\"abc\",\"score\":0.95}]",
            AssembledContext = "Context: The Transformer...",
            GeneratedResponse = "Attention is a mechanism...",
            Model = "llama-3.1-8b",
            LatencyMs = 1500,
            TotalTokens = 350,
            IsSuccess = true
        };

        trace.CollectionId.Should().Be(collectionId);
        trace.Query.Should().Be("What is attention?");
        trace.SearchType.Should().Be("Hybrid");
        trace.RetrievedChunkCount.Should().Be(3);
        trace.Model.Should().Be("llama-3.1-8b");
        trace.LatencyMs.Should().Be(1500);
        trace.IsSuccess.Should().BeTrue();
    }
}
