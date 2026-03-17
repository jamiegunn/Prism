using Prism.Common.Inference.Models;
using Prism.Features.PromptLab.Application.Rendering;
using Prism.Features.PromptLab.Domain;

namespace Prism.Tests.Unit.PromptLab;

public sealed class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Fact]
    public void Render_SubstitutesVariables()
    {
        var version = new PromptVersion
        {
            UserTemplate = "Hello {{name}}",
            Variables =
            [
                new PromptVariable { Name = "name", Required = true }
            ]
        };
        var values = new Dictionary<string, string> { ["name"] = "World" };

        var result = _renderer.Render(version, values);

        result.IsSuccess.Should().BeTrue();
        result.Value.RenderedUserPrompt.Should().Be("Hello World");
    }

    [Fact]
    public void Render_HandlesMultipleVariables()
    {
        var version = new PromptVersion
        {
            UserTemplate = "{{greeting}}, {{name}}! Welcome to {{place}}.",
            Variables =
            [
                new PromptVariable { Name = "greeting", Required = true },
                new PromptVariable { Name = "name", Required = true },
                new PromptVariable { Name = "place", Required = true }
            ]
        };
        var values = new Dictionary<string, string>
        {
            ["greeting"] = "Hi",
            ["name"] = "Alice",
            ["place"] = "Prism"
        };

        var result = _renderer.Render(version, values);

        result.IsSuccess.Should().BeTrue();
        result.Value.RenderedUserPrompt.Should().Be("Hi, Alice! Welcome to Prism.");
    }

    [Fact]
    public void Render_UsesDefaultValues()
    {
        var version = new PromptVersion
        {
            UserTemplate = "Hello {{name}}, your role is {{role}}.",
            Variables =
            [
                new PromptVariable { Name = "name", Required = true },
                new PromptVariable { Name = "role", Required = false, DefaultValue = "researcher" }
            ]
        };
        var values = new Dictionary<string, string> { ["name"] = "Bob" };

        var result = _renderer.Render(version, values);

        result.IsSuccess.Should().BeTrue();
        result.Value.RenderedUserPrompt.Should().Be("Hello Bob, your role is researcher.");
    }

    [Fact]
    public void Render_ValidatesRequiredVariables()
    {
        var version = new PromptVersion
        {
            UserTemplate = "Hello {{name}}, your age is {{age}}.",
            Variables =
            [
                new PromptVariable { Name = "name", Required = true },
                new PromptVariable { Name = "age", Required = true }
            ]
        };
        var values = new Dictionary<string, string>(); // no values provided

        var result = _renderer.Render(version, values);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("name");
        result.Error.Message.Should().Contain("age");
    }

    [Fact]
    public void Render_IncludesFewShotExamples()
    {
        var version = new PromptVersion
        {
            UserTemplate = "Translate: {{text}}",
            Variables =
            [
                new PromptVariable { Name = "text", Required = true }
            ],
            FewShotExamples =
            [
                new FewShotExample { Input = "Hello", Output = "Hola" },
                new FewShotExample { Input = "Goodbye", Output = "Adios" }
            ]
        };
        var values = new Dictionary<string, string> { ["text"] = "Thank you" };

        var result = _renderer.Render(version, values);

        result.IsSuccess.Should().BeTrue();
        List<ChatMessage> messages = result.Value.Messages;

        // 2 few-shot pairs (4 messages) + 1 final user message = 5
        messages.Should().HaveCount(5);
        messages[0].Role.Should().Be(ChatMessage.UserRole);
        messages[0].Content.Should().Be("Hello");
        messages[1].Role.Should().Be(ChatMessage.AssistantRole);
        messages[1].Content.Should().Be("Hola");
        messages[2].Role.Should().Be(ChatMessage.UserRole);
        messages[2].Content.Should().Be("Goodbye");
        messages[3].Role.Should().Be(ChatMessage.AssistantRole);
        messages[3].Content.Should().Be("Adios");
        messages[4].Role.Should().Be(ChatMessage.UserRole);
        messages[4].Content.Should().Be("Translate: Thank you");
    }

    [Fact]
    public void Render_IncludesSystemPrompt()
    {
        var version = new PromptVersion
        {
            SystemPrompt = "You are a helpful assistant.",
            UserTemplate = "Hello {{name}}",
            Variables =
            [
                new PromptVariable { Name = "name", Required = true }
            ]
        };
        var values = new Dictionary<string, string> { ["name"] = "World" };

        var result = _renderer.Render(version, values);

        result.IsSuccess.Should().BeTrue();
        List<ChatMessage> messages = result.Value.Messages;
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatMessage.SystemRole);
        messages[0].Content.Should().Be("You are a helpful assistant.");
        messages[1].Role.Should().Be(ChatMessage.UserRole);
        messages[1].Content.Should().Be("Hello World");
    }
}
