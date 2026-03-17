using Prism.Features.Workspaces.Application.Dtos;
using Prism.Features.Workspaces.Domain;

namespace Prism.Tests.Unit.Workspaces;

public sealed class WorkspaceTests
{
    [Fact]
    public void Workspace_DefaultValues_AreCorrect()
    {
        var workspace = new Workspace();

        workspace.Name.Should().Be("");
        workspace.IsDefault.Should().BeFalse();
        workspace.Description.Should().BeNull();
        workspace.IconColor.Should().BeNull();
        workspace.Id.Should().NotBeEmpty();
        workspace.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        workspace.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WorkspaceDto_FromEntity_MapsCorrectly()
    {
        var entity = new Workspace
        {
            Name = "Research Lab",
            Description = "My main workspace",
            IsDefault = true,
            IconColor = "#FF5733"
        };

        WorkspaceDto dto = WorkspaceDto.FromEntity(entity);

        dto.Id.Should().Be(entity.Id);
        dto.Name.Should().Be("Research Lab");
        dto.Description.Should().Be("My main workspace");
        dto.IsDefault.Should().BeTrue();
        dto.IconColor.Should().Be("#FF5733");
        dto.CreatedAt.Should().Be(entity.CreatedAt);
        dto.UpdatedAt.Should().Be(entity.UpdatedAt);
    }
}
