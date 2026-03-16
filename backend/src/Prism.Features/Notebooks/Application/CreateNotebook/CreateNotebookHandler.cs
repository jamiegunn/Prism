using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Notebooks.Application.Dtos;
using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Application.CreateNotebook;

/// <summary>
/// Command to create a new notebook.
/// </summary>
public sealed record CreateNotebookCommand(
    string Name,
    string? Description,
    string? Content);

/// <summary>
/// Handles creation of new notebooks with default .ipynb structure.
/// </summary>
public sealed class CreateNotebookHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateNotebookHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateNotebookHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateNotebookHandler(AppDbContext db, ILogger<CreateNotebookHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new notebook with default or provided content.
    /// </summary>
    /// <param name="command">The create notebook command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The created notebook summary DTO.</returns>
    public async Task<Result<NotebookSummaryDto>> HandleAsync(CreateNotebookCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Error.Validation("Notebook name is required.");

        string content = command.Content ?? GenerateDefaultNotebook(command.Name);

        var notebook = new Notebook
        {
            Name = command.Name,
            Description = command.Description,
            Content = content,
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            LastEditedAt = DateTime.UtcNow
        };

        _db.Set<Notebook>().Add(notebook);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created notebook {NotebookName} with ID {NotebookId}", notebook.Name, notebook.Id);

        return NotebookSummaryDto.FromEntity(notebook);
    }

    private static string GenerateDefaultNotebook(string name)
    {
        var notebook = new
        {
            nbformat = 4,
            nbformat_minor = 5,
            metadata = new
            {
                kernelspec = new
                {
                    display_name = "Python (Pyodide)",
                    language = "python",
                    name = "python"
                },
                language_info = new
                {
                    name = "python",
                    version = "3.11"
                }
            },
            cells = new object[]
            {
                new
                {
                    cell_type = "markdown",
                    metadata = new { },
                    source = new[] { $"# {name}\n", "\n", "Research notebook powered by Prism AI Workbench." }
                },
                new
                {
                    cell_type = "code",
                    metadata = new { },
                    source = new[] { "# Import the Prism workbench helper\n", "# import workbench\n", "# workbench.chat('model-name', 'Hello!')" },
                    outputs = Array.Empty<object>(),
                    execution_count = (int?)null
                }
            }
        };

        return JsonSerializer.Serialize(notebook, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
