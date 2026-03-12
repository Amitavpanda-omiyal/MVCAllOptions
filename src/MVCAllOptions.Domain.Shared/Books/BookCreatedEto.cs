using System;
using Volo.Abp.EventBus;

namespace MVCAllOptions.Books;

/// <summary>
/// Distributed Event Transfer Object published after a new Book is created.
/// Consumed by BookCreatedEventHandler (Application layer) which triggers
/// the MAF Book Enrichment Workflow in the AgentWorkflows service.
///
/// Lives in Domain.Shared so any module or microservice can reference it
/// without taking a dependency on Application or Domain layers.
/// </summary>
[EventName("MVCAllOptions.Books.BookCreated")]
public class BookCreatedEto
{
    public Guid   BookId      { get; set; }
    public string Name        { get; set; } = string.Empty;
    public string Type        { get; set; } = string.Empty;
    public float  Price       { get; set; }
    public string PublishDate { get; set; } = string.Empty;
}
