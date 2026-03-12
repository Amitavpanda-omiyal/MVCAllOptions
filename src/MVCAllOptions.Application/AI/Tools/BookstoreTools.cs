using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MVCAllOptions.AI.Data;

namespace MVCAllOptions.AI.Tools;

/// <summary>
/// AIFunction tools that allow agents to query the bookstore catalogue.
/// Each method is decorated with [Description] so the LLM can understand its purpose.
/// </summary>
public static class BookstoreTools
{
    [Description("Search the book catalogue by a keyword in the title. Returns a JSON array of matching books.")]
    public static string SearchBooks(
        [Description("The keyword to search for in book titles")] string keyword)
    {
        var results = BookstoreData.SearchByName(keyword).ToList();
        return results.Count == 0
            ? $"No books found matching '{keyword}'."
            : FormatBookList(results);
    }

    [Description("Get all books in a specific genre. Available genres: Adventure, Biography, Dystopia, Horror, Science, ScienceFiction.")]
    public static string GetBooksByGenre(
        [Description("The genre name (Adventure, Biography, Dystopia, Horror, Science, ScienceFiction)")] string genre)
    {
        var results = BookstoreData.GetByGenre(genre).ToList();
        return results.Count == 0
            ? $"No books found in genre '{genre}'. Available genres: {string.Join(", ", BookstoreData.GetAllGenres())}"
            : FormatBookList(results);
    }

    [Description("Get detailed information about a specific book by its exact title.")]
    public static string GetBookDetails(
        [Description("The exact title of the book")] string title)
    {
        var book = BookstoreData.GetByNameExact(title);
        return book is null
            ? $"Book '{title}' not found. Try searching with SearchBooks first."
            : $"""
               Title: {book.Name}
               Author: {book.Author}
               Genre: {book.Genre}
               Published: {book.PublishDate:MMMM dd, yyyy}
               Price: ${book.Price:F2}
               
               Description: {book.Description}
               """;
    }

    [Description("Get all available books in the catalogue. Use this to browse the full inventory.")]
    public static string GetAllBooks() =>
        FormatBookList(BookstoreData.Catalogue);

    [Description("Find books within a price range.")]
    public static string GetBooksByPriceRange(
        [Description("Minimum price in USD")] float minPrice,
        [Description("Maximum price in USD")] float maxPrice)
    {
        var results = BookstoreData.GetByPriceRange(minPrice, maxPrice).ToList();
        return results.Count == 0
            ? $"No books found between ${minPrice:F2} and ${maxPrice:F2}."
            : FormatBookList(results);
    }

    [Description("List all available genres in the bookstore.")]
    public static string ListGenres() =>
        $"Available genres: {string.Join(", ", BookstoreData.GetAllGenres())}";

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string FormatBookList(IEnumerable<BookRecord> books) =>
        string.Join("\n---\n", books.Select(b =>
            $"• {b.Name} by {b.Author} | Genre: {b.Genre} | ${b.Price:F2} | {b.PublishDate.Year}"));
}
